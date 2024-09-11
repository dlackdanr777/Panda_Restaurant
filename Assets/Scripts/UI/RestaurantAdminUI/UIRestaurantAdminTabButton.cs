using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRestaurantAdminTabButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;


    public void OnClickEvent(UnityAction action)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(action);
    }

    public void SelectButton()
    {
        _text.color = Color.white;
    }

    public void UnselectedButton()
    {
        _text.color = new Color(0.0f, 0.0f, 0.0f, 0.7f);
    }
}
