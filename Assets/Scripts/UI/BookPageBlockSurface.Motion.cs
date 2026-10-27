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
    private Graphic[] animatedInk;
    private readonly List<InkPage> inkPages = new List<InkPage>();
    private struct InkPage { public CanvasGroup group; public int page; }
    private const int PaperColumns = 24, PaperRows = 8;

    private bool IsPresentedPage(int page) => page == left || page == right || turning && (page == nextLeft || page == nextRight);
    private bool IsMovingPage(int page) => turning && (turnDirection > 0 ? page == right || page == nextLeft : page == left || page == nextRight);

    private void RegisterInk(GameObject obj, int page)
    {
        inkPages.Add(new InkPage { group = obj.GetComponent<CanvasGroup>(), page = page });
    }

    public void PreparePresentation()
    {
        if (surface == null) BuildSurface();
        if (surface == null) return;
        book.bookSkinnedMeshRenderer.BakeMesh(bakedPages, true);
        BuildPageMaps(book.bookSkinnedMeshRenderer, (RectTransform)surface.transform);
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
        turnDirection = direction;
        turning = true;
        RefreshPages();
        surface.gameObject.SetActive(true);
        surface.GetComponent<CanvasGroup>().blocksRaycasts = false;
        originalTurningMesh = turningFilter.sharedMesh;
        turningMesh = new Mesh { name = "Flexible book paper" };
        turningMesh.MarkDynamic();
        turningVertices = new Vector3[(PaperColumns + 1) * (PaperRows + 1)];
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
        turningFilter.sharedMesh = turningMesh;
        book.pageFlipRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
        book.pageFlipRenderer.receiveShadows = true;
        // At zero the existing Shader Graph leaves object vertices and normals unchanged.
        // CPU paper and printed ink then share exactly the same curved surface.
        book.pageTurnMaterial.SetFloat("_PageFlip", 0);
        animatedInk = surface.GetComponentsInChildren<Graphic>(true);
        UpdateTurn(0);
        return true;
    }

    public void UpdateTurn(float progress)
    {
        if (!turning) return;
        turnProgress = Mathf.Clamp01(progress);
        for (int y = 0; y <= PaperRows; y++)
            for (int x = 0; x <= PaperColumns; x++)
                turningVertices[y * (PaperColumns + 1) + x] = turningFilter.transform.InverseTransformPoint(
                    MapTurningPage(new Vector2((float)x / PaperColumns, (float)y / PaperRows)));
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
            ink.group.alpha = visible ? 1 : 0;
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
        if (animatedInk == null) animatedInk = surface.GetComponentsInChildren<Graphic>(true);
        foreach (var graphic in animatedInk)
        {
            if (graphic == null) continue;
            if (graphic is TMP_Text text)
            {
                var printed = text.GetComponent<BookPagePrintedGraphic>();
                if (printed != null) printed.RefreshProjection();
            }
            else graphic.SetVerticesDirty();
        }
    }

    public void EndTurn()
    {
        turning = false;
        ReleaseTurningMesh();
        if (surface == null) return;
        surface.GetComponent<CanvasGroup>().blocksRaycasts = true;
        animatedInk = null;
        RefreshPages();
    }

    private void ReleaseTurningMesh()
    {
        if (turningFilter != null && originalTurningMesh != null) turningFilter.sharedMesh = originalTurningMesh;
        if (turningMesh != null)
        {
            if (Application.isPlaying) Destroy(turningMesh); else DestroyImmediate(turningMesh);
        }
        turningMesh = null; turningVertices = null;
    }
}
