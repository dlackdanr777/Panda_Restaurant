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
    List<StaffData> _currentTypeDataList;

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
            slot.Init(OnSlotClicked);
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
        _currentTypeDataList = StaffDataManager.Instance.GetStaffDataList(type);

        switch(type)
        {
            case StaffType.Manager:
                _staffTypeText1.text = "�Ŵ���";
                _staffTypeText2.text = "�Ŵ���";
                break;

            case StaffType.Waiter:
                _staffTypeText1.text = "������";
                _staffTypeText2.text = "������";
                break;

            case StaffType.Chef:
                _staffTypeText1.text = "����";
                _staffTypeText2.text = "����";
                break;

            case StaffType.Cleaner:
                _staffTypeText1.text = "û�Һ�";
                _staffTypeText2.text = "û�Һ�";
                break;

            case StaffType.Marketer:
                _staffTypeText1.text = "������";
                _staffTypeText2.text = "������";
                break;

            case StaffType.Guard:
                _staffTypeText1.text = "����";
                _staffTypeText2.text = "����";
                break;

            case StaffType.Server:
                _staffTypeText1.text = "����";
                _staffTypeText2.text = "����";
                break;
        }

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            if (equipStaffData != null && _currentTypeDataList[i].Id == equipStaffData.Id)
            {
                _slots[i].transform.SetAsFirstSibling();
                _slots[i].SetUse(_currentTypeDataList[i]);
                _slots[i].SetOutline(true);
                continue;
            }
            _slots[i].SetOutline(false);

            if (UserInfo.IsGiveStaff(_currentTypeDataList[i]))
            {
                _slots[i].SetOperate(_currentTypeDataList[i]);
                continue;
            }

            else
            {
                if (_currentTypeDataList[i].BuyMinScore <= UserInfo.Score && _currentTypeDataList[i].MoneyData.Price <= UserInfo.Money)
                {
                    _slots[i].SetEnoughMoney(_currentTypeDataList[i]);
                    continue;
                }

                _slots[i].SetLowReputation(_currentTypeDataList[i]);
                continue;
            }
        }

        for(int i = _currentTypeDataList.Count; i < _createSlotValue; ++i)
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
        TimedDisplayManager.Instance.ShowText("���ο� ������ ä���߾��!");
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
            TimedDisplayManager.Instance.ShowText("��ȭ ����!");
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
        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0 || !gameObject.activeSelf)
            return;

        StaffData equipStaffData = UserInfo.GetEquipStaff(_currentType);

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            if (equipStaffData != null && _currentTypeDataList[i].Id == equipStaffData.Id)
            {
                _slots[i].transform.SetAsFirstSibling();
                _slots[i].SetUse(_currentTypeDataList[i]);
                continue;
            }

            if (UserInfo.IsGiveStaff(_currentTypeDataList[i]))
            {
                _slots[i].SetOperate(_currentTypeDataList[i]);
                continue;
            }

            else
            {
                if (_currentTypeDataList[i].BuyMinScore <= UserInfo.Score && _currentTypeDataList[i].MoneyData.Price <= UserInfo.Money)
                {
                    _slots[i].SetEnoughMoney(_currentTypeDataList[i]);
                    continue;
                }

                _slots[i].SetLowReputation(_currentTypeDataList[i]);
                continue;
            }
        }

        for (int i = _currentTypeDataList.Count; i < _createSlotValue; ++i)
        {
            _slots[i].gameObject.SetActive(false);
        }
    }

    private void OnShowUpgradeButtonClicked(StaffData data)
    {
        _uiNav.Push("UIStaffUpgrade");
        _uiStaffUpgrade.SetStaffData(data);
    }

    private void OnSlotClicked(StaffData data)
    {
        _uiStaffPreview.SetStaffData(data);
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; i++)
        {
            bool outlineEnabled = _currentTypeDataList[i] == data ? true : false;
            _slots[i].SetOutline(outlineEnabled);
        }
    }
}
