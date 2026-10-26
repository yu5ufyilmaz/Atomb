using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Explicit editor playtest commands only. Nothing auto-solves during normal Play.
public static class SecondTracePlayChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/İkinci İz/Canlı/Defteri Aç Kapat")]
    public static void ToggleNotebook() => NotebookWorkspaceController.Instance.ToggleNotebook();

    [MenuItem("Senzora/İkinci İz/Canlı/0 Başlangıç Düzenini Ölç")]
    public static void Baseline() => BuildAndMeasure(0);
    [MenuItem("Senzora/İkinci İz/Canlı/1 Ana Hat Kaybını Ölç")]
    public static void MainLoss() => BuildAndMeasure(1);
    [MenuItem("Senzora/İkinci İz/Canlı/2 Dönüş Kolunu Ölç")]
    public static void ReturnPath() => BuildAndMeasure(2);

    private static void BuildAndMeasure(int mode)
    {
        var trial = ResearchPuzzleTrial.Active;
        Check(Application.isPlaying && SecondTraceExperiment.Active != null && trial.Board.isMachineActive, "İkinci İz tahtasını aç.");
        var workspace = NotebookWorkspaceController.Instance;
        var notebook = NotebookUI.Instance;
        var board = trial.Board;
        board.ResetBoard();
        workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases();
        var target = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
            b => !b.IsSource && b.Data.blockType == NotebookBlockType.Quest && b.Data.questID == SecondTraceExperiment.QuestID);
        Check(target != null, "Hedefi önce gerçek kitaptan topla.");
        var targetArea = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(board.EnsureDocuments());
        Check(workspace.TryDropBlock(target, Camera.main.WorldToScreenPoint(targetArea.position)), "Toplanmış hedef panoya bırakılamadı.");
        var graph = SecondTraceSetup.Graph(mode);
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var window = (RectTransform)page.parent; var bounds = (RectTransform)window.parent;
        var previous = window.anchoredPosition;
        window.anchoredPosition = new Vector2(-bounds.rect.width + 290, previous.y);
        var positions = new[] { new Vector2(.2f, .20f), new Vector2(-.45f, .20f), new Vector2(-1.28f, .20f),
            new Vector2(-1f, -.65f), new Vector2(-.32f, -.65f) };
        var parts = new List<DraggableFormula>();
        try
        {
            for (int i = 0; i < graph.parts.Count; i++)
            {
                string id = graph.parts[i].itemID;
                int index = notebook.workspacePages.FindIndex(p => p.blocks.Exists(b => b.blockType == NotebookBlockType.PartClue && b.itemID == id));
                Check(index >= 0, "Parça Atlası'ndan çizimi önce topla: " + id);
                workspace.GoToPage(index); Canvas.ForceUpdateCanvases();
                var note = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
                    b => !b.IsSource && b.isActiveAndEnabled && b.Data.itemID == id);
                Vector3 point = board.boardTransform.TransformPoint(new Vector3(positions[i].x, positions[i].y, .02f));
                Check(note != null && workspace.TryDropBlock(note, Camera.main.WorldToScreenPoint(point)), "Gerçek defter çizimi tahtaya bırakılamadı.");
                parts.Add(Array.Find(board.boardTransform.GetComponentsInChildren<DraggableFormula>(), p => p.formulaData.itemID == id));
            }
            foreach (var bond in graph.bonds)
            {
                FormulaNode a = null, b = null; float distance = float.MaxValue;
                foreach (var first in parts[bond.fromIndex].GetOwnNodes())
                    foreach (var second in parts[bond.toIndex].GetOwnNodes())
                        if (!first.isOccupied && !second.isOccupied && Vector3.Distance(first.transform.position, second.transform.position) < distance)
                        { a = first; b = second; distance = Vector3.Distance(first.transform.position, second.transform.position); }
                Check(a != null && b != null, "Gerçek prefabda boş bağlantı ucu yok.");
                a.ConnectTo(b); Check(a.connectedNode == b, "Gerçek uçlar bağlanamadı.");
            }
            board.AttemptSynthesis();
            var reading = SecondTraceExperiment.Active.LastReading;
            Check(reading.Measurable && (mode == 2 ? reading.MeetsTarget : !reading.MeetsTarget), "Gerçek tahta beklenen izi üretmedi: " + board.LastSynthesisMessage);
            Check(!trial.Completed.Contains(SecondTraceExperiment.QuestID), "Tahta Turing sınamasından önce görevi tamamladı.");
            Debug.Log("SECOND TRACE PHYSICAL PASS: mode=" + mode + "; actual book notes -> notebook -> board -> connected prefab ports -> measurement=" + reading.Code);
        }
        finally { window.anchoredPosition = previous; Canvas.ForceUpdateCanvases(); }
    }

    [MenuItem("Senzora/İkinci İz/Canlı/3 Çıktıyı Deftere Al")]
    public static void CollectOutput()
    {
        var workspace = NotebookWorkspaceController.Instance;
        var output = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
            b => b.IsSource && b.isActiveAndEnabled && ((b.Data.itemID ?? "").StartsWith("trace-result/") || (b.Data.itemID ?? "").StartsWith("signal-result/")));
        Check(output != null, "Önce tahtada ölçüm al.");
        workspace.GoToPage(NotebookUI.Instance.workspacePages.Count); Canvas.ForceUpdateCanvases();
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        Check(workspace.TryDropBlock(output, RectTransformUtility.WorldToScreenPoint(null, page.position)), "Çıktı gerçek deftere alınamadı.");
        Debug.Log("SECOND TRACE OUTPUT COLLECT PASS: " + output.Data.title);
    }

    [MenuItem("Senzora/İkinci İz/Canlı/4 Turing'i Aç")]
    public static void OpenTerminal()
    {
        Check(Application.isPlaying && GameManager.Instance.activeInteraction == null, "Önce mevcut etkileşimden çık.");
        Application.runInBackground = true;
        var player = UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
        typeof(SynthesisBlockoutPlayer).GetMethod("Begin", Private).Invoke(player, new object[] { player.trialTerminal, player.terminalView });
    }

    [MenuItem("Senzora/İkinci İz/Canlı/5 Son Ölçümü Gerçek Çarklarda Sına")]
    public static void SubmitLastOutput()
    {
        var experiment = SecondTraceExperiment.Active;
        Check(Application.isPlaying && GameManager.Instance.activeInteraction == experiment.terminal, "Turing'e geç.");
        var machine = experiment.terminal;
        string code = experiment.LastReading.Code;
        string[] fields = code.Split('_');
        var letters = (string)typeof(InteractableTuringMachine).GetField("wordChars", Private).GetValue(machine);
        var wordIndices = (int[])typeof(InteractableTuringMachine).GetField("wordWheelIndices", Private).GetValue(machine);
        var numbers = (string)typeof(InteractableTuringMachine).GetField("numberChars", Private).GetValue(machine);
        var numberIndices = (int[])typeof(InteractableTuringMachine).GetField("numberWheelIndices", Private).GetValue(machine);
        var rotate = typeof(InteractableTuringMachine).GetMethod("RotateActiveWheel", Private);
        typeof(InteractableTuringMachine).GetField("currentGroup", Private).SetValue(machine, 0);
        for (int i = 0; i < wordIndices.Length; i++)
        {
            typeof(InteractableTuringMachine).GetField("currentWordIndex", Private).SetValue(machine, i);
            int steps = (letters.IndexOf(fields[0][i]) - wordIndices[i] + letters.Length) % letters.Length;
            rotate.Invoke(machine, new object[] { machine.useForwardLetterOrder ? steps : -steps });
        }
        typeof(InteractableTuringMachine).GetField("currentGroup", Private).SetValue(machine, 1);
        var symbols = (string[])typeof(InteractableTuringMachine).GetField("symbolChars", Private).GetValue(machine);
        var symbolIndex = typeof(InteractableTuringMachine).GetMethod("GetCorrectSymbolIndex", Private);
        for (int i = 0; i < symbols.Length && symbols[(int)symbolIndex.Invoke(machine, null)] != fields[1]; i++)
            rotate.Invoke(machine, new object[] { 1 });
        typeof(InteractableTuringMachine).GetField("currentGroup", Private).SetValue(machine, 2);
        string digits = code.Substring(code.LastIndexOf('_') + 1);
        for (int i = 0; i < 3; i++)
        {
            typeof(InteractableTuringMachine).GetField("currentNumberIndex", Private).SetValue(machine, i);
            int wanted = numbers.IndexOf(digits[i]);
            int steps = (numberIndices[i] - wanted + numbers.Length) % numbers.Length;
            rotate.Invoke(machine, new object[] { steps });
        }
        typeof(InteractableTuringMachine).GetMethod("CheckPassword", Private).Invoke(machine, null);
        bool completed = ResearchPuzzleTrial.Active.Completed.Contains(experiment.CurrentQuestID);
        Check(completed == experiment.LastReading.MeetsTarget, "Gerçek makine yanlış deneyi kabul etti veya doğru deneyi reddetti.");
        Debug.Log("SECOND TRACE TERMINAL PASS: actual wheel input=" + code + "; completed=" + completed);
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
