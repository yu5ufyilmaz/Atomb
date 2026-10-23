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
    // Sayfa-yerel UV (0-1) -> yüzey canvas koordinatı; sol ve sağ sayfa için ayrı çözülür.
    private PageMap leftMap, rightMap;

    private struct PageMap
    {
        public float ax, bx, cx, ay, by, cy;
        public Vector2 Map(Vector2 uv) => new Vector2(ax * uv.x + bx * uv.y + cx, ay * uv.x + by * uv.y + cy);
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
        surface.gameObject.SetActive(true);
        if (left != book.LeftPageIndex || right != book.RightPageIndex) RefreshPages();
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
            mesh.BakeMesh(bakedPages);
            bakedPages.RecalculateBounds();
            if (bakedPages.vertexCount > 0 && bakedPages.bounds.size.sqrMagnitude > 0.00000001f)
                bounds = bakedPages.bounds;
        }
        rect.sizeDelta = new Vector2(1000, 700);
        // Sayfa eğri/kalın olsa da çerçeveler yüzeyin altında kalmasın diye canvas yeterince yukarıda durur.
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

    // Sayfa meshinin gerçek UV'sinden, kırpılan bölgenin yüzeyde tam hangi noktaya denk geldiğini çıkarır.
    // Mesh okunamazsa veya sayfa düz değilse kitabı iki eşit yarım sayfa sayan yaklaşık yerleşime döner.
    private void BuildPageMaps(SkinnedMeshRenderer mesh, RectTransform rect)
    {
        leftMap = FallbackMap(false);
        rightMap = FallbackMap(true);
        if (bakedPages == null || bakedPages.vertexCount == 0) return;
        var uvs = new List<Vector2>();
        bakedPages.GetUVs(0, uvs);
        Vector3[] vertices = bakedPages.vertices;
        if (uvs.Count != vertices.Length || bakedPages.subMeshCount == 0) return;
        int submesh = Mathf.Clamp(book.bookMaterialIndex, 0, bakedPages.subMeshCount - 1);
        var used = new HashSet<int>(bakedPages.GetIndices(submesh));
        var leftUV = new List<Vector2>(); var leftPos = new List<Vector2>();
        var rightUV = new List<Vector2>(); var rightPos = new List<Vector2>();
        foreach (int index in used)
        {
            Vector2 uv = uvs[index];
            Vector3 local = rect.InverseTransformPoint(mesh.transform.TransformPoint(vertices[index]));
            Vector2 pos = new Vector2(local.x, local.y);
            if (uv.x < 0.5f) { leftUV.Add(new Vector2(uv.x * 2f, uv.y)); leftPos.Add(pos); }
            else { rightUV.Add(new Vector2((uv.x - 0.5f) * 2f, uv.y)); rightPos.Add(pos); }
        }
        if (TryFit(leftUV, leftPos, out PageMap fittedLeft)) leftMap = fittedLeft;
        if (TryFit(rightUV, rightPos, out PageMap fittedRight)) rightMap = fittedRight;
    }

    private static PageMap FallbackMap(bool rightPage)
    {
        return new PageMap { ax = 500, cx = rightPage ? 0 : -500, by = 700, cy = -350 };
    }

    // En küçük kareler ile pos = M * uv + t (2B afin) çözer; düz olmayan sayfalarda reddeder.
    private static bool TryFit(List<Vector2> uv, List<Vector2> pos, out PageMap map)
    {
        map = default;
        int count = uv.Count;
        if (count < 3) return false;
        double suu = 0, suv = 0, su = 0, svv = 0, sv = 0;
        double sux = 0, svx = 0, sx = 0, suy = 0, svy = 0, sy = 0;
        for (int i = 0; i < count; i++)
        {
            double u = uv[i].x, v = uv[i].y, x = pos[i].x, y = pos[i].y;
            suu += u * u; suv += u * v; su += u; svv += v * v; sv += v;
            sux += u * x; svx += v * x; sx += x;
            suy += u * y; svy += v * y; sy += y;
        }
        double[] m = { suu, suv, su, suv, svv, sv, su, sv, count };
        double det = Det3(m);
        if (System.Math.Abs(det) < 1e-9) return false;
        double[] rx = SolveCramer(m, det, sux, svx, sx);
        double[] ry = SolveCramer(m, det, suy, svy, sy);
        map = new PageMap { ax = (float)rx[0], bx = (float)rx[1], cx = (float)rx[2], ay = (float)ry[0], by = (float)ry[1], cy = (float)ry[2] };
        double error = 0;
        for (int i = 0; i < count; i++) error += (map.Map(uv[i]) - pos[i]).sqrMagnitude;
        // Sayfa eğriliği küçük sapma verir; büyük sapma UV düzeninin beklenenden farklı olduğunu gösterir.
        return System.Math.Sqrt(error / count) < 25.0;
    }

    private static double Det3(double[] m)
    {
        return m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
    }

    private static double[] SolveCramer(double[] m, double det, double r0, double r1, double r2)
    {
        double[] result = new double[3];
        double[] rhs = { r0, r1, r2 };
        for (int column = 0; column < 3; column++)
        {
            double[] c = (double[])m.Clone();
            for (int row = 0; row < 3; row++) c[row * 3 + column] = rhs[row];
            result[column] = Det3(c) / det;
        }
        return result;
    }

    private void PlaceClueRegion(RectTransform rect, BookClueRegion region, bool rightPage)
    {
        PageMap map = rightPage ? rightMap : leftMap;
        Rect uv = region.regionUV;
        Vector2 a = map.Map(new Vector2(uv.xMin, uv.yMin)), b = map.Map(new Vector2(uv.xMax, uv.yMin));
        Vector2 c = map.Map(new Vector2(uv.xMax, uv.yMax)), d = map.Map(new Vector2(uv.xMin, uv.yMax));
        Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        rect.anchoredPosition = (min + max) * 0.5f;
        rect.sizeDelta = Vector2.Max(max - min, new Vector2(24f, 24f));
    }

    private void RefreshPages()
    {
        left = book.LeftPageIndex; right = book.RightPageIndex;
        foreach (Transform child in surface.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var page in book.synthesisPages)
        {
            if (page == null || !book.IsOnPage(page.pageIndex) || page.blocks == null) continue;
            float x = page.pageIndex == right ? 250 : -250;
            for (int i = 0; i < page.blocks.Count; i++)
            {
                var obj = new GameObject("Sayfa İpucu", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
                obj.transform.SetParent(surface.transform, false);
                var block = obj.GetComponent<NotebookDraggableBlock>();
                block.Initialize(page.blocks[i], true);
                var rect = (RectTransform)obj.transform;
                rect.sizeDelta = new Vector2(360, 150);
                rect.anchoredPosition = new Vector2(x, 175 - i * 185);
                var image = obj.GetComponent<Image>();
                image.color = new Color(1f, 0.77f, 0.19f, 1f);
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
            : "SARI KARTI TUT > DEFTERE SURUKLE\nBu bant: kitabi tasi | A/D: sayfa"; text.fontSize = 21;
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

internal class BookPageSourceHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
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
        if (image != null) image.color = new Color(1f, 0.77f, 0.19f, 1);
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
