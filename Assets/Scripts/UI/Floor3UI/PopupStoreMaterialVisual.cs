using UnityEngine;
using UnityEngine.UI;

/// <summary>Fits the approved sprite's visible pixels inside its existing logical slot without changing input or source art.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PopupStoreMaterialVisual : BaseMeshEffect
{
    [SerializeField] private PopupStoreSpriteVisualBounds _bounds;

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices == null || vertices.currentVertCount == 0
            || !(graphic is Image image) || image.type != Image.Type.Simple || _bounds == null) return;
        var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
        if (!_bounds.TryGet(sprite, out var opaque)) return;

        var target = image.rectTransform.rect;
        if (target.width <= 0 || target.height <= 0) return;
        var vertex = new UIVertex();
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            minimum = Vector2.Min(minimum, vertex.position);
            maximum = Vector2.Max(maximum, vertex.position);
        }
        // Unity's simple Image mesh can already exclude Sprite importer padding. Recover the
        // full source rectangle before applying source-normalized measurements a single time.
        Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
        Vector2 spriteSize = sprite.rect.size;
        if (spriteSize.x <= 0 || spriteSize.y <= 0) return;
        Vector2 meshFraction = new Vector2(1 - (padding.x + padding.z) / spriteSize.x,
            1 - (padding.y + padding.w) / spriteSize.y);
        if (meshFraction.x <= 0 || meshFraction.y <= 0) return;
        Vector2 full = new Vector2((maximum.x - minimum.x) / meshFraction.x,
            (maximum.y - minimum.y) / meshFraction.y);
        minimum -= Vector2.Scale(full, new Vector2(padding.x / spriteSize.x, padding.y / spriteSize.y));
        Vector2 visibleSize = Vector2.Scale(full, opaque.size);
        if (visibleSize.x <= 0 || visibleSize.y <= 0) return;
        Vector2 visibleCenter = minimum + Vector2.Scale(full, opaque.center);
        float scale = Mathf.Min(target.width / visibleSize.x, target.height / visibleSize.y);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            Vector2 point = target.center + ((Vector2)vertex.position - visibleCenter) * scale;
            vertex.position = new Vector3(point.x, point.y, vertex.position.z);
            vertices.SetUIVertex(vertex, i);
        }
    }
}
