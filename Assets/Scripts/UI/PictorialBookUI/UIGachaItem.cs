using System.Collections.Generic;
using UnityEngine;

public class UIGachaItem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIGachaItemView _view;

    [Header("Slot Options")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIGachaItemSlot _slotPrefab;


    private List<GachaItemData> _gachaItemDataList;
    private List<UIGachaItemSlot> _slotList = new List<UIGachaItemSlot>();

    public void Init()
    {
        _gachaItemDataList = ItemManager.Instance.GetGachaItemDataList();
        
        for(int i = 0, cnt = _gachaItemDataList.Count; i < cnt; ++i) 
        {
            UIGachaItemSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetButtonEvent(OnSlotClicked);
            slot.SetData(_gachaItemDataList[i]);
            _slotList.Add(slot);
        }

        UserInfo.OnChangeGachaItemSortTypeHandler += OnChangeGachaItemSortTypeEvent;
    }

    private void OnChangeGachaItemSortTypeEvent()
    {
        _gachaItemDataList = ItemManager.Instance.GetGachaItemDataList();

        for (int i = 0, cnt = _gachaItemDataList.Count; i < cnt; ++i)
        {
            UIGachaItemSlot slot;
            if (_slotList.Count - 1 < i)
            {
                slot = Instantiate(_slotPrefab, _slotParent);
                _slotList.Add(slot);
            }
            else
            {
                slot = _slotList[i];
            }

            slot.SetButtonEvent(OnSlotClicked);
            slot.SetData(_gachaItemDataList[i]);
        }
    }


    private void OnSlotClicked(GachaItemData data)
    {
        _view.SetData(data);
    }
}
