using Muks.Tween;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FillAmountButton : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _image;


    [Space]
    [Header("Anime Options")]
    [SerializeField] private float _duration;
    [SerializeField] private Ease _tweenMode;


    public void AddListener(UnityAction action)
    {
        _button.onClick.AddListener(action);
        
    }

    public void RemoveAllListeners()
    {
        _button.onClick.RemoveAllListeners();
    }


    public void SetFillAmonut(float value)
    {
        _image.TweenStop();
        _image.TweenFillAmount(value, _duration, _tweenMode);
    }

    public void SetFillAmountNoAnime(float value)
    {
        value = Mathf.Clamp(value, 0, 1);
        _image.fillAmount = value;
    }

}
