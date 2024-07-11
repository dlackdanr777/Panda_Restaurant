using UnityEngine;

public class UIKitchenTab : MonoBehaviour
{
    [SerializeField] private UIKitchen _uiKitchen;

    [Header("Slots")]
    [SerializeField] private UITabSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;


    private UITabSlot[] _slots;

    public void Init()
    {
        _slots = new UITabSlot[(int)KitchenUtensilType.Length];
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            int index = i;
            UITabSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots[index] = slot;
            slot.Init(() => _uiKitchen.ShowUIKitchen((KitchenUtensilType)index));
            BasicData data = UserInfo.GetEquipKitchenUtensil((KitchenUtensilType)i);
            Sprite sprite = data != null ? data.ThumbnailSprite : null;
            _slots[i].UpdateUI(sprite, Utility.KitchenUtensilTypeStringConverter((KitchenUtensilType)i));
        }

        UserInfo.OnChangeKitchenUtensilHandler += UpdateUI;
    }


    public void UpdateUI()
    {
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; i++)
        {
            UpdateUI((KitchenUtensilType)i);
        }
    }


    private void UpdateUI(KitchenUtensilType type)
    {
        BasicData data = UserInfo.GetEquipKitchenUtensil(type);
        Sprite sprite = data != null ? data.ThumbnailSprite : null;
        _slots[(int)type].UpdateUI(sprite, Utility.KitchenUtensilTypeStringConverter(type));
    }


}
