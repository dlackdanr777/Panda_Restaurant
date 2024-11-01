using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoleClickHandler : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private RectTransform _parent;
    [SerializeField] private RectTransform _holeRect;
    private Action _action;

    private Button _selectButton;
    private ButtonPressEffect _selectButtonPress;

    public void SetActive(bool value)
    {
        _parent.gameObject.SetActive(value);
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

            if (!result.gameObject.TryGetComponent(out Button button) || !result.gameObject.TryGetComponent(out ButtonPressEffect effect))
                continue;

            button?.OnPointerDown(eventData);
            effect?.OnPointerDown(eventData);
            _selectButton = button;
            _selectButtonPress = effect;
            return;
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
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

            if (!result.gameObject.TryGetComponent(out Button button) || !result.gameObject.TryGetComponent(out ButtonPressEffect effect))
                continue;

            if (button != _selectButton || effect != _selectButtonPress)
                continue;

            _selectButton?.OnPointerClick(eventData);
            _selectButtonPress?.OnPointerUp(eventData);
            _action?.Invoke();
            return;
        }

        _selectButtonPress?.ResetScale();
    }


}
