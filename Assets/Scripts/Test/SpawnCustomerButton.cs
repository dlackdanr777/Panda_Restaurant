using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SpawnCustomerButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _buttonText;
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private string _testId;
    private void Awake()
    {
        _button.onClick.AddListener(OnButtonClickEvent);
        _buttonText.text = _testId;
    }

    private void OnButtonClickEvent()
    {
        _customerController.SpawnCustomer(_testId);
    }
}
