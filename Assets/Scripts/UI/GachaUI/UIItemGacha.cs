using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;


public class UIItemGacha : GachaMachineParent
{

    [Header("Components")]
    [SerializeField] private UIBouncingBall _bouncingBall;
    [SerializeField] private ScrollingImage _scrollImage;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _singleButton;
    public Button SingleButton => _singleButton;
    [SerializeField] private Button _tenButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Image _getItemImage;
    [SerializeField] private UIGachaCard _skinGachaCard;
    [SerializeField] private GachaCapsule _capsule;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _getItemSlotFrame;
    [SerializeField] private UIGachaCardSlot _slotPrefab;

    [Space]
    [Header("Capsule Options")]
    [SerializeField] private RectTransform _capsules;
    [SerializeField] private Image _upperCapsule;
    [SerializeField] private Image _lowerCapsule;
    [SerializeField] private Capsule[] _capsuleColors;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _leverSound;
    [SerializeField] private AudioClip _shakeCapsuleSound;
    [SerializeField] private AudioClip _fallCapsuleSound;
    [SerializeField] private AudioClip _openDoorSound;
    [SerializeField] private AudioClip _boomSound;
    [SerializeField] private AudioClip _getNormalItemSound;
    [SerializeField] private AudioClip _getSpecialItemSound;


    private List<UIGachaCardSlot> _getItemSlotList = new List<UIGachaCardSlot>();
    private List<GachaItemData> _getItemList = new List<GachaItemData>();
    private float _screenTouchWaitTime;
    private int _currentStep;
    private int _getItemIndex = 0;
    private bool _isCapsuleColorChanged;
    private bool _isPlayTextAnime;
    private AudioClip _getItemSound;
    private bool _purchaseInProgress;
    private bool _isShowingSummary;
    private bool _summaryComplete;
    private readonly Vector3[] _skipButtonCorners = new Vector3[4];
    private GachaResultCardPopup _resultPopup;
    private GachaResultCardHitArea _bonusHitArea;
    private readonly List<Button> _summaryButtons = new List<Button>();
    private readonly List<UnityEngine.Events.UnityAction> _summaryActions = new List<UnityEngine.Events.UnityAction>();
    private readonly List<bool> _summaryButtonStates = new List<bool>();
    private Transform _summarySlotParent;
    private int _summarySlotSibling;
    private GachaEconomyService _collectionEconomy;
    private bool _requiresCollectionEconomy;
    private readonly List<bool> _resultIsNew = new List<bool>();
    private readonly List<GachaAcquisitionResult> _confirmedResults = new List<GachaAcquisitionResult>();
    private GachaEconomyTransaction _lastPresentedTransaction;

    public void BindCollectionEconomy(UIGacha view, GachaEconomyService economy)
    {
        if (!ReferenceEquals(_collectionEconomy, economy))
        {
            CancelResultPresentation();
            if (_gachaMacineAnimator != null)
            {
                foreach (var parameter in _gachaMacineAnimator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger)
                        _gachaMacineAnimator.ResetTrigger(parameter.nameHash);
                if (Application.isPlaying && _gachaMacineAnimator.isActiveAndEnabled)
                    _gachaMacineAnimator.Play("Base Layer.Idle", 0, 0f);
            }
            if (_skinGachaCard != null) _skinGachaCard.gameObject.SetActive(false);
            if (_screenButton != null) _screenButton.gameObject.SetActive(false);
            if (_skipButton != null) _skipButton.gameObject.SetActive(false);
            if (_getItemImage != null) _getItemImage.gameObject.SetActive(false);
            if (_getItemSlotFrame != null) _getItemSlotFrame.gameObject.SetActive(false);
            _currentStep = 1;
            _screenTouchWaitTime = 0;
            if (view != null && view.IsCurrentMachine(this)) view.SetStartGacha(false);
            _getItemList.Clear();
            _resultIsNew.Clear();
            _confirmedResults.Clear();
            _lastPresentedTransaction = null;
            _getItemIndex = 0;
        }
        _uiGacha = view;
        _collectionEconomy = economy;
        _requiresCollectionEconomy = true;
    }

    public void PresentCollectionTransaction(GachaEconomyTransaction transaction)
    {
        if (transaction == null || !transaction.IsCompleted || !transaction.IsDraw || transaction == _lastPresentedTransaction ||
            transaction.Machine != GachaMachineKind.Item || transaction.Results.Count == 0 ||
            _collectionEconomy == null || transaction.After.AccountId != _collectionEconomy.Snapshot?.AccountId ||
            !gameObject.activeInHierarchy || !_uiGacha.IsCurrentMachine(this) ||
            _uiGacha.VisibleState != VisibleState.Appeared) return;
        _lastPresentedTransaction = transaction;
        CancelResultPresentation();
        _getItemList.Clear();
        _resultIsNew.Clear();
        _confirmedResults.Clear();
        foreach (var result in transaction.Results)
        {
            if (!(result.Data is GachaItemData item)) continue;
            _getItemList.Add(item);
            _resultIsNew.Add(result.IsNew);
            _confirmedResults.Add(result);
        }
        if (_getItemList.Count == 0) return;
        _getItemIndex = 0;
        _isPlayTextAnime = false;
        _isCapsuleColorChanged = true;
        PreparePurchasePresentation();
        _uiGacha.SetStartGacha(true);
        _gachaMacineAnimator.SetTrigger("Start");
    }

    private bool ResultIsNew(int index) => index >= 0 && index < _resultIsNew.Count && _resultIsNew[index];
    private void NotifyCollectionReveal(int index)
    {
        if (index >= 0 && index < _confirmedResults.Count)
            _uiGacha.NotifyCollectionReveal(_confirmedResults[index]);
    }

#if UNITY_EDITOR
    private bool _editorOfflineNavigation;
    private bool _editorOfflinePresentation;
    private AudioSource _editorPresentationAudio;
    public int EditorPresentationStep => _currentStep;
    public int EditorResultIndex => _getItemIndex;
    public bool EditorSummaryComplete => _summaryComplete;
    public bool EditorIsSummary => _isShowingSummary;
    public int EditorVisibleSlotCount => _getItemSlotList.Count(slot => slot != null && slot.gameObject.activeInHierarchy);
    public bool EditorPresentationIsIdle => _editorOfflinePresentation && _currentStep == 1 &&
        !_uiGacha.IsStartGacha && _uiGacha.VisibleState == VisibleState.Appeared &&
        _singleButton.gameObject.activeInHierarchy;
    public string[] EditorFixedResultIds => _getItemList.Select(item => item.Id).ToArray();
    public AudioSource EditorNativeSoundSource => _editorPresentationAudio;

    // This binds only presentation on the inactive, sanitized copy built by the existing
    // offline host. It is never a free purchase or an alternative reward/save path.
    public void ConfigureEditorOfflinePresentation(UIGacha view)
    {
        if (!_editorOfflineNavigation || _editorOfflinePresentation || gameObject.activeInHierarchy ||
            view != _uiGacha || view == null || !transform.IsChildOf(view.transform) ||
            _bouncingBall == null || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("An inactive, SDK-free item presentation copy is required.");

        for (int i = 0; i < 10; i++)
        {
            UIGachaCardSlot slot = Instantiate(_slotPrefab, _getItemSlotFrame);
            slot.InitPresentation();
            slot.gameObject.SetActive(false);
            _getItemSlotList.Add(slot);
        }
        _editorPresentationAudio = gameObject.AddComponent<AudioSource>();
        _editorPresentationAudio.playOnAwake = false;
        _editorPresentationAudio.loop = false;
        _screenButton.onClick.RemoveAllListeners();
        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        _skipButton.onClick.RemoveAllListeners();
        _skipButton.onClick.AddListener(OnSkipButtonClicked);
        _screenButton.interactable = _skipButton.interactable = true;
        _gachaMacineAnimator.fireEvents = true; // Original clip: display/step/sound events only.
        _editorOfflinePresentation = true;
    }

    public void ConfigureCollectionOffline(UIGacha view, GachaEconomyService economy)
    {
        ConfigureEditorOfflineNavigation(view);
        ConfigureEditorOfflinePresentation(view);
        BindCollectionEconomy(view, economy);
        _singleButton.onClick.AddListener(OnSingleGachaButtonClicked);
        _tenButton.onClick.AddListener(OnTenGachaButtonClicked);
    }

    public void BeginEditorOfflinePresentation(IReadOnlyList<GachaItemData> results)
    {
        if (!Application.isPlaying || !_editorOfflinePresentation || !gameObject.activeInHierarchy ||
            _uiGacha.EditorOfflineCurrentMachine != this || !EditorPresentationIsIdle ||
            BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || results == null ||
            (results.Count != 1 && results.Count != 11) ||
            results.Any(item => !IsRegisteredPurchaseItem(item)))
            throw new InvalidOperationException("Only an idle, visible offline item machine can display fixed resource results.");
        CancelResultPresentation();
        _getItemList.Clear();
        _getItemList.AddRange(results);
        _resultIsNew.Clear();
        _confirmedResults.Clear();
        _getItemIndex = 0;
        _isPlayTextAnime = false;
        _isCapsuleColorChanged = true;
        PreparePurchasePresentation(); // Visibility only; deliberately no Purchase/GetItem/StartAddItem.
        _uiGacha.SetStartGacha(true); // Fence repeated editor input before the first Animator event.
        _gachaMacineAnimator.SetTrigger("Start");
    }
    public void ConfigureEditorOfflineNavigation(UIGacha view)
    {
        if (gameObject.activeInHierarchy || view == null)
            throw new InvalidOperationException("Inactive copied item machine required.");
        _editorOfflineNavigation = true;
        _uiGacha = view;
        _itemDataList = new List<GachaData>();
        _scrollImage.Init();
        _skinGachaCard.Init();
        _gachaMacineAnimator.fireEvents = false;
        // This machine is a navigation destination, never an offline item purchase fixture.
        _singleButton.onClick.RemoveAllListeners();
        _tenButton.onClick.RemoveAllListeners();
        _singleButton.interactable = _tenButton.interactable = false;
    }
#endif


    public void PlayLeverSound()
    {
        PlayPresentationSound(_leverSound);
    }

    public void PlayShakeCapsuleSound()
    {
        PlayPresentationSound(_shakeCapsuleSound);
    }

    public void PlayFallCapsuleSound()
    {
        PlayPresentationSound(_fallCapsuleSound);
    }

    public void PlayOpenDoorSound()
    {
        PlayPresentationSound(_openDoorSound);
    }

    public void PlayBoomSound()
    {
        PlayPresentationSound(_boomSound);
    }

    public void PlayGetItemSound()
    {
        PlayPresentationSound(_getItemSound);
    }

    private void PlayPresentationSound(AudioClip clip)
    {
#if UNITY_EDITOR
        if (_editorOfflineNavigation)
        {
            if (_editorPresentationAudio != null && clip != null) _editorPresentationAudio.PlayOneShot(clip);
            return;
        }
#endif
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, clip);
    }

    public override void Init(UIGacha uiGacha)
    {
        _uiGacha = uiGacha;
        _scrollImage.Init();
        _skinGachaCard.Init();
        _itemDataList = ItemManager.Instance.GetSortGachaItemDataList(GradeSortType.GradeDescending).Select((data) => (GachaData)data).ToList();

        for (int i = 0; i < 10; ++i)
        {
            UIGachaCardSlot slot = Instantiate(_slotPrefab, _getItemSlotFrame);
            slot.InitPresentation();
            _getItemSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }

        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        _singleButton.onClick.AddListener(OnSingleGachaButtonClicked);
        _tenButton.onClick.AddListener(OnTenGachaButtonClicked);
        _skipButton.onClick.AddListener(OnSkipButtonClicked);

        SetStep(1);
        _skinGachaCard.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_collectionEconomy != null && _uiGacha != null && !_uiGacha.IsStartGacha)
        {
            PresentCollectionTransaction(_collectionEconomy.LastTransaction);
            bool ready = !_collectionEconomy.IsBusy && _uiGacha.VisibleState == VisibleState.Appeared;
            _singleButton.interactable = ready && _collectionEconomy.CanDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, out _);
            _tenButton.interactable = ready && _collectionEconomy.CanDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsEleven, out _);
        }
        if( 0 < _screenTouchWaitTime)
            _screenTouchWaitTime -= Time.deltaTime;
    }

    private void LateUpdate()
    {
        UpdateSkipButtonLayout();
        _resultPopup?.BringToFront();
    }

    private void UpdateSkipButtonLayout()
    {
        if (_skipButton == null || !_skipButton.gameObject.activeSelf || _uiGacha == null) return;
        AlignResultButton(_skipButton.transform as RectTransform,
            _uiGacha.transform as RectTransform, _skipButtonCorners);
        _skipButton.transform.SetAsLastSibling();
    }


    public override void Show()
    {
        CancelResultPresentation();
        gameObject.SetActive(true);
        _singleButton.gameObject.SetActive(true);
        _tenButton.gameObject.SetActive(true);
        _uiGacha.SetActiveUIComponents(true);
        _scrollImage.gameObject.SetActive(true);
        _screenButton.gameObject.SetActive(false);
        _skinGachaCard.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
        _capsule.gameObject.SetActive(false);
#if UNITY_EDITOR
        if (!_editorOfflineNavigation)
#endif
            _bouncingBall.ResetBalls();
        CapsuleSetSibilingIndex(1);

        SetStep(1);
        _screenTouchWaitTime = 0;
        _gachaMacineAnimator.enabled = true;
        // A disabled machine can retain queued skip/open triggers across navigation.
        foreach (AnimatorControllerParameter parameter in _gachaMacineAnimator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                _gachaMacineAnimator.ResetTrigger(parameter.nameHash);
        _gachaMacineAnimator.Play("Base Layer.Idle", 0, 0f);
        _gachaMacineAnimator.Update(0f);
    }


    public override void Hide()
    {
        CancelResultPresentation();
        SetStep(1);
        gameObject.SetActive(true);
        _screenTouchWaitTime = 0;
        _singleButton.gameObject.SetActive(false);
        _tenButton.gameObject.SetActive(false);
        _uiGacha.SetActiveUIComponents(false);
        _scrollImage.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _skinGachaCard.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
        _capsule.gameObject.SetActive(false);

        _gachaMacineAnimator.enabled = false;

    }

    private void OnDisable() => CancelResultPresentation();

    private void CancelResultPresentation()
    {
        ClearSummaryInspection();
#if UNITY_EDITOR
        if (_editorPresentationAudio != null) _editorPresentationAudio.Stop();
#endif
        StopAllCoroutines();
        _isShowingSummary = _summaryComplete = false;
        if (_capsule != null) _capsule.CancelPresentation();
        if (_skinGachaCard != null) _skinGachaCard.TweenStop();
        foreach (UIGachaCardSlot slot in _getItemSlotList)
        {
            if (slot == null) continue;
            slot.TweenStop();
            slot.gameObject.SetActive(false);
        }
    }


    public void GetItem(GachaItemData data)
    {
#if UNITY_EDITOR
        if (_editorOfflineNavigation) return;
#endif
        _getItemList.Clear();
        _getItemIndex = 0;

        _getItemList.Add(data);
        UserInfo.GiveGachaItem(data);

        _gachaMacineAnimator.SetTrigger("Start");
    }

    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }

    public void StartBallBounce()
    {
        _bouncingBall.NoSpeedDamping = false;
        _bouncingBall.StartBounce();
    }

    public void StopBallBounce()
    {
        _bouncingBall.NoSpeedDamping = true;
    }


    public override void OnScreenButtonClicked()
    {
        if (_resultPopup != null && _resultPopup.IsOpen) { _resultPopup.Hide(); return; }
        if (_isShowingSummary && !_summaryComplete) return;
        if (0 < _screenTouchWaitTime)
        {
            DebugLog.Log("아직 터치할 수 없습니다.");
            return;
        }

        switch (_currentStep)
        {
            case 1:
                _gachaMacineAnimator.SetTrigger("Stop");
                break;

            case 2:
                _gachaMacineAnimator.SetTrigger("Step2Skip");
                StopBallBounce();
                break;

            case 3:
                _gachaMacineAnimator.SetTrigger("CapsuleOpen");
                StopBallBounce();
                break;

            case 5:
                StopBallBounce();
                if (_getItemList.Count <= _getItemIndex)
                {
                    _gachaMacineAnimator.SetTrigger("Stop");
                    return;
                }

                if(_isPlayTextAnime)
                {
                    _skinGachaCard.SetData(_getItemList[_getItemIndex - 1], ResultIsNew(_getItemIndex - 1));
                    _isPlayTextAnime = false;
                    _screenTouchWaitTime = 0.5f;
                    return;
                }
                // The first ten have been revealed. The dedicated +1 capsule owns the
                // last reveal, rather than opening it once here and again in the summary.
                if (_getItemList.Count == 11 && _getItemIndex == _getItemList.Count - 1)
                {
                    OnSkipButtonClicked();
                    return;
                }
                else
                {
                    for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                    {
                        _getItemSlotList[i].gameObject.SetActive(false);
                    }
                }

                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;
        
        }

    }

    public void SetStep(int step)
    {
        // SkipButtonClick enters ZoomIn Item and emits SetStep(5). That event must
        // not restart SkipRoutine or advance the fixed result index a second time.
        if (_isShowingSummary && step != 1)
        {
            _currentStep = 5;
            return;
        }
        if (_currentStep == step)
            return;
            
        _uiGacha.StartGachaStepEvent(_currentStep);
        switch (step)
        {
            case 1:
                _currentStep = 1;
                CancelResultPresentation();

                _uiGacha.SetActiveUIComponents(true);
                _uiGacha.SetStartGacha(false);
                _singleButton.gameObject.SetActive(true);
                _tenButton.gameObject.SetActive(true);
                _screenButton.gameObject.SetActive(false);
                _skinGachaCard.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _getItemIndex = 0;
                _isCapsuleColorChanged = true;
                _skinGachaCard.TweenStop();
                _uiGacha.SetActiveGachaMachine(true);
                for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
                {
                    _getItemSlotList[i].gameObject.SetActive(false);
                    _getItemSlotList[i].TweenStop();
                }
                CapsuleSetSibilingIndex(1);
                break;

            case 2:
                _currentStep = 2;
                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(10 <= _getItemList.Count());
                _uiGacha.SetActiveUIComponents(false);
                _uiGacha.SetStartGacha(true);
                _singleButton.gameObject.SetActive(false);
                _tenButton.gameObject.SetActive(false);
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(1);
                break;

            case 3:
                _currentStep = 3;
               
                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(10 <= _getItemList.Count());
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);
                CapsuleSetSibilingIndex(11);
                break;

            case 4:
                _currentStep = 4;

                _skipButton.gameObject.SetActive(10 <= _getItemList.Count());
                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getItemImage.sprite = _getItemList[_getItemIndex].Sprite;
                Utility.ChangeImagePivot(_getItemImage);
                CapsuleSetSibilingIndex(11);
                break;

            case 5:
                _currentStep = 5;

                _skinGachaCard.gameObject.SetActive(false);
                _getItemSlotFrame.gameObject.SetActive(true);
    
                _skipButton.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);

                _isCapsuleColorChanged = true;

                _isPlayTextAnime = true;
                if (10 <= _getItemList.Count() && _getItemList.Count() <= _getItemIndex + 1)
                {
                    OnSkipButtonClicked();
                    return;
                }
                else
                {
                    _screenTouchWaitTime = 0.2f;
                    _getItemSound = _getItemList[_getItemIndex].Rank == Rank.Unique || _getItemList[_getItemIndex].Rank == Rank.Special ? _getSpecialItemSound : _getNormalItemSound;
                    PlayGetItemSound();

                    _skinGachaCard.gameObject.SetActive(true);
                    _skinGachaCard.ResetScale();
                    _getItemImage.gameObject.SetActive(true);
                    _skinGachaCard.SetData(_getItemList[_getItemIndex], ResultIsNew(_getItemIndex));
                    NotifyCollectionReveal(_getItemIndex);
                    _getItemImage.sprite = _getItemList[_getItemIndex].ThumbnailSprite;
                    Utility.ChangeImagePivot(_getItemImage);
                    _skinGachaCard.SetPosition(new Vector3(0, 0, 0));
                }

                _getItemIndex++;
                break;
        }
        UpdateSkipButtonLayout();
    }

    public bool StartAddItem(GachaItemData data)
    {
#if UNITY_EDITOR
        if (_editorOfflineNavigation) return false;
#endif
        // Only the current item tutorial may use this free, fixed result path. The
        // existing count/ownership is also a replay fence after a view is recreated.
        if (_purchaseInProgress || !GachaTutorial.IsCurrentItemTutorialQuest()
            || data == null || data.Id != "GOTCHA91" || !IsRegisteredPurchaseItem(data)
            || UserInfo.IsGiveGachaItem(data) || UserInfo.TotalUseGachaMachineCount == int.MaxValue)
            return false;

        _purchaseInProgress = true;
        try
        {
            if (!UserInfo.GiveGachaItem(data)) return false;
            UserInfo.AddUserGachaMachineCount();
            PreparePurchasePresentation();
            _getItemList.Clear();
            _getItemIndex = 0;
            _getItemList.Add(data);
            _resultIsNew.Clear();
            _resultIsNew.Add(true); // The tutorial admission above explicitly rejects previously owned data.
            _confirmedResults.Clear();
            StartTutorialPresentation();
            return true;
        }
        finally { _purchaseInProgress = false; }
    }

    protected virtual void StartTutorialPresentation() => _gachaMacineAnimator.SetTrigger("Start");

    public override void OnSingleGachaButtonClicked()
    {
        Purchase(1, 10);
    }


    public override void OnTenGachaButtonClicked()
    {
        Purchase(11, 100);
    }

    private void Purchase(int count, int cost)
    {
        if (_requiresCollectionEconomy && _collectionEconomy == null)
        { _uiGacha.ReportCollectionError("계정 복원을 확인 중입니다. 잠시 후 다시 시도해 주세요."); return; }
        if (_collectionEconomy != null)
        {
            if (_purchaseInProgress || _collectionEconomy.IsBusy || _uiGacha.IsStartGacha) return;
            _purchaseInProgress = true;
            try
            {
                if (!_collectionEconomy.TryDraw(GachaMachineKind.Item,
                    count == 1 ? GachaPaymentKind.DiamondsSingle : GachaPaymentKind.DiamondsEleven,
                    PresentCollectionTransaction, out string collectionError))
                    _uiGacha.ReportCollectionError(collectionError);
            }
            finally { _purchaseInProgress = false; }
            return;
        }
#if UNITY_EDITOR
        if (_editorOfflineNavigation) return;
#endif
        if (_purchaseInProgress) return;
        _purchaseInProgress = true;
        try
        {
            if (!UserInfo.IsDiaValid(cost))
            { ReportPurchaseError("현재 다이아를 사용할 수 없습니다."); return; }
            // Resolve the whole batch before deduction. Never charge for a null, unregistered,
            // wrong-type or partially selected result; retain the previous presentation on rejection.
            List<GachaItemData> results;
            try
            {
                var source = _itemDataList == null ? null : new List<GachaData>(_itemDataList);
                if (source == null || source.Count == 0 || source.Any(item => !IsRegisteredPurchaseItem(item)))
                { ReportPurchaseError("아이템 뽑기 자료를 확인할 수 없습니다."); return; }
                results = new List<GachaItemData>(count);
                for (int index = 0; index < count; index++)
                {
                    GachaData selected = DrawPurchaseResult(source);
                    if (!source.Any(item => ReferenceEquals(item, selected)) || !IsRegisteredPurchaseItem(selected))
                    { ReportPurchaseError("아이템 뽑기 결과를 확인할 수 없습니다."); return; }
                    results.Add((GachaItemData)selected);
                }
                if (results.Any(item => !IsRegisteredPurchaseItem(item)))
                { ReportPurchaseError("아이템 뽑기 자료가 변경되었습니다."); return; }
            }
            catch (Exception exception)
            {
                DebugLog.Log(exception.Message);
                ReportPurchaseError("아이템 뽑기 자료를 확인할 수 없습니다.");
                return;
            }
            if (!UserInfo.TrySpendDia(cost, out string error))
            { ReportPurchaseError(error); return; }
            ApplyPurchaseResults(results);
        }
        finally { _purchaseInProgress = false; }
    }

    private static bool IsRegisteredPurchaseItem(GachaData value)
    {
        if (!(value is GachaItemData item) || string.IsNullOrWhiteSpace(item.Id)
            || item.Rank < Rank.Normal1 || item.Rank >= Rank.Length) return false;
        return ReferenceEquals(ItemManager.Instance.GetGachaItemData(item.Id), item);
    }

    protected virtual GachaData DrawPurchaseResult(List<GachaData> source)
        => ItemManager.Instance.GetRandomGachaData(source);

    private void ApplyPurchaseResults(List<GachaItemData> results)
    {
        PreparePurchasePresentation();
        _getItemList.Clear();
        _getItemIndex = 0;
        _getItemList.AddRange(results);
        if (results.Count == 1)
        {
            if (!UserInfo.GiveGachaItem(results[0]))
            { ReportPurchaseError("아이템 지급 결과를 확인할 수 없습니다."); return; }
        }
        else UserInfo.GiveGachaItem(_getItemList);
        CompletePurchasePresentationAndSave(results.Count);
    }

    protected virtual void PreparePurchasePresentation()
    {
        _uiGacha.SetActiveGachaMachine(false);
        SetActiveGachaMachine(true);
    }

    protected virtual void CompletePurchasePresentationAndSave(int count)
    {
        _gachaMacineAnimator.SetTrigger("Start");
        UserInfo.AddUserGachaMachineCount(count);
        GameManager.Instance.AsyncSaveGameData();
        PaymentInfo.AddGachaData($"Normal Item Gacha {count}");
        PaymentInfo.SavePaymentData();
    }

    protected virtual void ReportPurchaseError(string error) => PopupManager.Instance.ShowDisplayText(error);

    private void CapsuleColorChange()
    {
        if (!_isCapsuleColorChanged)
            return;

        int randInt = UnityEngine.Random.Range(0, _capsuleColors.Length);
        _upperCapsule.sprite = _capsuleColors[randInt].UpperCapsule;
        _lowerCapsule.sprite = _capsuleColors[randInt].LowerCapsule;
        _isCapsuleColorChanged = false;
    }


    private void OnSkipButtonClicked()
    {
        if (_isShowingSummary || _getItemList.Count != 11) return;
        _isShowingSummary = true;
        _summaryComplete = false;
        _screenTouchWaitTime = 0f;
        _getItemIndex = _getItemList.Count;
        _skipButton.gameObject.SetActive(false);
        StopAllCoroutines();
        StartCoroutine(SkipRoutine());
    }
    

    private IEnumerator SkipRoutine()
    {
        _skinGachaCard.gameObject.SetActive(false);
        _getItemImage.gameObject.SetActive(false);
        _gachaMacineAnimator.SetTrigger("SkipButtonClick");
        _getItemSlotFrame.gameObject.SetActive(true);

        for (int i = 0, cnt = _getItemSlotList.Count; i < cnt; i++)
        {
            _getItemSlotList[i].gameObject.SetActive(false);
        }
        yield return null;
        _skinGachaCard.gameObject.SetActive(false);
        for (int i = 0, cnt = _getItemList.Count - 1; i < cnt; i++)
        {
            _getItemSlotList[i].SetData(_getItemList[i], ResultIsNew(i));
            NotifyCollectionReveal(i);
            _getItemSlotList[i].gameObject.SetActive(true);
            _getItemSlotList[i].TweenStop();
            _getItemSlotList[i].transform.localScale = Vector3.one * 1.2f;
            _getItemSlotList[i].TweenScale(Vector3.one, 0.2f, Ease.OutBack);
            _getItemSlotList[i].ChangeImagePivot();
            yield return YieldCache.WaitForSeconds(0.1f);
        }
        yield return YieldCache.WaitForSeconds(0.1f);
        GachaItemData finalItem = _getItemList[_getItemList.Count - 1];
        Vector2 finalPosition = new Vector2(600, 0);
        _capsule.PreparePresentation(finalItem.ThumbnailSprite ?? finalItem.Sprite,
            _capsuleColors[UnityEngine.Random.Range(0, _capsuleColors.Length)],
            finalPosition + new Vector2(0, -2000));
        _capsule.TweenAnchoredPosition(finalPosition, 1f, Ease.Smoothstep);
        while (_capsule.IsMoving) yield return null;
        _capsule.StartOpen();
        PlayBoomSound();
        while (!_capsule.IsOpenComplete) yield return null;
        _capsule.CancelPresentation();
        _skinGachaCard.gameObject.SetActive(true);
        _skinGachaCard.SetData(finalItem, ResultIsNew(_getItemList.Count - 1));
        NotifyCollectionReveal(_getItemList.Count - 1);
        _skinGachaCard.SetPosition(finalPosition);
        _skinGachaCard.TweenStop();

        _skinGachaCard.transform.localScale = Vector3.one * 1.3f;
        _skinGachaCard.TweenScale(Vector3.one * 1f, 0.2f, Ease.OutBack);
        
        _getItemSound = _getItemList[_getItemList.Count - 1].Rank == Rank.Unique || _getItemList[_getItemList.Count - 1].Rank == Rank.Special ? _getSpecialItemSound : _getNormalItemSound;
        PlayGetItemSound();
        _summaryComplete = true;
        PrepareSummaryInspection();
        _screenTouchWaitTime = 0.2f;
    }

    private void PrepareSummaryInspection()
    {
        if (!_summaryComplete || _getItemList.Count != 11 || _getItemSlotList.Count != 10 || _summaryButtons.Count != 0) return;
        // As with staff results, the native ten-card grid must sit above the
        // full-screen result input surface. Its original hierarchy is restored.
        _summarySlotParent = _getItemSlotFrame.parent;
        _summarySlotSibling = _getItemSlotFrame.GetSiblingIndex();
        _getItemSlotFrame.SetParent(transform, true);
        for (int index = 0; index < 10; index++) BindSummaryCard(_getItemSlotList[index].gameObject, index);
        BindSummaryCard(_skinGachaCard.gameObject, 10);
        _bonusHitArea = new GachaResultCardHitArea(_skinGachaCard.transform);
        _screenButton.gameObject.SetActive(true);
        _screenButton.transform.SetAsLastSibling();
        _getItemSlotFrame.SetAsLastSibling();
        _skinGachaCard.transform.SetAsLastSibling();
    }

    private void BindSummaryCard(GameObject card, int index)
    {
        Button button = card.GetComponent<Button>();
        _summaryButtonStates.Add(button != null && button.enabled);
        if (button == null) button = card.AddComponent<Button>();
        button.enabled = true;
        button.transition = Selectable.Transition.None;
        UnityEngine.Events.UnityAction action = () => SelectResultCard(index);
        button.onClick.AddListener(action);
        _summaryButtons.Add(button);
        _summaryActions.Add(action);
    }

    public bool SelectResultCard(int index)
    {
        if (!_isShowingSummary || !_summaryComplete || _getItemList.Count != 11 || index < 0 || index >= 11
            || !gameObject.activeInHierarchy || _uiGacha == null || !_uiGacha.IsCurrentMachine(this)
            || _uiGacha.VisibleState != VisibleState.Appeared) return false;
        if (_resultPopup == null) _resultPopup = new GachaResultCardPopup(_uiGacha.transform, _skinGachaCard);
        _resultPopup.ShowItem(_getItemList[index], ResultIsNew(index));
        return _resultPopup.IsOpen;
    }

    private void ClearSummaryInspection()
    {
        _resultPopup?.Dispose();
        _resultPopup = null;
        _bonusHitArea?.Dispose();
        _bonusHitArea = null;
        for (int i = 0; i < _summaryButtons.Count; i++)
        {
            if (_summaryButtons[i] == null) continue;
            _summaryButtons[i].onClick.RemoveListener(_summaryActions[i]);
            _summaryButtons[i].enabled = _summaryButtonStates[i];
        }
        _summaryButtons.Clear();
        _summaryActions.Clear();
        _summaryButtonStates.Clear();
        if (_summarySlotParent != null && _getItemSlotFrame != null)
        {
            _getItemSlotFrame.SetParent(_summarySlotParent, true);
            _getItemSlotFrame.SetSiblingIndex(_summarySlotSibling);
        }
        _summarySlotParent = null;
    }
}
