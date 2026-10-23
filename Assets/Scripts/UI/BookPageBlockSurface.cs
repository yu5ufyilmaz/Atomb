using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Kartlar modelin sayfa yüzeyine bağlıdır; deftere aktarım aynı UI sürükleme yolunu kullanır.
[RequireComponent(typeof(InteractableBook))]
public class BookPageBlockSurface : MonoBehaviour
{
    private InteractableBook book;
    private Canvas surface;
    private int left = -1, right = -1;
    private Mesh bakedPages;
    private readonly List<PageTriangle> pageTriangles = new List<PageTriangle>();

    private struct PageTriangle
    {
        public bool right;
        public Vector2 a, b, c;
        public Vector3 pa, pb, pc;
        public Vector3 Map(Vector2 uv)
        {
            Vector2 ab = b - a, ac = c - a, ap = uv - a;
            float det = Cross(ab, ac);
            float v = Cross(ap, ac) / det, w = Cross(ab, ap) / det;
            return pa + (pb - pa) * v + (pc - pa) * w;
        }
        public bool Contains(Vector2 uv)
        {
            float sign = Mathf.Sign(Cross(b - a, c - a));
            return sign * Cross(b - a, uv - a) >= -0.000001f
                && sign * Cross(c - b, uv - b) >= -0.000001f
                && sign * Cross(a - c, uv - c) >= -0.000001f;
        }
    }

    private void Awake()
    {
        book = GetComponent<InteractableBook>();
        BookClueData.Register(book.clueData);
    }

    private void LateUpdate()
    {
        bool visible = book.isOpen && !book.isAnimating && book.usePhysicalPageBlocks
            && GameManager.Instance != null && ReferenceEquals(GameManager.Instance.activeInteraction, book)
            && !GameManager.Instance.isGamePaused
            && NotebookUI.Instance != null && NotebookUI.Instance.IsWorkspaceActive;
        if (!visible) { if (surface != null) surface.gameObject.SetActive(false); return; }
        if (surface == null) BuildSurface();
        if (surface == null) return;
        bool refresh = !surface.gameObject.activeSelf || left != book.LeftPageIndex || right != book.RightPageIndex;
        surface.gameObject.SetActive(true);
        if (refresh)
        {
            if (bakedPages != null) book.bookSkinnedMeshRenderer.BakeMesh(bakedPages, true);
            BuildPageMaps(book.bookSkinnedMeshRenderer, (RectTransform)surface.transform);
            RefreshPages();
        }
    }

    private void BuildSurface()
    {
        var mesh = book.bookSkinnedMeshRenderer;
        if (mesh == null) { Debug.LogError("Kitabın sayfa renderer referansı eksik.", this); enabled = false; return; }
        var obj = new GameObject("Kitap Sayfası Blokları", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        obj.transform.SetParent(mesh.transform, false);
        surface = obj.GetComponent<Canvas>();
        surface.renderMode = RenderMode.WorldSpace;
        surface.worldCamera = Camera.main;
        surface.sortingOrder = 1;
        var rect = (RectTransform)obj.transform;
        Bounds bounds = mesh.localBounds;
        // localBounds bir culling kutusudur; rig'li kitapta gerçek sayfa boyutunu vermeyebilir.
        if (mesh.sharedMesh != null)
        {
            bakedPages = new Mesh();
            // Rig'in ölçeğini dahil et: false bu modelde parent ölçeğini bir kez daha uygular.
            // true ile elde edilen renderer-yerel noktalar GPU'daki kemik dönüşümleriyle eşleşir.
            mesh.BakeMesh(bakedPages, true);
            bakedPages.RecalculateBounds();
            if (bakedPages.vertexCount > 0 && bakedPages.bounds.size.sqrMagnitude > 0.00000001f)
                bounds = bakedPages.bounds;
        }
        rect.sizeDelta = new Vector2(1000, 700);
        // Kartlar ve taşıma bandı için ana düzlem; UV çerçeveleri gerçek yüzeye ayrıca yansıtılır.
        float lift = Mathf.Max(bounds.size.y * 0.1f, bounds.size.x * 0.01f);
        rect.localPosition = new Vector3(bounds.center.x, bounds.max.y + lift, bounds.center.z);
        rect.localRotation = Quaternion.Euler(90, 0, 0);
        rect.localScale = new Vector3(bounds.size.x / 1000, bounds.size.z / 700, bounds.size.x / 1000);
        if (surface.worldCamera != null)
        {
            if (Vector3.Dot(rect.forward, surface.worldCamera.transform.forward) < 0)
                rect.localRotation *= Quaternion.Euler(0, 180, 0);
            if (Vector3.Dot(rect.up, surface.worldCamera.transform.up) < 0)
                rect.localRotation *= Quaternion.Euler(0, 0, 180);
        }
        BuildPageMaps(mesh, rect);
    }

    // Afin yaklaşım yerine her UV üçgeninin gerçek 3B yüzeyini sakla; sırt ve eğrilik korunur.
    private void BuildPageMaps(SkinnedMeshRenderer mesh, RectTransform rect)
    {
        pageTriangles.Clear();
        if (bakedPages == null || bakedPages.vertexCount == 0) return;
        var uvs = new List<Vector2>();
        bakedPages.GetUVs(0, uvs);
        Vector3[] vertices = bakedPages.vertices;
        if (uvs.Count != vertices.Length || bakedPages.subMeshCount == 0) return;
        int submesh = Mathf.Clamp(book.bookMaterialIndex, 0, bakedPages.subMeshCount - 1);
        int[] indices = bakedPages.GetTriangles(submesh);
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            bool rightPage = (uvs[a].x + uvs[b].x + uvs[c].x) / 3f >= 0.5f;
            Vector2 offset = rightPage ? Vector2.right : Vector2.zero;
            var triangle = new PageTriangle
            {
                right = rightPage,
                a = Vector2.Scale(uvs[a], new Vector2(2, 1)) - offset,
                b = Vector2.Scale(uvs[b], new Vector2(2, 1)) - offset,
                c = Vector2.Scale(uvs[c], new Vector2(2, 1)) - offset,
                pa = rect.InverseTransformPoint(mesh.transform.TransformPoint(vertices[a])),
                pb = rect.InverseTransformPoint(mesh.transform.TransformPoint(vertices[b])),
                pc = rect.InverseTransformPoint(mesh.transform.TransformPoint(vertices[c])),
            };
            if (Mathf.Abs(Cross(triangle.b - triangle.a, triangle.c - triangle.a)) > 0.00000001f)
                pageTriangles.Add(triangle);
        }
    }

    private Vector3 MapPagePoint(Vector2 uv, bool rightPage)
    {
        foreach (var triangle in pageTriangles)
            if (triangle.right == rightPage && triangle.Contains(uv)) return triangle.Map(uv);
        return new Vector3(uv.x * 500 + (rightPage ? 0 : -500), uv.y * 700 - 350, 0);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Görünür alan ve dört kenar aynı UV üçgenlerinden kırpılır; sayfanın yüksekliği kaybolmaz.
    internal void PopulateRegion(VertexHelper vh, Rect uv, bool rightPage, Transform target, Color tint)
    {
        vh.Clear();
        foreach (var triangle in pageTriangles)
        {
            if (triangle.right != rightPage) continue;
            var polygon = new List<Vector2> { triangle.a, triangle.b, triangle.c };
            polygon = Clip(polygon, 0, uv.xMin, true);
            polygon = Clip(polygon, 0, uv.xMax, false);
            polygon = Clip(polygon, 1, uv.yMin, true);
            polygon = Clip(polygon, 1, uv.yMax, false);
            int start = vh.currentVertCount;
            foreach (Vector2 point in polygon)
                vh.AddVert(target.InverseTransformPoint(surface.transform.TransformPoint(triangle.Map(point))), tint, Vector2.zero);
            for (int i = 1; i + 1 < polygon.Count; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    internal bool HitRegion(Vector2 screenPoint, Camera camera, Rect region, bool rightPage)
    {
        if (camera == null) return false;
        Ray worldRay = camera.ScreenPointToRay(screenPoint);
        var ray = new Ray(surface.transform.InverseTransformPoint(worldRay.origin), surface.transform.InverseTransformVector(worldRay.direction));
        foreach (var triangle in pageTriangles)
        {
            if (triangle.right != rightPage) continue;
            var plane = new Plane(triangle.pa, triangle.pb, triangle.pc);
            if (!plane.Raycast(ray, out float distance)) continue;
            Vector3 ab = triangle.pb - triangle.pa, ac = triangle.pc - triangle.pa, ap = ray.GetPoint(distance) - triangle.pa;
            float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac);
            float det = aa * bb - cross * cross;
            if (Mathf.Abs(det) < 0.00000001f) continue;
            float v = (bb * Vector3.Dot(ap, ab) - cross * Vector3.Dot(ap, ac)) / det;
            float w = (aa * Vector3.Dot(ap, ac) - cross * Vector3.Dot(ap, ab)) / det;
            if (v < 0 || w < 0 || v + w > 1f) continue;
            Vector2 uv = triangle.a + (triangle.b - triangle.a) * v + (triangle.c - triangle.a) * w;
            if (region.Contains(uv)) return true;
        }
        return false;
    }

    private static List<Vector2> Clip(List<Vector2> polygon, int axis, float edge, bool keepGreater)
    {
        var result = new List<Vector2>();
        if (polygon.Count == 0) return result;
        Vector2 previous = polygon[polygon.Count - 1];
        bool wasInside = keepGreater ? previous[axis] >= edge : previous[axis] <= edge;
        foreach (Vector2 current in polygon)
        {
            bool inside = keepGreater ? current[axis] >= edge : current[axis] <= edge;
            if (inside != wasInside) result.Add(Vector2.Lerp(previous, current, (edge - previous[axis]) / (current[axis] - previous[axis])));
            if (inside) result.Add(current);
            previous = current;
            wasInside = inside;
        }
        return result;
    }

    private void PlaceClueRegion(RectTransform rect, BookClueRegion region, bool rightPage)
    {
        Rect uv = region.regionUV;
        Vector2 a = MapPagePoint(new Vector2(uv.xMin, uv.yMin), rightPage), b = MapPagePoint(new Vector2(uv.xMax, uv.yMin), rightPage);
        Vector2 c = MapPagePoint(new Vector2(uv.xMax, uv.yMax), rightPage), d = MapPagePoint(new Vector2(uv.xMin, uv.yMax), rightPage);
        Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        Vector3 center = MapPagePoint(uv.center, rightPage);
        rect.localPosition = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, center.z);
        // Eğri yüzeyin düz raycast dikdörtgenini aşabildiği kenarlara pay bırak;
        // gerçek tıklama alanını HitRegion UV üzerinden sınırlar.
        rect.sizeDelta = Vector2.Max(max - min, Vector2.one * 0.001f) + Vector2.one * 24f;
        if (pageTriangles.Count == 0) return;
        foreach (var image in rect.GetComponentsInChildren<Image>())
        {
            var projection = image.GetComponent<BookPageRegionProjection>() ?? image.gameObject.AddComponent<BookPageRegionProjection>();
            projection.Surface = this;
            projection.RegionRect = rect;
            projection.RegionUV = uv;
            projection.RightPage = rightPage;
            image.SetVerticesDirty();
        }
    }

    private void RefreshPages()
    {
        left = book.LeftPageIndex; right = book.RightPageIndex;
        foreach (Transform child in surface.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var page in book.synthesisPages)
        {
            if (page == null || !book.IsOnPage(page.pageIndex) || page.blocks == null) continue;
            float x = page.pageIndex == right ? 250 : -250;
            float y = 270;
            for (int i = 0; i < page.blocks.Count; i++)
            {
                var obj = new GameObject("Sayfa İpucu", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
                obj.transform.SetParent(surface.transform, false);
                var block = obj.GetComponent<NotebookDraggableBlock>();
                block.Initialize(page.blocks[i], true);
                var rect = (RectTransform)obj.transform;
                float height = page.blocks[i].blockType == NotebookBlockType.Quest ? 230 : 145;
                rect.sizeDelta = new Vector2(410, height);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(x, y);
                y -= height + 18;
                var image = obj.GetComponent<Image>();
                image.color = new Color(0.96f, 0.92f, 0.79f, 1f);
                var outline = obj.GetComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0.12f, 0.07f, 0.02f, 1);
                outline.effectDistance = new Vector2(4, -4);
                foreach (var label in obj.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    label.color = Color.black;
                    label.fontSize = label.fontStyle == FontStyles.Bold ? 25 : 21;
                    label.enableAutoSizing = false;
                }
                obj.AddComponent<BookPageSourceHighlight>();
            }
        }
        AddClueRegions();
        var handle = new GameObject("Kitabı Taşı", typeof(RectTransform), typeof(Image), typeof(BookPageMoveHandle));
        handle.transform.SetParent(surface.transform, false);
        var hr = (RectTransform)handle.transform;
        hr.sizeDelta = new Vector2(950, 65); hr.anchoredPosition = new Vector2(0, 315);
        handle.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.95f);
        handle.GetComponent<BookPageMoveHandle>().Book = book;
        var text = new GameObject("Başlık", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(handle.transform, false);
        text.rectTransform.sizeDelta = hr.sizeDelta;
        text.text = book.clueData != null ? "SARI CERCEVEYI TUT > DEFTERE SURUKLE\nBu bant: kitabi tasi | A/D: sayfa"
            : "CERCEVELI NOTU TUT > DEFTERE SURUKLE\nBu bant: kitabi tasi | A/D: sayfa"; text.fontSize = 21;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
    }

    // Sayfadaki gerçek görüntüden kırpılan parça bölgeleri: şeffaf alan + ince sarı çerçeve.
    private void AddClueRegions()
    {
        BookClueData clue = book.clueData;
        if (clue == null || clue.pageTexture == null || clue.regions == null) return;
        BookClueData.Register(clue);
        for (int i = 0; i < clue.regions.Count; i++)
        {
            BookClueRegion region = clue.regions[i];
            if (region == null || region.formula == null || !book.IsOnPage(region.pageIndex)) continue;
            var data = new NotebookBlockData
            {
                blockType = NotebookBlockType.PartClue,
                itemID = region.formula.itemID,
                title = region.formula.itemName,
                description = region.formula.description,
                clueDataID = clue.ID,
                clueRegionIndex = i,
            };
            var obj = new GameObject("Sayfa Görüntüsü - " + region.formula.itemName, typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
            obj.transform.SetParent(surface.transform, false);
            obj.GetComponent<NotebookDraggableBlock>().Initialize(data, true);
            PlaceClueRegion((RectTransform)obj.transform, region, region.pageIndex == right);
        }
    }

    private void OnDisable() { if (surface != null) surface.gameObject.SetActive(false); }
    private void OnDestroy() { if (bakedPages != null) Destroy(bakedPages); }
}

// Mevcut sürükleme ve hover görsellerini, seçilen UV'nin gerçek sayfa yüzeyine taşır.
internal sealed class BookPageRegionProjection : BaseMeshEffect, ICanvasRaycastFilter
{
    internal BookPageBlockSurface Surface;
    internal RectTransform RegionRect;
    internal Rect RegionUV;
    internal bool RightPage;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        Surface == null || Surface.HitRegion(screenPoint, eventCamera, RegionUV, RightPage);

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Surface == null || RegionRect == null) return;
        var rect = (RectTransform)transform;
        Vector3 min = RegionRect.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
        Vector3 max = RegionRect.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
        Rect bounds = RegionRect.rect;
        Vector2 a = new Vector2((min.x - bounds.xMin) / bounds.width, (min.y - bounds.yMin) / bounds.height);
        Vector2 b = new Vector2((max.x - bounds.xMin) / bounds.width, (max.y - bounds.yMin) / bounds.height);
        Vector2 uvMin = RegionUV.min + Vector2.Scale(a, RegionUV.size);
        Vector2 uvMax = RegionUV.min + Vector2.Scale(b, RegionUV.size);
        Surface.PopulateRegion(vh, Rect.MinMaxRect(uvMin.x, uvMin.y, uvMax.x, uvMax.y), RightPage, transform, graphic.color);
    }
}

internal class BookPageSourceHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Color originalColor;
    private void Awake() { originalColor = GetComponent<Image>().color; }
    public void OnPointerEnter(PointerEventData data)
    {
        GetComponent<Image>().color = new Color(1f, 0.95f, 0.5f, 1);
        transform.localScale = Vector3.one * 1.05f;
    }
    public void OnPointerExit(PointerEventData data) { ResetVisual(); }
    private void OnDisable() { ResetVisual(); }
    private void ResetVisual()
    {
        var image = GetComponent<Image>();
        if (image != null) image.color = originalColor;
        transform.localScale = Vector3.one;
    }
}

internal class BookPageMoveHandle : MonoBehaviour, IDragHandler
{
    internal InteractableBook Book;
    public void OnDrag(PointerEventData data)
    {
        if (Book == null || Book.isAnimating || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return;
        var camera = Camera.main;
        if (camera == null) return;
        Vector3 point = camera.WorldToScreenPoint(Book.transform.position);
        if (point.z <= camera.nearClipPlane) return;
        Book.transform.position += camera.ScreenToWorldPoint(point + new Vector3(data.delta.x, data.delta.y, 0)) - camera.ScreenToWorldPoint(point);
    }
}
