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
    private static readonly bool IsGachaExecutionEnabled = false;
    private const string UnavailableMessage = "직원 뽑기는 준비 중입니다.";

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
        _singleButton.onClick.AddListener(OnSingleGachaButtonClicked);
        _tenButton.onClick.AddListener(OnTenGachaButtonClicked);
        _skipButton.onClick.AddListener(OnSkipButtonClicked);

        ApplyUnavailableButtonState();
        SetStep(1);
        _gachaCard.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void ApplyUnavailableButtonState()
    {
        SetButtonUnavailable(_singleButton);
        SetButtonUnavailable(_tenButton);
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

    private static bool BlockUnavailableExecution()
    {
        if (IsGachaExecutionEnabled)
            return false;

        DebugLog.Log(UnavailableMessage);
        return true;
    }

    private void Update()
    {
        if( 0 < _screenTouchWaitTime)
            _screenTouchWaitTime -= Time.deltaTime;
    }


    public override void Show()
    {
        gameObject.SetActive(true);
        SetActiveGachaMachine(true);
        _singleButton.gameObject.SetActive(true);
        _tenButton.gameObject.SetActive(true);
        ApplyUnavailableButtonState();
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
    }


    public override void Hide()
    {
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


    public void GetStaff(GachaStaffData data)
    {
        if (BlockUnavailableExecution())
            return;

        if (data == null || data.StaffData == null)
        {
            DebugLog.LogError("지급할 직원 데이터가 없습니다.");
            return;
        }

        _getStaffList.Clear();
        _getStaffIndex = 0;

        _getStaffList.Add(data);
        UserInfo.GiveStaff(UserInfo.CurrentStage, data.StaffData);

        _gachaMacineAnimator.SetTrigger("Start");
    }

    public void CapsuleSetSibilingIndex(int index)
    {
        _capsules.SetSiblingIndex(index);
    }


    public override void OnScreenButtonClicked()
    {
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
        if (!IsGachaExecutionEnabled && step != 1)
        {
            DebugLog.Log(UnavailableMessage);
            step = 1;
        }

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
                ApplyUnavailableButtonState();
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
        if (BlockUnavailableExecution())
            return;

        if (data == null || data.StaffData == null)
        {
            DebugLog.LogError("지급할 직원 데이터가 없습니다.");
            return;
        }

        _uiGacha.SetActiveGachaMachine(false);
        SetActiveGachaMachine(true);

        _getStaffList.Clear();
        _getStaffIndex = 0;
        GachaStaffData staff = data;
        _getStaffList.Add(staff);
        UserInfo.GiveStaff(UserInfo.CurrentStage, staff.StaffData);
        _gachaMacineAnimator.SetTrigger("Start");
        UserInfo.AddUserGachaMachineCount();
    }

    public override void OnSingleGachaButtonClicked()
    {
        if (BlockUnavailableExecution())
            return;

        if(UserInfo.IsDiaValid(10))
        {
            GachaStaffData staff = StaffDataManager.GetRandomGachaStaffData(_itemDataList) as GachaStaffData;
            if (staff == null || staff.StaffData == null)
            {
                DebugLog.LogError("직원 단일 가챠 추첨에 실패했습니다.");
                return;
            }

            _uiGacha.SetActiveGachaMachine(false);
            SetActiveGachaMachine(true);
        
            _getStaffList.Clear();
            _getStaffIndex = 0;
            _getStaffList.Add(staff);
            UserInfo.GiveStaff(UserInfo.CurrentStage, staff.StaffData);

            _gachaMacineAnimator.SetTrigger("Start");
            UserInfo.AddDia(-10);
            UserInfo.AddUserGachaMachineCount();
            GameManager.Instance.AsyncSaveGameData();
            PaymentInfo.AddGachaData($"Normal Staff Gacha 1");
            PaymentInfo.SavePaymentData();
        }

        else
        {
            PopupManager.Instance.ShowTextLackDia();
        }
    }


    public override void OnTenGachaButtonClicked()
    {
        if (BlockUnavailableExecution())
            return;

        if(UserInfo.IsDiaValid(100))
        {
            List<GachaStaffData> selectedStaffList = new List<GachaStaffData>(11);
            for (int i = 0; i < 11; i++)
            {
                GachaStaffData selectedStaff =
                    StaffDataManager.GetRandomGachaStaffData(_itemDataList) as GachaStaffData;
                if (selectedStaff == null || selectedStaff.StaffData == null)
                {
                    DebugLog.LogError("직원 다회 가챠 추첨에 실패했습니다.");
                    return;
                }

                selectedStaffList.Add(selectedStaff);
            }

            _uiGacha.SetActiveGachaMachine(false);
            SetActiveGachaMachine(true);

            _getStaffList.Clear();
            _getStaffIndex = 0;
            _getStaffList.AddRange(selectedStaffList);

            foreach (var staffData in _getStaffList)
            {
                UserInfo.GiveStaff(UserInfo.CurrentStage, staffData.StaffData);
            }

            _gachaMacineAnimator.SetTrigger("Start");
            UserInfo.AddDia(-100);
            UserInfo.AddUserGachaMachineCount(11);
            GameManager.Instance.AsyncSaveGameData();
            PaymentInfo.AddGachaData($"Normal Staff Gacha 11");
            PaymentInfo.SavePaymentData();
        }
        else
        {
            PopupManager.Instance.ShowTextLackDia();
        }

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
        _screenTouchWaitTime = 5f;
        _getStaffIndex = _getStaffList.Count - 1;
        StopAllCoroutines();
        StartCoroutine(SkipRoutine());
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
