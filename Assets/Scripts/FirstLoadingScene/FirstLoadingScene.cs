using BackEnd;
using Muks.BackEnd;
using Muks.Tween;
using UnityEngine;

public class FirstLoadingScene : MonoBehaviour
{
    [SerializeField] private UIFirstLoadingScene _uiFirstLoadingScene;
    [SerializeField] private GoogleLoginManager _googleLoginManager;

    // 같은 로그인 세대(generation)에서 중복 진입하는 것을 막습니다(중복 로그인 이벤트/재시도 대응).
    private int _activeLoadGeneration = -1;

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
        int myGeneration = BackendManager.Instance.SaveGuard.SessionGeneration;
        // 중복 로그인 이벤트(자동/자동 로그인 핸들러가 동시에 따로 호출되는 경우)로 같은 세대의 로드가 이미 진행 중이면 무시합니다.
        if (_activeLoadGeneration == myGeneration)
            return;
        _activeLoadGeneration = myGeneration;

        // UUID(gamerId) 조회 - 실패해도 게임 진행
        BackendManager.Instance.FetchGamerIdAsync();

        using (new VersionManagement())
        {
            if (!new VersionManagement().UpdateCheck())
                return;
        }

        BackendManager.Instance.GetMyDataAsync("GameData", (bro) =>
        {
            // 이 응답이 도착하는 사이 계정이 전환되었다면(새 세대 시작) 적용하지 않습니다.
            if (myGeneration != BackendManager.Instance.SaveGuard.SessionGeneration)
                return;

            bool gameDataOk = UserInfo.LoadGameData(bro);
            if (!gameDataOk)
            {
                ShowLoadFailurePopup();
                return;
            }

            // Stage2/3은 아직 도달하지 않았을 수 있어 결과를 기다리지 않고 미리 로드만 합니다.
            UserInfo.LoadStageDataAsync(EStage.Stage2);
            UserInfo.LoadStageDataAsync(EStage.Stage3);
            PaymentInfo.LoadPaymentData();

            // Stage1은 튜토리얼을 클리어한 계정이라면 반드시 존재해야 하는 필수 데이터이므로 검증 완료를 실제로 기다립니다.
            UserInfo.LoadStageDataAsync(EStage.Stage1, (stage1Ok) =>
            {
                if (myGeneration != BackendManager.Instance.SaveGuard.SessionGeneration)
                    return;

                if (!stage1Ok)
                {
                    ShowLoadFailurePopup();
                    return;
                }

                ProceedAfterValidation();
            });
        }, (state) =>
        {
            Debug.LogError("[FirstLoadingScene] 게임 데이터 로드 실패: " + state);
            ShowLoadFailurePopup();
        });
    }

    /// <summary>GameData + 필수 스테이지(Stage1) 검증이 모두 완료된 뒤에만 호출됩니다.</summary>
    private void ProceedAfterValidation()
    {
        AssignRandomNicknameIfNeeded(() =>
        {
            Tween.Wait(0.7f, () =>
            {
                _uiFirstLoadingScene.HideTitle(() =>
                {
                    if (UserInfo.IsFirstTutorialClear)
                        Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("Stage1"));
                    else
                        Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("IntroScene"));
                });
            });
        });
    }

    /// <summary>필수 데이터 검증 실패 시 안내+재조회 팔업을 표시합니다. 재시도는 같은 세대라도 다시 검증을 시도합니다.</summary>
    private void ShowLoadFailurePopup()
    {
        BackendManager.Instance.ShowPopup("데이터 로드 실패", "게임 데이터를 불러오는 데 실패했습니다.\n다시 시도해주세요.");
        BackendManager.Instance.SetPopupButton1("재시도", () =>
        {
            _activeLoadGeneration = -1;
            if (BackendManager.Instance.IsLogin)
                OnLoginCompleted();
            else
                StartLoadDataAsync();
        });
        BackendManager.Instance.ShowPopupExitButton();
    }

    private void OnGoogleLoginFailed()
    {
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
