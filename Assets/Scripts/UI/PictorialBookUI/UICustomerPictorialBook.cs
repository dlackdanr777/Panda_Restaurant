using System.Collections.Generic;
using UnityEngine;

public class UICustomerPictorialBook : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UICustomerView _view;

    [Header("Slot Options")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UICustomerSlot _slotPrefab;


    private List<CustomerData> _customerList;
    private List<UICustomerSlot> _slotList = new List<UICustomerSlot>();

    public void Init()
    {
        _customerList = CustomerDataManager.Instance.GetSortCustomerList();
        
        for(int i = 0, cnt = _customerList.Count; i < cnt; ++i) 
        {
            UICustomerSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetButtonEvent(OnSlotClicked);
            slot.SetData(_customerList[i]);
            _slotList.Add(slot);
        }
        ResetData();
        UserInfo.OnChangeGachaItemSortTypeHandler += OnChangeGachaItemSortTypeEvent;
    }
    

    public void ResetData()
    {
        _view.SetData(_customerList[0] != null ? _customerList[0] : null);
    }

    public void ChoiceView()
    {
        _view.ChoiceView();
    }

    private void OnChangeGachaItemSortTypeEvent()
    {
        _customerList = CustomerDataManager.Instance.GetSortCustomerList();

        for (int i = 0, cnt = _customerList.Count; i < cnt; ++i)
        {
            UICustomerSlot slot;
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
            slot.SetData(_customerList[i]);
        }
    }

    private void OnSlotClicked(CustomerData data)
    {
        _view.SetData(data);
    }
}
