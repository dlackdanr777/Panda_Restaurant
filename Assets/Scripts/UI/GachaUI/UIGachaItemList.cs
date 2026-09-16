using System.Collections.Generic;
using UnityEngine;
using Muks.RecyclableScrollView;

public class UIGachaSlotList : RecyclableVerticalScrollView<GachaData>
{
    [SerializeField] private UIGachaCard _card;

    public void UpdateMachineData(List<GachaData> dataList)
    {
        _scrollRect.StopMovement();
        Vector2 contentPosition = _contentRect.anchoredPosition;
        contentPosition.y = 0f;
        _contentRect.anchoredPosition = contentPosition;

        UpdateData(dataList ?? new List<GachaData>());
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    public override void AddInit()
    {
        HidePreviewCard();
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
        HidePreviewCard();
    }

    private void OnDisable() => HidePreviewCard();

    // The catalog's detail card is a sibling, not a child of this list. Hiding
    // the list while its parent is inactive does not run OnEnable/OnDisable,
    // so entry owners must also close the preview explicitly.
    public void HidePreviewCard()
    {
        if (_card != null) _card.gameObject.SetActive(false);
    }
}
