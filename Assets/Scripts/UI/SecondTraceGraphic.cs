using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small drawn records shared by the physical book, notebook and pinned board notes.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class SecondTraceGraphic : MaskableGraphic
{
    private string kind;
    private float main = 1, echo = .5f;
    private readonly Color ink = new Color(.12f, .19f, .20f);

    public void Configure(string diagram, float first = 1, float second = .5f)
    {
        kind = diagram; main = first; echo = second; raycastTarget = false;
        if (diagram == "target") { Label("KAYIT", new Vector2(.12f, .77f)); Label("HEDEF", new Vector2(.12f, .27f)); }
        else if (diagram == "baseline" || diagram == "echo" || diagram == "mainloss")
        {
            Label("UYA", new Vector2(.10f, .72f));
            Label("NUM", new Vector2(.43f, .72f));
            Label("ALG", new Vector2(.88f, .72f));
            Label("REF", new Vector2(.68f, .16f));
            if (diagram == "mainloss") Label("SÖN", new Vector2(.26f, .72f));
        }
        else if (diagram.StartsWith("part/")) Label(diagram.Substring(5), new Vector2(.5f, .5f));
        SetVerticesDirty();
    }

    public static bool IsVisualNote(NotebookBlockData note) => note != null
        && (SignalTrialGraphic.IsNote(note) || (note.itemID ?? "").StartsWith("trace-result/")
            || ((note.itemID ?? "").StartsWith("trace-note/") && note.itemID != "trace-note/"));

    public static void Decorate(NotebookDraggableBlock block, TMP_Text title, TMP_Text description)
    {
        if (SignalTrialGraphic.IsNote(block.Data)) { SignalTrialGraphic.Decorate(block, description); return; }
        var note = block.Data;
        bool part = (note.itemID ?? "").StartsWith("trace-") && note.blockType == NotebookBlockType.PartClue;
        if (!part && !IsVisualNote(note)) return;
        description.rectTransform.anchorMin = Vector2.zero;
        description.rectTransform.anchorMax = new Vector2(1, .24f);
        description.rectTransform.offsetMin = new Vector2(10, 4);
        description.rectTransform.offsetMax = new Vector2(-10, 0);
        description.enableAutoSizing = true; description.fontSizeMin = 11; description.fontSizeMax = 13;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;
        var footer = block.transform.Find("Bulmaca Etiketi");
        if (footer != null) footer.gameObject.SetActive(false);
        AddNoteGraphic((RectTransform)block.transform, note, new Vector2(.04f, .28f), new Vector2(.96f, .73f));
    }

    public static void AddNoteGraphic(RectTransform parent, NotebookBlockData note, Vector2 min, Vector2 max)
    {
        if (SignalTrialGraphic.IsNote(note)) { SignalTrialGraphic.Add(parent, note, min, max); return; }
        var graphic = parent.GetComponentInChildren<SecondTraceGraphic>();
        if (graphic != null) return;
        graphic = new GameObject("Çizili Deney Kaydı", typeof(RectTransform), typeof(SecondTraceGraphic)).GetComponent<SecondTraceGraphic>();
        graphic.transform.SetParent(parent, false);
        graphic.rectTransform.anchorMin = min; graphic.rectTransform.anchorMax = max;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        string id = note.itemID ?? "";
        if (id.StartsWith("trace-result/"))
        {
            var values = id.Split('/');
            float.TryParse(values.Length > 1 ? values[1] : "0", out float first);
            float.TryParse(values.Length > 2 ? values[2] : "0", out float second);
            graphic.Configure("reading", first / 100, second / 100);
        }
        else if (id.StartsWith("trace-note/")) graphic.Configure(id.Substring("trace-note/".Length));
        else graphic.Configure("part/" + (id == SecondTraceExperiment.Source ? "UYA" : id == SecondTraceExperiment.Sample ? "NUM"
            : id == SecondTraceExperiment.Detector ? "ALG" : id == SecondTraceExperiment.Reference ? "REF" : "SÖN"));
    }

    private void Label(string text, Vector2 anchor)
    {
        var label = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(transform, false);
        label.text = text; label.fontSize = 13; label.color = ink;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
        label.rectTransform.sizeDelta = new Vector2(90, 24);
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (string.IsNullOrEmpty(kind)) return;
        if (kind == "target") { Wave(vh, .31f, .96f, .55f, .37f, 1, .5f); Wave(vh, .31f, .96f, .05f, .37f, 1, 0); }
        else if (kind == "reading") Wave(vh, .04f, .96f, .12f, .76f, main, echo);
        else if (kind == "absorber")
        {
            Wave(vh, .02f, .39f, .15f, .7f, 1, .5f);
            Line(vh, new Vector2(.44f, .50f), new Vector2(.57f, .50f));
            Line(vh, new Vector2(.49f, .20f), new Vector2(.49f, .80f), 3);
            Line(vh, new Vector2(.53f, .20f), new Vector2(.53f, .80f), 3);
            Wave(vh, .63f, .98f, .15f, .7f, .2f, .1f);
        }
        else if (kind == "threshold")
        {
            Wave(vh, .05f, .95f, .12f, .76f, 1, .08f);
            for (float x = .05f; x < .95f; x += .07f) Line(vh, new Vector2(x, .23f), new Vector2(x + .035f, .23f), 1);
        }
        else if (kind.StartsWith("part/"))
        {
            Circle(vh, new Vector2(.5f, .5f), .18f);
            Line(vh, new Vector2(.12f, .5f), new Vector2(.30f, .5f));
            Line(vh, new Vector2(.70f, .5f), new Vector2(.88f, .5f));
            Line(vh, new Vector2(.5f, .02f), new Vector2(.5f, .18f));
            Line(vh, new Vector2(.5f, .82f), new Vector2(.5f, .98f));
        }
        else
        {
            var u = new Vector2(.10f, .72f); var n = new Vector2(.43f, .72f);
            var d = new Vector2(.88f, .72f); var r = new Vector2(.68f, .16f);
            // Leave room for names instead of drawing wires through the letters.
            if (kind == "mainloss")
            {
                Line(vh, new Vector2(.17f, .72f), new Vector2(.19f, .72f));
                Line(vh, new Vector2(.33f, .72f), new Vector2(.36f, .72f));
                Box(vh, new Vector2(.26f, .72f));
            }
            else Line(vh, u + Vector2.right * .07f, n - Vector2.right * .07f);
            Line(vh, n + Vector2.right * .07f, d - Vector2.right * .07f);
            Line(vh, n + new Vector2(.035f, -.12f), r + new Vector2(-.03f, .12f));
            Line(vh, r + new Vector2(.05f, .12f), d + new Vector2(-.035f, -.12f));
            Box(vh, u); Box(vh, n); Box(vh, d); Box(vh, r);
        }
    }

    private void Wave(VertexHelper vh, float left, float right, float bottom, float height, float first, float second)
    {
        Line(vh, new Vector2(left, bottom), new Vector2(right, bottom), 1);
        Vector2 previous = new Vector2(left, bottom);
        for (int i = 1; i <= 100; i++)
        {
            float t = i / 100f;
            float y = first * Mathf.Exp(-Mathf.Pow((t - .29f) / .055f, 2)) + second * Mathf.Exp(-Mathf.Pow((t - .72f) / .07f, 2));
            var point = new Vector2(Mathf.Lerp(left, right, t), bottom + Mathf.Min(y, 1.15f) * height);
            Line(vh, previous, point, 2); previous = point;
        }
    }

    private void Circle(VertexHelper vh, Vector2 center, float radius)
    {
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= 28; i++)
        { var point = center + new Vector2(Mathf.Cos(i * Mathf.PI / 14), Mathf.Sin(i * Mathf.PI / 14)) * radius; Line(vh, previous, point); previous = point; }
    }

    private void Box(VertexHelper vh, Vector2 center)
    {
        var half = new Vector2(.07f, .17f);
        Line(vh, center - half, center + new Vector2(half.x, -half.y), 1.5f);
        Line(vh, center + new Vector2(half.x, -half.y), center + half, 1.5f);
        Line(vh, center + half, center + new Vector2(-half.x, half.y), 1.5f);
        Line(vh, center + new Vector2(-half.x, half.y), center - half, 1.5f);
    }

    private void Line(VertexHelper vh, Vector2 a, Vector2 b, float thickness = 2.5f)
    {
        Rect rect = rectTransform.rect;
        a = rect.min + Vector2.Scale(a, rect.size); b = rect.min + Vector2.Scale(b, rect.size);
        var normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * .5f;
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, ink, Vector2.zero); vh.AddVert(a + normal, ink, Vector2.zero);
        vh.AddVert(b + normal, ink, Vector2.zero); vh.AddVert(b - normal, ink, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start + 2, start + 3, start);
    }
}
