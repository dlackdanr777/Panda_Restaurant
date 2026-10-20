using Muks.Tween;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _cardBackgroundImage;
    [SerializeField] private Image _encounterPortrait;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private UIButtonAndText _button1;
    [SerializeField] private UIButtonAndText _button2;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private Action _onButton1Clicked;
    private Action _onButton2Clicked;
    private Vector2 _defaultTitlePosition;
    private Vector2 _defaultTitleSize;
    private Vector2 _defaultDescriptionPosition;
    private Vector2 _defaultDescriptionSize;
    private Vector3 _shownScale = Vector3.one;


    public void Init()
    {
        _defaultTitlePosition = _titleText.rectTransform.anchoredPosition;
        _defaultTitleSize = _titleText.rectTransform.sizeDelta;
        _defaultDescriptionPosition = _descriptionText.rectTransform.anchoredPosition;
        _defaultDescriptionSize = _descriptionText.rectTransform.sizeDelta;
        if (_encounterPortrait != null) _encounterPortrait.gameObject.SetActive(false);
        _button1.AddListener(OnButton1ClickEvent);
        _button2.AddListener(OnButton2ClickEvent);
        gameObject.SetActive(false);
    }


    public void Show(string title, string description)
    {
        ShowInternal(title, description, null, false);
    }

    public void ShowCustomerEncounter(string customerName, string message, Sprite portrait)
    {
        ShowInternal(customerName, message, portrait, true);
    }

    private void ShowInternal(string title, string description, Sprite portrait, bool isEncounter)
    {
        gameObject.SetActive(true);
        _button1.gameObject.SetActive(false);
        _button2.gameObject.SetActive(false);
        _titleText.text = title;
        _descriptionText.text = description;

        _shownScale = isEncounter ? Vector3.one * 0.82f : Vector3.one;
        if (_backgroundImage != null) _backgroundImage.enabled = !isEncounter;
        if (_cardBackgroundImage != null) _cardBackgroundImage.raycastTarget = isEncounter;
        if (_encounterPortrait != null)
        {
            _encounterPortrait.sprite = portrait;
            _encounterPortrait.gameObject.SetActive(isEncounter && portrait != null);
        }
        if (isEncounter)
        {
            _titleText.rectTransform.anchoredPosition = new Vector2(70f, _defaultTitlePosition.y);
            _titleText.rectTransform.sizeDelta = new Vector2(475f, _defaultTitleSize.y);
            _descriptionText.rectTransform.anchoredPosition = new Vector2(90f, -20f);
            _descriptionText.rectTransform.sizeDelta = new Vector2(465f, _defaultDescriptionSize.y);
        }
        else
        {
            _titleText.rectTransform.anchoredPosition = _defaultTitlePosition;
            _titleText.rectTransform.sizeDelta = _defaultTitleSize;
            _descriptionText.rectTransform.anchoredPosition = _defaultDescriptionPosition;
            _descriptionText.rectTransform.sizeDelta = _defaultDescriptionSize;
        }

        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = _shownScale * 0.3f;
        transform.SetAsLastSibling();

        TweenData tween = _animeUI.TweenScale(_shownScale, _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            _canvasGroup.blocksRaycasts = true;
        });
    }

    public void SetButton1(string buttonText, Action buttonClicked)
    {
        _button1.gameObject.SetActive(true);
        _button1.SetText(buttonText);
        _onButton1Clicked = buttonClicked;
    }

    public void SetButton2(string buttonText, Action buttonClicked)
    {
        _button2.gameObject.SetActive(true);
        _button2.SetText(buttonText);
        _onButton2Clicked = buttonClicked;
    }


    public void Hide()
    {
        _animeUI.SetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = _shownScale;

        TweenData tween = _animeUI.TweenScale(_shownScale * 0.3f, _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    private void OnButton1ClickEvent()
    {
        _onButton1Clicked?.Invoke();
    }

    private void OnButton2ClickEvent()
    {
        _onButton2Clicked?.Invoke();
    }
}
