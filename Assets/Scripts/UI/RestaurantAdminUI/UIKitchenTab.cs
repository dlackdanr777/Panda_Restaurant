using UnityEngine;

public class UIKitchenTab : MonoBehaviour
{
    [SerializeField] private UIFurniture _uiFurniture;

    [Header("Slots")]
    [SerializeField] private UIKitchenTabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UIKitchenTabSlot[] _slots;

    public void Init()
    {
        _slots = new UIKitchenTabSlot[(int)KitchenUtensilType.Length];
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            int index = i;
            UIKitchenTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            //slot.Init(() => _uiFurniture.ShowUIFurniture((KitchenUtensilType)index));
            slot.SetData(KitchenUtensilType.Burner1 + index);
        }

        UserInfo.OnChangeKitchenUtensilHandler += SlotUpdate;
    }



    public void UpdateUI()
    {

        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            SlotUpdate((KitchenUtensilType)i);
        }
    }

    private void SlotUpdate(KitchenUtensilType type)
    {
        _slots[(int)type].SetData(type);
    }
}
