using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ContentWorkbookPlayChecks
{
    const string Pending = "Senzora.ContentWorkbook.PlayChecks";
    const string Restore = "Senzora.ContentWorkbook.RestoreScene";
    [Serializable] class SceneState { public SceneInfo[] scenes; }
    [Serializable] class SceneInfo { public string path; public bool loaded, active; }
    static ContentWorkbookPlayChecks() { EditorApplication.playModeStateChanged += StateChanged; }
    [MenuItem("Senzora/Doğrulama/İçerik Tablosu Gerçek Oyun")]
    public static void Start()
    {
        if (EditorApplication.isPlaying) { GlobalSubtitleManager.Instance.StartCoroutine(Watch()); return; }
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty) throw new InvalidOperationException("Önce sahnelerinizi kaydedin.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var saved = new SceneInfo[setup.Length];
        for (int i = 0; i < setup.Length; i++) saved[i] = new SceneInfo { path = setup[i].path, loaded = setup[i].isLoaded, active = setup[i].isActive };
        SessionState.SetString(Restore, JsonUtility.ToJson(new SceneState { scenes = saved }));
        EditorSceneManager.OpenScene(LaboratorySynthesisSetup.ScenePath);
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.delayCall += () => GlobalSubtitleManager.Instance.StartCoroutine(Watch());
        }
        if (state == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(SessionState.GetString(Restore, "")))
        {
            var saved = JsonUtility.FromJson<SceneState>(SessionState.GetString(Restore, "")); SessionState.EraseString(Restore);
            var setup = new SceneSetup[saved.scenes.Length];
            for (int i = 0; i < setup.Length; i++) setup[i] = new SceneSetup { path = saved.scenes[i].path, isLoaded = saved.scenes[i].loaded, isActive = saved.scenes[i].active };
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
    static IEnumerator Watch()
    {
        SaveManager.Instance.sessionOnly = true;
        var stack = new Stack<IEnumerator>(); stack.Push(Checks()); string failure = null;
        while (stack.Count > 0)
        {
            var iterator = stack.Peek(); bool next = false;
            try { next = iterator.MoveNext(); }
            catch (Exception error) { failure = error.ToString(); }
            if (failure != null) break;
            if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (iterator.Current is IEnumerator child) stack.Push(child); else yield return iterator.Current;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        File.WriteAllText("Temp/content-workbook-play.result", failure == null ? "PASS " + DateTime.Now.ToString("s") : "FAIL " + failure);
        if (failure == null) Debug.Log("CONTENT WORKBOOK PLAY PASS: real rebound book exit, subtitle order/durations/language, input edges and tutorial regressions.");
        else Debug.LogError(failure);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    static IEnumerator Checks()
    {
        // Game input events are routed to the game state only in Play mode.
        ContentWorkbookChecks.CheckInputs(true);
        yield return TutorialRegressionChecks.Run();
        yield return ReboundBookExit();
        yield return AudioPriority();
        yield return SubtitleSequence();
    }
    static void Require(bool value, string message) { if (!value) throw new Exception("CONTENT PLAY CHECK: " + message); }
    static IEnumerator ReboundBookExit()
    {
        var tutorial = TutorialManager.Instance;
        var lesson = tutorial.tutorialEventsParent.GetComponent<ChalkboardTutorial>();
        tutorial.tutorialEventsParent.SetActive(false);
        var book = lesson.lessonBook; var game = GameManager.Instance;
        if (game.activeInteraction != null) game.activeInteraction.ForceExit();
        yield return new WaitForSeconds(2);
        const string key = "Senzora.InputOverrides.v1";
        bool had = PlayerPrefs.HasKey(key); string saved = PlayerPrefs.GetString(key);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var settings = InputSystem.settings; var isolated = UnityEngine.Object.Instantiate(settings);
        isolated.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = isolated;
        try
        {
            GameInput.Rebind("Cancel", "<Keyboard>/g"); book.Interact();
            yield return new WaitForSeconds(2);
            Require(game.activeInteraction == book, "Real book did not open for rebound input check.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null; yield return null;
            Require(game.activeInteraction == book, "Old F key still closes the book.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(2);
            Require(game.activeInteraction == null, "New G key did not close the real book.");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.settings = settings; UnityEngine.Object.Destroy(isolated);
            if (had) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
            GameInput.ReloadBindings();
        }
    }
    static IEnumerator SubtitleSequence()
    {
        var manager = GlobalSubtitleManager.Instance; var original = manager.database;
        string language = GameLocalization.CurrentCode;
        var fixture = ScriptableObject.CreateInstance<SubtitleData>();
        fixture.entries.Add(new SubtitleEntry { id = "content_sequence_regression", segments = new List<SubtitleSegment> {
            new SubtitleSegment { duration = .2f, textTR = "İlk cümle", textEN = "First line" },
            new SubtitleSegment { duration = .4f, textTR = "İkinci cümle", textEN = "Second line" }
        } });
        try
        {
            manager.database = fixture; GameLocalization.SetLanguage("tr"); manager.Show("content_sequence_regression", true);
            yield return null; Require(manager.subtitleText.text == "İlk cümle", "First segment text/order is wrong.");
            yield return new WaitForSeconds(.25f); Require(manager.subtitleText.text == "İkinci cümle", "Duration did not advance to second segment.");
            GameLocalization.SetLanguage("en"); Require(manager.subtitleText.text == "Second line", "Active segment did not follow language change.");
            yield return new WaitForSeconds(.45f); Require(!manager.IsPlaying, "Sequence did not finish after authored durations.");
        }
        finally { manager.HideSubtitle(); manager.database = original; GameLocalization.SetLanguage(language); UnityEngine.Object.Destroy(fixture); }
    }
    static IEnumerator AudioPriority()
    {
        var subtitles = GlobalSubtitleManager.Instance; var original = subtitles.database;
        var fixture = ScriptableObject.CreateInstance<SubtitleData>();
        var defaultClip = AudioClip.Create("Workbook clip fixture", 4410, 1, 44100, false);
        var overrideClip = AudioClip.Create("Event override fixture", 44100, 1, 44100, false);
        var reminderClip = AudioClip.Create("Reminder clip fixture", 44100, 1, 44100, false);
        fixture.entries.Add(new SubtitleEntry { id = "workbook_audio_fixture", voiceClip = defaultClip, segments = new List<SubtitleSegment> { new SubtitleSegment { duration = .1f, textEN = "Audio fixture" } } });
        fixture.entries.Add(new SubtitleEntry { id = "workbook_reminder_fixture", voiceClip = reminderClip, segments = new List<SubtitleSegment> { new SubtitleSegment { duration = .1f, textEN = "Reminder fixture" } } });
        var oneShot = new GameObject("Workbook ordinary audio"); var follow = new GameObject("Workbook conditional audio");
        try
        {
            subtitles.database = fixture;
            var action = oneShot.AddComponent<DialogAction>(); action.subtitleID = "workbook_audio_fixture";
            action.Execute(); Require(action.audioSource != null && action.audioSource.clip == defaultClip, "Ordinary dialogue ignored the workbook audio clip.");
            action.dialogClip = overrideClip; action.Execute(); Require(action.audioSource.clip == overrideClip, "Ordinary event override lost priority.");
            action.audioSource.Stop(); subtitles.HideSubtitle();
            follow.AddComponent<EventLogicController>(); var signal = follow.AddComponent<SignalCondition>();
            var repeating = follow.AddComponent<DialogAction>(); repeating.followConditions = true; repeating.subtitleID = "workbook_audio_fixture";
            repeating.dialogClip = overrideClip; repeating.reminderSubtitleIDs = new[] { "workbook_reminder_fixture" }; repeating.reminderDelay = .1f;
            yield return null; signal.ReceiveSignal();
            yield return new WaitForSeconds(.35f);
            Require(repeating.audioSource != null && repeating.audioSource.clip == overrideClip, "Conditional dialogue ignored the explicit initial override.");
            yield return new WaitForSeconds(1.2f);
            Require(repeating.audioSource.clip == reminderClip, "Reminder used the initial override instead of its own database clip.");
        }
        finally
        {
            UnityEngine.Object.Destroy(oneShot); UnityEngine.Object.Destroy(follow); subtitles.HideSubtitle(); subtitles.database = original;
            UnityEngine.Object.Destroy(fixture); UnityEngine.Object.Destroy(defaultClip); UnityEngine.Object.Destroy(overrideClip); UnityEngine.Object.Destroy(reminderClip);
        }
    }
}
