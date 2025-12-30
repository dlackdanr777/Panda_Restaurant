using System.Collections;
using Muks.Tween;
using TMPro;
using UnityEngine;

public class UIMain : MonoBehaviour
{
    private const string AdCustomerTimeKey = "AdCustomer";
    private const int AdCustomerTime = 300; // 5분

    [Header("Components")]
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private UIImageAndText _customerCountImage;
    [SerializeField] private GameObject _customerMaxCountImage;
    [SerializeField] private WatchAdButton _watchAdButton;
    [SerializeField] private TextMeshProUGUI _customerCountText;

    private void Awake()
    {
        _customerController.OnChangeCustomerHandler += OnChangeCustomerCountEvent;
        _customerController.OnGuideCustomerHandler += OnChangeCustomerCountEvent;
        GameManager.Instance.OnChangeMaxWaitCustomerCountHandler += OnChangeCustomerCountEvent;

        _customerController.OnAddCustomerHandler += OnUpdateAdButtonEvent;
        _customerController.OnGuideCustomerHandler += OnUpdateAdButtonEvent;
        GameManager.Instance.OnChangeMaxWaitCustomerCountHandler += OnUpdateAdButtonEvent;
        TimeManager.Instance.OnRemoveTimeHandler += OnRemoveTimeEvent;

        _watchAdButton.OnAdRewarded += OnAdButtonClicked;
        OnChangeCustomerCountEvent();
        OnUpdateAdButtonEvent();
    }

    private void OnAdButtonClicked()
    {
        StopAllCoroutines();
        StartCoroutine(AddCustomerRoutine());
    }


    private void OnEnable()
    {
        OnChangeCustomerCountEvent();
    }

    private void OnRemoveTimeEvent(string key)
    {
        if (key != AdCustomerTimeKey) return;

        OnUpdateAdButtonEvent();
    }


    private void OnUpdateAdButtonEvent()
    {
        if(!TimeManager.Instance.IsAddTime(AdCustomerTimeKey) ||  _customerController.IsMaxCount || 10 <= UserInfo.AddCustomerAdCount)
        {
            _watchAdButton.gameObject.SetActive(false);
            return;
        }

        _watchAdButton.gameObject.SetActive(true);
        _customerCountText.SetText("X " + (GameManager.Instance.MaxWaitCustomerCount - _customerController.Count).ToString());
    }


    private void OnChangeCustomerCountEvent()
    {
        if (_customerController.Count <= 9)
        {
            _customerCountImage.gameObject.SetActive(false);
            _customerMaxCountImage.gameObject.SetActive(false);
            return;
        }

        else if (_customerController.Count < 9 && _customerController.Count < GameManager.Instance.MaxWaitCustomerCount)
        {
            _customerCountImage.gameObject.SetActive(true);
            _customerMaxCountImage.SetActive(false);
            _customerCountImage.SetText(Mathf.Clamp(_customerController.Count - 9, 0, 30).ToString());
        }

        else if (GameManager.Instance.MaxWaitCustomerCount <= _customerController.Count)
        {
            _customerMaxCountImage.SetActive(true);
            _customerCountImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator AddCustomerRoutine()
    {
        UserInfo.AddAddCustomerAdCount();
        TimeManager.Instance.SetTime(AdCustomerTimeKey, AdCustomerTime);
        while (!_customerController.IsMaxCount)
        {
            _customerController.AddCustomer();
            _watchAdButton.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.3f);
        }

        OnUpdateAdButtonEvent();
    }

}
