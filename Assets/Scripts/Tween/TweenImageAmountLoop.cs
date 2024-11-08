using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class TweenImageAmountLoop : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _target;

    [Space]
    [Header("Animation Option")]
    [SerializeField] private float _startDelay;
    [SerializeField] private float _startValue;
    [SerializeField] private float _targetValue;
    [SerializeField] private float _duration;
    [SerializeField] private Ease _ease;

    [Space]
    [SerializeField] private float _loopDuration;
    private TweenData _tweenData;

    public void OnEnable()
    {
        _target.TweenStop();
        _target.fillAmount = _startValue;
        Invoke("TweenStart", _startDelay);
    }

    private void TweenStart()
    {
        if (!_target.gameObject.activeInHierarchy)
            return;

        _target.fillAmount = _startValue;
        _tweenData = _target.TweenFillAmount(_targetValue, _duration, _ease);
        _tweenData.OnComplete(() =>
        {
            _tweenData = _target.TweenFillAmount(_targetValue, _loopDuration, _ease);
            _tweenData.OnComplete(TweenStart);
        });
    }
}
