using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBookCustomerSkin : MonoBehaviour
{
    private const int MAX_SLOT_COUNT = 10;

    [SerializeField] private UICustomerPictorialBook _customerView;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _tendencyTypeText;
    [SerializeField] private ButtonPressEffect _hideButton;
    [SerializeField] private ButtonPressEffect _equipButton;
    [SerializeField] private Image _usingButton;

    [Header("Slot Options")]
    [SerializeField] private RectTransform _skinSrollView;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIPictorialBookCustomerSkinSlot _slotPrefab;


    private List<UIPictorialBookCustomerSkinSlot> _slotList = new List<UIPictorialBookCustomerSkinSlot>();

    private NormalCustomerData _customerData;
    private CustomerSkinData _currentSkinData;

    private Vector2 _originalSize;
    private Vector3 _originalPosition;


    public void Init(UICustomerPictorialBook customerView)
    {
        _originalSize = _skinImage.rectTransform.sizeDelta;
        _originalPosition = _skinImage.rectTransform.anchoredPosition;

        _customerView = customerView;
        _nameText.text = string.Empty;
        _descriptionText.text = string.Empty;
        _skinImage.sprite = null;

        for (int i = 0; i < MAX_SLOT_COUNT; ++i)
        {
            UIPictorialBookCustomerSkinSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(OnSlotClicked);
            slot.gameObject.SetActive(false);
            _slotList.Add(slot);
        }

        _hideButton.AddListener(OnHideButtonClicked);
        _equipButton.AddListener(OnChangeCustomerSkin);
        Hide();

        UserInfo.OnChangeCustomerSkinHandler += UpdateUI;
    }

    public void SetViewData(string name, string description, string effectText, CustomerTendencyType tendencyType, Sprite skinSprite)
    {
        _nameText.text = name;
        _descriptionText.text = description;
        _effectText.text = effectText;
        _skinImage.sprite = skinSprite;
        _tendencyTypeText.text = Utility.GetTendencyTypeToStr(tendencyType);
        SetScaleImage(1.3f, 13);
    }

    public void SetSkinList(NormalCustomerData customerData)
    {
        _customerData = customerData;
        if (_customerData == null)
        {
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }
        CustomerTendencyType tendencyType = _customerData.TendencyType;
        SetViewData(customerData.Name, customerData.Description, Utility.GetCustomerSkinEffectDescription(null), tendencyType, customerData.Sprite);

        List<CustomerSkinData> skinList = SkinDataManager.Instance.GetCustomerSkinDataList(customerData.Id);

        HideAllSlots();
        _slotList[0].gameObject.SetActive(true);
        _slotList[0].SetData(customerData, null);
        for (int i = 0; i < skinList.Count; ++i)
        {
            int slotIndex = i + 1;
            if (slotIndex >= MAX_SLOT_COUNT)
            {
                UIPictorialBookCustomerSkinSlot slot = Instantiate(_slotPrefab, _slotParent);
                _slotList.Add(slot);
            }
            _slotList[slotIndex].gameObject.SetActive(true);
            _slotList[slotIndex].SetData(customerData, skinList[i]);
        }
    }


    public void Show(NormalCustomerData customerData)
    {
        _skinSrollView.gameObject.SetActive(true);
        gameObject.SetActive(true);
        _currentSkinData = UserInfo.GetEquipCustomerSkin(customerData);
        SetSkinList(customerData);
        UpdateUI();
    }


    public void Hide()
    {
        _skinSrollView.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public void UpdateUI()
    {
        if (!gameObject.activeInHierarchy)
            return;

        SetSkinList(_customerData);
        OnSlotClicked(_customerData, _currentSkinData);
    }


    private void OnSlotClicked(NormalCustomerData customerData, CustomerSkinData skinData)
    {
        if (customerData == null)
        {
            _customerData = null;
            _currentSkinData = null;
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }

        CustomerSkinData equipSkinData = UserInfo.GetEquipCustomerSkin(customerData);
        if (skinData == null)
        {
            _currentSkinData = null;
            _skinImage.sprite = customerData.Sprite;
            _nameText.text = customerData.Name;
            _descriptionText.text = customerData.Description;
            _effectText.text = Utility.GetCustomerSkinEffectDescription(null);
            _equipButton.gameObject.SetActive(equipSkinData != null);
            _usingButton.gameObject.SetActive(equipSkinData == null);
            return;
        }

        _customerData = customerData;
        _currentSkinData = skinData;
        _skinImage.sprite = skinData.Sprite;
        _nameText.text = skinData.Name;
        _descriptionText.text = skinData.Description;
        _effectText.text = Utility.GetCustomerSkinEffectDescription(skinData);

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


    private void OnHideButtonClicked()
    {
        _customerView.HideSkinView();
    }

    public void OnChangeCustomerSkin()
    {
        if (_customerData == null)
        {
            Debug.LogError("고객 데이터 또는 스킨 데이터가 없습니다.");
            return;
        }

        UserInfo.SetCustomerSkin(_customerData, _currentSkinData);
    }


    private void HideAllSlots()
    {
        foreach (var slot in _slotList)
        {
            slot.gameObject.SetActive(false);
        }
    }
    
    public void SetScaleImage(float scale, float offset = 0)
    {
        Vector2 newSize = _originalSize * scale;
        float heightDifference = (newSize.y - _originalSize.y) / 2;

        Vector3 newPosition = new Vector3(
            _originalPosition.x,
            _originalPosition.y + heightDifference - offset,
            _originalPosition.z
        );

        _skinImage.rectTransform.sizeDelta = newSize;
        _skinImage.rectTransform.anchoredPosition = newPosition;
    }
}
