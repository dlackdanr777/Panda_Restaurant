using UnityEngine;
using UnityEngine.UI;

public class UIKitchenTab : UIRestaurantAdminTab
{
    [Header("Components")]
    [SerializeField] private UIKitchen _uiKitchen;


    [Space]
    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;

    public override void Init()
    {
        _slots = new UITabSlot[(int)KitchenUtensilType.Length];
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            BasicData data = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _floorType, (KitchenUtensilType)i);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.KitchenUtensilTypeStringConverter((KitchenUtensilType)i));
            slot.name = "KitchenTabSlot" + (i + 1);
        }

        UserInfo.OnChangeKitchenUtensilHandler += UpdateUI;
    }


    public override void SetAttention()
    {
    }


    public override void SetNotAttention()
    {
    }

    public void ShowUIKitchen(KitchenUtensilType type)
    {
        _uiKitchen.ShowUIKitchen(_floorType, type);
    }


    public override void UpdateUI()
    {
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            UpdateUI(_floorType, (KitchenUtensilType)i);
        }
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;
        UpdateUI();
    }


    private void UpdateUI(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        if (_floorType != floorType)
            return;

        BasicData data = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, floorType, type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.KitchenUtensilTypeStringConverter(type));
    }


    private void OnSlotClicked(int index)
    {
        _uiKitchen.ShowUIKitchen(_floorType, (KitchenUtensilType)index);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeKitchenUtensilHandler -= UpdateUI;
    }
}
