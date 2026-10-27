using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuse the book's UV mapping for printed ink, including TMP's separate text meshes.
public sealed class BookPagePrintedGraphic : BaseMeshEffect, ICanvasRaycastFilter
{
    internal BookPageBlockSurface Surface;
    internal RectTransform RegionRect;
    internal Rect RegionUV;
    internal bool RightPage;
    internal int PageIndex = -1;
    private TMP_Text text;
    private Vector3[][] sourceVertices;

    protected override void OnEnable()
    {
        base.OnEnable();
        text = GetComponent<TMP_Text>();
        if (text != null) text.OnPreRenderText += ProjectText;
    }

    protected override void OnDisable()
    {
        if (text != null) text.OnPreRenderText -= ProjectText;
        base.OnDisable();
    }

    private Vector3 Project(Vector3 vertex)
    {
        return transform.InverseTransformPoint(Surface.MapPrintedPoint(PageUV(vertex), RightPage, PageIndex));
    }

    private Vector2 PageUV(Vector3 vertex)
    {
        Vector3 local = RegionRect.InverseTransformPoint(transform.TransformPoint(vertex));
        Rect rect = RegionRect.rect;
        return RegionUV.min + Vector2.Scale(RegionUV.size,
            new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Surface == null || RegionRect == null || text != null) return;
        var triangles = new System.Collections.Generic.List<UIVertex>();
        vh.GetUIVertexStream(triangles);
        vh.Clear();
        for (int i = 0; i < triangles.Count; i += 3)
            ProjectTriangle(vh, triangles[i], triangles[i + 1], triangles[i + 2], 0);
    }

    private void ProjectTriangle(VertexHelper vh, UIVertex a, UIVertex b, UIVertex c, int depth)
    {
        // A four-corner card cuts through a curved sheet. Match the turning paper's sampling density.
        float ab = EdgeLength(a, b), bc = EdgeLength(b, c), ca = EdgeLength(c, a);
        if (depth < 12 && Mathf.Max(ab, Mathf.Max(bc, ca)) > 1)
        {
            if (ab >= bc && ab >= ca)
            { var middle = Midpoint(a, b); ProjectTriangle(vh, a, middle, c, depth + 1); ProjectTriangle(vh, middle, b, c, depth + 1); }
            else if (bc >= ca)
            { var middle = Midpoint(b, c); ProjectTriangle(vh, a, b, middle, depth + 1); ProjectTriangle(vh, a, middle, c, depth + 1); }
            else
            { var middle = Midpoint(c, a); ProjectTriangle(vh, a, b, middle, depth + 1); ProjectTriangle(vh, middle, b, c, depth + 1); }
            return;
        }
        int start = vh.currentVertCount;
        a.position = Project(a.position); b.position = Project(b.position); c.position = Project(c.position);
        vh.AddVert(a); vh.AddVert(b); vh.AddVert(c); vh.AddTriangle(start, start + 1, start + 2);
    }

    private float EdgeLength(UIVertex a, UIVertex b)
    {
        Vector2 delta = PageUV(a.position) - PageUV(b.position);
        return Mathf.Max(Mathf.Abs(delta.x) * 24, Mathf.Abs(delta.y) * 8);
    }

    private static UIVertex Midpoint(UIVertex a, UIVertex b)
    {
        a.position = (a.position + b.position) * .5f;
        a.color = Color32.Lerp(a.color, b.color, .5f);
        a.uv0 = (a.uv0 + b.uv0) * .5f;
        a.uv1 = (a.uv1 + b.uv1) * .5f;
        a.uv2 = (a.uv2 + b.uv2) * .5f;
        a.uv3 = (a.uv3 + b.uv3) * .5f;
        return a;
    }

    private void ProjectText(TMP_TextInfo info)
    {
        if (Surface == null || RegionRect == null) return;
        sourceVertices = new Vector3[info.materialCount][];
        for (int m = 0; m < info.materialCount; m++)
        {
            sourceVertices[m] = new Vector3[info.meshInfo[m].vertexCount];
            System.Array.Copy(info.meshInfo[m].vertices, sourceVertices[m], sourceVertices[m].Length);
            for (int v = 0; v < info.meshInfo[m].vertexCount; v++)
                info.meshInfo[m].vertices[v] = Project(sourceVertices[m][v]);
        }
    }

    internal void RefreshProjection()
    {
        if (text == null || Surface == null || RegionRect == null) return;
        if (sourceVertices == null) { text.ForceMeshUpdate(); return; }
        var info = text.textInfo;
        for (int m = 0; m < sourceVertices.Length; m++)
            for (int v = 0; v < sourceVertices[m].Length; v++)
                info.meshInfo[m].vertices[v] = Project(sourceVertices[m][v]);
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    public bool IsRaycastLocationValid(Vector2 point, Camera camera) =>
        Surface == null || Surface.HitRegion(point, camera, RegionUV, RightPage);
}
