using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIButtonImageText : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;

    public void AddListener(UnityAction action)
    {
        _button.onClick.AddListener(action);
    }

    public void RemoveAllListeners()
    {
        _button.onClick.RemoveAllListeners();
    }

    public void Interactable(bool value)
    {
        _button.interactable = value;
    }

    public void SetText(string text)
    {
        _text.SetText(text);
    }
    
    public void SetImage(Sprite sprite)
    {
        _image.sprite = sprite;
    }
}
