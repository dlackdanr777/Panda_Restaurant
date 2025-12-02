using System.Collections.Generic;
using Muks.MobileUI;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffSkin : MonoBehaviour
{
    private const int MAX_SLOT_COUNT = 10;
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _typeText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private Image _skinImage;
    [SerializeField] private Button _hideButton;
    [SerializeField] private Button _equipButton;
    [SerializeField] private Image _usingButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIImageAndText _noMoneyButton;
    [SerializeField] private Button[] _arrowButtons;
    [SerializeField] private GameObject _tokenUI;


    [Header("Slot Options")]
    [SerializeField] private RectTransform _skinSrollView;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIStaffSkinSlot _slotPrefab;
    [SerializeField] private GameObject _descriptionGroup;
    [SerializeField] private GameObject _effectGroup;


    private List<UIStaffSkinSlot> _slotList = new List<UIStaffSkinSlot>();

    private StaffData _customerData;
    private StaffSkinData _currentSkinData;



    public void Init()
    {
        _nameText.text = string.Empty;
        _descriptionText.text = string.Empty;
        _skinImage.sprite = null;

        for (int i = 0; i < MAX_SLOT_COUNT; ++i)
        {
            UIStaffSkinSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(OnSlotClicked);
            slot.gameObject.SetActive(false);
            _slotList.Add(slot);
        }
        _descriptionGroup.SetActive(true);
        _effectGroup.SetActive(false);
        _hideButton.onClick.AddListener(Hide);
        _equipButton.onClick.AddListener(OnChangeCustomerSkin);
        _buyButton.AddListener(OnBuyButtonClicked);
        foreach (var button in _arrowButtons)
            button.onClick.AddListener(ArrowButtonClicked);

        Hide();
        _tokenUI.gameObject.SetActive(false);
        UserInfo.OnChangeStaffSkinHandler += UpdateUI;
    }

    public void SetViewData(string name, string description, string effectText, Sprite skinSprite)
    {
        _nameText.text = name;
        _descriptionText.text = description;
        _effectText.text = effectText;
        _skinImage.sprite = skinSprite;

        _descriptionGroup.SetActive(true);
        _effectGroup.SetActive(false);
    }

    public void SetSkinList(StaffData customerData)
    {
        _customerData = customerData;
        StaffGroupType groupType = StaffDataManager.Instance.GetStaffGroupType(customerData);
        string typeText = Utility.StaffTypeStringConverter(groupType);
        _typeText.SetText(typeText);
        if (_customerData == null)
        {
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }

        if (_currentSkinData == null)
        {
            _skinImage.color = Utility.GetColor(ColorType.None);
            SetViewData(customerData.Name, customerData.Description, Utility.GetStaffSkinEffectDescription(null), customerData.ThumbnailSprite);
        }
        else
        {
            _skinImage.color = UserInfo.IsGiveStaffSkin(_currentSkinData.Id) ? Utility.GetColor(ColorType.None) : Utility.GetColor(ColorType.NoGive);
            SetViewData(_currentSkinData.Name, _currentSkinData.Description, Utility.GetStaffSkinEffectDescription(_currentSkinData), _currentSkinData.ThumbnailSprite);
        }

        List<StaffSkinData> skinList = SkinDataManager.Instance.GetStaffSkinDataList(customerData.Id);
        skinList.Sort((a, b) => b.Rank.CompareTo(a.Rank));
        HideAllSlots();
        _slotList[skinList.Count].gameObject.SetActive(true);
        _slotList[skinList.Count].SetData(customerData, null);
        for (int i = 0; i < skinList.Count; ++i)
        {
            int slotIndex = i;
            if (slotIndex >= MAX_SLOT_COUNT)
            {
                UIStaffSkinSlot slot = Instantiate(_slotPrefab, _slotParent);
                _slotList.Add(slot);
            }
            _slotList[slotIndex].gameObject.SetActive(true);
            _slotList[slotIndex].SetData(customerData, skinList[i]);
        }
    }


    public void Show(StaffData customerData)
    {
        gameObject.SetActive(true);
        _hideButton.gameObject.SetActive(true);
        _customerData = customerData;
        _currentSkinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, customerData);
        _tokenUI.gameObject.SetActive(true);
        UpdateUI();
    }


    public void Hide()
    {
        gameObject.SetActive(false);
        _tokenUI.gameObject.SetActive(false);
    }

    public void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        SetSkinList(_customerData);
        OnSlotClicked(_customerData, _currentSkinData);
    }


    private void OnSlotClicked(StaffData customerData, StaffSkinData skinData)
    {
        if (customerData == null)
        {
            _customerData = null;
            _currentSkinData = null;
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }

        _customerData = customerData;
        _currentSkinData = skinData;
        StaffSkinData equipSkinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, customerData);

        // 버튼 초기화
        _buyButton.gameObject.SetActive(false);
        _noMoneyButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _usingButton.gameObject.SetActive(false);

        // 기본 스킨 선택 (skinData == null)
        if (skinData == null)
        {
            UpdateUIForDefaultSkin(customerData, equipSkinData);
            return;
        }

        // 스킨 이미지 및 정보 업데이트
        UpdateSkinDisplay(skinData);

        // 직원을 보유하지 않은 경우
        if (!UserInfo.IsGiveStaff(UserInfo.CurrentStage, customerData.Id))
        {
            DebugLog.Log("직원 미보유");
            return;
        }

        // 스킨을 보유하지 않은 경우 - 구매 버튼 표시
        if (!UserInfo.IsGiveStaffSkin(skinData.Id))
        {
            ShowPurchaseButtons(skinData);
            return;
        }

        // 스킨을 보유한 경우 - 장착/사용중 버튼 표시
        ShowEquipButtons(equipSkinData, skinData);
    }

    private void UpdateUIForDefaultSkin(StaffData customerData, StaffSkinData equipSkinData)
    {
        _currentSkinData = null;
        _skinImage.sprite = customerData.ThumbnailSprite;
        _nameText.text = customerData.Name;
        _descriptionText.text = customerData.Description;
        _effectText.text = Utility.GetCustomerSkinEffectDescription(null);

        if (!UserInfo.IsGiveStaff(UserInfo.CurrentStage, customerData.Id))
        {
            DebugLog.Log("직원 미보유 - 기본 스킨");
            return;
        }

        // 장착된 스킨이 있으면 장착 버튼, 없으면 사용중 버튼 표시
        _equipButton.gameObject.SetActive(equipSkinData != null);
        _usingButton.gameObject.SetActive(equipSkinData == null);
        DebugLog.Log("기본 스킨 선택");
    }

    private void UpdateSkinDisplay(StaffSkinData skinData)
    {
        bool isSkinOwned = UserInfo.IsGiveStaffSkin(skinData.Id);
        _skinImage.color = isSkinOwned ? Utility.GetColor(ColorType.None) : Utility.GetColor(ColorType.NoGive);
        SetViewData(skinData.Name, skinData.Description, Utility.GetStaffSkinEffectDescription(skinData), skinData.ThumbnailSprite);
        DebugLog.Log($"스킨 보유 여부: {isSkinOwned}");
    }

    private void ShowPurchaseButtons(StaffSkinData skinData)
    {
        if (UserInfo.IsSkinTokenValid(skinData.BuyPrice))
        {
            DebugLog.Log("구매 가능");
            _buyButton.gameObject.SetActive(true);
            _buyButton.SetText(Utility.ConvertToMoney(skinData.BuyPrice));
        }
        else
        {
            DebugLog.Log("토큰 부족");
            _noMoneyButton.gameObject.SetActive(true);
            _noMoneyButton.SetText(Utility.ConvertToMoney(skinData.BuyPrice));
        }
    }

    private void ShowEquipButtons(StaffSkinData equipSkinData, StaffSkinData skinData)
    {
        bool isCurrentlyEquipped = equipSkinData != null && equipSkinData.Id == skinData.Id;
        
        if (isCurrentlyEquipped)
        {
            DebugLog.Log("현재 사용중인 스킨");
            _usingButton.gameObject.SetActive(true);
        }
        else
        {
            DebugLog.Log($"장착 가능한 스킨: {skinData.Id}");
            _equipButton.gameObject.SetActive(true);
        }
    }

    public void OnChangeCustomerSkin()
    {
        if (_customerData == null)
        {
            Debug.LogError("고객 데이터 또는 스킨 데이터가 없습니다.");
            return;
        }

        if (UserInfo.IsGiveStaff(UserInfo.CurrentStage, _customerData))
        {
            UserInfo.SetStaffSkin(UserInfo.CurrentStage, _customerData, _currentSkinData);
            PopupManager.Instance.ShowDisplayText("스킨이 교체되었습니다.");
        }
        else
        {
            PopupManager.Instance.ShowDisplayText("직원을 보유하고 있지 않습니다.");
        }

    }


    private void HideAllSlots()
    {
        foreach (var slot in _slotList)
        {
            slot.gameObject.SetActive(false);
        }
    }

    private void ArrowButtonClicked()
    {
        bool isActive = _descriptionGroup.gameObject.activeSelf;
        _descriptionGroup.SetActive(!isActive);
        _effectGroup.SetActive(isActive);
    }

    private void OnBuyButtonClicked()
    {
        if (_currentSkinData == null || _customerData == null)
        {
            Debug.LogError("고객 데이터 또는 스킨 데이터가 없습니다.");
            return;
        }

        if (UserInfo.IsSkinTokenValid(_currentSkinData.BuyPrice))
        {
            UserInfo.AddSkinToken(-_currentSkinData.BuyPrice);
            UserInfo.GiveStaffSkin(_currentSkinData.Id);
            UpdateUI();
            PopupManager.Instance.ShowDisplayText("스킨을 구매했습니다.");
        }
        else
        {
            PopupManager.Instance.ShowDisplayText("스킨 토큰이 부족합니다.");
        }
    }
}
