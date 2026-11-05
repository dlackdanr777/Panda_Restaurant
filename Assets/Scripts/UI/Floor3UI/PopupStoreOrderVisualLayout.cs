using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Layout-only order content inside an approved speech-frame rectangle. Keeps the original icon and progress bindings.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PopupStoreOrderVisualLayout : MonoBehaviour
{
    [SerializeField] private RectTransform _contentArea;
    [SerializeField, Min(0)] private float _spacing = 14;
    [SerializeField] private Vector2 _maxIconSize = new Vector2(84, 84);

    public RectTransform ContentArea => _contentArea;

    public bool Apply(IReadOnlyList<Image> icons) => Apply(icons, Vector2.zero);

    public bool Apply(IReadOnlyList<Image> icons, Vector2 framePadding)
    {
        if (!(transform is RectTransform frame) || _contentArea == null || _contentArea.parent != transform
            || icons == null || !Finite(_spacing) || _spacing < 0
            || !Finite(framePadding.x) || !Finite(framePadding.y) || framePadding.x < 0 || framePadding.y < 0
            || !Positive(_maxIconSize.x) || !Positive(_maxIconSize.y)) return false;
        var shown = new List<Image>(5);
        foreach (var icon in icons)
        {
            if (icon == null || icon.transform.parent != transform) return false;
            if (icon.gameObject.activeSelf && icon.sprite != null) shown.Add(icon);
        }
        if (shown.Count == 0) return true;
        var corners = new Vector3[4];
        _contentArea.GetWorldCorners(corners);
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var world in corners)
        {
            Vector2 local = frame.InverseTransformPoint(world);
            minimum = Vector2.Min(minimum, local);
            maximum = Vector2.Max(maximum, local);
        }
        // Inspector frame-size edits must remain safe even when the authored content rectangle
        // was not resized with them. The approved normal layout already lies within this limit.
        minimum = Vector2.Max(minimum, frame.rect.min + framePadding);
        maximum = Vector2.Min(maximum, frame.rect.max - framePadding);
        Vector2 area = maximum - minimum;
        float usableWidth = area.x - _spacing * (shown.Count - 1);
        if (!Positive(usableWidth) || !Positive(area.y)) return false;
        float scale = Mathf.Min(1, usableWidth / (_maxIconSize.x * shown.Count), area.y / _maxIconSize.y);
        Vector2 size = _maxIconSize * scale;
        float width = size.x * shown.Count + _spacing * (shown.Count - 1);
        Vector2 center = (minimum + maximum) * .5f;
        for (int i = 0; i < shown.Count; i++)
        {
            var rect = shown[i].rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = center - frame.rect.center
                + new Vector2(-width * .5f + size.x * .5f + i * (size.x + _spacing), 0);
            var separator = rect.Find("Order Separator") as RectTransform;
            if (separator != null)
            {
                separator.anchorMin = separator.anchorMax = new Vector2(0, .5f);
                separator.pivot = new Vector2(.5f, .5f);
                separator.anchoredPosition = new Vector2(-_spacing * .5f, 0);
                separator.sizeDelta = new Vector2(_spacing, Mathf.Min(32, area.y));
                separator.gameObject.SetActive(i > 0 && _spacing > 0);
            }
        }
        return true;
    }

    private static bool Positive(float value) => Finite(value) && value > 0;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
