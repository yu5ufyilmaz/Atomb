using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SignalTrialPlayChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Senzora/Çift Kayıt/Canlı/3 Çıktıyı Deftere Al")]
    public static void CollectOutput()
    {
        var workspace = NotebookWorkspaceController.Instance;
        var output = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
            b => b.IsSource && b.isActiveAndEnabled && SignalExperiment.IsMeasurementNote(b.Data));
        Check(output != null, "Önce tahtada ölçüm al.");
        workspace.GoToPage(NotebookUI.Instance.workspacePages.Count); Canvas.ForceUpdateCanvases();
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        Check(workspace.TryDropBlock(output, RectTransformUtility.WorldToScreenPoint(null, page.position)), "Çıktı gerçek deftere alınamadı.");
        Debug.Log("SIGNAL TRIAL OUTPUT COLLECT PASS: " + output.Data.title);
    }

    [MenuItem("Senzora/Çift Kayıt/Canlı/4 Turing'i Aç")]
    public static void OpenTerminal()
    {
        Check(Application.isPlaying && GameManager.Instance.activeInteraction == null, "Önce mevcut etkileşimden çık.");
        Application.runInBackground = true;
        var player = UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
        if (player != null)
            typeof(SynthesisBlockoutPlayer).GetMethod("Begin", Private).Invoke(player, new object[] { player.trialTerminal, player.terminalView });
        else SignalExperiment.Active.terminal.Interact();
    }

    [MenuItem("Senzora/Çift Kayıt/Canlı/5 Son Ölçümü Gerçek Çarklarda Sına")]
    public static void SubmitLastOutput()
    {
        var experiment = SignalExperiment.Active;
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
        Debug.Log("SIGNAL TRIAL TERMINAL PASS: actual wheel input=" + code + "; completed=" + completed);
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
