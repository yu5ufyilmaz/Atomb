using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Geçici çizim: model yerine tahtanın XY düzleminde kalan, kırık ve pürüzlü tebeşir izleri.
[RequireComponent(typeof(DraggableFormula))]
public sealed class ChalkFormulaSketch : MonoBehaviour
{
    [SerializeField] private Material chalkMaterial;
    private const string DrawingName = "Tebeşir Eskizi";

    public void Draw(FormulaItemSO data)
    {
        var formula = GetComponent<DraggableFormula>();
        Transform drawing = transform.Find(DrawingName);
        if (drawing != null)
        {
            formula.formulaLabel = drawing.GetComponentInChildren<TextMeshPro>();
            return;
        }
        drawing = new GameObject(DrawingName).transform;
        drawing.SetParent(transform, false);
        if (data != null && SignalTrial.IsPart(data.itemID))
        {
            DrawSignalPart(formula, drawing, data);
            return;
        }
        int hash = 17;
        foreach (char c in data != null ? data.itemID ?? "" : "") hash = unchecked(hash * 31 + c);
        float seed = (hash & 65535) * 0.013f;
        // Birkaç açıdan çizilmiş atom/deney şeması; bilimsel bir model değil, okunur bir eskiz.
        for (int orbit = 0; orbit < 3; orbit++)
        {
            float angle = (orbit * 60f + seed % 12f) * Mathf.Deg2Rad;
            var stroke = new List<Vector3>();
            for (int i = 0; i <= 64; i++)
            {
                float t = i * Mathf.PI * 2f / 64f;
                float x = Mathf.Cos(t) * 1.12f, y = Mathf.Sin(t) * 0.45f;
                float rough = (Mathf.PerlinNoise(seed + orbit, i * 1.7f) - 0.5f) * 0.055f;
                stroke.Add(new Vector3(x * Mathf.Cos(angle) - y * Mathf.Sin(angle) + rough,
                    x * Mathf.Sin(angle) + y * Mathf.Cos(angle) - rough, 0));
                // Küçük boşluklar kuru tebeşirin kesintili izini verir.
                if (i == 64 || (i > 0 && i % 13 == 10))
                {
                    Stroke(drawing, stroke, 0.05f);
                    stroke.Clear();
                }
            }
        }
        Ring(drawing, Vector3.zero, 0.13f, seed);
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.PI * 2f / 3f + seed;
            Ring(drawing, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0), 0.075f, seed + i);
        }

        var labelObject = new GameObject("Parça Adı");
        labelObject.transform.SetParent(drawing, false);
        labelObject.transform.localPosition = new Vector3(0, -1.45f, 0);
        // board.forward oyuncuya bakar; TMP'nin ön yüzünü de oyuncuya çevir.
        labelObject.transform.localRotation = Quaternion.Euler(0, 180, -2);
        var label = labelObject.AddComponent<TextMeshPro>();
        label.text = data != null ? data.itemName : "";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 5;
        label.enableAutoSizing = true;
        label.fontSizeMin = 3;
        label.fontSizeMax = 5;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(3.2f, 0.5f);
        label.color = new Color(0.88f, 0.9f, 0.84f);
        label.raycastTarget = false;
        formula.formulaLabel = label;

        foreach (var node in formula.GetOwnNodes())
        {
            var connection = node.GetComponent<LineRenderer>();
            connection.sharedMaterial = chalkMaterial;
            connection.widthMultiplier = 0.004f;
            connection.widthCurve = AnimationCurve.Constant(0, 1, 1);
            // Bağ noktaları dünya uzayındadır; View, bağlı parçaların yerel dönüşümünden etkilenmez.
            connection.alignment = LineAlignment.View;
            connection.shadowCastingMode = ShadowCastingMode.Off;
            connection.receiveShadows = false;
            Ring(node.transform, Vector3.zero, 0.19f, seed);
        }
    }

    private void DrawSignalPart(DraggableFormula formula, Transform drawing, FormulaItemSO data)
    {
        string id = data.itemID;
        void Path(params Vector3[] points) => Stroke(drawing, new List<Vector3>(points), .055f);
        void Box(float x, float y) => Path(new Vector3(-x, -y), new Vector3(x, -y), new Vector3(x, y), new Vector3(-x, y), new Vector3(-x, -y));
        if (id == SignalTrial.Source)
        {
            Ring(drawing, Vector3.zero, .72f, 0);
            Path(new Vector3(.4f, -.1f), new Vector3(.05f, -.1f), new Vector3(.05f, .4f), new Vector3(-.2f, .4f), new Vector3(-.2f, -.1f), new Vector3(-.5f, -.1f));
        }
        else if (id == SignalTrial.Narrow || id == SignalTrial.Wide)
        {
            Path(new Vector3(-.45f, .85f), new Vector3(-.45f, .4f), new Vector3(-.78f, -.65f), new Vector3(.78f, -.65f), new Vector3(.45f, .4f), new Vector3(.45f, .85f));
            Path(new Vector3(-.65f, -.2f), new Vector3(.65f, -.2f));
            if (id == SignalTrial.Wide) Path(new Vector3(-.4f, -.45f), new Vector3(.4f, -.45f));
        }
        else if (id == SignalTrial.Splitter)
            Path(new Vector3(1, 0), Vector3.zero, new Vector3(-1, .65f));
        else if (id == SignalTrial.Delay)
        {
            Ring(drawing, Vector3.zero, .70f, 0);
            Path(new Vector3(0, .5f), Vector3.zero, new Vector3(-.4f, 0));
        }
        else if (id == SignalTrial.Attenuator)
            Path(new Vector3(.9f, 0), new Vector3(.6f, .5f), new Vector3(.25f, -.5f), new Vector3(-.1f, .5f), new Vector3(-.45f, -.5f), new Vector3(-.8f, 0));
        else
        {
            Box(.9f, .65f);
            Path(new Vector3(.65f, -.25f), new Vector3(.25f, -.25f), new Vector3(.05f, .35f), new Vector3(-.15f, -.25f), new Vector3(-.65f, -.25f));
        }
        if (id == SignalTrial.Splitter) Path(Vector3.zero, new Vector3(-1, -.65f));
        foreach (var node in formula.GetOwnNodes())
        {
            if (!node.gameObject.activeSelf) continue;
            var line = node.GetComponent<LineRenderer>(); line.sharedMaterial = chalkMaterial;
            line.alignment = LineAlignment.View; line.widthMultiplier = .004f;
            Vector3 p = node.transform.localPosition;
            if (node.GetComponent<SignalPort>().output)
                Path(p + new Vector3(.18f, .18f), p + new Vector3(-.10f, 0), p + new Vector3(.18f, -.18f));
            else Ring(drawing, p, .17f, 0);
        }
        var text = new GameObject("Parça Adı", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        text.transform.SetParent(drawing, false); text.transform.localPosition = new Vector3(0, -1.15f, 0);
        text.transform.localRotation = Quaternion.Euler(0, 180, 0);
        text.text = data.itemName; text.fontSize = 4; text.enableAutoSizing = true; text.fontSizeMin = 2.5f; text.fontSizeMax = 4;
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(3.3f, .5f); text.color = new Color(.88f, .9f, .84f); text.raycastTarget = false;
        formula.formulaLabel = text;
    }

    private void Ring(Transform parent, Vector3 center, float radius, float seed)
    {
        var points = new List<Vector3>();
        for (int i = 0; i <= 24; i++)
        {
            float t = i * Mathf.PI * 2f / 24f;
            float r = radius * (1f + (Mathf.PerlinNoise(seed, i) - 0.5f) * 0.14f);
            points.Add(center + new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0));
        }
        Stroke(parent, points, 0.045f);
    }

    private void Stroke(Transform parent, List<Vector3> points, float width)
    {
        if (points.Count < 2) return;
        var line = new GameObject("Tebeşir İzi", typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.sharedMaterial = chalkMaterial;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.TransformZ;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.35f, 1), new Keyframe(0.65f, 0.6f), new Keyframe(1, 0.85f));
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }
}
