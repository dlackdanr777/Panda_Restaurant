using Muks.Tween;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpritePressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private bool _isMaintainScale = false;
    [SerializeField] private Vector3 _targetScale;
    [SerializeField] private float _pressDuration;
    [SerializeField] private Ease _buttonDownTweenMode;
    [SerializeField] private Ease _buttonUpTweenMode;

    public bool Interactable = true;
    private Action _onPointerUpEvent;
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
        _onPointerUpEvent = onButtonUpEvent;
    }

    public void RemoveAllListeners()
    {
        _onPointerUpEvent = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        gameObject.TweenStop();

        Vector3 targetScale = _isMaintainScale ? Vector3.Scale(_tmpScale, _targetScale) : _targetScale;

        gameObject.transform.localScale =_tmpScale;
        gameObject.TweenScale(targetScale, _pressDuration, _buttonDownTweenMode);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        gameObject.TweenStop();

        Vector3 targetScale = _isMaintainScale ? Vector3.Scale(_tmpScale, _targetScale) : _targetScale;
        gameObject.transform.localScale = targetScale;
        gameObject.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
        _onPointerUpEvent?.Invoke();
    }
}
