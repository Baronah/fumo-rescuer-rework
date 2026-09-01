using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TrapezoidImage : Image
{
    [SerializeField]
    [Range(-1f, 1f)]
    private float topWidthOffset = 0.5f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        if (sprite == null)
        {
            base.OnPopulateMesh(vh);
            return;
        }

        vh.Clear();
        Rect rect = rectTransform.rect;
        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;
        float topOffset = halfWidth * topWidthOffset;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        // Define custom trapezoidal shape
        Vector3 v0 = new Vector3(-halfWidth, -halfHeight); // Bottom-left
        Vector3 v1 = new Vector3(halfWidth, -halfHeight);  // Bottom-right
        Vector3 v2 = new Vector3(halfWidth - topOffset, halfHeight); // Top-right
        Vector3 v3 = new Vector3(-halfWidth + topOffset, halfHeight); // Top-left

        // Assign UVs from base class to keep tiling & PPU behavior
        Vector2[] uvs = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

        // Add vertices
        vert.position = v0; vert.uv0 = uvs[0]; vh.AddVert(vert);
        vert.position = v1; vert.uv0 = uvs[1]; vh.AddVert(vert);
        vert.position = v2; vert.uv0 = uvs[2]; vh.AddVert(vert);
        vert.position = v3; vert.uv0 = uvs[3]; vh.AddVert(vert);

        // Create triangles (two for a quad)
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }
}
