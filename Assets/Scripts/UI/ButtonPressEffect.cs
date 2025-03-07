using Muks.Tween;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] protected Vector3 _targetScale;
    [SerializeField] protected float _pressDuration;
    [SerializeField] protected Ease _buttonDownTweenMode;
    [SerializeField] protected Ease _buttonUpTweenMode;

    public bool Interactable = true;
    protected Action _onButtonClickEvent;
    protected Vector3 _tmpScale;

    public virtual void Awake()
    {
        _tmpScale = transform.localScale;
    }

    public virtual void OnEnable()
    {
        transform.localScale = _tmpScale;
    }

    public void AddListener(Action onButtonClickEvent)
    {
        _onButtonClickEvent = onButtonClickEvent;
    }

    public void RemoveAllListeners()
    {
        _onButtonClickEvent = null;
    }

    public virtual void ResetScale()
    {
        gameObject.TweenStop();
        gameObject.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        gameObject.TweenStop();
        gameObject.transform.localScale = _tmpScale;
        gameObject.TweenScale(_targetScale, _pressDuration, _buttonDownTweenMode);
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        gameObject.TweenStop();
        gameObject.transform.localScale = _targetScale;
        gameObject.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
        _onButtonClickEvent?.Invoke();
    }

}
