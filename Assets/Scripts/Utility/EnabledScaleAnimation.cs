using Muks.Tween;
using System;
using UnityEngine;

public class EnabledScaleAnimation : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject _target;

    [Space]
    [Header("Animation Option")]
    [SerializeField] private Vector3 _startScale;
    [SerializeField] private Vector3 _targetScale;
    [SerializeField] private float _duration;
    [SerializeField] private Ease _ease;

    private TweenData _tweenData;

    private Action _onStarted;
    private Action _onUpdated;
    private Action _onCompleted;

    public void SetCallBack(Action onStart, Action onUpdate, Action onComple)
    {
        _onStarted = onStart;
        _onUpdated = onUpdate;
        _onCompleted = onComple;
    }

    public void OnEnable()
    {
        _tweenData?.Clear();

        gameObject.transform.localScale = _startScale;
        _tweenData = _target.TweenScale(_targetScale, _duration, _ease);
        _tweenData.OnStart(_onStarted);
        _tweenData.OnUpdate(_onUpdated);
        _tweenData.OnComplete(_onCompleted);
    }
}
