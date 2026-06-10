using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Muks.BackEnd
{
    /// <summary>Google Play Games Services(GPGS2) 기반 로그인 및 뒤끝 연동 매니저</summary>
    public class GoogleLoginManager : MonoBehaviour
    {
        public static event Action OnGoogleLoginSuccessHandler;
        public static event Action OnGoogleAutoLoginSuccessHandler;
        public static event Action OnGoogleLoginFailedHandler;

        public enum LoginPreference { None = 0, Google = 1, Guest = 2 }

        private const string LOGIN_PREF_KEY = "LoginPreference";

        public static LoginPreference GetLoginPreference()
        {
            return (LoginPreference)PlayerPrefs.GetInt(LOGIN_PREF_KEY, (int)LoginPreference.None);
        }

        public static void SetLoginPreference(LoginPreference preference)
        {
            PlayerPrefs.SetInt(LOGIN_PREF_KEY, (int)preference);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
#if UNITY_ANDROID
            PlayGamesPlatform.Activate();
            Debug.Log("[GoogleLoginManager] Google Play Games 플랫폼 활성화 완료");
#endif
        }

        /// <summary>
        /// 앱 시작 시 뒤끝 토큰 자동 로그인을 먼저 시도합니다.
        /// 실패하면 GPG 자동 로그인(Silent Sign-In)을 시도하고,
        /// 그마저도 실패하면 onFail 콜백을 호출합니다(로그인 버튼 표시 등).
        /// </summary>
        public void TryAutoLogin(Action onFail = null)
        {
            // 1단계: 뒤끝 저장 토큰으로 자동 로그인
            BackendManager.Instance.TokenLoginAsync(
                onSuccess: (bro) =>
                {
                    Debug.Log("[GoogleLoginManager] 뒤끝 토큰 자동 로그인 성공");
                    OnGoogleAutoLoginSuccessHandler?.Invoke();
                },
                onFail: (state) =>
                {
                    Debug.Log("[GoogleLoginManager] 뒤끝 토큰 자동 로그인 실패, GPG 자동 로그인 시도");
                    // 2단계: GPG Silent Sign-In 시도
                    TryGPGSilentLogin(onFail);
                }
            );
        }

        /// <summary>뒤끝 토큰 로그인만 시도합니다 (GPG 없이). 게스트 사용자 재로그인에 사용합니다.</summary>
        public void TryTokenLogin(Action onSuccess, Action onFail = null)
        {
            BackendManager.Instance.TokenLoginAsync(
                onSuccess: (bro) =>
                {
                    Debug.Log("[GoogleLoginManager] 뒤끝 토큰 자동 로그인 성공");
                    onSuccess?.Invoke();
                },
                onFail: (state) =>
                {
                    Debug.Log("[GoogleLoginManager] 뒤끝 토큰 자동 로그인 실패");
                    onFail?.Invoke();
                }
            );
        }

        private void TryGPGSilentLogin(Action onFail)
        {
#if UNITY_ANDROID
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status == SignInStatus.Success)
                {
                    Debug.Log("[GoogleLoginManager] GPG 자동 로그인 성공, 서버 인증 코드 요청");
                    RequestServerAuthCodeAndLogin(onFail);
                }
                else
                {
                    Debug.Log("[GoogleLoginManager] GPG 자동 로그인 실패: " + status + " → 로그인 버튼 표시");
                    onFail?.Invoke();
                }
            });
#else
            Debug.LogWarning("[GoogleLoginManager] GPG는 Android 전용입니다.");
            onFail?.Invoke();
#endif
        }

        /// <summary>구글 로그인 버튼 클릭 시 호출합니다.</summary>
        public void OnClickGoogleLogin()
        {
#if UNITY_ANDROID
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                if (status == SignInStatus.Success)
                {
                    Debug.Log("[GoogleLoginManager] GPG 수동 로그인 성공, 서버 인증 코드 요청");
                    RequestServerAuthCodeAndLogin(null);
                }
                else
                {
                    Debug.LogError("[GoogleLoginManager] GPG 수동 로그인 실패: " + status);
                    OnGoogleLoginFailedHandler?.Invoke();
                }
            });
#else
            Debug.LogError("[GoogleLoginManager] GPG는 Android 전용입니다.");
            OnGoogleLoginFailedHandler?.Invoke();
#endif
        }

        private void RequestServerAuthCodeAndLogin(Action onFail)
        {
#if UNITY_ANDROID
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                /* forceRefreshToken= */ false,
                authCode =>
                {
                    if (string.IsNullOrEmpty(authCode))
                    {
                        Debug.LogError("[GoogleLoginManager] 서버 인증 코드가 비어 있습니다. WebClientId 설정을 확인하세요.");
                        if (onFail != null)
                            onFail.Invoke();
                        else
                            OnGoogleLoginFailedHandler?.Invoke();
                        return;
                    }

                    Debug.Log($"[GoogleLoginManager] authCode 획득 성공 (길이:{authCode.Length})");
                    LoginBackendWithGPG(authCode, onFail);
                }
            );
#endif
        }

        private void LoginBackendWithGPG(string serverAuthCode, Action onFail)
        {
            // 1단계: authCode → GPGS2 AccessToken
            Backend.BMember.GetGPGS2AccessToken(serverAuthCode, tokenBro =>
            {
                if (!tokenBro.IsSuccess())
                {
                    Debug.LogError("[GoogleLoginManager] GPGS2 AccessToken 획득 실패: " + tokenBro.GetMessage());
                    if (onFail != null)
                        onFail.Invoke();
                    else
                        OnGoogleLoginFailedHandler?.Invoke();
                    return;
                }

                string accessToken = tokenBro.GetReturnValuetoJSON()["access_token"].ToString();
                Debug.Log("[GoogleLoginManager] GPGS2 AccessToken 획득 성공, 뒤끝 페더레이션 로그인 시도");

                // 2단계: AccessToken → AuthorizeFederation(GPGS2)
                Backend.BMember.AuthorizeFederation(accessToken, FederationType.GPGS2, bro =>
                {
                    if (bro.IsSuccess())
                    {
                        BackendManager.Instance.NotifyFederationLoginSuccess();
                        Debug.Log($"[GoogleLoginManager] 뒤끝 GPGS2 페더레이션 로그인 성공 (statusCode: {bro.GetStatusCode()})");
                        if (onFail != null)
                            OnGoogleAutoLoginSuccessHandler?.Invoke();  // 자동 로그인 경로
                        else
                            OnGoogleLoginSuccessHandler?.Invoke();       // 수동 로그인 경로
                    }
                    else
                    {
                        Debug.LogError("[GoogleLoginManager] 뒤끝 GPGS2 페더레이션 로그인 실패: " + bro.GetMessage());
                        if (onFail != null)
                            onFail.Invoke();
                        else
                            OnGoogleLoginFailedHandler?.Invoke();
                    }
                });
            });
        }

        /// <summary>GPG 로그아웃 및 뒤끝 로그아웃을 수행합니다.</summary>
        public void SignOut()
        {
            BackendManager.Instance.LogOut();
            Debug.Log("[GoogleLoginManager] 로그아웃 완료");
        }

        /// <summary>
        /// 뒤끝 로컬 토큰을 포함한 세션을 완전히 초기화합니다.
        /// 다른 구글 계정으로 전환하기 위해 앱을 종료하기 전에 호출하면,
        /// 재시작 시 TokenLoginAsync가 실패하여 GPGS 자동 로그인이 수행됩니다.
        /// </summary>
        public void ClearSessionForSwitch()
        {
            Backend.BMember.Logout();
            BackendManager.Instance.LogOut();
            Debug.Log("[GoogleLoginManager] 세션 클리어 완료 — 재시작 시 GPGS 자동 로그인 예정");
        }

        // ──────────────────────────────────────────────────────────────────
        // 구글 연동 (설정 화면에서 사용)
        // ──────────────────────────────────────────────────────────────────

        private string _pendingAccessToken;
        private string _pendingDisplayName;

        private const string GOOGLE_DISPLAY_NAME_KEY = "GoogleDisplayName";

        public static string GetLinkedGoogleDisplayName()
        {
            return PlayerPrefs.GetString(GOOGLE_DISPLAY_NAME_KEY, string.Empty);
        }

        private static void SaveLinkedGoogleDisplayName(string displayName)
        {
            PlayerPrefs.SetString(GOOGLE_DISPLAY_NAME_KEY, displayName);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 설정 화면에서 구글 연동 버튼 클릭 시 호출합니다.
        /// GPGS 인증 → GPGS2 AccessToken 획득 → AuthorizeFederation 결과에 따라 콜백 호출.
        ///   onNewLink             : 연동된 계정 없음 (신규 연동 완료 상태 — 취소 시 CancelNewLink 호출)
        ///   onExistingOtherAccount: 이 구글 ID가 다른 백엔드 계정에 연동됨 (전환 여부 확인 필요)
        ///   onAlreadyLinked       : 이미 현재 계정에 연동되어 있음
        ///   onFail                : 인증 오류 또는 네트워크 오류
        /// </summary>
        public void LinkGoogleAccount(
            Action onNewLink,
            Action onExistingOtherAccount,
            Action onAlreadyLinked,
            Action onFail)
        {
#if UNITY_ANDROID
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                if (status != SignInStatus.Success)
                {
                    Debug.LogError("[GoogleLoginManager] 구글 연동: GPGS 인증 실패 " + status);
                    onFail?.Invoke();
                    return;
                }

                PlayGamesPlatform.Instance.RequestServerSideAccess(false, authCode =>
                {
                    if (string.IsNullOrEmpty(authCode))
                    {
                        Debug.LogError("[GoogleLoginManager] 구글 연동: 서버 인증 코드 획득 실패");
                        onFail?.Invoke();
                        return;
                    }

                    _pendingDisplayName = PlayGamesPlatform.Instance.localUser.userName;

                    Backend.BMember.GetGPGS2AccessToken(authCode, tokenBro =>
                    {
                        if (!tokenBro.IsSuccess())
                        {
                            Debug.LogError("[GoogleLoginManager] 구글 연동: GPGS2 AccessToken 획득 실패 " + tokenBro.GetMessage());
                            onFail?.Invoke();
                            return;
                        }

                        string accessToken = tokenBro.GetReturnValuetoJSON()["access_token"].ToString();
                        _pendingAccessToken = accessToken;

                        Backend.BMember.AuthorizeFederation(accessToken, FederationType.GPGS2, bro =>
                        {
                            if (bro.IsSuccess())
                            {
                                if (bro.GetStatusCode() == "201")
                                {
                                    Debug.Log("[GoogleLoginManager] 구글 연동: 신규 연동 완료 (201)");
                                    onNewLink?.Invoke();
                                }
                                else
                                {
                                    Debug.Log("[GoogleLoginManager] 구글 연동: 이미 현재 계정에 연동됨 (200)");
                                    _pendingAccessToken = null;
                                    onAlreadyLinked?.Invoke();
                                }
                            }
                            else
                            {
                                // 다른 계정에 이미 연동된 구글 ID
                                Debug.Log("[GoogleLoginManager] 구글 연동: 다른 계정에 연동된 구글 ID — " + bro.GetMessage());
                                onExistingOtherAccount?.Invoke();
                            }
                        });
                    });
                });
            });
#else
            Debug.LogWarning("[GoogleLoginManager] 구글 연동은 Android 전용입니다.");
            onFail?.Invoke();
#endif
        }

        /// <summary>신규 연동 확인 — 연동 상태 유지 및 선호도 저장.</summary>
        public void ConfirmNewLink()
        {
            SetLoginPreference(LoginPreference.Google);
            SaveLinkedGoogleDisplayName(_pendingDisplayName ?? string.Empty);
            _pendingAccessToken = null;
            _pendingDisplayName = null;
            Debug.Log("[GoogleLoginManager] 구글 연동 확정");
        }

        /// <summary>신규 연동 취소 — 선호도를 저장하지 않고 종료합니다.</summary>
        public void CancelNewLink()
        {
            _pendingAccessToken = null;
            Debug.Log("[GoogleLoginManager] 구글 연동 취소");
        }

        /// <summary>
        /// 연동된 다른 구글 계정으로 전환합니다.
        /// 현재 세션을 로그아웃하고 구글 연동 계정으로 재로그인합니다.
        /// </summary>
        public void SwitchToLinkedAccount(Action onSuccess, Action onFail)
        {
            string token = _pendingAccessToken;
            _pendingAccessToken = null;

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("[GoogleLoginManager] SwitchToLinkedAccount: pendingAccessToken이 없습니다.");
                onFail?.Invoke();
                return;
            }

            BackendManager.Instance.LogOut();
            BackendManager.Instance.FederationLoginWithAccessTokenAsync(
                token,
                FederationType.GPGS2,
                onSuccess: (bro) =>
                {
                    SetLoginPreference(LoginPreference.Google);
                    SaveLinkedGoogleDisplayName(_pendingDisplayName ?? string.Empty);
                    _pendingDisplayName = null;
                    Debug.Log("[GoogleLoginManager] 구글 연동 계정 전환 성공");
                    onSuccess?.Invoke();
                },
                onFail: (state) =>
                {
                    Debug.LogError("[GoogleLoginManager] 구글 연동 계정 전환 실패: " + state);
                    onFail?.Invoke();
                }
            );
        }
    }
}

