using Muks.Tween;
using System;
using UnityEngine;
using UnityEngine.UI;

public class GachaCapsule : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private RectTransform _rectTransform;  
    [SerializeField] private Image _image;
    [SerializeField] private Image _upperCapsuleImage;
    [SerializeField] private Image _lowerCapsuleImage;

    private static readonly int IdleState = Animator.StringToHash("Base Layer.Capsule_Idle");
    private static readonly int OpenState = Animator.StringToHash("Base Layer.Capsule_Open");
    private bool _isOpening;
    // Typed, read-only bindings for the staff-only retained-result layout. Item
    // presentation continues to use this component's unchanged authored geometry.
    internal Image PresentationImage => _image;
    internal Image UpperCapsuleImage => _upperCapsuleImage;
    internal Image LowerCapsuleImage => _lowerCapsuleImage;
    public bool IsMoving { get; private set; }
    public bool IsOpenComplete => _isOpening && _animator != null && _animator.enabled &&
        gameObject.activeInHierarchy && !_animator.IsInTransition(0) &&
        _animator.GetCurrentAnimatorStateInfo(0).fullPathHash == OpenState &&
        _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;

    /// <summary>Bind a fixed result and reset the existing capsule before it can be displayed.</summary>
    public void PreparePresentation(Sprite sprite, Capsule color, Vector2 startPosition)
    {
        if (_animator == null || _rectTransform == null || _image == null ||
            _upperCapsuleImage == null || _lowerCapsuleImage == null || color == null ||
            _animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("캡슐 연출 연결을 확인할 수 없습니다.");

        CancelPresentation();
        SetAnchoredPosition(startPosition);
        SetCapsuleColor(color);
        SetSprite(sprite);
        // The item capsule in the product scene was serialized with this Animator disabled.
        _animator.enabled = true;
        gameObject.SetActive(true);
        _animator.Rebind();
        _animator.ResetTrigger("Open");
        _animator.Play(IdleState, 0, 0f);
        _animator.Update(0f);
        // Rebind may restore serialized bindings; the committed result owns the displayed sprite.
        SetCapsuleColor(color);
        SetSprite(sprite);
    }

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
        TweenStop();
        IsMoving = true;
        _rectTransform.TweenAnchoredPosition(targetPosition, duration, easeType)
            .OnComplete(() => IsMoving = false);
    }

    public void TweenStop()
    {
        if (_rectTransform != null) _rectTransform.TweenStop();
        IsMoving = false;
    }

    public void StartOpen()
    {
        if (IsMoving) throw new InvalidOperationException("캡슐 이동 완료 후 열어야 합니다.");
        _animator.enabled = true;
        _animator.ResetTrigger("Open");
        _animator.Play(OpenState, 0, 0f);
        _animator.Update(0f);
        _isOpening = true;
    }

    public void CancelPresentation()
    {
        TweenStop();
        _isOpening = false;
        if (_animator != null && _animator.isInitialized) _animator.ResetTrigger("Open");
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        TweenStop();
        _isOpening = false;
    }
}
