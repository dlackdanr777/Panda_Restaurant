using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;



public class LoadingSceneManager : MonoBehaviour
{


    [Tooltip("로딩이 최소 몇 초가 걸리게 할지 설정")]
    [SerializeField] private float _changeSceneTime;

    private static string _nextScene;
    public static event Action OnLoadSceneHandler;


    private void Start()
    {
        StartCoroutine(LoadScene());
        GC.Collect();
    }


    public static void LoadScene(string sceneName)
    {
        _nextScene = sceneName;
        OnLoadSceneHandler?.Invoke();
        FadeManager.Instance.FadeIn( onComplete: () => SceneManager.LoadScene("LoadingScene") );
    }


    private IEnumerator LoadScene()
    {
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(_nextScene);

        op.allowSceneActivation = false;
        float timer = 0f;
        
        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            if(0.9f <= op.progress)
            {
                if(_changeSceneTime < timer)
                {
                    op.allowSceneActivation = true;
                    FadeManager.Instance.FadeOut();
                    yield break;
                }
            }
        }

        
    }
}
