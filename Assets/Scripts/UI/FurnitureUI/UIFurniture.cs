using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class UIFurniture : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private FurnitureSystem _furnitureSystem;
    [SerializeField] private UIFurniturePreview _uiFurniturePreview;
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
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UIRestaurantAdminFoodTypeSlot _slotPrefab;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _dequipSound;

    private FurnitureType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminFoodTypeSlot>[] _slots = new List<UIRestaurantAdminFoodTypeSlot>[(int)FurnitureType.Length];
    List<FurnitureData> _currentTypeDataList;

    public override void Init()
    {
        _leftArrowButton.AddListener(() => ChangeFurnitureData(-1));
        _rightArrowButton.AddListener(() => ChangeFurnitureData(1));
        _uiFurniturePreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        for(int i = 0, cntI = (int)FurnitureType.Length; i < cntI; ++i)
        {
            List<FurnitureData> typeDataList = FurnitureDataManager.Instance.GetSortFurnitureDataList((FurnitureType)i);
            _slots[i] = new List<UIRestaurantAdminFoodTypeSlot>();
            for (int j = 0, cntJ = typeDataList.Count; j < cntJ; ++j)
            {
                int index = j;
                UIRestaurantAdminFoodTypeSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[index]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }

        UserInfo.OnChangeFurnitureHandler += (floor, type) => UpdateUI();
        UserInfo.OnGiveFurnitureHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;

        SetFurnitureData(FurnitureType.Table1);
        SetFurniturePreview();

        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        _uiRestaurantAdmin.ShowFurnitureTab();
        _uiRestaurantAdmin.MainUISetActive(false);
        transform.SetAsLastSibling();
        SetFurniturePreview();

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


    private void SetFurnitureData(FurnitureType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        } 

        _currentType = type;
        FurnitureData equipFurnitureData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _currentFloorType, type);
        _currentTypeDataList = FurnitureDataManager.Instance.GetSortFurnitureDataList(type);
        string furnitureName = Utility.FurnitureTypeStringConverter(type);
        _typeText.text = furnitureName;

        UpdateUI();
    }

    private void SetFurniturePreview()
    {
        FurnitureData equipData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _currentFloorType, _currentType);
        _uiFurniturePreview.SetData(_currentFloorType, equipData != null ? equipData : _currentTypeDataList[0]);
    }


    private void ChangeFurnitureData(int dir)
    {
        FurnitureType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? FurnitureType.Length - 1 : (FurnitureType)((int)newTypeIndex % (int)FurnitureType.Length);
        SetFurnitureData(newTypeIndex);
        SetFurniturePreview();
    }

    
    private void OnEquipButtonClicked(ERestaurantFloorType type, FurnitureData data)
    {
        if (data == null)
        {
            SoundManager.Instance.PlayEffectAudio(_dequipSound);
            UserInfo.SetNullEquipFurniture(UserInfo.CurrentStage, type, _currentType);
            SetFurnitureData(_currentType);
            return;
        }

        SoundManager.Instance.PlayEffectAudio(_equipSound);
        UserInfo.SetEquipFurniture(UserInfo.CurrentStage, type, data);
        SetFurnitureData(_currentType);
    }

    private void OnBuyButtonClicked(FurnitureData data)
    {
        if (UserInfo.IsGiveFurniture(UserInfo.CurrentStage, data.Id))
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

        if(data.MoneyType == MoneyType.Gold)
            UserInfo.AddMoney(-data.BuyPrice);

        else if(data.MoneyType == MoneyType.Dia)
            UserInfo.AddDia(-data.BuyPrice);

        UserInfo.GiveFurniture(UserInfo.CurrentStage, data);
        PopupManager.Instance.ShowDisplayText("새로운 가구를 구매했어요!");
    }

    public void ShowUIFurniture(ERestaurantFloorType floorType, FurnitureType type)
    {
        _uiNav.Push("UIFurniture");
        _currentFloorType = floorType;
        SetFurnitureData(type);
        SetFurniturePreview();
    }

    private void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiFurniturePreview.UpdateUI();
        int slotsIndex = (int)_currentType;
        FurnitureData data;
        UIRestaurantAdminFoodTypeSlot slot;

        (ERestaurantFloorType, UIRestaurantAdminFoodTypeSlot)[] equipSlotArray = new (ERestaurantFloorType, UIRestaurantAdminFoodTypeSlot)[(int)ERestaurantFloorType.Length];
        int equipSlotCount = 0;

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);
            slot.SetFoodType(data.FoodType);
            slot.transform.SetSiblingIndex(i);

            if (UserInfo.IsGiveFurniture(UserInfo.CurrentStage, data))
            {
                ERestaurantFloorType floorType = UserInfo.GetEquipFurnitureFloorType(UserInfo.CurrentStage, data);
                if (floorType < ERestaurantFloorType.Length)
                {
                    equipSlotArray[equipSlotCount++] = (floorType, slot);
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
                if(!UnlockConditionManager.GetConditionEnabled(data.UnlockData))
                {
                    slot.SetLock(data.ThumbnailSprite, data.Name);
                    continue;
                }

                if (!UserInfo.IsScoreValid(data))
                {
                    slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                    continue;
                }

                if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
                {
                    slot.SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                else if(data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
                {
                    slot.SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice), data.MoneyType);
                continue;
            }
        }

        //장착된 슬롯들을 순회하며 층수로 오름차순 정렬
        Array.Sort(equipSlotArray, 0, equipSlotCount, Comparer<(ERestaurantFloorType, UIRestaurantAdminFoodTypeSlot)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));
        for (int i = 0; i < equipSlotCount; i++)
        {
            equipSlotArray[i].Item2.transform.SetSiblingIndex(i);
        }

    }

    private void OnSlotClicked(FurnitureData data)
    {
        _uiFurniturePreview.SetData(_currentFloorType, data);
    }
}
