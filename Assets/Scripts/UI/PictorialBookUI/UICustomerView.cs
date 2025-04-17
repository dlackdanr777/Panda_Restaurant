using JetBrains.Annotations;
using Muks.Tween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICustomerView : MonoBehaviour
{
    private enum CustomerEffectType
    {
        OrderFood1,
        OrderFood2,
        Effect,
        Length,
    }

    [Header("Components")]
    [SerializeField] private UICustomerBlackImage _blackImage;
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private UIImageAndText _gatecrasherFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _npcImage;
    [SerializeField] private UITextAndText _visitCountGroup;
    [SerializeField] private TextMeshProUGUI _npcNameText;
    [SerializeField] private TextMeshProUGUI _tendencyTypeText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectTitle;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private ButtonPressEffect _effectLeftArrowButton;
    [SerializeField] private ButtonPressEffect _effectRightArrowButton;
    [SerializeField] private Material _grayMat;

    [Space]
    [Header("OrderFood Slot Options")]
    [SerializeField] private RectTransform _orderFoodParent1;
    [SerializeField] private RectTransform _orderFoodParent2;
    [SerializeField] private UIOrderFoodSlot _orderFoodSlotPrefab;

    private CustomerData _data;
    private CustomerEffectType _effectType;
    private List<UIOrderFoodSlot> _orderFoodSlotList = new List<UIOrderFoodSlot>();

    private Vector2 _originalSize;
    private Vector3 _originalPosition;

    public void Init()
    {
        _originalSize = _npcImage.rectTransform.sizeDelta;
        _originalPosition = _npcImage.rectTransform.anchoredPosition;

        _blackImage.Init();
        _blackImage.gameObject.SetActive(false);
        CreateFoodSlot(3, _orderFoodParent1);
        CreateFoodSlot(3, _orderFoodParent2);


        _effectLeftArrowButton.AddListener(() => OnArrowButtonClicked(-1));
        _effectRightArrowButton.AddListener(() => OnArrowButtonClicked(1));
        void CreateFoodSlot(int count, Transform parent)
        {
            for (int i = 0; i < count; ++i)
            {
                UIOrderFoodSlot slot = Instantiate(_orderFoodSlotPrefab, parent);
                slot.gameObject.SetActive(false);
                _orderFoodSlotList.Add(slot);
            }
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
            _effectTitle.gameObject.SetActive(false);
            _visitCountGroup.gameObject.SetActive(false);
            _tendencyTypeText.gameObject.SetActive(false);
            _normalFrameImage.gameObject.SetActive(true);
            _effectLeftArrowButton.gameObject.SetActive(false);
            _effectRightArrowButton.gameObject.SetActive(false);
            _npcNameText.text = string.Empty;
            _tendencyTypeText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _effectDescription.text = string.Empty;
            SetScaleImage(1);
            _data = null;
            return;
        }
        _data = data;

        _npcImage.gameObject.SetActive(true);
        _tendencyTypeText.gameObject.SetActive(false);
        _npcImage.sprite = data.Sprite;

        if (data is SpecialCustomerData)
        {
            _normalFrameImage.gameObject.SetActive(false);
            _gatecrasherFrameImage.gameObject.SetActive(false);
            _specialFrameImage.gameObject.SetActive(true);
            _effectTitle.gameObject.SetActive(true);
            SetScaleImage(1);

        }
        else if(data is GatecrasherCustomerData)
        {
            _normalFrameImage.gameObject.SetActive(false);
            _gatecrasherFrameImage.gameObject.SetActive(true);
            _specialFrameImage.gameObject.SetActive(false);
            _effectTitle.gameObject.SetActive(false);
            SetScaleImage(1);
        }
        else
        {
            _normalFrameImage.gameObject.SetActive(true);
            _gatecrasherFrameImage.gameObject.SetActive(false);
            _specialFrameImage.gameObject.SetActive(false);
            _effectTitle.gameObject.SetActive(true);
            _tendencyTypeText.gameObject.SetActive(true);
            SetScaleImage(1.3f, 13);
        }

        Color imageColor = Color.white;
        if (!UserInfo.GetCustomerEnableState(data))
        {
            _visitCountGroup.gameObject.SetActive(false);
            _blackImage.gameObject.SetActive(true);
            _blackImage.SetData(data);
            HideOrderFoodSlots();
            _npcNameText.text = "???";
            _tendencyTypeText.text = "???";
            _descriptionText.text = "???";

            imageColor = Utility.GetColor(ColorType.NoGive);

            if (data is GatecrasherCustomerData)
            {
                _effectDescription.text = string.Empty;
                _gatecrasherFrameImage.SetText("???");
            }
            else
                _effectDescription.text = "???";
        }
        else
        {
            int visitCount = UserInfo.GetVisitedCustomerCount(data);
            
            if (visitCount <= 0)
            {
                _visitCountGroup.gameObject.SetActive(true);
                _visitCountGroup.SetText1(visitCount.ToString());

                imageColor = Utility.GetColor(ColorType.NoGive);
                SetOrderFood(data);
                _npcNameText.text = "???";
                _tendencyTypeText.text = "???";
                _descriptionText.text = data.Description;
            }

            else
            {
                _visitCountGroup.gameObject.SetActive(true);
                _visitCountGroup.SetText1(visitCount.ToString());
                SetOrderFood(data);

                imageColor = Utility.GetColor(ColorType.Give);
                _npcNameText.text = data.Name;
                _descriptionText.text = data.Description;

                if (data is NormalCustomerData)
                {
                    NormalCustomerData normalData = (NormalCustomerData)data;
                    _tendencyTypeText.text = Utility.GetTendencyTypeToStr(normalData.TendencyType);
                }
                else
                {
                    _tendencyTypeText.text = string.Empty;
                }


                if (data is GatecrasherCustomerData)
                {
                    _effectDescription.text = string.Empty;
                    _gatecrasherFrameImage.SetText(Utility.GetCustomerEffectDescription(data));
                }
                else
                    _effectDescription.text = Utility.GetCustomerEffectDescription(data);
            }

           
        }


        _npcImage.TweenStop();
        imageColor.a = 0;
        _npcImage.color = imageColor;
        _npcImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);

        SetEffectGroup(data);
    }


    public void ChoiceView()
    {
        // if (_data == null)
        //     return;

        // _npcImage.TweenStop();
        // Color npcColor = UserInfo.GetCustomerEnableState(_data) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        // npcColor.a = 0;
        // _npcImage.color = npcColor;
        // _npcImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        // _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        // _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
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


    private void SetOrderFood(CustomerData data)
    {
        HideOrderFoodSlots();
        if (!(data is NormalCustomerData))
            return;

        NormalCustomerData normalData = (NormalCustomerData)data;
        if(!UserInfo.GetCustomerEnableState(data))
        {
            DebugLog.Log(data.Id);
            return;
        }

        int visitCount = UserInfo.GetVisitedCustomerCount(data);

        SetOrderFoodSlot(normalData.RequiredDish, true, string.Empty);
        SetOrderFoodSlot(normalData.VisitCount100Food, 100 <= visitCount, "방문 " + Utility.SetStringColor(100.ToString(), ColorType.Negative) + "회");
        SetOrderFoodSlot(normalData.VisitCount200Food, 200 <= visitCount, "방문 " + Utility.SetStringColor(200.ToString(), ColorType.Negative) + "회");
        SetOrderFoodSlot(normalData.VisitCount300Food, 300 <= visitCount, "방문 " + Utility.SetStringColor(300.ToString(), ColorType.Negative) + "회");
        SetOrderFoodSlot(normalData.VisitCount400Food, 400 <= visitCount, "방문 " + Utility.SetStringColor(400.ToString(), ColorType.Negative) + "회");
        SetOrderFoodSlot(normalData.VisitCount500Food, 500 <= visitCount, "방문 " + Utility.SetStringColor(500.ToString(), ColorType.Negative) + "회");
    }


    private void SetOrderFoodSlot(string foodId, bool isUnlock, string lockText)
    {
        if(string.IsNullOrWhiteSpace(foodId))
        {
            DebugLog.LogError("해당 음식의 id값이 없습니다: " + foodId);
            return;
        }

        FoodData foodData = FoodDataManager.Instance.GetFoodData(foodId);

        if(foodData == null)
        {
            throw new System.Exception("해당 음식의 데이터가 없습니다: " + foodId);
        }

        foreach(UIOrderFoodSlot slot in _orderFoodSlotList)
        {
            if (slot.gameObject.activeSelf)
                continue;

            slot.gameObject.SetActive(true);
            slot.SetSprite(foodData.Sprite);
            slot.SetMaterial(UserInfo.IsGiveRecipe(foodData) ? null : _grayMat);
            slot.SetColor(UserInfo.IsGiveRecipe(foodData) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive));

            if (isUnlock)
            {
                slot.DisableLockGroup();

            }
            else
            {
                slot.EnableLockGroup(lockText);
            }

            return;
        }

        throw new System.Exception("주문 슬롯이 부족합니다.");
    }

    private void SetEffectGroup(CustomerData data)
    {
        if(!UserInfo.GetCustomerEnableState(data))
        {
            _effectTitle.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            _orderFoodParent1.gameObject.SetActive(false);
            _orderFoodParent2.gameObject.SetActive(false);
            _effectLeftArrowButton.gameObject.SetActive(false);
            _effectRightArrowButton.gameObject.SetActive(false);
            return;
        }

        // int visitCount = UserInfo.GetVisitedCustomerCount(data);

        // if(visitCount <= 0)
        // {
        //     _effectTitle.gameObject.SetActive(true);
        //     _effectDescription.gameObject.SetActive(false);
        //     _effectLeftArrowButton.gameObject.SetActive(false);
        //     _effectRightArrowButton.gameObject.SetActive(false);

        //     _effectTitle.SetText("???");
        //     return;
        // }

        if (data is GatecrasherCustomerData)
        {
            _effectTitle.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            _orderFoodParent1.gameObject.SetActive(false);
            _orderFoodParent2.gameObject.SetActive(false);
            _effectLeftArrowButton.gameObject.SetActive(false);
            _effectRightArrowButton.gameObject.SetActive(false);
            return;
        }

        else if (data is SpecialCustomerData)
        {
            _effectTitle.gameObject.SetActive(true);
            _effectLeftArrowButton.gameObject.SetActive(false);
            _effectRightArrowButton.gameObject.SetActive(false);
            ChangeEffectGroup(CustomerEffectType.Effect);
            return;
        }

        else if(data is NormalCustomerData)
        {
            _effectType = CustomerEffectType.OrderFood1;
            _effectTitle.gameObject.SetActive(true);
            _effectLeftArrowButton.gameObject.SetActive(true);
            _effectRightArrowButton.gameObject.SetActive(true);
            ChangeEffectGroup(CustomerEffectType.OrderFood1);
            return;
        }
    }

    private void OnArrowButtonClicked(int dir)
    {
        if(dir == 0)
            return;

        int typeIndex = (int)_effectType + dir;
        typeIndex %= (int)CustomerEffectType.Length;
        if (typeIndex < 0)
            typeIndex = (int)CustomerEffectType.Length - 1;

        if((int)CustomerEffectType.OrderFood2 == typeIndex)
        {
            int childActiveCount = 0;
            foreach(Transform child in _orderFoodParent2)
            {
                if (child.gameObject.activeSelf)
                    childActiveCount++;
            }
            if(dir <= -1 && childActiveCount == 0)
            {
                typeIndex = (int)CustomerEffectType.OrderFood1;
            }
            else if(1 <= dir && childActiveCount == 0)
            {
                typeIndex = (int)CustomerEffectType.Effect;
            }
        }
        
        ChangeEffectGroup((CustomerEffectType)typeIndex);
    }

    private void ChangeEffectGroup(CustomerEffectType type)
    {
        _effectType = type;
        switch (type)
        {
            case CustomerEffectType.OrderFood1:
                _orderFoodParent1.gameObject.SetActive(true);
                _orderFoodParent2.gameObject.SetActive(false);
                _effectDescription.gameObject.SetActive(false);
                _effectTitle.text = "주문 요리1";
                break;
            case CustomerEffectType.OrderFood2:
                _orderFoodParent2.gameObject.SetActive(true);
                _orderFoodParent1.gameObject.SetActive(false);
                _effectDescription.gameObject.SetActive(false);
                _effectTitle.text = "주문 요리2";
                break;
            case CustomerEffectType.Effect:
                _effectDescription.gameObject.SetActive(true);
                _orderFoodParent1.gameObject.SetActive(false);
                _orderFoodParent2.gameObject.SetActive(false);
                _effectTitle.text = "특수 효과";
                _effectDescription.text = Utility.GetCustomerEffectDescription(_data);
                break;
        }
    }
}
