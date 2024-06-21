using UnityEngine;
using UnityEngine.EventSystems;
using System;
using Muks.Tween;

public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Vector3 _targetScale;
    [SerializeField] private float _pressDuration;
    [SerializeField] private TweenMode _buttonDownTweenMode;
    [SerializeField] private TweenMode _buttonUpTweenMode;

    private Action _onButtonUpEvent;
    private Vector3 _tmpScale;

    public void Awake()
    {
        _tmpScale = transform.localScale;
    }

    public void OnEnable()
    {
        transform.localScale = _tmpScale;
    }

    public void SetAction(Action onButtonUpEvent)
    {
        _onButtonUpEvent = onButtonUpEvent;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        gameObject.TweenStop();
        gameObject.TweenScale(_targetScale, _pressDuration, _buttonDownTweenMode);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        gameObject.TweenStop();
        gameObject.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
        _onButtonUpEvent?.Invoke();
    }
}
