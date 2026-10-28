using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Reuse the book's UV mapping for printed ink, including TMP's separate text meshes.
public sealed class BookPagePrintedGraphic : BaseMeshEffect, ICanvasRaycastFilter
{
    internal BookPageBlockSurface Surface;
    internal RectTransform RegionRect;
    internal Rect RegionUV;
    internal bool RightPage;
    internal int PageIndex = -1;
    private TMP_Text text;
    private BookPageBlockSurface.InkPoint[][] textPoints;
    private readonly List<BookPageBlockSurface.InkPoint> points = new List<BookPageBlockSurface.InkPoint>();
    private readonly List<Vector3> projected = new List<Vector3>();
    private readonly List<UIVertex> source = new List<UIVertex>();
    private readonly List<UIVertex> vertices = new List<UIVertex>();
    private readonly List<int> indices = new List<int>();
    private readonly Dictionary<(Vector3, Vector4, Color32), int> unique = new Dictionary<(Vector3, Vector4, Color32), int>();
    private int geometrySignature;
    private Mesh projectedMesh;
    private Matrix4x4 localToRegion, surfaceToLocal;
    private bool moving;
    private Vector2 regionOrigin, regionInverseSize;

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

    private Vector2 PageUV(Vector3 vertex)
    {
        Vector3 local = localToRegion.MultiplyPoint3x4(vertex);
        return RegionUV.min + Vector2.Scale(RegionUV.size,
            Vector2.Scale((Vector2)local - regionOrigin, regionInverseSize));
    }

    private void Frame()
    {
        localToRegion = RegionRect.worldToLocalMatrix * transform.localToWorldMatrix;
        surfaceToLocal = transform.worldToLocalMatrix * Surface.InkToWorld;
        moving = Surface.MovingInk(PageIndex);
        Rect rect = RegionRect.rect;
        regionOrigin = rect.min; regionInverseSize = new Vector2(1 / rect.width, 1 / rect.height);
    }

    private Vector3 Project(BookPageBlockSurface.InkPoint point)
        => surfaceToLocal.MultiplyPoint3x4(Surface.EvaluateInk(point, moving));

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Surface == null || RegionRect == null || text != null) return;
        // Invisible drag regions need a raycast quad, not thousands of curved, transparent vertices.
        if (graphic is Image && graphic.color.a == 0) return;
        Frame();
        source.Clear();
        vh.GetUIVertexStream(source);
        int signature = 17;
        unchecked { foreach (var vertex in source) { signature = signature * 31 + vertex.position.GetHashCode(); signature = signature * 31 + vertex.uv0.GetHashCode(); signature = signature * 31 + vertex.color.GetHashCode(); } }
        if (projectedMesh != null && signature == geometrySignature && vertices.Count == points.Count)
        {
            vh.Clear();
            for (int i = 0; i < vertices.Count; i++) { var vertex = vertices[i]; vertex.position = Project(points[i]); vertices[i] = vertex; projected[i] = vertex.position; vh.AddVert(vertex); }
            for (int i = 0; i < indices.Count; i += 3) vh.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
            vh.FillMesh(projectedMesh); return;
        }
        geometrySignature = signature;
        points.Clear(); projected.Clear(); vertices.Clear(); indices.Clear(); unique.Clear();
        vh.Clear();
        if (graphic is Image && source.Count % 6 == 0)
            for (int i = 0; i < source.Count; i += 6) ProjectQuad(vh, i);
        else for (int i = 0; i < source.Count; i += 3)
            ProjectTriangle(vh, source[i], source[i + 1], source[i + 2], 0);
        if (projectedMesh == null) { projectedMesh = new Mesh { name = "Cached printed ink" }; projectedMesh.MarkDynamic(); }
        vh.FillMesh(projectedMesh);
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
        AddTriangle(vh, AddVertex(vh, a), AddVertex(vh, b), AddVertex(vh, c));
    }

    private int AddVertex(VertexHelper vh, UIVertex vertex)
    {
        var key = (vertex.position, vertex.uv0, vertex.color);
        if (unique.TryGetValue(key, out int found)) return found;
        int index = points.Count; unique.Add(key, index);
        var point = Surface.BindInk(PageUV(vertex.position), RightPage);
        points.Add(point); vertex.position = Project(point); projected.Add(vertex.position); vertices.Add(vertex); vh.AddVert(vertex);
        return index;
    }

    private void AddTriangle(VertexHelper vh, int a, int b, int c)
    { vh.AddTriangle(a, b, c); indices.Add(a); indices.Add(b); indices.Add(c); }

    private void ProjectQuad(VertexHelper vh, int start)
    {
        UIVertex bl = source[start], br = bl, tl = bl, tr = bl;
        Vector2 min = bl.position, max = min;
        for (int i = start; i < start + 6; i++) { min = Vector2.Min(min, source[i].position); max = Vector2.Max(max, source[i].position); }
        for (int i = start; i < start + 6; i++)
        {
            var v = source[i];
            bool left = Mathf.Abs(v.position.x - min.x) < .001f, bottom = Mathf.Abs(v.position.y - min.y) < .001f;
            if (left) { if (bottom) bl = v; else tl = v; } else { if (bottom) br = v; else tr = v; }
        }
        Vector2 uvMin = PageUV(bl.position), uvMax = PageUV(tr.position);
        int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(uvMax.x - uvMin.x) * 24), 1, 24);
        int rows = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(uvMax.y - uvMin.y) * 8), 1, 8);
        // Rectangles share a small indexed grid instead of recursively duplicating every triangle.
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
        {
            float u0 = (float)x / columns, u1 = (float)(x + 1) / columns, v0 = (float)y / rows, v1 = (float)(y + 1) / rows;
            int a = AddVertex(vh, Lerp(Lerp(bl, tl, v0), Lerp(br, tr, v0), u0));
            int b = AddVertex(vh, Lerp(Lerp(bl, tl, v1), Lerp(br, tr, v1), u0));
            int c = AddVertex(vh, Lerp(Lerp(bl, tl, v1), Lerp(br, tr, v1), u1));
            int d = AddVertex(vh, Lerp(Lerp(bl, tl, v0), Lerp(br, tr, v0), u1));
            AddTriangle(vh, a, b, c); AddTriangle(vh, a, c, d);
        }
    }

    private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
    {
        a.position = Vector3.LerpUnclamped(a.position, b.position, t); a.color = Color32.Lerp(a.color, b.color, t);
        a.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t); a.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
        a.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t); a.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
        return a;
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
        Frame();
        if (textPoints == null || textPoints.Length != info.materialCount) textPoints = new BookPageBlockSurface.InkPoint[info.materialCount][];
        for (int m = 0; m < info.materialCount; m++)
        {
            int count = info.meshInfo[m].vertexCount;
            if (textPoints[m] == null || textPoints[m].Length != count) textPoints[m] = new BookPageBlockSurface.InkPoint[count];
            for (int v = 0; v < count; v++)
            {
                var point = Surface.BindInk(PageUV(info.meshInfo[m].vertices[v]), RightPage);
                textPoints[m][v] = point; info.meshInfo[m].vertices[v] = Project(point);
            }
        }
    }

    internal void RefreshProjection()
    {
        if (Surface == null || RegionRect == null || !isActiveAndEnabled) return;
        if (graphic is Image && graphic.color.a == 0) return;
        Frame();
        if (text != null)
        {
            if (textPoints == null) return; // The first canvas build binds the real glyph layout once.
            var info = text.textInfo;
            for (int m = 0; m < textPoints.Length && m < info.materialCount; m++)
            {
                if (textPoints[m] == null) continue;
                for (int v = 0; v < textPoints[m].Length; v++) info.meshInfo[m].vertices[v] = Project(textPoints[m][v]);
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
        else if (projectedMesh != null)
        {
            for (int v = 0; v < points.Count; v++) projected[v] = Project(points[v]);
            projectedMesh.SetVertices(projected);
            graphic.canvasRenderer.SetMesh(projectedMesh);
        }
    }

    protected override void OnDestroy()
    {
        if (projectedMesh != null) { if (Application.isPlaying) Destroy(projectedMesh); else DestroyImmediate(projectedMesh); }
        base.OnDestroy();
    }

    public bool IsRaycastLocationValid(Vector2 point, Camera camera) =>
        Surface == null || Surface.HitRegion(point, camera, RegionUV, RightPage);
}
