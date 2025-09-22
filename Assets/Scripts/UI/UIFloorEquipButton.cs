using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI3LayerButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _backgroundButton;
    [SerializeField] private Image _leftImage;
    [SerializeField] private TextMeshProUGUI _leftText;
    [SerializeField] private TextMeshProUGUI _centerText;

    public void AddListener(UnityAction action)
    {
        _button.onClick.AddListener(action);
    }

    public void RemoveAllListeners()
    {
        _button.onClick.RemoveAllListeners();
    }

    public void SetLeftImageColor(Color color)
    {
        if (_leftImage != null)
        {
            _leftImage.color = color;
        }
    }

    public void SetBackgroundColor(Color color)
    {
        if (_backgroundButton != null)
        {
            _backgroundButton.color = color;
        }
    }

    public void SetLeftText(string text)
    {
        if (_leftText != null)
        {
            _leftText.text = text;
        }
    }

    public void SetCenterText(string text)
    {
        if (_centerText != null)
        {
            _centerText.text = text;
        }
    }
}
