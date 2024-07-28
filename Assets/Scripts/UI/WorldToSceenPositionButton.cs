using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public class WorldToSceenPositionButton : WorldToSceenPosition
{
    [SerializeField] private Button _button;

    public void AddListener(UnityAction call)
    {
        _button.onClick.AddListener(call);
    }

    public void RemoveAllListeners()
    {
        _button.onClick.RemoveAllListeners();
    }
}
