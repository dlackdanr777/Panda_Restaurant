using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Popup-local guide placement with a cover background and background-anchored controls.</summary>
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
        [Serializable]
        public sealed class AspectYPosition
        {
            public float AspectRatio;
            public float Y;
        }

        public RectTransform Target;
        [Tooltip("Absolute source-guide rectangle, with a top-left origin. Targets must be direct visual groups of this popup.")]
        public Rect GuideRect;
        [Tooltip("Expand proportionally to cover the popup viewport.")]
        public bool CoverParent;
        [Tooltip("Keep this target horizontally centered on the cover background and use its aspect-specific Y position.")]
        public bool FollowBackground;
        [Tooltip("Normalized horizontal position inside the background image.")]
        public float BackgroundAnchorX = .5f;
        [Tooltip("Aspect-ratio/Y pairs, sorted by aspect ratio; values between entries are interpolated.")]
        public AspectYPosition[] AspectYPositions = Array.Empty<AspectYPosition>();
    }

    [SerializeField] private Entry[] _placements = Array.Empty<Entry>();
    private bool _applying;

    public bool ApplyLayout()
    {
        if (_applying || !(transform is RectTransform root) || _placements == null
            || !Positive(root.rect.width) || !Positive(root.rect.height)) return false;
        var unique = new HashSet<RectTransform>();
        int coverParentCount = 0;
        bool hasBackgroundFollower = false;
        float rootAspect = root.rect.width / root.rect.height;
        if (!Positive(rootAspect)) return false;
        foreach (var entry in _placements)
        {
            if (entry == null || entry.Target == null || entry.Target.parent != transform
                || !unique.Add(entry.Target) || !Finite(entry.GuideRect.x) || !Finite(entry.GuideRect.y)
                || !Positive(entry.GuideRect.width) || !Positive(entry.GuideRect.height)
                || (entry.CoverParent && entry.FollowBackground)) return false;
            if (entry.CoverParent)
            {
                coverParentCount++;
                var image = entry.Target.GetComponent<Image>();
                if (image == null || image.sprite == null || !Positive(image.sprite.rect.width)
                    || !Positive(image.sprite.rect.height)) return false;
            }
            if (entry.FollowBackground)
            {
                hasBackgroundFollower = true;
                if (!Finite(entry.BackgroundAnchorX) || entry.BackgroundAnchorX < 0 || entry.BackgroundAnchorX > 1
                    || entry.AspectYPositions == null || entry.AspectYPositions.Length < 2) return false;
                for (int i = 0; i < entry.AspectYPositions.Length; i++)
                {
                    var position = entry.AspectYPositions[i];
                    if (position == null || !Positive(position.AspectRatio) || !Finite(position.Y)
                        || (i > 0 && position.AspectRatio <= entry.AspectYPositions[i - 1].AspectRatio)) return false;
                }
            }
        }
        if (coverParentCount > 1 || (hasBackgroundFollower && coverParentCount != 1)) return false;

        _applying = true;
        try
        {
            float scale = Mathf.Min(root.rect.width / GuideWidth, root.rect.height / GuideHeight);
            RectTransform background = null;
            foreach (var entry in _placements)
            {
                if (!entry.CoverParent) continue;
                var rect = entry.Target;
                var spriteSize = rect.GetComponent<Image>().sprite.rect.size;
                float aspect = spriteSize.x / spriteSize.y;
                float width = Mathf.Max(root.rect.width, root.rect.height * aspect);
                float height = Mathf.Max(root.rect.height, root.rect.width / aspect);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(width, height);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                rect.anchoredPosition = Vector2.zero;
                background = rect;
            }
            foreach (var entry in _placements)
            {
                if (entry.CoverParent) continue;
                var rect = entry.Target;
                Vector2 center = entry.GuideRect.center;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = entry.GuideRect.size;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * scale;
                rect.anchoredPosition = entry.FollowBackground
                    ? new Vector2(background.anchoredPosition.x
                            + (entry.BackgroundAnchorX - .5f) * background.rect.width,
                        EvaluateAspectY(entry.AspectYPositions, rootAspect))
                    : new Vector2((center.x - GuideWidth * .5f) * scale,
                        (GuideHeight * .5f - center.y) * scale);
            }
            return true;
        }
        finally { _applying = false; }
    }

    private static float EvaluateAspectY(Entry.AspectYPosition[] positions, float aspect)
    {
        if (aspect <= positions[0].AspectRatio) return positions[0].Y;
        for (int i = 1; i < positions.Length; i++)
        {
            if (aspect > positions[i].AspectRatio) continue;
            var before = positions[i - 1];
            var after = positions[i];
            float t = (aspect - before.AspectRatio) / (after.AspectRatio - before.AspectRatio);
            return Mathf.Lerp(before.Y, after.Y, t);
        }
        return positions[positions.Length - 1].Y;
    }

    private static bool Positive(float value) => Finite(value) && value > 0;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnEnable() => ApplyLayout();
    private void OnRectTransformDimensionsChange() => ApplyLayout();
}
