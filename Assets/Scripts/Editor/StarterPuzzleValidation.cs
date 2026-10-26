using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class StarterPuzzleValidation
{
    public const string QuestPath = "Assets/Scripts/Scriptables/Formulas/Starter/Quest_StarterX.asset";

    [MenuItem("Senzora/Doğrulama/Başlatıcı-X Döngüsü %#k")]
    public static void Run()
    {
        var blueprint = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(QuestPath);
        Check(blueprint != null, "Başlatıcı-X authored quest is missing.");
        Check(blueprint.clueParts.Count == 5 && blueprint.clueNotes.Count == 2, "Candidate or rule notes missing.");
        Check(blueprint.output == null, "Practice must not produce a new item.");
        Check(blueprint.nodes.FindAll(n => n.isUnknown).Count == 2, "Target must hide exactly two identities.");
        foreach (var part in blueprint.clueParts)
        {
            Check(part != null, "A starter candidate reference did not import.");
            Check(part.chalkPrefab != null, "Candidate chalk prefab did not import: " + part.name);
            Check(part.chalkPrefab.GetComponent<DraggableFormula>() != null, "Candidate prefab lacks DraggableFormula: " + part.name);
        }
        if (Application.isPlaying) throw new InvalidOperationException("Run the asset checks outside Play Mode.");
        var previous = ChalkboardManager.Instance;
        var host = new GameObject("Starter-X checks (temporary)");
        try
        {
            var manager = host.AddComponent<ChalkboardManager>();
            SetProperty(manager, "Instance", manager);
            manager.boardTransform = new GameObject("Board").transform;
            manager.boardTransform.SetParent(host.transform);
            manager.useProceduralQuests = false;
            manager.practiceQuest = blueprint;
            SetProperty(manager, "isMachineActive", true);
            var zone = new GameObject("Zone", typeof(BoxCollider), typeof(ReactionZone));
            zone.transform.SetParent(manager.boardTransform, false);
            zone.GetComponent<BoxCollider>().size = new Vector3(3.2f, 2.1f, 0.5f);
            manager.synthesisZone = zone.GetComponent<ReactionZone>();
            var u = Spawn(manager, blueprint.clueParts[0], new Vector3(0, 0.2f, 0.02f));
            var be = Spawn(manager, blueprint.clueParts[1], new Vector3(-0.6f, 0.2f, 0.02f));
            var rf = Spawn(manager, blueprint.clueParts[2], new Vector3(0, -0.5f, 0.02f));
            manager.AttemptSynthesis();
            Check(manager.LastSynthesisMessage.Contains("HEDEF"), "Practice bypassed collected target pinning.");
            Check(manager.TryPinQuest(blueprint.CreateQuest()), "Target could not be pinned.");
            Solve(u, be, rf);
            manager.AttemptSynthesis();
            Check(manager.LastSynthesisMessage == "Doğru bildin", "Correct solution failed: " + manager.LastSynthesisMessage);
            Check(manager.boardTransform.GetComponentsInChildren<DraggableFormula>().Length == 3, "Practice consumed inputs or created output.");
            manager.AttemptSynthesis();
            Check(manager.LastSynthesisMessage == "Doğru bildin", "Repeated testing broke practice mode.");
            var graph = manager.BuildGraph(u);
            graph.parts.Find(p => p.itemID == "starter-BE9").itemID = "starter-PO210";
            Check(!SynthesisGraphValidator.Validate(graph, blueprint.CreateQuest(), out _), "Wrong neutron candidate accepted.");
            graph = manager.BuildGraph(u);
            graph.parts.Find(p => p.itemID == "starter-REF").itemID = "starter-KALKAN";
            Check(!SynthesisGraphValidator.Validate(graph, blueprint.CreateQuest(), out _), "Wrong double-bond candidate accepted.");
            rf.GetOwnNodes()[0].Erase();
            manager.AttemptSynthesis();
            Check(manager.LastSynthesisMessage != "Doğru bildin", "Single bond accepted instead of required double.");
            rf.GetOwnNodes()[0].ConnectTo(u.GetOwnNodes()[1]);
            Spawn(manager, blueprint.clueParts[3], new Vector3(0.65f, 0.2f, 0.02f));
            manager.AttemptSynthesis();
            Check(manager.LastSynthesisMessage != "Doğru bildin", "Extra loose board piece was ignored.");
            var card = new GameObject("Rule card", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
            card.transform.SetParent(host.transform);
            card.GetComponent<NotebookDraggableBlock>().Initialize(new NotebookBlockData { blockType = NotebookBlockType.RuleClue, description = blueprint.clueNotes[0] }, true);
            Check(card.transform.Find("Bant") != null, "Rule note lost its visual identity.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            SetProperty(null, "Instance", previous);
        }
        Debug.Log("STARTER PASS: 5 candidates, 3 note types, open sockets, marked double-bond progress, wrong candidates, pin gate, repeated non-consuming success.");
    }

    [MenuItem("Senzora/Sentez Blockout/Başlatıcı-X Bulmacasını Açık Sahneye Bağla %&k")]
    public static void SetupScene()
    {
        Check(!Application.isPlaying, "Stop Play Mode first.");
        var root = UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutScene>();
        Check(root != null, "Open the synthesis blockout scene first.");
        Undo.RecordObject(root, "Configure Starter-X puzzle");
        root.starterPuzzle = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(QuestPath);
        Check(root.starterPuzzle != null, "Starter quest asset missing.");
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        Debug.Log("STARTER SETUP: current scene preserved; Starter-X configured.");
    }

    [MenuItem("Senzora/Doğrulama/Başlatıcı-X Notlarını Deftere Kopyala %&l")]
    public static void CollectLiveNotes()
    {
        Check(Application.isPlaying, "Enter Play Mode and open the book first.");
        var workspace = NotebookWorkspaceController.Instance;
        ValidateLiveBookDrawing();
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
        var targetArea = (RectTransform)typeof(NotebookWorkspaceController).GetField("targetDropZone", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
        var sources = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None);
        int copied = 0;
        foreach (var source in sources)
        {
            if (!source.IsSource || !source.gameObject.activeInHierarchy) continue;
            Vector2 local = source.Data.blockType == NotebookBlockType.Quest ? new Vector2(0, 170)
                : source.Data.itemID == "starter-U235" ? new Vector2(-120, 15)
                : source.Data.itemID == "starter-BE9" ? new Vector2(120, 15)
                : source.Data.itemID == "starter-REF" ? new Vector2(-120, -140) : new Vector2(120, -140);
            if (source.Data.blockType == NotebookBlockType.RuleClue) continue;
            workspace.GoToPage(NotebookUI.Instance.currentWorkspacePage);
            if (source.Data.blockType != NotebookBlockType.Quest)
                typeof(NotebookWorkspaceController).GetMethod("ChangePage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workspace, new object[] { 0 });
            Canvas.ForceUpdateCanvases();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, page.TransformPoint(local));
            if (source.Data.blockType == NotebookBlockType.Quest)
            {
                Check(!workspace.TryDropBlock(source, screen), "Target silently stacked when dropped on ordinary paper.");
                screen = RectTransformUtility.WorldToScreenPoint(null, targetArea.position);
            }
            else Check(!workspace.TryDropBlock(source, RectTransformUtility.WorldToScreenPoint(null, targetArea.position)), "Part note entered target collection.");
            Check(workspace.TryDropBlock(source, screen), "Source note could not be copied.");
            if (source.Data.blockType == NotebookBlockType.Quest)
            {
                Check(workspace.TryDropBlock(source, screen) && NotebookUI.Instance.collectedQuests.Count == 1,
                    "Collecting the same target duplicated it.");
            }
            copied++;
        }
        Check(copied >= 4, "Open the first book spread with target and three parts.");
        var notebook = NotebookUI.Instance;
        int originalPage = notebook.currentWorkspacePage;
        notebook.workspacePages.Add(new NotebookPageSaveData());
        notebook.currentWorkspacePage = notebook.workspacePages.Count - 1;
        workspace.RefreshPage();
        typeof(NotebookWorkspaceController).GetMethod("ChangePage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workspace, new object[] { 0 });
        int rules = 0;
        foreach (var source in sources)
        {
            if (!source.IsSource || !source.gameObject.activeInHierarchy || source.Data.blockType != NotebookBlockType.RuleClue) continue;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, page.TransformPoint(new Vector3(0, 130 - rules * 220, 0)));
            Check(workspace.TryDropBlock(source, screen), "Rule note could not be collected.");
            rules++;
        }
        Check(rules == 2, "Both rule clues must be collectible.");
        notebook.currentWorkspacePage = originalPage;
        workspace.RefreshPage();
        Debug.Log("STARTER LIVE COLLECT PASS: source notes copied through the real notebook drop path.");
    }

    public static void ValidateLiveGuide()
    {
        var workspace = NotebookWorkspaceController.Instance;
        var notebook = NotebookUI.Instance;
        int page = notebook.currentWorkspacePage;
        int targets = notebook.collectedQuests.Count;
        var drawing = workspace.DragCanvas.GetComponentInChildren<NotebookFreehandDrawer>(true);
        drawing.BeginStroke(new Vector2(-170, -40), false);
        drawing.ContinueStroke(new Vector2(-170, -10));
        drawing.EndStroke();
        string before = JsonUtility.ToJson(notebook.EnsureWorkspacePage());
        Check(targets == 1 && notebook.workspacePages.TrueForAll(p => p.blocks.TrueForAll(b => b.blockType != NotebookBlockType.Quest)), "Target collection was not separated from notes.");
        workspace.ToggleSynthesisGuide();
        var panel = (RectTransform)typeof(NotebookWorkspaceController).GetField("guidePanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(workspace);
        var entries = (List<NotebookSynthesisGuide.Entry>)typeof(NotebookWorkspaceController).GetField("guideEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(workspace);
        int practiceCount = Mathf.Max(1, ChalkboardManager.Instance.practicePuzzles.Count);
        Check(panel.gameObject.activeInHierarchy && entries.Count == practiceCount + 2, "Guide must include the practice targets and both archived recipe assets.");
        var diagram = (QuestDiagramGraphic)typeof(NotebookWorkspaceController).GetField("guideDiagram", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(workspace);
        Canvas.ForceUpdateCanvases();
        string labels = string.Join(" ", Array.ConvertAll(diagram.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text));
        Check(labels.Contains("BE-9") && labels.Contains("REF") && !labels.Contains("?1"), "Guide diagram did not reveal correct parts.");
        workspace.ToggleSynthesisGuide();
        Check(!panel.gameObject.activeSelf && notebook.currentWorkspacePage == page && notebook.collectedQuests.Count == targets
            && before == JsonUtility.ToJson(notebook.EnsureWorkspacePage()), "Closing guide changed the notebook page, drawing or targets.");
        workspace.ToggleSynthesisGuide();
        Debug.Log("NOTEBOOK GUIDE PASS: correct target destination, full guide, revealed copy and reversible toggle.");
    }

    private static void ValidateLiveBookDrawing()
    {
        var drawer = UnityEngine.Object.FindFirstObjectByType<BookPageFreehandDrawer>();
        Check(drawer != null, "Physical book drawing layer missing.");
        var surface = drawer.GetComponentInParent<Canvas>().GetComponentInParent<BookPageBlockSurface>();
        bool rightPage = (bool)typeof(BookPageFreehandDrawer).GetField("RightPage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(drawer);
        var map = typeof(BookPageBlockSurface).GetMethod("MapPageWorld", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector2 startUV = new Vector2(.035f, .45f);
        Vector3 world = (Vector3)map.Invoke(surface, new object[] { startUV, rightPage });
        var pointer = new PointerEventData(EventSystem.current) {
            position = Camera.main.WorldToScreenPoint(world), button = PointerEventData.InputButton.Left,
            pointerPressRaycast = new RaycastResult { module = drawer.GetComponentInParent<GraphicRaycaster>() } };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponent<BookPageFreehandDrawer>() == drawer,
            "Blank physical book area does not receive actual UI drawing input.");
        drawer.OnPointerDown(pointer);
        Check(drawer.Strokes.Count == 1, "Physical book left click did not draw.");
        Check(Vector2.Distance(drawer.Strokes[0].points[0], startUV) < .02f, "Book input drifted from UV position.");
        Vector3 end = (Vector3)map.Invoke(surface, new object[] { new Vector2(.06f, .65f), rightPage });
        pointer.position = Camera.main.WorldToScreenPoint(end);
        drawer.OnDrag(pointer);
        drawer.OnPointerUp(pointer);
        Check(drawer.Strokes[0].points.Count > 1, "Physical book stroke did not extend.");
        pointer.button = PointerEventData.InputButton.Right;
        drawer.OnPointerDown(pointer);
        drawer.OnPointerUp(pointer);
        Check(drawer.Strokes.Count == 0, "Physical book right click did not erase.");
        drawer.OnPointerDown(new PointerEventData(EventSystem.current) {
            position = Camera.main.WorldToScreenPoint(world), button = PointerEventData.InputButton.Left,
            pointerPressRaycast = pointer.pointerPressRaycast });
        drawer.ContinueStroke(new Vector2(-220, 105));
        drawer.EndStroke();
        Debug.Log("BOOK DRAWING PASS: mouse input maps to real UV, draws and erases.");
    }

    [MenuItem("Senzora/Doğrulama/Başlatıcı-X Tahta Döngüsünü Dene %&m")]
    public static void SolveLiveNotes()
    {
        Check(Application.isPlaying && ChalkboardManager.Instance.isMachineActive, "Open the board first.");
        var board = ChalkboardManager.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        // Önce hedefler bölümünden şemayı sabitle, sonra normal notlardan parçaları aktar.
        workspace.ShowCollectedTargets();
        Canvas.ForceUpdateCanvases();
        var target = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
            b => !b.IsSource && b.Data.blockType == NotebookBlockType.Quest);
        Check(target != null, "Collected target card is not visible in its section.");
        Vector2 targetPoint = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(.3f, .05f, .02f)));
        Check(board.TryGetBoardPoint(targetPoint, out _), "Target drop does not hit the physical board.");
        Check(workspace.TryDropBlock(target, targetPoint), "Collected target could not be pinned from its section; active=" + workspace.IsActive + ", paused=" + workspace.IsPaused + ", quest=" + target.Data.questID + ", target=" + targetPoint);
        typeof(NotebookWorkspaceController).GetMethod("ChangePage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workspace, new object[] { 0 });
        var blocks = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None);
        foreach (var block in blocks)
        {
            if (block.IsSource || !block.gameObject.activeInHierarchy || block.Data.blockType == NotebookBlockType.RuleClue) continue;
            Vector3 local = block.Data.itemID == "starter-BE9" ? new Vector3(.4f, 0.05f, 0.02f)
                : block.Data.itemID == "starter-REF" ? new Vector3(-.4f, -0.55f, 0.02f) : new Vector3(-.4f, 0.05f, 0.02f);
            Check(workspace.TryDropBlock(block, Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(local))), "Notebook-to-board drop failed: " + block.Data.title);
        }
        var parts = board.boardTransform.GetComponentsInChildren<DraggableFormula>();
        var u = Array.Find(parts, p => p.formulaData.itemID == "starter-U235");
        var be = Array.Find(parts, p => p.formulaData.itemID == "starter-BE9");
        var rf = Array.Find(parts, p => p.formulaData.itemID == "starter-REF");
        Solve(u, be, rf);
        board.AttemptSynthesis();
        Check(board.LastSynthesisMessage == "Doğru bildin", board.LastSynthesisMessage);
        var drawing = board.EnsureDrawing();
        drawing.BeginStroke(new Vector2(-160, 90), false);
        drawing.ContinueStroke(new Vector2(-100, 130));
        drawing.ContinueStroke(new Vector2(-40, 90));
        drawing.EndStroke();
        Check(drawing.Strokes.Count == 1, "Board free chalk stroke missing.");
        workspace.GoToPage(19);
        var notebookDrawing = workspace.DragCanvas.GetComponentInChildren<NotebookFreehandDrawer>();
        var pointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, notebookDrawing.transform.TransformPoint(new Vector3(-130, -40, 0))) };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject == notebookDrawing.gameObject, "Blank notebook page does not receive drawing input.");
        ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        notebookDrawing.ContinueStroke(new Vector2(10, 60));
        notebookDrawing.ContinueStroke(new Vector2(140, -40));
        notebookDrawing.EndStroke();
        Check(NotebookUI.Instance.workspacePages[19].strokes.Count == 1, "Live page 20 drawing missing.");
        Debug.Log("STARTER LIVE LOOP PASS: notebook target/parts dropped, sockets immediately usable, marked single+double connected, success displayed without consumption.");
    }

    private static void Solve(DraggableFormula u, DraggableFormula be, DraggableFormula rf)
    {
        Check(u != null && be != null && rf != null, "Required board pieces missing.");
        var bn = be.GetOwnNodes()[0];
        var rn = rf.GetOwnNodes()[0];
        Check(bn.nodeState == FormulaNodeState.Open && rn.nodeState == FormulaNodeState.Open, "Sockets should be usable immediately.");
        Check(rn.IsDoubleBondSocket && rn.SocketHint == "= 0/2", "REF double socket is not identified before connection.");
        bn.ConnectTo(u.GetOwnNodes()[0]);
        rn.ConnectTo(u.GetOwnNodes()[1]);
        Check(rn.bondType == BondType.Single && rn.isOccupied && rn.SocketHint == "= 1/2", "REF first bond/progress could not be drawn.");
        rn.ConnectTo(u.GetOwnNodes()[1]);
        Check(rn.bondType == BondType.Double && rn.SocketHint == "= 2/2" && u.UsedCapacity == 3 && be.UsedCapacity == 1, "Double bond upgrade/capacity failed.");
    }

    private static DraggableFormula Spawn(ChalkboardManager board, FormulaItemSO data, Vector3 local)
    {
        var formula = (DraggableFormula)typeof(ChalkboardManager).GetMethod("CreateFormula", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(board, new object[] { data, board.boardTransform.TransformPoint(local), false });
        foreach (var node in formula.GetOwnNodes())
            typeof(FormulaNode).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(node, null);
        return formula;
    }

    private static void SetProperty(object target, string name, object value) => typeof(ChalkboardManager)
        .GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).GetSetMethod(true).Invoke(target, new[] { value });

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
