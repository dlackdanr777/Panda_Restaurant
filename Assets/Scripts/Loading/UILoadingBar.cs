using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UILoadingBar : MonoBehaviour
{
    [SerializeField] private RectTransform _loadingBar;
    [SerializeField] private Image _gaugeBar;
    [SerializeField] private Image _iconImage;


    public void Init()
    {
        if(_iconImage != null)
        {
            _iconImage.transform.rotation = Quaternion.Euler(0, 0, 3);
            _iconImage.TweenRotate(new Vector3(0, 0, -3), 1.5f, Ease.Smoothstep).Loop(LoopType.Yoyo);
        }

    }

    public void SetFillAmount(float amount)
    {
        amount = Mathf.Clamp(amount, 0f, 1f);
        _gaugeBar.fillAmount = amount;

        if(_iconImage != null )
            _iconImage.rectTransform.anchoredPosition = new Vector2(_loadingBar.rect.width * amount, _iconImage.rectTransform.anchoredPosition.y);
    }
}
