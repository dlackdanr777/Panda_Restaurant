using Muks.DataBind;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private TextMeshProUGUI _typeText1;
    [SerializeField] private TextMeshProUGUI _typeText2;

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
    [SerializeField] private UISlot _slotPrefab;

    private FurnitureType _currentType;
    private List<UISlot>[] _slots = new List<UISlot>[(int)FurnitureType.Length];
    List<FurnitureData> _currentTypeDataList;

    private void OnDisable()
    {
        _uiRestaurantAdmin.MainUISetActive(true);
    }

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeFurnitureData(-1));
        _rightArrowButton.SetAction(() => ChangeFurnitureData(1));
        _uiFurniturePreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        for(int i = 0, cntI = (int)FurnitureType.Length; i < cntI; ++i)
        {
            List<FurnitureData> typeDataList = FurnitureDataManager.Instance.GetFurnitureDataList((FurnitureType)i);
            _slots[i] = new List<UISlot>();
            for (int j = 0, cntJ = typeDataList.Count; j < cntJ; ++j)
            {
                int index = j;
                UISlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[index]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }

        UserInfo.OnChangeFurnitureHandler += (type) => OnSlotUpdate(false);
        UserInfo.OnGiveFurnitureHandler += () => OnSlotUpdate(false);
        UserInfo.OnChangeMoneyHandler += () => OnSlotUpdate(false);
        UserInfo.OnChangeScoreHanlder += () => OnSlotUpdate(false);

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


    private void SetFurnitureData(FurnitureType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        } 

        _currentType = type;
        FurnitureData equipFurnitureData = UserInfo.GetEquipFurniture(type);
        _currentTypeDataList = FurnitureDataManager.Instance.GetFurnitureDataList(type);

        string furnitureName = Utility.FurnitureTypeStringConverter(type);
        _typeText1.text = furnitureName;
        _typeText2.text = furnitureName;

        OnSlotUpdate(true);
    }

    private void SetFurniturePreview()
    {
        FurnitureData equipStaffData = UserInfo.GetEquipFurniture(_currentType);
        _uiFurniturePreview.SetFurnitureData(equipStaffData);
    }


    private void ChangeFurnitureData(int dir)
    {
        FurnitureType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? FurnitureType.Length - 1 : (FurnitureType)((int)newTypeIndex % (int)FurnitureType.Length);
        SetFurnitureData(newTypeIndex);
        SetFurniturePreview();
    }

    
    private void OnEquipButtonClicked(FurnitureData data)
    {
        UserInfo.SetEquipFurniture(data);
        SetFurnitureData(_currentType);
        SetFurniturePreview();
    }

    private void OnBuyButtonClicked(FurnitureData data)
    {
        if (UserInfo.IsGiveFurniture(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        if (UserInfo.Score < data.BuyScore)
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (UserInfo.Money < data.BuyPrice)
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        UserInfo.AppendMoney(-data.BuyPrice);
        UserInfo.GiveFurniture(data);
        TimedDisplayManager.Instance.ShowText("새로운 가구를 구매했어요!");
    }

    public void ShowUIFurniture(FurnitureType type)
    {
        _uiNav.Push("UIFurniture");
        SetFurnitureData(type);
        SetFurniturePreview();
    }

    private void OnSlotUpdate(bool changeOutline)
    {
        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0 || !gameObject.activeSelf)
            return;

        FurnitureData equipFurnitureData = UserInfo.GetEquipFurniture(_currentType);

        int slotsIndex = (int)_currentType;
        FurnitureData data;
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            _slots[slotsIndex][i].gameObject.SetActive(true);
            if (equipFurnitureData != null && data.Id == equipFurnitureData.Id)
            {
                _slots[slotsIndex][i].transform.SetAsFirstSibling();
                _slots[slotsIndex][i].SetUse(data.ThumbnailSprite, data.Name, "사용중");
                if(changeOutline) _slots[slotsIndex][i].SetOutline(true);
                continue;
            }

            if(changeOutline) _slots[slotsIndex][i].SetOutline(false);
            if (UserInfo.IsGiveFurniture(data))
            {
                _slots[slotsIndex][i].SetOperate(data.ThumbnailSprite, data.Name, "사용");
                continue;
            }

            else
            {
                if (data.BuyScore <= UserInfo.Score && data.BuyPrice <= UserInfo.Money)
                {
                    _slots[slotsIndex][i].SetEnoughMoney(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyPrice));
                    continue;
                }

                _slots[slotsIndex][i].SetLowReputation(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyScore));
                continue;
            }
        }
    }

    private void OnSlotClicked(FurnitureData data)
    {
        _uiFurniturePreview.SetFurnitureData(data);

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; i++)
        {
            bool outlineEnabled = _currentTypeDataList[i] == data ? true : false;
            _slots[(int)_currentType][i].SetOutline(outlineEnabled);
        }
    }
}
