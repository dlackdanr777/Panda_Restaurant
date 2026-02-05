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
        // 저사양 기기 대응 설정
        _videoPlayer.skipOnDrop = false;
        _videoPlayer.waitForFirstFrame = true;
        
        // RenderTexture 모드 사용 시 강제로 활성화
        if (_videoPlayer.targetTexture != null)
        {
            _videoPlayer.targetTexture.Release();
        }
        
        _videoPlayer.Prepare();
        
        // 비디오 준비 완료 대기 (타임아웃 포함)
        float timeout = 5f;
        float elapsed = 0f;
        while (!_videoPlayer.isPrepared && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 준비 실패 시 바로 넘어감
        if (!_videoPlayer.isPrepared)
        {
            Debug.LogWarning("비디오 준비 실패 - 다음 씬으로 이동");
            LoadingSceneManager.LoadScene("Stage1");
            yield break;
        }
        
        _videoPlayer.Play();
        
        // 비디오가 실제로 재생되는지 확인
        float playCheckTimeout = 2f;
        float playCheckElapsed = 0f;
        while (!_videoPlayer.isPlaying && playCheckElapsed < playCheckTimeout)
        {
            playCheckElapsed += Time.deltaTime;
            yield return null;
        }
        
        // 재생 시작 실패 시 다음 씬으로
        if (!_videoPlayer.isPlaying)
        {
            Debug.LogWarning("비디오 재생 시작 실패 - 다음 씬으로 이동");
            LoadingSceneManager.LoadScene("Stage1");
            yield break;
        }
        
        // 비디오 재생 완료 대기 (최대 30초)
        float maxPlayTime = 30f;
        float playElapsed = 0f;
        while (_videoPlayer.isPlaying && playElapsed < maxPlayTime)
        {
            playElapsed += Time.deltaTime;
            yield return null;
        }
        
        LoadingSceneManager.LoadScene("Stage1");
    }
}
