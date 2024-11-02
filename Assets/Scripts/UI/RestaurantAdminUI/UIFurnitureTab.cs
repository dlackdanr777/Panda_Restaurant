using System;
using UnityEngine;

public class UIFurnitureTab : MonoBehaviour
{
    [SerializeField] private UIFurniture _uiFurniture;

    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;

    public void Init()
    {
        _slots = new UITabSlot[(int)FurnitureType.Length];
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => _uiFurniture.ShowUIFurniture((FurnitureType)index));
            BasicData data = UserInfo.GetEquipFurniture((FurnitureType)index);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            slot.UpdateUI(sprite, Utility.FurnitureTypeStringConverter((FurnitureType)index));
            slot.name = "Slot" + (i + 1);
        }

        UserInfo.OnChangeFurnitureHandler += UpdateUI;
    }



    public void UpdateUI()
    {
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            UpdateUI((FurnitureType)i);
        }
    }


    private void UpdateUI(FurnitureType type)
    {
        BasicData data = UserInfo.GetEquipFurniture(type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.FurnitureTypeStringConverter(type));
    }
}
