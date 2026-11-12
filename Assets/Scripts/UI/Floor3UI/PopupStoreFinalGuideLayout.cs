using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One popup-local, uniform conversion from the approved 1672 by 941 guide to direct visual groups.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PopupStoreFinalGuideLayout : MonoBehaviour
{
    public const float GuideWidth = 1672;
    public const float GuideHeight = 941;

    [Serializable]
    public sealed class Entry
    {
        public RectTransform Target;
        [Tooltip("Absolute source-guide rectangle, with a top-left origin. Targets must be direct visual groups of this popup.")]
        public Rect GuideRect;
    }

    [SerializeField] private Entry[] _placements = Array.Empty<Entry>();
    private bool _applying;

    public bool ApplyLayout()
    {
        if (_applying || !(transform is RectTransform root) || _placements == null
            || !Positive(root.rect.width) || !Positive(root.rect.height)) return false;
        var unique = new HashSet<RectTransform>();
        foreach (var entry in _placements)
            if (entry == null || entry.Target == null || entry.Target.parent != transform
                || !unique.Add(entry.Target) || !Finite(entry.GuideRect.x) || !Finite(entry.GuideRect.y)
                || !Positive(entry.GuideRect.width) || !Positive(entry.GuideRect.height)) return false;

        _applying = true;
        try
        {
            float scale = Mathf.Min(root.rect.width / GuideWidth, root.rect.height / GuideHeight);
            foreach (var entry in _placements)
            {
                var rect = entry.Target;
                Vector2 center = entry.GuideRect.center;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = entry.GuideRect.size;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * scale;
                rect.anchoredPosition = new Vector2((center.x - GuideWidth * .5f) * scale,
                    (GuideHeight * .5f - center.y) * scale);
            }
            return true;
        }
        finally { _applying = false; }
    }

    private static bool Positive(float value) => Finite(value) && value > 0;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnEnable() => ApplyLayout();
    private void OnRectTransformDimensionsChange() => ApplyLayout();
}
