using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SignalTrialSetup
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Defter Sprite Bağlantısını Kur")]
    public static void ApplyNotebookArtwork()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var workspace = UnityEngine.Object.FindFirstObjectByType<NotebookWorkspaceController>();
        Check(workspace != null && workspace.gameObject.scene.name == "scn_SynthesisBlockout", "Open the test scene first.");
        var serialized = new SerializedObject(workspace);
        const string paperPath = "Assets/Sprites/UI/UIInGame/LaboratoryLedger.png";
        AssetDatabase.ImportAsset(paperPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(paperPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = Vector4.zero;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        serialized.FindProperty("notebookSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paperPath);
        const string dividerPath = "Assets/Sprites/UI/UIInGame/LaboratoryLedgerDivider.png";
        AssetDatabase.ImportAsset(dividerPath);
        var dividerImporter = (TextureImporter)AssetImporter.GetAtPath(dividerPath);
        dividerImporter.textureType = TextureImporterType.Sprite;
        dividerImporter.spriteImportMode = SpriteImportMode.Single;
        dividerImporter.spriteBorder = Vector4.zero;
        dividerImporter.alphaIsTransparency = true;
        dividerImporter.textureCompression = TextureImporterCompression.Uncompressed;
        dividerImporter.SaveAndReimport();
        serialized.FindProperty("notebookDividerSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(dividerPath);
        serialized.FindProperty("notePaperSprite").objectReferenceValue = null;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Sprites/UI/MainMenu/Font/static/DMSans-Medium SDF.asset");
        serialized.FindProperty("notebookHeadingFont").objectReferenceValue = font;
        serialized.FindProperty("notebookBodyFont").objectReferenceValue = font;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);
        EditorSceneManager.SaveScene(workspace.gameObject.scene);
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Doğrula")]
    public static void Validate()
    {
        var good = SignalTrial.Simulate(Graph(0));
        Check(good.Measurable && good.MeetsTarget, "Authored target is unreachable.");
        for (int i = 1; i <= 4; i++)
        { var reading = SignalTrial.Simulate(Graph(i)); Check(reading.Measurable && !reading.MeetsTarget, "Distinct experiment outcomes must remain observable."); }
        var reversed = Graph(0); reversed.bonds[0].directed = false;
        Check(!SignalTrial.Simulate(reversed).Measurable, "Unoriented data was accepted.");
        var repeated = Graph(0);
        repeated.parts.Add(new SynthesisPartData { itemID = SignalTrial.Attenuator });
        repeated.parts.Add(new SynthesisPartData { itemID = SignalTrial.Delay });
        repeated.bonds.Find(e => e.fromIndex == 3 && e.toIndex == 5).toIndex = 6;
        repeated.bonds.Find(e => e.fromIndex == 4 && e.toIndex == 5).toIndex = 7;
        repeated.bonds.Add(new SynthesisBondData { fromIndex = 6, toIndex = 5, directed = true });
        repeated.bonds.Add(new SynthesisBondData { fromIndex = 7, toIndex = 5, directed = true });
        var repeatReading = SignalTrial.Simulate(repeated);
        Check(repeatReading.Measurable && !repeatReading.MeetsTarget && Mathf.Abs(repeatReading.pulses[0].height - .25f) < .01f
            && Mathf.Abs(repeatReading.pulses[1].time - .82f) < .01f, "Repeated processors or a graph larger than the catalog were rejected.");
        var twoSamples = Graph(0);
        twoSamples.parts.Add(new SynthesisPartData { itemID = SignalTrial.Narrow });
        twoSamples.bonds.Find(e => e.fromIndex == 2 && e.toIndex == 4).toIndex = 6;
        twoSamples.bonds.Add(new SynthesisBondData { fromIndex = 6, toIndex = 4, directed = true });
        var sampleReading = SignalTrial.Simulate(twoSamples);
        Check(sampleReading.Measurable && !sampleReading.MeetsTarget && !string.IsNullOrEmpty(sampleReading.targetFeedback), "The quest's one-sample condition must allow observation and explain its rejection.");
        foreach (var size in new[] { new Vector2(262, 120), new Vector2(469, 71), new Vector2(304, 103) })
        {
            Rect plot = SignalTrialGraphic.ComparisonPlot(size);
            Check(Mathf.Abs(plot.width * size.x / (plot.height * size.y) - 3) < .001f, "Comparison chart was stretched by its card.");
        }
        var content = SignalTrial.CreateContent(AssetDatabase.LoadAssetAtPath<FormulaItemSO>("Assets/Scripts/Scriptables/Formulas/SignalPartTemplate.asset"));
        var host = new GameObject("Signal port check (temporary)");
        try
        {
            Check(content.Quests.Count == 1 && content.Parts.Count == 7, "Catalog or quest count wrong.");
            foreach (var part in content.Parts)
            {
                var obj = UnityEngine.Object.Instantiate(part.chalkPrefab, host.transform);
                foreach (var node in obj.GetComponentsInChildren<FormulaNode>(true)) typeof(FormulaNode).GetMethod("Awake", Private).Invoke(node, null);
                var formula = obj.GetComponent<DraggableFormula>(); formula.Initialize(part);
                Check(formula.CanMoveOnBoard || !Application.isPlaying, "Chalk sketches cannot be moved.");
                int inputs = 0, outputs = 0;
                foreach (var port in obj.GetComponentsInChildren<SignalPort>()) if (port.output) outputs++; else inputs++;
                Check(inputs == SignalTrial.Inputs(part.itemID) && outputs == SignalTrial.Outputs(part.itemID), "Physical ports differ from the catalog.");
                foreach (var pages in content.Books)
                    foreach (var page in pages)
                        foreach (var note in page.blocks)
                            Check(note.blockType != NotebookBlockType.Quest || note.questID == SignalTrial.QuestID, "Previous target leaked into the new test.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); content.Dispose(); }
        Debug.Log("SIGNAL TRIAL VALIDATION PASS: repeatable processors, quest-specific quantity, fixed chart proportions, movable sketches, model outcomes and physical port counts.");
    }

    // Fixtures are explicit developer commands, never run at normal game start.
    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Defter Arşivini Doğrula")]
    public static void ValidateArchive()
    {
        var workspace = NotebookWorkspaceController.Instance; var notebook = NotebookUI.Instance;
        Check(Application.isPlaying && workspace != null && workspace.IsActive && workspace.IsArchiveNotebook, "Open the research notebook first.");
        int StoredCount()
        {
            int total = notebook.collectedQuests.Count;
            foreach (var saved in notebook.workspacePages) total += saved.blocks.Count;
            return total;
        }
        workspace.GoToPage(0); Canvas.ForceUpdateCanvases();
        var content = (RectTransform)typeof(NotebookWorkspaceController).GetField("partsContent", Private).GetValue(workspace);
        int original = StoredCount(); var ids = new HashSet<string>(); var expected = new HashSet<string>();
        foreach (var saved in notebook.workspacePages)
            foreach (var note in saved.blocks) if (note.blockType == NotebookBlockType.PartClue) expected.Add(note.itemID);
        Check(expected.Count > 0, "Collect atlas parts first.");
        int pages = (expected.Count + 3) / 4;
        for (int index = 0; index < pages; index++)
        {
            workspace.GoToPage(index); Canvas.ForceUpdateCanvases();
            var blocks = content.GetComponentsInChildren<NotebookDraggableBlock>();
            Check(blocks.Length > 0 && blocks.Length <= 4, "A part page must contain up to four cards.");
            var occupied = new List<Rect>();
            foreach (var block in blocks)
            {
                var rect = (RectTransform)block.transform;
                Check(block.Data.blockType == NotebookBlockType.PartClue && ids.Add(block.Data.itemID), "Archive contains mixed categories or duplicate parts.");
                var card = new Rect(rect.anchoredPosition.x - rect.rect.width / 2, rect.anchoredPosition.y - rect.rect.height, rect.rect.width, rect.rect.height);
                var pageSize = ((RectTransform)content.parent).rect.size;
                Check(card.xMin >= -pageSize.x / 2 && card.xMax <= pageSize.x / 2 && card.yMin >= -pageSize.y && card.yMax <= 0, "Card extends beyond its page.");
                Check(!occupied.Exists(r => r.Overlaps(card)), "Part cards overlap."); occupied.Add(card);
                foreach (var text in block.GetComponentsInChildren<TMP_Text>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Archive text overflow: " + block.Data.title); }
            }
        }
        Check(ids.SetEquals(expected), "Pagination hid or duplicated collected parts.");
        workspace.GoToPage(0); Canvas.ForceUpdateCanvases();
        var notes = content.GetComponentsInChildren<NotebookDraggableBlock>();
        workspace.DeleteBlock(notes[0]);
        Check(StoredCount() == original, "Right click lost a collected note.");
        typeof(NotebookWorkspaceController).GetMethod("StoreArchiveNote", Private).Invoke(workspace, new object[] { notes[0].Data });
        Check(StoredCount() == original, "Recollecting a part duplicated its archive entry.");
        workspace.ShowInformation(); workspace.ShowCollectedTargets(); workspace.GoToPage(0);
        Check(StoredCount() == original, "Changing archive sections lost a note.");
        var savedData = new GameData(); notebook.SaveData(ref savedData);
        string archiveJson = JsonUtility.ToJson(savedData);
        notebook.LoadData(JsonUtility.FromJson<GameData>(archiveJson));
        var reloadedData = new GameData(); notebook.SaveData(ref reloadedData);
        Check(JsonUtility.ToJson(reloadedData) == archiveJson && StoredCount() == original, "Notebook JSON roundtrip changed collected notes or old page data.");
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        Check(page.GetComponent<UnityEngine.UI.ScrollRect>() == null, "Parts still use vertical scrolling.");
        Check(!page.GetComponentInChildren<NotebookFreehandDrawer>(true).gameObject.activeSelf, "Notebook is still a free experiment canvas.");
        Debug.Log("SIGNAL ARCHIVE PASS: four cards per page, every collected part accessible without scrolling, no overlap or text overflow, reusable protected notes and notebook JSON roundtrip.");
    }

    private static SynthesisGraphData Graph(int variant)
    {
        var graph = new SynthesisGraphData();
        foreach (string id in new[] { SignalTrial.Source, variant == 1 ? SignalTrial.Wide : SignalTrial.Narrow,
            SignalTrial.Splitter, SignalTrial.Attenuator, SignalTrial.Delay, SignalTrial.Meter })
            graph.parts.Add(new SynthesisPartData { itemID = id, capacity = SignalTrial.Inputs(id) + SignalTrial.Outputs(id) });
        void Edge(int a, int b) => graph.bonds.Add(new SynthesisBondData { fromIndex = a, toIndex = b, directed = true });
        if (variant == 2)
        { Edge(0, 1); Edge(1, 3); Edge(3, 2); Edge(2, 5); Edge(2, 4); Edge(4, 5); }
        else if (variant == 3)
        { Edge(0, 1); Edge(1, 4); Edge(4, 2); Edge(2, 3); Edge(3, 5); Edge(2, 5); }
        else if (variant == 4)
        { Edge(0, 1); Edge(1, 2); Edge(2, 5); Edge(2, 4); Edge(4, 3); Edge(3, 5); }
        else
        { Edge(0, 1); Edge(1, 2); Edge(2, 3); Edge(3, 5); Edge(2, 4); Edge(4, 5); }
        return graph;
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Canlı Akışı Doğrula")]
    public static void ValidateLive()
    {
        var trial = ResearchPuzzleTrial.Active; var experiment = SignalExperiment.Active;
        Check(Application.isPlaying && experiment != null && trial.Board.isMachineActive, "Open the new trial board first.");
        var board = trial.Board; var graph = Graph(0); var workspace = NotebookWorkspaceController.Instance; var notebook = NotebookUI.Instance;
        int storedParts = 0;
        foreach (var savedPage in notebook.workspacePages) storedParts += savedPage.blocks.FindAll(n => n.blockType == NotebookBlockType.PartClue).Count;
        workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases();
        var target = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None), b =>
            !b.IsSource && b.Data.blockType == NotebookBlockType.Quest && b.Data.questID == SignalTrial.QuestID);
        Check(target != null, "Collect the target from its actual book.");
        var area = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(board.EnsureDocuments());
        Check(workspace.TryDropBlock(target, Camera.main.WorldToScreenPoint(area.position)), "Target drop failed.");
        board.EnsureDocuments().SetDocumentPanelOpen(false, true);
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var window = (RectTransform)page.parent; var bounds = (RectTransform)window.parent; var original = window.anchoredPosition;
        window.anchoredPosition = new Vector2(-bounds.rect.width + 290, original.y);
        var positions = new[] { new Vector2(.28f, .23f), new Vector2(-.35f, .23f), new Vector2(-1.05f, .23f),
            new Vector2(.25f, -.65f), new Vector2(-.45f, -.65f), new Vector2(-1.25f, -.65f) };
        if (trial.allowGameplayScene)
            for (int i = 0; i < positions.Length; i++) positions[i] = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds,
                    new Vector2(.2f + (i % 3) * .25f, i < 3 ? .7f : .3f));
        var parts = new List<DraggableFormula>();
        try
        {
            for (int i = 0; i < graph.parts.Count; i++)
            {
                string id = graph.parts[i].itemID;
                int index = notebook.workspacePages.FindIndex(p => p.blocks.Exists(b => b.blockType == NotebookBlockType.PartClue && b.itemID == id));
                Check(index >= 0, "Collect actual atlas notes before this check.");
                Check(workspace.ShowCollectedPart(id), "Collected part is missing from the archive."); Canvas.ForceUpdateCanvases();
                var note = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None), b => !b.IsSource && b.isActiveAndEnabled && b.Data.itemID == id);
                Vector3 point = board.boardTransform.TransformPoint(new Vector3(positions[i].x, positions[i].y, .02f));
                if (trial.allowGameplayScene)
                {
                    // Keep the movable notebook on the opposite side of this actual drop point.
                    float halfWidth = window.rect.width * window.localScale.x * .5f + 50;
                    window.anchoredPosition = new Vector2(Camera.main.WorldToScreenPoint(point).x < Screen.width * .5f
                        ? -halfWidth : -bounds.rect.width + halfWidth, original.y);
                    Canvas.ForceUpdateCanvases();
                }
                Check(note != null && workspace.TryDropBlock(note, Camera.main.WorldToScreenPoint(point)), "Notebook to board drop failed.");
                parts.Add(Array.Find(board.boardTransform.GetComponentsInChildren<DraggableFormula>(), p => p.formulaData.itemID == id));
            }
            foreach (var edge in graph.bonds)
            {
                var a = Array.Find(parts[edge.fromIndex].GetOwnNodes(), n => n.gameObject.activeSelf && !n.isOccupied && n.GetComponent<SignalPort>().output);
                var b = Array.Find(parts[edge.toIndex].GetOwnNodes(), n => n.gameObject.activeSelf && !n.isOccupied && !n.GetComponent<SignalPort>().output);
                Check(a != null && b != null && a.CanConnect(b.parentFormula, b), "Directed physical ports did not connect.");
                a.ConnectTo(b); Check(a.connectedNode == b, "Physical connection failed.");
            }
            board.AttemptSynthesis();
            Check(experiment.LastReading.MeetsTarget && !trial.Completed.Contains(SignalTrial.QuestID), "Board must measure without completing the target.");
            var saved = new GameData(); board.SaveData(ref saved);
            board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(saved)));
            var restored = board.boardTransform.GetComponentsInChildren<DraggableFormula>();
            Check(restored.Length == 6 && SignalTrial.Simulate(board.BuildGraph(restored[0])).MeetsTarget, "Board roundtrip lost directed port meaning.");
            var root = restored[0].GetRootFormula();
            Check(root.CanMoveOnBoard && Array.TrueForAll(restored, p => p.transform.parent == board.boardTransform), "Connected parts are still parented as a movement group.");
            var stationary = Array.Find(restored, p => p != root); var stationaryPosition = stationary.transform.position;
            var beforeMove = root.transform.position; root.transform.position += board.boardTransform.right * .08f;
            root.SyncMovementTarget();
            Check(SignalTrial.Simulate(board.BuildGraph(root)).MeetsTarget, "Moving a connected group changed its experiment.");
            Check(Vector3.Distance(stationary.transform.position, stationaryPosition) < .0001f, "Moving one part also moved its neighbor.");
            root.transform.position = beforeMove; root.SyncMovementTarget();
            board.EnsureDocuments().SetDocumentPanelOpen(false, true);
            Check(workspace.ShowCollectedPart(SignalTrial.Attenuator), "Repeated part is missing."); Canvas.ForceUpdateCanvases();
            var repeatNote = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None), b => !b.IsSource && b.isActiveAndEnabled && b.Data.itemID == SignalTrial.Attenuator);
            Vector2 repeatPosition = trial.allowGameplayScene ? Vector2.Lerp(board.minBounds, board.maxBounds, .5f) : new Vector2(.3f, -.7f);
            var repeatPoint = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(repeatPosition.x, repeatPosition.y, .02f)));
            Check(repeatNote != null && workspace.TryDropBlock(repeatNote, repeatPoint), "A collected drawing cannot be reused on the board.");
            var extraPart = Array.Find(board.boardTransform.GetComponentsInChildren<DraggableFormula>(), p => Array.IndexOf(restored, p) < 0);
            Check(extraPart != null && extraPart.formulaData.itemID == SignalTrial.Attenuator, "Repeated drawing did not create another instance.");
            board.RemoveFormula(extraPart);
            int retainedParts = 0;
            foreach (var savedPage in notebook.workspacePages) retainedParts += savedPage.blocks.FindAll(n => n.blockType == NotebookBlockType.PartClue).Count;
            Check(retainedParts == storedParts, "Board placement consumed notebook drawings.");
            Debug.Log("SIGNAL TRIAL LIVE PASS: collected target, real notebook drops, distinct physical parts, directed connections, measurement, board JSON roundtrip; task still incomplete.");
        }
        finally { window.anchoredPosition = original; }
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Kart Panosunu Doğrula")]
    public static void ValidateCards()
    {
        var workspace = NotebookWorkspaceController.Instance; var board = ChalkboardManager.Instance;
        Check(Application.isPlaying && board != null && board.isMachineActive && board.PinnedQuest != null, "Collect a target and open its board first.");
        var notebook = NotebookUI.Instance;
        var expected = new HashSet<string>();
        foreach (var note in PuzzleInformation.NotebookNotes(notebook))
            if (!SignalExperiment.IsMeasurementNote(note)) expected.Add(note.title + "|" + note.description);
        Check(expected.Count > 2, "Collect several book records to check multiple pages.");
        // Use the same buttons as the player to visit each record page.
        typeof(NotebookWorkspaceController).GetField("informationArchivePage", Private).SetValue(workspace, 0);
        workspace.ShowInformation(); Canvas.ForceUpdateCanvases();
        var panel = (RectTransform)typeof(NotebookWorkspaceController).GetField("informationPanel", Private).GetValue(workspace);
        var content = (RectTransform)typeof(NotebookWorkspaceController).GetField("informationContent", Private).GetValue(workspace);
        var pager = (GameObject)typeof(NotebookWorkspaceController).GetField("partsPager", Private).GetValue(workspace);
        Check(panel.GetComponent<UnityEngine.UI.ScrollRect>() == null, "Record view should use pages, not scrolling.");
        var found = new HashSet<string>(); NotebookBlockData firstNote = null;
        for (int page = 0; page < (expected.Count + 1) / 2; page++)
        {
            Canvas.ForceUpdateCanvases();
            var cards = content.GetComponentsInChildren<NotebookDraggableBlock>();
            Check(cards.Length > 0 && cards.Length <= 2, "A record page must show at most two cards.");
            foreach (var card in cards)
            {
                firstNote ??= card.Data.Clone();
                Check(found.Add(card.Data.title + "|" + card.Data.description), "A record appeared on multiple pages.");
                foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Record text overflow: " + card.Data.title); }
            }
            if (page + 1 < (expected.Count + 1) / 2) pager.transform.GetChild(1).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }
        Check(found.SetEquals(expected), "Some collected records are inaccessible.");
        var docs = board.EnsureDocuments();
        var target = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(docs);
        var surface = (RectTransform)target.parent;
        Check(docs.WorkingMaxX == board.maxBounds.x, "The old reserved document column remains.");
        Check(docs.TryDropDocument(firstNote, Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(.95f, -.15f, .04f)))), "Record cannot be pinned freely.");
        docs.SetDocumentPanelOpen(true, true);
        var noteCards = (Dictionary<string, RectTransform>)typeof(ChalkboardDocuments).GetField("noteCards", Private).GetValue(docs);
        var pinned = new List<RectTransform>(noteCards.Values)[0];
        var targetPosition = target.anchoredPosition;
        foreach (var card in new[] { pinned })
        {
            var before = card.anchoredPosition;
            var raycaster = surface.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>();
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                position = Camera.main.WorldToScreenPoint(card.position),
                pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult { module = raycaster } };
            var drag = card.GetComponent<DraggableWorkspaceItem>();
            drag.OnBeginDrag(pointer); pointer.position += new Vector2(30, -22); drag.OnDrag(pointer); drag.OnEndDrag(pointer);
            Check(Vector2.Distance(card.anchoredPosition, before) > 1, "Pinned card cannot be dragged.");
            foreach (var text in card.GetComponentsInChildren<TMP_Text>())
            { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Board card text overflow: " + text.text); }
        }
        Check(target.GetComponent<DraggableWorkspaceItem>() == null && target.anchoredPosition == targetPosition, "The target must stay in its fixed slot.");
        var saved = new GameData(); board.SaveData(ref saved);
        Check(saved.savedChalkboard.documents.Count >= 1, "Card positions were not saved.");
        board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(saved)));
        var again = new GameData(); board.SaveData(ref again);
        foreach (var item in saved.savedChalkboard.documents)
            Check(again.savedChalkboard.documents.Exists(n => n.id == item.id && Vector2.Distance(n.position, item.position) < .01f), "A card moved after reloading.");
        Debug.Log("SIGNAL PINBOARD PASS: two records per page, compact notebook and board cards, free pinning, actual card drag handlers and saved layout roundtrip.");
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Stor Panosunu Doğrula")]
    public static void ValidateShade()
    {
        var board = ChalkboardManager.Instance;
        Check(Application.isPlaying && board != null && board.isMachineActive && board.PinnedQuest != null, "Open a board with collected documents.");
        var docs = board.EnsureDocuments();
        var viewport = (RectTransform)typeof(ChalkboardDocuments).GetField("shadeViewport", Private).GetValue(docs);
        var toggle = (RectTransform)typeof(ChalkboardDocuments).GetField("shadeToggle", Private).GetValue(docs);
        var target = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(docs);
        docs.SetDocumentPanelOpen(true, true);
        var saved = new GameData(); board.SaveData(ref saved);
        Check(saved.savedChalkboard.documentShadeOpen, "The open document panel wasn't saved.");
        var outputSource = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None), b => b.IsSource && (b.Data.itemID ?? "").StartsWith("signal-result/"));
        Check(outputSource != null && !outputSource.transform.IsChildOf(viewport), "Measurement paper was hidden inside the document panel.");
        var experiment = SignalExperiment.Active;
        var paperInput = outputSource.GetComponentInParent<Canvas>().GetComponent<CanvasGroup>();
        var refreshPaper = typeof(SignalExperiment).GetMethod("LateUpdate", Private);
        refreshPaper.Invoke(experiment, null);
        Check(paperInput.alpha == 1 && paperInput.blocksRaycasts, "Opening documents hides or disables the measurement paper.");
        var paperPosition = board.boardTransform.InverseTransformPoint(paperInput.transform.position);
        Check(paperPosition.x < -.8f && paperPosition.y < -.5f, "Measurement paper is not in the board's lower-right corner.");
        docs.SetDocumentPanelOpen(false, true);
        refreshPaper.Invoke(experiment, null);
        Check(paperInput.alpha == 1 && paperInput.blocksRaycasts, "Closing documents did not restore the measurement paper.");
        Check(!viewport.gameObject.activeSelf && target.gameObject.activeInHierarchy && outputSource.gameObject.activeInHierarchy
            && toggle.gameObject.activeInHierarchy, "A closed panel still covers the experiment area or hides its handle.");
        var closed = new GameData(); board.SaveData(ref closed);
        Check(!closed.savedChalkboard.documentShadeOpen && closed.savedChalkboard.documents.Count == saved.savedChalkboard.documents.Count,
            "Closing the panel lost pinned documents.");
        board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(closed)));
        Check(!docs.IsDocumentPanelOpen && !viewport.gameObject.activeSelf, "Reloading reopened a closed document panel.");
        toggle.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(docs.IsDocumentPanelOpen && viewport.gameObject.activeSelf, "The actual panel handle does not open it.");
        docs.SetDocumentPanelOpen(true, true);
        var again = new GameData(); board.SaveData(ref again);
        foreach (var item in saved.savedChalkboard.documents)
            Check(again.savedChalkboard.documents.Exists(n => n.id == item.id && Vector2.Distance(n.position, item.position) < .01f), "Opening the panel changed the card arrangement.");
        Check(again.savedChalkboard.parts.Count == saved.savedChalkboard.parts.Count && again.savedChalkboard.bonds.Count == saved.savedChalkboard.bonds.Count,
            "Panel controls changed the experiment.");
        Debug.Log("SIGNAL SHADE PASS: bottom-right measurement paper stays visible and usable with documents open or closed; layout and experiment survive folding and JSON roundtrip.");
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Ölçüm Arşivini Doğrula")]
    public static void ValidateSolutions()
    {
        var experiment = SignalExperiment.Active; var board = ChalkboardManager.Instance;
        var workspace = NotebookWorkspaceController.Instance; var notebook = NotebookUI.Instance;
        Check(Application.isPlaying && experiment != null && board.isMachineActive, "Open the current experiment board first.");
        var docs = board.EnsureDocuments(); docs.SetDocumentPanelOpen(false, true);
        var previous = PuzzleInformation.NotebookNotes(notebook).Find(SignalExperiment.IsMeasurementNote);
        Check(previous != null, "Collect one actual measurement first.");
        string snapshot = JsonUtility.ToJson(previous);
        experiment.Measure(Graph(4)); SignalTrialPlayChecks.CollectOutput();
        Check(!docs.IsDocumentPanelOpen, "Taking a measurement or collecting output opened the document panel.");
        experiment.Measure(Graph(0)); SignalTrialPlayChecks.CollectOutput();
        Check(JsonUtility.ToJson(previous) == snapshot, "A new measurement overwrote an archived output.");
        var outputs = PuzzleInformation.NotebookNotes(notebook).FindAll(SignalExperiment.IsMeasurementNote);
        Check(outputs.Count >= 3, "Repeated measurements were not archived separately.");
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var content = (RectTransform)typeof(NotebookWorkspaceController).GetField("informationContent", Private).GetValue(workspace);
        var pager = (GameObject)typeof(NotebookWorkspaceController).GetField("partsPager", Private).GetValue(workspace);
        typeof(NotebookWorkspaceController).GetField("solutionsArchivePage", Private).SetValue(workspace, 0);
        page.parent.Find("Ölçümler").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        var found = new HashSet<string>();
        for (int index = 0; index < (outputs.Count + 1) / 2; index++)
        {
            Canvas.ForceUpdateCanvases();
            var cards = content.GetComponentsInChildren<NotebookDraggableBlock>();
            Check(cards.Length > 0 && cards.Length <= 2, "Output archive is not paginated.");
            foreach (var card in cards)
            {
                Check(SignalExperiment.IsMeasurementNote(card.Data) && found.Add(card.Data.title), "Output archive contains a book clue or duplicate.");
                foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Output text overflow: " + card.Data.title); }
            }
            if (index + 1 < (outputs.Count + 1) / 2) pager.transform.GetChild(1).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }
        Check(found.Count == outputs.Count, "Archived measurements are inaccessible.");
        workspace.ShowInformation(); Canvas.ForceUpdateCanvases();
        foreach (var card in content.GetComponentsInChildren<NotebookDraggableBlock>())
            Check(!SignalExperiment.IsMeasurementNote(card.Data), "A measurement remains mixed with book records.");
        var saved = new GameData(); notebook.SaveData(ref saved);
        notebook.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(saved)));
        Check(PuzzleInformation.NotebookNotes(notebook).FindAll(SignalExperiment.IsMeasurementNote).Count == outputs.Count,
            "Notebook reload lost archived measurements.");
        workspace.ShowSolutions();
        Debug.Log("SIGNAL SOLUTIONS PASS: direct board output, separate two-card archive, immutable distinct measurements, no book-clue mixing and notebook JSON roundtrip.");
    }

    [MenuItem("Senzora/Çift Kayıt/Canlı/Belge Panosunu Aç Kapat")]
    public static void ToggleShade()
    {
        var docs = ChalkboardManager.Instance?.EnsureDocuments();
        if (Application.isPlaying && docs != null) docs.SetDocumentPanelOpen(!docs.IsDocumentPanelOpen);
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Defter Görünümünü Doğrula")]
    public static void ValidateNotebookAppearance()
    {
        var workspace = NotebookWorkspaceController.Instance;
        Check(Application.isPlaying && workspace != null && workspace.IsActive, "Open the notebook in the test scene first.");
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var window = (RectTransform)page.parent;
        var artwork = window.GetComponent<UnityEngine.UI.Image>().sprite;
        if (GameManager.Instance.activeInteraction != null)
        {
            var previousView = typeof(NotebookWorkspaceController).GetField("currentNotebookView", Private).GetValue(workspace);
            workspace.ToggleStandaloneNotebook(); workspace.BrowseStandaloneNotebook(1, 1);
            Check(!workspace.IsStandaloneOpen && Equals(previousView, typeof(NotebookWorkspaceController).GetField("currentNotebookView", Private).GetValue(workspace)), "Standalone keys changed a workstation interaction.");
        }
        Check(artwork != null && AssetDatabase.GetAssetPath(artwork) == "Assets/Sprites/UI/UIInGame/LaboratoryLedger.png", "The laboratory ledger artwork is missing.");
        var expectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Sprites/UI/MainMenu/Font/static/DMSans-Medium SDF.asset");
        Check(expectedFont != null && (TMP_FontAsset)typeof(NotebookWorkspaceController).GetField("notebookHeadingFont", Private).GetValue(workspace) == expectedFont
            && (TMP_FontAsset)typeof(NotebookWorkspaceController).GetField("notebookBodyFont", Private).GetValue(workspace) == expectedFont,
            "Notebook headings and body must use DM Sans Medium.");
        Vector2 size = window.sizeDelta;
        Check(Mathf.Abs(size.x - 560) < .01f, "The notebook width changed.");
        Check(Mathf.Abs(size.x / size.y - artwork.rect.width / artwork.rect.height) < .001f, "Notebook artwork is stretched.");
        var resetTurn = typeof(NotebookWorkspaceController).GetMethod("ResetNotebookPageTurn", Private);
        resetTurn.Invoke(workspace, null);
        var snapshot = new GameData(); NotebookUI.Instance.SaveData(ref snapshot);
        string before = JsonUtility.ToJson(snapshot);
        foreach (string tab in new[] { "Parçalar", "Bilgiler", "Hedefler", "Ölçümler" })
        {
            window.Find(tab).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            resetTurn.Invoke(workspace, null);
            Canvas.ForceUpdateCanvases();
            Check(window.sizeDelta == size, "Changing sections resized the notebook.");
            Check(window.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Length == 0, "An archive page has scrolling.");
            var cards = window.GetComponentsInChildren<NotebookDraggableBlock>();
            Check(cards.Length <= (tab == "Parçalar" ? 4 : 2), "The notebook page exceeds its fixed card capacity.");
            foreach (var card in cards)
            {
                var parent = (RectTransform)card.transform.parent.parent;
                var corners = new Vector3[4]; ((RectTransform)card.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                    Check(parent.rect.Contains(parent.InverseTransformPoint(corner)), "A card extends outside its notebook page.");
                foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Notebook text overflow: " + card.Data.title + " / " + text.text); }
            }
        }
        var after = new GameData(); NotebookUI.Instance.SaveData(ref after);
        Check(JsonUtility.ToJson(after) == before, "Browsing the new layout changed archived notes.");
        Debug.Log("NOTEBOOK APPEARANCE PASS: laboratory ledger, DM Sans Medium, fixed aspect and size across four tabs, four/two-card capacity, no archive scrolling, cards and text fit, archived data unchanged.");
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Serbest Defteri Doğrula")]
    public static void ValidateStandaloneNotebook()
    {
        var workspace = NotebookWorkspaceController.Instance;
        Check(Application.isPlaying && workspace != null && GameManager.Instance != null && !workspace.IsPaused
            && GameManager.Instance.activeInteraction == null, "Exit the current interaction and unpause first.");
        if (workspace.IsStandaloneOpen) workspace.ToggleStandaloneNotebook();
        workspace.ToggleStandaloneNotebook();
        Check(workspace.IsStandaloneOpen && workspace.IsActive && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Tab notebook didn't open with a free pointer.");
        var type = typeof(NotebookWorkspaceController);
        var resetTurn = type.GetMethod("ResetNotebookPageTurn", Private);
        var stepTurn = type.GetMethod("UpdateNotebookPageTurn", Private);
        var popup = type.GetMethod("UpdateNotebookPopup", Private);
        var elapsed = type.GetField("pageTurnElapsed", Private);
        var direction = type.GetField("pageTurnDirection", Private);
        var pageIndex = type.GetField("partsArchivePage", Private);
        var layerField = type.GetField("pageTurnLayer", Private);
        var page = (RectTransform)type.GetField("page", Private).GetValue(workspace);
        var window = (RectTransform)page.parent;
        popup.Invoke(workspace, new object[] { 1f });
        var surfaces = new[] { page, (RectTransform)type.GetField("targetPanel", Private).GetValue(workspace),
            (RectTransform)type.GetField("informationPanel", Private).GetValue(workspace) };
        var positions = Array.ConvertAll(surfaces, surface => surface.anchoredPosition);
        var alphas = Array.ConvertAll(surfaces, surface => surface.GetComponent<CanvasGroup>()?.alpha ?? 1f);
        Action<string> checkRestored = message =>
        {
            for (int i = 0; i < surfaces.Length; i++)
                Check(Vector2.Distance(surfaces[i].anchoredPosition, positions[i]) < .01f
                    && Mathf.Abs((surfaces[i].GetComponent<CanvasGroup>()?.alpha ?? 1f) - alphas[i]) < .001f,
                    message + ": a page retained its animation offset or opacity.");
            var layer = (RectTransform)layerField.GetValue(workspace);
            Check((float)elapsed.GetValue(workspace) < 0 && (layer == null || !layer.gameObject.activeSelf),
                message + ": a page turn remained active.");
        };
        var saved = new GameData(); NotebookUI.Instance.SaveData(ref saved);
        string before = JsonUtility.ToJson(saved);
        try
        {
            checkRestored("Opening the notebook");
            workspace.BrowseStandaloneNotebook(1, 0);
            Check(window.Find("Bilgiler/Seçili Bölüm").gameObject.activeSelf, "E didn't advance to information.");
            stepTurn.Invoke(workspace, new object[] { .08f });
            Check((float)elapsed.GetValue(workspace) > 0, "Changing sections did not start a page turn.");
            workspace.BrowseStandaloneNotebook(-1, 0);
            Check(window.Find("Parçalar/Seçili Bölüm").gameObject.activeSelf
                && (float)elapsed.GetValue(workspace) == 0 && (int)direction.GetValue(workspace) == -1,
                "Rapid Q didn't immediately reverse and restart the page turn.");
            stepTurn.Invoke(workspace, new object[] { .08f });
            workspace.BrowseStandaloneNotebook(1, 0);
            Check(window.Find("Bilgiler/Seçili Bölüm").gameObject.activeSelf
                && (float)elapsed.GetValue(workspace) == 0 && (int)direction.GetValue(workspace) == 1,
                "Rapid E didn't replace the previous turn.");
            var turnLayer = (RectTransform)layerField.GetValue(workspace);
            Check(turnLayer != null && turnLayer.gameObject.activeSelf, "The page-turn presentation is missing.");
            foreach (var graphic in turnLayer.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Check(!graphic.raycastTarget, "A page-turn overlay blocks notebook or drag input.");
            stepTurn.Invoke(workspace, new object[] { 1f });
            checkRestored("Finishing a rapid section change");
            Check(window.Find("Bilgiler/Seçili Bölüm").gameObject.activeSelf, "A stale turn overwrote the latest section.");

            workspace.BrowseStandaloneNotebook(-1, 0);
            workspace.BrowseStandaloneNotebook(-1, 0);
            Check(window.Find("Ölçümler/Seçili Bölüm").gameObject.activeSelf
                && (int)direction.GetValue(workspace) == -1, "Q didn't wrap backwards to measurements.");
            workspace.BrowseStandaloneNotebook(1, 0);
            Check(window.Find("Parçalar/Seçili Bölüm").gameObject.activeSelf
                && (int)direction.GetValue(workspace) == 1, "E didn't wrap forwards to parts.");
            workspace.GoToPage(0); resetTurn.Invoke(workspace, null);
            workspace.BrowseStandaloneNotebook(0, -1);
            Check((int)pageIndex.GetValue(workspace) == 0, "Wheel navigation passed the first page.");
            checkRestored("Browsing before the first page");
            workspace.GoToPage(int.MaxValue); stepTurn.Invoke(workspace, new object[] { 1f });
            int last = (int)pageIndex.GetValue(workspace);
            workspace.BrowseStandaloneNotebook(0, 1);
            Check((int)pageIndex.GetValue(workspace) == last, "Wheel navigation passed the last page.");
            checkRestored("Browsing after the last page");
            if (last > 0)
            {
                workspace.BrowseStandaloneNotebook(0, -1);
                Check((int)pageIndex.GetValue(workspace) == last - 1 && (float)elapsed.GetValue(workspace) == 0,
                    "Wheel navigation didn't immediately change the archive page and start its turn.");
                stepTurn.Invoke(workspace, new object[] { 1f });
                checkRestored("Finishing a wheel page change");
            }

            workspace.BrowseStandaloneNotebook(1, 0);
            stepTurn.Invoke(workspace, new object[] { .08f });
            Check(Vector2.Distance(surfaces[2].anchoredPosition, positions[2]) > .01f
                && surfaces[2].GetComponent<CanvasGroup>().alpha < alphas[2],
                "The pause check did not interrupt a moving, faded page.");
            var savedView = type.GetField("currentNotebookView", Private).GetValue(workspace);
            GameManager.Instance.isGamePaused = true;
            workspace.BrowseStandaloneNotebook(1, 1);
            Check(Equals(savedView, type.GetField("currentNotebookView", Private).GetValue(workspace)), "Pause didn't block navigation.");
            stepTurn.Invoke(workspace, new object[] { .08f });
            checkRestored("Pausing during a page turn");
            resetTurn.Invoke(workspace, null);
            checkRestored("Resetting an already canceled page turn");
            GameManager.Instance.isGamePaused = false;

            workspace.BrowseStandaloneNotebook(1, 0);
            stepTurn.Invoke(workspace, new object[] { .08f });
            Check((float)elapsed.GetValue(workspace) > 0, "The close check did not interrupt an active page turn.");
            window.Find("×").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(!workspace.IsStandaloneOpen && !workspace.IsActive && !Cursor.visible && Cursor.lockState == CursorLockMode.Locked,
                "Closing didn't return the pointer to gameplay.");
            checkRestored("Closing during a page turn");
            workspace.ToggleStandaloneNotebook(); popup.Invoke(workspace, new object[] { 1f });
            checkRestored("Reopening after an interrupted page turn");
            Check(window.Find("Parçalar/Seçili Bölüm").gameObject.activeSelf, "Reopening retained a stale section transition.");
            var after = new GameData(); NotebookUI.Instance.SaveData(ref after);
            Check(JsonUtility.ToJson(after) == before, "Page turns, pause or close/reopen changed archived notebook data.");
            workspace.ToggleStandaloneNotebook();
            Check(!workspace.IsActive, "Second Tab didn't close the notebook.");
            Debug.Log("STANDALONE NOTEBOOK PASS: Tab and cursor, rapid Q/E reversal and wrap, bounded wheel pages, non-raycasting overlays, pause and close/reopen restoration, archived data unchanged.");
        }
        finally
        {
            GameManager.Instance.isGamePaused = false;
            resetTurn.Invoke(workspace, null);
            if (workspace.IsStandaloneOpen) workspace.Close();
        }
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Defter Stili Deneme Notlarını Hazırla")]
    public static void PrepareNotebookAppearance()
    {
        Check(Application.isPlaying && ResearchPuzzleTrial.Active != null && GameManager.Instance.activeInteraction == null, "Start Play outside an interaction.");
        var workspace = NotebookWorkspaceController.Instance;
        if (!workspace.IsStandaloneOpen) workspace.ToggleStandaloneNotebook();
        var store = typeof(NotebookWorkspaceController).GetMethod("StoreArchiveNote", Private);
        foreach (var book in ResearchPuzzleTrial.Active.Content.Books)
            foreach (var page in book)
                foreach (var note in page.blocks) store.Invoke(workspace, new object[] { note });
        workspace.RefreshPage();
    }
}
