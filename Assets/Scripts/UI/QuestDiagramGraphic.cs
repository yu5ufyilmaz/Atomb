using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Çözüm kimliklerini gizleyerek yalnız hedef topolojiyi gösterir.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public class QuestDiagramGraphic : MaskableGraphic
{
    private SynthesisQuestData quest;
    private readonly List<RectTransform> nodeLabels = new List<RectTransform>();
    private readonly List<(RectTransform rect, SynthesisBondData bond)> bondLabels = new List<(RectTransform, SynthesisBondData)>();

    public void SetQuest(SynthesisQuestData data)
    {
        quest = data;
        nodeLabels.Clear();
        bondLabels.Clear();
        raycastTarget = false;
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        int unknown = 0;
        if (quest != null && quest.nodes != null)
            for (int i = 0; i < quest.nodes.Count; i++)
            {
                var node = quest.nodes[i];
                var obj = new GameObject("Şema Yuvası", typeof(RectTransform), typeof(TextMeshProUGUI));
                obj.transform.SetParent(transform, false);
                var text = obj.GetComponent<TextMeshProUGUI>();
                var formula = ProceduralSynthesisGenerator.Instance != null ? ProceduralSynthesisGenerator.Instance.ResolveFormula(node.itemID) : null;
                text.text = node.isUnknown ? "[ ?" + (++unknown) + " ]" : "[" + (formula != null ? formula.itemName : node.itemID) + "]";
                text.fontSize = 13;
                text.color = new Color(0.1f, 0.12f, 0.1f);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(84, 26);
                text.rectTransform.anchoredPosition = Position(i);
                nodeLabels.Add(text.rectTransform);
            }
        if (quest != null && quest.bonds != null)
            foreach (var bond in quest.bonds)
            {
                if (string.IsNullOrEmpty(bond.label) || bond.fromIndex < 0 || bond.toIndex < 0
                    || bond.fromIndex >= quest.nodes.Count || bond.toIndex >= quest.nodes.Count) continue;
                var label = new GameObject("Bağ İpucu", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(transform, false);
                label.text = bond.label;
                label.fontSize = 12;
                label.color = new Color(0.1f, 0.12f, 0.1f);
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(55, 22);
                label.rectTransform.anchoredPosition = (Position(bond.fromIndex) + Position(bond.toIndex)) * 0.5f + Vector2.up * 14;
                bondLabels.Add((label.rectTransform, bond));
            }
        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (quest == null) return;
        for (int i = 0; i < nodeLabels.Count; i++)
            if (nodeLabels[i] != null) nodeLabels[i].anchoredPosition = Position(i);
        foreach (var label in bondLabels)
            if (label.rect != null) label.rect.anchoredPosition =
                (Position(label.bond.fromIndex) + Position(label.bond.toIndex)) * 0.5f + Vector2.up * 14;
    }

    private Vector2 Position(int index)
    {
        if (quest.nodes.Exists(n => n.position.sqrMagnitude > 0.0001f))
            return Vector2.Scale(quest.nodes[index].position,
                new Vector2(rectTransform.rect.width * 0.5f - 44, rectTransform.rect.height * 0.5f - 18));
        float angle = Mathf.PI * 0.5f + index * Mathf.PI * 2 / Mathf.Max(1, quest.nodes.Count);
        return new Vector2(Mathf.Cos(angle) * (rectTransform.rect.width * 0.5f - 44), Mathf.Sin(angle) * (rectTransform.rect.height * 0.5f - 18));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (quest == null || quest.nodes == null || quest.bonds == null) return;
        foreach (var bond in quest.bonds)
        {
            if (bond.fromIndex < 0 || bond.toIndex < 0 || bond.fromIndex >= quest.nodes.Count || bond.toIndex >= quest.nodes.Count) continue;
            Vector2 a = Position(bond.fromIndex), b = Position(bond.toIndex);
            Vector2 direction = (b - a).normalized;
            a += direction * 26;
            b -= direction * 26;
            Vector2 offset = new Vector2(-direction.y, direction.x) * 2.5f;
            Line(vh, a + (bond.bondType == BondType.Double ? offset : Vector2.zero), b + (bond.bondType == BondType.Double ? offset : Vector2.zero), 1.5f);
            if (bond.bondType == BondType.Double) Line(vh, a - offset, b - offset, 1.5f);
            if (bond.directed)
            {
                Line(vh, b, b - direction * 8 + new Vector2(-direction.y, direction.x) * 4, 1.5f);
                Line(vh, b, b - direction * 8 - new Vector2(-direction.y, direction.x) * 4, 1.5f);
            }
        }
    }

    private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        int n = vh.currentVertCount;
        Color32 lineColor = new Color(0.18f, 0.2f, 0.17f);
        vh.AddVert(a - normal, lineColor, Vector2.zero);
        vh.AddVert(a + normal, lineColor, Vector2.zero);
        vh.AddVert(b + normal, lineColor, Vector2.zero);
        vh.AddVert(b - normal, lineColor, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n + 2, n + 3, n);
    }
}
