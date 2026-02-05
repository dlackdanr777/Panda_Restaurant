using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class IntroSceneCanvas : MonoBehaviour
{
    [SerializeField] private Button _showHideButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private CanvasGroup _skipButtonCanvasGroup;

    public void Init(IntroScene introScene)
    {
        _skipButton.onClick.AddListener(() => introScene.SkipIntroVideo());
        _skipButtonCanvasGroup.alpha = 0;
        _skipButton.gameObject.SetActive(false);

        _showHideButton.onClick.AddListener(ShowHideButtonClicked);
    }

    private void ShowHideButtonClicked()
    {
        bool isActive = _skipButton.gameObject.activeSelf;
        _skipButtonCanvasGroup.alpha = isActive ? 1 : 0;
        _skipButtonCanvasGroup.TweenStop();
        if (isActive)
        {
            _skipButtonCanvasGroup.TweenAlpha(0, 0.1f).OnComplete(() =>
            {
                _skipButton.gameObject.SetActive(false);
            });
        }
        
        else 
        {
            _skipButton.gameObject.SetActive(true);
            _skipButtonCanvasGroup.TweenAlpha(1, 0.1f);
        }
    }
}
