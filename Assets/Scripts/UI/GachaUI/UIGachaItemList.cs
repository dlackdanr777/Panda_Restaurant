using System.Collections.Generic;
using UnityEngine;
using Muks.RecyclableScrollView;

public class UIGachaSlotList : RecyclableVerticalScrollView<GachaData>
{
    [SerializeField] private UIGachaCard _card;

    internal float FourRowFrameHeight(RectTransform frame)
    {
        var viewport = _scrollRect.viewport != null ? _scrollRect.viewport : (RectTransform)_scrollRect.transform;
        return 4 * _slotPrefab.Height + 3 * _spacing + _topOffset + _bottomOffset +
            Mathf.Max(0, frame.rect.height - viewport.rect.height);
    }

    // Copy of the last bound items. Even with the same List reference the contents may change, so compare element by element.
    private readonly List<GachaData> _boundItems = new List<GachaData>();
    private bool _hasBoundItems;
    private float _boundContentWidth;
    private static readonly List<GachaData> EmptyItems = new List<GachaData>();

    public void UpdateMachineData(List<GachaData> dataList)
    {
        dataList = dataList ?? EmptyItems;
        // Positions are computed from the first visible row, so the skip is only safe if the list was already at the top.
        bool wasAtTop = _tmpfirstVisibleIndex == 0;
        ResetScrollPosition();
        if (wasAtTop && IsBoundTo(dataList)) _dataList = dataList;
        else UpdateData(dataList);
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ResetScrollPosition()
    {
        _scrollRect.StopMovement();
        Vector2 contentPosition = _contentRect.anchoredPosition;
        contentPosition.y = 0f;
        _contentRect.anchoredPosition = contentPosition;
    }

    private bool IsBoundTo(List<GachaData> dataList)
    {
        if (!_hasBoundItems || _boundItems.Count != dataList.Count || _boundContentWidth != _contentRect.rect.width)
            return false;
        for (int i = 0; i < dataList.Count; i++)
            if (!ReferenceEquals(_boundItems[i], dataList[i])) return false;
        return true;
    }

    public override void Init(List<GachaData> dataList)
    {
        _hasBoundItems = false;
        base.Init(dataList);
    }

    public override void UpdateData(List<GachaData> dataList)
    {
        base.UpdateData(dataList);
        _boundItems.Clear();
        if (dataList != null) _boundItems.AddRange(dataList);
        _boundContentWidth = _contentRect.rect.width;
        _hasBoundItems = true;
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
