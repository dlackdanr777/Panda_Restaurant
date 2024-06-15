using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIStaff : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIStaffPreview _uiStaffPreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _staffTypeText1;
    [SerializeField] private TextMeshProUGUI _staffTypeText2;

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

    private StaffType _currentType;
    private UIStaffSlot[] _slots;

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeStaffData(-1));
        _rightArrowButton.SetAction(() => ChangeStaffData(1));

        _slots = new UIStaffSlot[_createSlotValue];
        for(int i = 0; i < _createSlotValue; ++i)
        {
            UIStaffSlot slot = Instantiate(_slotPrefab, _slotParnet);
            _slots[i] = slot;
        }

        DataBind.SetUnityActionValue("ExitUIStaff", ExitUIStaff);
        DataBind.SetUnityActionValue("ShowUIStaffManager", ShowUIStaffManager);
        DataBind.SetUnityActionValue("ShowUIStaffaiter", ShowUIStaffWaiter);
        DataBind.SetUnityActionValue("ShowUIStaffChef", ShowUIStaffChef);
        DataBind.SetUnityActionValue("ShowUIStaffCleaner", ShowUIStaffCleaner);
        DataBind.SetUnityActionValue("ShowUIStaffMarketer", ShowUIStaffMarketer);

        gameObject.SetActive(false);
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


    private void SetStaffData(StaffType type)
    {
        _currentType = type;
        _uiStaffPreview.SetStaff(UserInfo.GetSEquipStaff(type));
        List<StaffData> list = StaffDataManager.Instance.GetStaffDataList(type);

        switch(type)
        {
            case StaffType.Manager:
                _staffTypeText1.text = "매니저";
                _staffTypeText2.text = "매니저";
                break;

            case StaffType.Waiter:
                _staffTypeText1.text = "웨이터";
                _staffTypeText2.text = "웨이터";
                break;

            case StaffType.Chef:
                _staffTypeText1.text = "셰프";
                _staffTypeText2.text = "셰프";
                break;

            case StaffType.Cleaner:
                _staffTypeText1.text = "청소부";
                _staffTypeText2.text = "청소부";
                break;

            case StaffType.Marketer:
                _staffTypeText1.text = "마케터";
                _staffTypeText2.text = "마케터";
                break;

            case StaffType.Guard:
                _staffTypeText1.text = "가드";
                _staffTypeText2.text = "가드";
                break;

            case StaffType.Server:
                _staffTypeText1.text = "서버";
                _staffTypeText2.text = "서버";
                break;
        }

        for (int i = 0, cnt = list.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            _slots[i].SetUse(list[i]);
        }

        for(int i = list.Count; i < _createSlotValue; ++i)
        {
            _slots[i].gameObject.SetActive(false);
        }
    }


    private void ChangeStaffData(int dir)
    {
        StaffType newTypeIndex = _currentType + dir;
        _currentType = newTypeIndex < 0 ? StaffType.Length - 1 : (StaffType)((int)newTypeIndex % (int)StaffType.Length);
        SetStaffData(_currentType);
    }


    private void ExitUIStaff()
    {
        _uiNav.PopNoAnime("UIStaff");
    }

    private void ShowUIStaffManager()
    {
        _uiNav.Push("UIStaff");
        SetStaffData(StaffType.Manager);
    }

    private void ShowUIStaffWaiter()
    {
        _uiNav.Push("UIStaff");
        SetStaffData(StaffType.Waiter);
    }

    private void ShowUIStaffChef()
    {
        _uiNav.Push("UIStaff");
        SetStaffData(StaffType.Chef);
    }

    private void ShowUIStaffCleaner()
    {
        _uiNav.Push("UIStaff");
        SetStaffData(StaffType.Cleaner);
    }

    private void ShowUIStaffMarketer()
    {
        _uiNav.Push("UIStaff");
        SetStaffData(StaffType.Marketer);
    }
}
