using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LinkedClueValidation
{
    [MenuItem("Senzora/Doğrulama/Bulmacaya Bağlı İpuçları")]
    public static void Run()
    {
        var first = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(StarterPuzzleValidation.QuestPath);
        var second = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(SecondPracticeValidation.QuestPath);
        var pages = new List<BookWorkspacePage>();
        var build = typeof(SynthesisBlockoutScene).GetMethod("AddPracticePages", BindingFlags.Static | BindingFlags.NonPublic);
        build.Invoke(null, new object[] { pages, first, 0 }); build.Invoke(null, new object[] { pages, second, 4 });
        foreach (var page in pages)
            foreach (var note in page.blocks)
                if (note.blockType == NotebookBlockType.RuleClue)
                    Check(!string.IsNullOrEmpty(note.questID) && note.ruleIndex >= 0,
                        "Kitap ipuçlarında bulmaca kimliği ve bilgi yuvası eksik.");
        var previous = ChalkboardManager.Instance;
        var host = new GameObject("Linked clue checks");
        QuestBlueprintSO duplicate = null;
        try
        {
            var board = host.AddComponent<ChalkboardManager>();
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, board);
            typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, true);
            board.boardTransform = new GameObject("Board").transform; board.boardTransform.SetParent(host.transform);
            board.practiceQuest = first; board.practicePuzzles = new List<QuestBlueprintSO> { first, second };
            var helper = typeof(ChalkboardManager).Assembly.GetType("PuzzleInformation");
            Check(helper != null, "Ortak ipucu eşleştirmesi yok.");
            var resolve = helper.GetMethod("TryResolve");
            var legacy = new NotebookBlockData { blockType = NotebookBlockType.RuleClue, title = "İPUCU 1", description = second.clueNotes[0] };
            Check((bool)resolve.Invoke(null, new object[] { legacy, board }) && legacy.questID == second.questID && legacy.ruleIndex == 0,
                "Eski bilgi notu doğru bulmacaya taşınmadı.");
            duplicate = ScriptableObject.CreateInstance<QuestBlueprintSO>(); duplicate.questID = "ambiguous-test";
            duplicate.clueNotes.Add(second.clueNotes[0]); board.practicePuzzles.Add(duplicate);
            var ambiguous = new NotebookBlockData { blockType = NotebookBlockType.RuleClue, description = second.clueNotes[0] };
            Check(!(bool)resolve.Invoke(null, new object[] { ambiguous, board }) && string.IsNullOrEmpty(ambiguous.questID),
                "Belirsiz eski bilgi rastgele bir bulmacaya atandı.");
            board.practicePuzzles.Remove(duplicate);
            Check(board.TryPinQuest(first.CreateQuest()) && !board.TryPinInformation(legacy) && board.pinnedInformation.Count == 0,
                "Başka bulmacanın bilgisi seçili hedefe takıldı.");
            var one = new NotebookBlockData { blockType = NotebookBlockType.RuleClue, questID = first.questID, ruleIndex = 0,
                title = "İPUCU 1", description = first.clueNotes[0] };
            Check(board.TryPinInformation(one) && board.TryPinInformation(one) && board.pinnedInformation.Count == 1,
                "Tek bilgi iki yuvayı doldurdu veya aynı yuvaya iki kez takıldı.");
            var docs = board.EnsureDocuments(); docs.Refresh();
            var surface = (RectTransform)typeof(ChalkboardDocuments).GetField("surface", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(docs);
            Check(Find(surface, "Eksik İpucu 2") != null && Find(surface, "Sabit İpucu 1") != null,
                "Bulunan ve eksik ipucu yuvaları görünmüyor.");
            board.TryPinQuest(second.CreateQuest());
            Check(Find(surface, "Eksik İpucu 1") != null && Find(surface, "Sabit İpucu 1") == null && board.pinnedInformation.Count == 1,
                "Hedef değişince önceki bulmacanın notu yeni hedefin altında kaldı veya silindi.");
            Check(board.TryPinInformation(legacy), "Doğru bulmacaya ait bilgi kabul edilmedi.");
            var last = new NotebookBlockData { blockType = NotebookBlockType.RuleClue, questID = second.questID, ruleIndex = 1,
                title = "İPUCU 2", description = second.clueNotes[1] };
            Check(board.TryPinInformation(last) && Find(surface, "Eksik İpucu 2") == null && Find(surface, "Sabit İpucu 2") != null,
                "Son bilgi bulununca eksik yuva dolmadı.");
            board.RemoveInformation(board.pinnedInformation.Find(n => n.questID == second.questID && n.ruleIndex == 1));
            var save = new GameData(); board.SaveData(ref save); board.ResetBoard(); board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(save)));
            Check(board.pinnedInformation.Count == 2 && board.PinnedQuest.questID == second.questID
                && Find(surface, "Eksik İpucu 2") != null, "İlişkiler veya eksik bilgi kayıtta kayboldu.");
            board.TryPinQuest(first.CreateQuest());
            Check(Find(surface, "Sabit İpucu 1") != null && Find(surface, "Eksik İpucu 2") != null,
                "İlk bulmacaya dönünce bilgi grubu geri gelmedi.");
            ValidateNotebook(host, board, first, second, one, legacy);
            Debug.Log("LINKED CLUE PASS: authored ownership, safe legacy matching, wrong-group rejection, unique slots, missing knowledge, switching and save/load.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate);
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, previous);
        }
    }

    private static void ValidateNotebook(GameObject host, ChalkboardManager board, QuestBlueprintSO first, QuestBlueprintSO second,
        NotebookBlockData one, NotebookBlockData two)
    {
        var notebook = host.AddComponent<NotebookUI>();
        notebook.collectedQuests.Add(new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = first.questID });
        notebook.collectedQuests.Add(new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = second.questID });
        notebook.EnsureWorkspacePage().blocks.Add(one.Clone()); notebook.EnsureWorkspacePage().blocks.Add(two.Clone());
        var workspace = host.GetComponent<NotebookWorkspaceController>() ?? host.AddComponent<NotebookWorkspaceController>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(NotebookWorkspaceController).GetField("notebook", flags).SetValue(workspace, notebook);
        typeof(NotebookWorkspaceController).GetMethod("BuildWorkspace", flags).Invoke(workspace, null);
        var root = (GameObject)typeof(NotebookWorkspaceController).GetField("root", flags).GetValue(workspace);
        try
        {
            root.SetActive(true); board.TryPinQuest(second.CreateQuest()); workspace.ShowInformation();
            var content = (RectTransform)typeof(NotebookWorkspaceController).GetField("informationContent", flags).GetValue(workspace);
            int groups = 0, missing = 0;
            foreach (var child in content.GetComponentsInChildren<Transform>())
            { if (child.name == "Bulmaca Bilgi Grubu") groups++; if (child.name == "Eksik Bilgi 2") missing++; }
            Check(groups == 2 && missing == 2 && content.anchoredPosition.y > 0, "Defter grupları, boş bilgi yuvaları veya seçili hedefe odaklanma eksik.");
            string text = string.Join(" ", Array.ConvertAll(content.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text));
            Check(!text.Contains(first.clueNotes[1]) && !text.Contains(second.clueNotes[1]), "Eksik bilgi yuvası bulunmamış içeriği açıkladı.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>()) if (child.name == name) return child;
        return null;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
