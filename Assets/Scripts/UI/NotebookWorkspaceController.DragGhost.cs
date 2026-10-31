using TMPro;
using UnityEngine;

// Eğitim rehberi: kartın hayaleti ya da bir işaret, oyuncudan beklenen hareketi canlandırır.
// Sıradaki hareketi ChalkboardTutorial seçer; burası yalnız gösterir.
public partial class NotebookWorkspaceController
{
    private const float GhostCycle = 3.2f, GhostAlpha = .6f;
    private static readonly Color GuideColor = new Color(1f, .85f, .3f);
    private NotebookDraggableBlock ghostCard;
    private RectTransform ghost, ghostLine;
    private CanvasGroup ghostGroup;
    private string ghostLabel;
    private Vector3 ghostFrom, ghostTo;
    private float ghostTime;

    public void ShowBookExitLocked() => ShowFeedback("Önce tüm notları deftere sürükle.", false);

    internal static Vector3 ScreenPoint(Vector3 world)
    {
        Vector3 point = Camera.main.WorldToScreenPoint(world);
        point.z = 0;
        return point;
    }

    private static bool OnWorldCanvas(NotebookDraggableBlock card) =>
        card.GetComponentInParent<Canvas>().renderMode == RenderMode.WorldSpace;

    internal void ClearGuide()
    {
        if (ghost == null) return;
        RemoveObject(ghost.gameObject);
        ghost = ghostLine = null;
        ghostCard = null;
    }

    internal void GuideBook(InteractableBook book)
    {
        if (!IsActive) return;
        if (ghostCard != null && !IsArchived(ghostCard.Data)) { GuideCardToNotebook(ghostCard); return; }
        foreach (var note in book.GetComponentsInChildren<NotebookDraggableBlock>())
            if (note.IsSource && !IsArchived(note.Data)) { GuideCardToNotebook(note); return; }
        ClearGuide();
    }

    internal void GuideCardToNotebook(NotebookDraggableBlock card)
    {
        if (notebookExpanded) GuideCard(card, notebookWindow.position, "DEFTERE SÜRÜKLE");
        else GuideOpenNotebook();
    }

    // shortcut: kart aynı bölümün başka sayfasındaysa yine sekmeyi gösterir; ders kartları ilk sayfada toplandığı için yeterli.
    internal void GuideNote(System.Predicate<NotebookBlockData> match, string tab, Vector3 to, string label)
    {
        if (!IsActive) return;
        if (!notebookExpanded) { GuideOpenNotebook(); return; }
        foreach (var card in notebookWindow.GetComponentsInChildren<NotebookDraggableBlock>())
            if (match(card.Data)) { GuideCard(card, to, label); return; }
        Vector3 point = page.parent.Find(tab).position;
        GuideMark(point, point, "BU BÖLÜMÜ AÇ");
    }

    internal void GuideMark(Vector3 from, Vector3 to, string label, bool line = false)
    {
        if (!IsActive) return;
        if (ghost == null || ghostCard != null || ghostLabel != label)
        {
            ClearGuide();
            ghost = Panel("Eğitim Rehberi", bounds, Vector2.one, Color.clear);
            ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            if (line)
            {
                ghostLine = Panel("Rehber Çizgisi", ghost, new Vector2(0, 5), GuideColor);
                ghostLine.pivot = new Vector2(1, .5f);
            }
            var dot = Label(ghost, "●", Vector2.zero, Vector2.one * 40, 30);
            dot.font = TMP_Settings.defaultFontAsset;
            dot.color = GuideColor;
            // Etiket ekranın içine doğru açılır; sağ kenardaki sekmelerde taşmasın.
            bool rightSide = from.x > Screen.width * .5f;
            StartGhost(label, new Vector2(.5f, .5f), new Vector2(rightSide ? 1 : 0, .5f), new Vector2(rightSide ? -24 : 24, 0), 270);
        }
        ghostFrom = from;
        ghostTo = to;
    }

    private void GuideCard(NotebookDraggableBlock card, Vector3 to, string label)
    {
        if (!IsActive) return;
        bool world = OnWorldCanvas(card);
        if (ghost == null || ghostCard != card || ghostLabel != label)
        {
            ClearGuide();
            ghost = card.CreateFloatingCopy(DragCanvas, world);
            ghostCard = card;
            StartGhost(label, new Vector2(.5f, 1), new Vector2(.5f, 0), Vector2.zero, ghost.sizeDelta.x);
        }
        ghostFrom = world ? ScreenPoint(card.transform.position) : card.transform.position;
        ghostTo = to;
    }

    private void GuideOpenNotebook()
    {
        var point = new Vector3(Screen.width * .8f, Screen.height * (notebookOpenPosition.y > 0 ? .7f : .25f));
        GuideMark(point, point, "[{input:ToggleNotebook}] DEFTERİ AÇ");
    }

    private void StartGhost(string label, Vector2 anchor, Vector2 pivot, Vector2 offset, float width)
    {
        ghostGroup = ghost.GetComponent<CanvasGroup>();
        ghostGroup.alpha = 0;
        ghostTime = 0;
        ghostLabel = label;
        var tag = Panel("Rehber Etiketi", ghost, new Vector2(width, 34), ink);
        tag.anchorMin = tag.anchorMax = anchor;
        tag.pivot = pivot;
        tag.anchoredPosition = offset;
        Label(tag, label, Vector2.zero, tag.sizeDelta, 16).color = paper;
    }

    private void UpdateDragGhost(float delta)
    {
        if (ghost == null) return;
        // Oyuncu tuttuğu anda rehber çekilir; gerçek hareketle yarışmasın.
        if (Input.GetMouseButton(0))
        {
            ghostTime = 0;
            ghostGroup.alpha = 0;
            return;
        }
        ghostTime = (ghostTime + delta) % GhostCycle;
        float t = ghostTime / GhostCycle;
        ghost.position = Vector3.Lerp(ghostFrom, ghostTo, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .8f, t)));
        ghostGroup.alpha = (ghostCard != null ? GhostAlpha : 1) * Mathf.InverseLerp(0, .15f, t) * Mathf.InverseLerp(.95f, .8f, t);
        if (ghostLine == null) return;
        Vector3 trail = ghost.position - ghostFrom;
        ghostLine.sizeDelta = new Vector2(trail.magnitude / bounds.lossyScale.x, 5);
        ghostLine.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(trail.y, trail.x) * Mathf.Rad2Deg);
    }
}
