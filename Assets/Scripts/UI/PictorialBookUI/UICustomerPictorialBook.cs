using System.Collections.Generic;
using UnityEngine;

public class UICustomerPictorialBook : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UICustomerView _view;
    [SerializeField] private RectTransform _customerScrollView;
    [SerializeField] private UIPictorialBookCustomerSkin _skinView;
    [SerializeField] private GameObject _alarm;

    [Space]
    [Header("Slot Options")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UICustomerSlot _slotPrefab;

    private List<CustomerData> _customerList;
    private readonly List<UICustomerSlot> _slotList = new List<UICustomerSlot>();
    private CustomerData _selectedCustomer;
    private bool _initialized;
    private bool _tabVisible;
    private bool _listDirty = true;
    private bool _sortDirty;
    private bool _viewDirty = true;
    private bool _skinDirty;
    private bool CanRefresh => _initialized && _tabVisible && gameObject.activeInHierarchy;

    public void Init()
    {
        if (_initialized) return;
        _view.Init(this);
        _skinView.Init(this);
        BindSortedCustomers();
        _initialized = true;
        ResetData();
        UserInfo.OnChangeCustomerSortTypeHandler += OnChangeCustomerSortTypeEvent;
        UserInfo.OnAddNotificationHandler += CheckCustomerNotification;
        UserInfo.OnRemoveNotificationHandler += CheckCustomerNotification;
        UserInfo.OnChangeCustomerSkinHandler += UpdateUI;
        UserInfo.OnVisitedCustomerHandler += UpdateUI;
        UserInfo.OnGiveCustomerSkinHandler += UpdateUI;
    }

    public void ResetData()
    {
        _selectedCustomer = _customerList.Count > 0 ? _customerList[0] : null;
        _viewDirty = true;
        CheckCustomerNotification(string.Empty);
    }

    internal void PrepareForOpen()
    {
        _tabVisible = false;
        if (_sortDirty) BindSortedCustomers();
        ResetData();
        HideSkinView();
    }

    internal void SetTabVisible(bool visible)
    {
        _tabVisible = visible;
        if (visible) RefreshVisible();
    }

    public void ChoiceView() => _view.ChoiceView();

    // Legacy data events do not carry an identity. Merge them until the next visible flush.
    public void UpdateUI()
    {
        _listDirty = _viewDirty = _skinDirty = true;
    }

    private void LateUpdate() => RefreshVisible();

    internal void RefreshVisible()
    {
        if (!CanRefresh) return;
        if (_sortDirty) BindSortedCustomers();
        if (_listDirty && _customerScrollView.gameObject.activeInHierarchy)
        {
            _listDirty = false;
            foreach (var slot in _slotList) slot.UpdateUI();
        }
        if (_skinView.gameObject.activeSelf)
        {
            if (_skinDirty)
            {
                _skinDirty = false;
                _skinView.UpdateUI();
            }
        }
        else if (_viewDirty)
        {
            _viewDirty = false;
            _view.SetData(_selectedCustomer);
        }
    }

    public void OnChangeCustomerSortTypeEvent()
    {
        _sortDirty = _listDirty = true;
    }

    private void BindSortedCustomers()
    {
        _customerList = CustomerDataManager.Instance.GetSortCustomerList();
        for (int i = 0; i < _customerList.Count; i++)
        {
            if (i == _slotList.Count)
            {
                var slot = Instantiate(_slotPrefab, _slotParent);
                slot.Init();
                slot.SetButtonEvent(OnSlotClicked);
                _slotList.Add(slot);
            }
            _slotList[i].SetData(_customerList[i], false);
        }
        _sortDirty = false;
        _listDirty = true;
    }

    public void ShowSkinView(NormalCustomerData customerData)
    {
        _customerScrollView.gameObject.SetActive(false);
        _skinDirty = false;
        _skinView.Show(customerData);
    }

    public void HideSkinView()
    {
        _customerScrollView.gameObject.SetActive(true);
        _skinView.Hide();
        RefreshVisible();
    }

    private void OnSlotClicked(CustomerData data)
    {
        _selectedCustomer = data;
        _viewDirty = true;
        RefreshVisible();
    }

    private void CheckCustomerNotification(string id)
    {
        bool alarm = false;
        foreach (var customer in _customerList)
            if (UserInfo.IsAddNotification(customer.Id)) { alarm = true; break; }
        if (_alarm.activeSelf != alarm) _alarm.SetActive(alarm);
    }

    private void OnDestroy()
    {
        UserInfo.OnChangeCustomerSortTypeHandler -= OnChangeCustomerSortTypeEvent;
        UserInfo.OnAddNotificationHandler -= CheckCustomerNotification;
        UserInfo.OnRemoveNotificationHandler -= CheckCustomerNotification;
        UserInfo.OnChangeCustomerSkinHandler -= UpdateUI;
        UserInfo.OnVisitedCustomerHandler -= UpdateUI;
        UserInfo.OnGiveCustomerSkinHandler -= UpdateUI;
    }
}
