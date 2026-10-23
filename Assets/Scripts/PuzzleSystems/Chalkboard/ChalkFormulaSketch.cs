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
            connection.alignment = LineAlignment.TransformZ;
            connection.shadowCastingMode = ShadowCastingMode.Off;
            connection.receiveShadows = false;
            Ring(node.transform, Vector3.zero, 0.19f, seed);
        }
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
