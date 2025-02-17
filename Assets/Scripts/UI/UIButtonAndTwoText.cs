using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIButtonAndTwoText : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text1;
    [SerializeField] private TextMeshProUGUI _text2;

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

    public void SetText1(string text)
    {
        _text1.text = text;
    }

    public void SetText2(string text)
    {
        _text2.text = text;
    }
}
