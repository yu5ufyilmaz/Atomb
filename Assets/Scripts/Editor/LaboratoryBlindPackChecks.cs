using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LaboratoryBlindPackChecks
{
    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Üçlü Araştırma Modeli")]
    public static void Model()
    {
        var build = typeof(PuzzleBookDistribution).GetMethod("Build", new[] { typeof(List<PuzzleDefinition>), typeof(int), typeof(int) });
        Require(build != null, "RED: books cannot yet contain three simultaneous puzzle targets.");
        var puzzles = new List<PuzzleDefinition>();
        for (int i = 0; i < 3; i++) { var p = PuzzleDefinition.Legacy(); p.id = "pack-check-" + i; p.title = "Arşiv " + i; puzzles.Add(p); }
        for (int seed = 0; seed < 24; seed++) foreach (int bookCount in new[] { 4, 47 })
        {
            var books = (List<BookWorkspacePage>[])build.Invoke(null, new object[] { puzzles, bookCount, seed });
            var targets = new List<int>();
            foreach (var puzzle in puzzles)
            {
                var containing = Enumerable.Range(0, bookCount).Where(b => books[b].SelectMany(p => p.blocks).Any(n => n.blockType == NotebookBlockType.Quest && n.questID == puzzle.id)).ToList();
                Require(containing.Count == 1, "A target was duplicated or lost."); targets.Add(containing[0]);
            }
            Require(targets[0] == 0 && targets.Distinct().Count() == 3, "Targets must occupy three different books, with one at the start.");
            Require(books.All(b => b.Count > 0 && b.All(p => p.blocks.Count <= 2)), "Empty book or crowded page.");
            var notes = books.SelectMany(b => b).SelectMany(p => p.blocks).ToList();
            Require(notes.Select(n => n.instanceID).Distinct().Count() == notes.Count, "Archive identities collide across puzzles.");
            foreach (var part in PuzzleParts.ForKind(PuzzleKind.Signal).Where(p => puzzles.Any(q => q.rules.Allows(p.id))))
                Require(notes.Any(n => n.blockType == NotebookBlockType.PartClue && n.itemID == part.id), "Missing collectable part.");
        }
            Debug.Log("LAB BLIND PACK MODEL PASS: distinct target books, bounded pages, unique archive identities, 48 distributions including 47-book library.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Üçlü Araştırma Canlı")]
    public static void Live()
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null && ResearchPuzzleTrial.Active.Puzzles.Count == 3, "Üçlü laboratuvarda Play gerekli.");
        ResearchPuzzleTrial.Active.StartCoroutine(LiveFlow());
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Kitaplara Normal Erişim")]
    public static void ShelfSelection() => ResearchPuzzleTrial.Active.StartCoroutine(ShelfSelectionFlow());

    private static IEnumerator ShelfSelectionFlow()
    {
        GameManager.Instance.StartGameMode(); yield return null;
        CheckShelfSelection();
    }

    private static void CheckShelfSelection()
    {
        var trial = ResearchPuzzleTrial.Active;
        Require(Application.isPlaying && trial != null, "Laboratuvarda Play gerekli.");
        var interaction = UnityEngine.Object.FindFirstObjectByType<PlayerInteraction>();
        var body = interaction.GetComponent<CharacterController>() ?? interaction.GetComponentInParent<CharacterController>();
        Require(body != null && !interaction.isTutorialMode, "Normal oyuncu etkileşimi hazır değil.");
        float range = (float)typeof(PlayerInteraction).GetField("interactionDistance", Private).GetValue(interaction);
        int mask = ((LayerMask)typeof(PlayerInteraction).GetField("interactionLayer", Private).GetValue(interaction)).value;
        Physics.SyncTransforms();
        float floorY = body.bounds.min.y;
        float worldHeight = body.bounds.size.y, worldRadius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z);
        var origin = (Transform)typeof(PlayerInteraction).GetField("raycastOrigin", Private).GetValue(interaction);
        if (origin == null) origin = Camera.main.transform;
        float eyeHeight = origin.position.y - floorY;
        var failed = new List<string>();
        Physics.SyncTransforms();
        foreach (var book in trial.books)
        {
            var collider = book.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.enabled && !c.isTrigger && ((1 << c.gameObject.layer) & mask) != 0);
            if (collider == null) { failed.Add(book.name + " (no selectable collider)"); continue; }
            Vector3 target = collider.bounds.center;
            Vector3 extent = collider.bounds.extents * .8f;
            var aims = new[] { target, target + Vector3.right * extent.x, target - Vector3.right * extent.x,
                target + Vector3.up * extent.y, target - Vector3.up * extent.y,
                target + Vector3.forward * extent.z, target - Vector3.forward * extent.z };
            float closest = float.PositiveInfinity;
            int standing = 0;
            bool found = false;
            foreach (float radius in new[] { .45f, .7f, 1f, 1.25f })
            {
                for (int angle = 0; angle < 360 && !found; angle += 15)
                {
                    Vector3 feet = new Vector3(target.x + Mathf.Cos(angle * Mathf.Deg2Rad) * radius, floorY + .25f, target.z + Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                    if (!Physics.Raycast(feet, Vector3.down, out var floor, .75f, ~0, QueryTriggerInteraction.Ignore) || floor.normal.y < .8f) continue;
                    feet.y = floor.point.y + .04f;
                    float r = worldRadius * .95f;
                    var blockers = Physics.OverlapCapsule(feet + Vector3.up * r, feet + Vector3.up * (worldHeight - r), r, ~0, QueryTriggerInteraction.Ignore);
                    if (blockers.Any(c => !c.transform.IsChildOf(body.transform))) continue;
                    standing++;
                    Vector3 eye = feet + Vector3.up * eyeHeight;
                    foreach (var aim in aims)
                        if (Physics.Raycast(eye, (aim - eye).normalized, out var hit, Mathf.Max(range, 3f), mask)
                            && hit.collider.GetComponentInParent<InteractableBook>() == book)
                        { closest = Mathf.Min(closest, hit.distance); if (hit.distance <= range) found = true; }
                }
                if (found) break;
            }
            if (!found) failed.Add(book.name + " at " + target + " closest=" + closest + " standing=" + standing);
        }
        Require(failed.Count == 0, "Books lack a standing selection position within the normal interaction range (eye=" + eyeHeight + ", floor=" + floorY + ", radius=" + worldRadius + "): " + string.Join("; ", failed));
        Debug.Log("LAB BLIND PACK SHELF SELECTION PASS: all " + trial.books.Length + " book colliders selectable from unobstructed standing positions using the normal " + range + "m player raycast.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Üçlü Ölçüm Kimliği")]
    public static void MeasurementIdentity() => ResearchPuzzleTrial.Active.StartCoroutine(IdentityFlow());

    private static IEnumerator IdentityFlow()
    {
        var trial = ResearchPuzzleTrial.Active; var board = trial.Board; var experiment = SignalExperiment.Active;
        GameManager.Instance.StartGameMode();
        var interaction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        if (!board.isMachineActive) interaction.Interact();
        yield return WaitFor(() => board.isMachineActive, "Identity board opening");
        string measuredID = trial.Puzzles[0].id;
        board.TryPinQuest(trial.Content.Quests[0].CreateQuest()); experiment.Measure(trial.CurrentPuzzle.solution);
        board.TryPinQuest(trial.Content.Quests[1].CreateQuest());
        var saved = new GameData(); trial.CaptureSession(saved); trial.RestoreSession(saved);
        var output = (NotebookDraggableBlock)typeof(SignalExperiment).GetField("output", Private).GetValue(experiment);
        Require(output.Data.questID == measuredID, "Restored output note was relabeled with another target's identity.");
        Require(!experiment.Submit(experiment.LastReading.Code), "Restored reading accepted under a different target.");
        board.TryPinQuest(trial.Content.Quests[0].CreateQuest());
        Require(experiment.Submit(experiment.LastReading.Code), "Restored reading rejected under its own target.");
        Debug.Log("LAB BLIND PACK MEASUREMENT IDENTITY PASS.");
    }

    private static IEnumerator LiveFlow()
    {
        Application.runInBackground = true;
        var trial = ResearchPuzzleTrial.Active; var game = GameManager.Instance; var board = trial.Board;
        var experiment = SignalExperiment.Active; var notebook = NotebookUI.Instance; var workspace = NotebookWorkspaceController.Instance;
        string path = Path.Combine(Application.persistentDataPath, "SenzoraLocalSave.json");
        string normalBefore = File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null;
        yield return ResearchGuidanceChecks.EnterNormalGame();
        var player = UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
        var begin = typeof(SynthesisBlockoutPlayer).GetMethod("Begin", Private);
        int checkedBooks = 0;
        foreach (var book in trial.books)
        {
            if (player != null) begin.Invoke(player, new object[] { book, null }); else book.Interact();
            yield return WaitFor(() => !book.isAnimating, "Book opening");
            for (int spread = 0; spread < book.totalPages / 2; spread++)
            {
                yield return null;
                if (book.GetComponentsInChildren<NotebookDraggableBlock>().Any(n => n.IsSource && ResearchTrialBookChecks.Visible(n)))
                { ResearchTrialBookChecks.CheckReadability(); ResearchTrialBookChecks.CollectVisible(); }
                if (spread + 1 < book.totalPages / 2) { book.NextPage(); yield return WaitFor(() => !book.isAnimating, "Page turn"); }
            }
            book.ForceExit(); yield return WaitFor(() => !book.isAnimating && game.activeInteraction == null, "Book closing");
            yield return null;
            if (++checkedBooks % 10 == 0) Debug.Log("LAB BLIND PACK BOOKS CHECKED: " + checkedBooks + "/" + trial.books.Length);
        }
        Require(notebook.EnsureCollectedQuests().Count == 3, "Not all three goals can be collected from the physical books.");
        var boardInteraction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        var puzzles = trial.Puzzles.ToList(); var readings = new Dictionary<string, SignalExperiment.Reading>();
        foreach (int index in new[] { 2, 0, 1 })
        {
            var puzzle = puzzles[index];
            if (player != null) begin.Invoke(player, new object[] { boardInteraction, player.boardView }); else boardInteraction.Interact();
            yield return WaitFor(() => board.isMachineActive, "Board opening"); yield return null;
            board.ResetBoard(); yield return null;
            typeof(NotebookWorkspaceController).GetField("targetArchivePage", Private).SetValue(workspace, notebook.EnsureCollectedQuests().FindIndex(n => n.questID == puzzle.id));
            workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases();
            var goal = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
                .FirstOrDefault(n => !n.IsSource && n.isActiveAndEnabled && n.Data.blockType == NotebookBlockType.Quest && n.Data.questID == puzzle.id);
            var area = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(board.EnsureDocuments());
            var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
            var notebookWindow = (RectTransform)page.parent; var parent = (RectTransform)notebookWindow.parent;
            Vector2 original = notebookWindow.anchoredPosition;
            Vector2 goalPointer = Camera.main.WorldToScreenPoint(area.position);
            float goalHalf = notebookWindow.rect.width * notebookWindow.localScale.x * .5f + 50;
            notebookWindow.anchoredPosition = new Vector2(goalPointer.x < Screen.width * .5f ? -goalHalf : -parent.rect.width + goalHalf, original.y); Canvas.ForceUpdateCanvases();
            Require(goal != null && workspace.TryDropBlock(goal, goalPointer), "Collected target cannot be pinned.");
            notebookWindow.anchoredPosition = original;
            Require(trial.CurrentPuzzle.id == puzzle.id, "Pinning a different target did not select its simulator: pinned=" + board.PinnedQuest?.questID + " active=" + trial.CurrentPuzzle.id + " wanted=" + puzzle.id);
            board.EnsureDocuments().SetDocumentPanelOpen(false, true);
            var parts = new List<DraggableFormula>();
            for (int i = 0; i < puzzle.solution.parts.Count; i++)
            {
                string id = puzzle.solution.parts[i].itemID;
                Require(workspace.ShowCollectedPart(id), "A required part cannot be collected from the books."); Canvas.ForceUpdateCanvases();
                var note = UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
                    .FirstOrDefault(n => !n.IsSource && n.isActiveAndEnabled && n.Data.itemID == id);
                Vector2 local = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(.18f + i % 3 * .27f, .72f - i / 3 * .32f));
                Vector2 pointer = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(local.x, local.y, .02f)));
                float half = notebookWindow.rect.width * notebookWindow.localScale.x * .5f + 50;
                notebookWindow.anchoredPosition = new Vector2(pointer.x < Screen.width * .5f ? -half : -parent.rect.width + half, original.y); Canvas.ForceUpdateCanvases();
                var existing = new HashSet<DraggableFormula>(board.boardTransform.GetComponentsInChildren<DraggableFormula>());
                Require(note != null && workspace.TryDropBlock(note, pointer), "Collected part cannot be dropped on the physical board.");
                parts.Add(board.boardTransform.GetComponentsInChildren<DraggableFormula>().First(p => !existing.Contains(p)));
            }
            notebookWindow.anchoredPosition = original;
            foreach (var edge in puzzle.solution.bonds)
            {
                var a = parts[edge.fromIndex].GetOwnNodes().First(n => n.gameObject.activeSelf && !n.isOccupied && n.GetComponent<SignalPort>().output);
                var b = parts[edge.toIndex].GetOwnNodes().First(n => n.gameObject.activeSelf && !n.isOccupied && !n.GetComponent<SignalPort>().output);
                Require(a.CanConnect(b.parentFormula, b), "A valid graph cannot connect through the real ports."); a.ConnectTo(b);
            }
            board.AttemptSynthesis(); Require(experiment.LastReading.MeetsTarget, "Authored puzzle cannot be solved on the physical board.");
            readings.Add(puzzle.id, experiment.LastReading); SignalTrialPlayChecks.CollectOutput();
            var wrong = trial.Content.Quests.First(q => q.questID != puzzle.id);
            Require(board.TryPinQuest(wrong.CreateQuest()) && !experiment.Submit(experiment.LastReading.Code), "A different target accepted this result.");
            Require(board.TryPinQuest(trial.Content.Quests.First(q => q.questID == puzzle.id).CreateQuest()), "Cannot return to a previously pinned target.");
            boardInteraction.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Board closing"); yield return null;
            SignalTrialPlayChecks.OpenTerminal();
            yield return WaitFor(() => game.activeInteraction == experiment.terminal && experiment.terminal.RequiresMeasurementCursor, "Terminal opening");
            yield return new WaitForSeconds(.5f);
            SignalTrialPlayChecks.SubmitLastOutput();
            Require(trial.Completed.Contains(puzzle.id), "Real terminal did not complete the selected target.");
            experiment.terminal.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Terminal closing"); yield return null;
            Debug.Log("LAB BLIND PACK REAL TARGET PASS: " + trial.Completed.Count + "/3; other-target submission refused.");
        }
        Require(trial.Completed.Count == 3, "Completing a target lost previous completion.");
        var saved = new GameData(); trial.CaptureSession(saved); board.SaveData(ref saved); notebook.SaveData(ref saved);
        string exact = JsonUtility.ToJson(saved.puzzleSession); string oldID = trial.SessionID;
        trial.StartNewSession(); Require(trial.SessionID != oldID && trial.Completed.Count == 0, "New Game did not reset the full set.");
        trial.RestoreSession(saved); notebook.LoadData(saved); board.LoadData(saved);
        var restored = new GameData(); trial.CaptureSession(restored);
        Require(JsonUtility.ToJson(restored.puzzleSession) == exact, "Full set, pages, measurements or completion changed during restore.");
        boardInteraction.Interact(); yield return WaitFor(() => board.isMachineActive, "Restored board opening");
        foreach (var puzzle in puzzles)
        {
            board.TryPinQuest(trial.Content.Quests.First(q => q.questID == puzzle.id).CreateQuest());
            Require(experiment.Submit(readings[puzzle.id].Code), "A restored measurement was lost when another target was active.");
        }
        boardInteraction.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Restored board closing");
        Require((File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null) == normalBefore, "Playtest changed the normal save.");
        Debug.Log("LAB BLIND PACK LIVE PASS: all physical books readable/collectable, 3 goals solved out of order via notebook/board/real terminal, cross-target refusal, complete snapshot restore, normal save untouched.");
    }

    private static IEnumerator WaitFor(Func<bool> ready, string context)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Require(ready(), context + " timed out.");
    }

    public static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
