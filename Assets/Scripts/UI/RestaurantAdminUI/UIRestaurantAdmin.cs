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
    [SerializeField] private GameObject _floor1Object;
    [SerializeField] private GameObject _vipFloorObject;

    [Header("BackgroundImage")]
    [SerializeField] private ScrollingImage[] _scrollImages;
    private ScrollingImage _currentScrollImage;

    [Space]
    [Header("Floor Transition")]
    [SerializeField] private Material _transitionMaterial;
    [SerializeField] private GameObject _transitionOverlay;
    [SerializeField] private UnityEngine.UI.Image _normalTransitionImage;
    [SerializeField] private UnityEngine.UI.Image _vipTransitionImage;
    [SerializeField] private float _transitionDuration = 0.8f;
    [SerializeField] private float _transitionAngle = -45f;
    [SerializeField] private float _edgeWidth = 0.15f;
    [SerializeField] private Color _innerEdgeColor = Color.yellow;
    [SerializeField] private Color _outerEdgeColor = Color.white;

    [Space]
    [Header("Floor Button Groups")]
    [SerializeField] private GameObject _floor1ButtonGroup;
    [SerializeField] private GameObject[] _vipRoomButtonGroups;

    [Space]
    [Header("Tabs")]
    [SerializeField] private UIFurnitureTab _furnitureTab;
    [SerializeField] private UIStaffTab _staffTab;
    [SerializeField] private UIRecipeTab _recipeTab;
    [SerializeField] private UIKitchenTab _kitchenTab;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIRestaurantAdminTabButton[] _furnitureButtons;
    [SerializeField] private UIRestaurantAdminTabButton[] _staffButtons;
    [SerializeField] private UIRestaurantAdminTabButton[] _recipeButtons;
    [SerializeField] private UIRestaurantAdminTabButton[] _kitchenButtons;

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
    private ERestaurantFloorType _previousFloorType;
    private bool _isInitialized = false;

    // 캐시된 참조들
    private UIRestaurantAdminTab[] _tabs;

    private Vector3 _tmpScale;
    private Material _transitionMaterialInstance;
    private Coroutine _transitionCoroutine;

    private bool _isClosingSession;
    private bool _isFailSafeCloseStarted;
    private int _sessionVersion;
    private MobileUIView _nativeHideView;

    public override void Init()
    {
        if (_isInitialized) return;

        // Material 인스턴스 생성
        if (_transitionMaterial != null && _transitionOverlay != null)
        {
            _transitionMaterialInstance = new Material(_transitionMaterial);
            
            if (_normalTransitionImage != null)
                _normalTransitionImage.material = _transitionMaterialInstance;
            if (_vipTransitionImage != null)
                _vipTransitionImage.material = _transitionMaterialInstance;
            
            _transitionMaterialInstance.SetFloat("_Angle", _transitionAngle);
            _transitionMaterialInstance.SetFloat("_EdgeWidth", _edgeWidth);
            _transitionMaterialInstance.SetColor("_InnerEdgeColor", _innerEdgeColor);
            _transitionMaterialInstance.SetColor("_OuterEdgeColor", _outerEdgeColor);
            _transitionMaterialInstance.SetFloat("_Progress", 0);
        }

        DeactivateTransitionOverlay();

        // 배열 캐싱으로 반복 접근 최적화
        _tabs = new UIRestaurantAdminTab[] 
        { 
            _furnitureTab, _staffTab, _recipeTab, _kitchenTab 
        };

        // 이벤트 등록
        if (_furnitureButtons != null)
        {
            foreach (var btn in _furnitureButtons)
                btn?.OnClickEvent(ShowFurnitureTab);
        }
        
        if (_staffButtons != null)
        {
            foreach (var btn in _staffButtons)
                btn?.OnClickEvent(ShowStaffTab);
        }
        
        if (_recipeButtons != null)
        {
            foreach (var btn in _recipeButtons)
                btn?.OnClickEvent(ShowRecipeTab);
        }
        
        if (_kitchenButtons != null)
        {
            foreach (var btn in _kitchenButtons)
                btn?.OnClickEvent(ShowKitchenTab);
        }

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
        _tmpScale = _mainUI.transform.localScale;
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
        int openingSessionVersion = ++_sessionVersion;
        _isClosingSession = false;
        _isFailSafeCloseStarted = false;
        _nativeHideView = null;
        ClearCanvasAlphaTween();
        CleanupTransition();

        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_shopMusic, 0.5f);
        gameObject.SetActive(true);
        _mainUI.SetActive(false);
        
        ShowFurnitureTabOptimized();
        
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0;
        _dontTouchArea.gameObject.SetActive(true);
        
        // Floor 타입 강제 초기화하여 ChangeFloorType이 항상 실행되도록 함
        ERestaurantFloorType targetFloor = _mainScene.CurrentFloor;
        _floorType = (ERestaurantFloorType)(-1); // 강제로 다른 값으로 설정
        ChangeFloorTypeOptimized(targetFloor);
        
        // Floor Button Groups 초기 상태 설정
        UpdateFloorButtonGroups(targetFloor);

        _recipeTab.UpdateUI();

        TweenData tween = _canvasGroup.TweenAlpha(1, 0.1f);
        tween.OnComplete(() =>
        {
            if (!IsOpeningSessionCurrent(openingSessionVersion))
                return;

            VisibleState = VisibleState.Appeared;
            _mainUI.SetActive(true);
            _mainUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            TweenData tween2 = _mainUI.TweenScale(_tmpScale, _showDuration, _showTweenMode);
            tween2.OnComplete(() => 
            {
                if (!IsOpeningSessionCurrent(openingSessionVersion))
                    return;

                _canvasGroup.blocksRaycasts = true;
                _dontTouchArea.gameObject.SetActive(false);
            });
        });
    }

    public override void Hide()
    {
        if (_isClosingSession || !gameObject.activeInHierarchy)
            return;

        _isClosingSession = true;
        _isFailSafeCloseStarted = false;
        int closingSessionVersion = ++_sessionVersion;

        // CleanupTransition 전에 Navigation 최상위 View를 고정한다.
        MobileUIView currentView = _uiNav != null
            ? _uiNav.FirstView as MobileUIView
            : null;
        bool closeMainShop = currentView == this &&
                             _mainUI != null &&
                             _mainUI.activeSelf;

        // 진행 중인 전환 애니메이션과 Overlay만 정리한다.
        CleanupTransition();

        _mainScene.PlayMainMusic();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 1;
        _dontTouchArea.gameObject.SetActive(true);

        if (closeMainShop)
        {
            VisibleState = VisibleState.Disappearing;
            _mainUI.transform.localScale = _tmpScale;
            TweenData tween = _mainUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
            tween.OnComplete(() =>
            {
                if (!IsClosingSessionCurrent(closingSessionVersion))
                    return;

                _mainUI.SetActive(false);
                CompleteAfterExistingHide(closingSessionVersion);
            });
            return;
        }

        if (TryGetOwnedViewName(currentView, out string currentViewName))
        {
            CloseCurrentViewWithNativeHide(
                currentView,
                currentViewName,
                closingSessionVersion);
            return;
        }

        CompleteSessionCloseWithoutNativeHide(
            closingSessionVersion,
            "No supported active shop View matched the Navigation top View.");
    }

    private bool IsOpeningSessionCurrent(int sessionVersion)
    {
        return sessionVersion == _sessionVersion &&
               !_isClosingSession &&
               gameObject.activeInHierarchy;
    }

    private bool IsClosingSessionCurrent(int sessionVersion)
    {
        return sessionVersion == _sessionVersion &&
               _isClosingSession &&
               gameObject.activeInHierarchy;
    }

    private bool TryGetOwnedViewName(MobileUIView view, out string viewName)
    {
        if (view == _staffUI)
        {
            viewName = "UIStaff";
            return true;
        }

        if (view == _furnitureUI)
        {
            viewName = "UIFurniture";
            return true;
        }

        if (view == _kitchenUI)
        {
            viewName = "UIKitchen";
            return true;
        }

        if (view is UIStaffUpgrade)
        {
            viewName = "UIStaffUpgrade";
            return true;
        }

        if (view is UIRecipeUpgrade)
        {
            viewName = "UIRecipeUpgrade";
            return true;
        }

        viewName = null;
        return false;
    }

    private void CloseCurrentViewWithNativeHide(
        MobileUIView currentView,
        string currentViewName,
        int sessionVersion)
    {
        _nativeHideView = currentView;

        // Right Sample과 동일하게 Navigation Pop이 현재 View의 기존 Hide()를 호출한다.
        _uiNav.Pop(currentViewName);

        bool nativeHideStarted = currentView.VisibleState == VisibleState.Disappearing ||
                                 currentView.VisibleState == VisibleState.Disappeared;
        if (!nativeHideStarted)
        {
            _nativeHideView = null;
            CompleteSessionCloseWithoutNativeHide(
                sessionVersion,
                $"Native Hide did not start for '{currentViewName}'.");
            return;
        }

        VisibleState = VisibleState.Disappearing;
        _mainUI.SetActive(false);

        // Upgrade Hide는 원래 동기식이다. 애니메이션이 있는 View만 기존 시간만큼 기다린다.
        if (currentView.VisibleState == VisibleState.Disappeared)
        {
            CompleteAfterExistingHide(sessionVersion);
            return;
        }

        Tween.Wait(_hideDuration, () =>
        {
            if (IsClosingSessionCurrent(sessionVersion))
                CompleteAfterExistingHide(sessionVersion);
        });
    }

    private void CompleteAfterExistingHide(int sessionVersion)
    {
        if (!IsClosingSessionCurrent(sessionVersion))
            return;

        PopActiveUIViews(_nativeHideView);
        _mainUI.SetActive(false);
        ResetBackgroundImageOffsetOptimized();
        BeginBackgroundFade(sessionVersion);
    }

    private void PopActiveUIViews(MobileUIView alreadyHiddenView)
    {
        Transform navigationViewContainer = transform.parent;
        UIStaffUpgrade staffUpgradeUI = navigationViewContainer != null
            ? navigationViewContainer.GetComponentInChildren<UIStaffUpgrade>(true)
            : null;
        UIRecipeUpgrade recipeUpgradeUI = navigationViewContainer != null
            ? navigationViewContainer.GetComponentInChildren<UIRecipeUpgrade>(true)
            : null;
        UIStaffSkin staffSkinUI = _staffUI != null
            ? _staffUI.GetComponentInChildren<UIStaffSkin>(true)
            : null;

        CloseSynchronousOwnedView(staffUpgradeUI, "UIStaffUpgrade", alreadyHiddenView);
        CloseSynchronousOwnedView(recipeUpgradeUI, "UIRecipeUpgrade", alreadyHiddenView);

        if (staffSkinUI != null && alreadyHiddenView != _staffUI)
            staffSkinUI.Hide();

        CloseImmediateOwnedView(_staffUI, "UIStaff", alreadyHiddenView);
        CloseImmediateOwnedView(_furnitureUI, "UIFurniture", alreadyHiddenView);
        CloseImmediateOwnedView(_kitchenUI, "UIKitchen", alreadyHiddenView);
    }

    private void CloseSynchronousOwnedView(
        MobileUIView view,
        string viewName,
        MobileUIView alreadyHiddenView)
    {
        if (view == null)
            return;

        if (view != alreadyHiddenView)
            view.Hide();

        if (_uiNav != null && _uiNav.CheckActiveView(viewName))
            _uiNav.PopNoAnime(viewName);

        if (view != alreadyHiddenView)
        {
            view.VisibleState = VisibleState.Disappeared;
            view.gameObject.SetActive(false);
        }
    }

    private void CloseImmediateOwnedView(
        MobileUIView view,
        string viewName,
        MobileUIView alreadyHiddenView)
    {
        if (view == null || view == alreadyHiddenView)
            return;

        if (_uiNav != null && _uiNav.CheckActiveView(viewName))
            _uiNav.PopNoAnime(viewName);

        view.VisibleState = VisibleState.Disappeared;
        view.gameObject.SetActive(false);
    }

    private void CompleteSessionCloseWithoutNativeHide(int sessionVersion, string reason)
    {
        if (!IsClosingSessionCurrent(sessionVersion) || _isFailSafeCloseStarted)
            return;

        _isFailSafeCloseStarted = true;
        _nativeHideView = null;
        Debug.LogError($"[RestaurantAdmin Exit] {reason} Falling back to immediate View cleanup and background fade.");

        CleanupTransition();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 1;
        _dontTouchArea.gameObject.SetActive(true);
        VisibleState = VisibleState.Disappearing;

        PopActiveUIViews(null);
        _mainUI.SetActive(false);
        ResetBackgroundImageOffsetOptimized();
        BeginBackgroundFade(sessionVersion);
    }

    private void BeginBackgroundFade(int sessionVersion)
    {
        ClearCanvasAlphaTween();
        TweenData fadeTween = _canvasGroup.TweenAlpha(0, 0.1f);
        fadeTween.OnComplete(() => CompleteSessionClose(sessionVersion));
    }

    private void CompleteSessionClose(int sessionVersion)
    {
        if (!IsClosingSessionCurrent(sessionVersion))
            return;

        CleanupTransition();
        _mainUI.SetActive(false);

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0;
        _dontTouchArea.gameObject.SetActive(false);
        VisibleState = VisibleState.Disappeared;
        RemoveRestaurantAdminNavigationEntry();

        _nativeHideView = null;
        _isFailSafeCloseStarted = false;
        _isClosingSession = false;
        gameObject.SetActive(false);
    }

    private void RemoveRestaurantAdminNavigationEntry()
    {
        if (_uiNav != null && _uiNav.CheckActiveView("RestaurantAdminUI"))
            _uiNav.PopNoAnime("RestaurantAdminUI");
    }

    private void ClearCanvasAlphaTween()
    {
        if (_canvasGroup != null && _canvasGroup.TryGetComponent(out TweenCanvasGroupAlpha tween))
            tween.Clear();
    }

    public void MainUISetActive(bool active)
    {
        if (_isClosingSession || !gameObject.activeInHierarchy)
            return;

        if (VisibleState == VisibleState.Disappeared || VisibleState == VisibleState.Disappearing)
            _uiNav.PushNoAnime("RestaurantAdminUI");

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1;
        _dontTouchArea.gameObject.SetActive(false);
        _mainUI.transform.localScale = _tmpScale;
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
        // 탭 버튼 상태 설정 - 모든 버튼 그룹 처리
        UIRestaurantAdminTabButton[][] allButtons = { _furnitureButtons, _staffButtons, _recipeButtons, _kitchenButtons };
        
        for (int i = 0; i < allButtons.Length; i++)
        {
            if (allButtons[i] != null)
            {
                foreach (var btn in allButtons[i])
                {
                    if (btn != null)
                    {
                        if (i == activeIndex)
                            btn.SelectButton();
                        else
                            btn.UnselectedButton();
                    }
                }
            }
        }

        // 탭 Attention 상태 설정
        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].gameObject.SetActive(true);
            SetTabContentActive(_tabs[i], i == activeIndex);

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

    private static void SetTabContentActive(UIRestaurantAdminTab tab, bool active)
    {
        Transform tabTransform = tab.transform;
        for (int childIndex = 0; childIndex < tabTransform.childCount; childIndex++)
        {
            Transform child = tabTransform.GetChild(childIndex);
            bool isSelectedTabDuplicateTitle = child.name == "Title Text";
            if (isSelectedTabDuplicateTitle)
                continue;

            child.gameObject.SetActive(active);
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

    // 전환 애니메이션 정리 (UI 닫을 때 호출)
    private void CleanupTransition()
    {
        // 진행 중인 코루틴이 있다면 중지
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        DeactivateTransitionOverlay();
        
        // 모든 ScrollingImage를 비활성화 (정리)
        if (_scrollImages != null)
        {
            foreach (var scrollImage in _scrollImages)
            {
                if (scrollImage != null && scrollImage.gameObject.activeSelf)
                {
                    scrollImage.gameObject.SetActive(false);
                }
            }
        }
        
        // 현재 _floorType에 맞는 배경으로 강제 설정 (동기화)
        SetBackgroundImageImmediate(_floorType);
        
        // 탭들도 현재 _floorType에 맞게 업데이트
        _kitchenTab.ChangeFloorType(_floorType);
        _furnitureTab.ChangeFloorType(_floorType);
        _staffTab.ChangeFloorType(_floorType);
        _recipeTab.ChangeFloorType(_floorType);
    }

    private void DeactivateTransitionOverlay()
    {
        if (_normalTransitionImage != null)
            _normalTransitionImage.gameObject.SetActive(false);

        if (_vipTransitionImage != null)
            _vipTransitionImage.gameObject.SetActive(false);

        if (_transitionOverlay != null)
            _transitionOverlay.SetActive(false);
    }

    // 최적화된 배경 이미지 설정
    private void SetBackgroundImageOptimized(ERestaurantFloorType floor)
    {
        // 0(Floor1)과 1(Floor2 VIP) 사이 전환일 때만 애니메이션
        bool shouldAnimate = _currentScrollImage != null &&
                            (_previousFloorType == ERestaurantFloorType.Floor1 || _previousFloorType == ERestaurantFloorType.Floor2) &&
                            (floor == ERestaurantFloorType.Floor1 || floor == ERestaurantFloorType.Floor2) &&
                            _previousFloorType != floor &&
                            _transitionMaterialInstance != null;

        if (shouldAnimate)
        {
            // 기존 코루틴 중지
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
                DeactivateTransitionOverlay();
            }

            _transitionCoroutine = StartCoroutine(TransitionFloorCoroutine(floor));
        }
        else
        {
            // 즉시 전환 (애니메이션 없음)
            DeactivateTransitionOverlay();
            SetBackgroundImageImmediate(floor);
        }
    }

    private void SetBackgroundImageImmediate(ERestaurantFloorType floor)
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

    private System.Collections.IEnumerator TransitionFloorCoroutine(ERestaurantFloorType targetFloor)
    {
        // 사용자 입력 차단 (애니메이션 중 UI 조작 방지)
        bool prevBlocksRaycasts = _canvasGroup.blocksRaycasts;
        _canvasGroup.blocksRaycasts = false;
        
        // 메인 UI 비활성화
        bool mainUIWasActive = _mainUI.activeSelf;
        _mainUI.SetActive(false);
        
        ScrollingImage fromScrollImage = _currentScrollImage;
        ScrollingImage toScrollImage = _scrollImages[(int)targetFloor];
        
        if (fromScrollImage == null || toScrollImage == null)
        {
            Debug.LogError("ScrollingImage is null!");
            _mainUI.SetActive(mainUIWasActive);
            _canvasGroup.blocksRaycasts = prevBlocksRaycasts;
            DeactivateTransitionOverlay();
            SetBackgroundImageImmediate(targetFloor);
            _transitionCoroutine = null;
            yield break;
        }
        
        bool toVip = targetFloor == ERestaurantFloorType.Floor2;
        
        // 오프셋 동기화
        toScrollImage.SetOffset(fromScrollImage.Offset);
        
        // 텍스처를 가져오기 위해 잠시 활성화
        ScrollingImage normalScrollImage = _scrollImages[(int)ERestaurantFloorType.Floor1];
        ScrollingImage vipScrollImage = _scrollImages[(int)ERestaurantFloorType.Floor2];
        
        bool normalWasActive = normalScrollImage.gameObject.activeSelf;
        bool vipWasActive = vipScrollImage.gameObject.activeSelf;
        
        // 잠시 활성화하여 텍스처 가져오기
        normalScrollImage.gameObject.SetActive(true);
        vipScrollImage.gameObject.SetActive(true);
        
        yield return null; // 1프레임 대기
        
        // Material에 텍스처 설정
        Texture normalTex = normalScrollImage.GetTexture();
        Texture vipTex = vipScrollImage.GetTexture();
        
        if (normalTex == null || vipTex == null)
        {
            Debug.LogError($"Texture is null! Normal: {(normalTex != null)}, VIP: {(vipTex != null)}");
            Debug.LogError($"NormalImage: {normalScrollImage.gameObject.name}, VipImage: {vipScrollImage.gameObject.name}");
            
            // 원래 상태로 복구
            normalScrollImage.gameObject.SetActive(normalWasActive);
            vipScrollImage.gameObject.SetActive(vipWasActive);
            _mainUI.SetActive(mainUIWasActive);
            _canvasGroup.blocksRaycasts = prevBlocksRaycasts;

            DeactivateTransitionOverlay();
            SetBackgroundImageImmediate(targetFloor);
            _transitionCoroutine = null;
            yield break;
        }
        
        // 오버레이 Image 설정 복사 (sprite, type, pixelsPerUnit 등)
        if (_normalTransitionImage != null)
        {
            var normalImage = normalScrollImage.GetComponent<UnityEngine.UI.Image>();
            _normalTransitionImage.sprite = normalImage.sprite;
            _normalTransitionImage.type = normalImage.type;
            _normalTransitionImage.pixelsPerUnitMultiplier = normalImage.pixelsPerUnitMultiplier;
            _normalTransitionImage.material = new Material(_transitionMaterialInstance);
        }
        
        if (_vipTransitionImage != null)
        {
            var vipImage = vipScrollImage.GetComponent<UnityEngine.UI.Image>();
            _vipTransitionImage.sprite = vipImage.sprite;
            _vipTransitionImage.type = vipImage.type;
            _vipTransitionImage.pixelsPerUnitMultiplier = vipImage.pixelsPerUnitMultiplier;
            _vipTransitionImage.material = new Material(_transitionMaterialInstance);
        }
        
        _transitionMaterialInstance.SetTexture("_NormalTex", normalTex);
        _transitionMaterialInstance.SetTexture("_VipTex", vipTex);
        
        // 타일링 스케일 설정 (Tiled Image 타일링 유지)
        Vector2 normalScale = normalScrollImage.GetTextureScale();
        Vector2 vipScale = vipScrollImage.GetTextureScale();
        _transitionMaterialInstance.SetVector("_NormalScale", normalScale);
        _transitionMaterialInstance.SetVector("_VipScale", vipScale);
        
        // 텍스처 검증이 끝난 뒤에만 Overlay와 실제 이미지를 활성화한다.
        if (_transitionOverlay != null)
        {
            _transitionOverlay.SetActive(true);
            if (_normalTransitionImage != null)
                _normalTransitionImage.gameObject.SetActive(true);
            if (_vipTransitionImage != null)
                _vipTransitionImage.gameObject.SetActive(true);
            _transitionOverlay.transform.SetAsLastSibling();
        }
        
        float elapsed = 0f;
        float startProgress = toVip ? 0f : 1f;
        float endProgress = toVip ? 1f : 0f;
        
        Debug.Log($"Starting transition from {_previousFloorType} to {targetFloor}, toVip={toVip}, start={startProgress}, end={endProgress}");
        
        while (elapsed < _transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _transitionDuration);
            
            // Ease 적용
            t = Mathf.SmoothStep(0, 1, t);
            
            float currentProgress = Mathf.Lerp(startProgress, endProgress, t);
            _transitionMaterialInstance.SetFloat("_Progress", currentProgress);
            
            // 스크롤링 오프셋 업데이트 (배경이 계속 스크롤되도록)
            Vector2 normalOffset = normalScrollImage.Offset;
            Vector2 vipOffset = vipScrollImage.Offset;
            _transitionMaterialInstance.SetVector("_NormalOffset", normalOffset);
            _transitionMaterialInstance.SetVector("_VipOffset", vipOffset);
            
            // 두 이미지의 Material에도 적용
            if (_normalTransitionImage != null && _normalTransitionImage.material != null)
            {
                _normalTransitionImage.material.SetVector("_NormalOffset", normalOffset);
                _normalTransitionImage.material.SetVector("_VipOffset", vipOffset);
                _normalTransitionImage.material.SetFloat("_Progress", currentProgress);
            }
            if (_vipTransitionImage != null && _vipTransitionImage.material != null)
            {
                _vipTransitionImage.material.SetVector("_NormalOffset", normalOffset);
                _vipTransitionImage.material.SetVector("_VipOffset", vipOffset);
                _vipTransitionImage.material.SetFloat("_Progress", currentProgress);
            }
            
            yield return null;
        }
        
        // 최종 상태
        _transitionMaterialInstance.SetFloat("_Progress", endProgress);
        
        Debug.Log("Transition complete");
        
        // 이전 배경 비활성화, 새 배경은 이미 활성화 상태
        if (fromScrollImage != toScrollImage)
            fromScrollImage.gameObject.SetActive(false);
        _currentScrollImage = toScrollImage;
        
        // 정상 완료 시 자식 이미지와 Overlay 컨테이너를 함께 비활성화한다.
        DeactivateTransitionOverlay();
        
        // 메인 UI 다시 활성화
        _mainUI.SetActive(mainUIWasActive);
        
        // 사용자 입력 다시 허용
        _canvasGroup.blocksRaycasts = prevBlocksRaycasts;
        
        _transitionCoroutine = null;
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

        _previousFloorType = _floorType;
        _floorType = floorType;
        
        // 한 번에 모든 탭 업데이트 (각 탭이 자신의 배경을 관리)
        _kitchenTab.ChangeFloorType(_floorType);
        _furnitureTab.ChangeFloorType(_floorType);
        _staffTab.ChangeFloorType(_floorType);
        _recipeTab.ChangeFloorType(_floorType);
        _floorButtonGroup.SetFloorText(_floorType);

        SetBackgroundImageOptimized(_floorType);
        UpdateFloorButtonGroups(_floorType);
    }
    
    private void UpdateFloorButtonGroups(ERestaurantFloorType floorType)
    {
        bool isFloor1 = floorType == ERestaurantFloorType.Floor1;
        
        // 층별 오브젝트 활성/비활성
        if (_floor1Object != null)
            _floor1Object.SetActive(isFloor1);
        
        if (_vipFloorObject != null)
            _vipFloorObject.SetActive(!isFloor1);
        
        // 층별 버튼 그룹 활성/비활성
        if (_floor1ButtonGroup != null)
            _floor1ButtonGroup.SetActive(isFloor1);
        
        if (_vipRoomButtonGroups != null)
        {
            foreach (var vipRoomGroup in _vipRoomButtonGroups)
            {
                if (vipRoomGroup != null)
                    vipRoomGroup.SetActive(!isFloor1);
            }
        }
    }
    
}

