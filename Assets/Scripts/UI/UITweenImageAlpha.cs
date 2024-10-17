using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UITweenImageAlpha : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private float _targetAlpha;
    [SerializeField] private float _duration;
    [SerializeField] private Ease _ease;

    private float _startAlpha;
    private TweenData _tweenData;
    private void Awake()
    {
        _startAlpha = _image.color.a;
    }


    private void OnEnable()
    {
        if (_tweenData != null)
            _tweenData.TweenStop();

        _image.color = new Color(_image.color.r, _image.color.g, _image.color.b, _startAlpha);
        _tweenData = _image.TweenAlpha(_targetAlpha, _duration, _ease);
        _tweenData.Loop(LoopType.Yoyo);
    }


    private void OnDisable()
    {
        if(_tweenData != null)
            _tweenData.TweenStop();
    }
}
