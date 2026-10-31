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

    [MenuItem("Senzora/Doğrulama/Tutorial Gerçek Oyun Akışı")]
    public static void Run()
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Laboratuvarda Play gerekli.");
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
        yield return WaitFor(() => PlayerInteraction.Instance.CanInteractWithWorld, "Başlangıç kamera odağının bitmesi");
        lesson.lessonBook.Interact(); BroadcastInteraction(lesson.lessonBook.gameObject);
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
        Require(ChalkboardTutorial.HasCollectedCards(NotebookUI.Instance), "Beş eğitim kartı arşivlenmedi.");
        yield return null;
        Require(!lesson.lessonBook.ExitLocked && Field<RectTransform>(workspace, "ghost") == null, "Kartlar toplanınca kitap kilidi veya hayalet kalkmadı.");
        lesson.lessonBook.ForceExit(); yield return WaitFor(() => game.activeInteraction == null, "Kitaptan çıkış");
        Debug.Log("TUTORIAL PLAY STAGE PASS: book collection");

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
        Require(handlePoint.x > 0 && handlePoint.x < Screen.width && handlePoint.y > 0 && handlePoint.y < Screen.height, "Hedef kartı kamerada görünmüyor.");
        Require(Field<RectTransform>(workspace, "ghost") != null, "Tahtada hedef rehberi görünmüyor.");
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
        // Pano açıkken de orta alan boştur; ders parçaları toplamadan kurulabilmeli.
        handle.GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSeconds(.35f);
        var parts = new List<DraggableFormula>();
        for (int i = 0; i < 3; i++)
        {
            string id = new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter }[i];
            Require(workspace.ShowCollectedPart(id), "Toplanan parça bulunamadı: " + id); Canvas.ForceUpdateCanvases();
            var note = Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None)
                .First(n => !n.IsSource && n.isActiveAndEnabled && n.Data.itemID == id);
            Vector2 local = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(.6f - i * .14f, .72f));
            Vector2 pointer = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint(new Vector3(local.x, local.y, .02f)));
            var before = new HashSet<DraggableFormula>(board.boardTransform.GetComponentsInChildren<DraggableFormula>());
            DropAwayFromNotebook(workspace, note, pointer);
            parts.Add(board.boardTransform.GetComponentsInChildren<DraggableFormula>().Single(p => !before.Contains(p)));
        }
        board.AttemptSynthesis(); Require(!experiment.LastReading.MeetsTarget && !lesson.boardStep.HasTriggered, "Bağlanmamış parçalar doğru ölçüm sayıldı.");
        yield return null;
        Require(Field<RectTransform>(workspace, "ghostLine") != null, "Parçalar arası bağ rehberi görünmüyor.");
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
        Require(Field<NotebookDraggableBlock>(workspace, "ghostCard") == experiment.OutputCard, "Ölçüm kartı rehberi görünmüyor.");
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
        RequireAllowed(spot.gameObject);
        spot.Interact(); BroadcastInteraction(spot.gameObject);
        yield return WaitFor(() => spot.IsOccupied, "Gerçek dolaba giriş");
        Require(!spot.canExit, "Düşman eğitimi sırasında dolaptan erken çıkılabiliyor.");
        yield return WaitFor(() => spot.canExit, "Guderian'ın gitmesi ve güvenli çıkış", 100);
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
