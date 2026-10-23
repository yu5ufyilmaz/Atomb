using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
        Debug.Log("STARTER PASS: 5 candidates, 3 note types, blocked ports/eraser, single+double bonds, wrong candidates, pin gate, repeated non-consuming success.");
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
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
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
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, page.TransformPoint(local));
            Check(workspace.TryDropBlock(source, screen), "Source note could not be copied.");
            copied++;
        }
        Check(copied >= 4, "Open the first book spread with target and three parts.");
        Debug.Log("STARTER LIVE COLLECT PASS: source notes copied through the real notebook drop path.");
    }

    [MenuItem("Senzora/Doğrulama/Başlatıcı-X Tahta Döngüsünü Dene %&m")]
    public static void SolveLiveNotes()
    {
        Check(Application.isPlaying && ChalkboardManager.Instance.isMachineActive, "Open the board first.");
        var board = ChalkboardManager.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        var blocks = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None);
        foreach (var block in blocks)
        {
            if (block.IsSource || !block.gameObject.activeInHierarchy || block.Data.blockType == NotebookBlockType.RuleClue) continue;
            Vector3 local = block.Data.itemID == "starter-BE9" ? new Vector3(1, 0.05f, 0.02f)
                : block.Data.itemID == "starter-REF" ? new Vector3(0.3f, -0.55f, 0.02f) : new Vector3(0.3f, 0.05f, 0.02f);
            Check(workspace.TryDropBlock(block, Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(local))), "Notebook-to-board drop failed: " + block.Data.title);
        }
        var parts = board.boardTransform.GetComponentsInChildren<DraggableFormula>();
        var u = Array.Find(parts, p => p.formulaData.itemID == "starter-U235");
        var be = Array.Find(parts, p => p.formulaData.itemID == "starter-BE9");
        var rf = Array.Find(parts, p => p.formulaData.itemID == "starter-REF");
        Solve(u, be, rf);
        board.AttemptSynthesis();
        Check(board.LastSynthesisMessage == "Doğru bildin", board.LastSynthesisMessage);
        Debug.Log("STARTER LIVE LOOP PASS: notebook target/parts dropped, closed ends erased, single+double connected, success displayed without consumption.");
    }

    private static void Solve(DraggableFormula u, DraggableFormula be, DraggableFormula rf)
    {
        Check(u != null && be != null && rf != null, "Required board pieces missing.");
        var bn = be.GetOwnNodes()[0];
        var rn = rf.GetOwnNodes()[0];
        Check(bn.nodeState == FormulaNodeState.Blocked && rn.nodeState == FormulaNodeState.Blocked, "Authored closed ends did not load.");
        bn.ConnectTo(u.GetOwnNodes()[0]);
        Check(!bn.isOccupied, "Closed end connected before erasing.");
        bn.Erase(); rn.Erase();
        bn.ConnectTo(u.GetOwnNodes()[0]);
        rn.ConnectTo(u.GetOwnNodes()[1]);
        Check(rn.bondType == BondType.Single && rn.isOccupied, "REF single bond could not be drawn.");
        rn.ConnectTo(u.GetOwnNodes()[1]);
        Check(rn.bondType == BondType.Double && u.UsedCapacity == 3 && be.UsedCapacity == 1, "Double bond upgrade/capacity failed.");
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
