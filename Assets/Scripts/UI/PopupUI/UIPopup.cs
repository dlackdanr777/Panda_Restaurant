using Muks.Tween;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
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


    public void Init()
    {
        _button1.AddListener(OnButton1ClickEvent);
        _button2.AddListener(OnButton2ClickEvent);
        gameObject.SetActive(false);
    }


    public void Show(string title, string description)
    {
        gameObject.SetActive(true);
        _button1.gameObject.SetActive(false);
        _button2.gameObject.SetActive(false);
        _titleText.text = title;
        _descriptionText.text = description;

        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
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
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
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
