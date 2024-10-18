using Muks.MobileUI;
using Muks.Tween;
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
    [SerializeField] private UIRestaurantAdminSlot _slotPrefab;

    private StaffType _currentType;
    private List<UIRestaurantAdminSlot>[] _slots = new List<UIRestaurantAdminSlot>[(int)StaffType.Length];
    List<StaffData> _currentTypeDataList;


    public override void Init()
    {
        _leftArrowButton.AddListener(() => ChangeStaffData(-1));
        _rightArrowButton.AddListener(() => ChangeStaffData(1));
        _uiStaffPreview.Init(OnEquipButtonClicked, OnBuyButtonClicked, OnUpgradeButtonClicked);

        for (int i = 0, cntI = (int)StaffType.Length; i < cntI; ++i)
        {
            List<StaffData> typeDataList = StaffDataManager.Instance.GetStaffDataList((StaffType)i);
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

        UserInfo.OnChangeStaffHandler += UpdateUI;
        UserInfo.OnUpgradeStaffHandler += UpdateUI;
        UserInfo.OnGiveStaffHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;

        SetStaffData(StaffType.Manager);
        SetStaffPreview();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        SetStaffData(StaffType.Manager);
        SetStaffPreview();
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


    private void SetStaffData(StaffType type)
    {
        for (int i = 0, cnt = _slots[(int)_currentType].Count; i < cnt; ++i)
        {
            _slots[(int)_currentType][i].gameObject.SetActive(false);
        }

        _currentType = type;
        _currentTypeDataList = StaffDataManager.Instance.GetStaffDataList(type);
        _typeText.text = Utility.StaffTypeStringConverter(type);

        UpdateUI();
    }


    private void SetStaffPreview()
    {
        StaffData equipStaffData = UserInfo.GetEquipStaff(_currentType);
        _uiStaffPreview.SetData(equipStaffData);
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

        if (!UserInfo.IsScoreValid(data))
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }

        if (!UserInfo.IsMoneyValid(data))
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
        _uiStaffUpgrade.SetData(data);
        _uiNav.Push("UIStaffUpgrade");
    }


    private void UpdateUI()
    {
        if (!gameObject.activeSelf)
            return;

        if (_currentTypeDataList == null || _currentTypeDataList.Count == 0)
            return;

        _uiStaffPreview.UpdateUI();

        StaffData equipStaffData = UserInfo.GetEquipStaff(_currentType);

        int slotsIndex = (int)_currentType;
        StaffData data;
        UIRestaurantAdminSlot slot;
        for (int i = 0, cnt = _currentTypeDataList.Count; i < cnt; ++i)
        {
            data = _currentTypeDataList[i];
            slot = _slots[slotsIndex][i];
            slot.gameObject.SetActive(true);
            if (equipStaffData != null && data.Id == equipStaffData.Id)
            {
                slot.transform.SetAsFirstSibling();
                slot.SetUse(data.ThumbnailSprite, data.Name, "배치중");
                continue;
            }

            if (UserInfo.IsGiveStaff(data))
            {
                slot.SetOperate(data.ThumbnailSprite, data.Name, "배치하기");
                continue;
            }

            else
            {
                if (!UserInfo.IsScoreValid(data))
                {
                    slot.SetLowReputation(data.ThumbnailSprite, data.Name, data.BuyScore.ToString());
                    continue;
                }

                if (!UserInfo.IsMoneyValid(data))
                {
                    slot.SetNotEnoughPrice(data.ThumbnailSprite, data.Name, Utility.ConvertToMoney(data.BuyPrice));
                    continue;
                }

                slot.SetEnoughPrice(data.ThumbnailSprite, data.Name, Utility.ConvertToMoney(data.BuyPrice));
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
        _uiStaffPreview.SetData(data);
    }
}
