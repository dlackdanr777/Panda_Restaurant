using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITip : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _collectTipButton;
    [SerializeField] private Button _advertisingButton;
    [SerializeField] private TextMeshProUGUI _tipAnimeTmp;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;


    private Vector2 _tipAnimeTmpPos;
    private Color _tipAnimeTmpColor;

    public override void Init()
    {
        _collectTipButton.onClick.AddListener(OnCollectTipButtonClicked);
        _advertisingButton.onClick.AddListener(OnAdvertisingButtonClicked);

        _tipAnimeTmpPos = _tipAnimeTmp.rectTransform.anchoredPosition;
        _tipAnimeTmpColor = _tipAnimeTmp.color;
        _tipAnimeTmp.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    private void OnCollectTipButtonClicked()
    {
        if (UserInfo.Tip <= 0)
        {
            TimedDisplayManager.Instance.ShowText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        TMPAnime("+ " + UserInfo.Tip.ToString("N0"));
        UserInfo.TipCollection();
    }

    private void OnAdvertisingButtonClicked()
    {
        if(UserInfo.Tip <= 0)
        {
            TimedDisplayManager.Instance.ShowText("È¹µæ °¡´ÉÇÑ ÆÁÀÌ ¾ø½À´Ï´Ù.");
            return;
        }

        TMPAnime("+ " + (UserInfo.Tip * 2).ToString("N0"));
        UserInfo.TipCollection(true);
    }


    private void TMPAnime(string text)
    {
        _tipAnimeTmp.gameObject.SetActive(true);
        _tipAnimeTmp.rectTransform.TweenStop();
        _tipAnimeTmp.TweenStop();
        _tipAnimeTmp.text = text;
        _tipAnimeTmp.rectTransform.anchoredPosition = _tipAnimeTmpPos;
        _tipAnimeTmp.color = _tipAnimeTmpColor;
        _tipAnimeTmp.rectTransform.TweenAnchoredPosition(_tipAnimeTmpPos + new Vector2(0, 50), 1f, TweenMode.EaseInQuad);
        _tipAnimeTmp.TweenAlpha(0, 1, TweenMode.Smoothstep).OnComplete(() => _tipAnimeTmp.gameObject.SetActive(false));
    }
}
