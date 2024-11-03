using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UITutorial1 : MobileUIView
{
    [Header("Components")]
    [SerializeField] private TableManager _tableManager;

    [Space]
    [Header("Tutorial Components")]
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private UIAddCutomerController _addCustomerButton;
    [SerializeField] private Button _customerGuideButton;
    [SerializeField] private UIButtonAndImage _orderButton;
    [SerializeField] private UIButtonAndImage _servingButton;
    [SerializeField] private GameObject _uiPunchHole;


    [SerializeField] private HoleClickHandler _shopHole;
    [SerializeField] private HoleClickHandler _addCustomerHole;
    [SerializeField] private HoleClickHandler _tableHole;
    [SerializeField] private HoleClickHandler _kitchenHole;
    [SerializeField] private HoleClickHandler _recipeHole;
    [SerializeField] private HoleClickHandler _buyHole;
    [SerializeField] private HoleClickHandler _exitHole;
    [SerializeField] private HoleClickHandler _backHole;
    [SerializeField] private HoleClickHandler _customerGuideHole;
    [SerializeField] private HoleClickHandler _orderHole;

    [SerializeField] private EnabledScaleAnimation _addCustonerHoleAnime;
    [SerializeField] private EnabledScaleAnimation _shopMaskAnime;
    [SerializeField] private EnabledScaleAnimation _tableHoleAnime;
    [SerializeField] private EnabledScaleAnimation _kitchenHoleAnime;
    [SerializeField] private EnabledScaleAnimation _recipeHoleAnime;
    [SerializeField] private EnabledScaleAnimation _exitHoleAnime;
    [SerializeField] private EnabledScaleAnimation _buyHoleAnime;
    [SerializeField] private EnabledScaleAnimation _backHoleAnime;
    [SerializeField] private EnabledScaleAnimation _customerGuideHoleAnime;
    [SerializeField] private EnabledScaleAnimation _orderHoleAnime;

    [SerializeField] private GameObject _shopMaskCursor;
    [SerializeField] private GameObject _addCustomerHoleCursor;
    [SerializeField] private GameObject _tableHoleCursor;
    [SerializeField] private GameObject _kitchenHoleCursor;
    [SerializeField] private GameObject _recipeHoleCursor;
    [SerializeField] private GameObject _exitHoleCursor;
    [SerializeField] private GameObject _buyHoleCursor;
    [SerializeField] private GameObject _backHoleCursor;
    [SerializeField] private GameObject _customerGuideHoleCursor;
    [SerializeField] private GameObject _orderHoleCursor;


    private bool _isShopButtonClicked;
    public bool IsShopButtonClicked => _isShopButtonClicked;

    private bool _isTableHoleClicked;
    public bool IsTableHoleClicked => _isTableHoleClicked;

    private bool _isKitchenHoleClicked;
    public bool IsKitchenHoleClicked => _isKitchenHoleClicked;

    private bool _isRecipeHoleClicked;
    public bool IsRecipeHoleClicked => _isRecipeHoleClicked;

    private bool _isExitHoleClicked;
    public bool IsExitHoleClicked => _isExitHoleClicked;

    private bool _isBackHoleClicked;
    public bool IsBackHoleClicked => _isBackHoleClicked;

    private bool _isAddCustomerEventEnabled;
    public bool IsAddCustomerEventEnabled => _isAddCustomerEventEnabled;

    private bool _isCustomerGuideButtonClicked;
    public bool IsCustomerGuideButtonClicked => _isCustomerGuideButtonClicked;

    private bool _isOrderButtonClicked;
    public bool IsOrderButtonClicked => _isOrderButtonClicked;



    public override void Init()
    {
        _screenButton.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _uiPunchHole.gameObject.SetActive(false);
        _addCustomerButton.gameObject.SetActive(false);
        _shopButton.gameObject.SetActive(false);
        _customerGuideButton.gameObject.SetActive(false);
        _orderButton.gameObject.SetActive(false);
        _servingButton.gameObject.SetActive(false);

        _shopHole.SetActive(false);
        _addCustomerHole.SetActive(false);
        _tableHole .SetActive(false);
        _kitchenHole.SetActive(false);
        _recipeHole.SetActive(false);
        _buyHole.SetActive(false);
        _exitHole.SetActive(false);
        _backHole.SetActive(false);
        _customerGuideHole.SetActive(false);
        _orderHole.SetActive(false);

        _shopMaskCursor.SetActive(false);
        _addCustomerHoleCursor.SetActive(false);
        _tableHoleCursor.SetActive(false);
        _kitchenHoleCursor.SetActive(false);
        _recipeHoleCursor.SetActive(false);
        _buyHoleCursor.SetActive(false);
        _exitHoleCursor.SetActive(false);
        _backHoleCursor.SetActive(false);
        _customerGuideHoleCursor.SetActive(false);
        _orderHoleCursor.SetActive(false);

        _addCustomerButton.OnAddCustomerHandelr += OnAddCustomerEvent;
        _tableHole.AddListener(OnTableHoleClicked);
        _kitchenHole.AddListener(OnKitchenHoleClicked);
        _recipeHole.AddListener(OnRecipeHoleClicked);
        _buyHole.AddListener(OnBuyHoleClicked);
        _exitHole.AddListener(OnExitHoleClicked);
        _shopButton.onClick.AddListener(OnShopButtonClicked);
        _backHole.AddListener(OnBackHoleClicked);
        _customerGuideButton.onClick.AddListener(OnCustomerGuideButtonClicked);
        _orderButton.AddListener(OnOrderButtonClicked);
        _servingButton.AddListener(OnServingButtonClicked);

        _shopHole.SetTargetObjectName("Tutorial1 Shop Button");
        _addCustomerHole.SetTargetObjectName("Tutorial1 Add Customer Button");
        _tableHole.SetTargetObjectName("Slot1");
        _kitchenHole.SetTargetObjectName("Kichen Button");
        _recipeHole.SetTargetObjectName("Recipe Button");
        _buyHole.SetTargetObjectName("Buy Button");
        _exitHole.SetTargetObjectName("Exit Button");
        _backHole.SetTargetObjectName("Back Button");
        _customerGuideHole.SetTargetObjectName("Tutorial1 Guide Button");
        _orderHole.SetTargetObjectName("Tutorial1 Order Button");

        _shopMaskAnime.SetCallBack(() => _shopMaskCursor.SetActive(false), null, OnShopMaskAnimeCompleted);
        _addCustonerHoleAnime.SetCallBack(() => _addCustomerHoleCursor.SetActive(false), null, OnAddCustomerHoleAnimeCompleted);
        _tableHoleAnime.SetCallBack(() => _tableHoleCursor.SetActive(false), null, OnTableHoleAnimeCompleted);
        _kitchenHoleAnime.SetCallBack(() => _kitchenHoleCursor.SetActive(false), null, OnKitchenHoleAnimeCompleted);
        _recipeHoleAnime.SetCallBack(() => _recipeHoleCursor.SetActive(false), null, OnRecipeHoleAnimeCompleted);
        _buyHoleAnime.SetCallBack(() => _buyHoleCursor.SetActive(false), null, OnBuyHoleAnimeCompleted);
        _exitHoleAnime.SetCallBack(() => _exitHoleCursor.SetActive(false), null, OnExitHoleAnimeCompleted);
        _backHoleAnime.SetCallBack(() => _backHoleCursor.SetActive(false), null, OnBackHoleAnimeCompleted);
        _customerGuideHoleAnime.SetCallBack(() => _customerGuideHoleCursor.SetActive(false), null, OnCustomerGuideHoleAnimeCompleted);
        _orderHoleAnime.SetCallBack(() => _orderHoleCursor.SetActive(false), null, OnOrderHoleAnimeCompleted);

        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        _screenButton.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _uiPunchHole.gameObject.SetActive(false);
        _shopButton.gameObject.SetActive(false);
        _shopHole.SetActive(false);
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
        _shopMaskCursor.SetActive(false);
        _isShopButtonClicked = false;
        _shopButton.interactable = false;
    }

    public void ShopMaskSetActive(bool value)
    {
        _shopHole.SetActive(value);
        _shopMaskCursor.SetActive(false);
        _shopHole.Interactable = false;
        _isShopButtonClicked = false;
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

    public void RecipeHoleSetActive(bool value)
    {
        _recipeHole.SetActive(value);
        _recipeHoleCursor.SetActive(false);
        _recipeHole.Interactable = false;
        _isRecipeHoleClicked = false;
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

    public void AddCustomerButtonSetActive(bool value)
    {
        _addCustomerButton.gameObject.SetActive(value);
        _addCustomerHole.SetActive(false);
        _addCustomerHoleCursor.SetActive(false);
        _addCustomerHole.Interactable = false;
        _isAddCustomerEventEnabled = false;
    }

    public void AddCustomerHoleSetActive(bool value)
    {
        _addCustomerHole.SetActive(value);
        _addCustomerHoleCursor.SetActive(false);
        _addCustomerHole.Interactable = false;
        _isAddCustomerEventEnabled = false;
    }

    public void CustomerGuideButtonSetActive(bool value)
    {
        _customerGuideButton.gameObject.SetActive(value);
        _customerGuideHole.SetActive(false);
        _customerGuideHoleCursor.SetActive(false);
        _customerGuideHole.Interactable = false;
        _isAddCustomerEventEnabled = false;
    }


    public void CustomerGuideHoleSetActive(bool value)
    {
        _customerGuideHole.SetActive(value);
        _customerGuideHoleCursor.SetActive(false);
        _customerGuideHole.Interactable = false;
        _isAddCustomerEventEnabled = false;
    }

    public void OrderButtonSetActive(bool value)
    {
        _orderButton.gameObject.SetActive(value);
        _orderButton.SetImage(FoodDataManager.Instance.GetFoodData("FOOD01").Sprite);
        _orderHole.SetActive(false);
        _orderHoleCursor.SetActive(false);
        _orderHole.Interactable = false;
        _isOrderButtonClicked = false;
    }

    public void OrderHoleSetActive(bool value)
    {
        _orderHole.SetActive(value);
        _orderHoleCursor.SetActive(false);
        _orderHole.Interactable = false;
        _isOrderButtonClicked = false;
    }

    public void ServingButtonSetActive(bool value)
    {
        _servingButton.gameObject.SetActive(value);
        _servingButton.SetImage(FoodDataManager.Instance.GetFoodData("FOOD01").Sprite);
        _orderHole.SetActive(false);
        _orderHoleCursor.SetActive(false);
        _orderHole.Interactable = false;
        _isOrderButtonClicked = false;
    }


    public void SetBuyHoleTargetObjectName(string name)
    {
        _buyHole.SetTargetObjectName(name);
    }

    public void SetTableHoleTargetObjectName(string name)
    {
        _tableHole.SetTargetObjectName(name);
    }

    public void SetOrderHoleTargetObjectName(string name)
    {
        _orderHole.SetTargetObjectName(name);
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
        _shopHole.SetActive(false);
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

    private void OnRecipeHoleClicked()
    {
        _recipeHole.SetActive(false);
        _isRecipeHoleClicked = true;
    }


    private void OnShopMaskAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _shopButton.interactable = true;
            _shopHole.Interactable = true;
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

    private void OnRecipeHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _recipeHole.Interactable = true;
            _recipeHoleCursor.gameObject.SetActive(true);
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

    private void OnAddCustomerHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _addCustomerHole.Interactable = true;
            _addCustomerHoleCursor.SetActive(true);
        });
    }

    private void OnCustomerGuideHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _customerGuideHole.Interactable = true;
            _customerGuideHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnOrderHoleAnimeCompleted()
    {
        Tween.Wait(0.5f, () =>
        {
            _orderHole.Interactable = true;
            _orderHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnAddCustomerEvent()
    {
        _isAddCustomerEventEnabled = true;
    }

    private void OnCustomerGuideButtonClicked()
    {
        _tableManager.OnCustomerGuideButtonClicked(0);

        _customerGuideHole.SetActive(false);
        _customerGuideButton.gameObject.SetActive(false);
        _isCustomerGuideButtonClicked = true;
    }

    private void OnOrderButtonClicked()
    {
        _tableManager.OnCustomerOrder(0);
        _orderHole.SetActive(false);
        _orderButton.gameObject.SetActive(false);
        _isOrderButtonClicked = true;
    }

    private void OnServingButtonClicked()
    {
        _tableManager.OnServing(0);
        _orderHole.SetActive(false);
        _servingButton.gameObject.SetActive(false);
        _isOrderButtonClicked = true;
    }
}
