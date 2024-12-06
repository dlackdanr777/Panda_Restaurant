using System;
using UnityEngine;
using UnityEngine.UI;

public class UISettingButton : MonoBehaviour
{
    [SerializeField] private Button _onButton;
    [SerializeField] private Button _offButton;

    private Action _onButtonClicked;
    private Action _offButtonClicked;


    public void Init(Action onButtonClicked, Action offButtonClicked, bool isOn)
    {
        _onButtonClicked = onButtonClicked;
        _offButtonClicked = offButtonClicked;

        _onButton.gameObject.SetActive(isOn);
        _offButton.gameObject.SetActive(!isOn);

        _onButton.onClick.AddListener(OnButtonClicked);
        _offButton.onClick.AddListener(OffButtonClicked);
    }

    public void IsOn(bool isOn)
    {
        _onButton.gameObject.SetActive(isOn);
        _offButton.gameObject.SetActive(!isOn);
    }


    private void OnButtonClicked()
    {
        _offButton.gameObject.SetActive(true);
        _onButton.gameObject.SetActive(false);
        _onButtonClicked?.Invoke();
    }

    private void OffButtonClicked()
    {
        _onButton.gameObject.SetActive(true);
        _offButton.gameObject.SetActive(false);
        _offButtonClicked?.Invoke();
    }
}
