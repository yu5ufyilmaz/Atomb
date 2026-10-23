using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Fiziksel defteri taşımadan aynı kayıt verisinin ekran görünümünü yönetir.
public class NotebookWorkspaceController : MonoBehaviour
{
    public static NotebookWorkspaceController Instance { get; private set; }
    [Header("Prototip Ayarları")]
    [Tooltip("Kapalıysa çalışma alanı yalnız kitaptaki sabit blokları kullanır.")]
    public bool generateProceduralRun = true;
    public Canvas DragCanvas => root != null ? root.GetComponent<Canvas>() : null;
    public bool IsActive => root != null && root.activeSelf;
    public bool IsPaused => GameManager.Instance != null && GameManager.Instance.isGamePaused;
    public bool IsEraserActive => drawer != null && drawer.Tool == NotebookTool.Eraser;
    public bool CanMoveBlocks => IsActive && !IsPaused && drawer.Tool == NotebookTool.Select;
    public bool IsPointerOverWorkspace => IsActive && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private NotebookUI notebook;
    private GameObject root;
    private CanvasGroup group;
    private RectTransform bounds, page, bookWindow, bookContent;
    private NotebookFreehandDrawer drawer;
    private TextMeshProUGUI pageLabel, chapterLabel, statusLabel;
    private QuestDiagramGraphic pinnedDiagram;
    private GameObject pinnedPanel;
    private GameObject boardControls;
    private TextMeshProUGUI pinnedTitle, pinnedHint, boardFeedback;
    private InteractableBook activeBook;
    private int clueChapter, displayedStage = -1, displayedSeed;
    private readonly List<NotebookDraggableBlock> pageBlocks = new List<NotebookDraggableBlock>();
    private readonly Color paper = new Color(0.89f, 0.85f, 0.72f);
    private readonly Color ink = new Color(0.13f, 0.15f, 0.13f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        notebook = GetComponent<NotebookUI>();
        if (ProceduralSynthesisGenerator.Instance == null)
            gameObject.AddComponent<ProceduralSynthesisGenerator>();
        if (notebook != null && notebook.notebookData != null)
            ProceduralSynthesisGenerator.Instance.RegisterCatalog(notebook.notebookData.synthesisCatalog);
    }

    private void OnDestroy()
    {
        Close();
        if (root != null) Destroy(root);
        if (Instance == this) Instance = null;
    }

    public void Open(InteractableBook book)
    {
        if (root == null) BuildWorkspace();
        activeBook = book;
        if (generateProceduralRun) ProceduralSynthesisGenerator.Instance.EnsureRun();
        root.SetActive(true);
        bookWindow.gameObject.SetActive(book != null && !book.usePhysicalPageBlocks);
        clueChapter = Mathf.Clamp(ProceduralSynthesisGenerator.Instance.Run != null ? ProceduralSynthesisGenerator.Instance.Run.currentStage : 0, 0, 4);
        if (ChalkboardManager.Instance != null)
        {
            ChalkboardManager.Instance.SynthesisFeedback -= ShowFeedback;
            ChalkboardManager.Instance.SynthesisFeedback += ShowFeedback;
            ChalkboardManager.Instance.QuestPinned -= OnQuestPinned;
            ChalkboardManager.Instance.QuestPinned += OnQuestPinned;
        }
        drawer.Tool = NotebookTool.Select;
        RefreshPage();
        RefreshBook(book);
        RefreshPinnedQuest();
    }

    public void Close()
    {
        if (ChalkboardManager.Instance != null)
        {
            ChalkboardManager.Instance.SynthesisFeedback -= ShowFeedback;
            ChalkboardManager.Instance.QuestPinned -= OnQuestPinned;
        }
        if (root != null) root.SetActive(false);
        activeBook = null;
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

    private void OnQuestPinned(SynthesisQuestData quest) { RefreshPinnedQuest(); }

    private void Update()
    {
        if (!IsActive) return;
        group.interactable = !IsPaused;
        group.blocksRaycasts = !IsPaused;
        group.alpha = IsPaused ? 0f : 1f;
        var generator = ProceduralSynthesisGenerator.Instance;
        if (generator != null && generator.Run != null
            && (displayedStage != generator.Run.currentStage || displayedSeed != generator.Run.seed))
        {
            displayedStage = generator.Run.currentStage;
            displayedSeed = generator.Run.seed;
            RefreshPinnedQuest();
            if (activeBook != null) RefreshBook(activeBook);
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
            new GameObject("Workspace EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        RectTransform window = Panel("Not Defteri", bounds, new Vector2(540, 690), paper);
        window.anchorMin = window.anchorMax = new Vector2(1, 0.5f);
        window.anchoredPosition = new Vector2(-290, 0);
        MakeHeader(window, "SAHA DEFTERİ / KAYITLI SAYFALAR");
        pageLabel = Label(window, "", new Vector2(0, 295), new Vector2(280, 28), 18);
        Button(window, "<", new Vector2(-220, 295), new Vector2(45, 30), () => ChangePage(-1));
        Button(window, ">", new Vector2(220, 295), new Vector2(45, 30), () => ChangePage(1));
        Button(window, "+ Sayfa", new Vector2(170, -313), new Vector2(110, 32), AddPage);
        Button(window, "Taşı", new Vector2(-208, -313), new Vector2(70, 32), () => SetTool(NotebookTool.Select));
        Button(window, "Kalem", new Vector2(-126, -313), new Vector2(80, 32), () => SetTool(NotebookTool.Pen));
        Button(window, "Silgi", new Vector2(-38, -313), new Vector2(80, 32), () => SetTool(NotebookTool.Eraser));
        statusLabel = Label(window, "Taşı: blokları düzenle / Kalem: çiz / Silgi: sil", new Vector2(0, -270), new Vector2(505, 35), 14);

        page = Panel("Açık Sayfa", window, new Vector2(508, 530), new Color(0.97f, 0.94f, 0.82f));
        page.anchoredPosition = new Vector2(0, 8);
        page.gameObject.AddComponent<RectMask2D>();
        var drawing = new GameObject("Serbest Kalem", typeof(RectTransform), typeof(NotebookFreehandDrawer));
        var drawingRect = drawing.GetComponent<RectTransform>();
        drawingRect.SetParent(page, false);
        Stretch(drawingRect);
        drawer = drawing.GetComponent<NotebookFreehandDrawer>();
        drawer.Changed += () => notebook.EnsureWorkspacePage().strokes = drawer.Strokes;

        bookWindow = Panel("Açık Kitap", bounds, new Vector2(490, 680), paper);
        bookWindow.anchorMin = bookWindow.anchorMax = new Vector2(0, 0.5f);
        bookWindow.anchoredPosition = new Vector2(270, 0);
        MakeHeader(bookWindow, "ARAŞTIRMA KİTABI / ÖZEL BLOKLAR");
        chapterLabel = Label(bookWindow, "", new Vector2(0, 282), new Vector2(350, 30), 18);
        Button(bookWindow, "<", new Vector2(-210, 282), new Vector2(38, 30), () => ChangeChapter(-1));
        Button(bookWindow, ">", new Vector2(210, 282), new Vector2(38, 30), () => ChangeChapter(1));
        Label(bookWindow, "Metin basılıdır. Yalnız çerçeveli blokları deftere sürükle.", new Vector2(0, -315), new Vector2(465, 34), 14);
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

        var pin = Panel("Tahta Hedefi", bounds, new Vector2(490, 235), paper);
        pin.anchorMin = pin.anchorMax = new Vector2(0, 1);
        pin.anchoredPosition = new Vector2(280, -135);
        MakeHeader(pin, "SABİTLENEN HEDEF ŞEMA");
        pinnedPanel = pin.gameObject;
        pinnedTitle = Label(pin, "", new Vector2(0, 69), new Vector2(465, 30), 17);
        pinnedDiagram = new GameObject("Hedef Şema", typeof(RectTransform), typeof(QuestDiagramGraphic)).GetComponent<QuestDiagramGraphic>();
        pinnedDiagram.transform.SetParent(pin, false);
        var diagramRect = pinnedDiagram.rectTransform;
        diagramRect.sizeDelta = new Vector2(460, 140);
        diagramRect.anchoredPosition = new Vector2(0, -8);
        pinnedHint = Label(pin, "", new Vector2(0, -94), new Vector2(465, 30), 15);

        var controls = Panel("Tahta Araçları", bounds, new Vector2(720, 100), paper);
        controls.anchorMin = controls.anchorMax = Vector2.zero;
        controls.anchoredPosition = new Vector2(395, 85);
        boardControls = controls.gameObject;
        Button(controls, "Taşı", new Vector2(-300, 25), new Vector2(90, 34), () => SetBoardTool(ChalkboardTool.Move));
        Button(controls, "Tebeşir", new Vector2(-193, 25), new Vector2(110, 34), () => SetBoardTool(ChalkboardTool.Chalk));
        Button(controls, "Silgi", new Vector2(-83, 25), new Vector2(90, 34), () => SetBoardTool(ChalkboardTool.Eraser));
        Button(controls, "SENTEZLE / TEST ET", new Vector2(169, 25), new Vector2(300, 34), () => ChalkboardManager.Instance?.AttemptSynthesis());
        boardFeedback = Label(controls, "Tebeşir: uçtan uca çiz. İkinci çizim çift bağ yapar.", new Vector2(0, -22), new Vector2(700, 43), 17);
        root.SetActive(false);
    }

    public void RefreshPage()
    {
        if (root == null || notebook == null) return;
        foreach (var block in pageBlocks) if (block != null) Destroy(block.gameObject);
        pageBlocks.Clear();
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
        pageLabel.text = $"SAYFA {notebook.currentWorkspacePage + 1} / {notebook.workspacePages.Count}";
    }

    private void ChangePage(int direction)
    {
        notebook.currentWorkspacePage = Mathf.Clamp(notebook.currentWorkspacePage + direction, 0, notebook.workspacePages.Count - 1);
        RefreshPage();
    }

    private void AddPage()
    {
        notebook.workspacePages.Add(new NotebookPageSaveData());
        notebook.currentWorkspacePage = notebook.workspacePages.Count - 1;
        RefreshPage();
    }

    private void SetTool(NotebookTool tool)
    {
        drawer.Tool = tool;
        statusLabel.color = ink;
        foreach (var block in pageBlocks) if (block != null) block.transform.SetAsLastSibling();
        statusLabel.text = tool == NotebookTool.Select ? "Taşı: blokları düzenle veya tahtaya sürükle."
            : tool == NotebookTool.Pen ? "Kalem: boş kağıda işaretler çiz." : "Silgi: çizgiyi sil; bloğa tıklayarak kaldır.";
    }

    public bool TryDropBlock(NotebookDraggableBlock block, Vector2 screenPosition)
    {
        if (!CanMoveBlocks || block == null) return false;
        if (IsTopmostPage(screenPosition))
        {
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
                if (data.blockType == NotebookBlockType.Quest) StackQuest(copy);
                PlaceOnPage(copy);
            }
            else
            {
                block.Data.position = Normalize(local);
                PlaceOnPage(block);
            }
            return true;
        }
        var board = ChalkboardManager.Instance;
        if (block.IsSource || board == null || !board.isMachineActive || IsPointerOverWorkspace) return false;
        if (block.Data.blockType == NotebookBlockType.PartClue)
        {
            var formula = ProceduralSynthesisGenerator.Instance.ResolveFormula(block.Data.itemID);
            return formula != null && board.SpawnFormula(formula, screenPosition) != null;
        }
        if (block.Data.blockType == NotebookBlockType.Quest && board.TryGetBoardPoint(screenPosition, out _))
        {
            var quest = FindQuest(block.Data.questID);
            if (quest != null && board.TryPinQuest(quest)) { RefreshPinnedQuest(); return true; }
        }
        return false;
    }

    public bool TryStoreBoardFormula(DraggableFormula formula, Vector2 screenPosition)
    {
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
        return true;
    }

    public void DeleteBlock(NotebookDraggableBlock block)
    {
        if (block == null || block.IsSource || !IsActive || IsPaused) return;
        notebook.EnsureWorkspacePage().blocks.Remove(block.Data);
        pageBlocks.Remove(block);
        Destroy(block.gameObject);
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

    private void StackQuest(NotebookDraggableBlock block)
    {
        int count = notebook.EnsureWorkspacePage().blocks.FindAll(b => b.blockType == NotebookBlockType.Quest).Count;
        block.Data.position = new Vector2(0.5f, Mathf.Max(0.25f, 0.8f - (count - 1) * 0.055f));
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
        chapterLabel.text = book.includeProceduralClues ? $"SENTEZ DOSYASI {clueChapter + 1} / 5" : "SABİT TEST BLOKLARI";
        float y = 12;
        string explanation = "Araştırma arşivi. Hedef şemadaki [?] yuvalarını parça ve kural bloklarından çöz. Önceki sentez çıktısı sonraki aşamada kullanılır.\n";
        if (book.synthesisPages != null)
            foreach (var printed in book.synthesisPages)
                if (printed != null && book.IsOnPage(printed.pageIndex)) explanation += printed.title + "\n" + printed.explanation + "\n";
        AddPrintedText(explanation, ref y);
        if (book.synthesisPages != null)
            foreach (var printed in book.synthesisPages)
                if (printed != null && book.IsOnPage(printed.pageIndex) && printed.blocks != null)
                    foreach (var data in printed.blocks) AddSource(data, ref y);

        var generator = ProceduralSynthesisGenerator.Instance;
        if (book.includeProceduralClues && generator.Run != null && generator.Run.quests.Count > clueChapter)
        {
            var quest = generator.Run.quests[clueChapter];
            AddSource(new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = quest.questID, title = quest.title, description = "Bilinmeyen modülleri kural ipuçlarından çöz." }, ref y);
            for (int i = 0; i < quest.rules.Count; i++)
                AddSource(new NotebookBlockData { blockType = NotebookBlockType.RuleClue, questID = quest.questID, ruleIndex = i, title = "KURAL " + (i + 1), description = quest.rules[i].GetDescription() }, ref y);
            var produced = new HashSet<string>();
            foreach (var stage in generator.Run.quests) produced.Add(stage.outputItemID);
            foreach (var formula in generator.formulaCatalog)
            {
                if (formula == null || produced.Contains(formula.itemID)) continue;
                AddPrintedText(formula.isDecoy ? "UYARI: Bu modül kararsızdır; deney yapısında kullanmayın." : formula.description, ref y);
                AddSource(PartBlock(formula), ref y);
            }
            bool archiveHeading = false;
            foreach (var stage in generator.Run.quests)
            {
                if (!stage.completed) continue;
                var output = generator.ResolveFormula(stage.outputItemID);
                if (output == null) continue;
                if (!archiveHeading)
                {
                    AddPrintedText("SENTEZ ARŞİVİ\nTamamladığınız aşamaların ürünlerini kaybettiğinizde buradan tekrar deftere alabilirsiniz.", ref y);
                    archiveHeading = true;
                }
                AddSource(PartBlock(output), ref y);
            }
        }
        bookContent.sizeDelta = new Vector2(0, y + 12);
        bookContent.anchoredPosition = Vector2.zero;
    }

    private static NotebookBlockData PartBlock(FormulaItemSO formula) => new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = formula.itemID, title = formula.itemName, description = "Modül / kapasite " + formula.connectionCapacity };
    public static SynthesisQuestData FindQuest(string id)
    {
        var practice = ChalkboardManager.Instance != null ? ChalkboardManager.Instance.practiceQuest : null;
        if (practice != null && practice.CreateQuest().questID == id) return practice.CreateQuest();
        return ProceduralSynthesisGenerator.Instance?.Run?.quests.Find(q => q.questID == id);
    }

    private void SetBoardTool(ChalkboardTool tool)
    {
        if (ChalkboardManager.Instance == null) return;
        ChalkboardManager.Instance.Tool = tool;
        boardFeedback.text = tool == ChalkboardTool.Move ? "Taşı: parçayı tutup sürükle. Q/E: döndür."
            : tool == ChalkboardTool.Eraser ? "Silgi: (x) uca tıklayıp aç; bağı ucundan sil; fazlalığı gövdesinden sil."
            : "Tebeşir: bir uçtan diğerine çiz. Aynı bağı tekrar çizerek çift bağ yap.";
        boardFeedback.color = ink;
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
        if (pinnedPanel == null) return;
        bool boardActive = activeBook == null && ChalkboardManager.Instance != null && ChalkboardManager.Instance.isMachineActive;
        var quest = boardActive ? ChalkboardManager.Instance.PinnedQuest : null;
        pinnedPanel.SetActive(boardActive && quest != null);
        boardControls.SetActive(boardActive);
        if (quest != null)
        {
            pinnedTitle.text = quest.title;
            pinnedHint.text = ChalkboardManager.Instance.practiceQuest != null
                ? "?1: nötron üretmeli   /   ?2: çift bağ tutabilmeli"
                : "Bilinmeyen yuvaları topladığın kural notlarıyla çöz.";
            pinnedDiagram.SetQuest(quest);
        }
    }

    private void MakeHeader(RectTransform window, string title)
    {
        var handle = Panel("Başlıktan Sürükle", window, new Vector2(window.sizeDelta.x, 36), ink);
        handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 1);
        handle.pivot = new Vector2(0.5f, 1);
        var drag = handle.gameObject.AddComponent<DraggableWorkspaceItem>();
        drag.target = window;
        drag.workspaceBounds = bounds;
        var text = Label(handle, title, Vector2.zero, new Vector2(window.sizeDelta.x - 20, 32), 16);
        text.color = paper;
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
