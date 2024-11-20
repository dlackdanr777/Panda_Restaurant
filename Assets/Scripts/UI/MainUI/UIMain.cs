using UnityEngine;

public class UIMain : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private UIImageAndText _customerCountImage;


    private void Awake()
    {
        _customerController.OnAddCustomerHandler += OnChangeCustomerCountEvent;
        _customerController.OnGuideCustomerHandler += OnChangeCustomerCountEvent;

        OnChangeCustomerCountEvent();
    }

    private void OnEnable()
    {
        OnChangeCustomerCountEvent();
    }

    private void OnChangeCustomerCountEvent()
    {
        if(_customerController.Count <= 9)
        {
            _customerCountImage.gameObject.SetActive(false);
            return;
        }

        _customerCountImage.gameObject.SetActive(true);
        _customerCountImage.SetText((_customerController.Count - 9).ToString());
    }

}
