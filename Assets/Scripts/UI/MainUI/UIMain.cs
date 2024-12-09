using Muks.Tween;
using UnityEngine;

public class UIMain : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private UIImageAndText _customerCountImage;
    [SerializeField] private GameObject _customerMaxCountImage;
    [SerializeField] private UIButtonAndText _watchAdButton;


    private void Awake()
    {
        _customerController.OnAddCustomerHandler += OnChangeCustomerCountEvent;
        _customerController.OnGuideCustomerHandler += OnChangeCustomerCountEvent;
        GameManager.Instance.OnChangeMaxWaitCustomerCountHandler += OnChangeCustomerCountEvent;
        _customerController.OnAddCustomerHandler += OnUpdateAdButtonEvent;
        _customerController.OnGuideCustomerHandler += OnUpdateAdButtonEvent;
        GameManager.Instance.OnChangeMaxWaitCustomerCountHandler += OnUpdateAdButtonEvent;

        _watchAdButton.AddListener(OnAdButtonClicked);
        OnChangeCustomerCountEvent();
        OnUpdateAdButtonEvent();
    }

    private void OnAdButtonClicked()
    {
        int count = GameManager.Instance.MaxWaitCustomerCount - _customerController.Count;
        for(int i = 0; i < count; ++i)
        {
            Tween.Wait(0.2f, () => _customerController.AddCustomer());
        }
    }


    private void OnEnable()
    {
        OnChangeCustomerCountEvent();
    }


    private void OnUpdateAdButtonEvent()
    {
        if(GameManager.Instance.MaxWaitCustomerCount <= _customerController.Count)
        {
            _watchAdButton.gameObject.SetActive(false);
            return;
        }

        _watchAdButton.gameObject.SetActive(true);
        _watchAdButton.SetText("X "+ (GameManager.Instance.MaxWaitCustomerCount - _customerController.Count).ToString());
    }


    private void OnChangeCustomerCountEvent()
    {
        if(_customerController.Count <= 9)
        {
            _customerCountImage.gameObject.SetActive(false);
            _customerMaxCountImage.gameObject.SetActive(false);
            return;
        }


        if(_customerController.Count < GameManager.Instance.MaxWaitCustomerCount)
        {
            _customerCountImage.gameObject.SetActive(true);
            _customerMaxCountImage.SetActive(false);
            _customerCountImage.SetText((_customerController.Count - 9).ToString());
        }

        else
        {
            _customerMaxCountImage.SetActive(true);
            _customerCountImage.gameObject.SetActive(false);
        }
    }

}
