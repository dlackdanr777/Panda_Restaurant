using System;
using UnityEngine;

/// <summary>Read-only visual measurements of approved sprites. Source textures and catalog identities are unchanged.</summary>
[CreateAssetMenu(menuName = "Panda Restaurant/Popup Store/Sprite Visual Bounds")]
public sealed class PopupStoreSpriteVisualBounds : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public Sprite Sprite;
        [Tooltip("Opaque source bounds normalized to the sprite rectangle, with a bottom-left origin.")]
        public Rect OpaqueRect;
    }

    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

    public bool TryGet(Sprite sprite, out Rect bounds)
    {
        bounds = default;
        if (sprite == null || _entries == null) return false;
        foreach (var entry in _entries)
        {
            if (entry == null || entry.Sprite != sprite) continue;
            var rect = entry.OpaqueRect;
            if (!Finite(rect.x) || !Finite(rect.y) || !Finite(rect.width) || !Finite(rect.height)
                || rect.width <= 0 || rect.height <= 0 || rect.xMin < 0 || rect.yMin < 0
                || rect.xMax > 1.00001f || rect.yMax > 1.00001f) return false;
            bounds = rect;
            return true;
        }
        return false;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
