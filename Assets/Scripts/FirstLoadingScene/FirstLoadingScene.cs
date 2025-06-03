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
            BackendManager.Instance.GuestLoginAsync((bro) =>
            {
                BackendManager.Instance.GetMyDataAsync("GameData", (bro) =>
                {
                    UserInfo.LoadGameData(bro);
                    UserInfo.LoadStageDataAsync();
                    Tween.Wait(0.7f, () =>
                    {
                        _uiFirstLoadingScene.HideTitle(() =>
                        {
                            Tween.Wait(0.1f, () => LoadingSceneManager.LoadScene("Stage1"));
                        });
                    });
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
            });
        });
    }
}
