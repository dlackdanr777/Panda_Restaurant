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

    private void Start()
    {
        _uiLoadingScene.Init();
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
            FadeManager.Instance.FadeOut();
        });
    }


    private IEnumerator LoadScene()
    {
        if (string.IsNullOrWhiteSpace(_nextScene))
        {
            Debug.LogError("[LoadingSceneManager] 다음 씬이 지정되지 않아 로딩을 시작할 수 없습니다.");
            yield break;
        }

        // LoadingScene의 Start와 같은 프레임에 다음 씬 로드를 겹치지 않게 한 프레임만 양보한다.
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
