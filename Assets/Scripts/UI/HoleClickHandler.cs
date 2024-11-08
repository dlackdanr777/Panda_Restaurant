using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoleClickHandler : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
{
    public bool Interactable;
    [SerializeField] private RectTransform _parent;
    [SerializeField] private RectTransform _holeRect;
    public RectTransform HoleRect => _holeRect;
    private Action _action;
    private string _targetName;
    private Button _selectButton;
    private ButtonPressEffect _selectButtonPress;

    public void SetActive(bool value)
    {
        _parent.gameObject.SetActive(value);
    }

    public void SetTargetObjectName(string name)
    {
        _targetName = name;
    }

    public void AddListener(Action action)
    {
        _action += action;
    }

    public void RemoveAllListener() 
    { 
        _action = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        _selectButton = null;
        _selectButtonPress = null;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count <= 0)
            return;

        foreach (var result in results)
        {
            if (result.gameObject == gameObject)
                continue;

            if (result.gameObject == _holeRect.gameObject)
                continue;

            if (!string.IsNullOrWhiteSpace(_targetName) && !result.gameObject.name.Equals(_targetName))
                continue;

            Button button = result.gameObject.GetComponent<Button>();
            ButtonPressEffect effect = result.gameObject.GetComponent<ButtonPressEffect>();
            if (button == null && effect == null)
                continue;

            DebugLog.Log(name + ": ´Ù¿î");
            DebugLog.Log(button);
            DebugLog.Log(effect);
            button?.OnPointerDown(eventData);
            effect?.OnPointerDown(eventData);
            _selectButton = button;
            _selectButtonPress = effect;
            return;
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count <= 0)
            return;

        foreach (var result in results)
        {
            if (result.gameObject == gameObject)
                continue;

            if (result.gameObject == _holeRect.gameObject)
                continue;

            DebugLog.Log(name + ": " + result.gameObject.name + "(" + result.gameObject.name.Equals(_targetName) + ")");
            if (!string.IsNullOrWhiteSpace(_targetName) && !result.gameObject.name.Equals(_targetName))
                continue;

            Button button = result.gameObject.GetComponent<Button>();
            ButtonPressEffect effect = result.gameObject.GetComponent<ButtonPressEffect>();
            if (button == null && effect == null)
                continue;

            if (button != _selectButton || effect != _selectButtonPress)
                continue;

            DebugLog.Log(name + ": ¾÷");
            _selectButton?.OnPointerClick(eventData);
            _selectButtonPress?.OnPointerUp(eventData);
            _action?.Invoke();
            return;
        }

        _selectButtonPress?.ResetScale();
    }


}
