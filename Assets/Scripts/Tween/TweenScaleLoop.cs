using Muks.Tween;
using UnityEngine;

public class TweenScaleLoop : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject _target;

    [Space]
    [Header("Animation Option")]
    [SerializeField] private float _startDelay;
    [SerializeField] private Vector3 _firstStartScale;
    [SerializeField] private Vector3 _firstTargetScale;
    [SerializeField] private float _firstDuration;
    [SerializeField] private Ease _firstEase;

    [Space]
    [SerializeField] private float _middleDuration;

    [Space]
    [SerializeField] private Vector3 _lastStartScale;
    [SerializeField] private Vector3 _lastTargetScale;
    [SerializeField] private float _lastDuration;
    [SerializeField] private Ease _lastEase;
    [SerializeField] private float _loopDelay;

    private TweenData _tweenData;

    public void OnEnable()
    {
        _target.TweenStop();
        _target.transform.localScale = _firstTargetScale;
        Invoke("TweenStart", _startDelay);
    }

    private void TweenStart()
    {
        if (!_target.activeInHierarchy)
            return;

        _target.transform.localScale = _firstStartScale;
        _tweenData = _target.TweenScale(_firstTargetScale, _firstDuration, _firstEase);
        _tweenData.OnComplete(() =>
        {
            _tweenData = _target.TweenScale(_firstTargetScale, _middleDuration, _firstEase).OnComplete(() =>
            {
                _target.transform.localScale = _lastStartScale;
                _tweenData = _target.TweenScale(_lastTargetScale, _lastDuration, _lastEase);
                _tweenData.OnComplete(() =>
                {
                    _tweenData = _target.TweenScale(_lastTargetScale, _loopDelay, _lastEase).OnComplete(TweenStart);
                });
            });

        });
    }
}
