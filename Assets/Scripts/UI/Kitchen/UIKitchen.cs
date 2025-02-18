using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIKitchen : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIRestaurantAdmin _uiRestaurantAdmin;
    [SerializeField] private UIKitchenPreview _uikitchenPreview;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _typeText1;

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

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _dequipSound;

    private KitchenUtensilType _currentType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIRestaurantAdminSlot>[] _slots = new List<UIRestaurantAdminSlot>[(int)KitchenUtensilType.Length];
    List<KitchenUtensilData> _currentTypeDataList;


    public override void Init()
    {
        _leftArrowButton.AddListener(() => ChangeKitchenData(-1));
        _rightArrowButton.AddListener(() => ChangeKitchenData(1));
        _uikitchenPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        for (int i = 0, cntI = (int)KitchenUtensilType.Length; i < cntI; ++i)
        {
            List<KitchenUtensilData> typeDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList((KitchenUtensilType)i);
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

        UserInfo.OnChangeKitchenUtensilHandler += OnChangeKitchenEvent;
        UserInfo.OnGiveKitchenUtensilHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;

        SetKitchenUtensilDataData(KitchenUtensilType.Burner1);
        SetKitchenPreview();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        _uiRestaurantAdmin.ShowKitchenTab();
        _uiRestaurantAdmin.MainUISetActive(false);
        transform.SetAsLastSibling();
        SetKitchenUtensilDataData(KitchenUtensilType.Burner1);
        SetKitchenPreview();

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


    public void ShowUIKitchen(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        _uiNav.Push("UIKitchen");
        _currentFloorType = floorType;
        SetKitchenUtensilDataData(type);
        SetKitchenPreview();
    }


    private void SetKitchenUtensilDataData(KitchenUtensilType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        }

        _currentType = type;
        _currentTypeDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList(type);
        string typeStr = Utility.KitchenUtensilTypeStringConverter(type);
        _typeText1.text = typeStr;

        UpdateUI();
    }

    private void SetKitchenPreview()
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(_currentFloorType, _currentType);

        if(equipData == null)
        {
            KitchenUtensilData firstSlotData = _currentTypeDataList[0];
            _uikitchenPreview.SetData(_currentFloorType, firstSlotData);
            return;
        }

        _uikitchenPreview.SetData(_currentFloorType, equipData);
    }


    private void ChangeKitchenData(int dir)
    {
        KitchenUtensilType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? KitchenUtensilType.Length - 1 : (KitchenUtensilType)((int)newTypeIndex % (int)KitchenUtensilType.Length);
        SetKitchenUtensilDataData(newTypeIndex);
        SetKitchenPreview();
    }

    
    private void OnEquipButtonClicked(ERestaurantFloorType type, ShopData data)
    {
        if (data == null)
        {
            SoundManager.Instance.PlayEffectAudio(_dequipSound);
            UserInfo.SetNullEquipKitchenUtensil(type, _currentType);
            SetKitchenUtensilDataData(_currentType);
            return;
        }

        SoundManager.Instance.PlayEffectAudio(_equipSound);
        UserInfo.SetEquipKitchenUtensil(type, data.Id);
        SetKitchenUtensilDataData(_currentType);
    }

    private void OnBuyButtonClicked(ShopData data)
    {
        if (UserInfo.IsGiveKitchenUtensil(data.Id))
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

        UserInfo.GiveKitchenUtensil(data.Id);
        PopupManager.Instance.ShowDisplayText("새로운 주방 기구를 구매했어요!");
    }




    private void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uikitchenPreview.UpdateUI();
        int slotsIndex = (int)_currentType;
        KitchenUtensilData data;
        UIRestaurantAdminSlot slot;

        (ERestaurantFloorType, UIRestaurantAdminSlot)[] equipSlotArray = new (ERestaurantFloorType, UIRestaurantAdminSlot)[(int)ERestaurantFloorType.Length];
        int equipSlotCount = 0;

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);

            if (UserInfo.IsGiveKitchenUtensil(data))
            {
                ERestaurantFloorType floorType = UserInfo.GetEquipKitchenUtensilFloorType(data);
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

                else if (data.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(data))
                {
                    slot.SetNotEnoughDiaPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice), data.MoneyType);
                continue;
            }
        }

        //장착된 슬롯들을 순회하며 층수로 오름차순 정렬
        Array.Sort(equipSlotArray, 0, equipSlotCount, Comparer<(ERestaurantFloorType, UIRestaurantAdminSlot)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));
        for (int i = 0; i < equipSlotCount; i++)
        {
            equipSlotArray[i].Item2.transform.SetSiblingIndex(i);
        }
    }


    private void OnSlotClicked(KitchenUtensilData data)
    {
        _uikitchenPreview.SetData(_currentFloorType, data);
    }


    private void OnChangeKitchenEvent(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        UpdateUI();
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeKitchenUtensilHandler -= OnChangeKitchenEvent;
        UserInfo.OnGiveKitchenUtensilHandler -= UpdateUI;
        UserInfo.OnChangeMoneyHandler -= UpdateUI;
        UserInfo.OnChangeScoreHandler -= UpdateUI;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUI;
    }
}
