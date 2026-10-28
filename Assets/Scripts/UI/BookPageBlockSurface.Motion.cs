using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public partial class BookPageBlockSurface
{
    private bool turning;
    private int turnDirection, nextLeft, nextRight;
    private float turnProgress;
    private Mesh turningMesh, originalTurningMesh;
    private MeshFilter turningFilter;
    private Vector3[] turningVertices;
    private InkPoint[] paperPoints;
    private Vector3[] paperSurface;
    private float curlTime, curlSwing, curlArch;
    private Vector3 curlNormal, inkLift;
    private readonly Dictionary<Vector3, InkPoint> bindings = new Dictionary<Vector3, InkPoint>();
    private readonly List<InkPage> inkPages = new List<InkPage>();
    private struct InkPage { public CanvasGroup group; public int page; public BookPagePrintedGraphic[] ink; public Graphic[] legacy; }
    private const int PaperColumns = 24, PaperRows = 8;

    private bool IsPresentedPage(int page) => page == left || page == right || turning && (page == nextLeft || page == nextRight);
    private bool IsMovingPage(int page) => turning && (turnDirection > 0 ? page == right || page == nextLeft : page == left || page == nextRight);

    private void RegisterInk(GameObject obj, int page)
    {
        inkPages.Add(new InkPage { group = obj.GetComponent<CanvasGroup>(), page = page,
            ink = obj.GetComponentsInChildren<BookPagePrintedGraphic>(true), legacy = book.clueData != null ? obj.GetComponentsInChildren<Graphic>(true) : null });
    }

    public void PreparePresentation()
    {
        if (surface == null) BuildSurface();
        if (surface == null) return;
        int signature = 17;
        unchecked
        {
            foreach (var page in book.synthesisPages)
            {
                if (page == null) continue;
                signature = signature * 31 + page.pageIndex;
                signature = signature * 31 + (page.title?.GetHashCode() ?? 0);
                signature = signature * 31 + (page.explanation?.GetHashCode() ?? 0);
                if (page.blocks == null) continue;
                foreach (var note in page.blocks)
                {
                    if (note == null) continue;
                    signature = signature * 31 + (int)note.blockType;
                    signature = signature * 31 + (note.questID?.GetHashCode() ?? 0);
                    signature = signature * 31 + (note.clueDataID?.GetHashCode() ?? 0);
                    signature = signature * 31 + note.clueRegionIndex;
                    signature = signature * 31 + (note.title?.GetHashCode() ?? 0);
                    signature = signature * 31 + (note.description?.GetHashCode() ?? 0);
                    signature = signature * 31 + (note.itemID?.GetHashCode() ?? 0);
                    signature = signature * 31 + note.bookRegion.GetHashCode();
                }
            }
        }
        if (signature != contentSignature) { invalidPages = true; contentSignature = signature; }
        book.bookSkinnedMeshRenderer.BakeMesh(bakedPages, true);
        BuildPageMaps(book.bookSkinnedMeshRenderer, (RectTransform)surface.transform);
        UpdateInkFrame();
        RefreshPages();
    }

    public bool BeginTurn(int direction)
    {
        if (turning || (direction != 1 && direction != -1) || book.pageFlipRenderer == null || book.pageTurnMaterial == null) return false;
        if (surface == null) BuildSurface();
        if (surface == null || pageTriangles.Count == 0) return false;
        turningFilter = book.pageFlipRenderer.GetComponent<MeshFilter>();
        if (turningFilter == null) return false;
        left = book.LeftPageIndex; right = book.RightPageIndex;
        nextLeft = left + direction * 2; nextRight = right + direction * 2;
        if (book.allowLoop)
        {
            nextLeft = (nextLeft + book.totalPages) % book.totalPages;
            nextRight = (nextRight + book.totalPages) % book.totalPages;
        }
        if (nextLeft == left && nextRight == right) return false;
        if (!book.allowLoop && (nextLeft < 0 || nextRight >= book.totalPages)) return false;
        CancelSourceDrags();
        turnDirection = direction;
        turning = true;
        RefreshPages();
        surface.gameObject.SetActive(true);
        surface.GetComponent<CanvasGroup>().blocksRaycasts = false;
        originalTurningMesh = turningFilter.sharedMesh;
        if (turningMesh == null)
        {
        turningMesh = new Mesh { name = "Flexible book paper" }; turningMesh.MarkDynamic();
        turningVertices = new Vector3[(PaperColumns + 1) * (PaperRows + 1)];
        paperPoints = new InkPoint[turningVertices.Length];
        paperSurface = new Vector3[turningVertices.Length];
        var uv = new Vector2[turningVertices.Length];
        var triangles = new int[PaperColumns * PaperRows * 6];
        int index = 0;
        for (int y = 0; y <= PaperRows; y++)
            for (int x = 0; x <= PaperColumns; x++)
            {
                int vertex = y * (PaperColumns + 1) + x;
                uv[vertex] = new Vector2(.5f + .5f * x / PaperColumns, (float)y / PaperRows);
                if (x == PaperColumns || y == PaperRows) continue;
                triangles[index++] = vertex; triangles[index++] = vertex + PaperColumns + 1; triangles[index++] = vertex + 1;
                triangles[index++] = vertex + 1; triangles[index++] = vertex + PaperColumns + 1; triangles[index++] = vertex + PaperColumns + 2;
            }
        turningMesh.vertices = turningVertices; turningMesh.uv = uv; turningMesh.triangles = triangles;
        }
        for (int y = 0; y <= PaperRows; y++) for (int x = 0; x <= PaperColumns; x++)
            paperPoints[y * (PaperColumns + 1) + x] = BindPaper(new Vector2((float)x / PaperColumns, (float)y / PaperRows));
        turningFilter.sharedMesh = turningMesh;
        book.pageFlipRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
        book.pageFlipRenderer.receiveShadows = true;
        // At zero the existing Shader Graph leaves object vertices and normals unchanged.
        // CPU paper and printed ink then share exactly the same curved surface.
        book.pageTurnMaterial.SetFloat("_PageFlip", 0);
        UpdateTurn(0);
        return true;
    }

    public void UpdateTurn(float progress)
    {
        if (!turning) return;
        turnProgress = Mathf.Clamp01(progress);
        UpdateInkFrame();
        Matrix4x4 toPaper = turningFilter.transform.worldToLocalMatrix * surface.transform.localToWorldMatrix;
        for (int y = 0; y <= PaperRows; y++)
            for (int x = 0; x <= PaperColumns; x++)
            {
                int index = y * (PaperColumns + 1) + x;
                paperSurface[index] = EvaluateInk(paperPoints[index], true, false);
                turningVertices[index] = toPaper.MultiplyPoint3x4(paperSurface[index]);
            }
        turningMesh.vertices = turningVertices;
        turningMesh.RecalculateNormals(); turningMesh.RecalculateTangents(); turningMesh.RecalculateBounds();
        float amount = turnDirection > 0 ? turnProgress : 1 - turnProgress;
        foreach (var ink in inkPages)
        {
            if (ink.group == null) continue;
            bool front = (ink.page & 1) != 0;
            bool visible = IsMovingPage(ink.page) ? (front ? amount < .5f : amount >= .5f)
                : turnDirection > 0 ? (ink.page == left ? amount < .99f : amount > .01f)
                : ink.page == right ? amount > .01f : amount < .99f;
            if (ink.group.alpha != (visible ? 1 : 0)) ink.group.alpha = visible ? 1 : 0;
        }
        RefreshInkGeometry();
    }

    internal Vector3 MapTurningPage(Vector2 uv)
    {
        float t = turnDirection > 0 ? turnProgress : 1 - turnProgress;
        Vector3 from = MapPageWorld(uv, true);
        Vector3 to = MapPageWorld(new Vector2(1 - uv.x, uv.y), false);
        Vector3 spine = (MapPageWorld(new Vector2(0, uv.y), true) + MapPageWorld(new Vector2(1, uv.y), false)) * .5f;
        Vector3 across = Vector3.Lerp(from, to, .5f);
        Vector3 normal = book.bookSkinnedMeshRenderer.transform.up.normalized;
        float arch = Mathf.Sin(t * Mathf.PI);
        // The outer edge leads a little while the bound edge resists; zero at both endpoints.
        float angle = t * Mathf.PI + .28f * Mathf.Sin(t * 2 * Mathf.PI) * uv.x;
        Vector3 point = across + (from - to) * (.5f * Mathf.Cos(angle));
        point += normal * (Vector3.Distance(from, to) * .5f * Mathf.Sin(angle));
        point += normal * (Vector3.Distance(from, spine) * .08f * arch * Mathf.Sin(uv.y * Mathf.PI));
        return point;
    }

    internal Vector3 MapPrintedPoint(Vector2 uv, bool rightPage, int pageIndex)
    {
        Vector3 point = IsMovingPage(pageIndex)
            ? MapTurningPage(new Vector2(rightPage ? uv.x : 1 - uv.x, uv.y)) : MapPageWorld(uv, rightPage);
        Camera camera = surface.worldCamera != null ? surface.worldCamera : Camera.main;
        // Every ink type, including region highlights, stays just above the visible paper side.
        Vector3 outward = camera != null ? (camera.transform.position - point).normalized : book.bookSkinnedMeshRenderer.transform.up;
        return point + outward * .0005f;
    }

    private void RefreshInkGeometry()
    {
        UpdateInkFrame();
        foreach (var page in inkPages)
        {
            if (page.group == null || page.group.alpha == 0 || !IsPresentedPage(page.page)) continue;
            if (turning && !IsMovingPage(page.page)) continue;
            foreach (var ink in page.ink) if (ink != null) ink.RefreshProjection();
            if (page.legacy != null) foreach (var graphic in page.legacy) if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public void EndTurn()
    {
        turning = false;
        ReleaseTurningMesh();
        if (surface == null) return;
        surface.GetComponent<CanvasGroup>().blocksRaycasts = true;
        RefreshPages();
    }

    private void ReleaseTurningMesh(bool destroy = false)
    {
        if (turningFilter != null && originalTurningMesh != null) turningFilter.sharedMesh = originalTurningMesh;
        if (destroy && turningMesh != null)
        {
            if (Application.isPlaying) Destroy(turningMesh); else DestroyImmediate(turningMesh);
        }
        if (destroy) { turningMesh = null; turningVertices = null; paperPoints = null; paperSurface = null; }
    }

    internal struct InkPoint
    {
        public int triangle;
        public float v, w, x, y, yArch, radius, bow;
        public Vector3 rest, across, halfSpan;
    }

    internal bool MovingInk(int page) => IsMovingPage(page);
    internal Matrix4x4 InkToWorld => surface.transform.localToWorldMatrix;

    internal InkPoint BindInk(Vector2 uv, bool rightPage)
    {
        var key = new Vector3(uv.x, uv.y, rightPage ? 1 : 0);
        if (bindings.TryGetValue(key, out var cached)) return cached;
        var point = new InkPoint { triangle = -1, rest = new Vector3(uv.x * 500 + (rightPage ? 0 : -500), uv.y * 700 - 350, 0) };
        uv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < pageTriangles.Count; i++)
        {
            var triangle = pageTriangles[i];
            if (triangle.right != rightPage || !triangle.TryBind(uv, out Vector3 rest, out float v, out float w)) continue;
            float distance = mapHasCamera ? (mapToWorld.MultiplyPoint3x4(rest) - mapCameraPosition).sqrMagnitude : rest.z;
            if (distance >= nearest) continue;
            nearest = distance; point.triangle = i; point.rest = rest;
            point.v = v; point.w = w;
        }
        point.x = rightPage ? uv.x : 1 - uv.x;
        point.y = uv.y;
        bindings.Add(key, point);
        return point;
    }

    // Only the paper grid needs analytic curl spans. Thousands of ink vertices sample that grid.
    private InkPoint BindPaper(Vector2 uv)
    {
        var point = BindInk(uv, true);
        Vector3 from = MapPagePoint(new Vector2(point.x, uv.y), true);
        Vector3 to = MapPagePoint(new Vector2(1 - point.x, uv.y), false);
        Vector3 spine = (MapPagePoint(new Vector2(0, uv.y), true) + MapPagePoint(new Vector2(1, uv.y), false)) * .5f;
        point.across = (from + to) * .5f; point.halfSpan = (from - to) * .5f;
        Matrix4x4 world = mapToWorld;
        point.radius = world.MultiplyVector(from - to).magnitude * .5f;
        point.bow = world.MultiplyVector(from - spine).magnitude * .08f;
        point.yArch = Mathf.Sin(uv.y * Mathf.PI);
        return point;
    }

    private void UpdateInkFrame()
    {
        if (surface == null) return;
        curlTime = turnDirection > 0 ? turnProgress : 1 - turnProgress;
        curlSwing = .28f * Mathf.Sin(curlTime * 2 * Mathf.PI);
        curlArch = Mathf.Sin(curlTime * Mathf.PI);
        var inverse = surface.transform.worldToLocalMatrix;
        curlNormal = inverse.MultiplyVector(book.bookSkinnedMeshRenderer.transform.up.normalized);
        var camera = surface.worldCamera;
        Vector3 facing = camera != null ? (camera.transform.position - surface.transform.position).normalized : book.bookSkinnedMeshRenderer.transform.up;
        inkLift = inverse.MultiplyVector(facing * .0005f);
    }

    internal Vector3 EvaluateInk(InkPoint point, bool moving, bool lift = true)
    {
        Vector3 result;
        if (moving)
        {
            if (lift && paperSurface != null)
            {
                float x = Mathf.Clamp01(point.x) * PaperColumns, y = Mathf.Clamp01(point.y) * PaperRows;
                int column = Mathf.Min((int)x, PaperColumns - 1), row = Mathf.Min((int)y, PaperRows - 1);
                float u = x - column, v = y - row; int index = row * (PaperColumns + 1) + column;
                Vector3 a = paperSurface[index], b = paperSurface[index + 1], c = paperSurface[index + PaperColumns + 1], d = paperSurface[index + PaperColumns + 2];
                result = u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
            }
            else
            {
                float angle = curlTime * Mathf.PI + curlSwing * point.x;
                result = point.across + point.halfSpan * Mathf.Cos(angle)
                    + curlNormal * (point.radius * Mathf.Sin(angle) + point.bow * curlArch * point.yArch);
            }
        }
        else if (point.triangle >= 0 && point.triangle < pageTriangles.Count)
        {
            var triangle = pageTriangles[point.triangle];
            result = triangle.pa + (triangle.pb - triangle.pa) * point.v + (triangle.pc - triangle.pa) * point.w;
        }
        else result = point.rest;
        return lift ? result + inkLift : result;
    }
}
