using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIStaff : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private UIStaffUpgrade _uiStaffUpgrade;
    [SerializeField] private StaffController _staffController;
    [SerializeField] private UIStaffPreview _uiStaffPreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Slot Option")]
    [SerializeField] private int _createSlotValue;
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminStaffSlot _slotPrefab;


    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _dequipSound;

    private EquipStaffType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminStaffSlot>[] _slots = new List<UIRestaurantAdminStaffSlot>[(int)EquipStaffType.Length];
    List<StaffData> _currentTypeDataList;


    public override void Init()
    {
        _leftArrowButton.AddListener(() => ChangeStaffData(-1));
        _rightArrowButton.AddListener(() => ChangeStaffData(1));
        _uiStaffPreview.Init(OnEquipButtonClicked, OnUsingButtonClicked, OnBuyButtonClicked, OnUpgradeButtonClicked);

        for (int i = 0, cntI = (int)EquipStaffType.Length; i < cntI; ++i)
        {
            List<StaffData> typeDataList = StaffDataManager.Instance.GetSortStaffDataList((EquipStaffType)i);
            _slots[i] = new List<UIRestaurantAdminStaffSlot>();
            for (int j = 0, cntJ = typeDataList.Count; j < cntJ; ++j)
            {
                int index = j;
                UIRestaurantAdminStaffSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[index]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }

        UserInfo.OnChangeStaffHandler += OnChangeStaffEvent;
        UserInfo.OnGiveStaffHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;

        SetStaffData(EquipStaffType.Manager);
        SetStaffPreview();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        SetStaffData(EquipStaffType.Manager);
        SetStaffPreview();
        _uiRestaurantAdmin.ShowStaffTab();
        _uiRestaurantAdmin.MainUISetActive(false);
        transform.SetAsLastSibling();

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() => 
        {

            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true; 
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        _animeUI.SetActive(true);
        _uiRestaurantAdmin.MainUISetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    public void ShowUIStaff(ERestaurantFloorType floorType, EquipStaffType type)
    {
        _uiNav.Push("UIStaff");
        _currentFloorType = floorType;
        SetStaffData(type);
        SetStaffPreview();
    }


    private void SetStaffData(EquipStaffType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        }

        _currentType = type;
        _currentTypeDataList = StaffDataManager.Instance.GetSortStaffDataList(type);
        _typeText.text = Utility.StaffTypeStringConverter(type);

        UpdateUI();
    }


    private void SetStaffPreview()
    {
        StaffData equipStaffData = UserInfo.GetEquipStaff(UserInfo.CurrentStage, _currentFloorType, _currentType);
        _uiStaffPreview.SetData(_currentFloorType, _currentType, equipStaffData != null ? equipStaffData : _currentTypeDataList.Count <= 0 ? null : _currentTypeDataList[0]);
    }


    private void ChangeStaffData(int dir)
    {
        EquipStaffType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? EquipStaffType.Length - 1 : (EquipStaffType)((int)newTypeIndex % (int)EquipStaffType.Length);
        SetStaffData(newTypeIndex);
        SetStaffPreview();
    }

    
    private void OnEquipButtonClicked(ERestaurantFloorType floorType, EquipStaffType type, StaffData data)
    {
        SoundManager.Instance.PlayEffectAudio(_equipSound);
        UserInfo.SetEquipStaff(UserInfo.CurrentStage, floorType, type, data);
        SetStaffData(_currentType);
    }

    private void OnUsingButtonClicked(ERestaurantFloorType floorType, StaffData data)
    {
        SoundManager.Instance.PlayEffectAudio(_dequipSound);
        UserInfo.SetNullEquipStaff(UserInfo.CurrentStage, floorType, data);
        SetStaffData(_currentType);
    }



    private void OnBuyButtonClicked(StaffData data)
    {
        if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, data.Id))
        {
            PopupManager.Instance.ShowTextError();
            return;
        }

        if (!UserInfo.IsScoreValid(data))
        {
            PopupManager.Instance.ShowTextLackScore();
            return;
        }

        if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
        {
            PopupManager.Instance.ShowTextLackMoney();
            return;
        }

        if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
        {
            PopupManager.Instance.ShowTextLackDia();
            return;
        }

        if (data.MoneyType == MoneyType.Gold)
            UserInfo.AddMoney(-data.BuyPrice);

        else if (data.MoneyType == MoneyType.Dia)
            UserInfo.AddDia(-data.BuyPrice);

        UserInfo.GiveStaff(UserInfo.CurrentStage, data);
        PopupManager.Instance.ShowDisplayText("새로운 직원을 채용했어요!");
    }


    private void OnUpgradeButtonClicked(StaffData data)
    {
        _uiStaffUpgrade.SetData(data);
        _uiNav.Push("UIStaffUpgrade");
    }


    private void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiStaffPreview.UpdateUI();
        int slotsIndex = (int)_currentType;
        StaffData data;
        UIRestaurantAdminStaffSlot slot;

        (ERestaurantFloorType, UIRestaurantAdminStaffSlot)[] equipSlotArray = new (ERestaurantFloorType, UIRestaurantAdminStaffSlot)[(int)ERestaurantFloorType.Length];
        int equipSlotCount = 0;

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);
            slot.transform.SetSiblingIndex(i);
            slot.EquipGroupSetActive(false);

            if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, data))
            {
                ERestaurantFloorType floorType = UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, data);
                if(floorType < ERestaurantFloorType.Length)
                {
                    equipSlotArray[equipSlotCount++] = (floorType, slot);
                }

                if (UserInfo.IsEquipStaff(UserInfo.CurrentStage, data))
                {
                    slot.EquipGroupSetActive(true);
                    EquipStaffType equipType = UserInfo.GetEquipStaffType(UserInfo.CurrentStage, data);
                    slot.SetEquipText(Utility.StaffTypeStringConverter(equipType));
                }


                switch (floorType)
                {
                    case ERestaurantFloorType.Floor1:
                        slot.SetUse(data.ThumbnailSprite, data.Name, "1층 배치중");
                        break;

                    case ERestaurantFloorType.Floor2:
                        slot.SetUse(data.ThumbnailSprite, data.Name, "2층 배치중");
                        break;

                    case ERestaurantFloorType.Floor3:
                        slot.SetUse(data.ThumbnailSprite, data.Name, "3층 배치중");
                        break;

                    case ERestaurantFloorType.Length:
                        slot.SetOperate(data.ThumbnailSprite, data.Name, "배치하기");
                        break;

                    case ERestaurantFloorType.Error:
                        slot.SetOperate(data.ThumbnailSprite, data.Name, "배치하기");
                        break;
                }
                continue;
            }

            else
            {
                if (!UserInfo.IsScoreValid(data))
                {
                    slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                    continue;
                }

                if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
                {
                    _slots[slotsIndex][i].SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                else if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
                {
                    _slots[slotsIndex][i].SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice), data.MoneyType);
                continue;
            }
        }

        //장착된 슬롯들을 순회하며 층수로 오름차순 정렬
        Array.Sort(equipSlotArray, 0, equipSlotCount, Comparer<(ERestaurantFloorType, UIRestaurantAdminStaffSlot)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));
        for (int i = 0; i < equipSlotCount; i++)
        {
            equipSlotArray[i].Item2.transform.SetSiblingIndex(i);
        }
    }



    private void OnChangeStaffEvent(ERestaurantFloorType floorType, EquipStaffType type)
    {
        if (!gameObject.activeInHierarchy)
            return;

        UpdateUI();
    }


    private void OnSlotClicked(StaffData data)
    {
        _uiStaffPreview.SetData(_currentFloorType, _currentType, data);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeStaffHandler -= OnChangeStaffEvent;
        UserInfo.OnGiveStaffHandler -= UpdateUI;
        UserInfo.OnChangeMoneyHandler -= UpdateUI;
        UserInfo.OnChangeScoreHandler -= UpdateUI;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUI;
    }
}
