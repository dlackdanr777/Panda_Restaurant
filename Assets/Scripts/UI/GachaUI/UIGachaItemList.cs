using UnityEngine;
using Muks.RecyclableScrollView;

public class UIGachaSlotList : RecyclableVerticalScrollView<GachaData>
{
    [SerializeField] private UIGachaCard _card;
    public override void AddInit()
    {
        foreach (var slot in _slotList)
        {
            if (slot is UIGachaItemSlot itemSlot)
            {
                itemSlot.SetCard(_card);
            }
        }
        _card.Init();
    }

    private void OnEnable()
    {
        _card.gameObject.SetActive(false);
    }
}
