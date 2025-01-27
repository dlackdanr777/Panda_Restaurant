using Muks.Tween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICustomerView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UICustomerBlackImage _blackImage;
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private UIImageAndText _gatecrasherFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _npcImage;
    [SerializeField] private TextMeshProUGUI _npcNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private UIImageAndText _orderFoodTitle;
    [SerializeField] private TextMeshProUGUI _effectTitle;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private Material _grayMat;

    [Space]
    [Header("OrderFood Slot Options")]
    [SerializeField] private RectTransform _orderFoodParent;
    [SerializeField] private UIImageSlot _orderFoodSlotPrefab;
    [SerializeField] private int _orderFoodSlotCount;

    private CustomerData _data;
    private List<UIImageSlot> _orderFoodSlotList = new List<UIImageSlot>();

    private Vector2 _originalSize;
    private Vector3 _originalPosition;

    public void Init()
    {
        _originalSize = _npcImage.rectTransform.sizeDelta;
        _originalPosition = _npcImage.rectTransform.anchoredPosition;

        _blackImage.Init();
        _blackImage.gameObject.SetActive(false);
        for (int i = 0; i < _orderFoodSlotCount; ++i)
        {
            UIImageSlot slot = Instantiate(_orderFoodSlotPrefab, _orderFoodParent);
            slot.gameObject.SetActive(false);
            _orderFoodSlotList.Add(slot);
        }
    }

    public void UpdateUI()
    {
        SetData(_data);
    }


    public void SetData(CustomerData data)
    {
        _blackImage.gameObject.SetActive(false);
        if (data == null)
        {
            _npcImage.gameObject.SetActive(false);
            _orderFoodTitle.gameObject.SetActive(true);
            _effectTitle.gameObject.SetActive(false);
            _normalFrameImage.gameObject.SetActive(true);
            _npcNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _effectDescription.text = string.Empty;
            _orderFoodTitle.SetText(string.Empty);
            SetScaleImage(1);
            _data = null;
            return;
        }
        _data = data;

        _npcImage.gameObject.SetActive(true);
        _npcImage.sprite = data.Sprite;

        if (data is SpecialCustomerData)
        {
            _normalFrameImage.gameObject.SetActive(false);
            _gatecrasherFrameImage.gameObject.SetActive(false);
            _specialFrameImage.gameObject.SetActive(true);
            _effectTitle.gameObject.SetActive(true);
            _orderFoodTitle.gameObject.SetActive(true);
            _orderFoodTitle.SetText("林巩 夸府");
            SetScaleImage(1);

        }
        else if(data is GatecrasherCustomerData)
        {
            _normalFrameImage.gameObject.SetActive(false);
            _gatecrasherFrameImage.gameObject.SetActive(true);
            _specialFrameImage.gameObject.SetActive(false);
            _effectTitle.gameObject.SetActive(false);
            _orderFoodTitle.gameObject.SetActive(false);
            SetScaleImage(1);
        }
        else
        {
            _normalFrameImage.gameObject.SetActive(true);
            _gatecrasherFrameImage.gameObject.SetActive(false);
            _specialFrameImage.gameObject.SetActive(false);
            _effectTitle.gameObject.SetActive(true);
            _orderFoodTitle.gameObject.SetActive(true);
            _orderFoodTitle.SetText("林巩 夸府");
            SetScaleImage(1.3f, 13);
        }


        if (!UserInfo.GetCustomerEnableState(data))
        {
            _blackImage.gameObject.SetActive(true);
            _blackImage.SetData(data);
            HideOrderFoodSlots();
            _npcNameText.text = "???";
            _descriptionText.text = "???";

            if(data is GatecrasherCustomerData)
            {
                _effectDescription.text = string.Empty;
                _gatecrasherFrameImage.SetText("???");
            }
            else
                _effectDescription.text = "???";
        }
        else
        {
            SetOrderFoodSlot(data);
            _npcNameText.text = data.Name;
            _descriptionText.text = data.Description;

            if (data is GatecrasherCustomerData)
            {
                _effectDescription.text = string.Empty;
                _gatecrasherFrameImage.SetText(Utility.GetCustomerEffectDescription(data));
            }
            else
                _effectDescription.text = Utility.GetCustomerEffectDescription(data);
        }


        _npcImage.TweenStop();
        Color npcColor = UserInfo.GetCustomerEnableState(data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        npcColor.a = 0;
        _npcImage.color = npcColor;
        _npcImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
    }


    public void ChoiceView()
    {
        if (_data == null)
            return;

        _npcImage.TweenStop();
        Color npcColor = UserInfo.GetCustomerEnableState(_data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        npcColor.a = 0;
        _npcImage.color = npcColor;
        _npcImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
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

        _npcImage.rectTransform.sizeDelta = newSize;
        _npcImage.rectTransform.anchoredPosition = newPosition;
    }


    private void HideOrderFoodSlots()
    {
        for (int i = 0, cnt = _orderFoodSlotList.Count; i < cnt; ++i)
        {
            _orderFoodSlotList[i].gameObject.SetActive(false);
        }
    }

    private void SetOrderFoodSlot(CustomerData data)
    {
        HideOrderFoodSlots();
        if (data is SpecialCustomerData || data is GatecrasherCustomerData)
            return;

        List<string> orderFoodList = new List<string>();
        orderFoodList.Add(data.RequiredDish);
        orderFoodList.AddRange(data.OrderFoods);

        FoodData foodData;
        int slotIndex = 0;
        for (int i = 0, cnt = orderFoodList.Count; i < cnt; ++i)
        {
            if (string.IsNullOrWhiteSpace(orderFoodList[i]))
                continue;

            foodData = FoodDataManager.Instance.GetFoodData(orderFoodList[i]);
            if (foodData == null)
                continue;

            _orderFoodSlotList[slotIndex].gameObject.SetActive(true);
            _orderFoodSlotList[slotIndex].SetSprite(foodData.Sprite);
            _orderFoodSlotList[slotIndex].SetMaterial(UserInfo.IsGiveRecipe(foodData) ? null : _grayMat);
            _orderFoodSlotList[slotIndex].SetColor(UserInfo.IsGiveRecipe(foodData) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive));

            slotIndex++;
        }
    }
}
