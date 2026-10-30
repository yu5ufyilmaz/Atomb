using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ChalkboardDocuments
{
    private bool freeLayout, loadingLayout;
    private RectTransform documentBoard, shadeViewport, shadeToggle, shadeBottomBar, goalDirectionsViewport;
    private CanvasGroup shadeInput;
    private TextMeshProUGUI shadeToggleLabel, targetToggleLabel;
    private bool shadeExpanded, targetDetailsOpen;
    private float shadeReveal;
    public bool IsDocumentPanelOpen => freeLayout && shadeExpanded;
    public Vector3 TargetSlot => targetArea.position;
    public Vector3 PanelHandle => shadeToggle.position;

    private readonly Dictionary<string, Vector2> documentPositions = new Dictionary<string, Vector2>();
    private readonly Dictionary<string, RectTransform> noteCards = new Dictionary<string, RectTransform>();
    private readonly Dictionary<string, RectTransform[]> noteThreads = new Dictionary<string, RectTransform[]>();

    private void InitializeResearch()
    {
        freeLayout = true;
        var obj = new GameObject("Raptiyeli Araştırma Kartları", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        surface = (RectTransform)obj.transform; surface.SetParent(board.boardTransform, false);
        surface.sizeDelta = (board.maxBounds - board.minBounds) * PixelsPerUnit;
        surface.localScale = Vector3.one / PixelsPerUnit;
        Vector2 center = (board.minBounds + board.maxBounds) * .5f;
        surface.localPosition = new Vector3(center.x, center.y, .045f);
        surface.localRotation = Quaternion.Euler(0, 180, 0);
        var canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main; canvas.sortingOrder = 5;
        input = obj.GetComponent<CanvasGroup>();
        BuildDocumentShade();
        // Hedef belge panosundan bağımsızdır: pano toplansa da sol üstteki yuvada görünür kalır.
        targetArea = PinnedCard("Hedef Kartı", new Color(.93f, .85f, .62f), false);
        targetArea.gameObject.AddComponent<Outline>().effectColor = new Color(.94f, .9f, .75f, .8f);
        targetArea.gameObject.AddComponent<Button>().onClick.AddListener(() => SetTargetDetailsOpen(!targetDetailsOpen));
        title = Text(targetArea, "", new Vector2(2, 49), new Vector2(184, 30), 14);
        title.fontStyle = FontStyles.Bold;
        targetToggleLabel = Text(targetArea, "", Vector2.zero, new Vector2(56, 22), 12);
        targetToggleLabel.fontStyle = FontStyles.Bold;
        diagram = new GameObject("Tahtadaki Hedef Şema", typeof(RectTransform), typeof(QuestDiagramGraphic)).GetComponent<QuestDiagramGraphic>();
        diagram.transform.SetParent(targetArea, false);
        diagram.rectTransform.sizeDelta = new Vector2(196, 66);
        goalDirectionsViewport = Paper(targetArea, "Hedef Kaynakları", new Vector2(0, -60), new Vector2(246, 110), Color.clear);
        goalDirectionsViewport.gameObject.AddComponent<RectMask2D>();
        var directions = goalDirectionsViewport.gameObject.AddComponent<ScrollRect>();
        directions.horizontal = false; directions.viewport = goalDirectionsViewport;
        directions.movementType = ScrollRect.MovementType.Clamped; directions.scrollSensitivity = 22;
        hint = Text(goalDirectionsViewport, "", Vector2.zero, new Vector2(246, 110), 12);
        hint.enableAutoSizing = false; hint.overflowMode = TextOverflowModes.Overflow;
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(.5f, 1);
        directions.content = hint.rectTransform;
        informationArea = Paper(documentBoard, "Kayıt Bırakma İpucu", new Vector2(documentBoard.rect.xMin + 125, documentBoard.rect.yMax - 200),
            new Vector2(218, 38), new Color(.63f, .45f, .29f, .85f));
        Text(informationArea, "KAYDI BU PANOYA BIRAK", Vector2.zero, new Vector2(208, 32), 12);
        RefreshResearch();
        SetDocumentPanelOpen(false, true);
    }

    private void BuildDocumentShade()
    {
        float width = Mathf.Min(740, surface.rect.width - 28), height = surface.rect.height - 56;
        shadeViewport = new GameObject("Stor Belge Panosu", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup)).GetComponent<RectTransform>();
        shadeViewport.SetParent(surface, false); shadeViewport.pivot = new Vector2(.5f, 1);
        shadeViewport.anchoredPosition = new Vector2(surface.rect.xMin + width / 2 + 14, surface.rect.yMax - 48);
        shadeViewport.sizeDelta = new Vector2(width, height);
        shadeInput = shadeViewport.GetComponent<CanvasGroup>();
        documentBoard = Paper(shadeViewport, "Bağımsız Belge Yüzeyi", new Vector2(0, -height / 2), new Vector2(width, height), new Color(.47f, .32f, .21f));
        documentBoard.anchorMin = documentBoard.anchorMax = new Vector2(.5f, 1);
        PhysicalBoardFixture.Attach(shadeViewport, "Belge Panosu", Vector2.down * height / 2,
            new Vector2(width, height), new Color(.22f, .18f, .13f));
        var border = documentBoard.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(.18f, .16f, .12f); border.effectDistance = new Vector2(3, -3);
        for (int i = 0; i < 65; i++)
        {
            var fleck = Paper(documentBoard, "Mantar Tanesi", new Vector2(i * 83 % (int)(width - 12) - width / 2 + 6,
                i * 137 % (int)(height - 12) - height / 2 + 6), new Vector2(2 + i % 4, 2), new Color(.24f, .15f, .08f, .3f));
            fleck.GetComponent<Image>().raycastTarget = false;
        }
        shadeBottomBar = Paper(shadeViewport, "Stor Alt Çıtası", Vector2.zero, new Vector2(width, 10), new Color(.22f, .23f, .2f));
        shadeBottomBar.anchorMin = shadeBottomBar.anchorMax = new Vector2(.5f, 1);
        shadeToggle = Paper(surface, "Belge Panosu Tutamağı", new Vector2(surface.rect.xMin + 422, surface.rect.yMax - 22),
            new Vector2(256, 30), new Color(.22f, .24f, .2f));
        shadeToggle.gameObject.AddComponent<Button>().onClick.AddListener(() => SetDocumentPanelOpen(!shadeExpanded));
        PhysicalBoardFixture.Attach(shadeToggle, "Belge Tutamağı", Vector2.zero, shadeToggle.sizeDelta, new Color(.18f, .22f, .18f));
        shadeToggleLabel = Text(shadeToggle, "BELGELERİ AÇ", Vector2.zero, new Vector2(244, 28), 14);
        shadeToggleLabel.color = new Color(.95f, .9f, .74f);
    }

    public void SetDocumentPanelOpen(bool open, bool immediate = false)
    {
        if (!freeLayout) return;
        shadeExpanded = open;
        GameLocalization.Set(shadeToggleLabel, open ? "BELGELERİ TOPLA" : "BELGELERİ AÇ");
        if (open) shadeViewport.gameObject.SetActive(true);
        if (immediate) shadeReveal = open ? 1 : 0;
        ApplyShadeReveal();
    }

    public void SetTargetDetailsOpen(bool open)
    {
        if (!freeLayout || board.PinnedQuest == null) return;
        targetDetailsOpen = open;
        RefreshResearch();
    }

    private void ApplyShadeReveal()
    {
        float amount = Mathf.SmoothStep(0, 1, shadeReveal);
        shadeViewport.sizeDelta = new Vector2(documentBoard.rect.width, documentBoard.rect.height * amount);
        shadeBottomBar.anchoredPosition = new Vector2(0, -shadeViewport.rect.height + 5);
        shadeInput.interactable = shadeInput.blocksRaycasts = shadeExpanded && shadeReveal >= 1;
        shadeViewport.gameObject.SetActive(shadeReveal > 0 || shadeExpanded);
    }

    private void UpdateDocumentShade()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused) return;
        float wanted = shadeExpanded ? 1 : 0;
        if (!Mathf.Approximately(shadeReveal, wanted))
        {
            shadeReveal = Mathf.MoveTowards(shadeReveal, wanted, Time.unscaledDeltaTime / .24f);
            ApplyShadeReveal();
        }
    }

    private RectTransform PinnedCard(string name, Color tint, bool movable = true)
    {
        var card = Paper(movable ? documentBoard : surface, name, Vector2.zero, new Vector2(218, 156), tint);
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .4f); shadow.effectDistance = new Vector2(3, -4);
        if (movable)
        {
            var drag = card.gameObject.AddComponent<DraggableWorkspaceItem>();
            drag.target = card; drag.workspaceBounds = documentBoard;
        }
        var pin = Text(card, "●", new Vector2(-93, 69), new Vector2(18, 18), 17);
        pin.name = "Raptiye"; pin.color = new Color(.68f, .12f, .08f);
        return card;
    }

    private void CaptureLayout()
    {
        foreach (var pair in noteCards) documentPositions[pair.Key] = pair.Value.anchoredPosition;
    }

    private void RefreshResearch()
    {
        if (!loadingLayout) CaptureLayout();
        loadingLayout = false;
        foreach (var card in noteCards.Values) RemoveResearchObject(card.gameObject);
        foreach (var links in noteThreads.Values) foreach (var line in links) RemoveResearchObject(line.gameObject);
        noteCards.Clear(); noteThreads.Clear();
        var quest = board.PinnedQuest;
        if (quest == null) targetDetailsOpen = false;
        targetArea.sizeDelta = new Vector2(270, targetDetailsOpen ? 246 : 42);
        // Açılan ayrıntı aşağı doğru büyür; kartın sol üst köşesi yuvada sabit kalır.
        targetArea.anchoredPosition = new Vector2(surface.rect.xMin + 149, surface.rect.yMax - 6 - targetArea.sizeDelta.y / 2);
        targetArea.GetComponent<Image>().color = quest != null ? new Color(.93f, .85f, .62f) : new Color(.2f, .23f, .19f, .7f);
        targetArea.GetComponent<Outline>().enabled = quest == null;
        var targetPin = targetArea.Find("Raptiye");
        targetPin.gameObject.SetActive(quest != null);
        targetPin.GetComponent<RectTransform>().anchoredPosition = new Vector2(-targetArea.rect.width / 2 + 16, targetArea.rect.height / 2 - 9);
        if (quest != null) GameLocalization.SetFormat(title, "HEDEF / {0}", quest.title);
        else GameLocalization.Set(title, "HEDEFİ BURAYA BIRAK");
        title.color = hint.color = quest != null ? new Color(.12f, .15f, .13f) : new Color(.94f, .9f, .75f);
        title.rectTransform.anchoredPosition = quest == null ? new Vector2(0, 8) : new Vector2(-26, targetDetailsOpen ? 100 : 0);
        title.rectTransform.sizeDelta = quest == null ? new Vector2(250, 22) : new Vector2(190, 34);
        targetToggleLabel.gameObject.SetActive(quest != null);
        GameLocalization.Set(targetToggleLabel, targetDetailsOpen ? "KAPAT" : "AÇ");
        targetToggleLabel.rectTransform.anchoredPosition = new Vector2(100, targetDetailsOpen ? 100 : 0);
        GameLocalization.Set(hint, quest != null ? board.practiceQuest?.clueSummary ?? "Kayıtları karşılaştır." : "Defter > Hedefler");
        goalDirectionsViewport.gameObject.SetActive(true);
        goalDirectionsViewport.anchoredPosition = new Vector2(0, quest != null ? -60 : -10);
        goalDirectionsViewport.sizeDelta = quest != null ? new Vector2(246, 110) : new Vector2(250, 18);
        hint.rectTransform.anchoredPosition = Vector2.zero;
        hint.rectTransform.sizeDelta = new Vector2(goalDirectionsViewport.rect.width,
            Mathf.Max(goalDirectionsViewport.rect.height, hint.GetPreferredValues(hint.text, goalDirectionsViewport.rect.width, 0).y + 2));
        hint.ForceMeshUpdate(true);
        hint.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(goalDirectionsViewport.rect.height, hint.preferredHeight + 2));
        hint.alignment = quest != null ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        goalDirectionsViewport.gameObject.SetActive(quest == null || targetDetailsOpen);
        diagram.rectTransform.sizeDelta = new Vector2(246, 66);
        diagram.rectTransform.anchoredPosition = new Vector2(0, 47);
        diagram.gameObject.SetActive(targetDetailsOpen);
        if (targetDetailsOpen) diagram.SetQuest(quest);
        informationArea.gameObject.SetActive(board.pinnedInformation.Count == 0);
        informationArea.anchoredPosition = new Vector2(quest != null ? documentBoard.rect.xMax - 125 : documentBoard.rect.xMin + 125,
            documentBoard.rect.yMax - (quest != null ? 90 : 200));
        int index = 0;
        foreach (var note in board.pinnedInformation)
        {
            if (string.IsNullOrEmpty(note.instanceID)) note.instanceID = Guid.NewGuid().ToString("N");
            string id = "note/" + note.instanceID;
            var card = PinnedCard("Kayıt / " + note.title, new Color(.79f, .86f, .86f));
            card.anchoredPosition = documentPositions.TryGetValue(id, out var position) ? position
                // Sağdan sola iki sıra; altı karttan sonrası kaydırılarak dizilir, böylece hiçbiri tam örtülmez.
                : new Vector2(documentBoard.rect.xMax - 125 - index / 2 % 3 * 238 - index / 6 * 16,
                    documentBoard.rect.yMax - 90 - index % 2 * 175 - index / 6 * 16);
            ClampDocument(card);
            var heading = Text(card, note.title, new Vector2(0, 49), new Vector2(180, 30), 14);
            heading.fontStyle = FontStyles.Bold;
            bool visual = SignalTrialGraphic.IsNote(note);
            var body = Text(card, note.description, new Vector2(0, visual ? -53 : -16), new Vector2(196, visual ? 40 : 84), 12);
            body.alignment = TextAlignmentOptions.TopLeft;
            if (visual) SignalTrialGraphic.Add(card, note, new Vector2(.04f, .31f), new Vector2(.96f, .70f));
            var remove = Paper(card, "Panodan Çıkar", new Vector2(98, 67), new Vector2(18, 18), new Color(.67f, .76f, .76f));
            remove.gameObject.AddComponent<Button>().onClick.AddListener(() => board.RemoveInformation(note));
            Text(remove, "×", Vector2.zero, new Vector2(18, 18), 13);
            noteCards[id] = card;
            var lines = new RectTransform[2];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Paper(documentBoard, "Raptiyeler Arasındaki İp", Vector2.zero, Vector2.one, new Color(.62f, .12f, .08f));
                lines[i].GetComponent<Image>().raycastTarget = false; lines[i].SetAsFirstSibling();
            }
            noteThreads[id] = lines; index++;
        }
        GameLocalization.Set(shadeToggleLabel, shadeExpanded ? "BELGELERİ TOPLA" : "BELGELERİ AÇ");
        foreach (var localized in surface.GetComponentsInChildren<LocalizedText>(true)) localized.RefreshBaseline();
        UpdateResearchThreads();
    }

    private void UpdateResearchThreads()
    {
        Vector2 start = documentBoard.InverseTransformPoint(targetArea.Find("Raptiye").position);
        foreach (var pair in noteThreads)
        {
            Vector2 end = documentBoard.InverseTransformPoint(noteCards[pair.Key].Find("Raptiye").position);
            Vector2 middle = (start + end) * .5f + Vector2.down * 6;
            for (int i = 0; i < 2; i++)
            {
                var line = pair.Value[i]; line.gameObject.SetActive(board.PinnedQuest != null);
                Vector2 a = i == 0 ? start : middle, b = i == 0 ? middle : end;
                line.anchoredPosition = (a + b) * .5f;
                line.sizeDelta = new Vector2(Vector2.Distance(a, b), 1.8f);
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            }
        }
    }

    private void ClampDocument(RectTransform card)
    {
        var p = card.anchoredPosition; var r = documentBoard.rect; var half = card.rect.size * .5f;
        p.x = Mathf.Clamp(p.x, r.xMin + half.x + 4, r.xMax - half.x - 4);
        p.y = Mathf.Clamp(p.y, r.yMin + half.y + 4, r.yMax - half.y - 4);
        card.anchoredPosition = p;
    }

    public bool TryDropDocument(NotebookBlockData note, Vector2 screenPosition)
    {
        if (!freeLayout || !board.isMachineActive || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return false;
        bool overHandle = RectTransformUtility.RectangleContainsScreenPoint(shadeToggle, screenPosition, Camera.main);
        if (!overHandle && !board.TryGetBoardPoint(screenPosition, out _)) return false;
        bool placeAtPointer = shadeExpanded && !overHandle && RectTransformUtility.RectangleContainsScreenPoint(documentBoard, screenPosition, Camera.main);
        RectTransform card;
        if (note.blockType == NotebookBlockType.Quest)
        {
            // Hedef nereye bırakılırsa bırakılsın sabit yuvaya oturur; belge panosunu açmaz.
            var quest = NotebookWorkspaceController.FindQuest(note.questID);
            return quest != null && board.TryPinQuest(quest);
        }
        else if (note.blockType == NotebookBlockType.RuleClue)
        {
            if (!board.TryPinInformation(note)) return false;
            var pinned = board.pinnedInformation.Find(n => n.questID == note.questID && n.title == note.title && n.description == note.description);
            card = noteCards["note/" + pinned.instanceID];
        }
        else return false;
        if (placeAtPointer && RectTransformUtility.ScreenPointToLocalPointInRectangle(documentBoard, screenPosition, Camera.main, out var local))
            card.anchoredPosition = local;
        ClampDocument(card); CaptureLayout(); UpdateResearchThreads();
        SetDocumentPanelOpen(true);
        return true;
    }

    public void SaveLayout(SynthesisBoardSaveData snapshot)
    {
        if (!freeLayout) return;
        CaptureLayout();
        snapshot.documentShadeOpen = shadeExpanded;
        foreach (var pair in documentPositions) snapshot.documents.Add(new SynthesisBoardDocumentSaveData { id = pair.Key, position = pair.Value });
    }

    public void LoadLayout(SynthesisBoardSaveData snapshot)
    {
        if (!freeLayout) return;
        documentPositions.Clear();
        if (snapshot.documents != null) foreach (var item in snapshot.documents)
            if (item != null && !string.IsNullOrEmpty(item.id)) documentPositions[item.id] = item.position;
        loadingLayout = true;
        SetDocumentPanelOpen(snapshot.documentShadeOpen, true);
    }

    private static void RemoveResearchObject(GameObject obj)
    {
        obj.SetActive(false);
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
    }
}
