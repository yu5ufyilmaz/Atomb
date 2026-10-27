using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class PuzzleWorkshopChecks
{
    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Renk ve Karışım")]
    public static void ColorFamily()
    {
        var assembly = typeof(PuzzleDefinition).Assembly;
        Check(assembly.GetType("PuzzleKind") != null, "RED: puzzle types are still limited to waveforms.");
        Check(assembly.GetType("ColorMixtureSimulation") != null, "RED: no working color-mixture simulator.");
        var puzzle = new PuzzleDefinition { kind = PuzzleKind.ColorMixture, rules = PuzzleRules.ForKind(PuzzleKind.ColorMixture) };
        var chain = new SynthesisGraphData();
        foreach (string id in new[] { ColorMixtureLibrary.Source, ColorMixtureLibrary.Red, ColorMixtureLibrary.Blue, ColorMixtureLibrary.Meter })
            chain.parts.Add(new SynthesisPartData { itemID = id });
        Edge(chain, 0, 1); Edge(chain, 1, 2); Edge(chain, 2, 3);
        var reading = PuzzleSimulation.Measure(chain, puzzle);
        Check(reading.Measurable && reading.mixture.Volume == 3 && Mathf.Abs(reading.mixture.Concentration - 2f / 3) < .0001f, "Color dose model is wrong.");
        puzzle.solution = chain; puzzle.mixtureTarget = reading.mixture.Copy();
        Check(PuzzleSimulation.Validate(puzzle) == null, "Color witness does not validate.");
        var branch = new SynthesisGraphData();
        foreach (string id in new[] { ColorMixtureLibrary.Source, ColorMixtureLibrary.Splitter, ColorMixtureLibrary.Red, ColorMixtureLibrary.Blue, ColorMixtureLibrary.Mixer, ColorMixtureLibrary.Meter })
            branch.parts.Add(new SynthesisPartData { itemID = id });
        Edge(branch, 0, 1); Edge(branch, 1, 2); Edge(branch, 1, 3); Edge(branch, 2, 4); Edge(branch, 3, 4); Edge(branch, 4, 5);
        Check(PuzzleSimulation.Measure(branch, puzzle).MeetsTarget, "Equivalent branched mixture rejected or volume duplicated.");
        var diluted = Clone(chain); diluted.parts.Add(new SynthesisPartData { itemID = ColorMixtureLibrary.Solvent });
        diluted.bonds[2].toIndex = 4; Edge(diluted, 4, 3);
        var weaker = PuzzleSimulation.Measure(diluted, puzzle);
        Check(weaker.Measurable && !weaker.MeetsTarget && weaker.mixture.Volume == 4 && weaker.mixture.Concentration == .5f, "Dilution failed to alter actual concentration/quantity.");
        diluted.parts[4].itemID = ColorMixtureLibrary.Concentrator;
        Check(Mathf.Abs(PuzzleSimulation.Measure(diluted, puzzle).mixture.Concentration - .8f) < .0001f, "Concentrator behavior wrong.");
        for (int seed = 0; seed < 120; seed++)
        {
            var settings = new PuzzleDefinition { kind = PuzzleKind.ColorMixture, rules = PuzzleRules.ForKind(PuzzleKind.ColorMixture) };
            Check(PuzzleGenerator.TryGenerate(settings, seed, out var generated, out string issue), "Color generation failed: " + issue);
            Check(PuzzleSimulation.Validate(generated) == null, "Generated color witness fails.");
            Check(PuzzleGenerator.TryGenerate(settings, seed, out var repeat, out _) && JsonUtility.ToJson(repeat) == JsonUtility.ToJson(generated), "Color generation not deterministic.");
            var notes = PuzzleBookDistribution.Notes(generated);
            Check(notes.Any(n => (n.itemID ?? "").StartsWith("mixture-target/")) && !notes.Any(n => SignalExperiment.IsMeasurementNote(n)), "Color target is treated as collected measurement.");
        }
        Debug.Log("PUZZLE WORKSHOP COLOR FAMILY PASS: dose conservation, equivalent branch, real dilution/concentration, 120 deterministic witnesses and native target notes.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Yeni Numune Denemesini Hazırla")]
    public static void PrepareNewSample()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().path == PuzzleWorkshopSetup.ScenePath, "Atölye sahnesi Edit Mode'da açık olmalı.");
        var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(PuzzleWorkshopSetup.Folder + "/TekHattaCiftTepki.asset");
        var trial = PuzzleWorkshopSetup.FindTrial(SceneManager.GetActiveScene());
        trial.previewPuzzle = asset;
        EditorUtility.SetDirty(trial); EditorSceneManager.MarkSceneDirty(trial.gameObject.scene); EditorSceneManager.SaveScene(trial.gameObject.scene);
        PuzzleWorkshopWindow.OpenWindow().Select(asset);
        Debug.Log("PUZZLE WORKSHOP PREVIEW READY: " + asset.definition.title);
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Tümü (Edit Mode)")]
    public static void All()
    {
        Model(); Generation(); SessionModel(); VisualModel(); EditorModel(); EdgeCases(); ColorFamily(); Assets(); DraftSafety();
        SignalTrialSetup.Validate();
        Debug.Log("PUZZLE WORKSHOP ALL EDIT CHECKS PASS.");
    }

    public static void EdgeCases()
    {
        for (int profile = 0; profile < 3; profile++) for (int seed = 0; seed < 40; seed++)
        {
            var settings = new PuzzleDefinition();
            if (profile == 1) { settings.rules.traceCount = 1; settings.rules.Find(SignalTrial.Splitter).allowed = false; settings.rules.Find("pulse-n46").allowed = false; }
            if (profile == 2) { settings.rules.Find(SignalTrial.Splitter).allowed = false; settings.rules.Find("pulse-n46").minimum = 1; settings.rules.minSamples = settings.rules.maxSamples = 2; }
            Check(PuzzleGenerator.TryGenerate(settings, seed, out var puzzle, out string error), "Profile " + profile + ": " + error);
            Check(PuzzleSimulation.Validate(puzzle) == null, "Profile witness invalid.");
        }
        var bad = new PuzzleDefinition(); bad.rules.Find(SignalTrial.Narrow).allowed = false; bad.rules.Find(SignalTrial.Narrow).minimum = 1;
        string before = JsonUtility.ToJson(bad);
        Check(!PuzzleGenerator.TryGenerate(bad, 1, out var rejected, out _) && rejected == null && JsonUtility.ToJson(bad) == before, "Contradictory generation overwrote input.");
        var impossible = new PuzzleDefinition(); impossible.rules.minParts = impossible.rules.maxParts = 3;
        impossible.rules.Find(SignalTrial.Splitter).allowed = false; impossible.rules.Find("pulse-n46").allowed = false;
        Check(!PuzzleGenerator.TryGenerate(impossible, 2, out _, out _), "Impossible double trace was generated.");
        var p = PuzzleDefinition.Legacy();
        p.target[0] = new SignalTrial.Pulse { time = float.NaN, height = 1, width = .025f };
        Check(PuzzleSimulation.Validate(p) != null, "NaN target accepted.");
        p = PuzzleDefinition.Legacy(); p.target[1] = p.target[0];
        Check(PuzzleSimulation.Validate(p) != null, "Ambiguous target accepted.");
        p = PuzzleDefinition.Legacy(); var pulse = p.target[0]; pulse.time += .004f; p.target[0] = pulse;
        Check(PuzzleSimulation.Measure(p.solution, p).MeetsTarget, "Within-tolerance target rejected.");
        pulse.time += .01f; p.target[0] = pulse;
        Check(!PuzzleSimulation.Measure(p.solution, p).MeetsTarget, "Outside-tolerance target accepted.");
        var merged = PuzzleSimulation.Normalize(new List<SignalTrial.Pulse> { new SignalTrial.Pulse { time = .2f, height = .5f, width = .025f }, new SignalTrial.Pulse { time = .2f, height = .5f, width = .025f } });
        Check(merged.Count == 1 && merged[0].height == 1, "Coincident traces were not normalized.");
        Debug.Log("PUZZLE WORKSHOP EDGE CASES PASS: 120 more seeds in three profiles, bounded impossible rules, no input loss, NaN, overlap, tolerance and merged traces.");
    }

    public static void Assets()
    {
        PuzzleWorkshopSetup.EnsureAssets();
        var pool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PuzzleWorkshopSetup.PoolPath);
        var playable = pool.Playable(out string issue);
        Check(issue == null && playable.Count >= 4, "Starter pool is missing: " + issue);
        var targets = new HashSet<string>(playable.Select(p => SignalTrial.Encode(p.definition.target)));
        Check(targets.Count >= 4, "Starter pool has cosmetic variants of one target.");
        var kinds = new HashSet<PuzzleKind>();
        for (int seed = 0; seed < 32; seed++) kinds.Add(pool.Choose(seed).kind);
        Check(kinds.Contains(PuzzleKind.Signal) && kinds.Contains(PuzzleKind.ColorMixture), "New-game pool does not select both experiment families.");
        var preview = EditorSceneManager.OpenPreviewScene(PuzzleWorkshopSetup.LabPath);
        try
        {
            var trial = PuzzleWorkshopSetup.FindTrial(preview);
            Check(trial != null && trial.puzzlePool == pool && trial.previewPuzzle == null, "Laboratory is not bound to the random pool.");
            var ids = new HashSet<string>();
            foreach (var book in trial.books)
            {
                var identity = book != null ? book.GetComponent<PuzzleBookIdentity>() : null;
                Check(identity != null && identity.accessible && ids.Add(identity.bookID), "Lab book identities/accessibility invalid.");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (var asset in playable)
        {
            var quest = ScriptableObject.CreateInstance<QuestBlueprintSO>();
            try { quest.questID = asset.definition.id; quest.targetPulses = asset.definition.target;
                Check(SignalTrial.Encode(quest.CreateQuest().targetPulses) == SignalTrial.Encode(asset.definition.target), "Target lost in quest snapshot."); }
            finally { UnityEngine.Object.DestroyImmediate(quest); }
        }
        var document = ScriptableObject.CreateInstance<PuzzleDefinitionSO>();
        var window = ScriptableObject.CreateInstance<PuzzleWorkshopWindow>();
        try
        {
            document.definition = PuzzleDefinition.Legacy(); document.definition.id = "workshop-test-" + Guid.NewGuid().ToString("N");
            string original = JsonUtility.ToJson(document.definition);
            window.Select(document); window.NewDocument();
            Check(original == JsonUtility.ToJson(document.definition), "New command overwrote the selected puzzle.");
            window.Select(document);
            var working = (PuzzleDefinitionSO)typeof(PuzzleWorkshopWindow).GetField("document", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            var add = typeof(PuzzleWorkshopWindow).GetMethod("AddPart", BindingFlags.NonPublic | BindingFlags.Instance);
            int count = document.definition.solution.parts.Count;
            Undo.IncrementCurrentGroup(); add.Invoke(window, new object[] { "pulse-n08" }); Undo.FlushUndoRecordObjects();
            Check(working.definition.solution.parts.Count == count + 1 && document.definition.solution.parts.Count == count, "Editor failed to isolate the working copy.");
            Undo.PerformUndo(); Check(working.definition.solution.parts.Count == count, "Graph Undo failed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(document); }
        Debug.Log("PUZZLE WORKSHOP ASSETS PASS: four distinct verified targets, quest snapshots, New preserves original and graph Undo.");
    }

    public static void DraftSafety()
    {
        string folder = AssetDatabase.GenerateUniqueAssetPath(PuzzleWorkshopSetup.Folder + "/WorkshopChecks");
        AssetDatabase.CreateFolder(PuzzleWorkshopSetup.Folder, Path.GetFileName(folder));
        string draftPath = Path.GetFullPath("UserSettings/PuzzleWorkshop/drafts.json");
        string originalDrafts = File.Exists(draftPath) ? File.ReadAllText(draftPath) : null;
        var window = ScriptableObject.CreateInstance<PuzzleWorkshopWindow>();
        var probe = ScriptableObject.CreateInstance<PuzzleDefinitionSO>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var field = typeof(PuzzleWorkshopWindow).GetField("document", flags);
        try
        {
            probe.definition = PuzzleDefinition.Legacy(); probe.definition.id = "draft-test-" + Guid.NewGuid().ToString("N"); probe.includeInPool = false;
            window.Select(probe);
            typeof(PuzzleWorkshopWindow).GetMethod("AddPart", flags).Invoke(window, new object[] { SignalTrial.Narrow });
            var working = (PuzzleDefinitionSO)field.GetValue(window); string expected = JsonUtility.ToJson(working);
            window.NewDocument();
            object store = typeof(PuzzleWorkshopWindow).GetField("drafts", flags).GetValue(window);
            var entries = (System.Collections.IList)store.GetType().GetField("entries").GetValue(store);
            int found = -1;
            for (int i = 0; i < entries.Count; i++) if ((string)entries[i].GetType().GetField("json").GetValue(entries[i]) == expected) found = i;
            Check(found >= 0, "Switching away lost the unsaved draft.");
            typeof(PuzzleWorkshopWindow).GetMethod("RecoverDraft", flags).Invoke(window, new object[] { found });
            Check(JsonUtility.ToJson(field.GetValue(window)) == expected, "Recovered draft changed.");
            Check(window.SaveToPath(folder + "/First.asset", false), "Draft save failed.");
            string firstID = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(folder + "/First.asset").definition.id;
            Check(window.SaveToPath(folder + "/Copy.asset", true), "Save As failed.");
            working = (PuzzleDefinitionSO)field.GetValue(window);
            Check(working.definition.id != firstID, "Save As reused old identity.");
            working.definition.id = firstID;
            Check(!window.SaveToPath(folder + "/Rejected.asset", false) && AssetDatabase.LoadMainAssetAtPath(folder + "/Rejected.asset") == null,
                "Duplicate ID check happened after creating a conflicting asset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(probe);
            AssetDatabase.DeleteAsset(folder);
            if (originalDrafts != null) File.WriteAllText(draftPath, originalDrafts); else if (File.Exists(draftPath)) File.Delete(draftPath);
        }
        Debug.Log("PUZZLE WORKSHOP DRAFT SAFETY PASS: recover unsaved changes, isolated working copy, Save As identity and duplicate preflight.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Renk Denemesini Hazırla")]
    public static void PrepareColor()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Önce Play'i durdur.");
        PuzzleWorkshopSetup.EnsureAssets();
        if (SceneManager.GetActiveScene().path != PuzzleWorkshopSetup.ScenePath) PuzzleWorkshopSetup.Open();
        var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(PuzzleWorkshopSetup.Folder + "/MorKarisim.asset");
        var trial = PuzzleWorkshopSetup.FindTrial(SceneManager.GetActiveScene()); trial.previewPuzzle = asset;
        EditorUtility.SetDirty(trial); EditorSceneManager.MarkSceneDirty(trial.gameObject.scene); EditorSceneManager.SaveScene(trial.gameObject.scene);
        PuzzleWorkshopWindow.OpenWindow().Select(asset);
        Debug.Log("PUZZLE WORKSHOP COLOR PREVIEW READY.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Canlı Akış")]
    public static void Live()
    {
        Check(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Atölye sahnesinde Play gerekli.");
        Application.runInBackground = true;
        ResearchPuzzleTrial.Active.StartCoroutine(LiveFlow());
    }

    private static IEnumerator LiveFlow()
    {
        var trial = ResearchPuzzleTrial.Active;
        var experiment = SignalExperiment.Active;
        var game = GameManager.Instance;
        game.StartGameMode();
        string savePath = Path.Combine(Application.persistentDataPath, "SenzoraLocalSave.json");
        string normalBefore = File.Exists(savePath) ? Convert.ToBase64String(File.ReadAllBytes(savePath)) : null;
        var player = UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
        var begin = typeof(SynthesisBlockoutPlayer).GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var book in trial.books)
        {
            if (player != null) begin.Invoke(player, new object[] { book, null }); else book.Interact();
            float end = Time.realtimeSinceStartup + 12;
            while (book.isAnimating && Time.realtimeSinceStartup < end) yield return null;
            Check(!book.isAnimating, "Book opening timed out.");
            for (int spread = 0; spread < book.totalPages / 2; spread++)
            {
                yield return null;
                ResearchTrialBookChecks.CollectVisible(); ResearchTrialBookChecks.CheckReadability();
                if (spread + 1 == book.totalPages / 2) break;
                book.NextPage();
                end = Time.realtimeSinceStartup + 8;
                while (book.isAnimating && Time.realtimeSinceStartup < end) yield return null;
            }
            book.ForceExit(); end = Time.realtimeSinceStartup + 10;
            while ((book.isAnimating || game.activeInteraction != null) && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }
        Debug.Log("PUZZLE WORKSHOP LIVE BOOKS PASS.");
        var boardInteraction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        if (player != null) begin.Invoke(player, new object[] { boardInteraction, player.boardView }); else boardInteraction.Interact();
        float deadline = Time.realtimeSinceStartup + 10;
        while (!trial.Board.isMachineActive && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        var workspace = NotebookWorkspaceController.Instance; var notebook = NotebookUI.Instance; var board = trial.Board;
        Check(board.isMachineActive, "Board did not open.");
        workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases();
        var target = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
            .FirstOrDefault(b => !b.IsSource && b.isActiveAndEnabled && b.Data.blockType == NotebookBlockType.Quest && b.Data.questID == trial.CurrentPuzzle.id);
        Check(target != null, "Actual book target was not archived.");
        var area = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(board.EnsureDocuments());
        Check(workspace.TryDropBlock(target, Camera.main.WorldToScreenPoint(area.position)), "Target cannot be pinned.");
        board.EnsureDocuments().SetDocumentPanelOpen(false, true);
        var parts = new List<DraggableFormula>();
        var graph = trial.CurrentPuzzle.solution;
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(workspace);
        var notebookWindow = (RectTransform)page.parent; var parent = (RectTransform)notebookWindow.parent;
        Vector2 original = notebookWindow.anchoredPosition;
        for (int i = 0; i < graph.parts.Count; i++)
        {
            string id = graph.parts[i].itemID;
            Check(workspace.ShowCollectedPart(id), "Part missing from collected archive: " + id); Canvas.ForceUpdateCanvases();
            var note = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
                .FirstOrDefault(b => !b.IsSource && b.isActiveAndEnabled && b.Data.itemID == id);
            var local = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(.18f + i % 3 * .27f, .72f - i / 3 * .32f));
            var pointer = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(local.x, local.y, .02f)));
            float half = notebookWindow.rect.width * notebookWindow.localScale.x * .5f + 50;
            notebookWindow.anchoredPosition = new Vector2(pointer.x < Screen.width * .5f ? -half : -parent.rect.width + half, original.y); Canvas.ForceUpdateCanvases();
            var existing = new HashSet<DraggableFormula>(board.boardTransform.GetComponentsInChildren<DraggableFormula>());
            Check(note != null && workspace.TryDropBlock(note, pointer), "Actual notebook-to-board drop failed: " + id);
            parts.Add(board.boardTransform.GetComponentsInChildren<DraggableFormula>().First(p => !existing.Contains(p)));
        }
        notebookWindow.anchoredPosition = original;
        foreach (var edge in graph.bonds)
        {
            var a = parts[edge.fromIndex].GetOwnNodes().First(n => n.gameObject.activeSelf && !n.isOccupied && n.GetComponent<SignalPort>().output);
            var b = parts[edge.toIndex].GetOwnNodes().First(n => n.gameObject.activeSelf && !n.isOccupied && !n.GetComponent<SignalPort>().output);
            Check(a.CanConnect(b.parentFormula, b), "Physical port refused valid edge."); a.ConnectTo(b);
        }
        board.AttemptSynthesis();
        Check(experiment.LastReading.MeetsTarget, "Physical board did not produce the authored target.");
        SignalTrialPlayChecks.CollectOutput();
        workspace.ShowSolutions(); Canvas.ForceUpdateCanvases();
        foreach (var card in UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None))
        {
            if (card.IsSource || !card.isActiveAndEnabled || !SignalExperiment.IsMeasurementNote(card.Data)) continue;
            var drawing = card.GetComponentInChildren<MixtureGraphic>();
            var body = card.transform.Find("Açıklama")?.GetComponent<TMP_Text>();
            if (drawing != null && body != null)
            {
                var a = new Vector3[4]; var b = new Vector3[4]; drawing.rectTransform.GetWorldCorners(a); body.rectTransform.GetWorldCorners(b);
                Check(a[0].y >= b[1].y - 1, "Mixture diagram overlaps the notebook card description.");
            }
        }
        var measurement = PuzzleInformation.NotebookNotes(notebook).Last(SignalExperiment.IsMeasurementNote).Clone();
        Check(experiment.TryGetMeasurementCode(measurement, out _), "Fresh measurement identity rejected.");
        var saved = new GameData(); trial.CaptureSession(saved); board.SaveData(ref saved); notebook.SaveData(ref saved);
        string snapshot = JsonUtility.ToJson(saved);
        string previousSession = trial.SessionID;
        trial.StartNewSession(); Check(trial.SessionID != previousSession, "New Game retained session identity.");
        Check(!experiment.TryGetMeasurementCode(measurement, out _), "Old measurement accepted in new session.");
        var reloaded = JsonUtility.FromJson<GameData>(snapshot);
        trial.RestoreSession(reloaded); notebook.LoadData(reloaded); board.LoadData(reloaded);
        Check(trial.SessionID == previousSession && experiment.TryGetMeasurementCode(measurement, out _), "Measurement did not survive session restore.");
        var restored = new GameData(); trial.CaptureSession(restored);
        Check(JsonUtility.ToJson(saved.puzzleSession) == JsonUtility.ToJson(restored.puzzleSession), "Exact puzzle/book/measurement snapshot changed.");
        var missing = JsonUtility.FromJson<GameData>(snapshot); missing.puzzleSession.books[0].bookID = "missing-book";
        bool rejected = false;
        try { trial.RestoreSession(missing); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && trial.SessionID == previousSession, "Missing book silently rerolled or lost the session.");
        PersistentSaveSafety(trial, saved);
        Debug.Log("PUZZLE WORKSHOP LIVE SAVE PASS: exact snapshot, stale reading rejection and missing-book preservation.");
        boardInteraction.ForceExit();
        deadline = Time.realtimeSinceStartup + 10;
        while (game.activeInteraction != null && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        SignalTrialPlayChecks.OpenTerminal();
        deadline = Time.realtimeSinceStartup + 10;
        while ((game.activeInteraction != experiment.terminal || !experiment.terminal.RequiresMeasurementCursor) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSeconds(1);
        SignalTrialPlayChecks.SubmitLastOutput();
        Check(trial.Completed.Contains(trial.CurrentPuzzle.id), "Real Turing wheels did not complete the current puzzle.");
        Check((File.Exists(savePath) ? Convert.ToBase64String(File.ReadAllBytes(savePath)) : null) == normalBefore, "Normal save file changed during workshop test.");
        Debug.Log("PUZZLE WORKSHOP LIVE FLOW PASS: real books -> notebook -> physical graph -> target -> exact save -> restored output -> real Turing wheels; user's save untouched.");
    }

    private static void PersistentSaveSafety(ResearchPuzzleTrial trial, GameData snapshot)
    {
        var save = SaveManager.Instance;
        GameObject owner = null;
        if (save == null) { owner = new GameObject("Workshop isolated save test"); save = owner.AddComponent<SaveManager>(); }
        bool previousSessionOnly = save.sessionOnly; string previousPath = save.isolatedSavePath;
        string path = Path.GetFullPath("UserSettings/PuzzleWorkshop/isolated-check-" + Guid.NewGuid().ToString("N") + ".json");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            save.isolatedSavePath = path; save.sessionOnly = false;
            save.RegisterSaveable(NotebookUI.Instance); save.RegisterSaveable(trial.Board);
            save.SaveGame(); Check(File.Exists(path), "Isolated save was not written.");
            string saved = File.ReadAllText(path);
            trial.StartNewSession();
            Check(save.LoadGame() && trial.SessionID == snapshot.puzzleSession.sessionID, "SaveManager did not restore the session before board/notebook.");
            var broken = JsonUtility.FromJson<GameData>(saved); broken.puzzleSession.books[0].bookID = "missing-test-book";
            string invalid = JsonUtility.ToJson(broken); File.WriteAllText(path, invalid);
            Check(!save.LoadGame() && save.SaveBlocked, "Missing-book Continue was accepted.");
            save.SaveGame(); Check(File.ReadAllText(path) == invalid, "Autosave overwrote a refused save.");
            var menuPanel = new GameObject("Isolated Continue menu", typeof(RectTransform), typeof(CanvasGroup));
            bool wasStarted = GameManager.Instance.isGameStarted;
            try
            {
                var menu = menuPanel.AddComponent<MainMenuManager>();
                typeof(MainMenuManager).GetField("mainMenuPanel", flags).SetValue(menu, menuPanel);
                typeof(MainMenuManager).GetField("_menuCanvasGroup", flags).SetValue(menu, menuPanel.GetComponent<CanvasGroup>());
                typeof(MainMenuManager).GetField("fadeDuration", flags).SetValue(menu, 0f);
                GameManager.Instance.isGameStarted = false;
                var routine = (IEnumerator)typeof(MainMenuManager).GetMethod("StartGameTransitionRoutine", flags).Invoke(menu, new object[] { true });
                while (routine.MoveNext()) { }
                Check(menuPanel.activeSelf && menuPanel.GetComponent<CanvasGroup>().interactable && !GameManager.Instance.isGameStarted,
                    "Failed Continue fell through to gameplay instead of restoring the menu.");
            }
            finally { GameManager.Instance.isGameStarted = wasStarted; UnityEngine.Object.Destroy(menuPanel); }
            var activeField = typeof(ResearchPuzzleTrial).GetField("<Active>k__BackingField", flags);
            var originalBook = trial.books[0];
            try
            {
                activeField.SetValue(null, null); trial.books[0] = null;
                Check(!save.LoadGame() && save.SaveBlocked, "Startup configuration failure bypassed scene trial restoration.");
                save.SaveGame(); Check(File.ReadAllText(path) == invalid, "Failed scene configuration allowed overwrite.");
            }
            finally { trial.books[0] = originalBook; activeField.SetValue(null, trial); }
            File.WriteAllText(path, saved); Check(save.LoadGame() && !save.SaveBlocked, "A successful load did not release autosave protection.");
        }
        finally
        {
            save.sessionOnly = previousSessionOnly; save.isolatedSavePath = previousPath;
            if (File.Exists(path)) File.Delete(path);
            if (owner != null) UnityEngine.Object.Destroy(owner);
        }
        Debug.Log("PUZZLE WORKSHOP PERSISTENT SAVE PASS: isolated file, ordered restore, failed-Continue protection and failed-startup refusal.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Editör Modeli")]
    public static void EditorModel()
    {
        var assembly = typeof(PuzzleWorkshopChecks).Assembly;
        Check(assembly.GetType("PuzzleWorkshopWindow") != null, "RED: workshop editor window is missing.");
        Check(assembly.GetType("PuzzleWorkshopSetup") != null, "RED: workshop scene setup is missing.");
        Debug.Log("PUZZLE WORKSHOP EDITOR MODEL PASS.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Görsel Model")]
    public static void VisualModel()
    {
        Check(typeof(SynthesisQuestData).GetField("targetPulses") != null, "RED: target graphics still assume the single legacy target.");
        Check(typeof(QuestBlueprintSO).GetField("targetPulses") != null, "RED: puzzle assets cannot pass targets to notebook/board.");
        Debug.Log("PUZZLE WORKSHOP VISUAL MODEL PASS.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Oturum Modeli")]
    public static void SessionModel()
    {
        var assembly = typeof(SignalTrial).Assembly;
        Check(assembly.GetType("PuzzleSessionData") != null, "RED: persistent puzzle session is missing.");
        Check(typeof(ResearchPuzzleTrial).GetMethod("StartNewSession") != null, "RED: New Game does not select a puzzle.");
        Check(typeof(ResearchPuzzleTrial).GetMethod("RestoreSession") != null, "RED: session must restore before notebook/board.");
        Check(typeof(SignalExperiment.Reading).GetField("sessionID") != null, "RED: measurement has no session identity.");
        Debug.Log("PUZZLE WORKSHOP SESSION MODEL PASS.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Üretim")]
    public static void Generation()
    {
        var assembly = typeof(SignalTrial).Assembly;
        var generator = assembly.GetType("PuzzleGenerator");
        Check(generator != null, "RED: PuzzleGenerator is missing.");
        var model = assembly.GetType("PuzzleDefinition");
        var distribution = assembly.GetType("PuzzleBookDistribution");
        Check(distribution != null, "RED: book distribution is missing.");
        var generate = generator.GetMethod("TryGenerate");
        var validate = assembly.GetType("PuzzleSimulation").GetMethod("Validate");
        for (int seed = 0; seed < 120; seed++)
        {
            object settings = Activator.CreateInstance(model);
            var args = new[] { settings, (object)seed, null, null };
            Check((bool)generate.Invoke(null, args), "Generation failed at seed " + seed + ": " + args[3]);
            Check(string.IsNullOrEmpty((string)validate.Invoke(null, new[] { args[2] })), "Generated witness is invalid at " + seed);
            var repeat = new[] { settings, (object)seed, null, null };
            Check((bool)generate.Invoke(null, repeat), "Repeat generation failed.");
            Check(JsonUtility.ToJson(args[2]) == JsonUtility.ToJson(repeat[2]), "Same seed was not deterministic.");
            var build = distribution.GetMethod("Build", new[] { typeof(PuzzleDefinition), typeof(int), typeof(int) });
            var pages = (List<BookWorkspacePage>[])build.Invoke(null, new[] { args[2], (object)4, seed + 200 });
            var again = (List<BookWorkspacePage>[])build.Invoke(null, new[] { args[2], (object)4, seed + 200 });
            int targets = 0; var items = new HashSet<string>();
            for (int book = 0; book < pages.Length; book++)
                for (int page = 0; page < pages[book].Count; page++)
                {
                    Check(JsonUtility.ToJson(pages[book][page]) == JsonUtility.ToJson(again[book][page]), "Book assignment changed for same seed.");
                    Check(pages[book][page].blocks.Count <= 2, "Page overflow.");
                    foreach (var note in pages[book][page].blocks)
                    {
                        if (note.blockType == NotebookBlockType.Quest) targets++;
                        if (note.blockType == NotebookBlockType.PartClue) Check(items.Add(note.itemID), "Duplicate part clue.");
                    }
                }
            Check(targets == 1 && items.Count >= 7, "Required clues missing.");
        }
        Debug.Log("PUZZLE WORKSHOP GENERATION PASS: 120 seeds, simulated witnesses, determinism and complete two-block book pages.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Model")]
    public static void Model()
    {
        var assembly = typeof(SignalTrial).Assembly;
        var model = assembly.GetType("PuzzleDefinition");
        Check(model != null, "RED: PuzzleDefinition model is missing.");
        var simulation = assembly.GetType("PuzzleSimulation");
        Check(simulation != null, "RED: shared simulation is missing.");
        object legacy = model.GetMethod("Legacy").Invoke(null, null);
        var measure = simulation.GetMethod("Measure");
        var validate = simulation.GetMethod("Validate");
        Check(string.IsNullOrEmpty((string)validate.Invoke(null, new[] { legacy })), "Legacy witness does not validate.");
        var graph = (SynthesisGraphData)model.GetField("solution").GetValue(legacy);
        var reading = (SignalExperiment.Reading)measure.Invoke(null, new[] { graph, legacy });
        Check(reading.Measurable && reading.MeetsTarget, "Legacy target is unreachable.");
        Check(reading.pulses.Count == 2 && Mathf.Abs(reading.pulses[0].height - .5f) < .001f
            && Mathf.Abs(reading.pulses[1].time - .50f) < .001f, "Double Record changed.");
        foreach (var pair in new[] { ("pulse-n08", .5f, .18f, 1), ("pulse-n21", 1f, .34f, 1), ("pulse-n46", 1f, .18f, 2) })
        {
            var sample = Chain(pair.Item1);
            var result = (SignalExperiment.Reading)measure.Invoke(null, new[] { sample, legacy });
            Check(result.Measurable && result.pulses.Count == pair.Item4, "New sample trace count: " + pair.Item1);
            Check(Mathf.Abs(result.pulses[0].height - pair.Item2) < .001f
                && Mathf.Abs(result.pulses[0].time - pair.Item3) < .001f, "New sample behavior: " + pair.Item1);
        }
        var unused = Clone(graph);
        unused.parts.Add(new SynthesisPartData { itemID = SignalTrial.Wide });
        var unusedReading = (SignalExperiment.Reading)measure.Invoke(null, new[] { unused, legacy });
        Check(!unusedReading.MeetsTarget && !string.IsNullOrEmpty(unusedReading.targetFeedback), "Unused part bypassed constraints.");
        var cycle = Chain(SignalTrial.Narrow);
        cycle.parts.Add(new SynthesisPartData { itemID = SignalTrial.Delay });
        cycle.parts.Add(new SynthesisPartData { itemID = SignalTrial.Delay });
        Edge(cycle, 3, 4); Edge(cycle, 4, 3);
        Check(!((SignalExperiment.Reading)measure.Invoke(null, new[] { cycle, legacy })).Measurable, "Disconnected cycle accepted.");
        var reordered = Clone(graph);
        reordered.parts.Reverse();
        foreach (var edge in reordered.bonds) { edge.fromIndex = graph.parts.Count - 1 - edge.fromIndex; edge.toIndex = graph.parts.Count - 1 - edge.toIndex; }
        Check(((SignalExperiment.Reading)measure.Invoke(null, new[] { reordered, legacy })).MeetsTarget, "Equivalent node order rejected.");
        Debug.Log("PUZZLE WORKSHOP MODEL PASS: legacy, three new samples, unused parts, global cycles and equivalent graph.");
    }

    public static SynthesisGraphData Chain(params string[] ids)
    {
        var graph = new SynthesisGraphData();
        graph.parts.Add(new SynthesisPartData { itemID = SignalTrial.Source });
        foreach (string id in ids) graph.parts.Add(new SynthesisPartData { itemID = id });
        graph.parts.Add(new SynthesisPartData { itemID = SignalTrial.Meter });
        for (int i = 1; i < graph.parts.Count; i++) Edge(graph, i - 1, i);
        return graph;
    }
    public static void Edge(SynthesisGraphData graph, int from, int to) => graph.bonds.Add(new SynthesisBondData { fromIndex = from, toIndex = to, directed = true });
    public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    public static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
