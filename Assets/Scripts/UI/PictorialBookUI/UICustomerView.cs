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
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _npcImage;
    [SerializeField] private TextMeshProUGUI _npcNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _orderFoodTitle;
    [SerializeField] private TextMeshProUGUI _effectTitle;
    [SerializeField] private TextMeshProUGUI _effectDescription;

    [Space]
    [Header("OrderFood Slot Options")]
    [SerializeField] private RectTransform _orderFoodParent;
    [SerializeField] private UIImageSlot _orderFoodSlotPrefab;
    [SerializeField] private int _orderFoodSlotCount;

    private CustomerData _data;
    private List<UIImageSlot> _orderFoodSlotList = new List<UIImageSlot>();

    public void Init()
    {
        _blackImage.Init();
        _blackImage.gameObject.SetActive(false);
        for (int i = 0; i < _orderFoodSlotCount; ++i)
        {
            UIImageSlot slot = Instantiate(_orderFoodSlotPrefab, _orderFoodParent);
            slot.gameObject.SetActive(false);
            _orderFoodSlotList.Add(slot);
        }
    }


    public void SetData(CustomerData data)
    {
        if (data == _data)
            return;

        _normalFrameImage.gameObject.SetActive(true);
        _rareFrameImage.gameObject.SetActive(false);
        _uniqueFrameImage.gameObject.SetActive(false);
        _specialFrameImage.gameObject.SetActive(false);
        _blackImage.gameObject.SetActive(false);

        if (data == null)
        {
            _npcImage.gameObject.SetActive(false);
            _orderFoodTitle.gameObject.SetActive(false);
            _effectTitle.gameObject.SetActive(false);
            _npcNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _effectDescription.text = string.Empty;
            _data = null;
            return;
        }

        _data = data;
        _npcImage.gameObject.SetActive(true);
        _effectTitle.gameObject.SetActive(true);
        _orderFoodTitle.gameObject.SetActive(true);
        _npcImage.sprite = data.Sprite;

        if (!UserInfo.IsCustomerVisitEnabled(data))
        {
            _blackImage.gameObject.SetActive(true);
            _blackImage.SetData(data);
            HideOrderFoodSlots();
            _npcNameText.text = "???";
            _descriptionText.text = "???";
            _effectDescription.text = "???";
        }
        else
        {
            SetOrderFoodSlot(data);
            _npcNameText.text = data.Name;
            _descriptionText.text = data.Description;
            _effectDescription.text = data.Skill == null ? "¾øÀ½" : data.Skill.Description;
        }

        _npcImage.TweenStop();
        Color npcColor = UserInfo.IsCustomerVisitEnabled(data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
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
        Color npcColor = UserInfo.IsCustomerVisitEnabled(_data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        npcColor.a = 0;
        _npcImage.color = npcColor;
        _npcImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
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
            _orderFoodSlotList[slotIndex].SetColor(UserInfo.IsGiveRecipe(foodData) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive));

            slotIndex++;
        }
    }
}
