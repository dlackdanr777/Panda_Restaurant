using Muks.BackEnd;
using Muks.Tween;
using UnityEngine;

public class FirstLoadingScene : MonoBehaviour
{
    [SerializeField] private UIFirstLoadingScene _uiFirstLoadingScene;

    private void Start()
    {
        _uiFirstLoadingScene.Init();
        Tween.Wait(0.5f, StartLoadData2);
    }

    private void StartLoadDataAsync()
    {
        _uiFirstLoadingScene.ShowTitle(() =>
        {
            BackendManager.Instance.GuestLoginAsync((bro) =>
            {
                Debug.Log("00");
                BackendManager.Instance.GetMyDataAsync("GameData", (bro) =>
                {
                    UserInfo.LoadGameData(bro);
                    UserInfo.LoadStageDataAsync();
                    Tween.Wait(0.2f, () =>
                    {
                        _uiFirstLoadingScene.HideTitle(() =>
                        {
                            Tween.Wait(0.5f, () => LoadingSceneManager.LoadScene("Stage1"));
                        });
                    });
                });
            });
        });
    }

    private void StartLoadData2()
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
                        Tween.Wait(0.2f, () => LoadingSceneManager.LoadScene("Stage1"));
                    });
                });
            });
        });
    }
}
