using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIStaff : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private StaffController _staffController;
    [SerializeField] private UIStaffPreview _uiStaffPreview;
    [SerializeField] private UIStaffUpgrade _uiStaffUpgrade;
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

    private void OnDisable()
    {
        _uiRestaurantAdmin.MainUISetActive(true);
    }

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeStaffData(-1));
        _rightArrowButton.SetAction(() => ChangeStaffData(1));
        _uiStaffPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked, OnShowUpgradeButtonClicked);
        _uiStaffUpgrade.SetAction(OnUpgradeButtonClicked);

        _slots = new UIStaffSlot[_createSlotValue];
        for(int i = 0; i < _createSlotValue; ++i)
        {
            UIStaffSlot slot = Instantiate(_slotPrefab, _slotParnet);
            _slots[i] = slot;
            slot.Init((StaffData data) => _uiStaffPreview.SetStaffData(data));
        }

        UserInfo.OnChangeStaffHandler += OnSlotUpdate;
        UserInfo.OnUpgradeStaffHandler += OnSlotUpdate;
        UserInfo.OnGiveStaffHandler += OnSlotUpdate;
        UserInfo.OnChangeMoneyHandler += OnSlotUpdate;
        UserInfo.OnChangeScoreHanlder += OnSlotUpdate;

        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        _uiRestaurantAdmin.MainUISetActive(false);

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
        _uiRestaurantAdmin.MainUISetActive(true);
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

        StaffData equipStaffData = UserInfo.GetEquipStaff(type);

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
            if (equipStaffData != null && list[i].Id == equipStaffData.Id)
            {
                _slots[i].transform.SetAsFirstSibling();
                _slots[i].SetUse(list[i]);
                continue;
            }

            if (UserInfo.IsGiveStaff(list[i]))
            {
                _slots[i].SetOperate(list[i]);
                continue;
            }

            else
            {
                if (list[i].BuyMinScore <= UserInfo.Score && list[i].MoneyData.Price <= UserInfo.Money)
                {
                    _slots[i].SetEnoughMoney(list[i]);
                    continue;
                }

                _slots[i].SetLowReputation(list[i]);
                continue;
            }
        }

        for(int i = list.Count; i < _createSlotValue; ++i)
        {
            _slots[i].gameObject.SetActive(false);
        }
    }

    private void SetStaffPreview()
    {
        StaffData equipStaffData = UserInfo.GetEquipStaff(_currentType);
        _uiStaffPreview.SetStaffData(equipStaffData);
    }


    private void ChangeStaffData(int dir)
    {
        StaffType newTypeIndex = _currentType + dir;
        _currentType = newTypeIndex < 0 ? StaffType.Length - 1 : (StaffType)((int)newTypeIndex % (int)StaffType.Length);
        SetStaffData(_currentType);
        SetStaffPreview();
    }

    
    private void OnEquipButtonClicked(StaffData data)
    {
        _staffController.EquipStaff(data);
        SetStaffData(_currentType);
        SetStaffPreview();
    }

    private void OnBuyButtonClicked(StaffData data)
    {
        if (UserInfo.IsGiveStaff(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        if (UserInfo.Score < data.BuyMinScore)
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (UserInfo.Money < data.MoneyData.Price)
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        UserInfo.AppendMoney(-data.MoneyData.Price);
        UserInfo.GiveStaff(data);
        TimedDisplayManager.Instance.ShowText("새로운 직원을 채용했어요!");
    }

    private void OnUpgradeButtonClicked(StaffData data)
    {
        if (!UserInfo.IsGiveStaff(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        int recipeLevel = UserInfo.GetStaffLevel(data.Id);
        if (UserInfo.Score < data.GetUpgradeMinScore(recipeLevel))
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (UserInfo.Money < data.GetUpgradePrice(recipeLevel))
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        if (UserInfo.UpgradeStaff(data.Id))
        {
            TimedDisplayManager.Instance.ShowText("강화 성공!");
            return;
        }
    }

    public void ShowUIStaff(StaffType type)
    {
        _uiNav.Push("UIStaff");
        SetStaffData(type);
        SetStaffPreview();
    }

    private void OnSlotUpdate()
    {
        SetStaffData(_currentType);
    }

    private void OnShowUpgradeButtonClicked(StaffData data)
    {
        _uiNav.Push("UIStaffUpgrade");
        _uiStaffUpgrade.SetStaffData(data);
    }
}
