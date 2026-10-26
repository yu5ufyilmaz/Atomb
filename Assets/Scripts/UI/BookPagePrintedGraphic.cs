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
        Vector3 local = RegionRect.InverseTransformPoint(transform.TransformPoint(vertex));
        Rect rect = RegionRect.rect;
        Vector2 uv = RegionUV.min + Vector2.Scale(RegionUV.size,
            new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
        Vector3 world = Surface.MapPrintedPoint(uv, RightPage, PageIndex);
        if (Camera.main != null) world += (Camera.main.transform.position - world).normalized * .0005f;
        return transform.InverseTransformPoint(world);
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Surface == null || RegionRect == null || text != null) return;
        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i); vertex.position = Project(vertex.position); vh.SetUIVertex(vertex, i);
        }
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
