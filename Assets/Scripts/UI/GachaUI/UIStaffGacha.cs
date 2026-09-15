using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using TMPro;


public class UIStaffGacha : GachaMachineParent
{
    // Only the confirmed purchase service is enabled. Legacy grant/AnimationEvent entry points remain closed.
    private static readonly bool IsGachaExecutionEnabled = true;
    private const string UnavailableMessage = "이전 직원 지급 경로는 사용할 수 없습니다.";

    [Header("Components")]
    [SerializeField] private ScrollingImage _scrollImage;
    [SerializeField] private Animator _gachaMacineAnimator;
    [SerializeField] private Button _screenButton;
    [SerializeField] private Button _singleButton;
    public Button SingleButton => _singleButton;
    [SerializeField] private Button _tenButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Image _getStaffImage;
    [SerializeField] private UIGachaCard _gachaCard;
    [SerializeField] private GachaCapsule _capsule;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private Transform _getStaffSlotFrame;
    [SerializeField] private UIGachaCardSlot _slotPrefab;

    [Space]
    [Header("Capsule Options")]
    [SerializeField] private RectTransform _capsules;
    [SerializeField] private Image _upperCapsule;
    [SerializeField] private Image _lowerCapsule;
    [SerializeField] private Capsule[] _capsuleColors;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _getNormalStaffSound;
    [SerializeField] private AudioClip _getSpecialStaffSound;
    [SerializeField] private AudioSource _gachaSound;
    [SerializeField] private AudioClip _boomSound;


    private List<UIGachaCardSlot> _getStaffSlotList = new List<UIGachaCardSlot>();
    private List<GachaStaffData> _getStaffList = new List<GachaStaffData>();
    private float _screenTouchWaitTime;
    private int _currentStep;
    private int _getStaffIndex = 0;
    private bool _isCapsuleColorChanged;
    private bool _isPlayTextAnime;
    private AudioClip _getStaffSound;
    private bool _isInitialized;
    private StaffGachaPurchaseDisplay _purchaseDisplay;
    private Muks.BackEnd.BackendManager _purchaseDisplayOwner;
    private bool _hasExplicitPurchaseDisplayOwner;
    private StaffGachaPurchaseDisplay _questDisplay;
    private Muks.BackEnd.BackendManager _questOwner;
    private string _questId;
    private Button _questButton;
    private TextMeshProUGUI _questLabel;
    private bool _purchaseButtonsBound;
    private bool _startingPurchaseFromInput;

    internal bool IsQuestEntry => !string.IsNullOrEmpty(_questId);

    internal void PrepareQuestEntry(Muks.BackEnd.BackendManager owner, string questId)
    {
        if (owner == null || string.IsNullOrEmpty(questId))
            throw new ArgumentException("유효한 직원 안내가 필요합니다.");
        _purchaseDisplay?.Suspend();
        _questDisplay?.Dispose();
        _questOwner = owner;
        _questId = questId;
        _questDisplay = StaffGachaPurchaseDisplay.ForQuestGrant(this, _uiGacha, owner, questId,
            () => Muks.DataBind.DataBind.GetUnityActionBindData("HideGachaUI").Item?.Invoke());
    }

    internal void ClearQuestEntry()
    {
        _questDisplay?.Dispose();
        _questDisplay = null;
        _questOwner = null;
        _questId = null;
        if (_questButton != null) _questButton.gameObject.SetActive(false);
    }

    private void UpdateQuestButton()
    {
        if (!IsQuestEntry || _questOwner == null) return;
        if (_questButton == null)
        {
            // A separate tutorial input reuses the existing button art; paid listeners/state stay untouched.
            _questButton = Instantiate(_singleButton, _singleButton.transform.parent, false);
            _questButton.name = "Quest Staff Claim";
            _questButton.onClick = new Button.ButtonClickedEvent();
            _questButton.onClick.AddListener(OnQuestStaffButtonClicked);
            SetButtonUnavailable(_questButton);
            var rect = (RectTransform)_questButton.transform;
            rect.anchoredPosition = (((RectTransform)_singleButton.transform).anchoredPosition +
                ((RectTransform)_tenButton.transform).anchoredPosition) * 0.5f;
            _questLabel = _questButton.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        }
        _singleButton.gameObject.SetActive(false);
        _tenButton.gameObject.SetActive(false);
        if (_uiGacha.IsStartGacha) return; // The animation owns its captured input state until close.
        _questButton.gameObject.SetActive(true);
        bool available = _questOwner.TryGetCurrentQuestStaffOffer(out var offer, out _) && offer.QuestId == _questId;
        _questButton.interactable = available;
        var press = _questButton.GetComponent<ButtonPressEffect>();
        if (press != null) press.Interactable = available;
        if (_questLabel != null)
            _questLabel.text = available ? "무료 직원 뽑기" :
                (_questOwner.LastCompletedQuestStaffGrant?.QuestId == _questId ? "획득 완료" : "확인 중");
    }

    private void OnQuestStaffButtonClicked()
    {
        // The open view is not an entitlement; check the current quest again at the input boundary.
        if (!IsQuestEntry || _questOwner == null || _uiGacha.IsStartGacha ||
            !_questOwner.TryGetCurrentQuestStaffOffer(out var offer, out _) || offer.QuestId != _questId)
            return;
        if (!_questOwner.TryStartQuestStaffGrant(out _, out string error))
        {
            DebugLog.Log(error);
            PopupManager.Instance.ShowDisplayText("직원을 받을 수 없습니다. 현재 진행 상태를 확인해 주세요.");
        }
        UpdateQuestButton();
    }

    /// <summary>Bind a detached owner before this scene display is activated. No singleton is resolved.</summary>
    public void BindPurchaseDisplayOwner(UIGacha view, Muks.BackEnd.BackendManager owner)
    {
        if (gameObject.activeInHierarchy)
            throw new InvalidOperationException("직원 결과 표시의 소유자는 활성화 전에 연결해야 합니다.");
        if (view == null || owner == null) throw new ArgumentNullException(view == null ? nameof(view) : nameof(owner));
        _purchaseDisplay?.Dispose();
        _hasExplicitPurchaseDisplayOwner = true;
        _purchaseDisplayOwner = owner;
        _uiGacha = view;
        _purchaseDisplay = new StaffGachaPurchaseDisplay(this, view, owner);
    }

#if UNITY_EDITOR
    /// <summary>Initialize only the existing display bindings on an inactive, disposable scene copy.</summary>
    public void ConfigureEditorOffline(Muks.BackEnd.BackendManager owner, UIGacha view)
    {
        if (owner == null || !owner.IsEditorOfflineOwner)
            throw new InvalidOperationException("오프라인 전용 결과 소유자가 필요합니다.");
        BindPurchaseDisplayOwner(view, owner);
        // fireEvents is native runtime state and is not retained across entering Play.
        // Suppress the copied staff machine before its first activation, not just at extraction.
        _gachaMacineAnimator.fireEvents = false;
        _isInitialized = true;
        _itemDataList = new List<GachaData>();
        _scrollImage.Init();
        _gachaCard.Init();
        ConfigurePurchaseButtons();
    }

    public StaffGachaPurchaseDisplay EditorOfflinePurchaseDisplay => _hasExplicitPurchaseDisplayOwner ? _purchaseDisplay : null;
#endif

    // Explicit display-only bindings; no runtime UnityEditor/reflection dependency.
    internal Animator ResultAnimator => _gachaMacineAnimator;
    internal UIGachaCard ResultCard => _gachaCard;
    internal Image ResultImage => _getStaffImage;
    internal RectTransform ResultCapsules => _capsules;
    internal AudioSource ResultAudio => _gachaSound;
    internal AudioClip ResultBoom => _boomSound;
    internal Transform ResultSlots => _getStaffSlotFrame;
    internal Button ResultEntryButton => IsQuestEntry ? _questButton : _singleButton;
    internal Button[] ResultControlButtons => _questButton == null
        ? new[] { _singleButton, _tenButton, _screenButton, _skipButton }
        : new[] { _singleButton, _tenButton, _screenButton, _skipButton, _questButton };
    internal AudioClip GetResultSound(Rank rank) => rank == Rank.Unique || rank == Rank.Special
        ? _getSpecialStaffSound : _getNormalStaffSound;


    public void PlayGetStaffSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _getStaffSound);
    }

    public void PlayGachaSound()
    {
        _gachaSound.Play();
    }

    public void PlayBoomSound()
    {
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _boomSound);
    }

    public override void Init(UIGacha uiGacha)
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        _uiGacha = uiGacha;
        _scrollImage.Init();
        _gachaCard.Init();
        _itemDataList = StaffDataManager.Instance.GetSortGachaStaffDataList(GradeSortType.GradeDescending).Select((data) => (GachaData)data).ToList();

        for (int i = 0; i < 10; ++i)
        {
            UIGachaCardSlot slot = Instantiate(_slotPrefab, _getStaffSlotFrame);
            _getStaffSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }

        _screenButton.onClick.AddListener(OnScreenButtonClicked);
        BindPurchaseButtons();
        _skipButton.onClick.AddListener(OnSkipButtonClicked);

        ConfigurePurchaseButtons();
        SetStep(1);
        _gachaCard.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void BindPurchaseButtons()
    {
        if (_purchaseButtonsBound) return;
        _singleButton.onClick.AddListener(OnSingleGachaButtonClicked);
        _tenButton.onClick.AddListener(OnTenGachaButtonClicked);
        _purchaseButtonsBound = true;
    }

    private void ConfigurePurchaseButtons()
    {
        ConfigurePurchaseButton(_singleButton, StaffGachaPurchaseType.Single, "1회");
        ConfigurePurchaseButton(_tenButton, StaffGachaPurchaseType.Multi, "10+1회");
        RefreshPurchaseButtonState();
    }

    private static void ConfigurePurchaseButton(Button button, StaffGachaPurchaseType type, string text)
    {
        if (button == null) return;
        var label = button.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (label != null) label.SetText(text); // Preserve the original icon/text RectTransforms.
        Transform priceGroup = button.transform.Find("Money Image");
        if (priceGroup != null)
        {
            priceGroup.gameObject.SetActive(true);
            var price = priceGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            if (price != null && StaffGachaPurchasePlanCalculator.TryGetPolicy(type, out int cost, out _))
                price.SetText(cost.ToString());
        }
        var description = button.transform.Find("Description Text")?.GetComponent<TextMeshProUGUI>();
        if (description != null)
        {
            description.gameObject.SetActive(true);
            description.SetText(type == StaffGachaPurchaseType.Multi ? "1회 추가" : "1회");
        }
    }

    private bool CanUsePaidInput()
    {
        if (!IsGachaExecutionEnabled || !_isInitialized || _startingPurchaseFromInput || IsQuestEntry
            || !gameObject.activeInHierarchy || _uiGacha == null || _uiGacha.IsStartGacha
            || _uiGacha.VisibleState != VisibleState.Appeared || _gachaMacineAnimator == null
            || !_gachaMacineAnimator.enabled || _purchaseDisplayOwner == null) return false;
        // A current free offer is not bypassed by entering the ordinary machine from another shortcut.
        return !_purchaseDisplayOwner.TryGetCurrentQuestStaffOffer(out _, out _);
    }

    private void RefreshPurchaseButtonState()
    {
        // The result animation owns active/input presentation until Close restores it.
        if (_uiGacha != null && _uiGacha.IsStartGacha) return;
        bool visible = CanUsePaidInput();
        SetPaidInteractable(_singleButton, visible && _purchaseDisplayOwner.CanStartStaffPurchase(StaffGachaPurchaseType.Single, out _));
        SetPaidInteractable(_tenButton, visible && _purchaseDisplayOwner.CanStartStaffPurchase(StaffGachaPurchaseType.Multi, out _));
    }

    private static void SetPaidInteractable(Button button, bool value)
    {
        if (button == null) return;
        button.interactable = value;
        var press = button.GetComponent<ButtonPressEffect>();
        if (press != null)
        {
            if (Application.isPlaying && press.Interactable && !value && button.gameObject.activeInHierarchy)
                press.ResetScale(); // A reservation may disable input between pointer-down and pointer-up.
            press.Interactable = value;
        }
    }

    private static void SetButtonUnavailable(Button button)
    {
        if (button == null)
            return;

        button.interactable = false;
        ButtonPressEffect pressEffect = button.GetComponent<ButtonPressEffect>();
        if (pressEffect != null)
            pressEffect.Interactable = false;

        Transform priceGroup = button.transform.Find("Money Image");
        if (priceGroup != null)
            priceGroup.gameObject.SetActive(false);

        Transform description = button.transform.Find("Description Text");
        if (description != null)
            description.gameObject.SetActive(false);

        Transform labelTransform = button.transform.Find("Text");
        TextMeshProUGUI label = labelTransform == null ? null : labelTransform.GetComponent<TextMeshProUGUI>();

        if (label != null)
        {
            label.SetText("준비 중");
            label.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private void Update()
    {
        if (IsQuestEntry)
        {
            UpdateQuestButton();
            _questDisplay?.Tick();
        }
        else
        {
            _purchaseDisplay?.Tick();
            RefreshPurchaseButtonState();
        }
        if( 0 < _screenTouchWaitTime)
            _screenTouchWaitTime -= Time.deltaTime;
    }


    public override void Show()
    {
        if (!IsQuestEntry && _purchaseDisplay == null)
        {
            var owner = _hasExplicitPurchaseDisplayOwner ? _purchaseDisplayOwner : Muks.BackEnd.BackendManager.Instance;
            if (owner == null) return; // An invalid detached owner never falls back to the live account.
            _purchaseDisplayOwner = owner;
            _purchaseDisplay = new StaffGachaPurchaseDisplay(this, _uiGacha, owner);
        }
        gameObject.SetActive(true);
        SetActiveGachaMachine(true);
        _singleButton.gameObject.SetActive(true);
        _tenButton.gameObject.SetActive(true);
        ConfigurePurchaseButtons();
        _uiGacha.SetActiveUIComponents(true);
        _scrollImage.gameObject.SetActive(true);
        _screenButton.gameObject.SetActive(false);
        _gachaCard.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
        _capsule.gameObject.SetActive(false);
        CapsuleSetSibilingIndex(1);

        SetStep(1);
        _screenTouchWaitTime = 0;
        _gachaMacineAnimator.enabled = true;
        OnScreenButtonClicked();
        UpdateQuestButton();
        RefreshPurchaseButtonState();
    }


    public override void Hide()
    {
        _questDisplay?.Suspend();
        if (_questButton != null) _questButton.gameObject.SetActive(false);
        _purchaseDisplay?.Suspend();
        gameObject.SetActive(true);
        StopAllCoroutines();
        _screenTouchWaitTime = 0;
        if (_gachaSound != null)
            _gachaSound.Stop();
        SetStep(1);
        _singleButton.gameObject.SetActive(false);
        _tenButton.gameObject.SetActive(false);
        _uiGacha.SetActiveUIComponents(false);
        _scrollImage.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _gachaCard.gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
        _getStaffImage.gameObject.SetActive(false);
        _getStaffSlotFrame.gameObject.SetActive(false);
        _capsule.gameObject.SetActive(false);
        _gachaMacineAnimator.enabled = false;
    }

    private void OnDisable()
    {
        _questDisplay?.Suspend();
        _purchaseDisplay?.Suspend();
    }

    private void OnDestroy()
    {
        _questDisplay?.Dispose();
        _purchaseDisplay?.Dispose();
    }


    public void GetStaff(GachaStaffData data)
    {
        // Retained for serialized/old callers, never an alternative to a save-confirmed result.
        DebugLog.Log(UnavailableMessage);
    }

    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }


    public override void OnScreenButtonClicked()
    {
        if (_uiGacha != null && _uiGacha.IsStartGacha) return;
        if (0 < _screenTouchWaitTime)
        {
            DebugLog.Log("아직 터치할 수 없습니다.");
            return;
        }
        _gachaSound.Stop();
        switch (_currentStep)
        {
            case 1:
                StopAllCoroutines(); 
                _gachaMacineAnimator.SetTrigger("Stop");
                break;

            case 2:
                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;

            case 3:
                _gachaMacineAnimator.SetTrigger("CapsuleOpen");
                break;

            case 5:
                if (_getStaffList.Count <= _getStaffIndex)
                {
                    _gachaMacineAnimator.SetTrigger("Stop");
                    return;
                }

                if(_isPlayTextAnime)
                {
                    _gachaCard.TweenStop();
                    _gachaCard.gameObject.SetActive(true);
                    _gachaCard.SetData(_getStaffList[_getStaffIndex - 1]);
                    _isPlayTextAnime = false;
                    _screenTouchWaitTime = 0.5f;
                    return;
                }
                if (_getStaffList.Count() <= _getStaffIndex - 1)
                {
                    OnSkipButtonClicked();
                }
                else
                {
                    for (int i = 0, cnt = _getStaffSlotList.Count; i < cnt; i++)
                    {
                        _getStaffSlotList[i].gameObject.SetActive(false);
                    }
                }

                _gachaMacineAnimator.SetTrigger("Step2Skip");
                break;
        
        }

    }

    public void SetStep(int step)
    {
        if (step != 1)
        {
            DebugLog.Log(UnavailableMessage);
            return; // A stale animation event must not reset/unlock a current confirmed presentation either.
        }

        if (_uiGacha != null && _uiGacha.IsStartGacha) return;

        if (_currentStep == step)
            return;

        switch (step)
        {
            case 1:
                _currentStep = 1;
                StopAllCoroutines();
                _uiGacha.SetActiveUIComponents(true);
                _uiGacha.SetActiveGachaMachine(true);
                _uiGacha.SetStartGacha(false);
                _singleButton.gameObject.SetActive(true);
                _tenButton.gameObject.SetActive(true);
                ConfigurePurchaseButtons();
                _screenButton.gameObject.SetActive(false);
                _gachaCard.gameObject.SetActive(false);
                _skipButton.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _gachaCard.TweenStop();

                for (int i = 0, cnt = _getStaffSlotList.Count; i < cnt; i++)
                {
                    _getStaffSlotList[i].gameObject.SetActive(false);
                    _getStaffSlotList[i].TweenStop();
                }

                _getStaffIndex = 0;
                _isCapsuleColorChanged = true;
                CapsuleSetSibilingIndex(1);
                break;

            case 2:
                _currentStep = 2;
                _screenButton.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(10 <= _getStaffList.Count());
                _uiGacha.SetActiveUIComponents(false);
                _uiGacha.SetStartGacha(true);
                _singleButton.gameObject.SetActive(false);
                _tenButton.gameObject.SetActive(false);
                _gachaCard.gameObject.SetActive(false);
                _getStaffSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();
                CapsuleSetSibilingIndex(1);
                break;

            case 3:
                _currentStep = 3;

                _screenButton.gameObject.SetActive(true);
                _getStaffImage.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(10 <= _getStaffList.Count());
                _gachaCard.gameObject.SetActive(false);
                _getStaffSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getStaffImage.sprite = _getStaffList[_getStaffIndex].ThumbnailSprite;
                CapsuleSetSibilingIndex(11);
                break;

            case 4:
                _currentStep = 4;

                _skipButton.gameObject.SetActive(10 <= _getStaffList.Count());
                _getStaffImage.gameObject.SetActive(true);
                _gachaCard.gameObject.SetActive(false);
                _getStaffSlotFrame.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);
                _screenTouchWaitTime = 0.2f;
                CapsuleColorChange();

                _getStaffImage.sprite = _getStaffList[_getStaffIndex].ThumbnailSprite;
                CapsuleSetSibilingIndex(11);
                break;

            case 5:
                _currentStep = 5;
                _gachaCard.gameObject.SetActive(false);
                _getStaffSlotFrame.gameObject.SetActive(true);
                _skipButton.gameObject.SetActive(false);
                _getStaffImage.gameObject.SetActive(false);
                _capsule.gameObject.SetActive(false);

                _isCapsuleColorChanged = true;

                _isPlayTextAnime = true;
                if (10 <= _getStaffList.Count() && _getStaffList.Count() <= _getStaffIndex + 1)
                {

                    OnSkipButtonClicked();
                }
                else
                {
                    _screenTouchWaitTime = 0.2f;
                    _getStaffSound = _getStaffList[_getStaffIndex].Rank == Rank.Unique || _getStaffList[_getStaffIndex].Rank == Rank.Special ? _getSpecialStaffSound : _getNormalStaffSound;
                    PlayGetStaffSound();

                    _gachaCard.gameObject.SetActive(true);
                    _gachaCard.SetData(_getStaffList[_getStaffIndex]);
                    _gachaCard.ResetScale();
                    _getStaffImage.gameObject.SetActive(true);
                    _getStaffImage.sprite = _getStaffList[_getStaffIndex].ThumbnailSprite;
                    _gachaCard.SetPosition(new Vector3(0, 0, 0));
                }

                _getStaffIndex++;
                break;
        }
        
        _uiGacha.StartGachaStepEvent(_currentStep);
    }

    public void StartAddStaff(GachaStaffData data)
    {
        DebugLog.Log(UnavailableMessage);
    }

    public override void OnSingleGachaButtonClicked()
    {
        StartStaffPurchase(StaffGachaPurchaseType.Single);
    }

    public override void OnTenGachaButtonClicked()
    {
        StartStaffPurchase(StaffGachaPurchaseType.Multi);
    }

    private void StartStaffPurchase(StaffGachaPurchaseType type)
    {
        if (!CanUsePaidInput() || !_purchaseDisplayOwner.CanStartStaffPurchase(type, out _))
        {
            RefreshPurchaseButtonState();
            return;
        }
        _startingPurchaseFromInput = true; // Includes synchronous completion/notification reentry.
        try
        {
            _purchaseDisplay?.Close(); // Close only the old display before a possibly synchronous response.
            if (!_purchaseDisplayOwner.TryStartStaffPurchase(type, out _, out string error))
            {
                DebugLog.Log(error);
                if (!_hasExplicitPurchaseDisplayOwner)
                    PopupManager.Instance.ShowDisplayText("직원 뽑기를 진행할 수 없습니다. 잠시 후 다시 시도해 주세요.");
            }
        }
        finally { _startingPurchaseFromInput = false; RefreshPurchaseButtonState(); }
    }


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
        // Legacy skip is not the save-confirmed result-card navigation.
        DebugLog.Log(UnavailableMessage);
    }
    

    private IEnumerator SkipRoutine()
    {
        _gachaCard.gameObject.SetActive(false);
        _getStaffImage.gameObject.SetActive(false);
        _gachaMacineAnimator.SetTrigger("SkipButtonClick");
        _gachaSound.Stop();
        _getStaffSlotFrame.gameObject.SetActive(true);

        for (int i = 0, cnt = _getStaffSlotList.Count; i < cnt; i++)
        {
            _getStaffSlotList[i].gameObject.SetActive(false);
        }
        yield return null;
        _gachaCard.gameObject.SetActive(false);
        for (int i = 0, cnt = _getStaffList.Count - 1; i < cnt; i++)
        {
            _getStaffSlotList[i].SetData(_getStaffList[i]);
            _getStaffSlotList[i].gameObject.SetActive(true);
            _getStaffSlotList[i].TweenStop();
            _getStaffSlotList[i].transform.localScale = Vector3.one * 1.2f;
            _getStaffSlotList[i].TweenScale(Vector3.one, 0.2f, Ease.OutBack);
            yield return YieldCache.WaitForSeconds(0.1f);
        }
        yield return YieldCache.WaitForSeconds(0.1f);
        _capsule.gameObject.SetActive(true);
        _capsule.SetCapsuleColor(_capsuleColors[UnityEngine.Random.Range(0, _capsuleColors.Length)]);
        _capsule.TweenStop();
        _capsule.SetAnchoredPosition(new Vector2(0, -2000));
        _capsule.TweenAnchoredPosition(new Vector2(0, 0), 1f, Ease.Smoothstep);

        yield return YieldCache.WaitForSeconds(1.5f);

        _capsule.StartOpen();
        _capsule.SetSprite(_getStaffList[_getStaffList.Count - 1].ThumbnailSprite);

        yield return YieldCache.WaitForSeconds(1.5f);
        _capsule.gameObject.SetActive(false);
        _gachaCard.gameObject.SetActive(true);
        _gachaCard.SetData(_getStaffList[_getStaffList.Count - 1]);
        _gachaCard.SetPosition(new Vector3(600, 0, 0));

        _getStaffSound = _getStaffList[_getStaffList.Count - 1].Rank == Rank.Unique || _getStaffList[_getStaffList.Count - 1].Rank == Rank.Special ? _getSpecialStaffSound : _getNormalStaffSound;
        PlayGetStaffSound();
        _gachaCard.TweenStop();
        _gachaCard.transform.localScale = Vector3.one * 1.3f;
        _gachaCard.TweenScale(Vector3.one * 1f, 0.2f, Ease.OutBack);

    }
}
