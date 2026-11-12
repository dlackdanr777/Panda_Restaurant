using UnityEngine;
using UnityEngine.UI;

/// <summary>Resizes only the empty horizontal spans of an approved speech frame; corners and its central tail keep their aspect.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PopupStoreSpeechFrameVisual : BaseMeshEffect
{
    [SerializeField] private Sprite _sourceSprite;
    [SerializeField, Min(0)] private float _leftCornerPixels = 64;
    [SerializeField, Min(0)] private float _rightCornerPixels = 64;
    [SerializeField, Min(0)] private float _tailCenterPixels = 280.5f;
    [SerializeField, Min(0)] private float _tailWidthPixels = 82;

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices == null || vertices.currentVertCount == 0
            || !(graphic is Image image) || image.type != Image.Type.Simple
            || _sourceSprite == null || image.overrideSprite != _sourceSprite) return;
        if (_sourceSprite.packed && _sourceSprite.packingRotation != SpritePackingRotation.None) return;

        Vector2 sourceSize = _sourceSprite.rect.size;
        Rect target = image.GetPixelAdjustedRect();
        if (!Positive(sourceSize.x) || !Positive(sourceSize.y)
            || !Positive(target.width) || !Positive(target.height)
            || !Positive(_leftCornerPixels) || !Positive(_rightCornerPixels)
            || !Positive(_tailWidthPixels) || !Finite(_tailCenterPixels)) return;
        float tailLeft = _tailCenterPixels - _tailWidthPixels * .5f;
        float tailRight = _tailCenterPixels + _tailWidthPixels * .5f;
        float rightCornerStart = sourceSize.x - _rightCornerPixels;
        if (tailLeft <= _leftCornerPixels || tailRight >= rightCornerStart) return;

        float scale = target.height / sourceSize.y;
        float fixedWidth = (_leftCornerPixels + _tailWidthPixels + _rightCornerPixels) * scale;
        // Extremely narrow Inspector rectangles retain a uniformly fitted frame instead of
        // overlapping the protected tail and corners. Normal guide rectangles use five strips.
        bool fitWhole = target.width <= fixedWidth;
        if (fitWhole)
        {
            scale = Mathf.Min(target.width / sourceSize.x, scale);
            Vector2 size = sourceSize * scale;
            target = new Rect(target.center - size * .5f, size);
        }

        Vector4 outer = UnityEngine.Sprites.DataUtility.GetOuterUV(_sourceSprite);
        Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(_sourceSprite);
        Vector2 sampledSize = sourceSize - new Vector2(padding.x + padding.z, padding.y + padding.w);
        if (!Positive(sampledSize.x) || !Positive(sampledSize.y)) return;
        Vector2 uvPerPixel = new Vector2((outer.z - outer.x) / sampledSize.x,
            (outer.w - outer.y) / sampledSize.y);
        Vector2 uvOrigin = new Vector2(outer.x - padding.x * uvPerPixel.x,
            outer.y - padding.y * uvPerPixel.y);

        var template = new UIVertex();
        vertices.PopulateUIVertex(ref template, 0);
        vertices.Clear();
        if (fitWhole)
        {
            AddStrip(vertices, template, target.xMin, target.xMax, target.yMin, target.yMax,
                uvOrigin.x, uvOrigin.x + sourceSize.x * uvPerPixel.x,
                uvOrigin.y, uvOrigin.y + sourceSize.y * uvPerPixel.y);
            return;
        }

        float emptyLeft = tailLeft - _leftCornerPixels;
        float emptyRight = rightCornerStart - tailRight;
        float emptyWidth = target.width - fixedWidth;
        float leftEnd = target.xMin + _leftCornerPixels * scale;
        float tailStart = leftEnd + emptyWidth * emptyLeft / (emptyLeft + emptyRight);
        float tailEnd = tailStart + _tailWidthPixels * scale;
        float rightStart = target.xMax - _rightCornerPixels * scale;
        float[] source = { 0, _leftCornerPixels, tailLeft, tailRight, rightCornerStart, sourceSize.x };
        float[] destination = { target.xMin, leftEnd, tailStart, tailEnd, rightStart, target.xMax };
        for (int i = 0; i < 5; i++)
            AddStrip(vertices, template, destination[i], destination[i + 1], target.yMin, target.yMax,
                uvOrigin.x + source[i] * uvPerPixel.x, uvOrigin.x + source[i + 1] * uvPerPixel.x,
                uvOrigin.y, uvOrigin.y + sourceSize.y * uvPerPixel.y);
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
