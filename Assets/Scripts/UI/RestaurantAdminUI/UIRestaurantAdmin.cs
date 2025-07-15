using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;


public class UIRestaurantAdmin : MobileUIView
{
    [Header("Components")]
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private UIStaff _staffUI;
    [SerializeField] private UIFurniture _furnitureUI;
    [SerializeField] private UIKitchen _kitchenUI;
    [SerializeField] private GameObject _mainUI;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIFloorButtonGroup _floorButtonGroup;
    [SerializeField] private RectTransform _dontTouchArea;

    [Header("BackgroundImage")]
    [SerializeField] private ScrollingImage[] _scrollImages;
    private ScrollingImage _currentScrollImage;

    [Space]
    [Header("Tabs")]
    [SerializeField] private UIFurnitureTab _furnitureTab;
    [SerializeField] private UIStaffTab _staffTab;
    [SerializeField] private UIRecipeTab _recipeTab;
    [SerializeField] private UIKitchenTab _kitchenTab;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIRestaurantAdminTabButton _furnitureButton;
    [SerializeField] private UIRestaurantAdminTabButton _staffButton;
    [SerializeField] private UIRestaurantAdminTabButton _recipeButton;
    [SerializeField] private UIRestaurantAdminTabButton _kitchenButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _shopMusic;

    private ERestaurantFloorType _floorType;
    private bool _isInitialized = false;

    // 캐시된 참조들
    private UIRestaurantAdminTabButton[] _tabButtons;
    private UIRestaurantAdminTab[] _tabs;

    public override void Init()
    {
        if (_isInitialized) return;

        // 배열 캐싱으로 반복 접근 최적화
        _tabButtons = new UIRestaurantAdminTabButton[] 
        { 
            _furnitureButton, _staffButton, _recipeButton, _kitchenButton 
        };
        
        _tabs = new UIRestaurantAdminTab[] 
        { 
            _furnitureTab, _staffTab, _recipeTab, _kitchenTab 
        };

        // 이벤트 등록
        _staffButton.OnClickEvent(ShowStaffTab);
        _furnitureButton.OnClickEvent(ShowFurnitureTab);
        _recipeButton.OnClickEvent(ShowRecipeTab);
        _kitchenButton.OnClickEvent(ShowKitchenTab);

        // 탭 초기화
        _staffTab.Init();
        _recipeTab.Init();
        _furnitureTab.Init();
        _kitchenTab.Init();

        _floorButtonGroup.Init(
            () => ChangeFloorType(ERestaurantFloorType.Floor1), 
            () => ChangeFloorType(ERestaurantFloorType.Floor2), 
            () => ChangeFloorType(ERestaurantFloorType.Floor3)
        );

        // 스크롤 이미지 초기화 최적화
        InitializeScrollImages();

        SetBackgroundImageOptimized(_floorType);

        _isInitialized = true;
        gameObject.SetActive(false);
    }

    private void InitializeScrollImages()
    {
        int length = _scrollImages.Length;
        for (int i = 0; i < length; i++)
        {
            _scrollImages[i].Init();
            _scrollImages[i].gameObject.SetActive(false); // 미리 비활성화
        }
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_shopMusic, 0.5f);
        gameObject.SetActive(true);
        _mainUI.SetActive(false);
        
        ShowFurnitureTabOptimized();
        
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0;
        _dontTouchArea.gameObject.SetActive(true);
        ChangeFloorTypeOptimized(_mainScene.CurrentFloor);

        _recipeTab.UpdateUI();

        TweenData tween = _canvasGroup.TweenAlpha(1, 0.1f);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _mainUI.SetActive(true);
            _mainUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            TweenData tween2 = _mainUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
            tween2.OnComplete(() => 
            {
                _canvasGroup.blocksRaycasts = true;
                _dontTouchArea.gameObject.SetActive(false);
            });
        });
    }

    public override void Hide()
    {
        if(_mainUI.activeSelf)
        {
            VisibleState = VisibleState.Disappearing;
            _mainScene.PlayMainMusic();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 1;
            _dontTouchArea.gameObject.SetActive(true);
            _mainUI.transform.localScale = Vector3.one;
            TweenData tween = _mainUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
            tween.OnComplete(() =>
            {
                _mainUI.SetActive(false);
                ResetBackgroundImageOffsetOptimized();
                TweenData tween2 = _canvasGroup.TweenAlpha(0, 0.1f);
                tween2.OnComplete(() => 
                {
                    gameObject.SetActive(false);
                    _dontTouchArea.gameObject.SetActive(false);
                });
                VisibleState = VisibleState.Disappeared;
            });
        }
        else
        {
            _mainScene.PlayMainMusic();
            VisibleState = VisibleState.Disappeared;
            
            // 최적화된 UI 팝 처리
            PopActiveUIViews();

            _mainUI.SetActive(false);
            Tween.Wait(_hideDuration, () =>
            {
                TweenData tween2 = _canvasGroup.TweenAlpha(0, 0.1f);
                tween2.OnComplete(() => gameObject.SetActive(false));
                VisibleState = VisibleState.Disappeared;
            });
        }
    }

    private void PopActiveUIViews()
    {
        // 문자열 배열을 미리 캐시하여 반복 생성 방지
        string[] uiViewNames = { "UIStaff", "UIFurniture", "UIKitchen" };
        
        for (int i = 0; i < uiViewNames.Length; i++)
        {
            if (_uiNav.CheckActiveView(uiViewNames[i]))
                _uiNav.Pop(uiViewNames[i]);
        }
    }

    public void MainUISetActive(bool active)
    {
        if (VisibleState == VisibleState.Disappeared || VisibleState == VisibleState.Disappearing)
            _uiNav.PushNoAnime("RestaurantAdminUI");

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1;
        _dontTouchArea.gameObject.SetActive(false);
        _mainUI.transform.localScale = Vector3.one;
        _mainUI.SetActive(active);
    }

    // 최적화된 탭 표시 메서드들
    public void ShowStaffTab()
    {
        ShowStaffTabOptimized();
    }

    private void ShowStaffTabOptimized()
    {
        SetTabActive(1); // Staff = 1
        _floorButtonGroup.SetActive(true);
    }

    public void ShowFurnitureTab()
    {
        ShowFurnitureTabOptimized();
    }

    private void ShowFurnitureTabOptimized()
    {
        SetTabActive(0); // Furniture = 0
        _floorButtonGroup.SetActive(true);
    }

    public void ShowRecipeTab()
    {
        SetTabActive(2); // Recipe = 2
        _floorButtonGroup.SetActive(false);
    }

    public void ShowKitchenTab()
    {
        SetTabActive(3); // Kitchen = 3
        _floorButtonGroup.SetActive(true);
    }

    // 탭 활성화 로직 통합 및 최적화
    private void SetTabActive(int activeIndex)
    {
        // 탭 버튼 상태 설정
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            if (i == activeIndex)
                _tabButtons[i].SelectButton();
            else
                _tabButtons[i].UnselectedButton();
        }

        // 탭 Attention 상태 설정
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (i == activeIndex)
            {
                _tabs[i].SetAttention();
                _tabs[i].transform.SetAsLastSibling();
            }
            else
            {
                _tabs[i].SetNotAttention();
            }
        }
    }

    public void ShowUIFurniture(FurnitureType type)
    {
        _furnitureTab.ShowUIFurniture(type);
    }

    public void ShowUIStaff(EquipStaffType type)
    {
        _staffTab.ShowUIStaff(type);
    }

    public void ShowUIKitchen(KitchenUtensilType type)
    {
        _kitchenTab.ShowUIKitchen(type);
    }

    // 최적화된 배경 이미지 설정
    private void SetBackgroundImageOptimized(ERestaurantFloorType floor)
    {

        // 이전 배경 비활성화
        if (_currentScrollImage != null)
            _currentScrollImage.gameObject.SetActive(false);

        // 오프셋 설정 및 새 배경 활성화
        ScrollingImage newScrollImage = _scrollImages[(int)floor];
        if (_currentScrollImage != null)
            newScrollImage.SetOffset(_currentScrollImage.Offset);

        newScrollImage.gameObject.SetActive(true);
        _currentScrollImage = newScrollImage;
    }

    private void ResetBackgroundImageOffsetOptimized()
    {
        int length = _scrollImages.Length;
        for (int i = 0; i < length; i++)
        {
            _scrollImages[i].SetOffset(Vector2.zero);
        }
    }

    private void ChangeFloorType(ERestaurantFloorType floorType)
    {
        ChangeFloorTypeOptimized(floorType);
    }

    private void ChangeFloorTypeOptimized(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;
        // 한 번에 모든 탭 업데이트
        _kitchenTab.ChangeFloorType(_floorType);
        _furnitureTab.ChangeFloorType(_floorType);
        _staffTab.ChangeFloorType(_floorType);
        _floorButtonGroup.SetFloorText(_floorType);

        SetBackgroundImageOptimized(_floorType);
    }
}

