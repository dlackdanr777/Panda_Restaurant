using UnityEngine;
using UnityEngine.UI;

/// <summary>Customer-only clipping and authored body fitting. Never drives movement, order state, or a running entrance.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectMask2D))]
public sealed class PopupStoreCustomerVisualClip : MonoBehaviour
{
    [SerializeField] private Image _portrait;
    [SerializeField] private PopupStoreSpriteVisualBounds _bounds;
    [Tooltip("Unclipped target body area, directly under the same customer place as the order frame and gauge.")]
    [SerializeField] private RectTransform _bodyArea;
    [Tooltip("Optional existing complaint icon, kept directly under this original portrait.")]
    [SerializeField] private RectTransform _complaintIcon;
    [SerializeField] private Vector2 _complaintSize = new Vector2(56, 56);
    [SerializeField, Min(0)] private float _complaintHeadGap = 4;
    [SerializeField, HideInInspector] private Sprite _preparedSprite;

    public Image Portrait => _portrait;
    public RectTransform BodyArea => _bodyArea;

    public bool Owns(Image portrait, Transform place)
    {
        var popup = GetComponentInParent<UIPopupStore>(true);
        return portrait != null && portrait == _portrait && place != null && popup != null
            && transform.parent == place && portrait.transform.parent == transform
            && _bodyArea != null && _bodyArea.parent == place && _bodyArea != transform
            && _bodyArea != portrait.transform && _bounds != null
            && portrait.GetComponentInParent<UIPopupStore>(true) == popup
            && GetComponent<RectMask2D>() != null;
    }

    /// <summary>Called before a fresh entrance captures its home, or for the static inspection mode only.</summary>
    public bool Prepare() => Prepare(true);

    private bool Prepare(bool force)
    {
        if (!Owns(_portrait, transform.parent) || _portrait.sprite == null
            || !_bounds.TryGet(_portrait.sprite, out var opaque)
            || !(transform is RectTransform clip)) return false;
        // A repeated entrance must capture the authored/current home, including deliberate
        // anchor or pivot edits. Refit only when the source portrait changes; explicit authoring
        // calls above may reapply a changed approved body area before a session starts.
        if (!force && _preparedSprite == _portrait.sprite) return true;
        var corners = new Vector3[4];
        _bodyArea.GetWorldCorners(corners);
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var world in corners)
        {
            Vector2 local = clip.InverseTransformPoint(world);
            minimum = Vector2.Min(minimum, local);
            maximum = Vector2.Max(maximum, local);
        }
        Vector2 areaSize = maximum - minimum;
        Vector2 source = _portrait.sprite.rect.size;
        Vector2 visible = Vector2.Scale(source, opaque.size);
        if (areaSize.x <= 0 || areaSize.y <= 0 || visible.x <= 0 || visible.y <= 0) return false;
        float scale = Mathf.Min(areaSize.x / visible.x, areaSize.y / visible.y);
        Vector2 full = source * scale;
        Vector2 center = new Vector2((minimum.x + maximum.x) * .5f - (opaque.center.x - .5f) * full.x,
            minimum.y - (opaque.yMin - .5f) * full.y);
        var rect = _portrait.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = full;
        rect.anchoredPosition = center - clip.rect.center;
        _portrait.preserveAspect = true;
        _portrait.raycastTarget = false;
        if (_complaintIcon != null && _complaintIcon.parent == rect
            && Positive(_complaintSize.x) && Positive(_complaintSize.y)
            && !float.IsNaN(_complaintHeadGap) && !float.IsInfinity(_complaintHeadGap) && _complaintHeadGap >= 0)
        {
            _complaintIcon.anchorMin = _complaintIcon.anchorMax = _complaintIcon.pivot = new Vector2(.5f, .5f);
            _complaintIcon.sizeDelta = _complaintSize;
            _complaintIcon.anchoredPosition = new Vector2((opaque.center.x - .5f) * full.x,
                (opaque.yMax - .5f) * full.y + _complaintSize.y * .5f + _complaintHeadGap);
        }
        _preparedSprite = _portrait.sprite;
        return true;
    }

    public static bool Prepare(Image portrait)
    {
        var clip = portrait != null && portrait.transform.parent != null
            ? portrait.transform.parent.GetComponent<PopupStoreCustomerVisualClip>() : null;
        return clip == null || (clip._portrait == portrait && clip.Prepare(false));
    }

    private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
}
