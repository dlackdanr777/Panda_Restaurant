using UnityEngine;
using Muks.MobileUI;
using System;
using UnityEngine.UI;
using UnityEngine.Events;
using Muks.Tween;

public class UITutorial1 : MobileUIView
{
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private GameObject _uiPunchHole;
    [SerializeField] private GameObject _shopMask;
    [SerializeField] private HoleClickHandler _tableHole;
    [SerializeField] private HoleClickHandler _kitchenHole;
    [SerializeField] private HoleClickHandler _buyHole;
    [SerializeField] private HoleClickHandler _exitHole;
    [SerializeField] private HoleClickHandler _backHole;

    [SerializeField] private EnabledScaleAnimation _shopMaskAnime;
    [SerializeField] private EnabledScaleAnimation _tableHoleAnime;
    [SerializeField] private EnabledScaleAnimation _kitchenHoleAnime;
    [SerializeField] private EnabledScaleAnimation _exitHoleAnime;
    [SerializeField] private EnabledScaleAnimation _buyHoleAnime;
    [SerializeField] private EnabledScaleAnimation _backHoleAnime;

    [SerializeField] private GameObject _shopMaskCursor;
    [SerializeField] private GameObject _tableHoleCursor;
    [SerializeField] private GameObject _kitchenHoleCursor;
    [SerializeField] private GameObject _exitHoleCursor;
    [SerializeField] private GameObject _buyHoleCursor;
    [SerializeField] private GameObject _backHoleCursor;


    private bool _isShopButtonClicked;
    public bool IsShopButtonClicked => _isShopButtonClicked;

    private bool _isTableHoleClicked;
    public bool IsTableHoleClicked => _isTableHoleClicked;

    private bool _isKitchenHoleClicked;
    public bool IsKitchenHoleClicked => _isKitchenHoleClicked;

    private bool _isExitHoleClicked;
    public bool IsExitHoleClicked => _isExitHoleClicked;

    private bool _isBackHoleClicked;
    public bool IsBackHoleClicked => _isBackHoleClicked;

    public override void Init()
    {
        _screenButton.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _uiPunchHole.gameObject.SetActive(false);
        _shopButton.gameObject.SetActive(false);
        _shopMask.gameObject.SetActive(false);
        _tableHole .SetActive(false);
        _kitchenHole.SetActive(false);
        _buyHole.SetActive(false);
        _exitHole.SetActive(false);
        _backHole.SetActive(false);
        _tableHoleCursor.SetActive(false);
        _kitchenHoleCursor.SetActive(false);
        _buyHoleCursor.SetActive(false);
        _exitHoleCursor.SetActive(false);
        _backHoleCursor.SetActive(false);

        _tableHole.AddListener(OnTableHoleClicked);
        _kitchenHole.AddListener(OnKitchenHoleClicked);
        _buyHole.AddListener(OnBuyHoleClicked);
        _exitHole.AddListener(OnExitHoleClicked);
        _shopButton.onClick.AddListener(OnShopButtonClicked);
        _backHole.AddListener(OnBackHoleClicked);

        _tableHole.SetTargetObjectName("Slot1");
        _kitchenHole.SetTargetObjectName("Kichen Button");
        _buyHole.SetTargetObjectName("Buy Button");
        _exitHole.SetTargetObjectName("Exit Button");
        _backHole.SetTargetObjectName("Back Button");

        _shopMaskAnime.SetCallBack(() => _shopMaskCursor.gameObject.SetActive(false), null, OnShopMaskAnimeCompleted);
        _tableHoleAnime.SetCallBack(() => _tableHoleCursor.gameObject.SetActive(false), null, OnTableHoleAnimeCompleted);
        _kitchenHoleAnime.SetCallBack(() => _kitchenHoleCursor.gameObject.SetActive(false), null, OnKitchenHoleAnimeCompleted);
        _buyHoleAnime.SetCallBack(() => _buyHoleCursor.gameObject.SetActive(false), null, OnBuyHoleAnimeCompleted);
        _exitHoleAnime.SetCallBack(() => _exitHoleCursor.gameObject.SetActive(false), null, OnExitHoleAnimeCompleted);
        _backHoleAnime.SetCallBack(() => _backHoleCursor.gameObject.SetActive(false), null, OnBackHoleAnimeCompleted);

        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        _screenButton.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _uiPunchHole.gameObject.SetActive(false);
        _shopButton.gameObject.SetActive(false);
        _shopMask.gameObject.SetActive(false);
        _tableHole.SetActive(false);
        _kitchenHole.SetActive(false);
        _buyHole.SetActive(false);
        _exitHole.SetActive(false);
        _backHole.SetActive(false);

        VisibleState = VisibleState.Appeared;
        gameObject.SetActive(true);
    }

    public override void Hide()
    {
        _screenButton.gameObject.SetActive(false);
        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }


    public void ScreenButtonSetActive(bool value)
    {
        _screenButton.gameObject.SetActive(value);
    }


    public void PunchHoleSetActive(bool value)
    {
        _uiPunchHole.SetActive(value);
    }

    public void ShopButtonSetActive(bool value)
    {
        _shopButton.gameObject.SetActive(value);
        _isShopButtonClicked = false;
        _shopButton.interactable = false;
    }

    public void ShopMaskSetActive(bool value)
    {
        _shopMask.gameObject.SetActive(value);
        _shopMaskCursor.SetActive(false);
    }


    public void TableHoleSetActive(bool value)
    {
        _tableHole.SetActive(value);
        _tableHoleCursor.SetActive(false);
        _tableHole.Interactable = false;
        _isTableHoleClicked = false;
    }

    public void KitchenHoleSetActive(bool value)
    {
        _kitchenHole.SetActive(value);
        _kitchenHoleCursor.SetActive(false);
        _kitchenHole.Interactable = false;
        _isKitchenHoleClicked = false;
    }


    public void BuyHoleSetActive(bool value)
    {
        _buyHole.SetActive(value);
        _buyHoleCursor.SetActive(false);
        _buyHole.Interactable = false;
    }

    public void ExitHoleSetActive(bool value)
    {
        _exitHole.SetActive(value);
        _exitHoleCursor.SetActive(false);
        _exitHole.Interactable = false;
        _isExitHoleClicked = false;
    }

    public void BackHoleSetActive(bool value)
    {
        _backHole.SetActive(value);
        _backHoleCursor.SetActive(false);
        _backHole.Interactable = false;
        _isBackHoleClicked = false;
    }

    public void SetBuyHoleTargetObjectName(string name)
    {
        _buyHole.SetTargetObjectName(name);
    }


    public void StartTouch(UnityAction onButtonClicked)
    {
        _screenButton.gameObject.SetActive(true);
        _screenButton.onClick.RemoveAllListeners();
        _screenButton.onClick.AddListener(onButtonClicked);
    }

    public void StopTouch()
    {
        _screenButton.gameObject.SetActive(false);
        _screenButton.onClick.RemoveAllListeners();
    }

    private void OnShopButtonClicked()
    {
        _shopButton.gameObject.SetActive(false);
        _isShopButtonClicked = true;
    }

    private void OnTableHoleClicked()
    {
        _tableHole.SetActive(false);
        _isTableHoleClicked = true;
    }

    private void OnBuyHoleClicked()
    {
        _buyHole.SetActive(false);
    }

    private void OnExitHoleClicked()
    {
        _exitHole.SetActive(false);
        _isExitHoleClicked = true;
    }

    private void OnBackHoleClicked()
    {
        _backHole.SetActive(false);
        _isBackHoleClicked = true;
    }

    private void OnKitchenHoleClicked()
    {
        _kitchenHole.SetActive(false);
        _isKitchenHoleClicked = true;
    }


    private void OnShopMaskAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _shopButton.interactable = true;
            _shopMaskCursor.gameObject.SetActive(true);
        });
    }

    private void OnTableHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _tableHole.Interactable = true;
            _tableHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnKitchenHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _kitchenHole.Interactable = true;
            _kitchenHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnBuyHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _buyHole.Interactable = true;
            _buyHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnExitHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _exitHole.Interactable = true;
            _exitHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnBackHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _backHole.Interactable = true;
            _backHoleCursor.gameObject.SetActive(true);
        });
    }
}
