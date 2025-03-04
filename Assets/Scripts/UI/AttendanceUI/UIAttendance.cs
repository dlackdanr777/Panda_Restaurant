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
    [SerializeField] private UIButtonAndPressEffect _adButton;
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
        int startDay = UserInfo.GetTotalAttendanceDays();
        List<AttendanceData> dataList = AttendanceDataManager.Instance.GetRewardDataList(startDay);
        int baseStartDay = ((startDay - 1) / 7) * 7 + 1;
        for (int i = 0, cnt = 7; i < cnt; i++)
        {
            UIAttendanceSlot slot = Instantiate(_slotPrefab, _slotParent.transform);
            _slotList.Add(slot);

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
        _adButton.AddListener(() => OnAttendanceButtonClicked(true));
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
        GameManager.Instance.SaveGameData();
        SoundManager.Instance.PlayEffectAudio(_attendanceSound);
    }


    private void OnAttendanceCheck(bool isAd)
    {
        if (!UserInfo.CheckNoAttendance())
        {
            DebugLog.LogError("�̹� �⼮ üũ�� �����߽��ϴ�.");
            return;
        }

        // ���� �⼮ �ϼ� ���
        int totalDays = UserInfo.GetTotalAttendanceDays();
        int currentWeek = totalDays / 7; // ���� ���� ��� (0-based index)
        int currentDay = totalDays % 7; // �ش� ���� �� ��° ������ ���

        // ���� ������ ó��
        if (currentDay < _slotList.Count)
        {
            DebugLog.Log(currentWeek + "���� " + currentDay + "�� �⼮ üũ");
            _slotList[currentDay].ReceiveItem(isAd);
        }

        UserInfo.UpdateAttendanceData();
        // ���� UI ����
        UpdateUI();
    }


    private void UpdateUI()
    {
        bool checkAttendance = UserInfo.CheckNoAttendance();
        int adjustedDays = checkAttendance ? UserInfo.GetTotalAttendanceDays() : UserInfo.GetTotalAttendanceDays() - 1;

        // UI ����: ���� ���� ���Ը� ������Ʈ
        for (int i = 0; i < _slotList.Count; i++)
        {
            if (i < adjustedDays % 7)
            {
                _slotList[i].SetChecked(); // �̹� �⼮�� ���� ǥ��
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
                _slotList[i].SetUnchecked(); // �⼮���� ���� ���� ǥ��
            }
        }

        _attendanceButton.interactable = checkAttendance;
        _adButton.interactable = checkAttendance;
        float totalDays = UserInfo.GetTotalAttendanceDays();
        float loadingBarGauge = totalDays <= 0 ? 0 : (totalDays % 7) / 6f; // 6������ 1.0, 7������ 0���� �ʱ�ȭ
        _loadingBar.SetFillAmount(loadingBarGauge);
    }
}
