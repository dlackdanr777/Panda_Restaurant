using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UITweenFillAmountImage : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _fillAmountImage;


    [Space]
    [Header("Anime Options")]
    [SerializeField] private float _duration;
    [SerializeField] private Ease _tweenMode;


    public void SetFillAmonut(float value)
    {
        _fillAmountImage.TweenStop();
        _fillAmountImage.TweenFillAmount(value, _duration, _tweenMode);
    }

    public void SetFillAmountNoAnime(float value)
    {
        _fillAmountImage.TweenStop();
        _fillAmountImage.fillAmount = value;
    }

}
