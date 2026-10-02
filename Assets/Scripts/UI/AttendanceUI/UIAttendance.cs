using Muks.MobileUI;
using Muks.BackEnd;
using System;
using System.Linq;
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

    // 마지막으로 슬롯에 채워 넣은 주차의 시작일(1, 8, 15...). 주차가 바뀔 때만 슬롯 보상 데이터를 다시 채움
    private int _slotDataBaseStartDay = -1;

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
        if (baseStartDay == _slotDataBaseStartDay)
            return;

        _slotDataBaseStartDay = baseStartDay;
        List<AttendanceData> dataList = AttendanceDataManager.Instance.GetRewardDataList(todayDay);

        for (int i = 0, cnt = _slotList.Count; i < cnt; i++)
        {
            if (i >= dataList.Count)
            {
                _slotList[i].gameObject.SetActive(false);
                continue;
            }

            _slotList[i].gameObject.SetActive(true);
            if (i == cnt - 1)
            {
                _slotList[i].SetDataToSpecial(dataList[i]);
            }
            else
            {
                _slotList[i].SetData(baseStartDay + i, dataList[i]);
            }
        }
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
        int lastDay = AttendanceDataManager.Instance.GetRewardDic().Keys.DefaultIfEmpty(1).Max();
        bool hasReward = AttendanceDataManager.Instance.GetRewardDic().ContainsKey(todayDay);
        if (!hasReward && todayDay > lastDay) { todayDay = lastDay; checkAttendance = false; }
        int todaySlotIndex = (todayDay - 1) % 7;

        RefreshSlotRewardData(todayDay);

        // UI 갱신: 현재 주의 슬롯만 업데이트
        for (int i = 0; i < _slotList.Count; i++)
        {
            if (i < todaySlotIndex)
            {
                _slotList[i].SetChecked(); // 이미 출석한 슬롯 표시
            }
            else if (i == todaySlotIndex)
            {
                if (checkAttendance)
                {
                    _slotList[i].SetTotaySlotUnChecked();
                }
                else
                {
                    _slotList[i].SetTotaySlotChecked();
                }
            }
            else
            {
                _slotList[i].SetUnchecked(); // 출석하지 않은 슬롯 표시
            }
        }

        canClaim = canClaim && checkAttendance && hasReward;
        _attendanceButton.interactable = canClaim;
        _adButton.Interactable(canClaim && !_adEarned);
        float loadingBarGauge = AttendanceProgress.Fill(todayDay, !checkAttendance);
        _loadingBar.SetFillAmount(loadingBarGauge);
    }
}
