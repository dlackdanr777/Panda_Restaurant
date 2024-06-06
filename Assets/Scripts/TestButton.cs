using UnityEngine;
using UnityEngine.UI;

public class TestButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Transform _targetTr;
    [SerializeField] private CustomerController _customerController;


    void Start()
    {
        _button.onClick.AddListener(OnButtonClicked);
    }


    private void OnButtonClicked()
    {
        _customerController.GuideCustomer(_targetTr.position);
    }

}
