using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class TutorialTemporaryRunner
{
    static TutorialTemporaryRunner() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Temp/tutorial-regression.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        if (!EditorApplication.isPlaying)
        {
            if (File.ReadAllText(request).Trim() == "green")
            {
                TutorialSceneSetup.ConfigureDialogueEvents();
                TutorialVoiceChecks.Core();
                File.WriteAllText(request, "run");
            }
            EditorApplication.isPlaying = true;
            return;
        }
        if (ResearchPuzzleTrial.Active == null || TutorialManager.Instance == null) return;
        Application.runInBackground = true;
        File.Delete(request);
        ResearchPuzzleTrial.Active.StartCoroutine(Run());
    }
    private static IEnumerator Run()
    {
        yield return null;
        var stack = new Stack<IEnumerator>();
        stack.Push(TutorialRegressionChecks.Run());
        string result = "PASS";
        while (stack.Count > 0)
        {
            object value = null;
            bool next = false;
            try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
            catch (Exception error) { result = error.ToString(); break; }
            if (!next) { stack.Pop(); continue; }
            if (value is IEnumerator nested) stack.Push(nested);
            else yield return value;
        }
        File.WriteAllText("Temp/tutorial-regression.result", result);
        EditorApplication.isPlaying = false;
    }
}
