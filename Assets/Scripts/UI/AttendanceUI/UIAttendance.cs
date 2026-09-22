using Muks.MobileUI;
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

    public override void Init()
    {
        for (int i = 0, cnt = 7; i < cnt; i++)
        {
            UIAttendanceSlot slot = Instantiate(_slotPrefab, _slotParent.transform);
            _slotList.Add(slot);
        }

        RefreshSlotRewardData(UserInfo.GetTodayAttendanceDay());

        _attendanceButton.AddListener(() => OnAttendanceButtonClicked(false));
        _adButton.OnAdRewarded += () => OnAttendanceButtonClicked(true);
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
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
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



    private void OnAttendanceButtonClicked(bool isAd)
    {
        OnAttendanceCheck(isAd);
        GameManager.Instance.AsyncSaveGameData();
        SoundManager.Instance.PlayEffectAudio(EffectType.None, _attendanceSound);
    }


    private void OnAttendanceCheck(bool isAd)
    {
        if (!UserInfo.CheckNoAttendance())
        {
            DebugLog.LogError("이미 출석 체크를 진행했습니다.");
            return;
        }

        // 화면에 표시 중인 일차와 동일한 기준(GetTodayAttendanceDay)으로 지급할 슬롯을 결정
        int todayDay = UserInfo.GetTodayAttendanceDay();
        int currentDaySlot = (todayDay - 1) % 7;

        if (currentDaySlot < _slotList.Count)
        {
            _slotList[currentDaySlot].ReceiveItem(isAd);
        }

        UserInfo.UpdateAttendanceData();
        // 슬롯 UI 갱신
        UpdateUI();
    }


    private void UpdateUI()
    {
        bool checkAttendance = UserInfo.CheckNoAttendance();
        int todayDay = UserInfo.GetTodayAttendanceDay();
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

        _attendanceButton.interactable = checkAttendance;
        _adButton.Interactable(checkAttendance);
        float loadingBarGauge = todaySlotIndex / 6f; // 6일차에 1.0, 7일차에 0으로 초기화
        _loadingBar.SetFillAmount(loadingBarGauge);
    }
}
