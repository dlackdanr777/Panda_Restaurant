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
        _uiFirstLoadingScene.ShowTitle(() =>
        {
            BackendManager.Instance.GuestLoginAsync( (bro) =>
            {
                BackendManager.Instance.GetMyDataAsync("GameData", (bro) =>{
                    UserInfo.LoadGameData(bro);
                    UserInfo.LoadStageData();
                    Tween.Wait(0.5f, () =>
                    {
                    _uiFirstLoadingScene.HideTitle(() =>
                    {
                        Tween.Wait(1f, () => LoadingSceneManager.LoadScene("Stage1"));
                    });
                });
            });         
        });            
        });
    }

}
