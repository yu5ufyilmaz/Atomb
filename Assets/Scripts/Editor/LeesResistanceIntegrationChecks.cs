using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Cinemachine;

public static class LeesResistanceIntegrationChecks
{
    static int assertions;
    static void Require(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException("LEES INTEGRATION: " + message); }
    [MenuItem("Senzora/LEES Mücadelesi/Ana Oyun Bağlantısını Doğrula")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Önce Play modundan çık.");
        assertions = 0;
        var oldGame = GameManager.Instance; var oldGlobal = GlobalEnemyManager.Instance; var oldJump = JumpscareManager.Instance;
        GameManager.Instance = null; GlobalEnemyManager.Instance = null; JumpscareManager.Instance = null;
        try
        {
            foreach (string path in LeesResistanceGameplaySetup.GameplayScenes)
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try { ValidateScene(scene); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
        finally { GameManager.Instance = oldGame; GlobalEnemyManager.Instance = oldGlobal; JumpscareManager.Instance = oldJump; }
        Directory.CreateDirectory("outputs");
        File.WriteAllText("outputs/lees-integration-checks.txt", "PASS " + assertions + " assertions\n" + DateTime.UtcNow.ToString("O"));
        Debug.Log("LEES INTEGRATION PASS: " + assertions + " assertions; both gameplay scenes.");
    }
    static void ValidateScene(Scene scene)
    {
        var enemy = LeesResistanceGameplaySetup.Find<LeesEnemyAI>(scene);
        Require(enemy != null && enemy.resistance != null, "Live enemy/controller link: " + scene.path);
        var control = enemy.resistance;
        Require(control.viewCamera == enemy.playerCamera && control.headPivot == enemy.playerCamera.transform, "Use the actual gameplay camera.");
        Require(control.useGamePause && !control.keepResultVisible, "Game owns pause and outcome flow.");
        Require(control.GetComponentsInChildren<Camera>(true).Length == 0 && control.GetComponentsInChildren<AudioListener>(true).Length == 0, "No duplicate camera/listener.");
        Require(control.onEscaped.GetPersistentEventCount() == 1 && control.onEscaped.GetPersistentTarget(0) == enemy && control.onEscaped.GetPersistentMethodName(0) == "ResistanceEscaped", "Survival must despawn the actual LEES.");
        Require(control.onCaught.GetPersistentEventCount() == 1 && control.onCaught.GetPersistentTarget(0) == enemy && control.onCaught.GetPersistentMethodName(0) == "ResistanceCaught", "Failure must use actual LEES death flow.");
        var suspended = control.suspendDuringResistance;
        var actualMovement = enemy.playerTransform.GetComponent<StarterAssets.CharacterController>();
        Require(actualMovement != null && control.playerMovement == actualMovement && Array.IndexOf(suspended, actualMovement) < 0, "Keep actual CharacterController live; grip only takes camera ownership.");
        Require(Array.IndexOf(suspended, enemy.playerCamera.GetComponent<CinemachineBrain>()) >= 0, "Suspend Cinemachine ownership.");
        Require(control.heartbeatAudio != null && control.heartbeatAudio.clip != null, "Heartbeat.");
        Require(control.heartbeatAudio.outputAudioMixerGroup != null && control.heartbeatAudio.outputAudioMixerGroup == enemy.audioSource.outputAudioMixerGroup, "Heartbeat must respect the game's SFX/master mixer.");
        foreach (var audio in control.GetComponentsInChildren<AudioSource>(true))
            Require(audio.clip == null || (!audio.clip.name.Contains("Tinnitus") && !audio.clip.name.Contains("Violin") && !audio.clip.name.Contains("Jumpscare")), "No repeated tinnitus/jumpscare audio.");
        Require(control.hands != null && control.hands.viewCamera == enemy.playerCamera, "Camera hands.");
        ValidateHandFraming(control);
        foreach (var hand in new[] { control.hands.leftHand, control.hands.rightHand })
        {
            Require(hand != null && hand.IsChildOf(enemy.playerCamera.transform), "Hands must follow the camera.");
            Require(!hand.IsChildOf(enemy.transform), "World enemy renderer visibility must not control grip hands.");
            Require(hand.GetComponent<Collider>() == null, "Hands must not intercept interaction/vision rays.");
            var mesh = hand.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh != null && mesh.vertexCount > 100 && mesh.bounds.size.y > .08f && mesh.bounds.size.y < .25f, "Extracted geometry must contain only the hand.");
            Require(mesh.bounds.min.y >= -.013f, "No forearm below the wrist.");
            Require(mesh.bounds.center.magnitude < .4f, "Mesh coordinates must be centered on the wrist.");
        }
        var oldScale = Time.timeScale;
        try
        {
            control.BeginResistance(); Require(control.IsRunning, "Controller starts with gameplay references.");
            Require(actualMovement.enabled && !actualMovement.ExternalCameraControl, "Approach must leave walking and normal view control available.");
            typeof(LeesResistanceController).GetMethod("OnApplicationFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(control, new object[] { false });
            Require(!control.IsPaused, "Gameplay focus loss must not create a separate minigame pause.");
            var initial = control.Model.Elapsed; Time.timeScale = 0;
            typeof(LeesResistanceController).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(control, null);
            Require(control.IsPaused && control.Model.Elapsed == initial && !control.hud.gameObject.activeSelf, "Main game pause freezes encounter and yields to pause UI.");
        }
        finally { Time.timeScale = oldScale; control.CancelResistance(); }
        Require(!control.hands.leftHand.gameObject.activeSelf && !control.hands.rightHand.gameObject.activeSelf, "Cancel must clear both hands.");
        Outcomes(enemy, control);
    }
    static void ValidateHandFraming(LeesResistanceController control)
    {
        var camera = control.viewCamera;
        var hd = camera.GetComponent<HDAdditionalCameraData>();
        bool custom = hd.customRenderingSettings;
        bool focus = hd.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.DepthOfField);
        var focusMask = hd.renderingPathCustomFrameSettingsOverrideMask;
        var model = new LeesResistanceModel(new LeesResistanceModel.Tuning { approachSeconds = 0 }); model.Begin(1);
        model.Step(.5f, 0); control.hands.Present(model);
        Require(control.hands.fillLight != null && control.hands.fillLight.enabled, "Grip fill is enabled while hands are visible.");
        Require((uint)control.hands.fillLight.GetComponent<HDAdditionalLightData>().lightlayersMask == LeesResistanceGameplaySetup.HandRenderingLayer,
            "HDRP fill light must use its dedicated rendering layer.");
        Require(hd.customRenderingSettings && hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.DepthOfField]
            && !hd.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.DepthOfField), "Near hands must stay in focus despite world autofocus.");
        var expectedMask = custom ? focusMask : new FrameSettingsOverrideMask(); expectedMask.mask[(uint)FrameSettingsField.DepthOfField] = true;
        Require(hd.renderingPathCustomFrameSettingsOverrideMask.mask.Equals(expectedMask.mask), "Enabling DoF control must not activate dormant camera overrides.");
        foreach (var hand in new[] { control.hands.leftHand, control.hands.rightHand })
        {
            var mesh = hand.GetComponent<MeshFilter>().sharedMesh;
            Require((hand.GetComponent<Renderer>().renderingLayerMask & LeesResistanceGameplaySetup.HandRenderingLayer) != 0, "Grip mesh must receive the isolated fill light.");
            var bounds = new Bounds(); bool first = true;
            foreach (var vertex in mesh.vertices)
            {
                Vector3 point = camera.WorldToViewportPoint(hand.TransformPoint(vertex));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
            }
            Debug.Log($"LEES HAND FRAMING {camera.gameObject.scene.path}: camera scale={camera.transform.lossyScale:F4}, {hand.name} world size={hand.GetComponent<Renderer>().bounds.size:F4}, viewport={bounds.min:F4}..{bounds.max:F4}");
            Require(bounds.size.x > .05f && bounds.size.y > .1f, "Hands must occupy visible screen area with the actual gameplay camera scale.");
            Require(bounds.size.x < .3f && bounds.size.y < .65f, "Hands should frame the edges without filling the view.");
            Require(bounds.min.x < 1 && bounds.max.x > 0 && bounds.min.y < 1 && bounds.max.y > 0, "Each hand must overlap the screen.");
            Require(bounds.min.z > camera.nearClipPlane && bounds.max.z < camera.farClipPlane, "Hands must fit between the gameplay camera clipping planes.");
        }
        CaptureGameplayHands(control, model);
        control.hands.Hide();
        Require(!control.hands.fillLight.enabled, "Hide must also turn off the grip fill light.");
        Require(hd.customRenderingSettings == custom && hd.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.DepthOfField) == focus
            && hd.renderingPathCustomFrameSettingsOverrideMask.mask.Equals(focusMask.mask), "Hide must restore the exact camera DoF override state.");
    }
    static void CaptureGameplayHands(LeesResistanceController control, LeesResistanceModel model)
    {
        var camera = control.viewCamera; var scene = camera.gameObject.scene;
        camera.cameraType = CameraType.Game; camera.scene = scene;
        // Preview copies retain the real scene lighting, without mixing in the open editor scene's volumes.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var volume in root.GetComponentsInChildren<Volume>(true))
            { bool enabled = volume.enabled; volume.enabled = false; volume.gameObject.layer = 31; volume.enabled = enabled; }
        camera.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 1 << 31;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); target.Create();
        var previous = RenderTexture.active;
        Texture2D Read()
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request); RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            var result = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); result.Apply(); return result;
        }
        Texture2D before = null, after = null;
        try
        {
            control.hands.leftHand.gameObject.SetActive(false); control.hands.rightHand.gameObject.SetActive(false);
            before = Read();
            control.hands.Present(model); after = Read();
            var a = before.GetPixels32(); var b = after.GetPixels32(); int left = 0, right = 0;
            for (int y = 100; y < 650; y++)
                for (int x = 0; x < 1280; x++)
                {
                    if (x > 320 && x < 960) continue;
                    int i = y * 1280 + x;
                    int difference = Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                    if (difference > 18) { if (x < 320) left++; else right++; }
                }
            Directory.CreateDirectory("outputs");
            File.WriteAllBytes("outputs/lees-hands-" + Path.GetFileNameWithoutExtension(scene.path) + ".png", after.EncodeToPNG());
            File.WriteAllBytes("outputs/lees-no-hands-" + Path.GetFileNameWithoutExtension(scene.path) + ".png", before.EncodeToPNG());
            Require(left > 1500 && right > 1500, "Actual main-camera render must visibly draw both hands; changed edge pixels=" + left + "/" + right);
            Debug.Log("LEES MAIN HAND RENDER: " + scene.path + "; left/right pixels=" + left + "/" + right);
        }
        finally
        {
            if (before != null) UnityEngine.Object.DestroyImmediate(before);
            if (after != null) UnityEngine.Object.DestroyImmediate(after);
            RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    static void Outcomes(LeesEnemyAI enemy, LeesResistanceController control)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var update = typeof(LeesResistanceController).GetMethod("Update", flags);
        var ending = typeof(LeesResistanceController).GetField("endingTime", flags);
        int escaped = 0, caught = 0;
        var originalEnabled = new bool[control.suspendDuringResistance.Length];
        for (int i = 0; i < originalEnabled.Length; i++) originalEnabled[i] = control.suspendDuringResistance[i].enabled;
        // Saved callbacks correctly use RuntimeOnly; explicitly enable their preview copies for this edit-mode fixture.
        control.onEscaped.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        control.onCaught.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        control.onEscaped.AddListener(() => escaped++); control.onCaught.AddListener(() => caught++);
        // Preview fixtures exercise real callbacks, without playing a death cinematic or using live singletons.
        enemy.audioSource = null; enemy.timelineJumpscare = null; enemy.leesAnimator = null; enemy.isSafeSpawn = false;
        enemy.currentState = LeesEnemyAI.LeesState.Active;
        control.BeginResistance(); control.Model.Step(control.tuning.approachSeconds + .01f, 0);
        var playerPosition = enemy.playerTransform.position;
        typeof(LeesResistanceController).GetMethod("LateUpdate", flags).Invoke(control, null);
        Require(control.playerMovement.enabled && control.playerMovement.ExternalCameraControl && enemy.playerTransform.position == playerPosition,
            "Grip owns the head without moving or disabling the player's body.");
        control.hud.Present(control.Model, false, false);
        Require(!control.hud.footer.text.Contains("DURAKLAT") && !control.hud.footer.text.Contains("YENİDEN"), "Main HUD must not advertise workshop pause/retry.");
        for (int i = 0; i < 1600 && control.Model.State == LeesResistanceModel.Phase.Struggling; i++)
        {
            var model = control.Model;
            float pixels = -(model.PullDirection * model.PullStrength + model.HeadAngle * 2) / model.Settings.mouseDegreesPerPixel / 60;
            model.Step(1f / 60, pixels);
        }
        Require(control.Model.State == LeesResistanceModel.Phase.Survived, "Integrated encounter is winnable with actual tuning.");
        ending.SetValue(control, .65f); update.Invoke(control, null); update.Invoke(control, null);
        Require(enemy.currentState == LeesEnemyAI.LeesState.Hidden && !control.IsRunning && escaped == 1, "Success despawns LEES once through the saved scene callback.");
        Require(!control.playerMovement.ExternalCameraControl, "Release returns normal camera control.");
        enemy.currentState = LeesEnemyAI.LeesState.Active; control.BeginResistance(); control.CatchForMovement();
        ending.SetValue(control, .65f); update.Invoke(control, null); update.Invoke(control, null);
        Require(enemy.currentState == LeesEnemyAI.LeesState.Jumpscare && !control.IsRunning && caught == 1, "Failure enters existing death flow once through the saved callback.");
        enemy.currentState = LeesEnemyAI.LeesState.Active; control.BeginResistance();
        typeof(LeesEnemyAI).GetMethod("OnDisable", flags).Invoke(enemy, null);
        Require(!control.IsRunning && !control.hands.leftHand.gameObject.activeSelf && !control.hands.rightHand.gameObject.activeSelf, "Disabling world LEES releases camera and hands.");
        for (int i = 0; i < originalEnabled.Length; i++)
            Require(control.suspendDuringResistance[i].enabled == originalEnabled[i], "Actual gameplay control states must be restored after completion/disable.");
    }
}
