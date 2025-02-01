using UnityEngine;
using UnityEngine.UI;

public class UIKitchenTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIKitchen _uiKitchen;
    [SerializeField] private Button _floor1Button;
    [SerializeField] private Button _floor2Button;
    [SerializeField] private Button _floor3Button;


    [Space]
    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;

    public void Init()
    {
        _slots = new UITabSlot[(int)KitchenUtensilType.Length];
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            BasicData data = UserInfo.GetEquipKitchenUtensil(_floorType, (KitchenUtensilType)i);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.KitchenUtensilTypeStringConverter((KitchenUtensilType)i));
            slot.name = "KitchenTabSlot" + (i + 1);
        }

        _floor1Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor1));
        _floor2Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor2));
        _floor3Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor3));

        UserInfo.OnChangeKitchenUtensilHandler += UpdateUI;
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;
        UpdateUI();
    }


    public void UpdateUI()
    {
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            UpdateUI(_floorType, (KitchenUtensilType)i);
        }
    }


    private void UpdateUI(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        if (_floorType != floorType)
            return;

        BasicData data = UserInfo.GetEquipKitchenUtensil(floorType, type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.KitchenUtensilTypeStringConverter(type));
    }

    private void OnSlotClicked(int index)
    {
        _uiKitchen.ShowUIKitchen(_floorType, (KitchenUtensilType)index);
    }
}
