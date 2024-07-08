using UnityEngine;

public class UIFurnitureTab : MonoBehaviour
{
    [SerializeField] private UIFurniture _uiFurniture;

    [Header("Slots")]
    [SerializeField] private UIFurnitureTabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UIFurnitureTabSlot[] _slots;

    public void Init()
    {
        _slots = new UIFurnitureTabSlot[(int)FurnitureType.Length];
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            int index = i;
            UIFurnitureTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => _uiFurniture.ShowUIFurniture((FurnitureType)index));
            slot.SetData(FurnitureType.Table1 + index);
        }

        UserInfo.OnChangeFurnitureHandler += SlotUpdate;
    }



    public void UpdateUI()
    {

        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            SlotUpdate((FurnitureType)i);
        }
    }

    private void SlotUpdate(FurnitureType type)
    {
        _slots[(int)type].SetData(type);
    }
}
