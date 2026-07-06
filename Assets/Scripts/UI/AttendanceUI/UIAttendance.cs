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
     
    public override void Init()
    {
        int startDay = UserInfo.GetAttendanceDays();
        List<AttendanceData> dataList = AttendanceDataManager.Instance.GetRewardDataList(startDay);
        int baseStartDay = ((startDay - 1) / 7) * 7 + 1;
        for (int i = 0, cnt = 7; i < cnt; i++)
        {
            UIAttendanceSlot slot = Instantiate(_slotPrefab, _slotParent.transform);
            _slotList.Add(slot);

            if (i >= dataList.Count)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            if(i == cnt - 1)
            {
                slot.SetDataToSpecial(dataList[i]);
            }
            else
            {
                slot.SetData(baseStartDay + i, dataList[i]);
            }

        }

        _attendanceButton.AddListener(() => OnAttendanceButtonClicked(false));
        _adButton.OnAdRewarded += () => OnAttendanceButtonClicked(true);
        gameObject.SetActive(false);
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

        // 현재 출석 일수 계산
        int days = UserInfo.GetAttendanceDays();
        int currentWeek = days / 7; // 현재 주차 계산 (0-based index)
        int currentDay = days % 7; // 해당 주의 몇 번째 날인지 계산

        // 보상 아이템 처리
        if (currentDay < _slotList.Count)
        {
            _slotList[currentDay].ReceiveItem(isAd);
        }

        UserInfo.UpdateAttendanceData();
        // 슬롯 UI 갱신
        UpdateUI();
    }


    private void UpdateUI()
    {
        bool checkAttendance = UserInfo.CheckNoAttendance();
        int adjustedDays = checkAttendance ? UserInfo.GetAttendanceDays() : UserInfo.GetAttendanceDays() - 1;

        // UI 갱신: 현재 주의 슬롯만 업데이트
        for (int i = 0; i < _slotList.Count; i++)
        {
            if (i < adjustedDays % 7)
            {
                _slotList[i].SetChecked(); // 이미 출석한 슬롯 표시
            }

            else if(i == adjustedDays % 7)
            {
                if(checkAttendance)
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
        float totalDays = UserInfo.GetAttendanceDays();
        float loadingBarGauge = totalDays <= 0 ? 0 : (totalDays % 7) / 6f; // 6일차에 1.0, 7일차에 0으로 초기화
        _loadingBar.SetFillAmount(loadingBarGauge);
    }
}
