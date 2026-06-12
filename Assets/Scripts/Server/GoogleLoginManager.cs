using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;

namespace Muks.BackEnd
{
    /// <summary>뒤끝 Toolkit GoogleLogin 기반 로그인 및 연동 매니저</summary>
    public class GoogleLoginManager : MonoBehaviour
    {
        public static event Action OnGoogleLoginSuccessHandler;
        public static event Action OnGoogleAutoLoginSuccessHandler;
        public static event Action OnGoogleLoginFailedHandler;

        public enum LoginPreference { None = 0, Google = 1, Guest = 2 }

        private const string LOGIN_PREF_KEY = "LoginPreference";
        private const string GOOGLE_DISPLAY_NAME_KEY = "GoogleDisplayName";

        private string _pendingToken;
        private string _pendingDisplayName;
        private bool _isGoogleLoginInProgress;

        // LinkGoogleAccount 시작 시점의 현재 계정 indate를 저장합니다.
        // 연동 시도 중 세션이 바뀌더라도 원래 계정과의 비교에 사용합니다.
        // ConfirmNewLink / SwitchToLinkedAccount / SignOutGoogle 시 null로 초기화됩니다.
        private string _originalUserIndate;

        public static LoginPreference GetLoginPreference()
        {
            return (LoginPreference)PlayerPrefs.GetInt(LOGIN_PREF_KEY, (int)LoginPreference.None);
        }

        public static void SetLoginPreference(LoginPreference preference)
        {
            PlayerPrefs.SetInt(LOGIN_PREF_KEY, (int)preference);
            PlayerPrefs.Save();
        }

        public static string GetLinkedGoogleDisplayName()
        {
            return PlayerPrefs.GetString(GOOGLE_DISPLAY_NAME_KEY, string.Empty);
        }

        private static void SaveLinkedGoogleDisplayName(string displayName)
        {
            PlayerPrefs.SetString(GOOGLE_DISPLAY_NAME_KEY, displayName);
            PlayerPrefs.Save();
        }

        // ──────────────────────────────────────────────────────────────────
        // 자동 로그인
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 앱 시작 시 뒤끝 토큰 자동 로그인을 먼저 시도합니다.
        /// 실패하면 구글 로그인(Toolkit)을 시도하고,
        /// 그마저도 실패하면 onFail 콜백을 호출합니다.
        /// </summary>
        public void TryAutoLogin(Action onFail = null)
        {
            BackendManager.Instance.TokenLoginAsync(
                onSuccess: (bro) =>
                {
                    Debug.Log("[GoogleLoginManager] 뒤끝 토큰 자동 로그인 성공");
                    OnGoogleAutoLoginSuccessHandler?.Invoke();
                },
                onFail: (state) =>
                {
                    // 사용자가 명시적으로 로그아웃한 경우(LoginPreference.None) 구글 자동 로그인을 시도하지 않습니다.
                    // 이를 통해 로그아웃 후 이전 계정으로 자동 복귀되는 버그를 방지합니다.
                    if (GetLoginPreference() == LoginPreference.None)
                    {
                        Debug.Log("[GoogleLoginManager] 로그아웃 상태 — 구글 자동 로그인 건너뜀");
                        onFail?.Invoke();
                        return;
                    }

                    Debug.Log("[GoogleLoginManager] 뒤끝 토큰 자동 로그인 실패, 구글 로그인 시도");
                    TryGoogleAutoLogin(onFail);
                }
            );
        }

        private void TryGoogleAutoLogin(Action onFail)
        {
#if UNITY_ANDROID
            if (_isGoogleLoginInProgress) { onFail?.Invoke(); return; }
            _isGoogleLoginInProgress = true;
            TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin((isSuccess, errorMessage, token) =>
            {
                _isGoogleLoginInProgress = false;
                if (!isSuccess)
                {
                    Debug.Log("[GoogleLoginManager] 구글 자동 로그인 실패: " + errorMessage);
                    onFail?.Invoke();
                    return;
                }
                LoginBackendWithGoogleToken(token, isAuto: true, onFail: onFail);
            });
#else
            Debug.LogWarning("[GoogleLoginManager] 구글 로그인은 Android 전용입니다.");
            onFail?.Invoke();
#endif
        }

        /// <summary>뒤끝 토큰 로그인만 시도합니다. 게스트 사용자 재로그인에 사용합니다.</summary>
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

        // ──────────────────────────────────────────────────────────────────
        // 수동 로그인 / 로그아웃
        // ──────────────────────────────────────────────────────────────────

        /// <summary>구글 로그인 버튼 클릭 시 호출합니다.</summary>
        public void OnClickGoogleLogin()
        {
#if UNITY_ANDROID
            if (_isGoogleLoginInProgress) return;
            _isGoogleLoginInProgress = true;
            TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin((isSuccess, errorMessage, token) =>
            {
                _isGoogleLoginInProgress = false;
                if (!isSuccess)
                {
                    Debug.LogError("[GoogleLoginManager] 구글 로그인 실패: " + errorMessage);
                    OnGoogleLoginFailedHandler?.Invoke();
                    return;
                }
                LoginBackendWithGoogleToken(token, isAuto: false, onFail: null);
            });
#else
            Debug.LogError("[GoogleLoginManager] 구글 로그인은 Android 전용입니다.");
            OnGoogleLoginFailedHandler?.Invoke();
#endif
        }

        private void LoginBackendWithGoogleToken(string token, bool isAuto, Action onFail)
        {
            Backend.BMember.AuthorizeFederation(token, FederationType.Google, bro =>
            {
                if (bro.IsSuccess())
                {
                    BackendManager.Instance.NotifyFederationLoginSuccess();
                    Debug.Log($"[GoogleLoginManager] 뒤끝 구글 페더레이션 로그인 성공 (statusCode: {bro.GetStatusCode()})");
                    if (isAuto)
                        OnGoogleAutoLoginSuccessHandler?.Invoke();
                    else
                        OnGoogleLoginSuccessHandler?.Invoke();
                }
                else
                {
                    Debug.LogError("[GoogleLoginManager] 뒤끝 구글 페더레이션 로그인 실패: " + bro.GetMessage());
                    if (onFail != null)
                        onFail.Invoke();
                    else
                        OnGoogleLoginFailedHandler?.Invoke();
                }
            });
        }

        /// <summary>
        /// 구글 계정 로그아웃 후 뒤끝 세션도 초기화합니다.
        /// 이후 로그인 시 계정 선택 화면이 표시됩니다.
        /// </summary>
        public void SignOutGoogle(Action onSuccess = null, Action onFail = null)
        {
#if UNITY_ANDROID
            TheBackend.ToolKit.GoogleLogin.Android.GoogleSignOut((isSuccess, errorMessage) =>
            {
                if (!isSuccess)
                {
                    Debug.LogError("[GoogleLoginManager] 구글 로그아웃 실패: " + errorMessage);
                    onFail?.Invoke();
                    return;
                }
                Backend.BMember.Logout();
                BackendManager.Instance.LogOut();
                SetLoginPreference(LoginPreference.None);
                SaveLinkedGoogleDisplayName(string.Empty);
                _originalUserIndate = null;
                Debug.Log("[GoogleLoginManager] 구글 로그아웃 완료");
                onSuccess?.Invoke();
            });
#else
            Backend.BMember.Logout();
            BackendManager.Instance.LogOut();
            SetLoginPreference(LoginPreference.None);
            SaveLinkedGoogleDisplayName(string.Empty);
            _originalUserIndate = null;
            onSuccess?.Invoke();
#endif
        }

        /// <summary>뒤끝 세션만 초기화합니다.</summary>
        public void SignOut()
        {
            BackendManager.Instance.LogOut();
            Debug.Log("[GoogleLoginManager] 로그아웃 완료");
        }

        /// <summary>세션을 완전히 초기화합니다. 다른 계정으로 전환 전에 호출합니다.</summary>
        public void ClearSessionForSwitch()
        {
            Backend.BMember.Logout();
            BackendManager.Instance.LogOut();
            Debug.Log("[GoogleLoginManager] 세션 클리어 완료");
        }

        // ──────────────────────────────────────────────────────────────────
        // 구글 연동 (설정 화면에서 사용)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 설정 화면에서 구글 연동 버튼 클릭 시 호출합니다.
        /// 구글 로그인 → AuthorizeFederation 결과에 따라 콜백 호출.
        ///   onNewLink             : 이 구글 계정에 연동된 뒤끝 ID 없음 (201)
        ///   onExistingOtherAccount: 이 구글 계정이 다른 뒤끝 계정에 연동됨
        ///   onAlreadyLinked       : 현재 계정에 이미 연동됨 (200)
        ///   onFail                : 인증 오류 또는 네트워크 오류
        /// </summary>
        public void LinkGoogleAccount(
            Action onNewLink,
            Action onExistingOtherAccount,
            Action onAlreadyLinked,
            Action onFail)
        {
#if UNITY_ANDROID
            if (_isGoogleLoginInProgress)
            {
                Debug.LogWarning("[GoogleLoginManager] 구글 연동: 이미 진행 중입니다.");
                return;
            }
            _isGoogleLoginInProgress = true;
            TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin((isSuccess, errorMessage, token) =>
            {
                _isGoogleLoginInProgress = false;
                if (!isSuccess)
                {
                    Debug.LogError("[GoogleLoginManager] 구글 연동: 구글 로그인 실패 — " + errorMessage);
                    onFail?.Invoke();
                    return;
                }

                _pendingToken = token;
                _pendingDisplayName = string.Empty;

                // AuthorizeFederation 응답 의미:
                //   201 → 이 구글 계정을 현재 계정에 신규 연동
                //   200 → 이 구글 계정에 연동된 뒤끝 계정으로 로그인됨
                //          (같은 계정이면 이미 연동, 다른 계정이면 전환됨)
                //   Error → 현재 계정이 이미 다른 구글 계정에 연동되어 있음
                //
                // _originalUserIndate: 첫 연동 시도 시점의 원래 계정 indate.
                // CancelExistingAccountSwitch 후 세션이 완전히 복원되지 않은 경우에도
                // 올바른 비교가 가능하도록 null일 때만 초기화합니다.
                if (_originalUserIndate == null)
                    _originalUserIndate = Backend.UserInDate;
                string indateBefore = _originalUserIndate;
                Backend.BMember.AuthorizeFederation(token, FederationType.Google, bro =>
                {
                    if (bro.IsSuccess())
                    {
                        if (bro.GetStatusCode() == "201")
                        {
                            // 신규 연동 — 구글 계정 식별자(이메일)를 displayName으로 저장
                            Backend.BMember.GetUserInfo(userBro =>
                            {
                                if (userBro.IsSuccess())
                                {
                                    var row = userBro.GetReturnValuetoJSON()?["row"];
                                    string fedId = row?["federationId"]?.ToString();
                                    _pendingDisplayName = !string.IsNullOrEmpty(fedId) ? fedId : Backend.UserNickName;
                                }
                                else
                                {
                                    _pendingDisplayName = Backend.UserNickName;
                                }
                                Debug.Log($"[GoogleLoginManager] 구글 연동: 신규 연동 완료 (201) displayName={_pendingDisplayName}");
                                onNewLink?.Invoke();
                            });
                            return; // GetUserInfo 콜백에서 onNewLink 호출
                        }
                        else
                        {
                            // 200: 이 구글 계정에 연동된 계정으로 로그인됨
                            // → 연동 전후 indate 비교로 계정이 바뀌었는지 판단
                            string indateAfter = Backend.UserInDate;
                            if (indateBefore == indateAfter)
                            {
                                // 같은 계정 → 현재 계정에 이미 연동된 구글 계정
                                Debug.Log("[GoogleLoginManager] 구글 연동: 이미 현재 계정에 연동됨 (200)");
                                _pendingToken = null;
                                onAlreadyLinked?.Invoke();
                            }
                            else
                            {
                                // 다른 계정으로 전환됨 → 계정 전환 여부 확인 팝업
                                Debug.Log($"[GoogleLoginManager] 구글 연동: 다른 계정으로 전환됨 (200) {indateBefore} → {indateAfter}");
                                onExistingOtherAccount?.Invoke();
                            }
                        }
                    }
                    else
                    {
                        // 에러: 현재 계정이 이미 다른 구글 계정에 연동됨
                        Debug.Log("[GoogleLoginManager] 구글 연동: 에러 — " + bro.GetMessage());
                        _pendingToken = null;
                        onFail?.Invoke();
                    }
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
            _pendingToken = null;
            _pendingDisplayName = null;
            _originalUserIndate = null;
            Debug.Log("[GoogleLoginManager] 구글 연동 확정");
        }

        /// <summary>신규 연동 취소.</summary>
        public void CancelNewLink()
        {
            _pendingToken = null;
            _originalUserIndate = null;
            Debug.Log("[GoogleLoginManager] 구글 연동 취소");
        }

        /// <summary>
        /// 다른 계정으로의 전환을 취소하고 원래 세션을 복구합니다.
        /// AuthorizeFederation(200) 후 아니오를 선택했을 때 호출합니다.
        /// Backend.BMember.Logout()을 호출하지 않아 디스크 토큰(원래 계정)을 보존한 뒤
        /// TokenLogin으로 원래 계정 세션을 복구합니다.
        /// </summary>
        public void CancelExistingAccountSwitch(Action onSuccess = null, Action onFail = null)
        {
            _pendingToken = null;
            // Backend.BMember.Logout()을 호출하지 않아 디스크 토큰(원래 계정 A)을 보존합니다.
            BackendManager.Instance.LogOut(); // in-memory 상태만 리셋
            BackendManager.Instance.TokenLoginAsync(
                onSuccess: (bro) =>
                {
                    Debug.Log("[GoogleLoginManager] 계정 전환 취소, 원래 계정 복구 성공");
                    onSuccess?.Invoke();
                },
                onFail: (state) =>
                {
                    Debug.LogError("[GoogleLoginManager] 계정 전환 취소, 원래 계정 복구 실패: " + state);
                    onFail?.Invoke();
                }
            );
        }

        /// <summary>연동된 다른 구글 계정으로 전환합니다.</summary>
        public void SwitchToLinkedAccount(Action onSuccess, Action onFail)
        {
            string token = _pendingToken;
            _pendingToken = null;
            _originalUserIndate = null; // 계정 전환 확정 — 스냅샷 초기화

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("[GoogleLoginManager] SwitchToLinkedAccount: pendingToken이 없습니다.");
                onFail?.Invoke();
                return;
            }

            // Backend.BMember.Logout()으로 backend.dat 토큰 파일을 초기화합니다.
            // 이 과정 없이는 재시작 시 기존 A계정 토큰으로 로그인됩니다.
            Backend.BMember.Logout();
            BackendManager.Instance.LogOut();

            BackendManager.Instance.FederationLoginWithAccessTokenAsync(
                token,
                FederationType.Google,
                onSuccess: (bro) =>
                {
                    SetLoginPreference(LoginPreference.Google);
                    SaveLinkedGoogleDisplayName(_pendingDisplayName ?? string.Empty);
                    _pendingDisplayName = null;
                    // B계정 로그인 성공 후 즉시 저장을 비활성화합니다.
                    // Application.Quit() 시 OnApplicationQuit이 A계정 데이터를 B계정에 덮어쓰는 것을 방지합니다.
                    BackendManager.Instance.LogOut();
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

