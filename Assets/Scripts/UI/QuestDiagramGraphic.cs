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
    private static readonly Vector2[] ResearchLayout = { new Vector2(-.95f, .85f), new Vector2(0, .85f), new Vector2(.95f, 0),
        new Vector2(0, -.85f), new Vector2(.95f, -.85f), Vector2.zero };

    public void SetQuest(SynthesisQuestData data, bool revealSolution = false)
    {
        quest = !revealSolution && ResearchPuzzleTrial.Active != null ? ResearchPuzzleTrial.Active.DisplayQuest(data) : data;
        nodeLabels.Clear();
        bondLabels.Clear();
        raycastTarget = false;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        if (quest != null && quest.questID == SignalTrial.QuestID)
        {
            SignalTrialGraphic.Add(rectTransform, new NotebookBlockData { itemID = "signal-note/target" }, Vector2.zero, Vector2.one);
            quest = null; SetVerticesDirty(); return;
        }
        if (quest != null && quest.questID == SecondTraceExperiment.QuestID)
        {
            var trace = new GameObject("Aranan Ölçüm İzi", typeof(RectTransform), typeof(SecondTraceGraphic)).GetComponent<SecondTraceGraphic>();
            trace.transform.SetParent(transform, false);
            trace.rectTransform.anchorMin = Vector2.zero;
            trace.rectTransform.anchorMax = Vector2.one;
            trace.rectTransform.offsetMin = trace.rectTransform.offsetMax = Vector2.zero;
            trace.Configure("target");
            quest = null;
            SetVerticesDirty();
            return;
        }
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
                bool compactResearch = ResearchTrialContent.OwnsQuest(quest.questID);
                if (compactResearch && formula != null)
                    text.text = "[" + formula.itemName.Substring(0, Mathf.Min(3, formula.itemName.Length)).ToUpperInvariant() + "]";
                text.fontSize = 13;
                text.color = new Color(0.1f, 0.12f, 0.1f);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = compactResearch ? new Vector2(64, 30) : new Vector2(84, 26);
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
        if (quest.questID == ResearchTrialContent.SecondQuestID)
        {
            return Vector2.Scale(ResearchLayout[index], new Vector2(rectTransform.rect.width * .5f - 34, rectTransform.rect.height * .5f - 16));
        }
        if (ResearchTrialContent.OwnsQuest(quest.questID) && quest.bonds.Count == 0)
        {
            int rows = (quest.nodes.Count + 1) / 2;
            return new Vector2((index % 2 == 0 ? -1 : 1) * rectTransform.rect.width * .24f,
                ((rows - 1) * .5f - index / 2) * rectTransform.rect.height * .28f);
        }
        if (quest.nodes.Exists(n => n.position.sqrMagnitude > 0.0001f))
        {
            Vector2 position = quest.nodes[index].position;
            if (ResearchTrialContent.OwnsQuest(quest.questID))
                position = new Vector2(Mathf.Lerp(-.95f, .95f, Mathf.InverseLerp(-1.35f, .2f, position.x)), position.y / .7f);
            return Vector2.Scale(position,
                new Vector2(rectTransform.rect.width * 0.5f - 44, rectTransform.rect.height * 0.5f - 18));
        }
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
            float trim = Mathf.Min(42f / Mathf.Max(Mathf.Abs(direction.x), 0.001f),
                13f / Mathf.Max(Mathf.Abs(direction.y), 0.001f)) + 4f;
            if (ResearchTrialContent.OwnsQuest(quest.questID))
                trim = Mathf.Min(Mathf.Min(32f / Mathf.Max(Mathf.Abs(direction.x), .001f),
                    15f / Mathf.Max(Mathf.Abs(direction.y), .001f)) + 3f, Vector2.Distance(a, b) * .4f);
            a += direction * trim;
            b -= direction * trim;
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
