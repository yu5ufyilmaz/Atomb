using UnityEngine;
using UnityEngine.UI;

/// <summary>Code-drawn peripheral pressure, neck angle and counter-direction cues.</summary>
public sealed class LeesResistanceGraphic : MaskableGraphic
{
    [HideInInspector] public bool active;
    [HideInInspector] public float danger, progress, angle, pull, clock;
    [HideInInspector] public int direction;
    readonly Color bone = new Color(.77f, .78f, .72f, .65f);
    readonly Color red = new Color(.82f, .12f, .09f);
    public void Refresh() { SetVerticesDirty(); }
    Vector2 P(float x, float y) { return new Vector2(rectTransform.rect.xMin + x * rectTransform.rect.width, rectTransform.rect.yMin + y * rectTransform.rect.height); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        // Gradient vignette leaves the center completely transparent.
        for (int side = 0; side < 2; side++)
        {
            bool pressured = active && (side == 1 ? direction > 0 : direction < 0);
            Color edge = pressured ? new Color(.45f, .025f, .018f, .2f + danger * .5f) : new Color(0, 0, 0, .65f);
            if (side == 0) Quad(mesh, P(0, 0), P(.21f, 0), P(.21f, 1), P(0, 1), edge, Color.clear, Color.clear, edge);
            else Quad(mesh, P(.79f, 0), P(1, 0), P(1, 1), P(.79f, 1), Color.clear, edge, edge, Color.clear);
        }
        Quad(mesh, P(0, 0), P(1, 0), P(1, .3f), P(0, .3f), new Color(0, 0, 0, .85f), new Color(0, 0, 0, .85f), Color.clear, Color.clear);
        Quad(mesh, P(0, .82f), P(1, .82f), P(1, 1), P(0, 1), Color.clear, Color.clear, new Color(0, 0, 0, .65f), new Color(0, 0, 0, .65f));
        if (!active) return;
        float pulse = .7f + .3f * Mathf.Sin(clock * 8);
        Color stress = new Color(red.r, red.g, red.b, (.3f + .5f * danger) * pulse);
        for (int i = 0; i < 14; i++)
        {
            float y = .1f + i * .061f;
            float reach = .022f + .035f * Mathf.Abs(Mathf.Sin(i * 4.31f)) + danger * .045f;
            float x = direction < 0 ? .008f : .992f;
            float inward = direction < 0 ? 1 : -1;
            Line(mesh, P(x, y), P(x + inward * reach, y + .014f), 1.5f, stress);
            Line(mesh, P(x + inward * reach, y + .014f), P(x + inward * (reach + .02f), y - .009f), 1, stress * new Color(1, 1, 1, .5f));
        }
        // Neck axis: middle is the original direction, red ends are the failure limits.
        Line(mesh, P(.34f, .17f), P(.66f, .17f), 1, bone * new Color(1, 1, 1, .45f));
        for (int i = -6; i <= 6; i++)
        {
            float x = .5f + i * .025f;
            Line(mesh, P(x, .166f), P(x, i == 0 ? .19f : .179f), 1, i == -6 || i == 6 ? red : bone);
        }
        float marker = .5f + Mathf.Clamp(angle, -1, 1) * .15f;
        Line(mesh, P(marker, .155f), P(marker, .192f), 3, danger > .7f ? red : bone);
        Line(mesh, P(.34f, .085f), P(.66f, .085f), 2, new Color(.4f, .43f, .4f, .4f));
        Line(mesh, P(.34f, .085f), P(.34f + .32f * progress, .085f), 2, bone);
        // The prominent arrow always shows the PLAYER'S counter-direction.
        float sign = -direction;
        float ax = .5f + sign * .112f;
        for (int i = 0; i < 2; i++)
        {
            float x = ax + sign * i * .012f;
            Line(mesh, P(x - sign * .009f, .351f), P(x, .365f), 2, bone);
            Line(mesh, P(x, .365f), P(x - sign * .009f, .379f), 2, bone);
        }
        // A small head and shoulder outline responds to the actual forced angle.
        Vector2 center = P(.5f, .46f); float radius = rectTransform.rect.height * .016f;
        Vector2 last = center + Vector2.right * radius;
        for (int i = 1; i <= 32; i++)
        {
            float a = i / 32f * Mathf.PI * 2;
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Line(mesh, last, next, 1.4f, bone); last = next;
        }
        Vector2 nose = new Vector2(Mathf.Sin(angle * 1.2f), Mathf.Cos(angle * 1.2f));
        Line(mesh, center + nose * radius, center + nose * radius * 1.65f, 2, bone);
        Line(mesh, P(.478f, .432f), P(.522f, .432f), 1.4f, bone);
    }
    static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
        Quad(mesh, a - n, b - n, b + n, a + n, c, c, c, c);
    }
    static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, ca, Vector2.zero); mesh.AddVert(b, cb, Vector2.zero);
        mesh.AddVert(c, cc, Vector2.zero); mesh.AddVert(d, cd, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}

