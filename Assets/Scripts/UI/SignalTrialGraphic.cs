using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class SignalTrialGraphic : MaskableGraphic
{
    private string kind;
    private List<SignalTrial.Pulse> pulses;
    private Rect drawingRect;
    private readonly Color ink = new Color(.12f, .19f, .20f);
    public static bool IsNote(NotebookBlockData note) => note != null && (MixtureGraphic.IsNote(note) || SignalTrial.IsPart(note.itemID)
        || (note.itemID ?? "").StartsWith("signal-result") || (note.itemID ?? "").StartsWith("signal-target")
        || ((note.itemID ?? "").StartsWith("signal-note/") && note.itemID != "signal-note/"));

    public void Configure(string data)
    {
        kind = data ?? ""; raycastTarget = false;
        if (kind == "target") pulses = new List<SignalTrial.Pulse>(ResearchPuzzleTrial.Active?.CurrentPuzzle?.target ?? PuzzleDefinition.Legacy().target);
        else if (kind.StartsWith("signal-result") || kind.StartsWith("signal-target")) pulses = SignalTrial.Decode(kind);
        if (kind == "samples") { Label("N-12", new Vector2(.12f, .75f)); Label("N-34", new Vector2(.12f, .25f)); }
        SetVerticesDirty();
    }

    public static void Decorate(NotebookDraggableBlock block, TMP_Text description)
    {
        if (!IsNote(block.Data)) return;
        description.rectTransform.anchorMin = Vector2.zero; description.rectTransform.anchorMax = new Vector2(1, .25f);
        description.rectTransform.offsetMin = new Vector2(9, 3); description.rectTransform.offsetMax = new Vector2(-9, 0);
        description.enableAutoSizing = true; description.fontSizeMin = 11; description.fontSizeMax = 13;
        description.textWrappingMode = TextWrappingModes.Normal; description.overflowMode = TextOverflowModes.Overflow;
        var footer = block.transform.Find("Bulmaca Etiketi"); if (footer != null) footer.gameObject.SetActive(false);
        Add((RectTransform)block.transform, block.Data, new Vector2(.04f, .29f), new Vector2(.96f, .72f));
    }

    public static void Add(RectTransform parent, NotebookBlockData note, Vector2 min, Vector2 max)
    {
        if (MixtureGraphic.IsNote(note)) { MixtureGraphic.Add(parent, note, min, max); return; }
        if (parent.GetComponentInChildren<SignalTrialGraphic>() != null) return;
        var graphic = new GameObject("Deney Çizimi", typeof(RectTransform), typeof(SignalTrialGraphic)).GetComponent<SignalTrialGraphic>();
        graphic.transform.SetParent(parent, false);
        graphic.rectTransform.anchorMin = min; graphic.rectTransform.anchorMax = max;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        string data = note.itemID ?? "";
        graphic.Configure(data.StartsWith("signal-note/") ? data.Substring(12) : data);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if (kind == null) return;
        drawingRect = rectTransform.rect;
        if (pulses != null)
        {
            Rect plot = ComparisonPlot(rectTransform.rect.size);
            Plot(vh, pulses, plot.xMin, plot.xMax, plot.yMin, plot.height, 2.1f); return;
        }
        if (SignalTrial.IsPart(kind))
        {
            float width = Mathf.Min(drawingRect.width, drawingRect.height * 2);
            drawingRect = new Rect(drawingRect.center - new Vector2(width, width / 2) * .5f, new Vector2(width, width / 2));
            Part(vh, kind); return;
        }
        if (kind == "samples")
        {
            Plot(vh, One(.45f, 1, .025f), .28f, .97f, .52f, .43f, 1.2f);
            Plot(vh, One(.45f, 1, .075f), .28f, .97f, .02f, .43f, 1.2f); return;
        }
        if (kind == "coincident")
        { Plot(vh, new List<SignalTrial.Pulse> { new SignalTrial.Pulse { time = .4f, height = 2, width = .03f } }, .05f, .97f, .06f, .90f, 2.1f); return; }
        if (kind == "attenuate" || kind == "delay" || kind == "beforeafter")
        {
            Plot(vh, One(.35f, 1, .035f), .02f, .40f, .10f, .82f, 1.2f);
            Arrow(vh, new Vector2(.45f, .5f), new Vector2(.55f, .5f));
            Plot(vh, One(kind == "delay" ? .67f : .35f, kind == "delay" ? 1 : .5f, .035f), .60f, .98f, .10f, .82f, 1.2f); return;
        }
        if (kind == "split") { Part(vh, SignalTrial.Splitter); return; }
        Circle(vh, new Vector2(.15f, .5f), .12f);
        Arrow(vh, new Vector2(.33f, .5f), new Vector2(.82f, .5f));
    }

    // The same axes and aspect ratio on targets, measurements, books and notebook copies.
    // Fit inside the card rather than stretching the waveform to fill its available space.
    public static Rect ComparisonPlot(Vector2 size)
    {
        float width = Mathf.Min(size.x * .92f, size.y * .84f * 3f);
        float height = width / 3f;
        return new Rect((1f - width / Mathf.Max(1, size.x)) * .5f,
            (1f - height / Mathf.Max(1, size.y)) * .5f,
            width / Mathf.Max(1, size.x), height / Mathf.Max(1, size.y));
    }

    private void Part(VertexHelper vh, string id)
    {
        int inputs = SignalTrial.Inputs(id), outputs = SignalTrial.Outputs(id);
        for (int i = 0; i < inputs; i++) Circle(vh, new Vector2(.07f, inputs == 2 ? .3f + i * .4f : .5f), .07f);
        for (int i = 0; i < outputs; i++) Arrow(vh, new Vector2(.81f, outputs == 2 ? .3f + i * .4f : .5f), new Vector2(.95f, outputs == 2 ? .3f + i * .4f : .5f));
        if (id == SignalTrial.Source)
        { Circle(vh, new Vector2(.47f, .5f), .32f); Plot(vh, One(.4f, 1, .04f), .34f, .60f, .28f, .44f, 1.2f); }
        else if (SignalPartLibrary.Find(id)?.sample == true)
        {
            var part = SignalPartLibrary.Find(id);
            var traces = One(part.repeats == 2 ? .25f : .45f + part.delay, part.heightFactor, part.width > 0 ? part.width : .045f);
            if (part.repeats == 2) traces.Add(new SignalTrial.Pulse { time = .25f + part.repeatInterval, height = part.heightFactor, width = .045f });
            Box(vh, .23f, .80f, .14f, .87f); Plot(vh, traces, .29f, .73f, .25f, .50f, 1.2f);
        }
        else if (id == SignalTrial.Splitter)
        { Line(vh, .18f, .5f, .48f, .5f); Arrow(vh, new Vector2(.48f, .5f), new Vector2(.76f, .3f)); Arrow(vh, new Vector2(.48f, .5f), new Vector2(.76f, .7f)); }
        else if (id == SignalTrial.Delay)
        { Circle(vh, new Vector2(.5f, .5f), .34f); Line(vh, .5f, .77f, .5f, .5f); Line(vh, .5f, .5f, .68f, .5f); }
        else if (id == SignalTrial.Attenuator)
        { Line(vh, .22f, .5f, .33f, .77f); Line(vh, .33f, .77f, .44f, .23f); Line(vh, .44f, .23f, .55f, .77f); Line(vh, .55f, .77f, .68f, .23f); Line(vh, .68f, .23f, .79f, .5f); }
        else
        { Box(vh, .23f, .82f, .13f, .88f); Plot(vh, One(.4f, 1, .04f), .30f, .76f, .23f, .53f, 1.2f); }
    }

    private static List<SignalTrial.Pulse> One(float time, float height, float width) => new List<SignalTrial.Pulse> {
        new SignalTrial.Pulse { time = time, height = height, width = width } };
    private void Plot(VertexHelper vh, List<SignalTrial.Pulse> values, float left, float right, float bottom, float height, float max)
    {
        Line(vh, left, bottom, right, bottom, 1);
        for (int i = 1; i <= 4; i++)
        { float x = Mathf.Lerp(left, right, i / 5f); Line(vh, x, bottom, x, bottom + height, .5f, new Color(.12f, .19f, .2f, .14f)); }
        var previous = new Vector2(left, bottom);
        for (int i = 1; i <= 160; i++)
        {
            float time = i / 160f, y = 0;
            foreach (var p in values) y += p.height * Mathf.Exp(-Mathf.Pow((time - p.time) / Mathf.Max(.01f, p.width), 2));
            var point = new Vector2(Mathf.Lerp(left, right, time), bottom + y / max * height);
            Segment(vh, previous, point, 2, ink); previous = point;
        }
    }
    private void Label(string text, Vector2 at)
    {
        var label = new GameObject(text, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); label.transform.SetParent(transform, false);
        label.text = text; label.fontSize = 12; label.color = ink; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = at; label.rectTransform.sizeDelta = new Vector2(75, 24);
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }
    private void Arrow(VertexHelper vh, Vector2 from, Vector2 to)
    {
        Segment(vh, from, to, 2, ink);
        var d = (to - from).normalized; var n = new Vector2(-d.y, d.x);
        Segment(vh, to, to - d * .055f + n * .065f, 2, ink); Segment(vh, to, to - d * .055f - n * .065f, 2, ink);
    }
    private void Box(VertexHelper vh, float left, float right, float bottom, float top)
    { Line(vh, left, bottom, right, bottom); Line(vh, right, bottom, right, top); Line(vh, right, top, left, top); Line(vh, left, top, left, bottom); }
    private void Circle(VertexHelper vh, Vector2 center, float radius)
    {
        float ratio = drawingRect.height / Mathf.Max(1, drawingRect.width);
        Vector2 previous = center + new Vector2(radius * ratio, 0);
        for (int i = 1; i <= 30; i++)
        { float t = i * Mathf.PI / 15; var p = center + new Vector2(Mathf.Cos(t) * radius * ratio, Mathf.Sin(t) * radius); Segment(vh, previous, p, 2, ink); previous = p; }
    }
    private void Line(VertexHelper vh, float ax, float ay, float bx, float by, float width = 2, Color? tint = null) =>
        Segment(vh, new Vector2(ax, ay), new Vector2(bx, by), width, tint ?? ink);
    private void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Rect r = drawingRect; a = r.min + Vector2.Scale(a, r.size); b = r.min + Vector2.Scale(b, r.size);
        var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f; int start = vh.currentVertCount;
        vh.AddVert(a - normal, tint, Vector2.zero); vh.AddVert(a + normal, tint, Vector2.zero);
        vh.AddVert(b + normal, tint, Vector2.zero); vh.AddVert(b - normal, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start + 2, start + 3, start);
    }
}
