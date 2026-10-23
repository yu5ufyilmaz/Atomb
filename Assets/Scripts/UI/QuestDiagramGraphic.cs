using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Çözüm kimliklerini gizleyerek yalnız hedef topolojiyi gösterir.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public class QuestDiagramGraphic : MaskableGraphic
{
    private SynthesisQuestData quest;

    public void SetQuest(SynthesisQuestData data)
    {
        quest = data;
        raycastTarget = false;
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        if (quest != null && quest.nodes != null)
            for (int i = 0; i < quest.nodes.Count; i++)
            {
                var node = quest.nodes[i];
                var obj = new GameObject("Şema Yuvası", typeof(RectTransform), typeof(TextMeshProUGUI));
                obj.transform.SetParent(transform, false);
                var text = obj.GetComponent<TextMeshProUGUI>();
                var formula = ProceduralSynthesisGenerator.Instance != null ? ProceduralSynthesisGenerator.Instance.ResolveFormula(node.itemID) : null;
                text.text = node.isUnknown ? "[ ? ]" : "[" + (formula != null ? formula.itemName : node.itemID) + "]";
                text.fontSize = 13;
                text.color = new Color(0.1f, 0.12f, 0.1f);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(84, 26);
                text.rectTransform.anchoredPosition = Position(i);
            }
        SetVerticesDirty();
    }

    private Vector2 Position(int index)
    {
        // Yerleşim seed'inden bağımsız daire, küçük kartlarda çakışmayı önler.
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
