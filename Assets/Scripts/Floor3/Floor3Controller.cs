using Muks.DataBind;
using Muks.Tween;
using UnityEngine;

public class Floor3Controller : MonoBehaviour
{
    [SerializeField] private SpriteTouchEvent _touchEvent;
    [SerializeField] private GameObject _tweenTarget;


    private void Start()
    {
        _touchEvent.AddDownEvent(OnTouchStart);
        _touchEvent.AddUpEvent(OnTouchEnd);
    }
    
    private void OnTouchStart()
    {
        _tweenTarget.TweenStop();
        _tweenTarget.transform.localScale = Vector3.one;
        _tweenTarget.TweenScale(Vector3.one * 0.9f, 0.15f, Ease.OutBack);
    }

    private void OnTouchEnd()
    {
        _tweenTarget.TweenStop();
        _tweenTarget.transform.localScale = Vector3.one * 0.9f;
        _tweenTarget.TweenScale(Vector3.one * 1f, 0.15f, Ease.OutBack);
        DataBind.GetUnityActionValue("ShowFloor3UI")?.Invoke();
    }

}
