using Muks.Tween;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Vector3 _targetScale;
    [SerializeField] private float _pressDuration;
    [SerializeField] private Ease _buttonDownTweenMode;
    [SerializeField] private Ease _buttonUpTweenMode;

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

    public void AddListener(Action onButtonUpEvent)
    {
        _onButtonUpEvent = onButtonUpEvent;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        gameObject.TweenStop();
        gameObject.transform.localScale = _tmpScale;
        gameObject.TweenScale(_targetScale, _pressDuration, _buttonDownTweenMode);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        gameObject.TweenStop();
        DebugLog.Log(gameObject.name + _tmpScale);
        gameObject.transform.localScale = _targetScale;
        gameObject.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
        _onButtonUpEvent?.Invoke();
    }
}
