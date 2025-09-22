using Muks.Tween;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;



public class LoadingSceneManager : MonoBehaviour
{
    [SerializeField] private UILoadingScene _uiLoadingScene;


    [Tooltip("로딩이 최소 몇 초가 걸리게 할지 설정")]
    [SerializeField] private float _changeSceneTime;

    private static string _nextScene;
    public static event Action OnLoadSceneHandler;

    private static bool _isStart;

    private void Start()
    {
        GC.Collect();
        _uiLoadingScene.Init();
        _isStart = false;
        StartCoroutine(LoadScene());
    }


    public static void LoadScene(string sceneName)
    {
        _nextScene = sceneName;
        OnLoadSceneHandler?.Invoke();
        FadeManager.Instance.FadeIn( onComplete: () =>
        {
            SceneManager.LoadScene("LoadingScene");
            FadeManager.Instance.FadeSetActive(true);
            FadeManager.Instance.FadeOut(onComplete: () => _isStart = true);
        });
    }


    private IEnumerator LoadScene()
    {
        if(!_isStart)
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(_nextScene);
        op.allowSceneActivation = false;
        float timer = 0f;
        
        while (!op.isDone)
        {
            yield return null;
            _uiLoadingScene.SetLoadingBarFillAmount(op.progress);
            if (0.9f <= op.progress)
            {
                timer += Time.deltaTime;
                _uiLoadingScene.SetLoadingBarFillAmount(0.9f + ((timer / _changeSceneTime) * 0.1f));

                if (_changeSceneTime < timer)
                {
  
                    _uiLoadingScene.SetLoadingBarFillAmount(1);
                    FadeManager.Instance.FadeSetActive(false);
                    Tween.Wait(0.5f, () => FadeManager.Instance.FadeIn(onComplete: () => 
                    {
                        op.allowSceneActivation = true;
                        FadeManager.Instance.FadeSetActive(true);
                        Tween.Wait(0.1f, () =>
                        {
                            FadeManager.Instance.FadeSetActive(true);
                            FadeManager.Instance.FadeOut();
                        });
                    }));
                    yield break;
                }
            }
        }

        
    }
}
