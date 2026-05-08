using BackEnd;
using Muks.BackEnd;
using Muks.Tween;
using UnityEngine;

public class FirstLoadingScene : MonoBehaviour
{
    [SerializeField] private UIFirstLoadingScene _uiFirstLoadingScene;

    private void Start()
    {
        _uiFirstLoadingScene.Init();
        Tween.Wait(0.2f, StartLoadDataAsync);
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


                BackendManager.Instance.GuestLoginAsync((bro) =>
                           {
                                using(new VersionManagement())
                                {
                                   if(!new VersionManagement().UpdateCheck())
                                   {
                                        return;
                                   }
                                }

                               BackendManager.Instance.GetMyDataAsync("GameData", (bro) =>
                               {
                                   UserInfo.LoadGameData(bro);
                                   UserInfo.LoadStageDataAsync();
                                   PaymentInfo.LoadPaymentData();
                                   Tween.Wait(0.7f, () =>
                                   {
                                       _uiFirstLoadingScene.HideTitle(() =>
                                       {
                                           if (UserInfo.IsFirstTutorialClear)
                                               Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("Stage1"));
                                           else
                                               Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("IntroScene"));

                                        //Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("Stage1"));
                                       });
                                   });
                               }, (state) =>
                               {
                                   Debug.LogError("[FirstLoadingScene] 게임 데이터 로드 실패: " + state);
                                   BackendManager.Instance.ShowPopup("데이터 로드 실패", "게임 데이터를 불러오는 데 실패했습니다.\n다시 시도해주세요.");
                                   BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                                   BackendManager.Instance.ShowPopupExitButton();
                               });
                           }, (state) =>
                           {
                               Debug.LogError("[FirstLoadingScene] 게스트 로그인 실패: " + state);
                               BackendManager.Instance.ShowPopup("로그인 실패", "게스트 로그인에 실패했습니다.\n다시 시도해주세요.");
                               BackendManager.Instance.SetPopupButton1("재시도", () => StartLoadDataAsync());
                               BackendManager.Instance.ShowPopupExitButton();
                           });
            });
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
