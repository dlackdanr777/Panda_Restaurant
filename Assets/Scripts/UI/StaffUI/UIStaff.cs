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
    [SerializeField] private int _createSlotValue;
    [SerializeField] private Transform _slotParnet;
    [SerializeField] private UISlot _slotPrefab;

    private StaffType _currentType;
    private List<UISlot>[] _slots = new List<UISlot>[(int)StaffType.Length];
    List<StaffData> _currentTypeDataList;

    private void OnDisable()
    {
        _uiRestaurantAdmin.MainUISetActive(true);
    }

    public override void Init()
    {
        _leftArrowButton.SetAction(() => ChangeStaffData(-1));
        _rightArrowButton.SetAction(() => ChangeStaffData(1));
        _uiStaffPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked, OnUpgradeButtonClicked);

        for (int i = 0, cntI = (int)StaffType.Length; i < cntI; ++i)
        {
            List<StaffData> typeDataList = StaffDataManager.Instance.GetStaffDataList((StaffType)i);
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

        UserInfo.OnChangeStaffHandler += () => OnSlotUpdate(false);
        UserInfo.OnUpgradeStaffHandler += () => OnSlotUpdate(false);
        UserInfo.OnGiveStaffHandler += () => OnSlotUpdate(false);
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


    private void SetStaffData(StaffType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        }

        _currentType = type;
        StaffData equipStaffData = UserInfo.GetEquipStaff(type);
        _currentTypeDataList = StaffDataManager.Instance.GetStaffDataList(type);

        string staffName = Utility.StaffTypeStringConverter(type);
        _typeText1.text = staffName;
        _typeText2.text = staffName;

        OnSlotUpdate(true);
    }

    private void SetStaffPreview()
    {
        StaffData equipStaffData = UserInfo.GetEquipStaff(_currentType);
        _uiStaffPreview.SetStaffData(equipStaffData);
    }


    private void ChangeStaffData(int dir)
    {
        StaffType newTypeIndex = _currentType + dir;
        newTypeIndex = newTypeIndex < 0 ? StaffType.Length - 1 : (StaffType)((int)newTypeIndex % (int)StaffType.Length);
        SetStaffData(newTypeIndex);
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


    private void OnSlotUpdate(bool changeOutline)
    {
        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0 || !gameObject.activeSelf)
            return;

        StaffData equipStaffData = UserInfo.GetEquipStaff(_currentType);

        int slotsIndex = (int)_currentType;
        StaffData data;
        UISlot slot;
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);
            if (equipStaffData != null && data.Id == equipStaffData.Id)
            {
                slot.transform.SetAsFirstSibling();
                slot.SetUse(data.ThumbnailSprite, data.Name, "사용중");
                if (changeOutline) _slots[slotsIndex][i].SetOutline(true);
                continue;
            }

            if (changeOutline) _slots[slotsIndex][i].SetOutline(false);
            if (UserInfo.IsGiveStaff(data))
            {
                slot.SetOperate(data.ThumbnailSprite, data.Name, "사용");
                continue;
            }

            else
            {
                if (data.BuyScore <= UserInfo.Score && data.BuyPrice <= UserInfo.Money)
                {
                    slot.SetEnoughMoney(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyPrice));
                    continue;
                }

                slot.SetLowReputation(data.ThumbnailSprite, data.Name, Utility.ConvertToNumber(data.BuyScore));
                continue;
            }
        }
    }


    public void ShowUIStaff(StaffType type)
    {
        _uiNav.Push("UIStaff");
        SetStaffData(type);
        SetStaffPreview();
    }


    private void OnSlotClicked(StaffData data)
    {
        _uiStaffPreview.SetStaffData(data);
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; i++)
        {
            bool outlineEnabled = _currentTypeDataList[i] == data ? true : false;
            _slots[(int)_currentType][i].SetOutline(outlineEnabled);
        }
    }
}
