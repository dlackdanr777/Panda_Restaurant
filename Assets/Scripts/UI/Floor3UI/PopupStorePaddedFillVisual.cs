using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies an existing horizontal fill amount to its visible content, excluding approved transparent padding.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PopupStorePaddedFillVisual : BaseMeshEffect
{
    [SerializeField, Min(0)] private float _paddingPixels = 6;
    [SerializeField, Min(0)] private float _capPixels = 32;

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices == null || !(graphic is Image image)
            || image.type != Image.Type.Filled || image.fillMethod != Image.FillMethod.Horizontal
            || image.fillOrigin != (int)Image.OriginHorizontal.Left) return;
        var sprite = image.overrideSprite;
        if (sprite == null || (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)) return;
        Vector2 size = sprite.rect.size;
        Rect outerRect = image.GetPixelAdjustedRect();
        if (!Positive(size.x) || !Positive(size.y) || !Positive(outerRect.width) || !Positive(outerRect.height)
            || !Finite(_paddingPixels) || _paddingPixels < 0 || !Positive(_capPixels)
            || _capPixels <= _paddingPixels || _capPixels * 2 >= size.x || _paddingPixels * 2 >= size.y) return;

        float scale = outerRect.height / size.y;
        float padding = _paddingPixels * scale;
        var content = new Rect(outerRect.xMin + padding, outerRect.yMin + padding,
            outerRect.width - padding * 2, outerRect.height - padding * 2);
        float cap = (_capPixels - _paddingPixels) * scale;
        if (!Positive(content.width) || !Positive(content.height) || content.width <= cap * 2) return;

        Vector4 outerUv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        Vector4 importedPadding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
        Vector2 sampledSize = size - new Vector2(importedPadding.x + importedPadding.z,
            importedPadding.y + importedPadding.w);
        if (!Positive(sampledSize.x) || !Positive(sampledSize.y)) return;
        Vector2 uvPerPixel = new Vector2((outerUv.z - outerUv.x) / sampledSize.x,
            (outerUv.w - outerUv.y) / sampledSize.y);
        Vector2 uvOrigin = new Vector2(outerUv.x - importedPadding.x * uvPerPixel.x,
            outerUv.y - importedPadding.y * uvPerPixel.y);
        var template = UIVertex.simpleVert;
        template.color = image.color;
        if (vertices.currentVertCount > 0) vertices.PopulateUIVertex(ref template, 0);
        vertices.Clear();
        float amount = Finite(image.fillAmount) ? Mathf.Clamp01(image.fillAmount) : 0;
        if (amount <= 0) return;

        float limit = content.xMin + content.width * amount;
        float[] source = { _paddingPixels, _capPixels, size.x - _capPixels, size.x - _paddingPixels };
        float[] destination = { content.xMin, content.xMin + cap, content.xMax - cap, content.xMax };
        float bottomUv = uvOrigin.y + _paddingPixels * uvPerPixel.y;
        float topUv = uvOrigin.y + (size.y - _paddingPixels) * uvPerPixel.y;
        for (int i = 0; i < 3; i++)
        {
            float left = destination[i];
            float right = Mathf.Min(destination[i + 1], limit);
            if (right <= left) break;
            float sourceRight = Mathf.Lerp(source[i], source[i + 1],
                (right - left) / (destination[i + 1] - left));
            AddStrip(vertices, template, left, right, content.yMin, content.yMax,
                uvOrigin.x + source[i] * uvPerPixel.x, uvOrigin.x + sourceRight * uvPerPixel.x,
                bottomUv, topUv);
        }
    }

    private static void AddStrip(VertexHelper mesh, UIVertex template, float left, float right,
        float bottom, float top, float u0, float u1, float v0, float v1)
    {
        int first = mesh.currentVertCount;
        template.position = new Vector3(left, bottom, template.position.z);
        template.uv0 = new Vector4(u0, v0, 0, 0); mesh.AddVert(template);
        template.position = new Vector3(left, top, template.position.z);
        template.uv0 = new Vector4(u0, v1, 0, 0); mesh.AddVert(template);
        template.position = new Vector3(right, top, template.position.z);
        template.uv0 = new Vector4(u1, v1, 0, 0); mesh.AddVert(template);
        template.position = new Vector3(right, bottom, template.position.z);
        template.uv0 = new Vector4(u1, v0, 0, 0); mesh.AddVert(template);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first + 2, first + 3, first);
    }

    private static bool Positive(float value) => Finite(value) && value > 0;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
