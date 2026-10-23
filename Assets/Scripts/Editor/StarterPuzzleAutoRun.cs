using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Temporary, explicitly armed engine integration run; removed after verification.
[InitializeOnLoad]
internal static class StarterPuzzleAutoRun
{
    private const string Marker = "Temp/StarterPuzzleAuto.run";
    private const string Phase = "StarterPuzzleAuto.phase";
    private static double after;
    static StarterPuzzleAutoRun()
    {
        if (File.Exists(Marker) || SessionState.GetString(Phase, "") != "") EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < after) return;
        try
        {
            string phase = SessionState.GetString(Phase, "");
            if (phase == "" && File.Exists(Marker))
            {
                string job = File.ReadAllText(Marker).Trim();
                File.Delete(Marker);
                if (job == "edit")
                {
                    StarterPuzzleValidation.Run();
                    StarterPuzzleValidation.SetupScene();
                    SenzoraWorkspaceValidation.RunAll();
                    File.WriteAllText("Temp/StarterPuzzleAuto.result", "EDIT PASS");
                    EditorApplication.update -= Tick;
                    return;
                }
                SessionState.SetString(Phase, "starting");
                EditorApplication.isPlaying = true;
                after = EditorApplication.timeSinceStartup + 5;
                return;
            }
            if (!EditorApplication.isPlaying) return;
            var player = UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
            if (player == null || GameManager.Instance == null) return;
            if (phase == "starting")
            {
                Begin(player, player.book, player.bookView);
                SessionState.SetString(Phase, "book");
                after = EditorApplication.timeSinceStartup + 3;
            }
            else if (phase == "book")
            {
                ScreenCapture.CaptureScreenshot("Temp/starter-book.png");
                StarterPuzzleValidation.CollectLiveNotes();
                SessionState.SetString(Phase, "collected");
                after = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == "collected")
            {
                ScreenCapture.CaptureScreenshot("Temp/starter-notebook.png");
                player.book.ForceExit();
                SessionState.SetString(Phase, "exitbook");
                after = EditorApplication.timeSinceStartup + 3;
            }
            else if (phase == "exitbook")
            {
                Begin(player, player.board, player.boardView);
                SessionState.SetString(Phase, "board");
                after = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == "board")
            {
                StarterPuzzleValidation.SolveLiveNotes();
                SessionState.SetString(Phase, "solved");
                after = EditorApplication.timeSinceStartup + 3;
            }
            else if (phase == "solved")
            {
                ScreenCapture.CaptureScreenshot("Temp/starter-solved.png");
                SessionState.SetString(Phase, "finish");
                after = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == "finish")
            {
                SessionState.SetString(Phase, "");
                File.WriteAllText("Temp/StarterPuzzleAuto.result", "PLAY LOOP PASS");
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/StarterPuzzleAuto.result", e.ToString());
            Debug.LogException(e);
            SessionState.SetString(Phase, "");
            EditorApplication.update -= Tick;
        }
    }
    private static void Begin(SynthesisBlockoutPlayer player, IInteractable target, Transform view) =>
        typeof(SynthesisBlockoutPlayer).GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, new object[] { target, view });
}
