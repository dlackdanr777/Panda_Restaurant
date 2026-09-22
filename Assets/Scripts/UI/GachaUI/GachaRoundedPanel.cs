using UnityEngine;
using UnityEngine.UI;

/// <summary>Small code-native rounded UI surface; uses the same canvas/masking rules as an Image.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GachaRoundedPanel : MaskableGraphic
{
    public float Radius = 18f;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * .5f);
        vh.AddVert(rect.center, color, new Vector2(.5f, .5f));
        const int steps = 6;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= steps; step++)
            {
                float angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(point, color, Vector2.zero);
            }
        }
        int count = 4 * (steps + 1);
        for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
    }
}
