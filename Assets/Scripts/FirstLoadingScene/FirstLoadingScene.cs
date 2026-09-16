using BackEnd;
using Muks.BackEnd;
using Muks.PathFinding;
using Muks.Tween;
using System;
using System.Threading;
using UnityEngine;

public class FirstLoadingScene : MonoBehaviour
{
    private const float PaymentDataLoadTimeoutSeconds = 30f;

    [SerializeField] private UIFirstLoadingScene _uiFirstLoadingScene;
    [SerializeField] private GoogleLoginManager _googleLoginManager;

    private int _loadAttemptId;
    private int _loginCompletionVersion;
    private bool _sceneTransitionStarted;

    private void Start()
    {
        _uiFirstLoadingScene.Init();

#if UNITY_ANDROID
        GoogleLoginManager.OnGoogleLoginSuccessHandler += OnLoginCompleted;
        GoogleLoginManager.OnGoogleAutoLoginSuccessHandler += OnLoginCompleted;
        GoogleLoginManager.OnGoogleLoginFailedHandler += OnGoogleLoginFailed;
#endif

        Tween.Wait(0.2f, StartLoadDataAsync);
    }

    private void OnDestroy()
    {
        ++_loadAttemptId;
        PaymentInfo.CancelPendingLoad();

#if UNITY_ANDROID
        GoogleLoginManager.OnGoogleLoginSuccessHandler -= OnLoginCompleted;
        GoogleLoginManager.OnGoogleAutoLoginSuccessHandler -= OnLoginCompleted;
        GoogleLoginManager.OnGoogleLoginFailedHandler -= OnGoogleLoginFailed;
#endif
    }

    private void StartLoadDataAsync()
    {
        int loadAttemptId = ++_loadAttemptId;
        _sceneTransitionStarted = false;
        PaymentInfo.CancelPendingLoad();

        _uiFirstLoadingScene.ShowTitle(() =>
        {
            if (!IsCurrentLoadAttempt(loadAttemptId))
                return;

            Backend.Utils.GetServerStatus((callback) =>
            {
                if (!IsCurrentLoadAttempt(loadAttemptId))
                    return;

                if (callback.IsSuccess())
                {
                    int serverStatus = (int)callback.GetReturnValuetoJSON()["serverStatus"];
                    if (serverStatus == 0)
                    {
                        Debug.Log("서버 상태가 정상입니다. 데이터 로드를 시작합니다.");
                    }
                    else if (serverStatus == 1)
                    {
                        Debug.LogWarning("서버 상태 오프라인");
                        BackendManager.Instance.ShowPopup("서버 오프라인", "서버에 접속하지 못했습니다.\n잠시 후 다시 시도해주세요.");
                        BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                        BackendManager.Instance.ShowPopupExitButton();
                        return;
                    }
                    else if (serverStatus == 2)
                    {
                        Debug.LogWarning("서버 상태가 점검 중입니다. 잠시 후 다시 시도해주세요.");
                        BackendManager.Instance.ShowPopup("점검중", "현재 점검 중입니다.\n잠시 후 다시 시도해주세요.");
                        BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                        BackendManager.Instance.ShowPopupExitButton();
                        return;
                    }
                }
                else
                {
                    Debug.LogError("서버 상태 조회 실패: " + callback.GetErrorMessage());
                    BackendManager.Instance.ShowPopup("서버 오류", "서버 상태를 확인할 수 없습니다.\n잠시 후 다시 시도해주세요.");
                    BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                    BackendManager.Instance.ShowPopupExitButton();
                    return;
                }


                StartLoginFlow(loadAttemptId);
            });
        });
    }

    private void StartLoginFlow(int loadAttemptId)
    {
#if UNITY_ANDROID
        var pref = GoogleLoginManager.GetLoginPreference();
        if (pref == GoogleLoginManager.LoginPreference.Google)
        {
            // Google 선호: 자동 로그인 시도, 실패 시 게스트
            _googleLoginManager.TryAutoLogin(onFail: () => DoGuestLogin(loadAttemptId));
        }
        else
        {
            // 게스트 또는 최초 실행: 토큰 재로그인 시도, 실패 시 게스트
            _googleLoginManager.TryTokenLogin(
                onSuccess: () => OnLoginCompleted(loadAttemptId),
                onFail: () => DoGuestLogin(loadAttemptId)
            );
        }
#else
        DoGuestLogin(loadAttemptId);
#endif
    }

    private void DoGuestLogin(int loadAttemptId)
    {
        if (!IsCurrentLoadAttempt(loadAttemptId))
            return;

        BackendManager.Instance.GuestLoginAsync(
            onSuccess: (bro) => OnLoginCompleted(loadAttemptId),
            onFail: (state) =>
            {
                if (!IsCurrentLoadAttempt(loadAttemptId))
                    return;

                Debug.LogError("[FirstLoadingScene] 게스트 로그인 실패: " + state);
                BackendManager.Instance.ShowPopup("로그인 실패", "게스트 로그인에 실패했습니다.\n다시 시도해주세요.");
                BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                BackendManager.Instance.ShowPopupExitButton();
            }
        );
    }

    private void OnLoginCompleted()
    {
        OnLoginCompleted(_loadAttemptId);
    }

    private void OnLoginCompleted(int loadAttemptId)
    {
        if (!IsCurrentLoadAttempt(loadAttemptId))
            return;

        int loginCompletionVersion = ++_loginCompletionVersion;

        // UUID(gamerId) 조회 - 실패해도 게임 진행
        BackendManager.Instance.FetchGamerIdAsync();

        using (new VersionManagement())
        {
            if (!new VersionManagement().UpdateCheck())
                return;
        }

        BackendManager.Instance.GetMyDataAsync("GameData", (bro) => MainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion))
                return;

            UserInfo.LoadGameData(bro);
            UserInfo.LoadStageDataAsync();
            LoadPaymentDataAsync(loadAttemptId, loginCompletionVersion);
        }), (state) => MainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion))
                return;

            ShowDataLoadFailure(loadAttemptId, "게임 데이터 로드 실패: " + state);
        }));
    }

    private void LoadPaymentDataAsync(int loadAttemptId, int loginCompletionVersion)
    {
        string expectedOwnerInDate = Backend.UserInDate;
        int completionState = 0;

        Tween.Wait(PaymentDataLoadTimeoutSeconds, () =>
        {
            if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion)
                || Interlocked.Exchange(ref completionState, 1) != 0)
            {
                return;
            }

            PaymentInfo.CancelPendingLoad();
            if (!string.Equals(expectedOwnerInDate, Backend.UserInDate, StringComparison.Ordinal))
                return;

            ShowDataLoadFailure(loadAttemptId, "PaymentData 로드 시간 초과");
        });

        PaymentInfo.LoadPaymentDataAsync(expectedOwnerInDate, (result) =>
        {
            if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion)
                || !string.Equals(expectedOwnerInDate, Backend.UserInDate, StringComparison.Ordinal)
                || Interlocked.Exchange(ref completionState, 1) != 0)
            {
                return;
            }

            ContinueAfterPaymentDataLoaded(loadAttemptId, loginCompletionVersion);
        }, (failure) =>
        {
            if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion)
                || !string.Equals(expectedOwnerInDate, Backend.UserInDate, StringComparison.Ordinal)
                || Interlocked.Exchange(ref completionState, 1) != 0)
            {
                return;
            }

            ShowDataLoadFailure(loadAttemptId, "PaymentData 로드 실패: " + failure);
        });
    }

    private void ContinueAfterPaymentDataLoaded(int loadAttemptId, int loginCompletionVersion)
    {
        AssignRandomNicknameIfNeeded(() =>
        {
            if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion))
                return;

            Tween.Wait(0.7f, () =>
            {
                if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion)
                    || _sceneTransitionStarted)
                {
                    return;
                }

                _sceneTransitionStarted = true;
                _uiFirstLoadingScene.HideTitle(() =>
                {
                    if (!IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion))
                        return;

                    if (UserInfo.IsFirstTutorialClear)
                        Tween.Wait(0.1f, () => LoadSceneIfCurrent(loadAttemptId, loginCompletionVersion, "Stage1"));
                    else
                        Tween.Wait(0.1f, () => LoadSceneIfCurrent(loadAttemptId, loginCompletionVersion, "IntroScene"));
                });
            });
        });
    }

    private void LoadSceneIfCurrent(int loadAttemptId, int loginCompletionVersion, string sceneName)
    {
        if (IsCurrentLoginCompletion(loadAttemptId, loginCompletionVersion))
            LoadingSceneManager.LoadScene(sceneName);
    }

    private bool IsCurrentLoadAttempt(int loadAttemptId)
    {
        return this != null && loadAttemptId == _loadAttemptId;
    }

    private bool IsCurrentLoginCompletion(int loadAttemptId, int loginCompletionVersion)
    {
        return IsCurrentLoadAttempt(loadAttemptId)
            && loginCompletionVersion == _loginCompletionVersion;
    }

    private void ShowDataLoadFailure(int loadAttemptId, string reason)
    {
        if (!IsCurrentLoadAttempt(loadAttemptId))
            return;

        Debug.LogError("[FirstLoadingScene] " + reason);
        BackendManager.Instance.ShowPopup("데이터 로드 실패", "게임 데이터를 불러오는 데 실패했습니다.\n다시 시도해주세요.");
        BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
        BackendManager.Instance.ShowPopupExitButton();
    }

    private void OnGoogleLoginFailed()
    {
        int loadAttemptId = _loadAttemptId;
        if (!IsCurrentLoadAttempt(loadAttemptId))
            return;

        Debug.LogError("[FirstLoadingScene] 구글 로그인 실패");
        BackendManager.Instance.ShowPopup("로그인 실패", "구글 로그인에 실패했습니다.\n다시 시도해주세요.");
        BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
        BackendManager.Instance.ShowPopupExitButton();
    }

    private void AssignRandomNicknameIfNeeded(System.Action onComplete)
    {
        if (!string.IsNullOrWhiteSpace(UserInfo.UserId))
        {
            onComplete?.Invoke();
            return;
        }
        TryCreateRandomNickname(onComplete, 10);
    }

    private void TryCreateRandomNickname(System.Action onComplete, int retriesLeft)
    {
        if (retriesLeft <= 0)
        {
            Debug.LogError("[FirstLoadingScene] 닉네임 생성 실패: 최대 재시도 횟수 초과");
            onComplete?.Invoke();
            return;
        }

        string candidate = "User" + UnityEngine.Random.Range(10000000, 20000000);
        Backend.BMember.CheckNicknameDuplication(candidate, (checkBro) =>
        {
            if (checkBro.IsSuccess())
            {
                Backend.BMember.CreateNickname(candidate, (createBro) =>
                {
                    if (createBro.IsSuccess())
                    {
                        UserInfo.SetUserId(candidate);
                        BackendManager.Instance.SaveGameDataAsync("GameData", UserInfo.GetSaveUserData());
                        Debug.Log($"[FirstLoadingScene] 닉네임 생성 완료: {candidate}");
                        onComplete?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"[FirstLoadingScene] 닉네임 생성 실패, 재시도: {createBro.GetMessage()}");
                        TryCreateRandomNickname(onComplete, retriesLeft - 1);
                    }
                });
            }
            else
            {
                Debug.Log($"[FirstLoadingScene] 닉네임 중복 또는 오류, 재시도: {checkBro.GetMessage()}");
                TryCreateRandomNickname(onComplete, retriesLeft - 1);
            }
        });
    }

    private void StartLoadData()
    {
        _uiFirstLoadingScene.ShowTitle(() =>
        {
            BackendManager.Instance.GuestLoginAsync((bro) =>
            {
                UserInfo.LoadGameData(BackendManager.Instance.GetMyData("GameData"));
                UserInfo.LoadStageData();
                Tween.Wait(0.1f, () =>
                {
                    _uiFirstLoadingScene.HideTitle(() =>
                    {
                        Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("Stage1"));
                    });
                });
            }, (state) =>
            {
                Debug.LogError("[FirstLoadingScene] 게스트 로그인 실패: " + state);
                BackendManager.Instance.ShowPopup("로그인 실패", "게스트 로그인에 실패했습니다. 다시 시도해주세요.");
                BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                BackendManager.Instance.ShowPopupExitButton();
            });
        });
    }
}
