using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PointerClick : MonoBehaviour, IPointerClickHandler
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

   
    public void OnPointerClick(PointerEventData eventData)
    {
        _onPointerClickHandler?.Invoke();
    }
}
