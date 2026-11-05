using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TutorialPlayChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private enum Variant { Hide, Peek, Unlock, ExitDeath }
    private static Variant variant;

    [MenuItem("Senzora/Doğrulama/Tutorial Gerçek Oyun Akışı")]
    public static void Run() => Start(Variant.Hide);

    // Same flow, but the player peeks too long in the wardrobe, is marked, and dies on leaving it.
    [MenuItem("Senzora/Doğrulama/Tutorial Gerçek Oyun Akışı - Guderian'a Yakalan")]
    public static void RunCaught() => Start(Variant.Peek);

    // Same flow, but the player unlocks the door again and stays out in the room.
    [MenuItem("Senzora/Doğrulama/Tutorial Gerçek Oyun Akışı - Kilidi Açıp Yakalan")]
    public static void RunUnlockDeath() => Start(Variant.Unlock);

    // Same flow up to the wardrobe: leaving it while Guderian searches dies and rewinds into it. Stops there.
    [MenuItem("Senzora/Doğrulama/Tutorial Gerçek Oyun Akışı - Dolaptan Çıkıp Yakalan")]
    public static void RunExitDeath() => Start(Variant.ExitDeath);

    private static void Start(Variant chosen)
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Laboratuvarda Play gerekli.");
        variant = chosen;
        SaveManager.Instance.sessionOnly = true; // the real New Game path and the quit autosave must not touch the user's save
        // A run stopped mid-click leaves a temporary input settings object behind; start from the project asset.
        if (EditorBuildSettings.TryGetConfigObject("com.unity.input.settings", out InputSettings projectInput)) InputSystem.settings = projectInput;
        ResearchPuzzleTrial.Active.StartCoroutine(Flow());
    }

    private static IEnumerator Flow()
    {
        yield return ResearchGuidanceChecks.EnterNormalGame();
        var manager = TutorialManager.Instance;
        var lesson = manager.tutorialEventsParent.GetComponent<ChalkboardTutorial>();
        var trial = lesson.research; var game = GameManager.Instance;
        var board = trial.Board; var workspace = NotebookWorkspaceController.Instance;
        var experiment = SignalExperiment.Active;
        Require(manager.IsRunning && trial.Puzzles.Count(p => p.id == ChalkboardTutorial.QuestID) == 1, "Ders oturumda bir kez kurulmadı.");
        yield return WaitFor(() => lesson.firstStep.HasTriggered, "Başlangıç tetikleyicisi");
        Require(!lesson.collectedStep.HasTriggered && !lesson.boardStep.HasTriggered, "Boş arşiv dersi atladı.");
        float pressure = PressureSystemManager.Instance.GetPressure();
        yield return new WaitForSeconds(.5f);
        Require(Mathf.Approximately(pressure, PressureSystemManager.Instance.GetPressure()), "Eğitimde otomatik basınç artıyor.");
        RequireAllowed(lesson.lessonBook.gameObject);
        // 3 Nov playtest: the camera turns to the book, and a click while it holds there opens it at once.
        yield return WaitFor(() => PlayerInteraction.Instance.IsInteractionBlocked, "Kameranın kitaba dönmeye başlaması");
        yield return new WaitForSeconds(1.7f);
        Require(PlayerInteraction.Instance.IsInteractionBlocked, "Kamera kitaba varmadan bıraktı; bekleme evresi test edilemedi.");
        yield return ClickLeftMouse();
        Require(game.activeInteraction == lesson.lessonBook, "Kamera kitaba bakarken tıklama kitabı açmadı.");
        Debug.Log("TUTORIAL PLAY STAGE PASS: a click during the camera hold opens the book");
        yield return WaitFor(() => lesson.lessonBook.isOpen && !lesson.lessonBook.isAnimating && workspace.IsActive, "Eğitim kitabı");
        yield return null;
        Require(lesson.lessonBook.ExitLocked, "Kartlar toplanmadan kitaptan çıkılabiliyor.");
        yield return WaitFor(() => Field<RectTransform>(workspace, "ghost") != null, "Sürükleme hayaleti");
        workspace.ToggleNotebook();
        Require(Field<bool>(workspace, "notebookExpanded"), "Kilitli kitapta defter, kartların tek bırakma alanı kapatılabiliyor.");
        ResearchTrialBookChecks.CheckReadability();
        ScreenCapture.CaptureScreenshot("Temp/tutorial-book.png");
        ResearchTrialBookChecks.CollectVisible();
        yield return WaitFor(() => lesson.collectedStep.HasTriggered, "Gerçek kartların toplanması");
        Require(ChalkboardTutorial.HasCollectedCards(NotebookUI.Instance), "Dört masa kartı (hedef, bağlantı, kaynak, ölçer) arşivlenmedi.");
        yield return WaitFor(() => !lesson.lessonBook.ExitLocked && Field<RectTransform>(workspace, "ghost") == null, "Kartlar toplanınca kitap kilidinin ve hayaletin kalkması");
        lesson.lessonBook.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Kitaptan çıkış");
        Debug.Log("TUTORIAL PLAY STAGE PASS: book collection");

        // 5 Nov: N-12 is in a library book; the board opens only once it is in the notebook.
        var sampleBook = lesson.sampleBook; var chalkboard = Object.FindFirstObjectByType<InteractableChalkboard>();
        Require(!ChalkboardTutorial.HasSampleCard(NotebookUI.Instance) && !lesson.sampleStep.HasTriggered, "Numune masa kitabından toplandı.");
        foreach (string id in PuzzleBookDistribution.DeskParts)
            Require(lesson.lessonBook.synthesisPages.SelectMany(p => p.blocks).Count(n => n.blockType == NotebookBlockType.PartClue && n.itemID == id) == 1, "Masa kitabında aygıt kartı yok veya iki kez var: " + id);
        Require(!Field<HashSet<GameObject>>(PlayerInteraction.Instance, "allowedTutorialSet").Contains(chalkboard.gameObject), "Numune alınmadan tahta açılabiliyor.");
        RequireAllowed(sampleBook.gameObject);
        Require(sampleBook.emissionIntensity > 0, "Numune kitabı yanıp sönmüyor (ışık kapalı).");
        yield return WaitFor(() => GlobalSubtitleManager.Instance.CurrentID == "thought_sample_0", "Kütüphane kitabı altyazısı", 15);
        var partBook = typeof(NotebookWorkspaceController).GetMethod("PartBook", BindingFlags.Static | BindingFlags.NonPublic);
        string sampleWhere = (string)partBook.Invoke(null, new object[] { SignalTrial.Narrow, ChalkboardTutorial.QuestID });
        string deviceWhere = (string)partBook.Invoke(null, new object[] { SignalTrial.Meter, ChalkboardTutorial.QuestID });
        Require(sampleWhere != null && sampleWhere.StartsWith(ChalkboardTutorial.SampleBookTitle), "Defter numunenin yerini göstermiyor: " + sampleWhere);
        Require(deviceWhere != null && deviceWhere.StartsWith(lesson.lessonBook.GetComponent<PuzzleBookIdentity>().title), "Defter ölçerin yerini masa kitabı göstermiyor: " + deviceWhere);
        yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Numune kitabından önce kamera");
        sampleBook.Interact();
        yield return WaitFor(() => sampleBook.isOpen && !sampleBook.isAnimating && workspace.IsActive, "Numune kitabı");
        yield return null;
        workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases(); yield return null;
        ScreenCapture.CaptureScreenshot("Temp/tutorial-sample-book.png"); yield return null; yield return null;
        Require(workspace.ShowCollectedPart(SignalTrial.Source), "Defter Parçalar sayfasına dönmedi."); Canvas.ForceUpdateCanvases(); yield return null;
        ResearchTrialBookChecks.CollectVisible();
        yield return WaitFor(() => lesson.sampleStep.HasTriggered, "Numune N-12'nin toplanması");
        Require(sampleBook.emissionIntensity == 0, "Numune alındıktan sonra kitap ışığı söndürülmedi.");
        sampleBook.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Numune kitabından çıkış");
        Debug.Log("TUTORIAL PLAY STAGE PASS: library sample book (" + sampleWhere + ", devices: " + deviceWhere + ")");

        var interaction = Object.FindFirstObjectByType<InteractableChalkboard>();
        RequireAllowed(interaction.gameObject);
        interaction.Interact(); yield return WaitFor(() => board.isMachineActive && workspace.IsActive, "Tahtaya giriş"); yield return null;
        var docs = board.EnsureDocuments();
        var handle = Field<RectTransform>(docs, "targetArea");
        // Machine mode opens before the Cinemachine blend has necessarily settled.
        yield return WaitFor(() =>
        {
            var point = Camera.main.WorldToScreenPoint(handle.position);
            return point.z > 0 && point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height
                && !Camera.main.GetComponent<Cinemachine.CinemachineBrain>().IsBlending;
        }, "Tahta kamera geçişi");
        Vector2 handlePoint = Camera.main.WorldToScreenPoint(handle.position);
        Require(handlePoint.x > 0 && handlePoint.x < Screen.width && handlePoint.y > 0 && handlePoint.y < Screen.height, "Pano tutamağı kamerada görünmüyor.");
        yield return WaitFor(() => Field<RectTransform>(workspace, "ghost") != null, "Tahtada hedef rehberi");
        workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases();
        var target = Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
            .First(n => !n.IsSource && n.isActiveAndEnabled && n.Data.questID == ChalkboardTutorial.QuestID && n.Data.blockType == NotebookBlockType.Quest);
        DropAwayFromNotebook(workspace, target, handlePoint);
        workspace.ShowInformation(); Canvas.ForceUpdateCanvases();
        var clue = Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
            .First(n => !n.IsSource && n.isActiveAndEnabled && n.Data.itemID == "tutorial/ports");
        DropAwayFromNotebook(workspace, clue, handlePoint);
        Require(docs.IsDocumentPanelOpen && !lesson.boardStep.HasTriggered, "Kart sabitlemek tahta dersini atladı.");
        // Confirm the visible handle wins the actual UI raycast before invoking its button.
        var window = Field<RectTransform>(workspace, "notebookWindow");
        Vector2 oldWindow = window.anchoredPosition; window.anchoredPosition += Vector2.down * 1000; Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = handlePoint }, hits);
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(handle), "Pano kapatma kontrolü tıklanamıyor.");
        handle.GetComponent<Button>().onClick.Invoke(); window.anchoredPosition = oldWindow;
        yield return new WaitForSeconds(.35f);
        Require(!docs.IsDocumentPanelOpen && !Field<RectTransform>(docs, "shadeViewport").gameObject.activeInHierarchy, "Pano kapatılınca gövde/girdi kaldı.");
        var parts = new List<DraggableFormula>();
        for (int i = 0; i < 3; i++)
        {
            string id = new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter }[i];
            Require(workspace.ShowCollectedPart(id), "Toplanan parça bulunamadı: " + id); Canvas.ForceUpdateCanvases();
            var note = Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
                .First(n => !n.IsSource && n.isActiveAndEnabled && n.Data.itemID == id);
            Vector2 local = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(.76f - i * .25f, .62f)); // ChalkboardTutorial.GuideCircuit yuvaları
            Vector2 pointer = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(local.x, local.y, .02f)));
            var before = new HashSet<DraggableFormula>(board.boardTransform.GetComponentsInChildren<DraggableFormula>());
            DropAwayFromNotebook(workspace, note, pointer);
            parts.Add(board.boardTransform.GetComponentsInChildren<DraggableFormula>().Single(p => !before.Contains(p)));
        }
        board.AttemptSynthesis(); Require(!experiment.LastReading.MeetsTarget && !lesson.boardStep.HasTriggered, "Bağlanmamış parçalar doğru ölçüm sayıldı.");
        yield return null;
        yield return WaitFor(() => Field<RectTransform>(workspace, "ghostLine") != null, "Parçalar arası bağ rehberi");
        for (int i = 0; i < 2; i++)
        {
            var output = parts[i].GetOwnNodes().First(n => n.gameObject.activeSelf && n.GetComponent<SignalPort>().output);
            var input = parts[i + 1].GetOwnNodes().First(n => n.gameObject.activeSelf && !n.GetComponent<SignalPort>().output);
            output.ConnectTo(input); Require(output.connectedNode == input, "Gerçek yönlü portlar bağlanamadı.");
        }
        var loose = board.SpawnFormula(FormulaCatalog.Instance.ResolveFormula(SignalTrial.Narrow),
            Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(.5f, .3f)))));
        Require(loose != null, "Boşta durması gereken parça tahtaya konamadı.");
        board.AttemptSynthesis(); Require(experiment.LastReading.MeetsTarget, "Boştaki parça ölçümü bozdu veya tahta başlangıç hedefini üretemedi.");
        board.RemoveFormula(loose);
        Require(!lesson.boardStep.HasTriggered, "Ölçüm deftere alınmadan tahta dersi geçildi.");
        yield return null;
        yield return WaitFor(() => Field<NotebookDraggableBlock>(workspace, "ghostCard") == experiment.OutputCard, "Ölçüm kartı rehberi");
        SignalTrialPlayChecks.CollectOutput();
        workspace.ToggleNotebook(); yield return new WaitForSeconds(.35f);
        ScreenCapture.CaptureScreenshot("Temp/tutorial-board.png");
        interaction.ForceExit(); yield return WaitFor(() => game.activeInteraction == null && lesson.boardStep.HasTriggered, "Tahta dersi çıkışı");
        Debug.Log("TUTORIAL PLAY STAGE PASS: real board, close button, ports, measurement and archived output");

        var doorLock = lesson.officeDoor.GetComponentInChildren<InteractableDoorLock>();
        yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Kapıya odaklanmanın bitmesi");
        RequireAllowed(doorLock.gameObject); doorLock.Interact();
        yield return WaitFor(() => lesson.doorStep.HasTriggered, "Gerçek kapı kilidi");
        var spot = manager.tutorialEventsParent.GetComponentsInChildren<HidingSpotControlAction>().First().hidingSpot;
        yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Saklanmadan önce kamera odağı");
        // 3 Nov playtest: the jumpscare still plays, then the lesson rewinds without the death screen.
        var volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        volume.profile.TryGet(out UnityEngine.Rendering.HighDefinition.Vignette vignette);
        float vignetteBefore = vignette != null ? vignette.intensity.value : 0;
        Func<bool> rewoundCleanly = () => Time.timeScale == 1 && !Field<GameObject>(DeathUIManager.Instance, "deathScreenPanel").activeSelf
            && (vignette == null || Mathf.Approximately(vignette.intensity.value, vignetteBefore));
        var guderian = GuderianAI.Instance;
        if (variant == Variant.Unlock)
        {
            // 4 Nov: the handle stays usable; unlocked and out in the open, he comes in and the lesson rewinds to the lock.
            var mover = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
            var player = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
            Vector3 lockPosition = Field<Vector3>(manager, "lockPosition");
            Require(Vector3.Distance(player.transform.position, lockPosition) < .3f, "Kilitleme anı kaydedilmedi.");
            RequireAllowed(doorLock.gameObject); doorLock.Interact();
            // Shut pose of every animated door part, to prove the rewind snaps it back without the swing.
            var doorParts = Field<Animator>(lesson.officeDoor, "doorAnimator").GetComponentsInChildren<Transform>();
            var shutPose = doorParts.Select(t => t.localRotation).ToArray();
            Func<float> doorSwing = () => doorParts.Select((t, i) => Quaternion.Angle(t.localRotation, shutPose[i])).Max();
            Require(!lesson.officeDoor.IsLocked(), "Kapı kilidi geri açılamadı.");
            // Facing the door makes him wait behind it; turned away, he comes in and catches the player from behind.
            Vector3 away = mover.transform.position - lesson.officeDoor.transform.position; away.y = 0;
            float awayYaw = Quaternion.LookRotation(away).eulerAngles.y;
            float caughtAt = Time.realtimeSinceStartup;
            yield return WaitFor(() => { if (!game.IsGameplayOver) mover.ForceCameraRotation(awayYaw, 0); return game.IsGameplayOver; }, "Guderian'ın yakalaması", 100);
            Debug.Log($"TUTORIAL PLAY TIMING: unlocked -> caught in {Time.realtimeSinceStartup - caughtAt:F1}s, door swung {doorSwing():F0} deg"); caughtAt = Time.realtimeSinceStartup;
            float swingBeforeRewind = 0;
            yield return WaitFor(() => { if (game.IsGameplayOver) swingBeforeRewind = doorSwing(); return !game.IsGameplayOver; }, "Ölümden sonra geri sarma", 30);
            Debug.Log($"TUTORIAL PLAY TIMING: caught -> rewound in {Time.realtimeSinceStartup - caughtAt:F1}s");
            float swingAtRewind = doorSwing();
            Require(swingAtRewind < 1f && lesson.officeDoor.IsLocked() && !lesson.officeDoor.isOpen, $"Geri sarma karesinde kapı kapalı değil ({swingAtRewind:F1} derece açık).");
            Debug.Log($"TUTORIAL PLAY STAGE PASS: the door is shut on the rewind frame ({swingBeforeRewind:F0} deg open the frame before, {swingAtRewind:F2} deg after)");
            yield return new WaitForSeconds(.5f);
            Require(rewoundCleanly(), "Eğitim ölümü ölüm ekranını açtı veya jumpscare efektleri geri alınmadı.");
            float distance = Vector3.Distance(player.transform.position, lockPosition);
            Require(distance < .3f && player.enabled && !spot.IsOccupied && game.activeInteraction == null, $"Geri sarma oyuncuyu kilitleme anına koymadı ({distance:F2} m).");
            Require(lesson.officeDoor.IsLocked() && !lesson.officeDoor.isOpen && doorSwing() < 1f, "Geri sarmada kapı kilitli ve kapalı değil.");
            Require(guderian.currentState == GuderianAI.GuderianState.Approaching, "Guderian yeniden yaklaşmıyor: " + guderian.currentState);
            ScreenCapture.CaptureScreenshot("outputs/playtest-guderian-unlock-rewind.png");
            // Turn to the door for a shot of it shut after the rewind.
            Vector3 toDoor = lesson.officeDoor.GetComponentInChildren<Renderer>().bounds.center - Camera.main.transform.position;
            var doorLook = Quaternion.LookRotation(toDoor).eulerAngles;
            mover.ForceCameraRotation(doorLook.y, doorLook.x);
            yield return new WaitForSeconds(1f);
            ScreenCapture.CaptureScreenshot("outputs/guderian-rewind-2026-11-04/unlock-rewind-door.png");
            Debug.Log("TUTORIAL PLAY STAGE PASS: unlocked and caught in the open rewinds to the lock moment");
            yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Geri sarmadan sonra etkileşim");
        }
        RequireAllowed(spot.gameObject);
        spot.Interact(); BroadcastInteraction(spot.gameObject);
        yield return WaitFor(() => spot.IsOccupied && !Field<bool>(spot, "inTransition"), "Gerçek dolaba giriş");
        if (variant == Variant.Peek)
        {
            // Peeking too long while he is about only marks the player; leaving the wardrobe later is the death.
            yield return WaitFor(() => guderian.currentState == GuderianAI.GuderianState.Searching, "Guderian'ın aramaya başlaması", 40);
            // Lid opening in degrees from its shut pose: it must not swing shut while Peek is held, or when the death starts.
            var lid = Field<Animator>(spot, "propAnimator").GetComponentsInChildren<Transform>();
            var lidShut = lid.Select(t => t.localRotation).ToArray();
            Func<float> lidOpen = () => lid.Select((t, i) => Quaternion.Angle(t.localRotation, lidShut[i])).Max();
            for (int round = 0; round < 2; round++)
            {
                // Real Peek bindings through the Input System: left mouse in round 1, W in round 2.
                var mouse = AddGameMouse(); var keyboard = InputSystem.AddDevice<Keyboard>();
                try
                {
                    if (round == 0) InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                    else { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); }
                    // Round 1 holds 6.5 s, past the 2 s mark, sampling the lid every frame.
                    float heldFor = 0, peak = 0;
                    while (heldFor < (round == 0 ? 6.5f : 2.5f))
                    {
                        yield return null; heldFor += Time.deltaTime;
                        if (round != 0) continue;
                        float open = lidOpen(); peak = Mathf.Max(peak, open);
                        Require(heldFor < 1.5f || open >= peak - 2f, $"Gözetleme basılıyken kapak kapandı ({open:F0}/{peak:F0} derece, {heldFor:F1} s).");
                        if (heldFor >= 6f && peak > 0) { ScreenCapture.CaptureScreenshot("outputs/guderian-rewind-2026-11-04/peek-6s.png"); Debug.Log($"TUTORIAL PLAY STAGE PASS: the lid stays open through 6 s of held peek ({open:F0} deg)"); peak = -1; }
                    }
                }
                finally { InputSystem.RemoveDevice(keyboard); RemoveGameMouse(mouse); }
                yield return null;
                Require(!game.IsGameplayOver && spot.IsOccupied && Field<bool>(spot, "isMarkedForDeath"),
                    $"Fazla gözetleme (tur {round + 1}) oyuncuyu işaretlemedi veya hemen öldürdü.");
            }
            Require(guderian.currentState == GuderianAI.GuderianState.Searching, "Gözetleme aramayı bozdu.");
            Debug.Log("TUTORIAL PLAY STAGE PASS: peeking too long marks the player without killing");
            float searchAt = Time.realtimeSinceStartup;
            yield return WaitFor(() => guderian.currentState == GuderianAI.GuderianState.Hidden, "Guderian'ın gitmesi", 100);
            Debug.Log($"TUTORIAL PLAY TIMING: marked -> Guderian gone in {Time.realtimeSinceStartup - searchAt:F1}s");
            Require(Field<bool>(spot, "isMarkedForDeath"), "Guderian gidince gözetleme işareti kalktı.");
            // Real exit input: F (Cancel), pressed while peeking (W) so the lid is open when the death starts.
            float exitAt = Time.realtimeSinceStartup;
            var exitMouse = AddGameMouse(); var exitKeys = InputSystem.AddDevice<Keyboard>(); exitKeys.MakeCurrent();
            try
            {
                InputSystem.QueueStateEvent(exitKeys, new KeyboardState(Key.W));
                float peekAt = Time.realtimeSinceStartup;
                yield return WaitFor(() => lidOpen() > 20f, "Çıkıştan önce gözetleme kapağının açılması", 5);
                Debug.Log($"TUTORIAL PLAY TIMING: peek lid open in {Time.realtimeSinceStartup - peekAt:F1}s");
                float lastLid = lidOpen();
                exitAt = Time.realtimeSinceStartup;
                InputSystem.QueueStateEvent(exitKeys, new KeyboardState(Key.W, Key.F));
                float deadline = Time.realtimeSinceStartup + 5;
                while (!game.IsGameplayOver && Time.realtimeSinceStartup < deadline) yield return null;
                ScreenCapture.CaptureScreenshot("outputs/guderian-rewind-2026-11-04/death-frame0.png");
                var deathLid = new List<float>();
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return null;
                    float open = lidOpen(); deathLid.Add(open);
                    Require(open >= lastLid - 2f, $"Ölüm başında kapak kapandı (kare {frame}: {open:F0} < {lastLid:F0} derece).");
                    lastLid = open;
                }
                ScreenCapture.CaptureScreenshot("outputs/guderian-rewind-2026-11-04/death-frame3.png");
                yield return new WaitForSeconds(.4f);
                ScreenCapture.CaptureScreenshot("outputs/guderian-rewind-2026-11-04/death-0.4s.png");
                Debug.Log($"TUTORIAL PLAY STAGE PASS: the peeking lid does not snap shut as the death starts ({string.Join(", ", deathLid.Select(a => a.ToString("F0")))} deg)");
            }
            finally { InputSystem.RemoveDevice(exitKeys); RemoveGameMouse(exitMouse); }
            yield return WaitFor(() => game.IsGameplayOver, "İşaretli oyuncunun dolaptan çıkarken yakalanması", 5);
            Debug.Log($"TUTORIAL PLAY TIMING: exit pressed -> caught in {Time.realtimeSinceStartup - exitAt:F1}s");
            float caughtAt = Time.realtimeSinceStartup;
            yield return WaitFor(() => !game.IsGameplayOver && spot.IsOccupied, "Ölümden sonra dolapta devam", 30);
            Debug.Log($"TUTORIAL PLAY TIMING: caught -> rewound in {Time.realtimeSinceStartup - caughtAt:F1}s");
            yield return new WaitForSeconds(.5f);
            Require(rewoundCleanly(), "Eğitim ölümü ölüm ekranını açtı veya jumpscare efektleri geri alınmadı.");
            Require(game.activeInteraction == spot && guderian.currentState == GuderianAI.GuderianState.Searching,
                "Geri sarma dolabı veya Guderian'ın aramasını kurmadı: " + guderian.currentState);
            Require(lesson.officeDoor.isOpen && !lesson.officeDoor.IsLocked(), "Geri sarmada kapı açık değil.");
            Require(!Field<bool>(spot, "isMarkedForDeath") && Field<float>(spot, "currentPeekTimer") == 0, "Geri sarma gözetleme işaretini/sayacını temizlemedi.");
            ScreenCapture.CaptureScreenshot("outputs/playtest-stage3-death-rewind.png");
            Debug.Log("TUTORIAL PLAY STAGE PASS: a marked exit dies, then rewinds into the wardrobe with Guderian searching");
            searchAt = Time.realtimeSinceStartup;
            yield return WaitFor(() => guderian.currentState == GuderianAI.GuderianState.Hidden, "Geri sarmadan sonra aramanın bitmesi", 100);
            Debug.Log($"TUTORIAL PLAY TIMING: rewound search ended in {Time.realtimeSinceStartup - searchAt:F1}s");
        }
        else if (variant == Variant.ExitDeath)
        {
            // 4 Nov: no exit lock; F while he searches is the death, and the lesson rewinds into the wardrobe.
            yield return WaitFor(() => guderian.currentState == GuderianAI.GuderianState.Searching, "Guderian'ın aramaya başlaması", 40);
            Require(spot.canExit, "Dolap çıkışı hâlâ kilitli.");
            yield return PressKey(Key.F);
            yield return WaitFor(() => game.IsGameplayOver, "Arama sırasında dolaptan çıkarken yakalanma", 5);
            yield return WaitFor(() => !game.IsGameplayOver && spot.IsOccupied, "Ölümden sonra dolapta devam", 30);
            yield return new WaitForSeconds(.5f);
            Require(rewoundCleanly(), "Eğitim ölümü ölüm ekranını açtı veya jumpscare efektleri geri alınmadı.");
            Require(game.activeInteraction == spot && guderian.currentState == GuderianAI.GuderianState.Searching,
                "Geri sarma dolabı veya Guderian'ın aramasını kurmadı: " + guderian.currentState);
            ScreenCapture.CaptureScreenshot("outputs/guderian-rewind-2026-11-04/exit-death-rewind.png");
            Debug.Log("TUTORIAL PLAY STAGE PASS: leaving the wardrobe while he searches dies, then rewinds into it with Guderian searching");
            yield break;
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot("outputs/playtest-stage3-hidden-normal.png");
            yield return WaitFor(() => GuderianAI.Instance.currentState == GuderianAI.GuderianState.Hidden, "Guderian'ın gitmesi", 100);
        }
        Require(GuderianAI.Instance.currentState == GuderianAI.GuderianState.Hidden, "Dolap Guderian gitmeden açıldı.");
        spot.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Dolaptan çıkış");
        yield return WaitFor(() => !spot.IsOccupied && Object.FindFirstObjectByType<UnityEngine.CharacterController>().enabled
            && Camera.main.GetComponent<Cinemachine.CinemachineBrain>().enabled, "Dolap çıkış animasyonu ve oyuncu kontrolü");
        yield return WaitFor(() => LeesEnemyAI.Instance.currentState == LeesEnemyAI.LeesState.Active, "Lees'in görünmesi");
        var enemy = LeesEnemyAI.Instance;
        // Doğma noktası rastgele; raf arkasına düşerse görülemez, oyuncu Lees'in görüş hattına alınır.
        var body = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
        Vector3 leesEyes = enemy.eyesPosition != null ? enemy.eyesPosition.position : enemy.transform.position + Vector3.up * 1.5f;
        Vector3 sightPoint = Enumerable.Range(0, 16).Select(i => Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward * 2.5f + enemy.transform.position)
            .First(p => !Physics.Linecast(new Vector3(p.x, leesEyes.y, p.z), leesEyes, ~LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore)
                && !Physics.CheckSphere(new Vector3(p.x, leesEyes.y, p.z), .45f, ~LayerMask.GetMask("Player", "Enemy"), QueryTriggerInteraction.Ignore));
        sightPoint.y = body.transform.position.y;
        body.enabled = false; body.transform.position = sightPoint; body.enabled = true;
        float sightDeadline = Time.realtimeSinceStartup + 12;
        while (!enemy.HasBeenSpotted && Time.realtimeSinceStartup < sightDeadline)
        {
            Vector3 eyes = enemy.eyesPosition != null ? enemy.eyesPosition.position : enemy.transform.position + Vector3.up * 1.5f;
            var look = Quaternion.LookRotation(eyes - Camera.main.transform.position).eulerAngles;
            Object.FindFirstObjectByType<StarterAssets.CharacterController>().ForceCameraRotation(look.y, look.x > 180 ? look.x - 360 : look.x);
            yield return null;
        }
        if (!enemy.HasBeenSpotted)
        {
            var camera = enemy.playerCamera;
            Vector3 eyes = enemy.eyesPosition != null ? enemy.eyesPosition.position : enemy.transform.position + Vector3.up * 1.5f;
            Vector3 origin = camera.transform.position + camera.transform.forward * .3f;
            Vector3 direction = eyes - origin;
            var visibilityHits = Physics.RaycastAll(origin, direction, direction.magnitude, Field<int>(enemy, "visionLayerMask"), QueryTriggerInteraction.Collide);
            string trace = string.Join("\n", visibilityHits.OrderBy(h => h.distance).Select(h => h.collider.name + " / " + h.collider.transform.root.name
                + " / " + LayerMask.LayerToName(h.collider.gameObject.layer) + " / trigger=" + h.collider.isTrigger + " / distance=" + h.distance));
            var inputs = Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
            System.IO.File.WriteAllText("Temp/tutorial-lees-state.txt", "state=" + enemy.currentState + ", enabled=" + enemy.enabled
                + ", stopAllEnemies=" + GlobalEnemyManager.Instance.stopAllEnemies + ", controllable=" + Object.FindFirstObjectByType<UnityEngine.CharacterController>().enabled
                + ", camera=" + camera.name + ", main=" + Camera.main.name + ", camera position=" + camera.transform.position + ", eyes=" + eyes
                + ", viewport=" + camera.WorldToViewportPoint(eyes) + ", look=" + inputs.look + ", focus blocked=" + PlayerInteraction.Instance.IsInteractionBlocked + "\n" + trace);
            ScreenCapture.CaptureScreenshot("Temp/tutorial-lees-failure.png");
            yield return new WaitForEndOfFrame();
        }
        Require(enemy.HasBeenSpotted, "Gerçek kamera/raycast Lees'i görmedi; Temp/tutorial-lees-state.txt ayrıntılarını kontrol et.");
        yield return ResistWithMouse(enemy, () => lesson.terminalReadyStep.HasTriggered);
        Debug.Log("TUTORIAL PLAY STAGE PASS: lock, hiding and scripted enemies");

        yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Turing kamera odağının bitmesi");
        RequireAllowed(experiment.terminal.gameObject); experiment.terminal.Interact();
        yield return WaitFor(() => game.activeInteraction == experiment.terminal && experiment.terminal.RequiresMeasurementCursor, "Turing'e giriş");
        yield return WaitFor(() => Field<RectTransform>(workspace, "ghost") != null, "Turing okuyucu rehberi");
        Call(experiment.terminal, "CheckPassword");
        yield return null;
        Require(!lesson.terminalAccepted.IsMet() && manager.tutorialEventsParent.GetComponentsInChildren<ChalkboardTutorialCondition>()
            .Any(c => c.context == ChalkboardTutorialCondition.Context.RetryMeasurement && c.IsMet()), "Yanlış kayıt sonrası iç ses aynı onayı tekrar istiyor.");
        workspace.ShowSolutions(); Canvas.ForceUpdateCanvases();
        var measurement = Field<RectTransform>(workspace, "informationContent").GetComponentsInChildren<NotebookDraggableBlock>()
            .First(n => n.Data.questID == ChalkboardTutorial.QuestID);
        var reader = Field<Transform>(experiment.terminal, "measurementReader");
        DropAwayFromNotebook(workspace, measurement, Camera.main.WorldToScreenPoint(reader.position));
        yield return WaitFor(() => !experiment.terminal.IsReadingMeasurement, "Ölçüm okuyucusu", 40);
        Call(experiment.terminal, "CheckPassword");
        yield return WaitFor(() => lesson.terminalAccepted.IsMet() && game.activeInteraction == null, "Turing doğrulaması ve çıkışı");
        Require(manager.IsRunning && PlayerInteraction.Instance.isTutorialMode, "Turing eğitimi erkenden bitirdi.");
        Debug.Log("TUTORIAL PLAY STAGE PASS: real Turing reader and acceptance");

        var movement = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        var closeDoor = Field<EventLogicController>(lesson, "closeDoorStep");
        lesson.officeDoor.SetLocked(false); lesson.officeDoor.SetOpen(true);
        body.enabled = false; body.transform.position = closeDoor.GetComponent<Collider>().bounds.center + Vector3.forward * .55f; body.enabled = true;
        yield return WaitFor(() => closeDoor.HasTriggered && movement.IsFrozen, "Kapıdan ilk çıkışta oyuncunun durması");
        Require(lesson.officeDoor.isOpen, "Kapı dersi kapıyı oyuncudan önce kapattı.");
        yield return ClickLeftMouse();
        yield return WaitFor(() => !lesson.officeDoor.isOpen && !movement.IsFrozen, "Kapı kapanınca oyuncunun serbest kalması");
        Debug.Log("TUTORIAL PLAY STAGE PASS: first door exit stops the player until the door closes");

        var trigger = lesson.valveRoomStep.GetComponent<Collider>();
        body.enabled = false; body.transform.position = trigger.bounds.center; body.enabled = true;
        yield return WaitFor(() => lesson.valveRoomStep.HasTriggered, "Vana odası tetikleyicisi");
        yield return new WaitForSeconds(2);
        var valve = Object.FindFirstObjectByType<InteractablePressureValve>();
        yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Vana kamera odağının bitmesi");
        RequireAllowed(valve.gameObject);
        valve.Interact(); yield return WaitFor(() => game.activeInteraction == valve, "Vanaya giriş");
        yield return new WaitForSeconds(2);
        valve.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Vanadan işlem yapmadan çıkış");
        Require(!lesson.valveStep.HasTriggered && !BreakerBox.Instance.IsTripped, "Vanadan hemen çıkmak basınç dersini atladı.");
        valve.Interact(); yield return WaitFor(() => game.activeInteraction == valve, "Vanaya tekrar giriş"); yield return new WaitForSeconds(2);
        PressureSystemManager.Instance.ReducePressure(70); // Same operation called by the valve's mouse rotation.
        valve.ForceExit(); yield return WaitFor(() => game.activeInteraction == null && lesson.valveStep.HasTriggered, "Gerçek basınç koşulu");
        yield return WaitFor(() => BreakerBox.Instance.IsTripped, "Eğitime özel tek kesinti"); yield return null;
        RequireAllowed(BreakerBox.Instance.gameObject); BreakerBox.Instance.Interact();
        yield return WaitFor(() => !manager.IsRunning, "Şalter sonrası eğitim tamamlanması");
        Require(!PlayerInteraction.Instance.isTutorialMode && BreakerBox.Instance.isSystemActive && PressureSystemManager.Instance.isSystemActive,
            "Eğitim bittiğinde normal oynanış açılmadı.");
        Debug.Log("TUTORIAL REAL FLOW PASS: book → archive → pinned documents → closed panel → physical graph → measured note → lock/hiding/enemies → Turing → valve → single scripted breaker reset → normal play.");
    }

    // Direniş gerçek fare deltasını okur; sanal fare çekişin tersine sürerek oyuncu gibi kazanır.
    private static IEnumerator ResistWithMouse(LeesEnemyAI enemy, Func<bool> finished)
    {
        var mouse = AddGameMouse();
        float end = Time.realtimeSinceStartup + 75;
        while (!finished() && Time.realtimeSinceStartup < end)
        {
            var resistance = enemy.resistance;
            if (resistance.IsRunning && resistance.Model.State == LeesResistanceModel.Phase.Struggling)
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(-resistance.Model.PullDirection * 400 * Time.deltaTime, 0));
            yield return null;
        }
        RemoveGameMouse(mouse);
        Require(finished(), "Lees eğitimi fareyle tamamlanamadı.");
    }

    private static IEnumerator PressKey(Key key)
    {
        var mouse = AddGameMouse(); var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null; yield return null;
        InputSystem.RemoveDevice(keyboard); RemoveGameMouse(mouse);
    }

    private static IEnumerator ClickLeftMouse()
    {
        var mouse = AddGameMouse();
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null; yield return null;
        RemoveGameMouse(mouse);
    }

    private static InputSettings originalInput;

    private static Mouse AddGameMouse()
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        originalInput = InputSystem.settings;
        var isolated = Object.Instantiate(originalInput);
        isolated.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = isolated; mouse.MakeCurrent();
        return mouse;
    }

    // Kopya ayar play sonuna kalırsa Input System bir sonraki etki alanı yenilemesinde açılamaz.
    private static void RemoveGameMouse(Mouse mouse)
    {
        var isolated = InputSystem.settings;
        InputSystem.RemoveDevice(mouse);
        InputSystem.settings = originalInput;
        Object.Destroy(isolated);
    }

    private static void DropAwayFromNotebook(NotebookWorkspaceController workspace, NotebookDraggableBlock note, Vector2 point)
    {
        var window = Field<RectTransform>(workspace, "notebookWindow"); var parent = (RectTransform)window.parent;
        Vector2 original = window.anchoredPosition;
        float half = window.rect.width * window.localScale.x * .5f + 50;
        window.anchoredPosition = new Vector2(point.x < Screen.width * .5f ? -half : -parent.rect.width + half, original.y);
        Canvas.ForceUpdateCanvases();
        try { Require(workspace.TryDropBlock(note, point), "Gerçek kart sürükleme reddedildi: " + note.Data.title); }
        finally { window.anchoredPosition = original; }
    }

    private static void BroadcastInteraction(GameObject target)
        => ((Action<GameObject>)typeof(PlayerInteraction).GetField("OnPlayerInteracted", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))?.Invoke(target);
    private static void RequireAllowed(GameObject target)
        => Require(!PlayerInteraction.Instance.isTutorialMode || Field<HashSet<GameObject>>(PlayerInteraction.Instance, "allowedTutorialSet").Contains(target),
            "Oyuncunun gerçek etkileşim whitelist'i bu adımın hedefini engelliyor: " + target.name);
    private static IEnumerator WaitFor(Func<bool> condition, string label, float seconds = 25)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        Require(condition(), label + " tamamlanmadı.");
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static object Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
