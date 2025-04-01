using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpriteTouchEvent : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Action _onPointerDownHandler;
    private Action _onPointerUpHandler;

    public void AddDownEvent(Action action)
    {
        _onPointerDownHandler += action;
    }

    public void RemoveDownEvent(Action action)
    {
        _onPointerDownHandler -= action;
    }

    public void RemoveAllDownEvent()
    {
        _onPointerDownHandler = null;
    }

    public void AddUpEvent(Action action)
    {
        _onPointerUpHandler += action;
    }

    public void RemoveUpEvent(Action action)
    {
        _onPointerUpHandler -= action;
    }

    public void RemoveAllUpEvent()
    {
        _onPointerUpHandler = null;
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        _onPointerDownHandler?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _onPointerUpHandler?.Invoke();
    }
}
