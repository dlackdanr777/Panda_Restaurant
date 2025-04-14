using System;
using UnityEngine;
using UnityEngine.UI;

public class UIFurnitureTab : UIRestaurantAdminTab
{
    [Header("Components")]
    [SerializeField] private UIFurniture _uiFurniture;

    [Space]
    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;
    private ERestaurantFloorType _floorType;

    public override void Init()
    {
        _slots = new UITabSlot[(int)FurnitureType.Length];
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => ShowUIFurniture(index));
            BasicData data = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _floorType, (FurnitureType)index);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.FurnitureTypeStringConverter((FurnitureType)index));
            slot.name = "FurnitureTabSlot" + (i + 1);
        }

        UserInfo.OnChangeFurnitureHandler += UpdateUI;
    }


    public override void UpdateUI()
    {
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            UpdateUI(_floorType, (FurnitureType)i);
        }
    }

    public override void SetAttention()
    {

    }

    public override void SetNotAttention()
    {
    }

    public void ChangeFloorType(ERestaurantFloorType floorType)
    {
        if (_floorType == floorType)
            return;

        _floorType = floorType;
        UpdateUI();
    }


    private void UpdateUI(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (_floorType != floorType)
            return;

        BasicData data = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, floorType, type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.FurnitureTypeStringConverter(type));
    }


    public void ShowUIFurniture(int index)
    {
        _uiFurniture.ShowUIFurniture(_floorType, (FurnitureType)index);
    }

    public void ShowUIFurniture(FurnitureType type)
    {
        _uiFurniture.ShowUIFurniture(_floorType, type);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= UpdateUI;
    }
}
