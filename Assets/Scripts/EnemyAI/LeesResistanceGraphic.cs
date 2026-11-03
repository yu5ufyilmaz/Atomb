using System.Collections.Generic;
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
            Color edge = pressured ? new Color(.45f, .025f, .018f, .15f + danger * .35f) : new Color(0, 0, 0, .65f); // lighter: the veins carry the pull
            if (side == 0) Quad(mesh, P(0, 0), P(.21f, 0), P(.21f, 1), P(0, 1), edge, Color.clear, Color.clear, edge);
            else Quad(mesh, P(.79f, 0), P(1, 0), P(1, 1), P(.79f, 1), Color.clear, edge, edge, Color.clear);
        }
        Quad(mesh, P(0, 0), P(1, 0), P(1, .3f), P(0, .3f), new Color(0, 0, 0, .85f), new Color(0, 0, 0, .85f), Color.clear, Color.clear);
        Quad(mesh, P(0, .82f), P(1, .82f), P(1, 1), P(0, 1), Color.clear, Color.clear, new Color(0, 0, 0, .65f), new Color(0, 0, 0, .65f));
        if (!active) return;
        // Veins under the skin instead of straight strokes (3 Nov playtest): they creep in with danger
        // and swell on a lub-dub heartbeat that quickens; the pulled side is strong, the other faint.
        float rate = 1.1f + danger * 1.4f, phase = Mathf.Repeat(clock * rate, 1);
        float beat = Mathf.Exp(-Mathf.Pow(phase / .06f, 2)) + .55f * Mathf.Exp(-Mathf.Pow((phase - .22f) / .07f, 2));
        float growth = .35f + .65f * danger;
        for (int side = 0; side < 2; side++)
        {
            bool pulled = side == 1 ? direction > 0 : direction < 0;
            float strength = pulled ? 1 : .35f * danger;
            if (strength > .01f) Veins(mesh, side, growth * (pulled ? 1 : .6f), strength * (.55f + .3f * danger) * (.8f + .35f * beat), beat);
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
    // One branching network per side, grown once in height units (x inward from the edge, y up) with a fixed seed.
    sealed class Vein { public readonly List<Vector2> points = new List<Vector2>(); public readonly List<float> reach = new List<float>(), width = new List<float>(); }
    static readonly List<Vein>[] networks = new List<Vein>[2];
    const float FullReach = .5f;

    static List<Vein> Network(int side)
    {
        if (networks[side] != null) return networks[side];
        var veins = new List<Vein>(); var rng = new System.Random(side == 0 ? 7193 : 4261);
        foreach (float y in new[] { .16f, .3f, .43f, .56f, .7f, .84f })
            Grow(veins, rng, new Vector2(0, y + (float)(rng.NextDouble() - .5) * .06f), (float)(rng.NextDouble() - .5) * .9f, .01f, 0, 0);
        return networks[side] = veins;
    }

    static void Grow(List<Vein> veins, System.Random rng, Vector2 at, float heading, float width, float reach, int depth)
    {
        var vein = new Vein(); veins.Add(vein);
        vein.points.Add(at); vein.reach.Add(reach); vein.width.Add(width);
        int steps = depth == 0 ? 15 : 10 - depth * 2;
        for (int i = 0; i < steps; i++)
        {
            // Wander, but keep heading inward (0 rad = straight in).
            heading = Mathf.Clamp(heading + (float)(rng.NextDouble() - .5) * .55f, -1.2f, 1.2f);
            float length = (.022f + (float)rng.NextDouble() * .018f) * (1 - depth * .12f);
            at += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * length; reach += length; width *= .9f;
            vein.points.Add(at); vein.reach.Add(reach); vein.width.Add(width);
            if (depth < 3 && rng.NextDouble() < .3f)
                Grow(veins, rng, at, heading + (rng.NextDouble() < .5 ? -1 : 1) * (.45f + (float)rng.NextDouble() * .5f), width * .62f, reach, depth + 1);
        }
    }

    static readonly List<Vector2> points = new List<Vector2>();
    static readonly List<float> widths = new List<float>(), fades = new List<float>();

    void Veins(VertexHelper mesh, int side, float growth, float alpha, float beat)
    {
        Rect r = rectTransform.rect; float h = r.height;
        float limit = growth * FullReach;
        foreach (var vein in Network(side))
        {
            if (vein.reach[0] >= limit) continue;
            points.Clear(); widths.Clear(); fades.Clear();
            for (int i = 0; i < vein.points.Count; i++)
            {
                Vector2 v = vein.points[i]; float width = vein.width[i], fade = 1 - vein.reach[i] / limit;
                bool tip = vein.reach[i] > limit;
                // The growing tip: cut the last segment where the growth front is.
                if (tip) { v = Vector2.Lerp(vein.points[i - 1], v, Mathf.InverseLerp(vein.reach[i - 1], vein.reach[i], limit)); width = fade = 0; }
                points.Add(new Vector2(side == 0 ? r.xMin + v.x * h : r.xMax - v.x * h, r.yMin + v.y * h));
                widths.Add(width * h); fades.Add(fade);
                if (tip) break;
            }
            if (points.Count < 2) continue;
            float swell = 1 + .3f * beat;
            // A soft halo under the skin, then the dark core.
            Strip(mesh, 2.8f * swell, new Color(.38f, .03f, .04f, alpha * .13f));
            Strip(mesh, swell, new Color(.28f, .015f, .04f, alpha * .95f)); // venous: dark, slightly blue-red
        }
    }

    static void Strip(VertexHelper mesh, float scale, Color color)
    {
        int start = mesh.currentVertCount;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[Mathf.Max(0, i - 1)], b = points[Mathf.Min(points.Count - 1, i + 1)];
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * widths[i] * scale * .5f;
            Vector2 p = points[i];
            Color c = color; c.a *= Mathf.Clamp01(fades[i] * 1.6f);
            mesh.AddVert(p - normal, c, Vector2.zero); mesh.AddVert(p + normal, c, Vector2.zero);
            if (i > 0) { int k = start + i * 2; mesh.AddTriangle(k - 2, k - 1, k + 1); mesh.AddTriangle(k - 2, k + 1, k); }
        }
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

