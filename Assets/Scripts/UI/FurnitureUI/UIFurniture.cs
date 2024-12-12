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
    [SerializeField] private UIRestaurantAdminSlot _slotPrefab;

    private FurnitureType _currentType;
    private List<UIRestaurantAdminSlot>[] _slots = new List<UIRestaurantAdminSlot>[(int)FurnitureType.Length];
    List<FurnitureData> _currentTypeDataList;

    public override void Init()
    {
        _leftArrowButton.AddListener(() => ChangeFurnitureData(-1));
        _rightArrowButton.AddListener(() => ChangeFurnitureData(1));
        _uiFurniturePreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        for(int i = 0, cntI = (int)FurnitureType.Length; i < cntI; ++i)
        {
            List<FurnitureData> typeDataList = FurnitureDataManager.Instance.GetFurnitureDataList((FurnitureType)i);
            _slots[i] = new List<UIRestaurantAdminSlot>();
            for (int j = 0, cntJ = typeDataList.Count; j < cntJ; ++j)
            {
                int index = j;
                UIRestaurantAdminSlot slot = Instantiate(_slotPrefab, _slotParnet);
                slot.Init(() => OnSlotClicked(typeDataList[index]));
                _slots[i].Add(slot);
                slot.gameObject.SetActive(false);
            }
        }

        UserInfo.OnChangeFurnitureHandler += (type) => UpdateUI();
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
        FurnitureData equipFurnitureData = UserInfo.GetEquipFurniture(type);
        _currentTypeDataList = FurnitureDataManager.Instance.GetFurnitureDataList(type);
        string furnitureName = Utility.FurnitureTypeStringConverter(type);
        _typeText.text = furnitureName;

        UpdateUI();
    }

    private void SetFurniturePreview()
    {
        FurnitureData equipData = UserInfo.GetEquipFurniture(_currentType);
        _uiFurniturePreview.SetData(equipData != null ? equipData : _currentTypeDataList[0]);
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

        UserInfo.GiveFurniture(data);
        PopupManager.Instance.ShowDisplayText("새로운 가구를 구매했어요!");
    }

    public void ShowUIFurniture(FurnitureType type)
    {
        _uiNav.Push("UIFurniture");
        SetFurnitureData(type);
        SetFurniturePreview();
    }

    private void UpdateUI()
    {
        if (!gameObject.activeSelf)
            return;

        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiFurniturePreview.UpdateUI();
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
                _slots[slotsIndex][i].SetUse(data.ThumbnailSprite, data.Name, "배치중");
                continue;
            }

            if (UserInfo.IsGiveFurniture(data))
            {
                _slots[slotsIndex][i].SetOperate(data.ThumbnailSprite, data.Name, "배치하기");
                continue;
            }

            else
            {
                if (!UserInfo.IsScoreValid(data))
                {
                    _slots[slotsIndex][i].SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                    continue;
                }

                if (data.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(data))
                {
                    _slots[slotsIndex][i].SetNotEnoughMoneyPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                if(data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
                {
                    _slots[slotsIndex][i].SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                _slots[slotsIndex][i].SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice), data.MoneyType);
                continue;
            }
        }
    }

    private void OnSlotClicked(FurnitureData data)
    {
        _uiFurniturePreview.SetData(data);
    }
}
