using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffSkin : MonoBehaviour
{
    private const int MAX_SLOT_COUNT = 10;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private Image _skinImage;
    [SerializeField] private Button _hideButton;
    [SerializeField] private Button _equipButton;
    [SerializeField] private Image _usingButton;
    [SerializeField] private Button[] _arrowButtons;


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
        
        foreach (var button in _arrowButtons)
            button.onClick.AddListener(ArrowButtonClicked);

        Hide();

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
        if (_customerData == null)
        {
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }

        if (_currentSkinData == null)
        {
            SetViewData(customerData.Name, customerData.Description, Utility.GetStaffSkinEffectDescription(null), customerData.ThumbnailSprite);
        }
        else
        {
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
        _currentSkinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, customerData);
        SetSkinList(customerData);
        UpdateUI();
    }


    public void Hide()
    {
        gameObject.SetActive(false);
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

        StaffSkinData equipSkinData = UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, customerData);
        if (skinData == null)
        {
            _currentSkinData = null;
            _skinImage.sprite = customerData.ThumbnailSprite;
            _nameText.text = customerData.Name;
            _descriptionText.text = customerData.Description;
            _effectText.text = Utility.GetCustomerSkinEffectDescription(null);
            _equipButton.gameObject.SetActive(equipSkinData != null);
            _usingButton.gameObject.SetActive(equipSkinData == null);
            return;
        }

        _customerData = customerData;
        _currentSkinData = skinData;
        if (_currentSkinData == null)
        {
            SetViewData(customerData.Name, customerData.Description, Utility.GetStaffSkinEffectDescription(null), customerData.ThumbnailSprite);
        }
        else
        {
            SetViewData(_currentSkinData.Name, _currentSkinData.Description, Utility.GetStaffSkinEffectDescription(_currentSkinData), _currentSkinData.ThumbnailSprite);
        }

        if (equipSkinData != null && equipSkinData.Id == skinData.Id)
        {
            _equipButton.gameObject.SetActive(false);
            _usingButton.gameObject.SetActive(true);
        }
        else
        {
            _equipButton.gameObject.SetActive(true);
            _usingButton.gameObject.SetActive(false);
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
        }
        else
        {
            PopupManager.Instance.ShowDisplayText("직원을 보유하고 있지 않습니다.");
        }

    }


    private void HideAllSlots()
    {
        DebugLog.Log("Hide All Slots");
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
}
