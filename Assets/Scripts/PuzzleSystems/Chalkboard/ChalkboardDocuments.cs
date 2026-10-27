using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Belge kağıtları ekranı değil fiziksel tahtanın sol kenarını takip eder.
public sealed partial class ChalkboardDocuments : MonoBehaviour
{
    private ChalkboardManager board;
    private RectTransform surface, targetArea, informationArea, informationContent;
    private CanvasGroup input;
    private TextMeshProUGUI title, hint, informationHeading;
    private QuestDiagramGraphic diagram;
    private const float PixelsPerUnit = 300f;
    public float WorkingMaxX => freeLayout ? board.maxBounds.x : surface != null
        ? surface.localPosition.x - surface.rect.width * surface.localScale.x * .5f - .18f : board.maxBounds.x;

    public void Initialize(ChalkboardManager manager)
    {
        board = manager;
        if (surface != null || board.boardTransform == null) return;
        if (ResearchPuzzleTrial.IsResearch(board)) { InitializeResearch(); return; }
        var obj = new GameObject("Tahtaya Takılı Belgeler", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        surface = (RectTransform)obj.transform;
        surface.SetParent(board.boardTransform, false);
        surface.sizeDelta = new Vector2(280, 640);
        surface.localScale = Vector3.one / PixelsPerUnit;
        // Formüller gibi +Z tahta önüdür; bu yüze bakan oyuncunun solu yerel +X olur.
        // Kayıt yüklenirken oyuncunun başka yöne bakması kağıtları ters çeviremez.
        surface.localPosition = new Vector3(board.maxBounds.x - .5f, (board.minBounds.y + board.maxBounds.y) * .5f, .035f);
        surface.localRotation = Quaternion.Euler(0, 180, 0);
        var canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main; canvas.sortingOrder = 5;
        input = obj.GetComponent<CanvasGroup>();

        var cork = Paper(surface, "Mantar Pano", Vector2.zero, new Vector2(296, 660), new Color(.53f, .34f, .19f));
        cork.GetComponent<Image>().raycastTarget = false;
        for (int i = 0; i < 36; i++)
        {
            var fleck = Paper(cork, "Mantar Dokusu", new Vector2((i * 83 % 280) - 140, (i * 137 % 640) - 320),
                new Vector2(3 + i % 3, 2), new Color(.28f, .17f, .09f, .3f));
            fleck.GetComponent<Image>().raycastTarget = false;
        }
        var stringToClues = Paper(surface, "Hedef Bilgi İpi", new Vector2(-126, 23), new Vector2(2, 57), new Color(.62f, .14f, .1f));
        stringToClues.GetComponent<Image>().raycastTarget = false;

        Paper(surface, "GÖREV", new Vector2(0, 301), new Vector2(276, 30), new Color(.16f, .2f, .16f));
        targetArea = Paper(surface, "Görev Kağıdı", new Vector2(0, 165), new Vector2(276, 236), new Color(.91f, .85f, .66f));
        title = Text(targetArea, "", new Vector2(0, 87), new Vector2(252, 45), 16);
        diagram = new GameObject("Tahtadaki Hedef Şema", typeof(RectTransform), typeof(QuestDiagramGraphic)).GetComponent<QuestDiagramGraphic>();
        diagram.transform.SetParent(targetArea, false);
        diagram.rectTransform.sizeDelta = new Vector2(262, 136);
        diagram.rectTransform.anchoredPosition = new Vector2(0, -6);
        hint = Text(targetArea, "", new Vector2(0, -90), new Vector2(254, 48), 13);
        var tack = Paper(targetArea, "Raptiye", new Vector2(0, 113), new Vector2(11, 11), new Color(.58f, .15f, .1f));
        tack.GetComponent<Image>().raycastTarget = false;
        var clueTack = Paper(targetArea, "Bağlantı Raptiyesi", new Vector2(-126, -110), new Vector2(7, 7), new Color(.62f, .14f, .1f));
        clueTack.GetComponent<Image>().raycastTarget = false;
        var heading = Paper(surface, "Bilgi Başlığı", new Vector2(0, 22), new Vector2(276, 30), new Color(.16f, .2f, .16f));
        informationHeading = Text(heading, "BİLGİLER / İPUÇLARI", Vector2.zero, new Vector2(270, 28), 16);
        informationHeading.color = new Color(.94f, .94f, .83f);
        informationArea = Paper(surface, "Tahta Bilgi Alanı", new Vector2(0, -153), new Vector2(276, 304), new Color(.58f, .4f, .24f, .8f));
        informationArea.gameObject.AddComponent<RectMask2D>();
        var scroll = informationArea.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.viewport = informationArea;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        informationContent = new GameObject("Sabit Bilgi Kağıtları", typeof(RectTransform)).GetComponent<RectTransform>();
        informationContent.SetParent(informationArea, false);
        informationContent.anchorMin = new Vector2(0, 1); informationContent.anchorMax = Vector2.one;
        informationContent.pivot = new Vector2(.5f, 1); scroll.content = informationContent;
        Text(surface, "GÖREV", new Vector2(0, 301), new Vector2(270, 28), 16).color = new Color(.94f, .94f, .83f);
        Refresh();
    }

    public void Refresh()
    {
        if (surface == null) return;
        if (freeLayout) { RefreshResearch(); return; }
        var quest = board.PinnedQuest;
        title.text = quest != null ? quest.title : "HEDEF NOTUNU BURAYA BIRAK";
        hint.text = quest != null ? board.practiceQuest != null ? board.practiceQuest.clueSummary : "Bilinmeyenleri bilgi notlarından çöz."
            : "Defter > Hedefler\nTopladığın şemayı buraya sürükle.";
        diagram.gameObject.SetActive(quest != null);
        if (quest != null) diagram.SetQuest(quest);
        for (int i = informationContent.childCount - 1; i >= 0; i--)
        {
            var child = informationContent.GetChild(i).gameObject; child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        float y = 10;
        int expected = quest != null ? PuzzleInformation.ExpectedCount(quest.questID, board) : 0;
        int found = quest != null ? PuzzleInformation.FoundCount(board.pinnedInformation, quest.questID, board) : 0;
        var knownNotes = PuzzleInformation.NotebookNotes(NotebookUI.Instance);
        knownNotes.AddRange(board.pinnedInformation);
        int known = quest != null ? PuzzleInformation.FoundCount(knownNotes, quest.questID, board) : 0;
        bool research = ResearchPuzzleTrial.IsResearch(board);
        if (research)
        {
            diagram.rectTransform.sizeDelta = new Vector2(262, 120);
            diagram.rectTransform.anchoredPosition = new Vector2(0, 2);
            hint.rectTransform.sizeDelta = new Vector2(254, 58);
            hint.rectTransform.anchoredPosition = new Vector2(0, -84);
            hint.fontSize = 11;
        }
        informationHeading.text = research ? "SABİTLEDİĞİN KAYITLAR" : quest != null ? "BİLGİ " + known + "/" + expected + "  |  PANODA " + found + "/" + expected : "HEDEF SEÇ / BİLGİLER";
        if (quest == null && board.pinnedInformation.Count == 0)
        {
            var empty = Text(informationContent, "Defter > Bilgiler\nİpuçlarını buraya sürükle.\n\nTekerlek: notları kaydır", new Vector2(0, -100), new Vector2(250, 150), 15);
            empty.color = new Color(.92f, .93f, .84f);
            empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(.5f, 1);
        }
        if (research)
        {
            foreach (var note in board.pinnedInformation) y = DrawInformation(note, -1, y);
        }
        else if (quest != null)
            for (int slot = 0; slot < expected; slot++)
                y = DrawInformation(PuzzleInformation.Find(board.pinnedInformation, quest.questID, slot, board), slot, y,
                    PuzzleInformation.Find(knownNotes, quest.questID, slot, board) != null);
        else
            foreach (var note in board.pinnedInformation) y = DrawInformation(note, -1, y);
        if (expected > 0)
        {
            var thread = Paper(informationContent, "İpucu Bağlantı İpi", Vector2.zero, new Vector2(2, Mathf.Max(1, y - 60)), new Color(.62f, .14f, .1f));
            thread.anchorMin = thread.anchorMax = thread.pivot = new Vector2(.5f, 1);
            thread.anchoredPosition = new Vector2(-126, 0); thread.GetComponent<Image>().raycastTarget = false;
            thread.SetAsFirstSibling();
        }
        informationContent.sizeDelta = new Vector2(0, Mathf.Max(304, y));
    }

    private float DrawInformation(NotebookBlockData note, int slot, float y, bool inNotebook = false)
    {
            bool missing = note == null;
            var branch = Paper(informationContent, "Bağlantı", Vector2.zero, new Vector2(18, 2), new Color(.62f, .14f, .1f));
            branch.anchorMin = branch.anchorMax = new Vector2(.5f, 1);
            branch.anchoredPosition = new Vector2(-117, -y - 13); branch.GetComponent<Image>().raycastTarget = false;
            var card = Paper(informationContent, (missing ? "Eksik İpucu " : "Sabit İpucu ") + (slot + 1), Vector2.zero,
                new Vector2(238, 120), missing ? new Color(.8f, .72f, .55f, .55f) : new Color(.78f, .86f, .88f));
            card.anchorMin = card.anchorMax = new Vector2(.5f, 1); card.pivot = new Vector2(.5f, 1);
            card.anchoredPosition = new Vector2(10, -y);
            var heading = Text(card, slot >= 0 ? "İPUCU " + (slot + 1) + (missing ? inNotebook ? " / DEFTERDE" : " / EKSİK" : " / BULUNDU") : note.title,
                new Vector2(-8, -18), new Vector2(204, 30), 14);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(.5f, 1);
            string description = missing ? inNotebook ? "Bu bilgi defterinde hazır.\nKartı buraya sürükle."
                : "Henüz bulunmadı.\nKitaplarda bu hedefin ipucunu ara." : note.description;
            var body = Text(card, description, new Vector2(0, -43), new Vector2(216, 90), 13);
            body.alignment = TextAlignmentOptions.TopLeft; body.enableAutoSizing = false;
            float height = Mathf.Max(112, body.GetPreferredValues(description ?? "", 216, 0).y + 56);
            card.sizeDelta = new Vector2(238, height);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(.5f, 1);
            body.rectTransform.pivot = new Vector2(.5f, 1);
            body.rectTransform.sizeDelta = new Vector2(216, height - 49);
            if (!missing && SignalTrialGraphic.IsNote(note))
            {
                card.sizeDelta = new Vector2(238, 180);
                body.rectTransform.anchoredPosition = new Vector2(0, -143);
                body.rectTransform.sizeDelta = new Vector2(216, 32);
                SignalTrialGraphic.Add(card, note, new Vector2(.04f, .24f), new Vector2(.96f, .77f));
                height = 180;
            }
            var tack = Paper(card, "Bilgi Raptiyesi", new Vector2(-108, -10), new Vector2(7, 7), new Color(.62f, .14f, .1f));
            tack.anchorMin = tack.anchorMax = new Vector2(.5f, 1); tack.GetComponent<Image>().raycastTarget = false;
            if (!missing)
            {
            var remove = Paper(card, "Notu Çıkar", new Vector2(106, -15), new Vector2(24, 25), new Color(.63f, .71f, .72f));
            remove.anchorMin = remove.anchorMax = new Vector2(.5f, 1);
            remove.gameObject.AddComponent<Button>().onClick.AddListener(() => board.RemoveInformation(note));
            Text(remove, "x", Vector2.zero, new Vector2(24, 25), 15);
            }
            return y + height + 12;
    }

    private void LateUpdate()
    {
        if (input == null) return;
        bool active = board.isMachineActive && (GameManager.Instance == null || !GameManager.Instance.isGamePaused);
        input.interactable = input.blocksRaycasts = active;
        if (freeLayout) { UpdateDocumentShade(); if (shadeReveal > 0) UpdateResearchThreads(); }
    }

    private void OnDestroy() { if (surface != null) { if (Application.isPlaying) Destroy(surface.gameObject); else DestroyImmediate(surface.gameObject); } }

    public bool ContainsPointer(Vector2 point)
    {
        if (surface == null || !board.isMachineActive || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return false;
        if (EventSystem.current == null) return RectTransformUtility.RectangleContainsScreenPoint(surface, point, Camera.main);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(surface);
    }

    public bool IsTargetArea(Vector2 point) => RectTransformUtility.RectangleContainsScreenPoint(targetArea, point, Camera.main);
    public bool IsInformationArea(Vector2 point) => RectTransformUtility.RectangleContainsScreenPoint(informationArea, point, Camera.main);

    private static RectTransform Paper(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)obj.transform; rect.SetParent(parent, false);
        rect.anchoredPosition = position; rect.sizeDelta = size; obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static TextMeshProUGUI Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var obj = new GameObject("Metin", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false); var text = obj.GetComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = text.fontSizeMax = fontSize; text.fontSizeMin = 11; text.enableAutoSizing = true;
        text.color = new Color(.12f, .15f, .13f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
        return text;
    }
}
