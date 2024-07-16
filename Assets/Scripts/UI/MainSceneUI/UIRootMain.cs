using UnityEngine;

public class UIRootMain : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private CustomerController _customerController;

    [Header("Buttons")]
    [SerializeField] private FillAmountButton _addCustomerButton;


    private int _tabCount = 0;


    private void Awake()
    {
        _addCustomerButton.AddListener(OnAddCustomerButtonClicked);
    }


    private void OnAddCustomerButtonClicked()
    {
        if(GameManager.Instance.TotalTabCount - 1 <= _tabCount)
        {
            _customerController.AddCustomer();
            _addCustomerButton.SetFillAmonut(0);
            _tabCount = 0;
            return;
        }

        _tabCount++;
        _addCustomerButton.SetFillAmonut((float)_tabCount / (GameManager.Instance.TotalTabCount - 1));
    }
}
