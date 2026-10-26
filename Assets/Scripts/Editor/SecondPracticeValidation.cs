using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SecondPracticeValidation
{
    public const string QuestPath = "Assets/Scripts/Scriptables/Formulas/Practice2/Quest_FlowBalance.asset";

    [MenuItem("Senzora/Doğrulama/İkinci Alıştırma")]
    public static void Run()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(QuestPath);
        Check(quest != null, "İkinci alıştırmanın içerik asseti henüz yok.");
        Check(quest.clueParts.Count == 5 && quest.clueNotes.Count == 2 && quest.nodes.Count == 3
            && quest.nodes.FindAll(n => n.isUnknown).Count == 2 && quest.bonds.Count == 2 && quest.output == null,
            "İkinci alıştırma aynı boyutta ve zorlukta değil.");
        Check(quest.bonds.FindAll(b => b.bondType == BondType.Single).Count == 1
            && quest.bonds.FindAll(b => b.bondType == BondType.Double).Count == 1, "Tek/çift bağ dengesi yanlış.");
        foreach (var part in quest.clueParts) Check(part != null && part.chalkPrefab != null && part.chalkPrefab.GetComponent<DraggableFormula>() != null, "Aday referansı eksik.");
        int solutions = 0;
        for (int a = 1; a < quest.clueParts.Count; a++)
            for (int b = 1; b < quest.clueParts.Count; b++)
                if (a != b && SynthesisGraphValidator.Validate(CandidateGraph(quest, a, b), quest.CreateQuest(), out _)) solutions++;
        Check(solutions == 1, "İpucu/bağ kuralları tek bir doğru kombinasyon vermiyor.");

        var previous = ChalkboardManager.Instance;
        var host = new GameObject("Second practice checks");
        try
        {
            var board = host.AddComponent<ChalkboardManager>();
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, board);
            typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, true);
            var original = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(StarterPuzzleValidation.QuestPath);
            board.practiceQuest = original;
            board.practicePuzzles = new List<QuestBlueprintSO> { original, quest };
            board.formulaCatalog.AddRange(original.clueParts); board.formulaCatalog.AddRange(quest.clueParts);
            board.boardTransform = new GameObject("Board").transform; board.boardTransform.SetParent(host.transform, false);
            var zone = new GameObject("Zone", typeof(BoxCollider), typeof(ReactionZone));
            zone.transform.SetParent(board.boardTransform, false); zone.GetComponent<BoxCollider>().size = new Vector3(3.2f, 2.1f, .5f);
            board.synthesisZone = zone.GetComponent<ReactionZone>();
            var generator = host.AddComponent<ProceduralSynthesisGenerator>();
            generator.RegisterCatalog(board.formulaCatalog);
            Check(generator.CanRealizeQuest(quest.CreateQuest()), "Yeni hedef gerçek prefab uçlarıyla çözülemiyor.");
            Check(board.TryPinQuest(quest.CreateQuest()) && board.practiceQuest == quest, "Yeni hedef sabitlenince alıştırma değişmedi.");
            var parts = new List<DraggableFormula>();
            foreach (var node in quest.nodes)
            {
                var data = quest.clueParts.Find(p => p.itemID == node.itemID);
                Vector3 local = new Vector3(node.position.x * .55f, node.position.y * .55f, .02f);
                var part = (DraggableFormula)typeof(ChalkboardManager).GetMethod("CreateFormula", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(board, new object[] { data, board.boardTransform.TransformPoint(local), false });
                foreach (var port in part.GetOwnNodes()) typeof(FormulaNode).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(port, null);
                parts.Add(part);
            }
            ConnectSolution(parts, quest);
            board.AttemptSynthesis();
            Check(board.LastSynthesisMessage == "Doğru bildin" && parts.Count == 3, "Yeni alıştırma gerçek düğümlerle doğrulanmadı.");
            var firstLeaf = parts[1].formulaData;
            var secondLeaf = parts[2].formulaData;
            parts[1].formulaData = original.clueParts[1];
            parts[2].formulaData = original.clueParts[2];
            board.AttemptSynthesis();
            Check(board.LastSynthesisMessage != "Doğru bildin"
                && !board.LastSynthesisMessage.Contains(firstLeaf.itemName) && !board.LastSynthesisMessage.Contains(firstLeaf.itemID)
                && !board.LastSynthesisMessage.Contains(secondLeaf.itemName) && !board.LastSynthesisMessage.Contains(secondLeaf.itemID),
                "Karışık alıştırma parçaları hata mesajında gizli çözüm kimliğini açığa çıkardı.");
            parts[1].formulaData = firstLeaf;
            parts[2].formulaData = secondLeaf;
            var save = new GameData(); board.SaveData(ref save); board.ResetBoard(); board.practiceQuest = original;
            board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(save)));
            Check(board.PinnedQuest != null && board.PinnedQuest.questID == quest.questID && board.practiceQuest == quest, "Yeni hedef kayıt yüklemesinde bulunamadı.");
            Check(board.TryPinQuest(original.CreateQuest()) && board.practiceQuest == original && NotebookWorkspaceController.FindQuest(quest.questID) != null,
                "İlk hedefe dönülemiyor veya ikinci hedef kayboluyor.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, previous); }

        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scn_SynthesisBlockout.unity");
        try
        {
            SynthesisBlockoutScene scene = null;
            foreach (var root in preview.GetRootGameObjects()) if (scene == null) scene = root.GetComponentInChildren<SynthesisBlockoutScene>(true);
            Check(scene != null && scene.additionalPracticePuzzles.Contains(quest), "İkinci hedef sahneye bağlanmadı.");
            Check(scene.starterPuzzle == AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(StarterPuzzleValidation.QuestPath),
                "İlk alıştırmanın yerini ikinci hedef aldı.");
            var pages = new List<BookWorkspacePage>();
            var build = typeof(SynthesisBlockoutScene).GetMethod("AddPracticePages", BindingFlags.Static | BindingFlags.NonPublic);
            build.Invoke(null, new object[] { pages, scene.starterPuzzle, 0 });
            build.Invoke(null, new object[] { pages, quest, 4 });
            var indices = new HashSet<int>();
            foreach (var page in pages) Check(indices.Add(page.pageIndex) && page.pageIndex < 8, "Alıştırma sayfaları çakışıyor veya kitap dışında kalıyor.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Debug.Log("SECOND PRACTICE PASS: unique solution, comparable shape, actual ports, target switching, save/load and separate spreads. Answer omitted.");
    }

    [MenuItem("Senzora/Doğrulama/İkinci Alıştırma Tahta Döngüsünü Dene")]
    public static void ValidateLiveNotes()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(QuestPath);
        var board = ChalkboardManager.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        Check(Application.isPlaying && board.isMachineActive, "Open the board first.");
        workspace.ShowCollectedTargets();
        Canvas.ForceUpdateCanvases();
        var target = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
            b => !b.IsSource && b.Data.questID == quest.questID);
        var pin = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(.3f, .7f, .02f)));
        Check(target != null && workspace.TryDropBlock(target, pin) && board.practiceQuest == quest, "Collected new target did not select second puzzle.");
        var parts = new List<DraggableFormula>();
        foreach (var node in quest.nodes)
        {
            int page = NotebookUI.Instance.workspacePages.FindIndex(p => p.blocks.Exists(b => b.itemID == node.itemID));
            Check(page >= 0, "Physical book candidate not collected.");
            workspace.GoToPage(page); Canvas.ForceUpdateCanvases();
            var note = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None), b => !b.IsSource && b.Data.itemID == node.itemID);
            var local = new Vector3(-.2f + node.position.x * .55f, node.position.y * .55f, .02f);
            Check(note != null && workspace.TryDropBlock(note, Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(local))), "New candidate notebook-to-board drop failed.");
            parts.Add(Array.Find(board.boardTransform.GetComponentsInChildren<DraggableFormula>(), p => p.formulaData.itemID == node.itemID));
        }
        ConnectSolution(parts, quest);
        var doubleBond = quest.bonds.Find(b => b.bondType == BondType.Double);
        var socket = Array.Find(parts[doubleBond.toIndex].GetOwnNodes(), n => n.isOccupied);
        socket.Erase(); board.AttemptSynthesis();
        Check(board.LastSynthesisMessage != "Doğru bildin", "Wrong single bond accepted in live second puzzle.");
        socket.ConnectTo(socket.connectedNode); board.AttemptSynthesis();
        Check(board.LastSynthesisMessage == "Doğru bildin" && board.boardTransform.GetComponentsInChildren<DraggableFormula>().Length == 3,
            "Live second puzzle failed or consumed parts.");
        Debug.Log("SECOND PRACTICE LIVE PASS: collected target and candidates, selected target, rejected wrong bond, accepted full graph.");
    }

    private static SynthesisGraphData CandidateGraph(QuestBlueprintSO quest, int a, int b)
    {
        var graph = new SynthesisGraphData { bonds = quest.bonds };
        foreach (int i in new[] { 0, a, b }) graph.parts.Add(new SynthesisPartData { itemID = quest.clueParts[i].itemID, capacity = quest.clueParts[i].connectionCapacity });
        return graph;
    }

    private static void ConnectSolution(List<DraggableFormula> parts, QuestBlueprintSO quest)
    {
        var next = new int[parts.Count];
        foreach (var bond in quest.bonds)
        {
            var a = parts[bond.fromIndex].GetOwnNodes()[next[bond.fromIndex]++];
            var b = parts[bond.toIndex].GetOwnNodes()[next[bond.toIndex]++];
            a.ConnectTo(b);
            if (bond.bondType == BondType.Double && a.bondType != BondType.Double) a.ConnectTo(b);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
