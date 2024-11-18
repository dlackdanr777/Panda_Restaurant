using Muks.BackEnd;
using Muks.Tween;
using UnityEngine;

public class FirstLoadingScene : MonoBehaviour
{
    [SerializeField] private UIFirstLoadingScene _uiFirstLoadingScene;

    private void Start()
    {
        _uiFirstLoadingScene.Init();
        Tween.Wait(1, StartLoadData);
    }

    private void StartLoadData()
    {
        _uiFirstLoadingScene.ShowTitle(async () =>
        {
            await BackendManager.Instance.GuestLogin(10);
            BackendManager.Instance.GetMyData("Test", 10, UserInfo.LoadGameData);
            Tween.Wait(1, () =>
            {
                _uiFirstLoadingScene.HideTitle(() =>
                {
                    Tween.Wait(1f, () => LoadingSceneManager.LoadScene("MainScene"));
                });
            });
        });
    }

}
