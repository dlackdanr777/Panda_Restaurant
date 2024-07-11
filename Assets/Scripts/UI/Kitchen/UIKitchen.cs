using Muks.MobileUI;
using Muks.Tween;
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
    [SerializeField] private TextMeshProUGUI _typeText2;

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
    [SerializeField] private UIKitchenSlot _slotPrefab;

    private KitchenUtensilType _currentType;
    private UIKitchenSlot[] _slots;
    List<KitchenUtensilData> _currentTypeDataList;

    private void OnDisable()
    {
        _uiRestaurantAdmin.MainUISetActive(true);
    }

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeKitchenData(-1));
        _rightArrowButton.SetAction(() => ChangeKitchenData(1));
        _uikitchenPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        _slots = new UIKitchenSlot[_createSlotValue];
        for(int i = 0; i < _createSlotValue; ++i)
        {
            UIKitchenSlot slot = Instantiate(_slotPrefab, _slotParnet);
            _slots[i] = slot;
            slot.Init(OnSlotClicked);
        }

        UserInfo.OnChangeFurnitureHandler += (type) => OnSlotUpdate();
        UserInfo.OnGiveFurnitureHandler += OnSlotUpdate;
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


    private void SetKitchenUtensilDataData(KitchenUtensilType type)
    {
        _currentType = type;

        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(type);
        _currentTypeDataList = KitchenUtensilDataManager.Instance.GetKitchenUtensilDataList(type);

        string typeStr = Utility.KitchenUtensilTypeStringConverter(type);
        _typeText1.text = typeStr;
        _typeText2.text = typeStr;

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            if (equipData != null && _currentTypeDataList[i].Id == equipData.Id)
            {
                _slots[i].transform.SetAsFirstSibling();
                _slots[i].SetUse(_currentTypeDataList[i]);
                _slots[i].SetOutline(true);
                continue;
            }

            _slots[i].SetOutline(false);
            if (UserInfo.IsGiveKitchenUtensil(_currentTypeDataList[i]))
            {
                _slots[i].SetOperate(_currentTypeDataList[i]);
                continue;
            }

            else
            {
                if (_currentTypeDataList[i].BuyMinScore <= UserInfo.Score && _currentTypeDataList[i].BuyMinPrice <= UserInfo.Money)
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

    private void SetKitchenPreview()
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(_currentType);
        _uikitchenPreview.SetData(equipData);
    }


    private void ChangeKitchenData(int dir)
    {
        KitchenUtensilType newTypeIndex = _currentType + dir;
        _currentType = newTypeIndex < 0 ? KitchenUtensilType.Length - 1 : (KitchenUtensilType)((int)newTypeIndex % (int)KitchenUtensilType.Length);
        SetKitchenUtensilDataData(_currentType);
        SetKitchenPreview();
    }

    
    private void OnEquipButtonClicked(KitchenUtensilData data)
    {
        UserInfo.SetEquipKitchenUtensil(data);
        SetKitchenUtensilDataData(_currentType);
        SetKitchenPreview();
    }

    private void OnBuyButtonClicked(KitchenUtensilData data)
    {
        if (UserInfo.IsGiveKitchenUtensil(data.Id))
        {
            TimedDisplayManager.Instance.ShowTextError();
            return;
        }

        if (UserInfo.Score < data.BuyMinScore)
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (UserInfo.Money < data.BuyMinPrice)
        {
            TimedDisplayManager.Instance.ShowTextLackMoney();
            return;
        }

        UserInfo.AppendMoney(-data.BuyMinPrice);
        UserInfo.GiveKitchenUtensil(data);
        TimedDisplayManager.Instance.ShowText("새로운 주방 용품을 구매했어요!");
    }

    public void ShowUIKitchen(KitchenUtensilType type)
    {
        _uiNav.Push("UIFurniture");
        SetKitchenUtensilDataData(type);
        SetKitchenPreview();
    }

    private void OnSlotUpdate()
    {
        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0 || !gameObject.activeSelf)
            return;

        KitchenUtensilData equipFurnitureData = UserInfo.GetEquipKitchenUtensil(_currentType);

        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            if (equipFurnitureData != null && _currentTypeDataList[i].Id == equipFurnitureData.Id)
            {
                _slots[i].transform.SetAsFirstSibling();
                _slots[i].SetUse(_currentTypeDataList[i]);
                continue;
            }

            if (UserInfo.IsGiveKitchenUtensil(_currentTypeDataList[i]))
            {
                _slots[i].SetOperate(_currentTypeDataList[i]);
                continue;
            }

            else
            {
                if (_currentTypeDataList[i].BuyMinScore <= UserInfo.Score && _currentTypeDataList[i].BuyMinPrice <= UserInfo.Money)
                {
                    _slots[i].SetEnoughMoney(_currentTypeDataList[i]);
                    continue;
                }

                _slots[i].SetLowReputation(_currentTypeDataList[i]);
                continue;
            }
        }
    }

    private void OnSlotClicked(KitchenUtensilData data)
    {
        _uikitchenPreview.SetData(data);
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; i++)
        {
            bool outlineEnabled = _currentTypeDataList[i] == data ? true : false;
            _slots[i].SetOutline(outlineEnabled);
        }
    }
}
