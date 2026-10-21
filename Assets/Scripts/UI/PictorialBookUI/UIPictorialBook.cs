using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBook : MobileUIView
{
    private GameManager _subscribedGameManager;

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIPictorialBookGachaItem _uiGachaItem;
    [SerializeField] private UICustomerPictorialBook _uiCustomer;
    [SerializeField] private UIStatus _uiStatus;

    [Header("Button Options")]
    [SerializeField] private Button _gachaItemButton;
    [SerializeField] private Button _customerButton;
    [SerializeField] private Button _statusButton;
    [SerializeField] private RectTransform _gachaItemButtonClickPos;
    [SerializeField] private RectTransform _customerButtonClickPos;
    [SerializeField] private RectTransform _statusButtonClickPos;

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
    private RectTransform _statusButtonTr;
    private Vector2 _tmpGachaItemButtonPos;
    private Vector2 _tmpCustomerButtonPos;
    private Vector2 _tmpStatusButtonPos;
    private bool _initialized;
    private enum Tab { GachaItem, Customer, Status }


    public override void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _uiGachaItem.Init();
        _uiCustomer.Init();
        _uiStatus.Init();

        _gachaItemButton.onClick.AddListener(OnGachaItemButtonClicked);
        _customerButton.onClick.AddListener(OnCustomerButtonClicked);
        _statusButton.onClick.AddListener(OnStatusButtonClicked);

        _gachaItemButtonTr = _gachaItemButton.GetComponent<RectTransform>();
        _customerButtonTr = _customerButton.GetComponent<RectTransform>();
        _statusButtonTr = _statusButton.GetComponent<RectTransform>();

        _tmpGachaItemButtonPos = _gachaItemButtonTr.anchoredPosition;
        _tmpCustomerButtonPos = _customerButtonTr.anchoredPosition;
        _tmpStatusButtonPos = _statusButtonTr.anchoredPosition;

        UserInfo.OnGiveRecipeHandler += CustomerUpdateUI;
        UserInfo.OnChangeScoreHandler += CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler += CustomerUpdateUI;
        _subscribedGameManager = GameManager.Instance;
        _subscribedGameManager.OnChangeScoreHandler += CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler += GachaItemUpdateUI;

        SetTab(Tab.GachaItem);
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;

        _uiCustomer.SetTabVisible(false);
        _uiGachaItem.SetTabVisible(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
        _uiCustomer.PrepareForOpen();
        _uiGachaItem.ResetData();
        SetTab(Tab.GachaItem);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.gameObject.SetActive(true);
        _animeUI.anchoredPosition = _hideTargetPos.anchoredPosition;
        gameObject.SetActive(true);
        _uiGachaItem.SetTabVisible(true);
        TweenData tween = _animeUI.TweenAnchoredPosition(_showTargetPos.anchoredPosition, _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _uiCustomer.SetTabVisible(false);
        _uiGachaItem.SetTabVisible(false);
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
        SetTab(Tab.GachaItem);
        _uiGachaItem.ResetData();
        _uiGachaItem.SetTabVisible(true);
    }

    private void OnCustomerButtonClicked()
    {
        _uiCustomer.SetTabVisible(false);
        _uiCustomer.HideSkinView();
        SetTab(Tab.Customer);
        _uiCustomer.SetTabVisible(true);
        _uiCustomer.ChoiceView();
    }


    private void OnStatusButtonClicked()
    {
        SetTab(Tab.Status);
        _uiStatus.UpdateUI();
        _uiStatus.Show();
    }

    private void CustomerUpdateUI()
    {
        _uiCustomer.UpdateUI();
    }

    private void GachaItemUpdateUI()
    {
        _uiGachaItem.UpdateUI();
    }

    private void SetTab(Tab tab)
    {
        _uiCustomer.SetTabVisible(false);
        _uiGachaItem.SetTabVisible(false);
        _gachaItemButtonTr.anchoredPosition = tab == Tab.GachaItem ? _gachaItemButtonClickPos.anchoredPosition : _tmpGachaItemButtonPos;
        _customerButtonTr.anchoredPosition = tab == Tab.Customer ? _customerButtonClickPos.anchoredPosition : _tmpCustomerButtonPos;
        _statusButtonTr.anchoredPosition = tab == Tab.Status ? _statusButtonClickPos.anchoredPosition : _tmpStatusButtonPos;
        _uiGachaItem.gameObject.SetActive(tab == Tab.GachaItem);
        _uiCustomer.gameObject.SetActive(tab == Tab.Customer);
        _uiStatus.gameObject.SetActive(tab == Tab.Status);
    }

    private void OnDestroy()
    {
        UserInfo.OnGiveRecipeHandler -= CustomerUpdateUI;
        UserInfo.OnChangeScoreHandler -= CustomerUpdateUI;
        UserInfo.OnGiveGachaItemHandler -= CustomerUpdateUI;
        if (_subscribedGameManager != null)
            _subscribedGameManager.OnChangeScoreHandler -= CustomerUpdateUI;
        _subscribedGameManager = null;
        UserInfo.OnGiveGachaItemHandler -= GachaItemUpdateUI;
    }
}
