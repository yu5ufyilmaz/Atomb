using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Kalem girdisi ve çizgi tepeleri, çerçevelerle aynı gerçek sayfa UV haritasını kullanır.
public sealed class BookPageFreehandDrawer : NotebookFreehandDrawer, ICanvasRaycastFilter
{
    internal BookPageBlockSurface Surface;
    internal bool RightPage;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        Surface != null && Surface.TryGetPageUV(screenPoint, eventCamera, RightPage, out _);

    protected override bool TryGetLocalPoint(PointerEventData data, out Vector2 point)
    {
        point = Vector2.zero;
        if (Surface == null || !Surface.TryGetPageUV(data.position, data.pressEventCamera, RightPage, out Vector2 uv)) return false;
        point = rectTransform.rect.min + Vector2.Scale(uv, rectTransform.rect.size);
        return true;
    }

    protected override void ConfigureGraphic(MaskableGraphic graphic)
    {
        var projection = graphic.gameObject.AddComponent<BookPageStrokeProjection>();
        projection.Drawer = this;
    }
}

internal sealed class BookPageStrokeProjection : BaseMeshEffect
{
    internal BookPageFreehandDrawer Drawer;
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Drawer == null || Drawer.Surface == null) return;
        Rect rect = Drawer.rectTransform.rect;
        var vertex = new UIVertex();
        Camera camera = Camera.main;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            Vector2 uv = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y));
            Vector3 world = Drawer.Surface.MapPageWorld(uv, Drawer.RightPage);
            if (camera != null) world += (camera.transform.position - world).normalized * .0002f;
            vertex.position = transform.InverseTransformPoint(world);
            vh.SetUIVertex(vertex, i);
        }
    }
}
