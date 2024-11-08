using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SpawnCustomerButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private string _testId;
    private void Awake()
    {
        _button.onClick.AddListener(OnButtonClickEvent);
    }

    private void OnButtonClickEvent()
    {
        _customerController.SpawnCustomer(_testId);
    }
}
