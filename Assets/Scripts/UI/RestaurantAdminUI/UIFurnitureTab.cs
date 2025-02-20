using System;
using UnityEngine;
using UnityEngine.UI;

public class UIFurnitureTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIFurniture _uiFurniture;
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
        _slots = new UITabSlot[(int)FurnitureType.Length];
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => OnSlotClicked(index));
            BasicData data = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _floorType, (FurnitureType)index);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.FurnitureTypeStringConverter((FurnitureType)index));
            slot.name = "FurnitureTabSlot" + (i + 1);
        }

        _floor1Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor1));
        _floor2Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor2));
        _floor3Button.onClick.AddListener(() => ChangeFloorType(ERestaurantFloorType.Floor3));

        UserInfo.OnChangeFurnitureHandler += UpdateUI;
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
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            UpdateUI(_floorType, (FurnitureType)i);
        }
    }


    private void UpdateUI(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (_floorType != floorType)
            return;

        BasicData data = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, floorType, type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.FurnitureTypeStringConverter(type));
    }


    private void OnSlotClicked(int index)
    {
        _uiFurniture.ShowUIFurniture(_floorType, (FurnitureType)index);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= UpdateUI;
    }
}
