using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBookCustomerSkin : MonoBehaviour
{
    private const int MAX_SLOT_COUNT = 10;

    [SerializeField] private Image _normalSkinFrame;
    [SerializeField] private Image _rareSkinFrame;
    [SerializeField] private Image _uniqueSkinFrame;
    [SerializeField] private Image _specialSkinFrame;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _tendencyTypeText;
    [SerializeField] private Button _hideButton;
    [SerializeField] private Button _equipButton;
    [SerializeField] private Image _usingButton;

    [Header("Slot Options")]
    [SerializeField] private RectTransform _skinSrollView;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIPictorialBookCustomerSkinSlot _slotPrefab;


    private List<UIPictorialBookCustomerSkinSlot> _slotList = new List<UIPictorialBookCustomerSkinSlot>();

    private UICustomerPictorialBook _customerView;
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

        _hideButton.onClick.AddListener(OnHideButtonClicked);
        _equipButton.onClick.AddListener(OnChangeCustomerSkin);
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
        if (_currentSkinData == null)
        {
            _skinImage.color = Utility.GetColor(ColorType.None);
            SetViewData(customerData.Name, customerData.Description, Utility.GetCustomerSkinEffectDescription(null), tendencyType, customerData.Sprite);
        }
        else
        {
            _skinImage.color = UserInfo.IsGiveCustomerSkin(_currentSkinData.Id) ? Utility.GetColor(ColorType.None) : Utility.GetColor(ColorType.NoGive);
            SetViewData(_currentSkinData.Name, _currentSkinData.Description, Utility.GetCustomerSkinEffectDescription(_currentSkinData), tendencyType, _currentSkinData.Sprite);
        }

        UpdateFrame(customerData, _currentSkinData);
        List<CustomerSkinData> skinList = SkinDataManager.Instance.GetCustomerSkinDataList(customerData.Id);
        skinList.Sort((a, b) => b.Rank.CompareTo(a.Rank));
        HideAllSlots();
        _slotList[skinList.Count].gameObject.SetActive(true);
        _slotList[skinList.Count].SetData(customerData, null);
        for (int i = 0; i < skinList.Count; ++i)
        {
            int slotIndex = i;
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
        _customerData = customerData;
        _currentSkinData = UserInfo.GetEquipCustomerSkin(customerData);
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
            _skinImage.sprite = customerData.ThumbnailSprite;
            _skinImage.color = Utility.GetColor(ColorType.None);
            _nameText.text = customerData.Name;
            _descriptionText.text = customerData.Description;
            _effectText.text = Utility.GetCustomerSkinEffectDescription(null);
            _equipButton.gameObject.SetActive(equipSkinData != null);
            _usingButton.gameObject.SetActive(equipSkinData == null);
            UpdateFrame(customerData, null);
            return;
        }

        _customerData = customerData;
        _currentSkinData = skinData;
        CustomerTendencyType tendencyType = _customerData.TendencyType;
        if (_currentSkinData == null)
        {
            _skinImage.color = Utility.GetColor(ColorType.None);
            SetViewData(customerData.Name, customerData.Description, Utility.GetCustomerSkinEffectDescription(null), tendencyType, customerData.Sprite);
        }
        else
        {
            _skinImage.color = UserInfo.IsGiveCustomerSkin(_currentSkinData.Id) ? Utility.GetColor(ColorType.None) : Utility.GetColor(ColorType.NoGive);
            SetViewData(_currentSkinData.Name, _currentSkinData.Description, Utility.GetCustomerSkinEffectDescription(_currentSkinData), tendencyType, _currentSkinData.Sprite);
        }

        UpdateFrame(customerData, skinData);
        if(_currentSkinData != null && !UserInfo.IsGiveCustomerSkin(_currentSkinData.Id))
        {
            _equipButton.gameObject.SetActive(false);
            _usingButton.gameObject.SetActive(false);
            return;
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

    private void UpdateFrame(CustomerData data, SkinData skinData)
    {
        _normalSkinFrame.gameObject.SetActive(false);
        _rareSkinFrame.gameObject.SetActive(false);
        _uniqueSkinFrame.gameObject.SetActive(false);
        _specialSkinFrame.gameObject.SetActive(false);

        if (skinData == null)
        {
            _normalSkinFrame.gameObject.SetActive(true);
        }
        else
        {
            switch (skinData.Rank)
            {
                case Rank.Normal1:
                case Rank.Normal2:
                    _normalSkinFrame.gameObject.SetActive(true);
                    break;

                case Rank.Rare:
                    _rareSkinFrame.gameObject.SetActive(true);
                    break;
                case Rank.Unique:
                    _uniqueSkinFrame.gameObject.SetActive(true);
                    break;
                case Rank.Special:
                    _specialSkinFrame.gameObject.SetActive(true);
                    break;
            }
        }
        SetScaleImage(1.3f, 12);
    }
}
