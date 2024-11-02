using Muks.Tween;
using System;
using UnityEngine;


public class LoopTweenAnchoredPosition : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _target;

    [Space]
    [Header("Animation Option")]
    [SerializeField] private Vector2 _movePos;
    [SerializeField] private float _duration;
    [SerializeField] private Ease _ease;

    private Vector2 _startPos;
    private TweenData _tweenData;

    public void Awake()
    {
        if(_target == null)
            _target = GetComponent<RectTransform>();

        _startPos = _target.anchoredPosition;
    }

    public void OnEnable()
    {
        _tweenData?.Clear();

        _target.anchoredPosition = _startPos;
        _tweenData = _target.TweenAnchoredPosition(_startPos + _movePos, _duration, _ease);
        _tweenData.Loop(LoopType.Yoyo);
    }
}
