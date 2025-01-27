using System.Collections.Generic;
using UnityEngine;

public class UIPictorialBookGachaItem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIPictorialBookGachaItemView _view;
    [SerializeField] private GameObject _alarm;

    [Header("Slot Options")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIPictorialBookGachaItemSlot _slotPrefab;


    private List<GachaItemData> _gachaItemDataList;
    private List<UIPictorialBookGachaItemSlot> _slotList = new List<UIPictorialBookGachaItemSlot>();

    public void Init()
    {
        _view.Init();
        _gachaItemDataList = ItemManager.Instance.GetSortGachaItemDataList();
        
        for(int i = 0, cnt = _gachaItemDataList.Count; i < cnt; ++i) 
        {
            UIPictorialBookGachaItemSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetButtonEvent(OnSlotClicked);
            slot.SetData(_gachaItemDataList[i]);
            _slotList.Add(slot);
        }
        ResetData();
        CheckItemNotification(string.Empty);
        UserInfo.OnChangeGachaItemSortTypeHandler += OnChangeGachaItemSortTypeEvent;
        UserInfo.OnAddNotificationHandler += CheckItemNotification;
        UserInfo.OnRemoveNotificationHandler += CheckItemNotification;
    }
    

    public void ResetData()
    {
        _view.SetData(_gachaItemDataList[0] != null ? _gachaItemDataList[0] : null);
        CheckItemNotification(string.Empty);
    }


    public void ChoiceView()
    {
        _view.ChoiceView();
    }


    public void UpdateUI()
    {
        _view.SetData(_gachaItemDataList[0] != null ? _gachaItemDataList[0] : null);
        for (int i = 0, cnt = _slotList.Count; i < cnt; ++i)
        {
            _slotList[i].UpdateUI();
        }
    }


    private void OnChangeGachaItemSortTypeEvent()
    {
        _gachaItemDataList = ItemManager.Instance.GetSortGachaItemDataList();

        for (int i = 0, cnt = _gachaItemDataList.Count; i < cnt; ++i)
        {
            UIPictorialBookGachaItemSlot slot;
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


    private void CheckItemNotification(string id)
    {
        for(int i = 0, cnt = _gachaItemDataList.Count; i < cnt; ++i)
        {
            if (!UserInfo.IsAddNotification(_gachaItemDataList[i].Id))
                continue;

            _alarm.SetActive(true);
            return;
        }

        _alarm.SetActive(false);
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeGachaItemSortTypeHandler -= OnChangeGachaItemSortTypeEvent;
        UserInfo.OnAddNotificationHandler -= CheckItemNotification;
        UserInfo.OnRemoveNotificationHandler -= CheckItemNotification;
    }
}
