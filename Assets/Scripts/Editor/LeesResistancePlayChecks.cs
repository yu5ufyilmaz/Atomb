using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

// Exercises the saved scene's real timeline and death UI, without restarting or loading a save.
public static class LeesResistancePlayChecks
{
    const string Result = "outputs/lees-play-checks.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static void Require(bool value, string message)
    {
        assertions++;
        if (!value) { File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("LEES PLAY: " + message); }
    }
    [MenuItem("Senzora/LEES Mücadelesi/Gerçek Ölüm Akışını Doğrula")]
    public static void Run()
    {
        Require(Application.isPlaying, "Ana sahnede Play gerekli.");
        assertions = 0; Directory.CreateDirectory("outputs"); File.WriteAllText(Result, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
        LeesEnemyAI.Instance.StartCoroutine(Flow());
    }

    [MenuItem("Senzora/LEES Mücadelesi/İlk Bakış ve Yeniden Spawnı Doğrula")]
    public static void FirstSight()
    {
        const string path = "outputs/lees-first-sight-checks.txt";
        if (!Application.isPlaying || Time.deltaTime <= 0) throw new InvalidOperationException("Play modunda çalıştır.");
        Directory.CreateDirectory("outputs"); File.WriteAllText(path, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
        int checks = 0;
        void Check(bool value, string message)
        {
            checks++;
            if (!value) { File.AppendAllText(path, "FAIL " + message + "\n"); throw new InvalidOperationException(message); }
        }
        var oldGlobal = GlobalEnemyManager.Instance; var oldJump = JumpscareManager.Instance;
        var root = new GameObject("LEES first-sight physics check"); root.transform.position = Vector3.one * 10000;
        GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go; }
        GlobalEnemyManager.Instance = null; JumpscareManager.Instance = null;
        try
        {
            var camera = Child("View").AddComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 60; camera.aspect = 16f / 9;
            var player = Child("Player"); player.layer = 2; var capsule = player.AddComponent<UnityEngine.CharacterController>();
            var body = Child("LEES"); body.transform.localPosition = new Vector3(2.5f, 0, 5);
            body.AddComponent<BoxCollider>().size = new Vector3(.6f, 1.8f, .6f);
            var enemy = body.AddComponent<LeesEnemyAI>(); enemy.enabled = false;
            enemy.playerTransform = player.transform; enemy.playerCamera = camera; enemy.eyesPosition = body.transform;
            enemy.useShockEffect = false; enemy.showDebugLogs = false;
            typeof(LeesEnemyAI).GetField("targetCharacterController", Private).SetValue(enemy, capsule);
            typeof(LeesEnemyAI).GetField("visionLayerMask", Private).SetValue(enemy, 1);
            typeof(LeesEnemyAI).GetField("lastPlayerPos", Private).SetValue(enemy, player.transform.position);
            var active = typeof(LeesEnemyAI).GetMethod("HandleActiveLogic", Private);
            var visible = typeof(LeesEnemyAI).GetMethod("CheckIfVisible", Private);
            var turned = typeof(LeesEnemyAI).GetField("hasTurnedAway", Private);
            void Tick() { Physics.SyncTransforms(); active.Invoke(enemy, null); }
            bool Visible() { Physics.SyncTransforms(); return (bool)visible.Invoke(enemy, null); }
            enemy.currentState = LeesEnemyAI.LeesState.Active;
            Check(Visible(), "Fixture must see the actual enemy collider."); Tick();
            Check(enemy.HasBeenSpotted && enemy.currentState == LeesEnemyAI.LeesState.Active, "First sight must only spot LEES.");
            camera.fieldOfView = 30; Check(!Visible(), "Shock-like FOV narrowing must hide the off-center target."); Tick();
            Check(!(bool)turned.GetValue(enemy), "FOV/visibility loss without turning must not arm the look-again death.");
            camera.fieldOfView = 60; Tick();
            Check(enemy.currentState == LeesEnemyAI.LeesState.Active, "Restoring the first view must not kill the player.");
            var blocker = Child("Brief occlusion").AddComponent<BoxCollider>();
            blocker.transform.position = Vector3.Lerp(camera.transform.position, body.transform.position, .5f); blocker.size = Vector3.one;
            Check(!Visible(), "Occluder must interrupt the real raycast."); Tick();
            Check(!(bool)turned.GetValue(enemy), "Occlusion without turning must not count as turning away.");
            blocker.enabled = false; Tick();
            Check(enemy.currentState == LeesEnemyAI.LeesState.Active, "Reappearing after occlusion must not cause instant death.");
            camera.transform.rotation = Quaternion.Euler(0, 90, 0); Tick();
            Check(!(bool)turned.GetValue(enemy), "A side glance must not count as turning the back.");
            var control = Child("Resistance").AddComponent<LeesResistanceController>();
            control.headPivot = camera.transform; control.viewCamera = camera; control.useGamePause = true; enemy.resistance = control;
            camera.transform.rotation = Quaternion.Euler(0, 180, 0); Tick();
            Check((bool)turned.GetValue(enemy) && control.IsRunning, "A real turn must start the approach.");
            camera.transform.rotation = Quaternion.identity; Tick();
            Check(enemy.currentState == LeesEnemyAI.LeesState.Jumpscare && !control.IsRunning, "Looking back after an actual turn must still trigger death.");
            enemy.StopAllCoroutines(); enemy.currentState = LeesEnemyAI.LeesState.Active; camera.transform.rotation = Quaternion.identity;
            control.BeginResistance(); Check(control.IsRunning, "Respawn regression requires an existing active struggle.");
            var room = Child("Room").AddComponent<RoomManager>(); room.GetComponent<BoxCollider>().enabled = false;
            var point = Child("Spawn point").transform; point.localPosition = Vector3.back * 5; room.spawnPoints.Add(point); enemy.EnterRoom(room);
            enemy.SpawnLeesInRoom();
            Check(!control.IsRunning && !enemy.HasBeenSpotted && !(bool)turned.GetValue(enemy), "A successful respawn must clear the old struggle and sight state.");
            camera.transform.rotation = Quaternion.Euler(0, 180, 0); Tick();
            Check(enemy.HasBeenSpotted && enemy.currentState == LeesEnemyAI.LeesState.Active, "First sight after respawn must not inherit the old look-again death.");
            File.AppendAllText(path, "PASS " + checks + " physics/encounter assertions\n" + DateTime.UtcNow.ToString("O"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            GlobalEnemyManager.Instance = oldGlobal; JumpscareManager.Instance = oldJump;
        }
    }
    static IEnumerator Flow()
    {
        Application.runInBackground = true; Time.timeScale = 1; AudioListener.pause = false;
        if (SaveManager.Instance != null) SaveManager.Instance.sessionOnly = true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        // Enter a transient gameplay fixture; do not call PlayGame/NewGame, which touches the player's save.
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        if (menu != null)
        {
            var menuPanel = (GameObject)new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue;
            if (menuPanel != null) menuPanel.SetActive(false);
        }
        var intro = UnityEngine.Object.FindFirstObjectByType<InGameMenuController>();
        if (intro != null)
        {
            intro.StopAllCoroutines(); intro.enabled = false;
            if (intro.introTimeline != null) intro.introTimeline.Stop();
            if (intro.cameraTarget != null && intro.cameraAnchor != null)
            {
                intro.cameraTarget.SetParent(intro.cameraAnchor, false);
                intro.cameraTarget.localPosition = Vector3.zero; intro.cameraTarget.localRotation = Quaternion.identity;
            }
            if (intro.playerAnimator != null) intro.playerAnimator.Play("Base Layer.Blend Tree");
            foreach (var head in intro.headModelsToHide) if (head != null) head.SetActive(false);
        }
        GameManager.Instance.isGamePaused = false; GameManager.Instance.StartGameMode();
        yield return null;
        var enemy = LeesEnemyAI.Instance; var control = enemy.resistance;
        TutorialManager.Instance?.CompleteTutorial();
        GlobalEnemyManager.Instance.stopAllEnemies = false;
        enemy.CancelInvoke(); enemy.currentState = LeesEnemyAI.LeesState.Active; enemy.HasBeenSpotted = true; enemy.isSafeSpawn = false;
        // Keep the authored room guard stable while testing the resistance -> death boundary.
        typeof(LeesEnemyAI).GetField("spawnRoom", Private).SetValue(enemy, typeof(LeesEnemyAI).GetField("currentRoom", Private).GetValue(enemy));
        var inputs = enemy.playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>(); inputs.move = Vector2.zero; inputs.look = Vector2.zero;
        var movement = enemy.playerTransform.GetComponent<StarterAssets.CharacterController>();
        var capsule = enemy.playerTransform.GetComponent<UnityEngine.CharacterController>();
        movement.enabled = true; capsule.enabled = true; movement.SetFrozen(false, false, false);
        typeof(LeesEnemyAI).GetField("lastPlayerPos", Private).SetValue(enemy, enemy.playerTransform.position);
        Vector3 back = enemy.playerCamera.transform.forward; back.y = 0; back.Normalize();
        enemy.transform.position = enemy.playerTransform.position - back * 4;
        typeof(LeesEnemyAI).GetField("resistanceApproachStart", Private).SetValue(enemy, enemy.transform.position);
        typeof(LeesEnemyAI).GetField("resistanceApproachForward", Private).SetValue(enemy, back);
        var tutorial = TutorialManager.Instance;
        Require(tutorial != null, "Saved scene must have its real tutorial manager.");
        var authoredEvents = tutorial.tutorialEventsParent;
        var practice = new GameObject("LEES practice check");
        tutorial.tutorialEventsParent = practice; tutorial.isTutorialEnabled = true;
        enemy.isSafeSpawn = true;
        int retries = 0; UnityEngine.Events.UnityAction countRetry = () => retries++;
        control.onCaught.AddListener(countRetry); control.BeginResistance();
        Vector3 standing = enemy.playerTransform.position;
        inputs.move = Vector2.right;
        float moveDeadline = Time.realtimeSinceStartup + enemy.movementGraceTime + .15f;
        while (Time.realtimeSinceStartup < moveDeadline) yield return null;
        inputs.move = Vector2.zero;
        Require(Vector3.Distance(standing, enemy.playerTransform.position) > .01f, "Movement input must actually move the body during resistance.");
        float retryDeadline = Time.realtimeSinceStartup + 3;
        while (retries == 0 && Time.realtimeSinceStartup < retryDeadline) yield return null;
        Require(retries == 1 && control.IsRunning && enemy.currentState == LeesEnemyAI.LeesState.Active,
            "Moving during the safe tutorial must fail and retry the lesson, without dying.");
        control.onCaught.RemoveListener(countRetry); control.CancelResistance();
        tutorial.CompleteTutorial(); tutorial.tutorialEventsParent = authoredEvents; UnityEngine.Object.Destroy(practice);
        File.AppendAllText(Result, "PRACTICE movement and retry passed\n");
        yield return new WaitForSeconds(.4f);
        // Even a stale safe-spawn flag must not grant invulnerability outside the tutorial.
        enemy.isSafeSpawn = true;
        typeof(LeesEnemyAI).GetField("lastPlayerPos", Private).SetValue(enemy, enemy.playerTransform.position);
        control.BeginResistance();
        File.AppendAllText(Result, $"BEGIN movement={movement.enabled}, pause={control.IsPaused}, timeScale={Time.timeScale}, result={control.keepResultVisible}, gamePause={control.useGamePause}\n");
        Require(control.IsRunning && movement.enabled && !movement.IsFrozen, "Resistance must leave player movement available.");
        Require(!movement.ExternalCameraControl && !control.IsPaused && Time.timeScale == 1, "Approach must not grab the camera or pause the game.");
        bool reachedGrip = false;
        float deadline = Time.realtimeSinceStartup + control.tuning.approachSeconds + 9;
        while (enemy.currentState != LeesEnemyAI.LeesState.Jumpscare && Time.realtimeSinceStartup < deadline)
        {
            reachedGrip |= control.Model.State == LeesResistanceModel.Phase.Struggling && movement.ExternalCameraControl && movement.enabled;
            yield return null;
        }
        Require(reachedGrip, "Actual grip must take only the camera while the motor remains live.");
        Require(enemy.currentState == LeesEnemyAI.LeesState.Jumpscare, "Letting go must enter death, never restart resistance.");
        Require(!control.IsRunning && !control.hud.gameObject.activeSelf, "Gameplay death must clear minigame UI.");
        Require(enemy.timelineJumpscare != null && enemy.timelineJumpscare.state == PlayState.Playing, "The actual LEES jumpscare timeline must be playing.");
        File.AppendAllText(Result, "TIMELINE playing; duration=" + enemy.timelineJumpscare.duration + "\n");
        var death = DeathUIManager.Instance;
        Require(death != null, "Actual death UI is required.");
        var panel = (GameObject)typeof(DeathUIManager).GetField("deathScreenPanel", Private).GetValue(death);
        deadline = Time.realtimeSinceStartup + (float)enemy.timelineJumpscare.duration + 5;
        while (!panel.activeInHierarchy && Time.realtimeSinceStartup < deadline) yield return null;
        Require(panel.activeInHierarchy && Time.timeScale == 0, "Timeline must finish at the actual death screen.");
        Require(enemy.currentState == LeesEnemyAI.LeesState.Jumpscare && !control.IsRunning, "Death cannot re-enter resistance.");
        File.AppendAllText(Result, "PASS " + assertions + " real Play assertions\n" + DateTime.UtcNow.ToString("O"));
        Debug.Log("LEES REAL DEATH PASS: timeline and death screen; " + assertions + " assertions.");
    }
}
