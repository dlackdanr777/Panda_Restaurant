using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class IntroScene : MonoBehaviour
{
    [SerializeField] private IntroSceneCanvas _introSceneCanvas;
    [SerializeField] private Animator _animator;

    private void Start()
    {
        _introSceneCanvas.Init(this);
        StartCoroutine(LoadNextScene());
    }

    public void SkipIntroVideo()
    {
        StopAllCoroutines();
        LoadingSceneManager.LoadScene("Stage1");
    }

    private IEnumerator LoadNextScene()
    {
        yield return new WaitForSeconds(1f);
yield return new WaitUntil(() =>
    _animator.GetCurrentAnimatorStateInfo(0).IsName("FirstIntro") &&
    _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
        yield return new WaitForSeconds(1f);
        LoadingSceneManager.LoadScene("Stage1");
    }
}
