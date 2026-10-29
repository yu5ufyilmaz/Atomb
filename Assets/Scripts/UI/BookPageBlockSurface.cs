using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Kartlar modelin sayfa yüzeyine bağlıdır; deftere aktarım aynı UI sürükleme yolunu kullanır.
[RequireComponent(typeof(InteractableBook))]
public partial class BookPageBlockSurface : MonoBehaviour
{
    private InteractableBook book;
    private Canvas surface;
    private int left = -1, right = -1;
    private Mesh bakedPages;
    private Mesh pageMapSource;
    private bool[] exposedFaces;
    private int pageMapVertexCount, pageMapMaterial;
    private readonly List<PageTriangle> pageTriangles = new List<PageTriangle>();
    private readonly List<Vector3> mappedVertices = new List<Vector3>();
    private readonly List<Vector2> mappedUVs = new List<Vector2>();
    private readonly List<int> mappedIndices = new List<int>();
    private readonly Dictionary<int, Transform> pageRoots = new Dictionary<int, Transform>();
    private Transform pageParent;
    private bool invalidPages = true;
    private int contentSignature;

    private struct PageTriangle
    {
        public bool right;
        public Vector2 a, b, c;
        public Vector3 pa, pb, pc;
        public Vector2 vAxis, wAxis;
        public float edgeTolerance;
        public bool TryBind(Vector2 uv, out Vector3 point, out float v, out float w)
        {
            Vector2 delta = uv - a;
            v = Vector2.Dot(delta, vAxis); w = Vector2.Dot(delta, wAxis);
            point = default;
            if (v < -edgeTolerance || w < -edgeTolerance || v + w > 1 + edgeTolerance) return false;
            point = pa + (pb - pa) * v + (pc - pa) * w;
            return true;
        }
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

    private void OnEnable() { GameLocalization.LanguageChanged += InvalidatePages; }

    public void InvalidatePages() { left = right = -1; invalidPages = true; }

    private void CancelSourceDrags()
    {
        foreach (var note in surface.GetComponentsInChildren<NotebookDraggableBlock>(true)) note.CancelSourceDrag();
    }

    private void LateUpdate()
    {
        bool visible = !book.IsShelfPulling && (book.isOpen || book.IsClosing) && book.usePhysicalPageBlocks
            && GameManager.Instance != null && (ReferenceEquals(GameManager.Instance.activeInteraction, book) || book.IsClosing)
            && (!book.isAnimating || turning || book.IsOpening || book.IsClosing);
        if (!visible)
        {
            if (surface != null && surface.enabled)
            {
                CancelSourceDrags(); surface.enabled = false; surface.GetComponent<CanvasGroup>().blocksRaycasts = false;
            }
            return;
        }
        if (surface == null) BuildSurface();
        if (surface == null) return;
        bool refresh = left != book.LeftPageIndex || right != book.RightPageIndex;
        surface.gameObject.SetActive(true);
        surface.enabled = true;
        var input = surface.GetComponent<CanvasGroup>();
        input.blocksRaycasts = input.interactable = !book.isAnimating && !GameManager.Instance.isGamePaused;
        if (refresh)
        {
            if (bakedPages != null) book.bookSkinnedMeshRenderer.BakeMesh(bakedPages, true);
            BuildPageMaps(book.bookSkinnedMeshRenderer, (RectTransform)surface.transform);
            RefreshPages();
        }
        if (GameManager.Instance.isGamePaused) return;
        if (book.IsOpening || book.IsClosing)
        {
            book.bookSkinnedMeshRenderer.BakeMesh(bakedPages, true);
            BuildPageMaps(book.bookSkinnedMeshRenderer, (RectTransform)surface.transform);
            RefreshInkGeometry();
        }
    }

    private void BuildSurface()
    {
        var mesh = book.bookSkinnedMeshRenderer;
        if (mesh == null) { Debug.LogError("Kitabın sayfa renderer referansı eksik.", this); enabled = false; return; }
        var obj = new GameObject("Kitap Sayfası Blokları", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
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
        var uvs = mappedUVs;
        bakedPages.GetUVs(0, uvs);
        var vertices = mappedVertices;
        bakedPages.GetVertices(vertices);
        if (uvs.Count != vertices.Count || bakedPages.subMeshCount == 0) return;
        int submesh = Mathf.Clamp(book.bookMaterialIndex, 0, bakedPages.subMeshCount - 1);
        bakedPages.GetTriangles(mappedIndices, submesh);
        var indices = mappedIndices;
        Camera camera = surface != null ? surface.worldCamera : Camera.main;
        mapToWorld = rect.localToWorldMatrix;
        mapHasCamera = camera != null;
        if (mapHasCamera) mapCameraPosition = camera.transform.position;
        bool selectFaces = exposedFaces == null || exposedFaces.Length != indices.Count / 3
            || pageMapSource != mesh.sharedMesh || pageMapVertexCount != vertices.Count || pageMapMaterial != submesh;
        if (selectFaces)
        {
            bindings.Clear();
            exposedFaces = new bool[indices.Count / 3];
            pageMapSource = mesh.sharedMesh; pageMapVertexCount = vertices.Count; pageMapMaterial = submesh;
            bool any = false;
            for (int i = 0; i < indices.Count; i += 3)
            {
                Vector3 a = mesh.transform.TransformPoint(vertices[indices[i]]);
                Vector3 b = mesh.transform.TransformPoint(vertices[indices[i + 1]]);
                Vector3 c = mesh.transform.TransformPoint(vertices[indices[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                float outward = Vector3.Dot(normal, mesh.transform.up);
                // Establish the authored paper side once in the open pose, independent of where the book is viewed.
                // Keep those triangle identities as bones rotate; the ink must not jump to the reverse side.
                bool exposed = Mathf.Abs(outward) > .001f ? outward > 0
                    : camera != null && Vector3.Dot(normal, camera.transform.position - (a + b + c) / 3) > 0;
                exposedFaces[i / 3] = exposed;
                any |= exposed;
            }
            // A one-sided mesh can use a different authored axis. Retain its map rather than losing all page input.
            if (!any) for (int i = 0; i < exposedFaces.Length; i++) exposedFaces[i] = true;
        }
        Matrix4x4 meshToSurface = rect.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
        for (int i = 0; i < indices.Count; i += 3)
        {
            if (!exposedFaces[i / 3]) continue;
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            bool rightPage = (uvs[a].x + uvs[b].x + uvs[c].x) / 3f >= 0.5f;
            Vector2 offset = rightPage ? Vector2.right : Vector2.zero;
            var triangle = new PageTriangle
            {
                right = rightPage,
                a = Vector2.Scale(uvs[a], new Vector2(2, 1)) - offset,
                b = Vector2.Scale(uvs[b], new Vector2(2, 1)) - offset,
                c = Vector2.Scale(uvs[c], new Vector2(2, 1)) - offset,
                pa = meshToSurface.MultiplyPoint3x4(vertices[a]),
                pb = meshToSurface.MultiplyPoint3x4(vertices[b]),
                pc = meshToSurface.MultiplyPoint3x4(vertices[c]),
            };
            Vector2 ab = triangle.b - triangle.a, ac = triangle.c - triangle.a;
            float determinant = Cross(ab, ac);
            if (Mathf.Abs(determinant) > 0.00000001f)
            {
                triangle.vAxis = new Vector2(ac.y, -ac.x) / determinant;
                triangle.wAxis = new Vector2(-ab.y, ab.x) / determinant;
                triangle.edgeTolerance = .000001f / Mathf.Abs(determinant);
                pageTriangles.Add(triangle);
            }
        }
    }

    internal Vector3 MapPagePoint(Vector2 uv, bool rightPage)
    {
        uv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
        Vector3 result = new Vector3(uv.x * 500 + (rightPage ? 0 : -500), uv.y * 700 - 350, 0);
        float nearest = float.PositiveInfinity;
        foreach (var triangle in pageTriangles)
        {
            if (triangle.right != rightPage || !triangle.TryBind(uv, out Vector3 point, out _, out _)) continue;
            float distance = mapHasCamera ? (mapToWorld.MultiplyPoint3x4(point) - mapCameraPosition).sqrMagnitude : point.z;
            if (distance >= nearest) continue;
            nearest = distance; result = point;
        }
        return result;
    }

    internal Vector3 MapPageWorld(Vector2 uv, bool rightPage) => surface.transform.TransformPoint(MapPagePoint(uv, rightPage));

    private Matrix4x4 mapToWorld;
    private Vector3 mapCameraPosition;
    private bool mapHasCamera;

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Görünür alan ve dört kenar aynı UV üçgenlerinden kırpılır; sayfanın yüksekliği kaybolmaz.
    internal void PopulateRegion(VertexHelper vh, Rect uv, bool rightPage, Transform target, Color tint, int pageIndex = -1)
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
            if (polygon.Count < 3) continue;
            Vector2 center = Vector2.zero;
            foreach (var point in polygon) center += point;
            center /= polygon.Count;
            if ((triangle.Map(center) - MapPagePoint(center, rightPage)).sqrMagnitude > .0001f) continue;
            int start = vh.currentVertCount;
            foreach (Vector2 point in polygon)
                vh.AddVert(target.InverseTransformPoint(MapPrintedPoint(point, rightPage, pageIndex)), tint, Vector2.zero);
            for (int i = 1; i + 1 < polygon.Count; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    internal bool HitRegion(Vector2 screenPoint, Camera camera, Rect region, bool rightPage)
    {
        return TryGetPageUV(screenPoint, camera, rightPage, out Vector2 uv) && region.Contains(uv);
    }

    internal bool TryGetPageUV(Vector2 screenPoint, Camera camera, bool rightPage, out Vector2 uv)
    {
        uv = Vector2.zero;
        if (camera == null) return false;
        Ray worldRay = camera.ScreenPointToRay(screenPoint);
        var ray = new Ray(surface.transform.InverseTransformPoint(worldRay.origin), surface.transform.InverseTransformVector(worldRay.direction));
        float nearest = float.PositiveInfinity;
        bool hit = false;
        foreach (var triangle in pageTriangles)
        {
            if (triangle.right != rightPage) continue;
            var plane = new Plane(triangle.pa, triangle.pb, triangle.pc);
            if (!plane.Raycast(ray, out float distance) || distance >= nearest) continue;
            Vector3 ab = triangle.pb - triangle.pa, ac = triangle.pc - triangle.pa, ap = ray.GetPoint(distance) - triangle.pa;
            float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac);
            float det = aa * bb - cross * cross;
            if (Mathf.Abs(det) < 0.00000001f) continue;
            float v = (bb * Vector3.Dot(ap, ab) - cross * Vector3.Dot(ap, ac)) / det;
            float w = (aa * Vector3.Dot(ap, ac) - cross * Vector3.Dot(ap, ab)) / det;
            if (v < 0 || w < 0 || v + w > 1f) continue;
            uv = triangle.a + (triangle.b - triangle.a) * v + (triangle.c - triangle.a) * w;
            nearest = distance;
            hit = true;
        }
        return hit;
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
        if (invalidPages)
        {
            invalidPages = false;
            inkPages.Clear(); pageRoots.Clear(); bindings.Clear();
            var oldChildren = new List<GameObject>();
            foreach (Transform child in surface.transform) oldChildren.Add(child.gameObject);
            foreach (var child in oldChildren)
            {
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            AddBookMovePage(false); AddBookMovePage(true);
        }
        foreach (var page in book.synthesisPages)
        {
            if (page == null || !IsPresentedPage(page.pageIndex)) continue;
            if (pageRoots.ContainsKey(page.pageIndex)) continue;
            var root = new GameObject("Basılı Sayfa " + page.pageIndex, typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(surface.transform, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 700);
            pageParent = root.transform; pageRoots.Add(page.pageIndex, pageParent);
            bool rightPage = (page.pageIndex & 1) != 0;
            AddPrintedPageText(page.title, new Rect(.09f, .89f, .82f, .07f), page.pageIndex, 24, true);
            AddPrintedPageText("s. {0}", new Rect(.7f, .035f, .2f, .05f), page.pageIndex, 14, false, page.pageIndex + 1);
            if (page.blocks == null || page.blocks.Count == 0)
            {
                AddPrintedPageText(page.explanation, new Rect(.09f, .23f, .82f, .60f), page.pageIndex, 20, false);
                AddClueRegions(page.pageIndex); continue;
            }
            for (int i = 0; i < page.blocks.Count; i++)
            {
                var obj = new GameObject("Sayfa İpucu", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
                obj.transform.SetParent(pageParent, false);
                var block = obj.GetComponent<NotebookDraggableBlock>();
                block.Initialize(page.blocks[i], true);
                if (book.printNotesOnPages) block.UsePrintedTitle();
                var rect = (RectTransform)obj.transform;
                bool traceVisual = SignalTrialGraphic.IsNote(page.blocks[i]);
                Rect region = NoteRegion(page.blocks[i], i, page.blocks.Count);
                rect.sizeDelta = new Vector2(region.width * 500, region.height * 700);
                var image = obj.GetComponent<Image>();
                image.color = new Color(0.96f, 0.92f, 0.79f, 1f);
                var outline = obj.GetComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0.12f, 0.07f, 0.02f, 1);
                outline.effectDistance = new Vector2(4, -4);
                foreach (var label in obj.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    label.color = Color.black;
                    bool heading = label.fontStyle == FontStyles.Bold;
                    label.fontSize = heading ? 22 : 18;
                    label.enableAutoSizing = heading || traceVisual;
                    label.fontSizeMin = traceVisual && !heading ? 12 : 16;
                    label.fontSizeMax = 22;
                }
                NotebookWorkspaceController.Instance?.StyleNotebookNote(block);
                foreach (var label in obj.GetComponentsInChildren<TMP_Text>())
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = label.name == "Başlık" ? 14 : 12;
                    label.overflowMode = TextOverflowModes.Overflow;
                }
                if (book.printNotesOnPages)
                {
                    // Leave the ink and diagrams on the model; the transparent region still supports dragging.
                    image.color = Color.clear;
                    image.canvasRenderer.cullTransparentMesh = false;
                    foreach (var border in obj.GetComponentsInChildren<NotebookSketchBorder>())
                    {
                        border.enabled = true;
                        border.BlockType = NotebookBlockType.PartClue;
                        border.color = new Color(.48f, .31f, .09f, .65f);
                    }
                    outline.enabled = false;
                    var tape = obj.transform.Find("Bant");
                    if (tape != null) tape.gameObject.SetActive(false);
                }
                rect.pivot = new Vector2(.5f, .5f);
                PlaceClueRegion(rect, new BookClueRegion { regionUV = region }, rightPage);
                foreach (var localized in obj.GetComponentsInChildren<LocalizedText>(true)) localized.RefreshBaseline();
                foreach (var graphic in obj.GetComponentsInChildren<Graphic>())
                {
                    var previous = graphic.GetComponent<BookPageRegionProjection>();
                    if (previous != null) previous.enabled = false;
                    var printed = graphic.gameObject.AddComponent<BookPagePrintedGraphic>();
                    printed.Surface = this; printed.RegionRect = rect; printed.RegionUV = region;
                    printed.RightPage = rightPage; printed.PageIndex = page.pageIndex;
                }
                RegisterInk(obj, page.pageIndex);
                obj.AddComponent<BookPageSourceHighlight>();
            }
            AddClueRegions(page.pageIndex);
        }
        if (book.clueData != null && book.clueData.regions != null) foreach (var region in book.clueData.regions)
        {
            if (region == null || !IsPresentedPage(region.pageIndex) || pageRoots.ContainsKey(region.pageIndex)) continue;
            var root = new GameObject("Basılı Sayfa " + region.pageIndex, typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(surface.transform, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 700);
            pageParent = root.transform; pageRoots.Add(region.pageIndex, pageParent); AddClueRegions(region.pageIndex);
        }
        pageParent = null;
        foreach (var pair in pageRoots)
        {
            var group = pair.Value.GetComponent<CanvasGroup>();
            bool visible = IsPresentedPage(pair.Key);
            if (group.alpha != (visible ? 1 : 0)) group.alpha = visible ? 1 : 0;
            group.blocksRaycasts = visible;
        }
        foreach (var ink in inkPages)
            if (ink.group != null && ink.group.alpha != (IsPresentedPage(ink.page) ? 1 : 0)) ink.group.alpha = IsPresentedPage(ink.page) ? 1 : 0;
        RefreshInkGeometry();
    }

    private static Rect NoteRegion(NotebookBlockData note, int index, int count)
    {
        Rect assigned = note.bookRegion;
        if (assigned.width > 0 && assigned.height > 0)
            return Rect.MinMaxRect(Mathf.Clamp(assigned.xMin, .09f, .91f), Mathf.Clamp(assigned.yMin, .11f, .85f),
                Mathf.Clamp(assigned.xMax, .09f, .91f), Mathf.Clamp(assigned.yMax, .11f, .85f));
        float gap = Mathf.Min(.035f, .74f / (Mathf.Max(1, count) * 3));
        float height = Mathf.Min(note.blockType == NotebookBlockType.Quest && count == 1 ? .74f : .32f,
            (.74f - gap * (count - 1)) / Mathf.Max(1, count));
        return new Rect(.09f, .85f - height - index * (height + gap), .82f, height);
    }

    private void AddPrintedPageText(string content, Rect region, int pageIndex, float size, bool heading, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var obj = new GameObject(heading ? "Sayfa Başlığı" : "Kitap Dizini", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        obj.transform.SetParent(pageParent != null ? pageParent : surface.transform, false);
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = text.fontSizeMax = size;
        text.fontSizeMin = heading ? 14 : 12;
        text.enableAutoSizing = true;
        text.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
        text.color = new Color(.16f, .12f, .07f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        bool rightPage = (pageIndex & 1) != 0;
        PlaceClueRegion(text.rectTransform, new BookClueRegion { regionUV = region }, rightPage);
        if (args.Length > 0) GameLocalization.SetFormat(text, content, args);
        else GameLocalization.Set(text, content);
        var printed = obj.AddComponent<BookPagePrintedGraphic>();
        printed.Surface = this; printed.RegionRect = text.rectTransform; printed.RegionUV = region;
        printed.RightPage = rightPage; printed.PageIndex = pageIndex;
        RegisterInk(obj, pageIndex);
    }

    private void AddBookMovePage(bool rightPage)
    {
        var obj = new GameObject("Kitabı Boş Sayfadan Taşı", typeof(RectTransform), typeof(Image), typeof(BookPageMoveHandle));
        obj.transform.SetParent(surface.transform, false);
        var image = obj.GetComponent<Image>();
        image.color = Color.clear; image.canvasRenderer.cullTransparentMesh = false;
        var move = obj.GetComponent<BookPageMoveHandle>();
        move.Book = book; move.Surface = this; move.RightPage = rightPage;
        PlaceClueRegion((RectTransform)obj.transform, new BookClueRegion { regionUV = new Rect(.01f, .01f, .98f, .98f) }, rightPage);
    }

    // Sayfadaki gerçek görüntüden kırpılan parça bölgeleri: şeffaf alan + ince sarı çerçeve.
    private void AddClueRegions(int onlyPage)
    {
        BookClueData clue = book.clueData;
        if (clue == null || clue.GetPageTexture() == null || clue.regions == null) return;
        BookClueData.Register(clue);
        for (int i = 0; i < clue.regions.Count; i++)
        {
            BookClueRegion region = clue.regions[i];
            if (region == null || region.formula == null || region.pageIndex != onlyPage) continue;
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
            obj.transform.SetParent(pageParent != null ? pageParent : surface.transform, false);
            obj.GetComponent<NotebookDraggableBlock>().Initialize(data, true);
            PlaceClueRegion((RectTransform)obj.transform, region, (region.pageIndex & 1) != 0);
            foreach (var projection in obj.GetComponentsInChildren<BookPageRegionProjection>()) projection.PageIndex = region.pageIndex;
            RegisterInk(obj, region.pageIndex);
        }
    }

    private void OnDisable() { GameLocalization.LanguageChanged -= InvalidatePages; if (turning) EndTurn(); if (surface != null) surface.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        ReleaseTurningMesh(true);
        if (bakedPages != null) { if (Application.isPlaying) Destroy(bakedPages); else DestroyImmediate(bakedPages); }
    }
}

// Mevcut sürükleme ve hover görsellerini, seçilen UV'nin gerçek sayfa yüzeyine taşır.
internal sealed class BookPageRegionProjection : BaseMeshEffect, ICanvasRaycastFilter
{
    internal BookPageBlockSurface Surface;
    internal RectTransform RegionRect;
    internal Rect RegionUV;
    internal bool RightPage;
    internal int PageIndex = -1;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        Surface == null || Surface.HitRegion(screenPoint, eventCamera, RegionUV, RightPage);

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Surface == null || RegionRect == null) return;
        if (graphic is Image && graphic.color.a == 0) return;
        var rect = (RectTransform)transform;
        Vector3 min = RegionRect.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
        Vector3 max = RegionRect.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
        Rect bounds = RegionRect.rect;
        Vector2 a = new Vector2((min.x - bounds.xMin) / bounds.width, (min.y - bounds.yMin) / bounds.height);
        Vector2 b = new Vector2((max.x - bounds.xMin) / bounds.width, (max.y - bounds.yMin) / bounds.height);
        Vector2 uvMin = RegionUV.min + Vector2.Scale(a, RegionUV.size);
        Vector2 uvMax = RegionUV.min + Vector2.Scale(b, RegionUV.size);
        Surface.PopulateRegion(vh, Rect.MinMaxRect(uvMin.x, uvMin.y, uvMax.x, uvMax.y), RightPage, transform, graphic.color, PageIndex);
    }
}

internal class BookPageSourceHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Color originalColor;
    private bool printed;
    private NotebookSketchBorder border;
    private Color borderColor;
    private void Awake()
    {
        originalColor = GetComponent<Image>().color;
        printed = GetComponentInParent<InteractableBook>()?.printNotesOnPages == true;
        border = GetComponentInChildren<NotebookSketchBorder>();
        if (border != null) borderColor = border.color;
    }
    public void OnPointerEnter(PointerEventData data)
    {
        GetComponent<Image>().color = printed ? Color.clear
            : NotebookWorkspaceController.Instance != null && NotebookWorkspaceController.Instance.IsArchiveNotebook
            ? new Color(.88f, .91f, .84f, 1) : new Color(1f, 0.95f, 0.5f, 1);
        if (printed && border != null) border.color = new Color(.5f, .25f, .03f, 1);
        if (!printed) transform.localScale = Vector3.one * 1.05f;
    }
    public void OnPointerExit(PointerEventData data) { ResetVisual(); }
    private void OnDisable() { ResetVisual(); }
    private void ResetVisual()
    {
        var image = GetComponent<Image>();
        if (image != null) image.color = originalColor;
        if (border != null) border.color = borderColor;
        transform.localScale = Vector3.one;
    }
}

internal class BookPageMoveHandle : MonoBehaviour, IDragHandler, ICanvasRaycastFilter
{
    internal InteractableBook Book;
    internal BookPageBlockSurface Surface;
    internal bool RightPage;
    public bool IsRaycastLocationValid(Vector2 point, Camera camera) =>
        Surface == null || Surface.TryGetPageUV(point, camera, RightPage, out _);
    public void OnDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || Book == null || Book.isAnimating || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return;
        var camera = Camera.main;
        if (camera == null) return;
        Vector3 point = camera.WorldToScreenPoint(Book.transform.position);
        if (point.z <= camera.nearClipPlane) return;
        Book.transform.position += camera.ScreenToWorldPoint(point + new Vector3(data.delta.x, data.delta.y, 0)) - camera.ScreenToWorldPoint(point);
    }
}
