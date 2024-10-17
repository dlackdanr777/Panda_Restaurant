using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class AddTestButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private CustomerController _customerController;

    private void Start()
    {
        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        _customerController.AddCustomer();
    } 
}
