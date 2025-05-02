using System.Collections;
using UnityEngine;

public class MiniGameStartTimer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _blackImage;
    [SerializeField] private RectTransform _3Image;
    [SerializeField] private RectTransform _2Image;
    [SerializeField] private RectTransform _1Image;
    [SerializeField] private RectTransform _startImage;
    [SerializeField] private RectTransform _gameOverImage;
    [SerializeField] private RectTransform _claerImage;

    public void Init()
    {
        ResetTimer();
    }

    public void ShowClearImage()
    {
        ResetTimer();
        _claerImage.gameObject.SetActive(true);
    }

    public void ShowGameOverImage()
    {
        ResetTimer();
        _gameOverImage.gameObject.SetActive(true);
    }

    public void ResetTimer()
    {
        _blackImage.gameObject.SetActive(false);
        _3Image.gameObject.SetActive(false);
        _2Image.gameObject.SetActive(false);
        _1Image.gameObject.SetActive(false);
        _startImage.gameObject.SetActive(false);
        _gameOverImage.gameObject.SetActive(false);
        _claerImage.gameObject.SetActive(false);
    }

    public IEnumerator StartTimer()
    {
        ResetTimer();
        DebugLog.Log("StartTimer()");
        _blackImage.gameObject.SetActive(true);
        yield return YieldCache.WaitForSeconds(0.5f);
        _3Image.gameObject.SetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        _3Image.gameObject.SetActive(false);
        _2Image.gameObject.SetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        _2Image.gameObject.SetActive(false);
        _1Image.gameObject.SetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        _1Image.gameObject.SetActive(false);
        _startImage.gameObject.SetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        _startImage.gameObject.SetActive(false);
        _blackImage.gameObject.SetActive(false);
    }
}
