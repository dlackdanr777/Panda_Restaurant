using Muks.MobileUI;
using Muks.Tween;
using Muks.UI;
using UnityEngine;

public class UIMiniGameController : MobileUIView
{
    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private MiniGame1 _miniGame1;
    [SerializeField] private MiniGameFever _miniGameFever;


    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeUI;
    [SerializeField] private RectTransform _startPos;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private RectTransform _targetPos;
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _toturialAudio;
    [SerializeField] private AudioClip _bgAudio;

    public override void Init()
    {
        _miniGame1.Init(this);
        _miniGameFever.Init(this);
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        gameObject.SetActive(true);
        _dontTouchArea.SetActive(true);
        PopEnabled = false;
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_bgAudio, 0.5f);
        _startPos.anchoredPosition = new Vector2(Screen.width, 0);
        _animeUI.position = _startPos.position;
        transform.SetAsLastSibling();

        TweenData tween = _animeUI.TweenMove(_targetPos.position, _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            PopEnabled = false;
            _dontTouchArea.SetActive(false);
            VisibleState = VisibleState.Appeared;
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;

        StopAllCoroutines();
        gameObject.SetActive(true);
        _dontTouchArea.SetActive(true);
        transform.SetAsLastSibling();
        _startPos.anchoredPosition = new Vector2(Screen.width, 0);
        _animeUI.position = _targetPos.position;
        TweenData tween = _animeUI.TweenMove(_startPos.position, _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            PopEnabled = false;
            VisibleState = VisibleState.Disappeared;
            _miniGame1.Hide();
            _miniGameFever.Hide();
            gameObject.SetActive(false);
        });
    }

    public void HideUI()
    {
        PopEnabled = true;
        _uiNav.Pop("UIMiniGame");
    }

    public void ShowMiniGame1(FoodData foodData)
    {
        _uiNav.Push("UIMiniGame");
        _miniGame1.Show(foodData);
        _miniGameFever.Hide();
    }
}
