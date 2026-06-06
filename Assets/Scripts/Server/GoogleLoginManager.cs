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
            BackendManager.Instance.GoogleFederationLoginAsync(
                serverAuthCode,
                FederationType.GPGS2,
                onSuccess: (bro) =>
                {
                    Debug.Log("[GoogleLoginManager] 뒤끝 GPG 연동 로그인 성공");
                    if (onFail != null)
                        OnGoogleAutoLoginSuccessHandler?.Invoke();  // 자동 로그인 경로
                    else
                        OnGoogleLoginSuccessHandler?.Invoke();       // 수동 로그인 경로
                },
                onFail: (state) =>
                {
                    Debug.LogError("[GoogleLoginManager] 뒤끝 GPG 연동 로그인 실패: " + state);
                    if (onFail != null)
                        onFail.Invoke();
                    else
                        OnGoogleLoginFailedHandler?.Invoke();
                }
            );
        }

        /// <summary>GPG 로그아웃 및 뒤끝 로그아웃을 수행합니다.</summary>
        public void SignOut()
        {
            BackendManager.Instance.LogOut();
            Debug.Log("[GoogleLoginManager] 로그아웃 완료");
        }
    }
}

