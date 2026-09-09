using BackEnd;
using Muks.BackEnd;
using Muks.Tween;
using UnityEngine;

public class FirstLoadingScene : MonoBehaviour
{
    [SerializeField] private UIFirstLoadingScene _uiFirstLoadingScene;
    [SerializeField] private GoogleLoginManager _googleLoginManager;

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
#if UNITY_ANDROID
        GoogleLoginManager.OnGoogleLoginSuccessHandler -= OnLoginCompleted;
        GoogleLoginManager.OnGoogleAutoLoginSuccessHandler -= OnLoginCompleted;
        GoogleLoginManager.OnGoogleLoginFailedHandler -= OnGoogleLoginFailed;
#endif
    }

    private void StartLoadDataAsync()
    {
        _uiFirstLoadingScene.ShowTitle(() =>
        {
            Backend.Utils.GetServerStatus((callback) =>
            {
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


                StartLoginFlow();
            });
        });
    }

    private void StartLoginFlow()
    {
        BackendManager.Instance.InvalidateGameDataRestore();
#if UNITY_ANDROID
        var pref = GoogleLoginManager.GetLoginPreference();
        if (pref == GoogleLoginManager.LoginPreference.Google)
        {
            // Google 선호: 자동 로그인 시도, 실패 시 게스트
            _googleLoginManager.TryAutoLogin(onFail: () => DoGuestLogin());
        }
        else
        {
            // 게스트 또는 최초 실행: 토큰 재로그인 시도, 실패 시 게스트
            _googleLoginManager.TryTokenLogin(
                onSuccess: () => OnLoginCompleted(),
                onFail: () => DoGuestLogin()
            );
        }
#else
        DoGuestLogin();
#endif
    }

    private void DoGuestLogin()
    {
        BackendManager.Instance.GuestLoginAsync(
            onSuccess: (bro) => OnLoginCompleted(),
            onFail: (state) =>
            {
                Debug.LogError("[FirstLoadingScene] 게스트 로그인 실패: " + state);
                BackendManager.Instance.ShowPopup("로그인 실패", "게스트 로그인에 실패했습니다.\n다시 시도해주세요.");
                BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                BackendManager.Instance.ShowPopupExitButton();
            }
        );
    }

    private void OnLoginCompleted()
    {
        using (new VersionManagement())
        {
            if (!new VersionManagement().UpdateCheck())
                return;
        }

        BackendManager backend = BackendManager.Instance;
        backend.GetAndRestoreGameDataAsync((query, result) =>
        {
            bool IsCurrent() => this != null && backend.IsCurrentGameDataQuery(query);
            if (!IsCurrent()) return;
            if (!result.CanContinueLegacy)
            {
                ShowGameDataLoadFailure(result.Status + ": " + result.Reason);
                return;
            }

            // 공용 필드 부재는 기존 진입만 허용한다. 새 저장 Ready나 Stage 이전 완료를 뜻하지 않는다.
            backend.FetchGamerIdAsync(canApply: IsCurrent);
            if (!IsCurrent()) return;
            UserInfo.LoadStageDataAsync();
            if (!IsCurrent()) return;
            PaymentInfo.LoadPaymentData();
            AssignRandomNicknameIfNeeded(IsCurrent, () =>
            {
                if (!IsCurrent()) return;
                Tween.Wait(0.7f, () =>
                {
                    if (!IsCurrent()) return;
                    _uiFirstLoadingScene.HideTitle(() =>
                    {
                        if (!IsCurrent()) return;
                        Tween.Wait(0.1f, () =>
                        {
                            if (IsCurrent()) LoadingSceneManager.LoadScene(
                                UserInfo.IsFirstTutorialClear ? "Stage1" : "IntroScene");
                        });
                    });
                });
            });
        }, (state) =>
        {
            if (this != null) ShowGameDataLoadFailure(state.ToString());
        });
    }

    private void ShowGameDataLoadFailure(string reason)
    {
        Debug.LogWarning("[FirstLoadingScene] GameData 복원 중단: " + reason);
        BackendManager.Instance.ShowPopup("데이터 로드 확인 필요", "게임 데이터 원본을 확인하지 못했습니다.\n다시 시도하거나 문의해주세요.");
        BackendManager.Instance.SetPopupButton1("재시도", () => { if (this != null) OnLoginCompleted(); });
        BackendManager.Instance.ShowPopupExitButton();
    }

    private void OnGoogleLoginFailed()
    {
        Debug.LogError("[FirstLoadingScene] 구글 로그인 실패");
        BackendManager.Instance.ShowPopup("로그인 실패", "구글 로그인에 실패했습니다.\n다시 시도해주세요.");
        BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
        BackendManager.Instance.ShowPopupExitButton();
    }

    private void AssignRandomNicknameIfNeeded(System.Func<bool> isCurrent, System.Action onComplete)
    {
        if (!isCurrent()) return;
        if (!string.IsNullOrWhiteSpace(UserInfo.UserId))
        {
            onComplete?.Invoke();
            return;
        }
        TryCreateRandomNickname(isCurrent, onComplete, 10);
    }

    private void TryCreateRandomNickname(System.Func<bool> isCurrent, System.Action onComplete, int retriesLeft)
    {
        if (!isCurrent()) return;
        if (retriesLeft <= 0)
        {
            Debug.LogError("[FirstLoadingScene] 닉네임 생성 실패: 최대 재시도 횟수 초과");
            onComplete?.Invoke();
            return;
        }

        string candidate = "User" + UnityEngine.Random.Range(10000000, 20000000);
        Backend.BMember.CheckNicknameDuplication(candidate, (checkBro) =>
        {
            if (!isCurrent()) return;
            if (checkBro.IsSuccess())
            {
                Backend.BMember.CreateNickname(candidate, (createBro) =>
                {
                    if (!isCurrent()) return;
                    if (createBro.IsSuccess())
                    {
                        UserInfo.SetUserId(candidate);
                        if (BackendManager.Instance.CanSaveLegacyGameData)
                            BackendManager.Instance.SaveGameDataAsync("GameData", UserInfo.GetSaveUserData());
                        Debug.Log($"[FirstLoadingScene] 닉네임 생성 완료: {candidate}");
                        onComplete?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"[FirstLoadingScene] 닉네임 생성 실패, 재시도: {createBro.GetMessage()}");
                        TryCreateRandomNickname(isCurrent, onComplete, retriesLeft - 1);
                    }
                });
            }
            else
            {
                Debug.Log($"[FirstLoadingScene] 닉네임 중복 또는 오류, 재시도: {checkBro.GetMessage()}");
                TryCreateRandomNickname(isCurrent, onComplete, retriesLeft - 1);
            }
        });
    }

    private void StartLoadData()
    {
        // 남아 있는 이전 진입점도 검증된 동일 비동기 흐름을 사용한다.
        StartLoadDataAsync();
    }
}
