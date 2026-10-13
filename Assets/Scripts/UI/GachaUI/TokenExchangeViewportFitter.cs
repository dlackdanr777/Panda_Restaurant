using UnityEngine;

/// <summary>
/// Fits only the dedicated outer container. Its RectTransform size is the authored
/// composition area; all child positions, anchors, scales and animation poses remain editable.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class TokenExchangeViewportFitter : MonoBehaviour
{
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private RectTransform _content;
    [SerializeField, Range(.1f, 1f)] private float _viewportFraction = .94f;
    private Vector2 _lastViewport, _lastContent;
    private float _lastFraction = -1;

    public bool ValidateLayout(out string error)
    {
        if (_viewport == null || _viewport != transform)
        { error = "TokenExchangeViewportFitter._viewport must reference this view root"; return false; }
        if (_content == null || _content == _viewport || _content.parent != _viewport)
        { error = "TokenExchangeViewportFitter._content must reference this view's direct outer container"; return false; }
        error = null;
        return true;
    }

    public void Refresh()
    {
        if (!ValidateLayout(out _)) return;
        Vector2 viewport = _viewport.rect.size, content = _content.rect.size;
        if (viewport.x <= 0 || viewport.y <= 0 || content.x <= 0 || content.y <= 0) return;
        if (viewport == _lastViewport && content == _lastContent && _viewportFraction == _lastFraction) return;
        _lastViewport = viewport; _lastContent = content; _lastFraction = _viewportFraction;
        float fraction = float.IsNaN(_viewportFraction) || float.IsInfinity(_viewportFraction)
            ? 1 : Mathf.Clamp(_viewportFraction, .1f, 1);
        float scale = Mathf.Min(viewport.x / content.x, viewport.y / content.y) * fraction;
        _content.localScale = Vector3.one * scale;
    }

    private void OnEnable() { _lastFraction = -1; Refresh(); }
    private void OnRectTransformDimensionsChange() => Refresh();
    // Also detects Inspector changes to the composition's size. An unchanged viewport
    // does not rewrite transforms, and animation never changes the size used for fitting.
    private void LateUpdate() => Refresh();
}
