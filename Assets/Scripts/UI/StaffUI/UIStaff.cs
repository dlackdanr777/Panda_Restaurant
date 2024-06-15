using Muks.MobileUI;
using UnityEngine;
using System.Collections.Generic;
using Muks.Tween;
using Muks.DataBind;
using TMPro;
using UnityEngine.UI;

public class UIStaff : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIStaffPreview _preview;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _staffTypeText;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private int _createSlotValue;
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIStaffSlot _slotPrefab;

    private StaffType _type;


    private UIStaffSlot[] _slots;

    public override void Init()
    {
        _slots = new UIStaffSlot[_createSlotValue];
        for(int i = 0; i < _createSlotValue; ++i)
        {
            UIStaffSlot slot = Instantiate(_slotPrefab, _slotParnet);
            _slots[i] = slot;
        }

        gameObject.SetActive(false);

        DataBind.SetUnityActionValue("ExitUIStaff", ExitUIStaff);
        DataBind.SetUnityActionValue("ShowUIStaffManager", ShowUIStaffManager);
        DataBind.SetUnityActionValue("ShowUIStaffaiter", ShowUIStaffWaiter);
        DataBind.SetUnityActionValue("ShowUIStaffChef", ShowUIStaffChef);
        DataBind.SetUnityActionValue("ShowUIStaffCleaner", ShowUIStaffCleaner);
        DataBind.SetUnityActionValue("ShowUIStaffMarketer", ShowUIStaffMarketer);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        Debug.Log("실행");
        tween.OnComplete(() => 
        {
            Debug.Log("종료");
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true; 
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });

    }


    private void SetSaffData(StaffType type)
    {
        List<StaffData> list = StaffDataManager.Instance.GetStaffDataList(type);

        for(int i = 0, cnt = list.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            _slots[i].SetUse(list[i]);
        }

        for(int i = list.Count; i < _createSlotValue; ++i)
        {
            _slots[i].gameObject.SetActive(false);
        }
    }

    private void ExitUIStaff()
    {
        _uiNav.PopNoAnime("UIStaff");
    }

    private void ShowUIStaffManager()
    {
        _uiNav.Push("UIStaff");
        SetSaffData(StaffType.Manager);

        _preview.SetStaff(UserInfo.GetSEquipStaff(StaffType.Manager));
        _staffTypeText.text = "매니저";
    }

    private void ShowUIStaffWaiter()
    {
        _uiNav.Push("UIStaff");
        SetSaffData(StaffType.Waiter);
        _preview.SetStaff(UserInfo.GetSEquipStaff(StaffType.Waiter));
        _staffTypeText.text = "웨이터";
    }

    private void ShowUIStaffChef()
    {
        _uiNav.Push("UIStaff");
        SetSaffData(StaffType.Chef);
        _preview.SetStaff(UserInfo.GetSEquipStaff(StaffType.Chef));
        _staffTypeText.text = "셰프";
    }

    private void ShowUIStaffCleaner()
    {
        _uiNav.Push("UIStaff");
        SetSaffData(StaffType.Cleaner);
        _preview.SetStaff(UserInfo.GetSEquipStaff(StaffType.Cleaner));
        _staffTypeText.text = "청소부";
    }

    private void ShowUIStaffMarketer()
    {
        _uiNav.Push("UIStaff");
        SetSaffData(StaffType.Marketer);
        _preview.SetStaff(UserInfo.GetSEquipStaff(StaffType.Marketer));
        _staffTypeText.text = "마케터";
    }
}
