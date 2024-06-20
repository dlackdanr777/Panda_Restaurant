using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRestaurantAdminTabButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _image;


    public void OnClickEvent(UnityAction action)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(action);
    }

    public void SelectButton()
    {
        _image.color = Color.white;
    }

    public void UnselectedButton()
    {
        _image.color = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    }
}
