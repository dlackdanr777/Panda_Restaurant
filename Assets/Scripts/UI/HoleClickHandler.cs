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
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

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
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, _raycastResults);

        if (_raycastResults.Count <= 0)
            return;

        foreach (var result in _raycastResults)
        {
            if (result.gameObject == gameObject)
                continue;

            if (result.gameObject == _holeRect.gameObject)
                continue;

            if (!string.IsNullOrWhiteSpace(_targetName) && !result.gameObject.name.Equals(_targetName))
                continue;

            Button button = result.gameObject.GetComponent<Button>();
            ButtonPressEffect effect = result.gameObject.GetComponent<ButtonPressEffect>();
            IPointerDownHandler clickHandler = result.gameObject.GetComponent<IPointerDownHandler>();
            SpriteTouchEvent touchEvent = result.gameObject.GetComponent<SpriteTouchEvent>();
            if (button == null && effect == null && clickHandler == null && touchEvent == null)
                continue;

            DebugLog.Log(name + ": ´Ù¿î");
            DebugLog.Log(button);
            DebugLog.Log(effect);
            button?.OnPointerDown(eventData);
            effect?.OnPointerDown(eventData);
            clickHandler?.OnPointerDown(eventData);
            touchEvent?.OnPointerDown(eventData);
            _selectButton = button;
            _selectButtonPress = effect;
            return;
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        _raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, _raycastResults);

        if (_raycastResults.Count <= 0)
            return;

        foreach (var result in _raycastResults)
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
            IPointerUpHandler clickHandler = result.gameObject.GetComponent<IPointerUpHandler>();
            SpriteTouchEvent touchEvent = result.gameObject.GetComponent<SpriteTouchEvent>();
            if (button != _selectButton || effect != _selectButtonPress)
                continue;

            _selectButton?.OnPointerClick(eventData);
            _selectButtonPress?.OnPointerUp(eventData);
            clickHandler?.OnPointerUp(eventData);
            touchEvent?.OnPointerUp(eventData); 
            _action?.Invoke();  
            return;
        }

        _selectButtonPress?.ResetScale();
    }


}
