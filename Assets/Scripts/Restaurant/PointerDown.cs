using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PointerDown : MonoBehaviour, IPointerDownHandler
{
    private Action _onPointerClickHandler;

    public void AddEvent(Action action)
    {
        _onPointerClickHandler += action;
    }

    public void RemoveEvent(Action action)
    {
        _onPointerClickHandler -= action;
    }

    public void RemoveAllEvent()
    {
        _onPointerClickHandler = null;
    }

   
    public void OnPointerDown(PointerEventData eventData)
    {
        _onPointerClickHandler?.Invoke();
    }
}
