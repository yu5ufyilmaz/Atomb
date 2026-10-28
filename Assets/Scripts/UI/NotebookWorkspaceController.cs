using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Fiziksel defteri taşımadan aynı kayıt verisinin ekran görünümünü yönetir.
public partial class NotebookWorkspaceController : MonoBehaviour
{
    public static NotebookWorkspaceController Instance { get; private set; }
    [Header("Prototip Ayarları")]
    [SerializeField] private Sprite notebookSprite;
    [SerializeField] private Sprite notePaperSprite;
    [SerializeField] private TMP_FontAsset notebookHeadingFont;
    [SerializeField] private TMP_FontAsset notebookBodyFont;
    [SerializeField] private TMP_FontAsset notebookHandwritingFont;
    private bool UsesNotebookSkin => IsArchiveNotebook && notebookSprite != null;
    public Canvas DragCanvas => root != null ? root.GetComponent<Canvas>() : null;
    public bool IsActive => root != null && root.activeSelf;
    public bool IsPaused => GameManager.Instance != null && GameManager.Instance.isGamePaused;
    public bool CanMoveBlocks => IsActive && !IsPaused;
    public bool IsArchiveNotebook => ResearchPuzzleTrial.IsResearch(ChalkboardManager.Instance);
    public bool IsStandaloneOpen { get; private set; }
    [Tooltip("Test sahnesinde serbest fare; oyun sahnesinde kapatarak Q/E ile gezin.")]
    public bool standaloneUnlocksCursor = true;
    public bool StandaloneRequiresCursor => IsStandaloneOpen && standaloneUnlocksCursor;

    private NotebookUI notebook;
    private GameObject root;
    private CanvasGroup group;
    private RectTransform bounds, page, bookWindow, bookContent;
    private RectTransform bookmarkContent;
    private RectTransform partsContent;
    private GameObject partsPager;
    private int partsArchivePage;
    private const int PartsPerPage = 4;
    private string bookmarkState;
    private NotebookFreehandDrawer drawer;
    private TextMeshProUGUI pageLabel, chapterLabel, statusLabel, archiveFooter;
    private GameObject boardControls;
    private TextMeshProUGUI boardFeedback;
    private InteractableBook activeBook;
    private int clueChapter;
    private readonly List<NotebookDraggableBlock> pageBlocks = new List<NotebookDraggableBlock>();
    private readonly Color paper = new Color(0.89f, 0.85f, 0.72f);
    private readonly Color ink = new Color(0.13f, 0.15f, 0.13f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        notebook = GetComponent<NotebookUI>();
        if (FormulaCatalog.Instance == null)
            gameObject.AddComponent<FormulaCatalog>();
        if (notebook != null && notebook.notebookData != null)
            FormulaCatalog.Instance.RegisterCatalog(notebook.notebookData.synthesisCatalog);
    }

    private void OnDestroy()
    {
        Close();
        if (root != null) Destroy(root);
        if (Instance == this) Instance = null;
    }

    public void Open(InteractableBook book)
    {
        RestoreWorkstationNotebookPosition();
        ResetNotebookPageTurn();
        openingNotebook = true;
        IsStandaloneOpen = false;
        if (notebook != null && notebook.useScreenSpaceNotebook) notebook.isNotebookOpen = false;
        if (root == null) BuildWorkspace();
        root.SetActive(true);
        Canvas.ForceUpdateCanvases();
        notebookWindow.localScale = Vector3.one * NotebookScale;
        if (notebook != null && notebook.useScreenSpaceNotebook)
        {
            notebookOpenPosition = new Vector2(-notebookWindow.sizeDelta.x * NotebookScale * .5f - 50,
                -bounds.rect.height * .5f + notebookWindow.sizeDelta.y * NotebookScale * .5f + 75);
            notebookWindow.anchoredPosition = notebookOpenPosition;
        }
        activeBook = book;
        if (archiveFooter != null && IsArchiveNotebook) archiveFooter.text = "NOTU SÜRÜKLE  /  ASLI DEFTERDE KALIR";
        bookWindow.gameObject.SetActive(book != null && !book.usePhysicalPageBlocks
            && book.synthesisPages.Count > 0);
        if (ChalkboardManager.Instance != null)
        {
            ChalkboardManager.Instance.SynthesisFeedback -= ShowFeedback;
            ChalkboardManager.Instance.SynthesisFeedback += ShowFeedback;
            ChalkboardManager.Instance.QuestPinned -= OnQuestPinned;
            ChalkboardManager.Instance.QuestPinned += OnQuestPinned;
        }
        drawer.Tool = NotebookTool.Pen;
        ShowNotebookView(NotebookView.Notes);
        RefreshPage();
        RefreshBook(book);
        RefreshPinnedQuest();
        openingNotebook = false;
    }

    public void Close()
    {
        ResetNotebookPageTurn();
        bool restoreCursor = IsStandaloneOpen;
        IsStandaloneOpen = false;
        if (notebook != null && notebook.useScreenSpaceNotebook) notebook.isNotebookOpen = false;
        if (ChalkboardManager.Instance != null)
        {
            ChalkboardManager.Instance.SynthesisFeedback -= ShowFeedback;
            ChalkboardManager.Instance.QuestPinned -= OnQuestPinned;
        }
        if (root != null) root.SetActive(false);
        activeBook = null;
        if (restoreCursor && GameManager.Instance != null) GameManager.Instance.UpdateCursorState();
    }

    private void ShowFeedback(string message, bool success)
    {
        if (statusLabel == null) return;
        statusLabel.text = message;
        statusLabel.color = success ? new Color(0.15f, 0.35f, 0.15f) : new Color(0.55f, 0.12f, 0.1f);
        if (boardFeedback != null)
        {
            boardFeedback.text = message;
            boardFeedback.color = statusLabel.color;
        }
    }

    private void OnQuestPinned(SynthesisQuestData quest)
    {
        RefreshPinnedQuest();
        if (!ResearchPuzzleTrial.IsResearch(ChalkboardManager.Instance)) return;
        quest = ChalkboardManager.Instance.PinnedQuest;
        string message = quest == null ? "Defterden bir hedef seç; mevcut bilgilerinle bir hipotez kur."
            : "Hedef: " + quest.title + (quest.completed ? " / Tamamlandı." : " / Hipotezini kur ve test et.");
        if (statusLabel != null) { statusLabel.text = message; statusLabel.color = ink; }
        if (boardFeedback != null) { boardFeedback.text = message; boardFeedback.color = ink; }
    }

    private void Update()
    {
        if (!IsActive) return;
        group.interactable = group.blocksRaycasts = !IsPaused && (!IsStandaloneOpen || standaloneUnlocksCursor);
        group.alpha = IsPaused ? 0f : 1f;
        if (IsPaused) ResetNotebookPageTurn();
        else
        {
            if (IsArchiveNotebook && !IsStandaloneOpen && Input.GetKeyDown(KeyCode.Tab)) ToggleNotebook();
            UpdateNotebookPopup(Time.unscaledDeltaTime);
            UpdateNotebookPageTurn(Time.unscaledDeltaTime);
        }
    }

    private void BuildWorkspace()
    {
        root = new GameObject("Senzora Workspace", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        bounds = root.GetComponent<RectTransform>();
        group = root.GetComponent<CanvasGroup>();
        if (EventSystem.current == null)
            new GameObject("Workspace EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

        RectTransform window = Panel("Not Defteri", bounds, new Vector2(540, 690), paper);
        window.anchorMin = window.anchorMax = new Vector2(1, 0.5f);
        window.anchoredPosition = new Vector2(-290, 0);
        MakeHeader(window, "SAHA DEFTERİ / TUTUP TAŞI");
        pageLabel = Label(window, "", new Vector2(0, 295), new Vector2(280, 28), 18);
        if (!IsArchiveNotebook)
        {
            Button(window, "<", new Vector2(-220, 295), new Vector2(45, 30), () => ChangePage(-1));
            Button(window, ">", new Vector2(220, 295), new Vector2(45, 30), () => ChangePage(1));
        }
        else
        {
            var pager = Panel("Parça Sayfa Okları", window, new Vector2(508, 32), Color.clear);
            pager.anchoredPosition = new Vector2(0, 295);
            pager.GetComponent<Image>().raycastTarget = false;
            partsPager = pager.gameObject;
            Button(pager, "<", new Vector2(-220, 0), new Vector2(45, 30), () => ChangeArchivePage(-1));
            Button(pager, ">", new Vector2(220, 0), new Vector2(45, 30), () => ChangeArchivePage(1));
        }
        archiveFooter = Label(window, IsArchiveNotebook ? "DEFTER: ARŞİV  /  TAHTA: DENEY" : "SOL TIK: ÇİZ  / SAĞ TIK: SİL", new Vector2(0, -313), new Vector2(505, 32), 16);
        statusLabel = Label(window, "Notu tahtaya sürükle. Oklarla dilediğin sayfaya geç.", new Vector2(0, -270), new Vector2(505, 35), 14);

        var tabs = Panel("Dolu Sayfalar", window, new Vector2(42, 530), new Color(.25f, .22f, .16f, .35f));
        tabs.anchoredPosition = new Vector2(-294, 8);
        tabs.gameObject.AddComponent<RectMask2D>();
        var tabScroll = tabs.gameObject.AddComponent<ScrollRect>();
        tabScroll.horizontal = false;
        tabScroll.viewport = tabs;
        tabScroll.movementType = ScrollRect.MovementType.Clamped;
        bookmarkContent = new GameObject("Sayfa Etiketleri", typeof(RectTransform)).GetComponent<RectTransform>();
        bookmarkContent.SetParent(tabs, false);
        bookmarkContent.anchorMin = new Vector2(0, 1);
        bookmarkContent.anchorMax = Vector2.one;
        bookmarkContent.pivot = new Vector2(.5f, 1);
        tabScroll.content = bookmarkContent;
        tabs.gameObject.SetActive(!IsArchiveNotebook);

        page = Panel("Açık Sayfa", window, new Vector2(508, 390), new Color(0.97f, 0.94f, 0.82f));
        page.anchoredPosition = new Vector2(0, -62);
        page.gameObject.AddComponent<RectMask2D>();
        var drawing = new GameObject("Serbest Kalem", typeof(RectTransform), typeof(NotebookFreehandDrawer));
        var drawingRect = drawing.GetComponent<RectTransform>();
        drawingRect.SetParent(page, false);
        Stretch(drawingRect);
        drawer = drawing.GetComponent<NotebookFreehandDrawer>();
        drawer.Changed += () => { notebook.EnsureWorkspacePage().strokes = drawer.Strokes; RefreshBookmarks(); };
        if (IsArchiveNotebook)
        {
            drawing.SetActive(false);
            partsContent = new GameObject("Toplanan Parça Çizimleri", typeof(RectTransform)).GetComponent<RectTransform>();
            partsContent.SetParent(page, false);
            partsContent.anchorMin = new Vector2(0, 1); partsContent.anchorMax = Vector2.one;
            partsContent.pivot = new Vector2(.5f, 1);
        }
        BuildNotebookSections(window);
        BuildNotebookPopup(window);
        if (UsesNotebookSkin) ApplyNotebookSkin(window);

        bookWindow = Panel("Açık Kitap", bounds, new Vector2(490, 680), paper);
        bookWindow.anchorMin = bookWindow.anchorMax = new Vector2(0, 0.5f);
        bookWindow.anchoredPosition = new Vector2(270, 0);
        MakeHeader(bookWindow, "ARAŞTIRMA KİTABI / ÖZEL BLOKLAR");
        chapterLabel = Label(bookWindow, "", new Vector2(0, 282), new Vector2(350, 30), 18);
        Button(bookWindow, "<", new Vector2(-210, 282), new Vector2(38, 30), () => ChangeChapter(-1));
        Button(bookWindow, ">", new Vector2(210, 282), new Vector2(38, 30), () => ChangeChapter(1));
        Label(bookWindow, "Çerçeveli blokları deftere sürükle.", new Vector2(0, -315), new Vector2(465, 34), 14);
        var viewport = Panel("Kitap İçeriği", bookWindow, new Vector2(460, 550), new Color(0.96f, 0.92f, 0.8f));
        viewport.anchoredPosition = new Vector2(0, -8);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.viewport = viewport;
        bookContent = new GameObject("Basılı Sayfa", typeof(RectTransform)).GetComponent<RectTransform>();
        bookContent.SetParent(viewport, false);
        bookContent.anchorMin = new Vector2(0, 1);
        bookContent.anchorMax = new Vector2(1, 1);
        bookContent.pivot = new Vector2(0.5f, 1);
        scroll.content = bookContent;

        if (!IsArchiveNotebook)
        {
            var controls = Panel("Tahta Araçları", bounds, new Vector2(720, 100), paper);
            controls.anchorMin = controls.anchorMax = Vector2.zero;
            controls.anchoredPosition = new Vector2(395, 85);
            boardControls = controls.gameObject;
            Label(controls, "Sol tık: çiz / uçları bağla\nSağ tık: sil • Yazılar sabittir", new Vector2(-164, 25), new Vector2(365, 42), 16);
            Button(controls, "SENTEZLE / TEST ET", new Vector2(199, 25), new Vector2(280, 34), () => ChalkboardManager.Instance?.AttemptSynthesis());
            boardFeedback = Label(controls, "Boş tahtada çiz; uçtan uca bağla. İkinci bağ çizimi çift bağ yapar.", new Vector2(0, -22), new Vector2(700, 43), 16);
        }
        root.SetActive(false);
    }

    public void RefreshPage()
    {
        if (root == null || notebook == null) return;
        foreach (var block in pageBlocks) if (block != null) RemoveObject(block.gameObject);
        pageBlocks.Clear();
        notebook.EnsureCollectedQuests();
        if (IsArchiveNotebook)
        {
            RefreshPartsArchive();
            RefreshCollectedTargets();
            RefreshInformation();
            return;
        }
        var data = notebook.EnsureWorkspacePage();
        if (data.blocks == null) data.blocks = new List<NotebookBlockData>();
        if (data.strokes == null) data.strokes = new List<NotebookStrokeData>();
        drawer.SetStrokes(data.strokes);
        data.strokes = drawer.Strokes;
        for (int i = 0; i < data.blocks.Count; i++)
        {
            var block = CreateBlock(data.blocks[i], false, page);
            data.blocks[i] = block.Data;
            PlaceOnPage(block);
            pageBlocks.Add(block);
        }
        pageLabel.text = $"SAYFA {notebook.currentWorkspacePage + 1}";
        RefreshCollectedTargets();
        RefreshBookmarks();
        RefreshInformation();
    }

    private void StoreArchiveNote(NotebookBlockData note)
    {
        var copy = note.Clone();
        copy.instanceID = Guid.NewGuid().ToString("N"); copy.rotation = 0;
        if (note.blockType == NotebookBlockType.Quest)
        {
            var targets = notebook.EnsureCollectedQuests();
            if (!targets.Exists(n => n.questID == note.questID)) targets.Add(copy);
            return;
        }
        foreach (var saved in notebook.workspacePages)
            if (saved?.blocks != null && saved.blocks.Exists(n => n != null && n.blockType == note.blockType
                && (note.blockType == NotebookBlockType.PartClue ? n.itemID == note.itemID : SameInformation(n, note)))) return;
        notebook.EnsureWorkspacePage().blocks.Add(copy);
    }

    private List<NotebookBlockData> ArchivedParts()
    {
        var notes = new List<NotebookBlockData>(); var seen = new HashSet<string>();
        foreach (var saved in notebook.workspacePages)
            if (saved?.blocks != null)
                foreach (var note in saved.blocks)
                    if (note != null && note.blockType == NotebookBlockType.PartClue && seen.Add(note.itemID ?? note.title)) notes.Add(note);
        return notes;
    }

    public bool ShowCollectedPart(string itemID)
    {
        int index = ArchivedParts().FindIndex(n => n.itemID == itemID);
        if (index < 0) return false;
        int previousPage = partsArchivePage;
        bool wasParts = currentNotebookView == NotebookView.Notes;
        partsArchivePage = index / PartsPerPage;
        ShowNotebookView(NotebookView.Notes);
        if (wasParts && previousPage != partsArchivePage) BeginNotebookPageTurn(partsArchivePage - previousPage);
        return true;
    }

    private void ChangeArchivePage(int delta)
    {
        if (delta == 0 || IsPaused) return;
        bool information = informationPanel.gameObject.activeSelf;
        bool targets = targetPanel.gameObject.activeSelf;
        int previous = information ? showingSolutions ? solutionsArchivePage : informationArchivePage
            : targets ? targetArchivePage : partsArchivePage;
        int count = information ? InformationNotes(showingSolutions).Count
            : targets ? notebook.EnsureCollectedQuests().Count : ArchivedParts().Count;
        int perPage = information ? 2 : targets ? 1 : PartsPerPage;
        int pages = Mathf.Max(1, (count + perPage - 1) / perPage);
        int next = Mathf.Clamp(previous + delta, 0, pages - 1);
        if (next == previous) return;
        ResetNotebookPageTurn();
        if (information)
        {
            if (showingSolutions) solutionsArchivePage = next; else informationArchivePage = next;
            RefreshInformation();
        }
        else if (targets) { targetArchivePage = next; RefreshCollectedTargets(); }
        else { partsArchivePage = next; RefreshPartsArchive(); }
        BeginNotebookPageTurn(next - previous);
    }

    private void UpdateArchivePager(int index, int count)
    {
        if (partsPager == null) return;
        partsPager.transform.GetChild(0).GetComponent<Button>().interactable = index > 0;
        partsPager.transform.GetChild(1).GetComponent<Button>().interactable = index < count - 1;
        if (UsesNotebookSkin)
            foreach (var button in partsPager.GetComponentsInChildren<Button>())
                button.GetComponentInChildren<TMP_Text>().color = button.interactable ? NotebookInk : new Color(.09f, .11f, .095f, .25f);
    }

    private void RefreshPartsArchive()
    {
        if (partsContent == null) return;
        for (int i = partsContent.childCount - 1; i >= 0; i--) RemoveObject(partsContent.GetChild(i).gameObject);
        pageBlocks.Clear();
        var notes = ArchivedParts();
        int pageCount = Mathf.Max(1, (notes.Count + PartsPerPage - 1) / PartsPerPage);
        partsArchivePage = Mathf.Clamp(partsArchivePage, 0, pageCount - 1);
        for (int slot = 0; slot < PartsPerPage; slot++)
        {
            int index = partsArchivePage * PartsPerPage + slot;
            if (index >= notes.Count) break;
            var block = CreateBlock(notes[index], false, partsContent);
            var rect = (RectTransform)block.transform;
            rect.sizeDelta = new Vector2(UsesNotebookSkin ? 218 : 234, UsesNotebookSkin ? 190 : 174);
            Top(rect, new Vector2(slot % 2 == 0 ? -(UsesNotebookSkin ? 116 : 125) : (UsesNotebookSkin ? 116 : 125), -8 - slot / 2 * (UsesNotebookSkin ? 204 : 190)));
            ApplyArchiveNotePose(rect, notes[index]); pageBlocks.Add(block);
        }
        if (page.gameObject.activeSelf) pageLabel.text = UsesNotebookSkin ? $"{partsArchivePage + 1} / {pageCount}" : $"PARÇALAR  {partsArchivePage + 1} / {pageCount}";
        if (page.gameObject.activeSelf) UpdateArchivePager(partsArchivePage, pageCount);
        if (notes.Count == 0)
        {
            var empty = Label(partsContent, "Kitaplardan aldığın parça çizimleri burada birikir.\nAynı çizimi tahtada tekrar kullanabilirsin.\nDefterdeki aslı burada kalır.", Vector2.zero, new Vector2(450, 140), 17);
            Top(empty.rectTransform, new Vector2(0, -12));
        }
        if (UsesNotebookSkin) ShowEmptyArchivePaper(page, notes.Count == 0);
        partsContent.sizeDelta = new Vector2(0, page.rect.height);
        partsContent.anchoredPosition = Vector2.zero;
    }

    private bool IsNotebookDropArea(Vector2 position)
    {
        if (!Contains((RectTransform)page.parent, position)) return false;
        if (EventSystem.current == null) return true;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(page.parent);
    }

    private void ChangePage(int direction)
    {
        GoToPage(Mathf.Max(0, notebook.currentWorkspacePage + direction));
    }

    public void GoToPage(int index)
    {
        if (IsArchiveNotebook)
        {
            int previousPage = partsArchivePage;
            bool wasParts = currentNotebookView == NotebookView.Notes;
            partsArchivePage = Mathf.Max(0, index);
            ShowNotebookView(NotebookView.Notes);
            if (wasParts && previousPage != partsArchivePage) BeginNotebookPageTurn(partsArchivePage - previousPage);
            return;
        }
        ShowNotebookView(NotebookView.Notes);
        notebook.currentWorkspacePage = Mathf.Max(0, index);
        RefreshPage();
    }

    private void RefreshBookmarks()
    {
        if (bookmarkContent == null) return;
        var occupied = new List<int>();
        for (int i = 0; i < notebook.workspacePages.Count; i++)
        {
            var data = notebook.workspacePages[i];
            if (data != null && ((data.blocks != null && data.blocks.Count > 0)
                || (data.strokes != null && data.strokes.Exists(s => s != null && s.points != null && s.points.Count > 0))))
                occupied.Add(i);
        }
        string state = notebook.currentWorkspacePage + ":" + string.Join(",", occupied);
        if (bookmarkState == state) return;
        bookmarkState = state;
        for (int i = bookmarkContent.childCount - 1; i >= 0; i--) RemoveObject(bookmarkContent.GetChild(i).gameObject);
        bookmarkContent.sizeDelta = new Vector2(0, occupied.Count * 32);
        for (int i = 0; i < occupied.Count; i++)
        {
            int index = occupied[i];
            Button(bookmarkContent, (index + 1).ToString(), new Vector2(0, -16 - i * 32), new Vector2(38, 28), () => GoToPage(index));
            var tab = (RectTransform)bookmarkContent.GetChild(bookmarkContent.childCount - 1);
            tab.anchorMin = tab.anchorMax = new Vector2(.5f, 1);
            tab.GetComponent<Image>().color = index == notebook.currentWorkspacePage
                ? new Color(.92f, .72f, .31f) : new Color(.77f, .7f, .5f);
        }
    }

    public bool TryDropBlock(NotebookDraggableBlock block, Vector2 screenPosition)
    {
        if (!CanMoveBlocks || block == null || block.Data == null) return false;
        if (IsArchiveNotebook && IsNotebookDropArea(screenPosition))
        {
            if (block.IsSource) StoreArchiveNote(block.Data);
            ShowNotebookView(block.Data.blockType == NotebookBlockType.Quest ? NotebookView.Targets
                : SignalExperiment.IsMeasurementNote(block.Data) ? NotebookView.Solutions
                : block.Data.blockType == NotebookBlockType.RuleClue ? NotebookView.Information : NotebookView.Notes);
            if (block.Data.blockType == NotebookBlockType.PartClue) ShowCollectedPart(block.Data.itemID);
            if (SignalExperiment.IsMeasurementNote(block.Data))
                solutionsArchivePage = Mathf.Max(0, InformationNotes(true).FindIndex(n => SameInformation(n, block.Data))) / 2;
            else if (block.Data.blockType == NotebookBlockType.RuleClue)
                informationArchivePage = Mathf.Max(0, InformationNotes().FindIndex(n => SameInformation(n, block.Data))) / 2;
            if (block.Data.blockType == NotebookBlockType.Quest)
                targetArchivePage = Mathf.Max(0, notebook.EnsureCollectedQuests().FindIndex(n => n.questID == block.Data.questID));
            RefreshPage();
            ShowFeedback("Kayıt arşivde saklanır. Tahtaya sürüklediğinde bir kopyasını kullanırsın.", true);
            return true;
        }
        if (informationPanel.gameObject.activeSelf && IsInformationDropArea(screenPosition))
        {
            if (block.Data.blockType != NotebookBlockType.RuleClue)
            { ShowFeedback("Bilgiler bölümü kural ve ipucu notları içindir.", false); return false; }
            if (block.IsSource && !InformationNotes().Exists(n => SameInformation(n, block.Data)))
            {
                var copy = block.Data.Clone();
                copy.instanceID = Guid.NewGuid().ToString("N");
                copy.position = Vector2.one * .5f;
                notebook.EnsureWorkspacePage().blocks.Add(copy);
            }
            RefreshInformation(); RefreshBookmarks();
            return true;
        }
        if (IsTargetDropArea(screenPosition))
        {
            if (block.Data.blockType != NotebookBlockType.Quest)
            { ShowFeedback("Burası yalnız HEDEF notları için. Parçayı Notlar sayfasına bırak.", false); return false; }
            if (block.IsSource)
            {
                var targets = notebook.EnsureCollectedQuests();
                if (!targets.Exists(q => !string.IsNullOrEmpty(q.questID) && q.questID == block.Data.questID))
                {
                    var data = block.Data.Clone();
                    data.instanceID = Guid.NewGuid().ToString("N");
                    data.rotation = 0;
                    targets.Add(data);
                }
            }
            RefreshCollectedTargets();
            ShowNotebookView(NotebookView.Targets);
            ShowFeedback("Hedef toplandı. Bu bölümden tahtaya sürükleyebilirsin.", true);
            return true;
        }
        if (IsTopmostPage(screenPosition))
        {
            if (block.Data.blockType == NotebookBlockType.Quest)
            { ShowFeedback("HEDEF notunu üstteki TOPLANAN HEDEFLER alanına bırak.", false); return false; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(page, screenPosition, null, out Vector2 local);
            if (block.IsSource)
            {
                var data = block.Data.Clone();
                data.instanceID = Guid.NewGuid().ToString("N");
                data.position = Normalize(local);
                float variation = notebook.notebookData != null ? notebook.notebookData.sketchRotation : 5f;
                data.rotation = UnityEngine.Random.Range(-variation, variation);
                data.sketchVariant = UnityEngine.Random.Range(0, 10000);
                var copy = CreateBlock(data, false, page);
                notebook.EnsureWorkspacePage().blocks.Add(copy.Data);
                pageBlocks.Add(copy);
                PlaceOnPage(copy);
                RefreshBookmarks();
                RefreshInformation();
            }
            else
            {
                // Defter kartları sabittir; sürükleme yalnız tahtaya kopyalamak içindir.
                return true;
            }
            return true;
        }
        var experiment = SignalExperiment.Active;
        if (!block.IsSource && experiment != null && experiment.terminal != null
            && GameManager.Instance != null && GameManager.Instance.activeInteraction == experiment.terminal)
        {
            if (IsWorkspaceAt(screenPosition)) return false;
            return experiment.terminal.TryInsertMeasurement(block.Data, screenPosition);
        }
        var board = ChalkboardManager.Instance;
        if (block.IsSource || board == null || !board.isMachineActive) return false;
        var documents = board.EnsureDocuments();
        if (IsArchiveNotebook && block.Data.blockType != NotebookBlockType.PartClue)
        {
            if (IsWorkspaceAt(screenPosition)) return false;
            return documents != null && documents.TryDropDocument(block.Data, screenPosition);
        }
        if (documents != null && documents.ContainsPointer(screenPosition))
        {
            if (block.Data.blockType == NotebookBlockType.RuleClue && documents.IsInformationArea(screenPosition))
                return board.TryPinInformation(block.Data);
            if (block.Data.blockType == NotebookBlockType.Quest && documents.IsTargetArea(screenPosition))
            {
                var target = FindQuest(block.Data.questID);
                return target != null && board.TryPinQuest(target);
            }
            ShowFeedback(IsArchiveNotebook ? "Parçayı kara tahtaya bırak. Belge panosunu tutamaktan toplayabilirsin."
                : "Hedefi GÖREV alanına, ipuçlarını BİLGİLER alanına bırak.", false);
            return false;
        }
        if (IsWorkspaceAt(screenPosition)) return false;
        if (block.Data.blockType == NotebookBlockType.PartClue)
        {
            var formula = FormulaCatalog.Instance.ResolveFormula(block.Data.itemID);
            return formula != null && board.SpawnFormula(formula, screenPosition) != null;
        }
        if (block.Data.blockType == NotebookBlockType.Quest && board.TryGetBoardPoint(screenPosition, out _))
        {
            var quest = FindQuest(block.Data.questID);
            if (quest != null && board.TryPinQuest(quest)) { RefreshPinnedQuest(); return true; }
        }
        if (block.Data.blockType == NotebookBlockType.RuleClue)
            ShowFeedback("Bilgi notunu tahtanın solundaki BİLGİLER alanına bırak.", false);
        return false;
    }

    public bool TryStoreBoardFormula(DraggableFormula formula, Vector2 screenPosition)
    {
        if (IsArchiveNotebook)
        {
            if (!CanMoveBlocks || activeBook != null || formula?.formulaData == null
                || formula.GetConnectedParts().Count != 1 || !IsNotebookDropArea(screenPosition)) return false;
            StoreArchiveNote(PartBlock(formula.formulaData));
            ShowNotebookView(NotebookView.Notes); RefreshPage();
            return true;
        }
        if (!CanMoveBlocks || activeBook != null || formula == null || formula.formulaData == null
            || formula.GetConnectedParts().Count != 1 || !IsTopmostPage(screenPosition)) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(page, screenPosition, null, out Vector2 local);
        var data = PartBlock(formula.formulaData);
        data.instanceID = Guid.NewGuid().ToString("N");
        data.position = Normalize(local);
        data.rotation = UnityEngine.Random.Range(-4f, 4f);
        var copy = CreateBlock(data, false, page);
        notebook.EnsureWorkspacePage().blocks.Add(copy.Data);
        pageBlocks.Add(copy);
        PlaceOnPage(copy);
        RefreshBookmarks();
        return true;
    }

    public void DeleteBlock(NotebookDraggableBlock block)
    {
        if (block == null || block.IsSource || !IsActive || IsPaused) return;
        if (IsArchiveNotebook)
        {
            ShowFeedback("Toplanan kayıtlar defterde korunur. Deney kopyasını tahtadan kaldırabilirsin.", true);
            return;
        }
        if (block.Data.blockType == NotebookBlockType.RuleClue)
        {
            foreach (var savedPage in notebook.workspacePages)
                if (savedPage != null && savedPage.blocks != null)
                    savedPage.blocks.RemoveAll(n => n != null && (ReferenceEquals(n, block.Data)
                        || (!string.IsNullOrEmpty(block.Data.instanceID) && n.instanceID == block.Data.instanceID)));
            RefreshPage();
            return;
        }
        if (block.Data.blockType == NotebookBlockType.Quest)
        {
            notebook.EnsureCollectedQuests().Remove(block.Data);
            RefreshCollectedTargets();
            return;
        }
        notebook.EnsureWorkspacePage().blocks.Remove(block.Data);
        pageBlocks.Remove(block);
        RemoveObject(block.gameObject);
        RefreshBookmarks();
    }

    private void PlaceOnPage(NotebookDraggableBlock block)
    {
        var rect = block.GetComponent<RectTransform>();
        rect.SetParent(page, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Vector2 pos = Vector2.Scale(block.Data.position - Vector2.one * 0.5f, page.rect.size);
        Vector2 half = rect.sizeDelta * 0.5f;
        pos.x = Mathf.Clamp(pos.x, page.rect.xMin + half.x, page.rect.xMax - half.x);
        pos.y = Mathf.Clamp(pos.y, page.rect.yMin + half.y, page.rect.yMax - half.y);
        rect.anchoredPosition = pos;
        block.Data.position = Normalize(pos);
        rect.localRotation = Quaternion.Euler(0, 0, block.Data.rotation);
        rect.SetAsLastSibling();
    }

    private Vector2 Normalize(Vector2 local) => new Vector2(Mathf.InverseLerp(page.rect.xMin, page.rect.xMax, local.x), Mathf.InverseLerp(page.rect.yMin, page.rect.yMax, local.y));
    private static bool Contains(RectTransform rect, Vector2 point) => rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, point, null);

    private bool IsTopmostPage(Vector2 screenPosition)
    {
        if (!Contains(page, screenPosition)) return false;
        if (EventSystem.current == null) return true;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPosition }, hits);
        if (hits.Count == 0) return false;
        return hits[0].gameObject.transform == page || hits[0].gameObject.transform.IsChildOf(page);
    }

    private bool IsWorkspaceAt(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        // Bırakma konumunu kullan; EventSystem'in önceki imleç hedefi sürükleme sırasında eski kalabilir.
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(root.transform);
    }

    private NotebookDraggableBlock CreateBlock(NotebookBlockData data, bool source, Transform parent)
    {
        NotebookDraggableBlock block;
        if (notebook.notebookData != null && notebook.notebookData.workspaceBlockPrefab != null)
            block = Instantiate(notebook.notebookData.workspaceBlockPrefab, parent);
        else
        {
            var obj = new GameObject("Belge Bloğu", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
            obj.transform.SetParent(parent, false);
            block = obj.GetComponent<NotebookDraggableBlock>();
        }
        block.Initialize(data, source);
        return block;
    }

    private void ChangeChapter(int delta)
    {
        clueChapter = (clueChapter + delta + 5) % 5;
        RefreshBook(activeBook);
    }

    public void RefreshBook(InteractableBook book)
    {
        if (book != null && book.usePhysicalPageBlocks) return;
        if (root == null || book == null || activeBook != book) return;
        for (int i = bookContent.childCount - 1; i >= 0; i--) Destroy(bookContent.GetChild(i).gameObject);
        var header = bookWindow.GetComponentInChildren<DraggableWorkspaceItem>();
        if (header != null) header.worldTarget = book.transform;
        chapterLabel.text = "ARAŞTIRMA NOTLARI";
        float y = 12;
        string explanation = "Araştırma arşivi. Hedefi, parça atlasını ve deney gözlemlerini karşılaştır.\n";
        if (book.synthesisPages != null)
            foreach (var printed in book.synthesisPages)
                if (printed != null && book.IsOnPage(printed.pageIndex)) explanation += printed.title + "\n" + printed.explanation + "\n";
        AddPrintedText(explanation, ref y);
        if (book.synthesisPages != null)
            foreach (var printed in book.synthesisPages)
                if (printed != null && book.IsOnPage(printed.pageIndex) && printed.blocks != null)
                    foreach (var data in printed.blocks) AddSource(data, ref y);

        bookContent.sizeDelta = new Vector2(0, y + 12);
        bookContent.anchoredPosition = Vector2.zero;
    }

    private static NotebookBlockData PartBlock(FormulaItemSO formula) => new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = formula.itemID, title = formula.itemName, description = "Modül / kapasite " + formula.connectionCapacity };
    public static SynthesisQuestData FindQuest(string id)
    {
        var practice = ChalkboardManager.Instance != null ? ChalkboardManager.Instance.FindPracticeQuest(id) : null;
        if (practice != null) return practice.CreateQuest();
        return null;
    }

    private void AddSource(NotebookBlockData data, ref float y)
    {
        var block = CreateBlock(data, true, bookContent);
        var rect = block.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -y);
        y += rect.sizeDelta.y + 14;
    }

    private void AddPrintedText(string text, ref float y)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var label = Label(bookContent, text, Vector2.zero, new Vector2(425, 100), 16);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1);
        label.rectTransform.pivot = new Vector2(0.5f, 1);
        float height = Mathf.Max(28, label.GetPreferredValues(text, 425, 0).y + 8);
        label.rectTransform.sizeDelta = new Vector2(425, height);
        label.rectTransform.anchoredPosition = new Vector2(0, -y);
        y += height + 12;
    }

    private void RefreshPinnedQuest()
    {
        bool boardActive = activeBook == null && ChalkboardManager.Instance != null && ChalkboardManager.Instance.isMachineActive;
        if (boardControls != null) boardControls.SetActive(boardActive);
        if (boardActive) ChalkboardManager.Instance.EnsureDocuments()?.Refresh();
    }

    private void MakeHeader(RectTransform window, string title, bool movable = true)
    {
        var handle = Panel("Başlıktan Sürükle", window, new Vector2(window.sizeDelta.x, 36), ink);
        handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 1);
        handle.pivot = new Vector2(0.5f, 1);
        if (movable)
        {
            var drag = handle.gameObject.AddComponent<DraggableWorkspaceItem>();
            drag.target = window;
            drag.workspaceBounds = bounds;
        }
        var text = Label(handle, title, Vector2.zero, new Vector2(window.sizeDelta.x - 20, 32), 16);
        text.color = paper;
    }

    private static void RemoveObject(GameObject obj)
    {
        obj.SetActive(false);
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var obj = new GameObject("Metin", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        if (UsesNotebookSkin && notebookBodyFont != null) label.font = notebookBodyFont;
        label.fontSize = fontSize;
        label.color = ink;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = size;
        label.rectTransform.anchoredPosition = position;
        return label;
    }

    private void Button(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rect = Panel(text, parent, size, new Color(0.76f, 0.73f, 0.62f));
        rect.anchoredPosition = position;
        rect.gameObject.AddComponent<Button>().onClick.AddListener(action);
        Label(rect, text, Vector2.zero, size, 16);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
