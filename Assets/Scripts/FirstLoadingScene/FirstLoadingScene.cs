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
            await BackendManager.Instance.GuestLogin(10, (bro) =>
            {
                BackendManager.Instance.GetMyData("GameData", 10, UserInfo.LoadGameData);
                UserInfo.LoadStageData();
                Tween.Wait(1, () =>
                {
                    _uiFirstLoadingScene.HideTitle(() =>
                    {
                        Tween.Wait(1f, () => LoadingSceneManager.LoadScene("Stage1"));
                    });
                });
            }, (bro) =>
            {
                _uiFirstLoadingScene.ShowErrorText(bro.GetErrorCode() + "\n" + bro.GetErrorMessage());
                //�����ϸ� ���⼭ �˾� ����
            });
            
        });
    }

}
