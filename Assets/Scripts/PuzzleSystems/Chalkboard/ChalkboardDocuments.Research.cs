using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ChalkboardDocuments
{
    // Pano tahtanın üçte biri kadardır; ortası deney, sağ sütunu defter ve ölçüm cihazı içindir.
    private const float PanelWidth = 320, TargetBarHeight = 42, TargetOpenHeight = 186, NoteCardScale = .68f;
    private bool freeLayout, loadingLayout;
    private RectTransform corkBoard, documentBoard, shadeViewport, shadeBottomBar, goalDirectionsViewport;
    private CanvasGroup shadeInput;
    private TextMeshProUGUI targetToggleLabel;
    private bool shadeExpanded;
    private float shadeReveal;
    public bool IsDocumentPanelOpen => freeLayout && shadeExpanded;
    public Vector3 TargetSlot => targetArea.position;
    public Vector3 NotesSlot => documentBoard.position;

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
        // Hedef panonun üst kenarıdır: pano toplansa da sol üstteki yuvada görünür kalır ve panoyu o açar.
        targetArea = PinnedCard("Hedef Kartı", new Color(.93f, .85f, .62f), false);
        float width = corkBoard.rect.width;
        targetArea.sizeDelta = new Vector2(width, TargetBarHeight);
        targetArea.anchoredPosition = new Vector2(shadeViewport.anchoredPosition.x, 0);
        targetArea.gameObject.AddComponent<RectMask2D>();
        targetArea.gameObject.AddComponent<Outline>().effectColor = new Color(.94f, .9f, .75f, .8f);
        targetArea.gameObject.AddComponent<Button>().onClick.AddListener(() => SetDocumentPanelOpen(!shadeExpanded));
        title = Text(targetArea, "", Vector2.zero, new Vector2(width - 108, 34), 14);
        title.fontStyle = FontStyles.Bold;
        targetToggleLabel = Text(targetArea, "", new Vector2(width / 2 - 38, -TargetBarHeight / 2), new Vector2(60, 22), 12);
        targetToggleLabel.fontStyle = FontStyles.Bold;
        diagram = new GameObject("Tahtadaki Hedef Şema", typeof(RectTransform), typeof(QuestDiagramGraphic)).GetComponent<QuestDiagramGraphic>();
        diagram.transform.SetParent(targetArea, false);
        diagram.rectTransform.sizeDelta = new Vector2(width - 24, 62);
        diagram.rectTransform.anchoredPosition = new Vector2(0, -TargetBarHeight - 31);
        goalDirectionsViewport = Paper(targetArea, "Hedef Kaynakları", Vector2.zero, new Vector2(width - 24, 70), Color.clear);
        goalDirectionsViewport.gameObject.AddComponent<RectMask2D>();
        var directions = goalDirectionsViewport.gameObject.AddComponent<ScrollRect>();
        directions.horizontal = false; directions.viewport = goalDirectionsViewport;
        directions.movementType = ScrollRect.MovementType.Clamped; directions.scrollSensitivity = 22;
        hint = Text(goalDirectionsViewport, "", Vector2.zero, goalDirectionsViewport.sizeDelta, 11);
        hint.enableAutoSizing = false; hint.overflowMode = TextOverflowModes.Overflow;
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(.5f, 1);
        directions.content = hint.rectTransform;
        // Kart yükseldikçe içerik yerinde kalsın; fazlası kartın maskesinde saklanır.
        foreach (var part in new[] { title.rectTransform, targetToggleLabel.rectTransform, diagram.rectTransform, goalDirectionsViewport })
            part.anchorMin = part.anchorMax = new Vector2(.5f, 1);
        var targetPin = targetArea.Find("Raptiye").GetComponent<RectTransform>();
        targetPin.anchorMin = targetPin.anchorMax = new Vector2(.5f, 1);
        targetPin.anchoredPosition = new Vector2(-width / 2 + 16, -12);
        informationArea = Paper(documentBoard, "Kayıt Bırakma İpucu", Vector2.zero, new Vector2(218, 38), new Color(.63f, .45f, .29f, .85f));
        Text(informationArea, "KAYDI BU PANOYA BIRAK", Vector2.zero, new Vector2(208, 32), 12);
        RefreshResearch();
        SetDocumentPanelOpen(false, true);
    }

    private void BuildDocumentShade()
    {
        float width = Mathf.Min(PanelWidth, surface.rect.width - 28), height = surface.rect.height - 56;
        shadeViewport = new GameObject("Stor Belge Panosu", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup)).GetComponent<RectTransform>();
        shadeViewport.SetParent(surface, false); shadeViewport.pivot = new Vector2(.5f, 1);
        shadeViewport.anchoredPosition = new Vector2(surface.rect.xMin + width / 2 + 14, surface.rect.yMax - 48);
        shadeViewport.sizeDelta = new Vector2(width, height);
        shadeInput = shadeViewport.GetComponent<CanvasGroup>();
        corkBoard = Paper(shadeViewport, "Bağımsız Belge Yüzeyi", new Vector2(0, -height / 2), new Vector2(width, height), new Color(.47f, .32f, .21f));
        corkBoard.anchorMin = corkBoard.anchorMax = new Vector2(.5f, 1);
        // Gövde hedef şeridini de çevreler; üst çıtası açık hedefin ortasından geçmesin.
        PhysicalBoardFixture.Attach(shadeViewport, "Belge Panosu", Vector2.down * (height - TargetBarHeight) / 2,
            new Vector2(width, height + TargetBarHeight), new Color(.22f, .18f, .13f));
        var border = corkBoard.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(.18f, .16f, .12f); border.effectDistance = new Vector2(3, -3);
        for (int i = 0; i < 30; i++)
        {
            var fleck = Paper(corkBoard, "Mantar Tanesi", new Vector2(i * 83 % (int)(width - 12) - width / 2 + 6,
                i * 137 % (int)(height - 12) - height / 2 + 6), new Vector2(2 + i % 4, 2), new Color(.24f, .15f, .08f, .3f));
            fleck.GetComponent<Image>().raycastTarget = false;
        }
        // Açık hedef panonun üstünü kullanır; kartlar onun altında gezer, hedefi örtmez.
        float notesHeight = height - (TargetOpenHeight - TargetBarHeight) - 4;
        documentBoard = Paper(corkBoard, "Kayıt Alanı", new Vector2(0, (notesHeight - height) / 2), new Vector2(width, notesHeight), Color.clear);
        shadeBottomBar = Paper(shadeViewport, "Stor Alt Çıtası", Vector2.zero, new Vector2(width, 10), new Color(.22f, .23f, .2f));
        shadeBottomBar.anchorMin = shadeBottomBar.anchorMax = new Vector2(.5f, 1);
    }

    public void SetDocumentPanelOpen(bool open, bool immediate = false)
    {
        if (!freeLayout) return;
        shadeExpanded = open;
        GameLocalization.Set(targetToggleLabel, open ? "KAPAT" : "AÇ");
        if (open) shadeViewport.gameObject.SetActive(true);
        if (immediate) shadeReveal = open ? 1 : 0;
        ApplyShadeReveal();
    }

    private void ApplyShadeReveal()
    {
        float amount = Mathf.SmoothStep(0, 1, shadeReveal);
        shadeViewport.sizeDelta = new Vector2(corkBoard.rect.width, corkBoard.rect.height * amount);
        shadeBottomBar.anchoredPosition = new Vector2(0, -shadeViewport.rect.height + 5);
        shadeInput.interactable = shadeInput.blocksRaycasts = shadeExpanded && shadeReveal >= 1;
        shadeViewport.gameObject.SetActive(shadeReveal > 0 || shadeExpanded);
        // Açılan ayrıntı panoyla birlikte aşağı iner; kartın sol üst köşesi yuvada sabit kalır.
        float height = Mathf.Lerp(TargetBarHeight, board.PinnedQuest != null ? TargetOpenHeight : TargetBarHeight, amount);
        targetArea.sizeDelta = new Vector2(targetArea.sizeDelta.x, height);
        targetArea.anchoredPosition = new Vector2(targetArea.anchoredPosition.x, surface.rect.yMax - 6 - height / 2);
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
        targetArea.GetComponent<Image>().color = quest != null ? new Color(.93f, .85f, .62f) : new Color(.2f, .23f, .19f, .7f);
        targetArea.GetComponent<Outline>().enabled = quest == null;
        targetArea.Find("Raptiye").gameObject.SetActive(quest != null);
        if (quest != null) GameLocalization.SetFormat(title, "HEDEF / {0}", quest.title);
        else GameLocalization.Set(title, "HEDEFİ BURAYA BIRAK");
        title.color = hint.color = targetToggleLabel.color = quest != null ? new Color(.12f, .15f, .13f) : new Color(.94f, .9f, .75f);
        title.rectTransform.anchoredPosition = new Vector2(-26, quest != null ? -TargetBarHeight / 2 : -13);
        GameLocalization.Set(targetToggleLabel, shadeExpanded ? "KAPAT" : "AÇ");
        GameLocalization.Set(hint, quest != null ? board.practiceQuest?.clueSummary ?? "Kayıtları karşılaştır." : "Defter > Hedefler");
        goalDirectionsViewport.anchoredPosition = new Vector2(quest != null ? 0 : -26, quest != null ? -TargetBarHeight - 62 - 37 : -30);
        goalDirectionsViewport.sizeDelta = new Vector2(targetArea.sizeDelta.x - 24, quest != null ? 70 : 18);
        hint.rectTransform.anchoredPosition = Vector2.zero;
        hint.rectTransform.sizeDelta = new Vector2(goalDirectionsViewport.rect.width,
            Mathf.Max(goalDirectionsViewport.rect.height, hint.GetPreferredValues(hint.text, goalDirectionsViewport.rect.width, 0).y + 2));
        hint.ForceMeshUpdate(true);
        hint.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(goalDirectionsViewport.rect.height, hint.preferredHeight + 2));
        hint.alignment = quest != null ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        diagram.gameObject.SetActive(quest != null);
        if (quest != null) diagram.SetQuest(quest);
        informationArea.gameObject.SetActive(board.pinnedInformation.Count == 0);
        ApplyShadeReveal();
        int index = 0;
        foreach (var note in board.pinnedInformation)
        {
            if (string.IsNullOrEmpty(note.instanceID)) note.instanceID = Guid.NewGuid().ToString("N");
            string id = "note/" + note.instanceID;
            var card = PinnedCard("Kayıt / " + note.title, new Color(.79f, .86f, .86f));
            card.localScale = Vector3.one * NoteCardScale;
            card.anchoredPosition = documentPositions.TryGetValue(id, out var position) ? position
                // İki sütun, iki sıra; dört karttan sonrası kaydırılarak üste dizilir, oyuncu sürükleyip ayırır.
                : new Vector2(documentBoard.rect.xMin + 80 + index % 2 * 156 + index / 4 * 12,
                    documentBoard.rect.yMax - 57 - index / 2 % 2 * 106 - index / 4 * 12);
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
        var p = card.anchoredPosition; var r = documentBoard.rect; var half = card.rect.size * card.localScale.x * .5f;
        p.x = Mathf.Clamp(p.x, r.xMin + half.x + 4, r.xMax - half.x - 4);
        p.y = Mathf.Clamp(p.y, r.yMin + half.y + 4, r.yMax - half.y - 4);
        card.anchoredPosition = p;
    }

    public bool TryDropDocument(NotebookBlockData note, Vector2 screenPosition)
    {
        if (!freeLayout || !board.isMachineActive || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return false;
        bool overTarget = RectTransformUtility.RectangleContainsScreenPoint(targetArea, screenPosition, Camera.main);
        if (!overTarget && !board.TryGetBoardPoint(screenPosition, out _)) return false;
        bool placeAtPointer = shadeExpanded && !overTarget && RectTransformUtility.RectangleContainsScreenPoint(documentBoard, screenPosition, Camera.main);
        RectTransform card;
        if (note.blockType == NotebookBlockType.Quest)
        {
            // Hedef nereye bırakılırsa bırakılsın sabit yuvaya oturur; ayrıntısı panoyla birlikte açılır.
            var quest = NotebookWorkspaceController.FindQuest(note.questID);
            if (quest == null || !board.TryPinQuest(quest)) return false;
            SetDocumentPanelOpen(true);
            return true;
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
