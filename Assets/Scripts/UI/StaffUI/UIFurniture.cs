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
    [SerializeField] private TextMeshProUGUI _furnitureTypeText1;
    [SerializeField] private TextMeshProUGUI _furnitureTypeText2;

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
    [SerializeField] private UIFurnitureSlot _slotPrefab;

    private FurnitureType _currentType;
    private UIFurnitureSlot[] _slots;

    private void OnDisable()
    {
        _uiRestaurantAdmin.MainUISetActive(true);
    }

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeFurnitureData(-1));
        _rightArrowButton.SetAction(() => ChangeFurnitureData(1));
        _uiFurniturePreview.Init(OnEquipButtonClicked, OnBuyButtonClicked);

        _slots = new UIFurnitureSlot[_createSlotValue];
        for(int i = 0; i < _createSlotValue; ++i)
        {
            UIFurnitureSlot slot = Instantiate(_slotPrefab, _slotParnet);
            _slots[i] = slot;
            slot.Init((FurnitureData data) => _uiFurniturePreview.SetFurnitureData(data));
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


    private void SetFurnitureData(FurnitureType type)
    {
        _currentType = type;

        FurnitureData equipFurnitureData = UserInfo.GetEquipFurniture(type);

        List<FurnitureData> list = FurnitureDataManager.Instance.GetFoodDataList(type);

        switch (type)
        {
            case FurnitureType.Table1:
                _furnitureTypeText1.text = "테이블1";
                _furnitureTypeText2.text = "테이블2";
                break;

            case FurnitureType.Table2:
                _furnitureTypeText1.text = "테이블2";
                _furnitureTypeText2.text = "테이블2";
                break;

            case FurnitureType.Table3:
                _furnitureTypeText1.text = "테이블3";
                _furnitureTypeText2.text = "테이블3";
                break;

            case FurnitureType.Table4:
                _furnitureTypeText1.text = "테이블4";
                _furnitureTypeText2.text = "테이블4";
                break;

            case FurnitureType.Table5:
                _furnitureTypeText1.text = "테이블5";
                _furnitureTypeText2.text = "테이블5";
                break;

            case FurnitureType.Counter:
                _furnitureTypeText1.text = "카운터";
                _furnitureTypeText2.text = "카운터";
                break;

            case FurnitureType.Rack:
                _furnitureTypeText1.text = "선반";
                _furnitureTypeText2.text = "선반";
                break;

            case FurnitureType.Frame:
                _furnitureTypeText1.text = "액자";
                _furnitureTypeText2.text = "액자";
                break;

            case FurnitureType.Flower:
                _furnitureTypeText1.text = "화분";
                _furnitureTypeText2.text = "화분";
                break;

            case FurnitureType.Acc:
                _furnitureTypeText1.text = "조명";
                _furnitureTypeText2.text = "조명";
                break;

            case FurnitureType.Wallpaper:
                _furnitureTypeText1.text = "벽지";
                _furnitureTypeText2.text = "벽지";
                break;

            default:
                _furnitureTypeText1.text = string.Empty;
                _furnitureTypeText2.text = string.Empty;
                break;
        }

        for (int i = 0, cnt = list.Count; i < cnt; ++i)
        {
            _slots[i].gameObject.SetActive(true);
            if (equipFurnitureData != null && list[i].Id == equipFurnitureData.Id)
            {
                _slots[i].transform.SetAsFirstSibling();
                _slots[i].SetUse(list[i]);
                continue;
            }

            if (UserInfo.IsGiveFurniture(list[i]))
            {
                _slots[i].SetOperate(list[i]);
                continue;
            }

            else
            {
                if (list[i].BuyMinScore <= UserInfo.Score && list[i].BuyMinPrice <= UserInfo.Money)
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

    private void SetFurniturePreview()
    {
        FurnitureData equipStaffData = UserInfo.GetEquipFurniture(_currentType);
        _uiFurniturePreview.SetFurnitureData(equipStaffData);
    }


    private void ChangeFurnitureData(int dir)
    {
        FurnitureType newTypeIndex = _currentType + dir;
        _currentType = newTypeIndex < 0 ? FurnitureType.Length - 1 : (FurnitureType)((int)newTypeIndex % (int)FurnitureType.Length);
        SetFurnitureData(_currentType);
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
        UserInfo.GiveFurniture(data);
        TimedDisplayManager.Instance.ShowText("새로운 가구를 구매했어요!");
    }

    public void ShowUIFurniture(FurnitureType type)
    {
        _uiNav.Push("UIFurniture");
        SetFurnitureData(type);
        SetFurniturePreview();
    }

    private void OnSlotUpdate()
    {
        SetFurnitureData(_currentType);
    }
}
