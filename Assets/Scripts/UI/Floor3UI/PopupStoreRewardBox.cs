using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One authored, display-only image of the existing immutable result. Never awards a reward.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreRewardBox : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Sprite _cSprite, _bSprite, _aSprite, _sSprite, _ssSprite, _sssSprite;
    [SerializeField] private bool _showImage = true;
    [SerializeField] private bool _animate = true;
    [SerializeField, Min(0)] private int _siblingIndex = 1;
    [SerializeField, Min(.01f)] private float _baseScale = 1f;
    [SerializeField, Min(.01f)] private float _peakScale = 1.08f;
    [SerializeField, Min(0f)] private float _duration = .35f;
    [SerializeField] private AnimationCurve _scaleCurve = new AnimationCurve(
        new Keyframe(0, 0, 0, 0), new Keyframe(.4f, 1, 0, 0), new Keyframe(1, 0, 0, 0));

    private Image _displayedImage;
    private RectTransform _sizedTransform;
    private Vector3 _authoredScale;
    private PopupStoreGradeResult _animatedResult;
    private float _elapsed;
    private int _previousSibling;
    private string _lastError;
    public Image BoxImage => _image;
    public Sprite DisplayedSprite => IsVisible ? _displayedImage.sprite : null;
    public bool IsVisible => _displayedImage != null && _displayedImage.enabled && _displayedImage.gameObject.activeInHierarchy;
    public bool IsAnimating { get; private set; }
    public int AnimationCount { get; private set; }
    public PopupStoreGradeResult ShownResult { get; private set; }

    public bool ValidateMapping(out string error)
    {
        error = null;
        var sprites = new[] { _cSprite, _bSprite, _aSprite, _sSprite, _ssSprite, _sssSprite };
        for (int i = 0; i < sprites.Length; i++)
        {
            var grade = (PopupStoreGradeTier)(i + 1);
            var sprite = sprites[i];
            if (sprite == null || sprite.texture == null || sprite.rect.width <= 0 || sprite.rect.height <= 0)
            { error = "Missing approved sprite for " + grade + "."; return false; }
            // The six approved single-Sprite source names are an identity contract, not positional guesses.
            // Editor tests additionally verify GUID/path and manifest SHA-256 of each original PNG.
            if (!string.Equals(sprite.name, "PopupRewardBox_" + grade + "_APPROVED", StringComparison.Ordinal))
            { error = "Approved sprite name does not match grade " + grade + "."; return false; }
            for (int j = 0; j < i; j++)
                if (sprites[j] == sprite || sprites[j].texture == sprite.texture)
                { error = "Reward sprites must be six distinct approved images."; return false; }
        }
        return true;
    }

    public bool Present(PopupStoreGradeResult result)
    {
        var panel = GetComponent<PopupStoreResultPanel>();
        if (!isActiveAndEnabled || panel == null || !panel.IsVisible || result == null
            || !ReferenceEquals(panel.ShownResult, result) || !result.IsFinalized
            || (ShownResult != null && !ReferenceEquals(ShownResult, result)))
            return Reject("Reward image requires this panel's unchanged visible final result.");
        if (!Enum.IsDefined(typeof(PopupStoreGradeTier), result.Grade)) return Reject("Unknown final grade.");
        ShownResult = result;
        if (!_showImage || result.Grade == PopupStoreGradeTier.None) { HidePresentation(); return false; }
        if (!ValidateImage(panel, out string error)) return Reject(error);
        if (!ValidateMapping(out error))
        {
            _image.enabled = false; _image.sprite = null;
            return Reject(error);
        }
        if (!Finite(_baseScale) || _baseScale <= 0 || !Finite(_peakScale) || _peakScale < _baseScale
            || !Finite(_duration) || _duration < 0 || _scaleCurve == null || _scaleCurve.length == 0)
            return Reject("Reward scale and duration must be finite and valid.");
        foreach (var key in _scaleCurve.keys)
            if (!Finite(key.time) || !Finite(key.value)) return Reject("Reward curve keys must be finite.");
        var sprite = SpriteFor(result.Grade);
        // Repeated calls during one presentation must not restart an in-progress pulse.
        if (_displayedImage == _image && IsVisible && _image.sprite == sprite) return true;
        HidePresentation();
        if (_sizedTransform != _image.rectTransform)
        { _sizedTransform = _image.rectTransform; _authoredScale = _sizedTransform.localScale; }
        _lastError = null;
        _displayedImage = _image;
        _previousSibling = _image.transform.GetSiblingIndex();
        _image.transform.SetSiblingIndex(Mathf.Clamp(_siblingIndex, 0, _image.transform.parent.childCount - 1));
        _image.sprite = sprite;
        _image.preserveAspect = true;
        _image.raycastTarget = false;
        _image.enabled = true;
        _sizedTransform.localScale = _authoredScale * _baseScale;
        IsAnimating = _animate && _duration > 0 && !ReferenceEquals(_animatedResult, result);
        // A result is presented once even if animation is disabled or its first appearance is interrupted.
        if (!ReferenceEquals(_animatedResult, result))
        { _animatedResult = result; if (IsAnimating) AnimationCount++; }
#if UNITY_EDITOR
        if (Application.isPlaying)
            Debug.Log("POPUP_STORE_REWARD_SHOWN session=" + result.SessionId + " grade=" + result.Grade
                + " sprite=" + sprite.name + " animations=" + AnimationCount, this);
#endif
        return true;
    }

    private bool ValidateImage(PopupStoreResultPanel panel, out string error)
    {
        error = null;
        if (_image == null || !_image.transform.IsChildOf(transform) || _image.gameObject == gameObject
            || _image.GetComponentInParent<UIPopupStore>(true) != GetComponent<UIPopupStore>()
            || !panel.ContainsResultGraphic(_image))
        { error = "Reward image must be a dedicated child of this result panel."; return false; }
        return true;
    }

    private Sprite SpriteFor(PopupStoreGradeTier grade)
    {
        switch (grade)
        {
            case PopupStoreGradeTier.C: return _cSprite;
            case PopupStoreGradeTier.B: return _bSprite;
            case PopupStoreGradeTier.A: return _aSprite;
            case PopupStoreGradeTier.S: return _sSprite;
            case PopupStoreGradeTier.SS: return _ssSprite;
            case PopupStoreGradeTier.SSS: return _sssSprite;
            default: return null;
        }
    }

    public void TickPresentation(float unscaledDelta)
    {
        if (!IsAnimating) return;
        if (!Finite(unscaledDelta) || unscaledDelta < 0) { Reject("Reward delta must be finite and non-negative."); return; }
        if (!IsVisible) { HidePresentation(); return; }
        _elapsed = Mathf.Min(_duration, _elapsed + unscaledDelta);
        float t = _duration > 0 ? Mathf.Clamp01(_elapsed / _duration) : 1;
        if (t >= 1) { IsAnimating = false; _sizedTransform.localScale = _authoredScale * _baseScale; return; }
        float value = _scaleCurve.Evaluate(t);
        if (!Finite(value)) { Reject("Reward curve returned an invalid value."); return; }
        _sizedTransform.localScale = _authoredScale * Mathf.Lerp(_baseScale, _peakScale, Mathf.Clamp01(value));
    }

    public void HidePresentation()
    {
        IsAnimating = false; _elapsed = 0;
        if (_displayedImage != null)
        {
            _displayedImage.enabled = false; _displayedImage.sprite = null;
            _displayedImage.transform.SetSiblingIndex(_previousSibling);
        }
        var panel = GetComponent<PopupStoreResultPanel>();
        // Also hide an Inspector-enabled dedicated image before the first valid presentation or for None.
        if (panel != null && ValidateImage(panel, out _)) { _image.enabled = false; _image.sprite = null; }
        if (_sizedTransform != null) _sizedTransform.localScale = _authoredScale;
        _displayedImage = null;
    }

    private bool Reject(string error)
    {
        HidePresentation();
        // Never disable a substituted image: it might be the result's frame or confirmation button.
        if (_lastError != error) Debug.LogError("PopupStoreRewardBox: " + error, this);
        _lastError = error;
        return false;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnDisable() => HidePresentation();
    private void OnDestroy() => HidePresentation();
}
