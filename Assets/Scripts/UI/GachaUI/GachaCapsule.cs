using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class GachaCapsule : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private RectTransform _rectTransform;  
    [SerializeField] private Image _image;
    [SerializeField] private Image _upperCapsuleImage;
    [SerializeField] private Image _lowerCapsuleImage;

    public void SetSprite(Sprite sprite)
    {
        _image.sprite = sprite;
    }

    public void SetCapsuleColor(Capsule capsule)
    {
        _upperCapsuleImage.sprite = capsule.UpperCapsule;
        _lowerCapsuleImage.sprite = capsule.LowerCapsule;
    }


    public void SetAnchoredPosition(Vector2 position)
    {
        _rectTransform.anchoredPosition = position;
    }

    public void TweenAnchoredPosition(Vector2 targetPosition, float duration, Ease easeType)
    {
        _rectTransform.TweenAnchoredPosition(targetPosition, duration, easeType);
    }

    public void TweenStop()
    {
        _rectTransform.TweenStop();
    }

    public void StartOpen()
    {
        _animator.SetTrigger("Open");
    }
}
