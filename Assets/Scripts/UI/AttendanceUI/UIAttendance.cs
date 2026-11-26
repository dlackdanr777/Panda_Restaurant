using Muks.MobileUI;
using Muks.BackEnd;
using System;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIAttendance : MobileUIView
{

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIButtonAndPressEffect _attendanceButton;
    [SerializeField] private WatchAdButton _adButton;
    [SerializeField] private UILoadingBar _loadingBar;



    [Space]
    [Header("Slots")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIAttendanceSlot _slotPrefab;


    [Space]
    [Header("Sound")]
    [SerializeField] private AudioClip _attendanceSound;



    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private List<UIAttendanceSlot> _slotList = new List<UIAttendanceSlot>();

    private struct SlotDisplay
    {
        public UIAttendanceSlot Slot;
        public AttendanceData Data;
        public MoneyType MoneyType;
        public int RewardValue, Day, State;
        public bool Bound, Visible, Special;
    }

    private SlotDisplay[] _slotDisplays = Array.Empty<SlotDisplay>();
    private bool _buttonDisplayValid, _adDisplayValid, _gaugeDisplayValid;
    private bool _lastButtonDisplay, _lastAdDisplay;
    private float _lastGaugeDisplay, _lastGaugeWidth;
#if UNITY_EDITOR
    private int _editorSlotDataApplications, _editorSlotStateApplications;
    private int _editorButtonApplications, _editorAdApplications, _editorGaugeApplications;
#endif

    private BackendManager _attendanceOwner;
    private GameDataRestoreQuery _adQuery;
    private string _adBefore;
    private DateTime _adDay;
    private bool _adPending, _adEarned, _adClosed;
    private string _lastSoundClaim;
    private float _nextRefresh;

    public override void Init()
    {
        for (int i = 0, cnt = 7; i < cnt; i++)
        {
            UIAttendanceSlot slot = Instantiate(_slotPrefab, _slotParent.transform);
            _slotList.Add(slot);
        }

        RefreshSlotRewardData(UserInfo.GetTodayAttendanceDay());

        _attendanceOwner = BackendManager.Instance;
        _attendanceOwner.AttendanceChanged += OnConfirmedAttendanceChanged;
        _attendanceButton.AddListener(() => OnAttendanceButtonClicked(false));
        _adButton.OnAdButtonClicked += BeginAttendanceAd;
        _adButton.OnAdRewarded += CompleteAttendanceAd;
        _adButton.OnAdDisplayFailed += CancelAttendanceAd;
        _adButton.OnAdClosed += CloseAttendanceAd;
        gameObject.SetActive(false);
    }

    // 오늘의 일차가 속한 주차로 슬롯 보상 데이터를 맞춤 (연속 출석 초기화·주차 전환 시에도 갱신되도록 UpdateUI에서도 호출)
    private void RefreshSlotRewardData(int todayDay)
    {
        int baseStartDay = ((todayDay - 1) / 7) * 7 + 1;
        var rewards = AttendanceDataManager.Instance.GetRewardDic();
        if (_slotDisplays.Length != _slotList.Count) Array.Resize(ref _slotDisplays, _slotList.Count);
        int sourceDay = baseStartDay;
        for (int i = 0; i < _slotList.Count; i++)
        {
            var slot = _slotList[i];
            var display = _slotDisplays[i];
            AttendanceData data = null;
            bool found = false;
            while (sourceDay < baseStartDay + 7)
            {
                if (rewards.TryGetValue(sourceDay++, out data)) { found = true; break; }
            }
            int day = baseStartDay + i;
            bool special = i == _slotList.Count - 1;
            bool changed = !display.Bound || display.Slot != slot || display.Visible != found
                || display.Day != day || display.Special != special || !ReferenceEquals(display.Data, data)
                || (data != null && (display.MoneyType != data.MoneyType || display.RewardValue != data.RewardValue));
            if (slot.gameObject.activeSelf != found) slot.gameObject.SetActive(found);
            if (changed)
            {
                display.Slot = slot;
                display.Data = data;
                display.Day = day;
                display.Visible = found;
                display.Special = special;
                display.Bound = true;
                display.State = -1;
                if (data != null) { display.MoneyType = data.MoneyType; display.RewardValue = data.RewardValue; }
                if (found)
                {
                    if (special) slot.SetDataToSpecial(data);
                    else slot.SetData(day, data);
#if UNITY_EDITOR
                    _editorSlotDataApplications++;
#endif
                }
            }
            _slotDisplays[i] = display;
        }
    }

    private void InvalidateDisplay()
    {
        Array.Clear(_slotDisplays, 0, _slotDisplays.Length);
        _buttonDisplayValid = _adDisplayValid = _gaugeDisplayValid = false;
    }

    public override void Show()
    {
        _animeUI.TweenStop();
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();
        InvalidateDisplay();
        UpdateUI();
        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

    }


    public override void Hide()
    {
        _animeUI.TweenStop();
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }



    private void BeginAttendanceAd()
    {
        if (_attendanceOwner == null || !_attendanceOwner.CanClaimAttendance(out _, out _)) return;
        _adQuery = _attendanceOwner.CurrentAttendanceQuery;
        _adBefore = UserInfo.LastAttendanceTime;
        _adDay = AttendanceProgress.GameDay(UserInfo.GetKoreanTime());
        _adPending = true; _adEarned = _adClosed = false;
        UpdateUI();
    }

    private bool IsAttendanceAdCurrent() => _adQuery != null && _attendanceOwner != null &&
        ReferenceEquals(_adQuery, _attendanceOwner.CurrentAttendanceQuery) &&
        _adBefore == UserInfo.LastAttendanceTime && _adDay == AttendanceProgress.GameDay(UserInfo.GetKoreanTime());

    private void CompleteAttendanceAd()
    {
        _adPending = _adClosed = false;
        if (!IsAttendanceAdCurrent()) return;
        _adEarned = true;
        OnAttendanceButtonClicked(true);
    }
    private void CancelAttendanceAd() { _adPending = _adEarned = _adClosed = false; _adQuery = null; UpdateUI(); }
    private void CloseAttendanceAd() { _adClosed = true; UpdateUI(); }

    private void OnAttendanceButtonClicked(bool isAd)
    {
        if (_attendanceOwner == null || (_adPending && !isAd)) return;
        bool doubled = _adEarned && IsAttendanceAdCurrent();
        if (isAd && !doubled) return;
        if (!_attendanceOwner.TryClaimAttendance(doubled, out _, out string error) && !string.IsNullOrEmpty(error))
            DebugLog.Log(error);
        UpdateUI();
    }

    private void OnConfirmedAttendanceChanged()
    {
        var claim = _attendanceOwner?.CurrentAttendanceClaim;
        if (claim?.Committed == true && claim.IsCurrent)
        {
            if (_lastSoundClaim != claim.Identity.RequestId && isActiveAndEnabled)
            {
                _lastSoundClaim = claim.Identity.RequestId;
                if (_attendanceSound != null && Application.isPlaying) SoundManager.Instance.PlayEffectAudio(EffectType.None, _attendanceSound);
            }
            _adEarned = _adPending = _adClosed = false; _adQuery = null;
        }
        if (isActiveAndEnabled) UpdateUI();
    }

    private void Update()
    {
        if (VisibleState != VisibleState.Appeared || Time.unscaledTime < _nextRefresh) return;
        if (_adPending && _adClosed && !AdManager.IsAdPlaying) _adPending = false;
        _nextRefresh = Time.unscaledTime + .2f;
        UpdateUI();
    }

    private void OnDisable()
    {
        _animeUI.TweenStop();
        _canvasGroup.interactable = _canvasGroup.blocksRaycasts = false;
        VisibleState = VisibleState.Disappeared;
    }

    private void OnDestroy()
    {
        if (_attendanceOwner != null) _attendanceOwner.AttendanceChanged -= OnConfirmedAttendanceChanged;
        if (_adButton == null) return;
        _adButton.OnAdButtonClicked -= BeginAttendanceAd;
        _adButton.OnAdRewarded -= CompleteAttendanceAd;
        _adButton.OnAdDisplayFailed -= CancelAttendanceAd;
        _adButton.OnAdClosed -= CloseAttendanceAd;
    }


    private void UpdateUI()
    {
        if (_adEarned && !IsAttendanceAdCurrent()) { _adEarned = false; _adQuery = null; }
        bool checkAttendance = UserInfo.CheckNoAttendance();
        int todayDay = UserInfo.GetTodayAttendanceDay();
        bool canClaim = !_adPending && _attendanceOwner != null && _attendanceOwner.CanClaimAttendance(out _, out _);
        BindConfirmedState(todayDay, checkAttendance, canClaim);
    }

    private void BindConfirmedState(int todayDay, bool checkAttendance, bool canClaim)
    {
        var rewards = AttendanceDataManager.Instance.GetRewardDic();
        bool hasReward = rewards.ContainsKey(todayDay);
        if (!hasReward)
        {
            int lastDay = 1;
            bool any = false;
            foreach (int day in rewards.Keys)
            {
                if (!any || day > lastDay) lastDay = day;
                any = true;
            }
            if (todayDay > lastDay) { todayDay = lastDay; checkAttendance = false; }
        }
        int todaySlotIndex = (todayDay - 1) % 7;
        RefreshSlotRewardData(todayDay);

        for (int i = 0; i < _slotList.Count; i++)
        {
            int state = i < todaySlotIndex ? 0 : i > todaySlotIndex ? 1 : checkAttendance ? 2 : 3;
            var display = _slotDisplays[i];
            if (display.State == state) continue;
            if (state == 0) _slotList[i].SetChecked();
            else if (state == 1) _slotList[i].SetUnchecked();
            else if (state == 2) _slotList[i].SetTotaySlotUnChecked();
            else _slotList[i].SetTotaySlotChecked();
            display.State = state;
            _slotDisplays[i] = display;
#if UNITY_EDITOR
            _editorSlotStateApplications++;
#endif
        }

        canClaim = canClaim && checkAttendance && hasReward;
        if (!_buttonDisplayValid || _lastButtonDisplay != canClaim || _attendanceButton.interactable != canClaim)
        {
            _attendanceButton.interactable = canClaim;
            _lastButtonDisplay = canClaim; _buttonDisplayValid = true;
#if UNITY_EDITOR
            _editorButtonApplications++;
#endif
        }
        bool canWatchAd = canClaim && !_adEarned;
        if (!_adDisplayValid || _lastAdDisplay != canWatchAd)
        {
            _adButton.Interactable(canWatchAd);
            _lastAdDisplay = canWatchAd; _adDisplayValid = true;
#if UNITY_EDITOR
            _editorAdApplications++;
#endif
        }
        float loadingBarGauge = AttendanceProgress.Fill(todayDay, !checkAttendance);
        float gaugeWidth = ((RectTransform)_loadingBar.transform).rect.width;
        if (!_gaugeDisplayValid || _lastGaugeDisplay != loadingBarGauge || _lastGaugeWidth != gaugeWidth)
        {
            _loadingBar.SetFillAmount(loadingBarGauge);
            _lastGaugeDisplay = loadingBarGauge; _lastGaugeWidth = gaugeWidth; _gaugeDisplayValid = true;
#if UNITY_EDITOR
            _editorGaugeApplications++;
#endif
        }
    }
}
