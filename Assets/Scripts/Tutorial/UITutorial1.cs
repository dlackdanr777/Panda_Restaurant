using UnityEngine;
using Muks.MobileUI;
using System;
using UnityEngine.UI;
using UnityEngine.Events;

public class UITutorial1 : MobileUIView
{
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private GameObject _uiPunchHole;
    [SerializeField] private GameObject _shopMask;
    [SerializeField] private HoleClickHandler _tableHole;
    [SerializeField] private HoleClickHandler _tableBuyHole;


    private bool _isShopButtonClicked;
    public bool IsShopButtonClicked => _isShopButtonClicked;

    private bool _isTableHoleClicked;
    public bool IsTableHoleClicked => _isTableHoleClicked;


    public override void Init()
    {
        _screenButton.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _uiPunchHole.gameObject.SetActive(false);
        _shopButton.gameObject.SetActive(false);
        _shopMask.gameObject.SetActive(false);
        _tableHole .SetActive(false);
        _tableBuyHole.SetActive(false);

        _tableHole.AddListener(OnTableHoleClicked);
        _tableBuyHole.AddListener(OnTableBuyHoleClicked);

        _shopButton.onClick.AddListener(OnShopButtonClicked);
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
        _tableBuyHole.SetActive(false);

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
    }

    public void ShopMaskSetActive(bool value)
    {
        _shopMask.gameObject.SetActive(value);
    }


    public void TableHoleSetActive(bool value)
    {
        _tableHole.SetActive(value);
    }

    public void TableBuyHoleSetActive(bool value)
    {
        _tableBuyHole.SetActive(value);
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

    private void OnTableBuyHoleClicked()
    {
        _tableBuyHole.SetActive(false);
    }
}
