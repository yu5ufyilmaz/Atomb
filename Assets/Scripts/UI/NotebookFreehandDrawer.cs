using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public class NotebookFreehandDrawer : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Kalem ve Silgi")]
    [SerializeField, Min(0.5f)] private float penWidth = 3f;
    [SerializeField, Min(1f)] private float eraserRadius = 14f;
    [SerializeField, Min(0.5f)] private float minimumPointDistance = 1.5f;
    [SerializeField] private Color penColor = new Color(0.12f, 0.13f, 0.15f);

    public List<NotebookStrokeData> Strokes { get; private set; } = new List<NotebookStrokeData>();
    public event Action Changed;

    public NotebookTool Tool
    {
        get { return tool; }
        set
        {
            if (tool == value)
                return;
            tool = value;
            EndStroke();
            RefreshRaycast();
        }
    }

    private NotebookTool tool = NotebookTool.Pen;
    private NotebookStrokeData activeStroke;
    private NotebookStrokeGraphic activeGraphic;
    private bool erasing;
    private Vector2 previousEraserPoint;
    private readonly List<NotebookStrokeGraphic> graphics = new List<NotebookStrokeGraphic>();
    // Her UI ağı Unity'nin tepe noktası sınırının altında kalır; yeni çizimler yeni ağlara bölünür.
    private const int PointsPerGraphic = 4000;

    private bool IsPaused => NotebookWorkspaceController.Instance != null && NotebookWorkspaceController.Instance.IsPaused;

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshRaycast();
    }

    protected override void OnDisable()
    {
        EndStroke();
        base.OnDisable();
    }

    private void Update()
    {
        RefreshRaycast();
        if (IsPaused)
            EndStroke();
    }

    private void RefreshRaycast()
    {
        raycastTarget = tool != NotebookTool.Select && !IsPaused;
    }

    public void SetStrokes(List<NotebookStrokeData> strokes)
    {
        EndStroke();
        Strokes = new List<NotebookStrokeData>();
        if (strokes != null)
            foreach (var stroke in strokes)
                if (stroke != null)
                    Strokes.Add(stroke.Clone());
        RebuildGraphics();
    }

    public void Clear()
    {
        EndStroke();
        Strokes.Clear();
        RebuildGraphics();
        Changed?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsPaused || eventData.button == PointerEventData.InputButton.Middle || tool == NotebookTool.Select
            || !TryGetLocalPoint(eventData, out var point))
            return;
        BeginStroke(point, eventData.button == PointerEventData.InputButton.Right || tool == NotebookTool.Eraser);
    }

    public void ConfigurePen(Color color, float width)
    {
        penColor = color;
        penWidth = width;
    }

    // Dünya yüzeyleri aynı çizim/silgi geometrisini kendi yerel koordinatlarıyla kullanır.
    public void BeginStroke(Vector2 point, bool erase)
    {
        if (IsPaused) return;
        EndStroke();
        if (erase)
        {
            erasing = true;
            previousEraserPoint = point;
            Erase(point, point);
            return;
        }
        activeStroke = new NotebookStrokeData { color = penColor, width = penWidth };
        activeStroke.points.Add(Normalize(point));
        Strokes.Add(activeStroke);
        activeGraphic = AddGraphic(activeStroke, 0);
        Changed?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsPaused || !TryGetLocalPoint(eventData, out var point))
        {
            if (IsPaused)
                EndStroke();
            return;
        }
        ContinueStroke(point);
    }

    public void ContinueStroke(Vector2 point)
    {
        if (IsPaused) { EndStroke(); return; }
        if (erasing)
        {
            Erase(previousEraserPoint, point);
            previousEraserPoint = point;
            return;
        }
        if (activeStroke == null)
            return;
        Vector2 normalized = Normalize(point);
        if ((Denormalize(activeStroke.points[activeStroke.points.Count - 1]) - point).sqrMagnitude
            < minimumPointDistance * minimumPointDistance)
            return;
        activeStroke.points.Add(normalized);
        if (activeStroke.points.Count - activeGraphic.StartPoint > PointsPerGraphic)
            activeGraphic = AddGraphic(activeStroke, activeStroke.points.Count - 2);
        activeGraphic.SetVerticesDirty();
        Changed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Middle)
            EndStroke();
    }

    public void EndStroke()
    {
        activeStroke = null;
        activeGraphic = null;
        erasing = false;
    }

    protected virtual bool TryGetLocalPoint(PointerEventData eventData, out Vector2 point)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out point))
            return false;
        Rect rect = rectTransform.rect;
        point = new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        return rect.width > 0f && rect.height > 0f;
    }

    private Vector2 Normalize(Vector2 point)
    {
        Rect rect = rectTransform.rect;
        return new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x), Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
    }

    private Vector2 Denormalize(Vector2 point)
    {
        Rect rect = rectTransform.rect;
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, point.x), Mathf.Lerp(rect.yMin, rect.yMax, point.y));
    }

    private void Erase(Vector2 from, Vector2 to)
    {
        bool changed = false;
        for (int i = Strokes.Count - 1; i >= 0; i--)
        {
            NotebookStrokeData stroke = Strokes[i];
            if (stroke == null || stroke.points == null || stroke.points.Count == 0)
                continue;
            float radius = eraserRadius + Mathf.Max(0.5f, stroke.width) * 0.5f;
            bool hit = PointSegmentDistanceSquared(Denormalize(stroke.points[0]), from, to) <= radius * radius;
            for (int j = 1; !hit && j < stroke.points.Count; j++)
                hit = SegmentDistanceSquared(from, to, Denormalize(stroke.points[j - 1]), Denormalize(stroke.points[j])) <= radius * radius;
            if (!hit)
                continue;
            Strokes.RemoveAt(i);
            changed = true;
        }
        if (changed)
        {
            RebuildGraphics();
            Changed?.Invoke();
        }
    }

    private static float PointSegmentDistanceSquared(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float t = segment.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / segment.sqrMagnitude) : 0f;
        return (point - (from + segment * t)).sqrMagnitude;
    }

    private static float SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Vector2 ab = b - a;
        Vector2 cd = d - c;
        float denominator = ab.x * cd.y - ab.y * cd.x;
        if (Mathf.Abs(denominator) > 0.000001f)
        {
            Vector2 ac = c - a;
            float t = (ac.x * cd.y - ac.y * cd.x) / denominator;
            float u = (ac.x * ab.y - ac.y * ab.x) / denominator;
            if (t >= 0f && t <= 1f && u >= 0f && u <= 1f)
                return 0f;
        }
        return Mathf.Min(Mathf.Min(PointSegmentDistanceSquared(a, c, d), PointSegmentDistanceSquared(b, c, d)),
            Mathf.Min(PointSegmentDistanceSquared(c, a, b), PointSegmentDistanceSquared(d, a, b)));
    }

    private NotebookStrokeGraphic AddGraphic(NotebookStrokeData stroke, int start)
    {
        var strokeObject = new GameObject("Kalem İzi", typeof(RectTransform));
        strokeObject.transform.SetParent(transform, false);
        var graphic = strokeObject.AddComponent<NotebookStrokeGraphic>();
        graphic.rectTransform.anchorMin = Vector2.zero;
        graphic.rectTransform.anchorMax = Vector2.one;
        graphic.rectTransform.offsetMin = Vector2.zero;
        graphic.rectTransform.offsetMax = Vector2.zero;
        graphic.raycastTarget = false;
        graphic.Stroke = stroke;
        graphic.StartPoint = start;
        graphic.PointLimit = PointsPerGraphic;
        graphic.SetVerticesDirty();
        ConfigureGraphic(graphic);
        graphics.Add(graphic);
        return graphic;
    }

    protected virtual void ConfigureGraphic(MaskableGraphic graphic) { }

    private void RebuildGraphics()
    {
        foreach (var graphic in graphics)
        {
            if (graphic == null)
                continue;
            graphic.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(graphic.gameObject);
            else DestroyImmediate(graphic.gameObject);
        }
        graphics.Clear();
        foreach (var stroke in Strokes)
        {
            if (stroke.points == null || stroke.points.Count == 0)
                continue;
            for (int start = 0; start < stroke.points.Count; start += PointsPerGraphic - 1)
            {
                AddGraphic(stroke, start);
                if (start + PointsPerGraphic >= stroke.points.Count)
                    break;
            }
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        // Sayfa giriş katmanıdır; izler ayrı ağlarda olduğundan sayfa uzadıkça vertex sınırına takılmaz.
        vh.Clear();
    }
}

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
internal sealed class NotebookStrokeGraphic : MaskableGraphic
{
    internal NotebookStrokeData Stroke;
    internal int StartPoint;
    internal int PointLimit;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Stroke == null || Stroke.points == null || Stroke.points.Count == 0)
            return;
        Rect rect = rectTransform.rect;
        float halfWidth = Mathf.Max(0.5f, Stroke.width) * 0.5f;
        int end = Mathf.Min(Stroke.points.Count, StartPoint + PointLimit);
        for (int i = StartPoint; i < end; i++)
        {
            Vector2 point = LocalPoint(Stroke.points[i], rect);
            // Küçük kare uçlar tek noktalı işaretleri ve keskin kalem dönüşlerini de görünür kılar.
            Quad(vh, point + new Vector2(-halfWidth, -halfWidth), point + new Vector2(-halfWidth, halfWidth),
                point + new Vector2(halfWidth, halfWidth), point + new Vector2(halfWidth, -halfWidth));
            if (i == StartPoint)
                continue;
            Vector2 previous = LocalPoint(Stroke.points[i - 1], rect);
            Vector2 delta = point - previous;
            if (delta.sqrMagnitude < 0.000001f)
                continue;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * halfWidth;
            Quad(vh, previous - normal, previous + normal, point + normal, point - normal);
        }
    }

    private static Vector2 LocalPoint(Vector2 normalized, Rect rect)
    {
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, normalized.x), Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));
    }

    private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a, Stroke.color, Vector2.zero);
        vh.AddVert(b, Stroke.color, Vector2.zero);
        vh.AddVert(c, Stroke.color, Vector2.zero);
        vh.AddVert(d, Stroke.color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
