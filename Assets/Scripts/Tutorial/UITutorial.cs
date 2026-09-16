using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UITutorial : MobileUIView
{
    [Header("Components")]
    [SerializeField] private TableManager _tableManager;

    [Space]
    [Header("Tutorial Components")]
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _shopButton;
    [SerializeField] private UIAddCutomerController _addCustomerButton;
    [SerializeField] private Button _customerGuideButton;
    [SerializeField] private Button _table1Button;
    [SerializeField] private ButtonPressEffect _gacha1Button;
    public ButtonPressEffect Gacha1Button => _gacha1Button;
    private RectTransform _gacha1ButtonReference;
    [SerializeField] private TableButton _orderButton;
    [SerializeField] private TableButton _servingButton;
    [SerializeField] private GameObject _cookTimer;
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
        [SerializeField] private HoleClickHandler _servingHole;
    [SerializeField] private HoleClickHandler _table1Hole;

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
    [SerializeField] private EnabledScaleAnimation _servingHoleAnime;
    [SerializeField] private EnabledScaleAnimation _table1HoleAnime;

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
    [SerializeField] private GameObject _servingHoleCursor;
    [SerializeField] private GameObject _table1HoleCursor;

    [Space]
    [SerializeField] private HoleClickHandler _customHole;
    [SerializeField] private EnabledScaleAnimation _customHoleAnime;
    [SerializeField] private RectTransform _customHoleCursorParent;
    [SerializeField] private GameObject _customHoleCursorUp;
    [SerializeField] private GameObject _customHoleCursorDown;
    private bool _isCustomCursorUp;

    private bool _isButtonClicked;
    public bool IsButtonClicked => _isButtonClicked;

    private int _guideLifetime;
    private readonly Dictionary<HoleClickHandler, int> _holeLifetimes = new Dictionary<HoleClickHandler, int>();
    private CanvasGroup _guidanceCanvasGroup;
    private bool _guidancePassthrough, _previousGuidanceRaycasts;


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
        _table1Button.gameObject.SetActive(false);
        _cookTimer.SetActive(false);
        _gacha1Button.gameObject.SetActive(false);

        SetGuideHoleActive(_shopHole, false);
        SetGuideHoleActive(_addCustomerHole, false);
        SetGuideHoleActive(_tableHole, false);
        SetGuideHoleActive(_kitchenHole, false);
        SetGuideHoleActive(_recipeHole, false);
        SetGuideHoleActive(_buyHole, false);
        SetGuideHoleActive(_exitHole, false);
        SetGuideHoleActive(_backHole, false);
        SetGuideHoleActive(_customerGuideHole, false);
        SetGuideHoleActive(_orderHole, false);
        SetGuideHoleActive(_servingHole, false);
        SetGuideHoleActive(_table1Hole, false);
        SetGuideHoleActive(_customHole, false);

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
        _servingHoleCursor.SetActive(false);
        _table1HoleCursor.SetActive(false);
        _customHoleCursorParent.gameObject.SetActive(false);

        _addCustomerButton.OnAddCustomerHandelr += OnButtonClickEvent;
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
        _table1Button.onClick.AddListener(OnTable1ButtonClicked);
        _customHole.AddListener(OnCustomHoleClicked);

        _shopHole.SetTargetObjectName("Tutorial Shop Button");
        _addCustomerHole.SetTargetObjectName("Tutorial Add Customer Button");
        _tableHole.SetTargetObjectName("Slot1");
        _kitchenHole.SetTargetObjectName("Kichen Button");
        _recipeHole.SetTargetObjectName("Recipe Button");
        _buyHole.SetTargetObjectName("Buy Button");
        _exitHole.SetTargetObjectName("Exit Button");
        _backHole.SetTargetObjectName("Back Button");
        _customerGuideHole.SetTargetObjectName("Tutorial Guide Button");
        _orderHole.SetTargetObjectName("Tutorial Order Button");
        _servingHole.SetTargetObjectName("Tutorial Serving Button");
        _table1Hole.SetTargetObjectName("Table1 Button");


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
        _table1HoleAnime.SetCallBack(() => _table1HoleCursor.SetActive(false), null, OnTable1HoleAnimeCompleted);
        _customHoleAnime.SetCallBack(() => _customHoleCursorParent.gameObject.SetActive(false), null, OnCustomHoleAnimeCompleted);
        _servingHoleAnime.SetCallBack(() => _servingHoleCursor.SetActive(false), null, OnServingHoleAnimeCompleted);

        _orderButton.Init();
        _servingButton.Init();

        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        EndGuidancePassthrough();
        ResetGuideIndicators();
        _screenButton.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _uiPunchHole.gameObject.SetActive(false);
        _shopButton.gameObject.SetActive(false);

        VisibleState = VisibleState.Appeared;
        gameObject.SetActive(true);
    }

    public override void Hide()
    {
        Gacha1ButtonSetActive(false);
        EndGuidancePassthrough();
        ResetGuideIndicators();
        _screenButton.gameObject.SetActive(false);
        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        _gacha1ButtonReference = null;
        if (_gacha1Button != null) _gacha1Button.gameObject.SetActive(false);
        EndGuidancePassthrough();
        ResetGuideIndicators();
        if (_uiPunchHole != null) _uiPunchHole.SetActive(false);
        if (_screenButton != null) _screenButton.gameObject.SetActive(false);
    }

    public void BeginGuidancePassthrough(RectTransform target)
    {
        if (!_guidancePassthrough)
        {
            if (_guidanceCanvasGroup == null)
            {
                // UnityEngine.Object overloads == to treat destroyed components as
                // null. The null-coalescing operator does not use that overload,
                // so stale references from a rebuilt view are replaced first.
                var group = GetComponent<CanvasGroup>();
                if (group == null)
                    group = gameObject.AddComponent<CanvasGroup>();
                _guidanceCanvasGroup = group;
            }
            _previousGuidanceRaycasts = _guidanceCanvasGroup.blocksRaycasts;
            _guidanceCanvasGroup.blocksRaycasts = false;
            _guidancePassthrough = true;
        }
        ScreenButtonSetActive(false);
        PunchHoleSetActive(target != null);
        if (target != null)
            CustomHoleSetActive(true, Mathf.Max(230f, target.rect.width + 50f), target.name, target, false);
    }

    public void EndGuidancePassthrough()
    {
        if (_guidancePassthrough && _guidanceCanvasGroup != null)
            _guidanceCanvasGroup.blocksRaycasts = _previousGuidanceRaycasts;
        _guidancePassthrough = false;
    }

    private void ResetGuideIndicators()
    {
        // Tween.Wait is global, not a child of this view. Old callbacks must not
        // revive a pointer/input after hiding or reusing the tutorial overlay.
        ++_guideLifetime;
        ResetGuideHole(_shopHole, _shopMaskCursor);
        ResetGuideHole(_addCustomerHole, _addCustomerHoleCursor);
        ResetGuideHole(_tableHole, _tableHoleCursor);
        ResetGuideHole(_kitchenHole, _kitchenHoleCursor);
        ResetGuideHole(_recipeHole, _recipeHoleCursor);
        ResetGuideHole(_buyHole, _buyHoleCursor);
        ResetGuideHole(_exitHole, _exitHoleCursor);
        ResetGuideHole(_backHole, _backHoleCursor);
        ResetGuideHole(_customerGuideHole, _customerGuideHoleCursor);
        ResetGuideHole(_orderHole, _orderHoleCursor);
        ResetGuideHole(_servingHole, _servingHoleCursor);
        ResetGuideHole(_table1Hole, _table1HoleCursor);
        ResetGuideHole(_customHole, _customHoleCursorParent == null ? null : _customHoleCursorParent.gameObject);
        if (_customHoleCursorUp != null) _customHoleCursorUp.SetActive(false);
        if (_customHoleCursorDown != null) _customHoleCursorDown.SetActive(false);
        if (_table1Button != null) _table1Button.gameObject.SetActive(false);
        if (_shopButton != null) _shopButton.interactable = false;
    }

    private void ResetGuideHole(HoleClickHandler hole, GameObject cursor)
    {
        if (hole != null) SetGuideHoleActive(hole, false);
        if (cursor != null) cursor.SetActive(false);
    }

    private void SetGuideHoleActive(HoleClickHandler hole, bool value)
    {
        int lifetime;
        _holeLifetimes.TryGetValue(hole, out lifetime);
        _holeLifetimes[hole] = lifetime + 1;
        hole.Interactable = false;
        hole.SetActive(value);
    }

    private void ScheduleGuideReveal(HoleClickHandler hole, Action reveal)
    {
        if (!gameObject.activeInHierarchy || VisibleState != VisibleState.Appeared || !hole.gameObject.activeInHierarchy)
            return;

        int guideLifetime = _guideLifetime;
        int holeLifetime;
        _holeLifetimes.TryGetValue(hole, out holeLifetime);
        Tween.Wait(0.05f, () =>
        {
            int currentHoleLifetime;
            if (this == null || hole == null || !gameObject.activeInHierarchy ||
                VisibleState != VisibleState.Appeared || !hole.gameObject.activeInHierarchy ||
                guideLifetime != _guideLifetime || !_holeLifetimes.TryGetValue(hole, out currentHoleLifetime) ||
                holeLifetime != currentHoleLifetime)
                return;

            reveal();
        });
    }


    public void ScreenButtonSetActive(bool value)
    {
        _screenButton.gameObject.SetActive(value);
    }


    public void PunchHoleSetActive(bool value)
    {
        if (!value) ResetGuideIndicators();
        _uiPunchHole.SetActive(value);
    }

    public void ShopButtonSetActive(bool value)
    {
        _shopButton.gameObject.SetActive(value);
        _shopMaskCursor.SetActive(false);
        _isButtonClicked = false;
        _shopButton.interactable = false;
    }

    public void ShopMaskSetActive(bool value)
    {
        SetGuideHoleActive(_shopHole, value);
        _shopMaskCursor.SetActive(false);
        _shopHole.Interactable = false;
        _isButtonClicked = false;
    }


    public void TableHoleSetActive(bool value)
    {
        SetGuideHoleActive(_tableHole, value);
        _tableHoleCursor.SetActive(false);
        _tableHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void KitchenHoleSetActive(bool value)
    {
        SetGuideHoleActive(_kitchenHole, value);
        _kitchenHoleCursor.SetActive(false);
        _kitchenHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void RecipeHoleSetActive(bool value)
    {
        SetGuideHoleActive(_recipeHole, value);
        _recipeHoleCursor.SetActive(false);
        _recipeHole.Interactable = false;
        _isButtonClicked = false;
    }


    public void BuyHoleSetActive(bool value)
    {
        SetGuideHoleActive(_buyHole, value);
        _buyHoleCursor.SetActive(false);
        _buyHole.Interactable = false;
    }

    public void ExitHoleSetActive(bool value)
    {
        SetGuideHoleActive(_exitHole, value);
        _exitHoleCursor.SetActive(false);
        _exitHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void BackHoleSetActive(bool value)
    {
        SetGuideHoleActive(_backHole, value);
        _backHoleCursor.SetActive(false);
        _backHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void AddCustomerButtonSetActive(bool value)
    {
        _addCustomerButton.gameObject.SetActive(value);
        SetGuideHoleActive(_addCustomerHole, false);
        _addCustomerHoleCursor.SetActive(false);
        _addCustomerHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void AddCustomerHoleSetActive(bool value)
    {
        SetGuideHoleActive(_addCustomerHole, value);
        _addCustomerHoleCursor.SetActive(false);
        _addCustomerHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void CustomerGuideButtonSetActive(bool value)
    {
        _customerGuideButton.gameObject.SetActive(value);
        SetGuideHoleActive(_customerGuideHole, false);
        _customerGuideHoleCursor.SetActive(false);
        _customerGuideHole.Interactable = false;
        _isButtonClicked = false;
    }


    public void CustomerGuideHoleSetActive(bool value)
    {
        SetGuideHoleActive(_customerGuideHole, value);
        _customerGuideHoleCursor.SetActive(false);
        _customerGuideHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void OrderButtonSetActive(bool value)
    {
        _orderButton.gameObject.SetActive(value);
        DebugLog.Log("¿À´õ È¦: " + value);
        _orderButton.SetData(FoodDataManager.Instance.GetFoodData("FOOD01"));
        SetGuideHoleActive(_orderHole, false);
        _orderHoleCursor.SetActive(false);
        _orderHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void OrderHoleSetActive(bool value)
    {
        SetGuideHoleActive(_orderHole, value);
        _orderHoleCursor.SetActive(false);
        _orderHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void ServingButtonSetActive(bool value)
    {
        _servingButton.gameObject.SetActive(value);
        _servingButton.SetData(FoodDataManager.Instance.GetFoodData("FOOD01"));
        SetGuideHoleActive(_servingHole, false);
        _servingHoleCursor.SetActive(false);
        _servingHole.Interactable = false;
        _isButtonClicked = false;
    }

    public void ServingHoleSetActive(bool value)
    {
        SetGuideHoleActive(_servingHole, value);
        _servingHoleCursor.SetActive(false);
        _servingHole.Interactable = false;
        _isButtonClicked = false;
    }


    public void CookTimerSetActive(bool value)
    {
        _cookTimer.SetActive(value);
    }

    public void Table1HoleSetActive(bool value)
    {
        SetGuideHoleActive(_table1Hole, value);
        _table1Button.gameObject.SetActive(false);
        _table1HoleCursor.SetActive(false);
        _table1Hole.Interactable = false;
        _isButtonClicked = false;
    }

    public void CustomHoleSetActive(bool value, float holeDiameter, string targetObjName, Transform pos, bool isCursorUp = true)
    {
        SetGuideHoleActive(_customHole, value);
        _customHole.HoleRect.sizeDelta = new Vector2(holeDiameter, holeDiameter);
        _customHole.SetTargetObjectName(targetObjName);
        _customHoleCursorParent.gameObject.SetActive(false);

        // RectTransform?? ???? ??
        RectTransform rectTransform = pos as RectTransform;
        if (rectTransform != null)
        {
            // RectTransform?? ??? ??
            _customHole.HoleRect.transform.position = rectTransform.position;
            _customHoleCursorParent.transform.position = rectTransform.position;
        }
        else
        {
            // ?? Transform?? World to Screen ??
            Vector3 screenPos = Camera.main.WorldToScreenPoint(pos.position);
            _customHole.HoleRect.transform.position = screenPos;
            _customHoleCursorParent.transform.position = screenPos;
        }

        _customHoleCursorParent.sizeDelta = new Vector2(holeDiameter, holeDiameter);
        _customHole.Interactable = true;
        _isButtonClicked = false;
        _isCustomCursorUp = isCursorUp;
    }

    public void CustomHoleHide()
    {
        SetGuideHoleActive(_customHole, false);
        _customHoleCursorParent.gameObject.SetActive(false);
    }

    // ????? ??? ?? ?? Interactable = true? ?? (?? ???)
    public void CustomHoleSetActiveImmediate(bool value, float holeDiameter, string targetObjName, Transform pos, bool isCursorUp = true)
    {
        SetGuideHoleActive(_customHole, value);
        _customHole.HoleRect.sizeDelta = new Vector2(holeDiameter, holeDiameter);
        _customHole.SetTargetObjectName(targetObjName);

        RectTransform rectTransform = pos as RectTransform;
        if (rectTransform != null)
        {
            _customHole.HoleRect.transform.position = rectTransform.position;
            _customHoleCursorParent.transform.position = rectTransform.position;
        }
        else
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(pos.position);
            _customHole.HoleRect.transform.position = screenPos;
            _customHoleCursorParent.transform.position = screenPos;
        }

        _customHoleCursorParent.sizeDelta = new Vector2(holeDiameter, holeDiameter);
        _customHole.Interactable = true;
        _isButtonClicked = false;
        _isCustomCursorUp = isCursorUp;
        _customHoleCursorParent.gameObject.SetActive(true);
        _customHoleCursorUp.gameObject.SetActive(isCursorUp);
        _customHoleCursorDown.gameObject.SetActive(!isCursorUp);
    }
    
    public bool GetCustomHoleActive()
    {
        return _customHole.gameObject.activeInHierarchy;
    }


    public void Gacha1ButtonSetActive(bool value, RectTransform reference = null)
    {
        _gacha1ButtonReference = value ? reference : null;
        _gacha1Button.gameObject.SetActive(value);
        _isButtonClicked = false;
        if (value)
        {
            Canvas.ForceUpdateCanvases();
            UpdateGacha1ButtonLayout();
        }
    }

    private void LateUpdate()
    {
        if (_gacha1ButtonReference != null && _gacha1Button.gameObject.activeSelf)
            UpdateGacha1ButtonLayout();
    }

    private void UpdateGacha1ButtonLayout()
    {
        var target = _gacha1Button != null ? _gacha1Button.transform as RectTransform : null;
        var source = _gacha1ButtonReference;
        var parent = target != null ? target.parent as RectTransform : null;
        if (source == null || target == null || parent == null) return;

        // The normal button and tutorial overlay belong to different canvases.
        // Copy the rendered rectangle, not anchoredPosition from another parent.
        Vector3 width = parent.InverseTransformVector(source.TransformVector(Vector3.right * source.rect.width));
        Vector3 height = parent.InverseTransformVector(source.TransformVector(Vector3.up * source.rect.height));
        Vector3 center = source.TransformPoint(source.rect.center);
        target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
        target.pivot = new Vector2(0.5f, 0.5f);
        target.sizeDelta = new Vector2(width.magnitude, height.magnitude);
        target.rotation = source.rotation;
        target.position = center;
        // Keep the press-effect scale under its own control. Only follow the
        // source layout and pointer position while this particular input is up.
        if (_customHole != null && _customHole.gameObject.activeInHierarchy)
        {
            _customHole.HoleRect.position = center;
            _customHoleCursorParent.position = center;
        }
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

    public void SetGacha1ButtonClickEvent(Action action)
    {
        _gacha1Button.RemoveAllListeners();
        _gacha1Button.AddListener(action);
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
        SetGuideHoleActive(_shopHole, false);
        _isButtonClicked = true;
    }

    private void OnTableHoleClicked()
    {
        SetGuideHoleActive(_tableHole, false);
        _isButtonClicked = true;
    }

    private void OnBuyHoleClicked()
    {
        SetGuideHoleActive(_buyHole, false);
    }

    private void OnExitHoleClicked()
    {
        SetGuideHoleActive(_exitHole, false);
        _isButtonClicked = true;
    }

    private void OnBackHoleClicked()
    {
        SetGuideHoleActive(_backHole, false);
        _isButtonClicked = true;
    }

    private void OnKitchenHoleClicked()
    {
        SetGuideHoleActive(_kitchenHole, false);
        _isButtonClicked = true;
    }

    private void OnRecipeHoleClicked()
    {
        SetGuideHoleActive(_recipeHole, false);
        _isButtonClicked = true;
    }

    private void OnCustomHoleClicked()
    {
        SetGuideHoleActive(_customHole, false);
        _isButtonClicked = true;
    }


    private void OnShopMaskAnimeCompleted()
    {
        ScheduleGuideReveal(_shopHole, () =>
        {
            _shopButton.interactable = true;
            _shopHole.Interactable = true;
            _shopMaskCursor.gameObject.SetActive(true);
        });
    }

    private void OnTableHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_tableHole, () =>
        {
            _tableHole.Interactable = true;
            _tableHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnKitchenHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_kitchenHole, () =>
        {
            _kitchenHole.Interactable = true;
            _kitchenHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnRecipeHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_recipeHole, () =>
        {
            _recipeHole.Interactable = true;
            _recipeHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnBuyHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_buyHole, () =>
        {
            _buyHole.Interactable = true;
            _buyHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnExitHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_exitHole, () =>
        {
            _exitHole.Interactable = true;
            _exitHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnBackHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_backHole, () =>
        {
            _backHole.Interactable = true;
            _backHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnAddCustomerHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_addCustomerHole, () =>
        {
            _addCustomerHole.Interactable = true;
            _addCustomerHoleCursor.SetActive(true);
        });
    }

    private void OnCustomerGuideHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_customerGuideHole, () =>
        {
            _customerGuideHole.Interactable = true;
            _customerGuideHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnOrderHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_orderHole, () =>
        {
            _orderHole.Interactable = true;
            _orderHoleCursor.gameObject.SetActive(true);
        });
    }
    
        private void OnServingHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_servingHole, () =>
        {
            _servingHole.Interactable = true;
            _servingHoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnTable1HoleAnimeCompleted()
    {
        ScheduleGuideReveal(_table1Hole, () =>
        {
            _table1Hole.Interactable = true;
            _table1Button.gameObject.SetActive(true);
            _table1HoleCursor.gameObject.SetActive(true);
        });
    }

    private void OnCustomHoleAnimeCompleted()
    {
        ScheduleGuideReveal(_customHole, () =>
        {
            // Interactable? CustomHoleSetActive?? ?? true? ???
            _customHoleCursorParent.gameObject.SetActive(_customHole.gameObject.activeInHierarchy);
            _customHoleCursorUp.gameObject.SetActive(_isCustomCursorUp && _customHole.gameObject.activeInHierarchy);
            _customHoleCursorDown.gameObject.SetActive(!_isCustomCursorUp && _customHole.gameObject.activeInHierarchy);
        });
    }

    private void OnButtonClickEvent()
    {
        _isButtonClicked = true;
    }

    private void OnCustomerGuideButtonClicked()
    {
        if (!_tableManager.OnCustomerGuideEventPlayUISound(0))
            return;

        SetGuideHoleActive(_customerGuideHole, false);
        _customerGuideButton.gameObject.SetActive(false);
        _isButtonClicked = true;
    }

    private void OnOrderButtonClicked()
    {
        TableData data = _tableManager.GetTableData(ERestaurantFloorType.Floor1, TableType.Table1);
        _tableManager.OnCustomerOrder(data);
        SetGuideHoleActive(_orderHole, false);
        _orderButton.gameObject.SetActive(false);
        _isButtonClicked = true;
    }

    private void OnServingButtonClicked()
    {
        TableData data = _tableManager.GetTableData(ERestaurantFloorType.Floor1, TableType.Table1);
        _tableManager.OnServing(data);
        SetGuideHoleActive(_orderHole, false);
        _servingButton.gameObject.SetActive(false);
        _isButtonClicked = true;
    }

    private void OnTable1ButtonClicked()
    {
        _table1Button.gameObject.SetActive(false);
        SetGuideHoleActive(_table1Hole, false);

        TableData data = _tableManager.GetTableData(ERestaurantFloorType.Floor1, TableType.Table1);
        data.DropGarbageArea.CleanGarbage();
        for(int i = 0, cnt = data.DropCoinAreas.Length; i < cnt; ++i)
        {
            data.DropCoinAreas[i].GiveCoin();
        }

        _isButtonClicked = true;
    }
}
