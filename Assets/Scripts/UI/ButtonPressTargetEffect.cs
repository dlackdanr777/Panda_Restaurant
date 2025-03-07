using Muks.Tween;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPressTargetEffect : ButtonPressEffect
{
    [Space]
    [Header("Target")]
    [SerializeField] private Transform _target;


    public override void Awake()
    {
        _tmpScale = _target.localScale;
    }

    public override void OnEnable()
    {
        _target.localScale = _tmpScale;
    }

    public override void ResetScale()
    {
        _target.TweenStop();
        _target.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        _target.TweenStop();
        _target.localScale = _tmpScale;
        _target.TweenScale(_targetScale, _pressDuration, _buttonDownTweenMode);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if (!Interactable)
            return;

        _target.TweenStop();
        _target.localScale = _targetScale;
        _target.TweenScale(_tmpScale, _pressDuration, _buttonUpTweenMode);
        _onButtonClickEvent?.Invoke();
    }

}
