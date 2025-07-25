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
    [SerializeField] private Image _skinImage;
    [SerializeField] private Button _hideButton;

    [Header("Slot Options")]
    [SerializeField] private RectTransform _skinSrollView;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIPictorialBookCustomerSkinSlot _slotPrefab;


    private List<UIPictorialBookCustomerSkinSlot> _slotList = new List<UIPictorialBookCustomerSkinSlot>();

    private NormalCustomerData _customerData;


    public void Init(UICustomerPictorialBook customerView)
    {
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
        Hide();
    }

    public void SetViewData(string name, string description, Sprite skinSprite)
    {
        _nameText.text = name;
        _descriptionText.text = description;
        _skinImage.sprite = skinSprite;
    }

    public void SetSkinList(NormalCustomerData customerData)
    {
        _customerData = customerData;
        if (_customerData == null)
        {
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }
        SetViewData(customerData.Name, customerData.Description, customerData.Sprite);

        List<CustomerSkinData> skinList = SkinDataManager.Instance.GetCustomerSkinDataList(customerData.Id);

        HideAllSlots();
        _slotList[0].gameObject.SetActive(true);
        _slotList[0].SetData(customerData, null);
        for (int i = 0; i < skinList.Count ; ++i)
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
        SetSkinList(customerData);
    }


    public void Hide()
    {
        _skinSrollView.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }


    private void OnSlotClicked(NormalCustomerData customerData, CustomerSkinData skinData)
    {
        if(customerData == null)
        {
            Debug.LogError("고객 데이터가 없습니다.");
            return;
        }

        if (skinData == null)
        {
            _skinImage.sprite = customerData.Sprite;
            _nameText.text = customerData.Name;
            _descriptionText.text = customerData.Description;
            return;
        }

        _skinImage.sprite = skinData.Sprite;
        _nameText.text = skinData.Name;
        _descriptionText.text = skinData.Description;
    }


    private void OnHideButtonClicked()
    {
        _customerView.HideSkinView();
    }


    private void HideAllSlots()
    {
        foreach (var slot in _slotList)
        {
            slot.gameObject.SetActive(false);
        }
    }
}
