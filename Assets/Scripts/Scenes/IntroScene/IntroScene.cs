using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class IntroScene : MonoBehaviour
{
    [SerializeField] private IntroSceneCanvas _introSceneCanvas;
    [SerializeField] private VideoPlayer _videoPlayer;

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
        _videoPlayer.Prepare();
        yield return new WaitForSeconds(1f);
        _videoPlayer.Play();
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => !_videoPlayer.isPlaying);
        LoadingSceneManager.LoadScene("Stage1");
    }
}
