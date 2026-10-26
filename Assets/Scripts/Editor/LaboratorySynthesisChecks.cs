using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class LaboratorySynthesisChecks
{
    [MenuItem("Senzora/Lab Migration/Validate Board Pointer")]
    public static void ValidateBoardPointer() => ResearchPuzzleTrial.Active.StartCoroutine(BoardPointerFlow());

    private static IEnumerator BoardPointerFlow()
    {
        Application.runInBackground = true;
        if (!GameManager.Instance.isGameStarted)
        {
            UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().PlayGame();
            while (!GameManager.Instance.isGameStarted) yield return null;
            yield return new WaitForSeconds(2);
        }
        var board = ResearchPuzzleTrial.Active.Board;
        if (!board.isMachineActive)
        {
            UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>().Interact();
            while (!board.isMachineActive) yield return null;
        }
        yield return null;
        CheckBoardPointer();
    }

    private static void CheckBoardPointer()
    {
        var board = ResearchPuzzleTrial.Active.Board;
        var camera = Camera.main;
        var lens = VolumeManager.instance.stack.GetComponent<LensDistortion>();
        Check(lens != null && Mathf.Abs(lens.intensity.value) < .001f && Mathf.Abs(lens.scale.value - 1) < .001f,
            "Rendered board is distorted relative to its pointer ray.");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var calculate = typeof(DraggableFormula).GetMethod("CalculateDragPosition", flags);
        var center = (board.minBounds + board.maxBounds) * .5f;
        var formula = board.SpawnFormula(ResearchPuzzleTrial.Active.Content.Parts[0],
            camera.WorldToScreenPoint(board.boardTransform.TransformPoint(center)));
        Check(formula != null, "Pointer test could not create a chalk part.");
        typeof(DraggableFormula).GetField("mainCam", flags).SetValue(formula, camera);
        try
        {
            foreach (float x in new[] { .25f, .5f, .72f })
            {
                Vector2 local = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(x, .55f));
                Vector2 pointer = camera.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(local.x, local.y, formula.surfaceOffset)));
                formula.dragOffset = Vector3.zero;
                calculate.Invoke(formula, new object[] { pointer });
                float error = Vector2.Distance(pointer, camera.WorldToScreenPoint(formula.targetPosition));
                Check(error < 1, "Chalk part drifts from the pointer by " + error.ToString("F1") + " pixels.");
            }
            Debug.Log("LAB BOARD POINTER PASS: chalk part stays under the cursor at three board positions.");
        }
        finally { board.RemoveFormula(formula); }
    }

    [MenuItem("Senzora/Lab Migration/Preview Drag Feel")]
    public static void PreviewDragFeel() => ResearchPuzzleTrial.Active.StartCoroutine(DragFeel());

    private static IEnumerator DragFeel()
    {
        Application.runInBackground = true;
        Check(GameManager.Instance.isGameStarted && GameManager.Instance.activeInteraction == null, "Start the game and exit interactions first.");
        NotebookWorkspaceController.Instance.Close();
        var book = ResearchPuzzleTrial.Active.books[1];
        book.Interact();
        while (book.isAnimating) yield return null;
        yield return null;
        CheckBookMove(book);
        var source = book.GetComponentsInChildren<NotebookDraggableBlock>().First(n => n.IsSource);
        var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .47f, Screen.height * .52f) };
        source.OnPointerEnter(pointer); source.OnBeginDrag(pointer); source.OnDrag(pointer);
        yield return new WaitForSeconds(.3f);
        ScreenCapture.CaptureScreenshot("Temp/lab-note-drag.png");
        yield return null;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", flags).GetValue(NotebookWorkspaceController.Instance);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, page.position);
        source.OnDrag(pointer); source.OnEndDrag(pointer); source.OnPointerExit(pointer);
        Debug.Log("LAB DRAG PREVIEW: Temp/lab-note-drag.png");
    }

    [MenuItem("Senzora/Lab Migration/Audit Presentation")]
    public static void AuditPresentation()
    {
        var scene = SceneManager.GetActiveScene();
        var trial = LaboratorySynthesisSetup.Find<ResearchPuzzleTrial>(scene);
        var board = LaboratorySynthesisSetup.Find<ChalkboardManager>(scene);
        var lines = LaboratorySynthesisSetup.All<InteractableBook>(scene).Select(b =>
            PathOf(b.transform) + " | research=" + Array.IndexOf(trial.books, b) + " | procedural=" + b.includeProceduralClues
            + " | physical=" + b.usePhysicalPageBlocks + " | view=" + b.viewPositionOffset + " | scale=" + b.transform.lossyScale);
        lines = lines.Concat(new[] { "BOARD " + PathOf(board.boardTransform) + " bounds=" + board.minBounds + "/" + board.maxBounds });
        lines = lines.Concat(LaboratorySynthesisSetup.All<MeshRenderer>(scene)
            .Where(r => r.name.Contains("Cube") || r.transform.IsChildOf(board.boardTransform.parent))
            .Select(r => PathOf(r.transform) + " | bounds=" + r.bounds + " | active=" + r.gameObject.activeInHierarchy));
        File.WriteAllLines("Temp/lab-presentation-audit.txt", lines);
        Debug.Log("LAB PRESENTATION AUDIT: Temp/lab-presentation-audit.txt");
    }

    [MenuItem("Senzora/Lab Migration/Preview Presentation")]
    public static void PreviewPresentation() => ResearchPuzzleTrial.Active.StartCoroutine(Presentation());

    private static IEnumerator Presentation()
    {
        Application.runInBackground = true;
        if (!GameManager.Instance.isGameStarted)
        {
            UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().PlayGame();
            while (!GameManager.Instance.isGameStarted) yield return null;
            yield return new WaitForSeconds(2);
        }
        var trial = ResearchPuzzleTrial.Active;
        for (int i = 0; i < trial.books.Length; i++)
        {
            var book = trial.books[i];
            book.Interact();
            while (book.isAnimating) yield return null;
            yield return new WaitForSeconds(.4f);
            ScreenCapture.CaptureScreenshot("Temp/lab-book-" + i + ".png");
            yield return null;
            book.ForceExit();
            while (book.isAnimating || GameManager.Instance.activeInteraction != null) yield return null;
        }
        var board = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        board.Interact();
        while (!trial.Board.isMachineActive) yield return null;
        yield return new WaitForSeconds(.5f);
        ScreenCapture.CaptureScreenshot("Temp/lab-board.png");
        Debug.Log("LAB PRESENTATION PREVIEW READY");
    }

    [MenuItem("Senzora/Lab Migration/Validate Menu Start")]
    public static void ValidateMenuStart()
    {
        Check(Application.isPlaying && !GameManager.Instance.isGameStarted && SaveManager.Instance.sessionOnly, "Start in the copied scene's title screen.");
        ResearchPuzzleTrial.Active.StartCoroutine(MenuStart());
    }

    private static IEnumerator MenuStart()
    {
        Application.runInBackground = true;
        var game = GameManager.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        game.UpdateCursorState();
        workspace.ToggleStandaloneNotebook();
        Check(!workspace.IsActive && Cursor.visible, "Title-screen Tab captured the pointer.");
        var save = SaveManager.Instance;
        Check(!save.HasSaveFile() && !save.LoadGame(), "Session copy exposed Continue.");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        Check(menu != null, "Original laboratory menu missing.");
        var start = UnityEngine.Object.FindFirstObjectByType<InGameMenuController>();
        float introDuration = start != null && start.introTimeline != null ? (float)start.introTimeline.duration : 0;
        menu.PlayGame();
        float end = Time.time + introDuration + 15;
        float realEnd = Time.realtimeSinceStartup + 180;
        Debug.Log("LAB MENU WAIT: authored intro duration=" + introDuration);
        while (!game.isGameStarted && Time.time < end && Time.realtimeSinceStartup < realEnd) yield return null;
        Check(game.isGameStarted, "Original menu/start animation did not reach gameplay.");
        Check(!Cursor.visible, "Original menu/start animation did not lock the pointer.");
        ValidateNotebook();
        // The blockout's original free-pointer notebook mode must retain its behavior.
        workspace.standaloneUnlocksCursor = true;
        try { SignalTrialSetup.ValidateStandaloneNotebook(); }
        finally { workspace.Close(); workspace.standaloneUnlocksCursor = false; }
        workspace.ToggleStandaloneNotebook();
        yield return new WaitForSeconds(.4f);
        ScreenCapture.CaptureScreenshot("Temp/lab-notebook-start.png");
        Debug.Log("LAB MENU PASS: original New Game/stand-up transition, title-screen notebook guard, session-only save, walking notebook and original free-pointer regression.");
    }

    [MenuItem("Senzora/Lab Migration/Validate Play Flow")]
    public static void ValidatePlayFlow()
    {
        Check(Application.isPlaying && ResearchPuzzleTrial.Active != null && SecondTraceExperiment.Active != null,
            "Research and measurement systems must both initialize in the copied scene.");
        ResearchPuzzleTrial.Active.StartCoroutine(PlayFlow());
    }

    private static IEnumerator PlayFlow()
    {
        Application.runInBackground = true;
        var game = GameManager.Instance;
        var trial = ResearchPuzzleTrial.Active;
        var notebook = NotebookUI.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        bool started = game.isGameStarted;
        Check(game.activeInteraction == null, "Exit current interaction first.");
        workspace.Close();
        game.isGameStarted = false;
        game.UpdateCursorState();
        workspace.ToggleStandaloneNotebook();
        Check(!workspace.IsActive && Cursor.visible, "Title-screen Tab captured the pointer.");
        Check(SaveManager.Instance != null && SaveManager.Instance.sessionOnly && !SaveManager.Instance.HasSaveFile(),
            "Migration copy can read or overwrite the original save.");
        if (started) game.StartGameMode();
        else
        {
            UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().PlayGame();
            float startDeadline = Time.realtimeSinceStartup + 90;
            while (!game.isGameStarted && Time.realtimeSinceStartup < startDeadline) yield return null;
            Check(game.isGameStarted, "Laboratory New Game did not start.");
            yield return new WaitForSeconds(2);
        }
        foreach (var book in trial.books)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<StarterAssets.CharacterController>();
            var inputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
            float animationSpeed = player.GetComponent<Animator>().speed;
            // Reproduce opening while the body is still catching up to the view direction.
            Quaternion viewRotation = player.CinemachineCameraTarget.transform.rotation;
            player.transform.rotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y - 25, 0);
            player.CinemachineCameraTarget.transform.rotation = viewRotation;
            Quaternion bodyRotation = player.transform.rotation;
            book.Interact();
            Check(player.enabled && player.GetComponent<UnityEngine.CharacterController>().enabled
                && player.GetComponent<Animator>().enabled && !inputs.cursorInputForLook,
                "Book entry restarts player/camera components instead of freezing input.");
            float deadline = Time.realtimeSinceStartup + 12;
            while (book.isAnimating && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                CheckBookPose(player, bodyRotation, viewRotation);
            }
            Check(!book.isAnimating && workspace.IsActive && Cursor.visible, "Book entry did not finish.");
            var brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
            // The authored Perlin breathing noise deliberately moves the final view; isolate the underlying aim.
            Quaternion cameraAim = brain.CurrentCameraState.RawOrientation;
            float holdUntil = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < holdUntil)
            {
                inputs.look = new Vector2(20, 20);
                yield return null;
                CheckBookPose(player, bodyRotation, viewRotation);
                Check(Quaternion.Angle(cameraAim, brain.CurrentCameraState.RawOrientation) < .1f,
                    "Camera aim drifts while the book is open.");
            }
            inputs.look = Vector2.zero;
            Debug.Log("LAB BOOK POSE PASS: body, camera target and camera aim remain still while opening/reading " + book.name);
            for (int spread = 0; spread < book.totalPages / 2; spread++)
            {
                yield return null; // Physical page cards rebuild in LateUpdate after the animation ends.
                if (book.printNotesOnPages) CheckPrintedInput(book);
                yield return null; // Archive note render depth and masks settle after its drag preview is destroyed.
                CheckArchivedInput();
                ResearchTrialBookChecks.CollectVisible();
                ResearchTrialBookChecks.CheckReadability();
                if (spread + 1 >= book.totalPages / 2) break;
                book.NextPage();
                yield return null;
                while (book.isAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            }
            inputs.look = new Vector2(20, 20);
            book.ForceExit();
            Check(Mathf.Approximately(player.GetComponent<Animator>().speed, animationSpeed),
                "Book close left the character animation paused.");
            Check(inputs.cursorInputForLook && inputs.look == Vector2.zero && player.enabled
                && !(bool)typeof(StarterAssets.CharacterController).GetField("_isFrozen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player),
                "Book close waits for its animation before restoring clean player input.");
            deadline = Time.realtimeSinceStartup + 12;
            while ((book.isAnimating || game.activeInteraction != null) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.activeInteraction == null, "Book exit did not restore gameplay.");
        }
        Debug.Log("LAB BOOKS PASS: four real laboratory books, page turns, readable physical notes and notebook collection.");
        var boardInteraction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        boardInteraction.Interact();
        float end = Time.realtimeSinceStartup + 8;
        while (!trial.Board.isMachineActive && Time.realtimeSinceStartup < end) yield return null;
        Check(trial.Board.isMachineActive && Cursor.visible, "Original board entry failed.");
        yield return null;
        CheckBoardPointer();
        trial.Board.ResetBoard();
        SignalTrialSetup.ValidateArchive();
        SignalTrialSetup.ValidateLive();
        SecondTracePlayChecks.CollectOutput();
        SignalTrialSetup.ValidateSolutions();
        boardInteraction.ForceExit();
        end = Time.realtimeSinceStartup + 8;
        while (game.activeInteraction != null && Time.realtimeSinceStartup < end) yield return null;
        Check(game.activeInteraction == null, "Original board exit failed.");
        var terminal = SecondTraceExperiment.Active.terminal;
        workspace.ToggleStandaloneNotebook();
        terminal.Interact();
        Check(!workspace.IsActive && !notebook.isNotebookOpen, "Notebook remained open while approaching Turing.");
        yield return new WaitForSeconds(3.5f);
        Check(game.activeInteraction == terminal && workspace.IsActive && terminal.RequiresMeasurementCursor,
            "Original Turing entry did not open the measurement reader.");
        Check(Vector3.Distance(UnityEngine.Object.FindFirstObjectByType<UnityEngine.CharacterController>().transform.position, terminal.interactionStandPoint.position) < .01f,
            "Turing sitting position no longer matches the authored stand point.");
        game.isGamePaused = true; game.UpdateCursorState();
        game.isGamePaused = false; game.UpdateCursorState();
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Resuming Turing lost its reader pointer.");
        Debug.Log("LAB TURING ENTRY PASS: real character approach/sit pose, authored camera, measurement workspace and pause cursor restoration.");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        workspace.ShowSolutions(); Canvas.ForceUpdateCanvases();
        var content = (RectTransform)typeof(NotebookWorkspaceController).GetField("informationContent", flags).GetValue(workspace);
        var note = content.GetComponentsInChildren<NotebookDraggableBlock>().First();
        var slot = (Transform)typeof(InteractableTuringMachine).GetField("measurementReader", flags).GetValue(terminal);
        Vector2 point = Camera.main.WorldToScreenPoint(slot.position);
        Check(point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height, "Original Turing camera cannot see the reader.");
        Check(SecondTraceExperiment.Active.TryGetMeasurementCode(note.Data, out string wanted), "Measurement code missing.");
        Check(workspace.TryDropBlock(note, point) && terminal.IsReadingMeasurement, "Measurement cannot be dropped into the original Turing reader.");
        end = Time.realtimeSinceStartup + 35;
        while (terminal.IsReadingMeasurement && Time.realtimeSinceStartup < end) yield return null;
        Check(!terminal.IsReadingMeasurement, "Turing reader did not finish.");
        string letters = (string)typeof(InteractableTuringMachine).GetField("wordChars", flags).GetValue(terminal);
        string digits = (string)typeof(InteractableTuringMachine).GetField("numberChars", flags).GetValue(terminal);
        var symbols = (string[])typeof(InteractableTuringMachine).GetField("symbolChars", flags).GetValue(terminal);
        string code = new string(((int[])typeof(InteractableTuringMachine).GetField("wordWheelIndices", flags).GetValue(terminal)).Select(i => letters[i]).ToArray())
            + "_" + symbols[(int)typeof(InteractableTuringMachine).GetMethod("GetCorrectSymbolIndex", flags).Invoke(terminal, null)] + "_"
            + new string(((int[])typeof(InteractableTuringMachine).GetField("numberWheelIndices", flags).GetValue(terminal)).Select(i => digits[i]).ToArray());
        Check(code == wanted, "Turing reader produced a different code: " + code + " / " + wanted);
        Check(!trial.Completed.Contains(SecondTraceExperiment.Active.CurrentQuestID), "Reading automatically completed the experiment.");
        Debug.Log("LAB TURING READER PASS: original camera sees the slot; notebook drop feeds the physical card and enters the measured code.");
        terminal.ForceExit();
        end = Time.realtimeSinceStartup + 8;
        while (game.activeInteraction != null && Time.realtimeSinceStartup < end) yield return null;
        Check(game.activeInteraction == null && !workspace.IsActive && !notebook.notebookPanel.activeSelf && !Cursor.visible
            && Cursor.lockState == CursorLockMode.Locked,
            "Turing exit left a notebook or interaction active.");
        ValidateNotebook();
        workspace.ToggleStandaloneNotebook();
        yield return new WaitForSeconds(.4f);
        ScreenCapture.CaptureScreenshot("Temp/lab-notebook.png");
        Debug.Log("LAB PLAY FLOW PASS: books -> notebook -> original board -> measurement -> original Turing -> gameplay. Preview: Temp/lab-notebook.png");
    }

    private static void CheckBookPose(StarterAssets.CharacterController player, Quaternion body, Quaternion view)
    {
        float bodyDrift = Quaternion.Angle(body, player.transform.rotation);
        float viewDrift = Quaternion.Angle(view, player.CinemachineCameraTarget.transform.rotation);
        Check(bodyDrift < .1f && viewDrift < .1f,
            $"Book interaction rotates the player/view without input: body={bodyDrift:F2}, view={viewDrift:F2} degrees.");
    }

    [MenuItem("Senzora/Lab Migration/Validate Scene Copy")]
    public static void ValidateScene()
    {
        var scene = SceneManager.GetActiveScene();
        Check(scene.path == LaboratorySynthesisSetup.ScenePath, "Open the laboratory synthesis copy.");
        var original = EditorSceneManager.OpenPreviewScene(LaboratorySynthesisSetup.SourcePath);
        try
        {
            foreach (var source in LaboratorySynthesisSetup.All<Transform>(original))
            {
                if (source.GetComponent<InteractableTuringMachine>() == null && source.GetComponent<InteractableChalkboard>() == null
                    && source.GetComponent<StarterAssets.CharacterController>() == null) continue;
                var copy = LaboratorySynthesisSetup.All<Transform>(scene).First(t => PathOf(t) == PathOf(source));
                Check(source.position == copy.position && source.rotation == copy.rotation && source.lossyScale == copy.lossyScale,
                    "Authored player or machine transform changed: " + PathOf(source));
                foreach (var sourceChild in source.GetComponentsInChildren<Transform>(true))
                {
                    if (sourceChild.name == "SynthesisButton") continue; // Removed redundant placeholder per user request.
                    var copyChild = copy.GetComponentsInChildren<Transform>(true).First(t => PathOf(t) == PathOf(sourceChild));
                    Check(sourceChild.localPosition == copyChild.localPosition && sourceChild.localRotation == copyChild.localRotation,
                        "Machine or player child pose changed: " + PathOf(sourceChild));
                }
                foreach (var sourceComponent in source.GetComponents<MonoBehaviour>().Where(c => c != null))
                {
                    var counterpart = copy.GetComponent(sourceComponent.GetType());
                    var serialized = new SerializedObject(sourceComponent);
                    var other = new SerializedObject(counterpart);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue is not Component sourceRef) continue;
                        if (sourceRef.gameObject.scene != original) continue;
                        var copyRef = other.FindProperty(property.propertyPath)?.objectReferenceValue as Component;
                        Check(copyRef != null && copyRef.gameObject.scene == scene && PathOf(sourceRef.transform) == PathOf(copyRef.transform),
                            "Machine/player scene reference changed: " + property.propertyPath);
                    }
                }
            }
            var trial = LaboratorySynthesisSetup.Find<ResearchPuzzleTrial>(scene);
            Check(trial != null && trial.allowGameplayScene && trial.books.Distinct().Count() == 4, "Four research books missing.");
            Check(PathOf(trial.books[0].transform) == LaboratorySynthesisSetup.DeskBookPath, "Targets are not on the existing desk book.");
            foreach (var book in trial.books)
                Check(book != null && book.gameObject.activeInHierarchy && book.usePhysicalPageBlocks && !book.includeProceduralClues
                    && book.GetComponent<BookPageBlockSurface>() != null,
                    "Research book configuration invalid.");
            Check(LaboratorySynthesisSetup.All<InteractableBook>(scene).All(b => !b.includeProceduralClues),
                "Other shelf books still show shared procedural targets.");
            var board = LaboratorySynthesisSetup.Find<ChalkboardManager>(scene);
            var collider = board.synthesisZone.GetComponent<Collider>();
            Physics.SyncTransforms();
            foreach (var point in new[] { board.minBounds, board.maxBounds,
                new Vector2(board.minBounds.x, board.maxBounds.y), new Vector2(board.maxBounds.x, board.minBounds.y) })
            {
                Vector3 world = board.boardTransform.TransformPoint(point);
                Check((collider.ClosestPoint(world) - world).sqrMagnitude < .000001f, "Expanded board edge is outside the measurement zone.");
            }
            var notebook = LaboratorySynthesisSetup.Find<NotebookUI>(scene);
            Check(notebook.useScreenSpaceNotebook && !notebook.notebookPanel.activeSelf, "Hand notebook is still enabled.");
            var workspace = notebook.GetComponent<NotebookWorkspaceController>();
            Check(workspace != null && !workspace.standaloneUnlocksCursor && !workspace.generateProceduralRun, "Notebook input settings invalid.");
            Check(LaboratorySynthesisSetup.Find<SaveManager>(scene).sessionOnly, "Migration copy can modify the original save.");
            Check(LaboratorySynthesisSetup.Find<SynthesisBlockoutPlayer>(scene) == null
                && LaboratorySynthesisSetup.Find<SynthesisBlockoutScene>(scene) == null, "Blockout player/bootstrap leaked into Lab.");
            Debug.Log("LAB SCENE PASS: original player/machine transforms, child poses and references unchanged; four research books, UI notebook, no blockout bootstrap.");
        }
        finally { EditorSceneManager.ClosePreviewScene(original); }
    }

    [MenuItem("Senzora/Lab Migration/Audit Sources")]
    public static void Audit()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scn_labratory.unity");
        try
        {
            var lines = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.GetComponent<InteractableBook>() != null || t.GetComponent<InteractableTuringMachine>() != null
                    || t.GetComponent<InteractableChalkboard>() != null || t.GetComponent<ChalkboardManager>() != null
                    || t.GetComponent<NotebookUI>() != null || t.name.Contains("Notebook") || t.name.Contains("Defter"))
                .Select(t => PathOf(t) + " | active=" + t.gameObject.activeInHierarchy + " | pos=" + t.position
                    + " | components=" + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
            File.WriteAllLines("Temp/laboratory-audit.txt", lines);
            Debug.Log("LAB AUDIT: Temp/laboratory-audit.txt");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Senzora/Lab Migration/Validate Notebook")]
    public static void ValidateNotebook()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        var workspace = NotebookWorkspaceController.Instance;
        var game = GameManager.Instance;
        var notebook = NotebookUI.Instance;
        if (workspace == null || !workspace.IsArchiveNotebook || game == null)
            throw new InvalidOperationException("Research notebook required.");
        if (workspace.IsStandaloneOpen) workspace.Close();
        game.activeInteraction = null;
        game.StartGameMode();
        float timeScale = Time.timeScale;
        workspace.ToggleStandaloneNotebook();
        game.UpdateCursorState();
        Check(workspace.IsStandaloneOpen && workspace.IsActive, "Notebook did not open.");
        Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked, "Walking notebook unlocked the pointer.");
        Check(!game.isGamePaused && Time.timeScale == timeScale, "Notebook paused the game.");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var window = (RectTransform)typeof(NotebookWorkspaceController).GetField("notebookWindow", flags).GetValue(workspace);
        float startY = window.anchoredPosition.y;
        typeof(NotebookWorkspaceController).GetMethod("UpdateNotebookPopup", flags).Invoke(workspace, new object[] { .3f });
        Check(window.anchoredPosition.y > startY && window.position.x < Screen.width * .5f, "Notebook did not rise at the lower left.");
        notebook.SetNotebookVisibility(true);
        Check(!notebook.notebookPanel.activeSelf, "Animation event reactivated the hand notebook.");
        var field = typeof(NotebookWorkspaceController).GetField("currentNotebookView", flags);
        var before = field.GetValue(workspace);
        workspace.BrowseStandaloneNotebook(1, 0);
        Check(!Equals(before, field.GetValue(workspace)), "E did not change section.");
        workspace.BrowseStandaloneNotebook(-1, 0);
        Check(Equals(before, field.GetValue(workspace)), "Q did not return to the previous section.");
        workspace.Close();
        Check(!workspace.IsActive && !Cursor.visible, "Closing notebook failed to restore gameplay.");
        Debug.Log("LAB NOTEBOOK PASS: live game, locked pointer, Q/E, close.");
    }

    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private static void CheckPrintedInput(InteractableBook book)
    {
        CheckBookMove(book);
        Canvas.ForceUpdateCanvases();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", flags).GetValue(workspace);
        var surface = book.GetComponent<BookPageBlockSurface>();
        var handwriting = (TMPro.TMP_FontAsset)typeof(NotebookWorkspaceController).GetField("notebookHandwritingFont", flags).GetValue(workspace);
        foreach (var source in book.GetComponentsInChildren<NotebookDraggableBlock>())
        {
            if (!source.IsSource || !source.isActiveAndEnabled) continue;
            var print = source.GetComponent<BookPagePrintedGraphic>();
            Check(print != null, "Book source is still a floating card: " + source.Data.title);
            var uv = (Rect)typeof(BookPagePrintedGraphic).GetField("RegionUV", flags).GetValue(print);
            bool right = (bool)typeof(BookPagePrintedGraphic).GetField("RightPage", flags).GetValue(print);
            var world = (Vector3)typeof(BookPageBlockSurface).GetMethod("MapPageWorld", flags)
                .Invoke(surface, new object[] { uv.center, right });
            var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(world) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Debug.Log("LAB INK HIT " + source.Data.title + " at " + pointer.position + " rect="
                + Camera.main.WorldToScreenPoint(source.transform.position) + " hits="
                + string.Join(",", hits.Select(h => PathOf(h.gameObject.transform))));
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<NotebookDraggableBlock>() == source,
                "Printed note is not reachable by the pointer: " + source.Data.title);
            source.OnBeginDrag(pointer);
            var floating = (RectTransform)typeof(NotebookDraggableBlock).GetField("floatingRect", flags).GetValue(source);
            Check(floating != null && floating.GetComponent<UnityEngine.UI.Image>().color.a > .95f,
                "Dragged book note is loose ink instead of opaque paper.");
            Canvas.ForceUpdateCanvases();
            foreach (var text in floating.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Drag preview text is clipped: " + source.Data.title);
                Check(text.font == handwriting, "Human-copied note is still typeset: " + source.Data.title);
            }
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, page.position);
            source.OnDrag(pointer); source.OnEndDrag(pointer);
            bool stored = source.Data.blockType == NotebookBlockType.Quest
                ? NotebookUI.Instance.collectedQuests.Exists(n => n.questID == source.Data.questID)
                : NotebookUI.Instance.workspacePages.Exists(p => p.blocks.Exists(n => n.title == source.Data.title));
            Check(stored, "Dragging printed ink did not archive its note: " + source.Data.title);
        }
        Debug.Log("LAB PRINTED INPUT PASS: visible book ink raycasts and actual drag handlers archive the original notes.");
    }

    private static void CheckArchivedInput()
    {
        var workspace = NotebookWorkspaceController.Instance;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(NotebookWorkspaceController).GetMethod("ResetNotebookPageTurn", flags).Invoke(workspace, null);
        Canvas.ForceUpdateCanvases();
        var notes = workspace.DragCanvas.GetComponentsInChildren<NotebookDraggableBlock>()
            .Where(n => n.isActiveAndEnabled && !n.IsSource && n.Data != null).ToArray();
        Check(notes.Length > 0, "The copied handwritten notes are not visible.");
        foreach (var archived in notes)
        {
            var rect = (RectTransform)archived.transform;
            var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<NotebookDraggableBlock>() == archived,
                "Rendered handwritten note cannot be grabbed: " + archived.Data.title + " hits="
                + string.Join(",", hits.Select(h => h.gameObject.name)));
        }
        Debug.Log("LAB HANDWRITTEN INPUT PASS: rendered archive notes retain their real drag hit areas.");
    }

    private static void CheckBookMove(InteractableBook book)
    {
        Check(book.GetComponentsInChildren<BookPageFreehandDrawer>().Length == 0, "Book still accepts drawing.");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var surface = book.GetComponent<BookPageBlockSurface>();
        var world = (Vector3)typeof(BookPageBlockSurface).GetMethod("MapPageWorld", flags)
            .Invoke(surface, new object[] { new Vector2(.5f, .025f), false });
        var camera = Camera.main;
        var pointer = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(world), delta = new Vector2(75, 25) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0, "Empty book page cannot be grabbed.");
        var handle = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
        Check(handle != null && handle.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name == "BookPageMoveHandle"),
            "Empty page still intercepts book movement.");
        Vector2 before = camera.WorldToScreenPoint(book.transform.position);
        ExecuteEvents.Execute(handle, pointer, ExecuteEvents.dragHandler);
        Vector2 moved = (Vector2)camera.WorldToScreenPoint(book.transform.position) - before;
        Check(Vector2.Distance(moved, pointer.delta) < 1, "Book does not follow its grab movement.");
        pointer.delta *= -1; ExecuteEvents.Execute(handle, pointer, ExecuteEvents.dragHandler);
    }

    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
