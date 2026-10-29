using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The same swatch and quantities appear on books, targets and immutable measurement cards.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class MixtureGraphic : MaskableGraphic
{
    private Color tint = Color.white;
    private float fill = .7f;
    public static bool IsNote(NotebookBlockData note) => note != null && (ColorMixtureLibrary.Find(note.itemID) != null
        || (note.itemID ?? "").StartsWith("mixture-target/") || (note.itemID ?? "").StartsWith("mixture-result/"));

    public static void Add(RectTransform parent, NotebookBlockData note, Vector2 min, Vector2 max)
    {
        if (parent.GetComponentInChildren<MixtureGraphic>() != null) return;
        var graphic = new GameObject("Karışım Örneği", typeof(RectTransform), typeof(MixtureGraphic)).GetComponent<MixtureGraphic>();
        graphic.transform.SetParent(parent, false); graphic.raycastTarget = false;
        graphic.rectTransform.anchorMin = min; graphic.rectTransform.anchorMax = max;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        var mixture = MixtureResult.Decode(note.itemID);
        graphic.tint = mixture != null ? mixture.Swatch : ColorMixtureLibrary.Tint(note.itemID);
        graphic.fill = mixture != null ? Mathf.Lerp(.18f, .83f, mixture.Concentration) : .7f;
        var label = new GameObject("Karışım Değerleri", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(graphic.transform, false); label.raycastTarget = false;
        label.rectTransform.anchorMin = new Vector2(.36f, .06f); label.rectTransform.anchorMax = new Vector2(.98f, .95f);
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.color = new Color(.12f, .22f, .2f); label.enableAutoSizing = true; label.fontSizeMin = 9; label.fontSizeMax = 17;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        if (mixture != null)
            GameLocalization.SetFormat(label, "Yoğunluk: {0:P0} · Miktar: {1:0.##} doz\nPigment K/Y/M: {2:P0} / {3:P0} / {4:P0}",
                mixture.Concentration, mixture.Volume, mixture.Ratios.x, mixture.Ratios.y, mixture.Ratios.z);
        else GameLocalization.Set(label, ColorMixtureLibrary.Find(note.itemID)?.symbol ?? "KARIŞIM");
        graphic.SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect r = rectTransform.rect;
        void Box(float x, float y, float w, float h, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(r.min + Vector2.Scale(new Vector2(x, y), r.size), color, Vector2.zero);
            vh.AddVert(r.min + Vector2.Scale(new Vector2(x, y + h), r.size), color, Vector2.zero);
            vh.AddVert(r.min + Vector2.Scale(new Vector2(x + w, y + h), r.size), color, Vector2.zero);
            vh.AddVert(r.min + Vector2.Scale(new Vector2(x + w, y), r.size), color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index + 2, index + 3, index);
        }
        var ink = new Color(.16f, .27f, .25f);
        Box(.06f, .12f, .24f, .72f, ink); Box(.075f, .15f, .21f, .67f, new Color(.94f, .96f, .94f));
        Box(.08f, .16f, .2f, .64f * fill, tint);
        Box(.04f, .83f, .28f, .035f, ink);
        for (int i = 1; i < 4; i++) Box(.08f, .16f + i * .15f, .035f, .014f, ink);
    }
}
