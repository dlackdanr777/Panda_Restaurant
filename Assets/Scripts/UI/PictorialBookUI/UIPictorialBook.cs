using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBook : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIPictorialBookGachaItem _uiGachaItem;
    [SerializeField] private UICustomerPictorialBook _uiCustomer;

    [Header("Button Options")]
    [SerializeField] private Button _gachaItemButton;
    [SerializeField] private Button _customerButton;
    [SerializeField] private RectTransform _gachaItemButtonClickPos;
    [SerializeField] private RectTransform _customerButtonClickPos;


    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;
    [SerializeField] private RectTransform _showTargetPos;


    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;
    [SerializeField] private RectTransform _hideTargetPos;

    private RectTransform _gachaItemButtonTr;
    private RectTransform _customerButtonTr;
    private Vector2 _tmpGachaItemButtonPos;
    private Vector2 _tmpCustomerButtonPos;


    public override void Init()
    {
        _uiGachaItem.Init();
        _uiCustomer.Init();

        _gachaItemButton.onClick.AddListener(OnGachaItemButtonClicked);
        _customerButton.onClick.AddListener(OnCustomerButtonClicked);

        _gachaItemButtonTr = _gachaItemButton.GetComponent<RectTransform>();
        _customerButtonTr = _customerButton.GetComponent<RectTransform>();

        _tmpGachaItemButtonPos = _gachaItemButtonTr.anchoredPosition;
        _tmpCustomerButtonPos = _customerButtonTr.anchoredPosition;

        UserInfo.OnGiveRecipeHandler += CustomerUpdateUI;
        UserInfo.OnChangeScoreHandler += CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler += CustomerUpdateUI;
        GameManager.Instance.OnChangeScoreHandler += CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler += GachaItemUpdateUI;

        OnGachaItemButtonClicked();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;

        gameObject.SetActive(true);
        _animeUI.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.anchoredPosition = _hideTargetPos.anchoredPosition;
        OnGachaItemButtonClicked();

        TweenData tween = _animeUI.TweenAnchoredPosition(_showTargetPos.anchoredPosition, _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

        _uiGachaItem.ResetData();
        _uiCustomer.ResetData();
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.anchoredPosition = _showTargetPos.anchoredPosition;

        TweenData tween = _animeUI.TweenAnchoredPosition(_hideTargetPos.anchoredPosition, _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    private void OnGachaItemButtonClicked()
    {
        _gachaItemButtonTr.anchoredPosition = _gachaItemButtonClickPos.anchoredPosition;
        _customerButtonTr.anchoredPosition = _tmpCustomerButtonPos;

        _uiGachaItem.gameObject.SetActive(true);
        _uiCustomer.gameObject.SetActive(false);

        _uiGachaItem.UpdateUI();
        _uiGachaItem.ChoiceView();
    }

    private void OnCustomerButtonClicked()
    {
        _customerButtonTr.anchoredPosition = _customerButtonClickPos.anchoredPosition;
        _gachaItemButtonTr.anchoredPosition = _tmpGachaItemButtonPos;

        _uiCustomer.gameObject.SetActive(true);
        _uiGachaItem.gameObject.SetActive(false);

        _uiCustomer.UpdateUI();
        _uiCustomer.ChoiceView();
    }

    private void CustomerUpdateUI()
    {
        if (!gameObject.activeSelf)
            return;

        _uiCustomer.UpdateUI();
    }

    private void GachaItemUpdateUI()
    {
        if (!gameObject.activeSelf)
            return;

        _uiGachaItem.UpdateUI();
    }

    private void OnDestroy()
    {
        UserInfo.OnGiveRecipeHandler -= CustomerUpdateUI;
        UserInfo.OnChangeScoreHandler -= CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler -= CustomerUpdateUI;
        GameManager.Instance.OnChangeScoreHandler -= CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler -= GachaItemUpdateUI;
    }
}
