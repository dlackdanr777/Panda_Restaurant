using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIButtonAndPressEffect : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private ButtonPressEffect _pressEffect;


    public bool interactable
    {
        get
        {
            return _button.interactable;
        }

        set
        {
            _button.interactable = value;
            _pressEffect.Interactable = value;
        }
    }


    public void AddListener(UnityAction action)
    {
        _button.onClick.AddListener(action);
    }

    public void RemoveListener(UnityAction action)
    {
        _button.onClick.RemoveListener(action);
    }

    public void RemoveAllListener()
    {
        _button.onClick.RemoveAllListeners();
    }
}
