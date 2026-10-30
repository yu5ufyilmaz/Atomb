using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class LeesResistanceWorkshopChecks
{
    static int assertions;
    static void Require(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException("LEES CHECK: " + message); }

    [MenuItem("Senzora/LEES Mücadelesi/Doğrula")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("LEES doğrulaması için önce Play modundan çık.");
        assertions = 0;
        var scene = EditorSceneManager.OpenPreviewScene(LeesResistanceWorkshopSetup.ScenePath);
        try
        {
            LeesResistanceDemo demo = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                demo = demo != null ? demo : root.GetComponentInChildren<LeesResistanceDemo>();
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script at " + transform.name);
                Require(root.GetComponentInChildren<GameManager>(true) == null, "Workshop must not need GameManager.");
            }
            Require(demo != null, "Demo rig missing.");
            var control = demo.resistance;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LeesResistanceWorkshopSetup.Folder + "/WorkshopExposure.asset");
            Require(profile.TryGet<Exposure>(out _) && profile.TryGet<Tonemapping>(out _), "Saved exposure and tone mapping.");
            Require(profile.TryGet<VisualEnvironment>(out var environment) && environment.skyType.overrideState && environment.skyType.value == 0, "Workshop must override the project's daylight HDRI.");
            foreach (var component in profile.components) Require(AssetDatabase.Contains(component), "Volume components must be persistent subassets.");
            Require(control.viewCamera != null && control.headPivot != null && control.hud != null, "Rig references.");
            for (int seed = 0; seed < 8; seed++)
            {
                var settings = JsonUtility.FromJson<LeesResistanceModel.Tuning>(JsonUtility.ToJson(control.tuning));
                settings.approachSeconds = 0;
                var unattended = new LeesResistanceModel(settings); unattended.Begin(seed);
                for (int i = 0; i < 135 && unattended.State == LeesResistanceModel.Phase.Struggling; i++) unattended.Step(1f / 60, 0);
                Require(unattended.State == LeesResistanceModel.Phase.Caught, "Saved scene tuning must punish letting go within 2.25 seconds.");
            }
            Require(demo.leesVisual.GetComponent<LeesEnemyAI>() == null, "Live enemy AI must not run in the workshop.");
            Require(control.heartbeatAudio.clip != null, "Heartbeat clip.");
            foreach (var text in control.hud.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(); Require(text.font != null, "Missing HUD font.");
            }
            Lifecycle(control);
            Capture(demo, false, "outputs/lees-workshop-intro.png");
            Capture(demo, true, "outputs/lees-workshop-struggle.png");
            Directory.CreateDirectory("outputs");
            File.WriteAllText("outputs/lees-workshop-checks.txt", "PASS " + assertions + " Unity assertions\n" + DateTime.UtcNow.ToString("O"));
            Debug.Log("LEES UNITY PASS: " + assertions + " assertions; intro and struggle renders captured.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Lifecycle(LeesResistanceController control)
    {
        var first = control.gameObject.AddComponent<AudioSource>();
        var second = control.hud.gameObject.AddComponent<AudioSource>(); second.enabled = false;
        control.suspendDuringResistance = new Behaviour[] { first, second, first };
        var rotation = control.headPivot.localRotation; var position = control.headPivot.localPosition;
        float fov = control.viewCamera.fieldOfView;
        var oldLock = Cursor.lockState; bool oldVisible = Cursor.visible;
        try
        {
            control.BeginResistance();
            Require(control.IsRunning && !first.enabled && !second.enabled, "Begin must suspend only assigned components.");
            control.SetPaused(true); float before = control.Model.Elapsed;
            typeof(LeesResistanceController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, null);
            Require(control.IsPaused && control.Model.Elapsed == before, "Pause must freeze encounter.");
            control.CancelResistance();
            Require(!control.IsRunning && !control.IsPaused && first.enabled && !second.enabled, "Cancel must restore exact component states.");
            Require(!control.hud.gameObject.activeSelf, "Cancel must remove encounter UI.");
            Require(control.headPivot.localRotation == rotation && control.headPivot.localPosition == position && control.viewCamera.fieldOfView == fov, "Cancel must restore camera.");
            Require(Cursor.lockState == oldLock && Cursor.visible == oldVisible, "Cancel must restore cursor.");
            control.BeginResistance();
            typeof(LeesResistanceController).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { false });
            Require(control.IsPaused, "Focus loss must pause.");
            typeof(LeesResistanceController).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { true });
            Require(control.IsPaused, "Focus return must await explicit resume.");
            typeof(LeesResistanceController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, null);
            Require(!control.IsRunning && first.enabled && !second.enabled, "Disable must release controls.");
            control.CancelResistance();
            Outcomes(control, first, second);
        }
        finally
        {
            control.CancelResistance(); control.suspendDuringResistance = new Behaviour[0];
            UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
        }
    }
    static void Outcomes(LeesResistanceController control, Behaviour first, Behaviour second)
    {
        int escaped = 0, caught = 0;
        UnityEngine.Events.UnityAction escapedListener = () => escaped++;
        UnityEngine.Events.UnityAction caughtListener = () => caught++;
        control.onEscaped.AddListener(escapedListener); control.onCaught.AddListener(caughtListener);
        var update = typeof(LeesResistanceController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        var ending = typeof(LeesResistanceController).GetField("endingTime", BindingFlags.Instance | BindingFlags.NonPublic);
        float oldApproach = control.tuning.approachSeconds; bool oldResult = control.keepResultVisible;
        try
        {
            control.tuning.approachSeconds = 0; control.keepResultVisible = false;
            control.BeginResistance();
            for (int i = 0; i < 1600 && control.Model.State == LeesResistanceModel.Phase.Struggling; i++)
            {
                var model = control.Model;
                float pixels = -(model.PullDirection * model.PullStrength + model.HeadAngle * 2) / model.Settings.mouseDegreesPerPixel / 60;
                model.Step(1f / 60, pixels);
            }
            Require(control.Model.State == LeesResistanceModel.Phase.Survived, "Actual controller model must be winnable.");
            ending.SetValue(control, .65f); update.Invoke(control, null); update.Invoke(control, null);
            Require(escaped == 1 && caught == 0 && !control.IsRunning && first.enabled && !second.enabled, "Success emits once after releasing exact control states.");
            Require(!control.hud.gameObject.activeSelf, "Integrated result hides workshop UI.");
            control.keepResultVisible = true; control.BeginResistance(); control.CatchForMovement();
            ending.SetValue(control, .65f); update.Invoke(control, null); update.Invoke(control, null);
            Require(escaped == 1 && caught == 1 && !control.IsRunning, "Movement failure emits once.");
            Require(control.hud.gameObject.activeSelf && control.hud.title.text == "YAKALANDIN", "Workshop keeps outcome visible.");
            control.CancelResistance(); Require(!control.hud.gameObject.activeSelf, "Cancel after result clears UI.");
        }
        finally
        {
            control.CancelResistance(); control.tuning.approachSeconds = oldApproach; control.keepResultVisible = oldResult;
            control.onEscaped.RemoveListener(escapedListener); control.onCaught.RemoveListener(caughtListener);
        }
    }
    static void Capture(LeesResistanceDemo demo, bool struggle, string path)
    {
        var control = demo.resistance; var camera = demo.viewCamera;
        camera.cameraType = CameraType.Game;
        // Keep any open gameplay scene's global volumes out of this isolated preview.
        foreach (var root in demo.gameObject.scene.GetRootGameObjects())
            foreach (var volume in root.GetComponentsInChildren<Volume>())
            { volume.enabled = false; volume.gameObject.layer = 31; volume.enabled = true; }
        camera.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 1 << 31;
        camera.scene = demo.gameObject.scene;
        if (struggle)
        {
            camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            var model = new LeesResistanceModel(new LeesResistanceModel.Tuning { approachSeconds = 0 }); model.Begin(1);
            for (int i = 0; i < 60 && model.Danger < .72f; i++) model.Step(1f / 60, 0);
            camera.transform.rotation *= Quaternion.Euler(0, model.HeadAngle * .4f, -3);
            control.hud.Present(model, false);
            control.hands?.Present(model);
        }
        else control.hud.ShowPrompt("ARKANA BAKMA", "LEES'i gör. Arkanı dön. Kıpırdama.\nBaşını çevirmeye çalıştığında fareyi ters yöne çek.", "BAŞLAMAK İÇİN TIKLA                         R  /  YENİDEN DENE");
        // Preview scenes don't run CanvasScaler's normal editor update. Use an equivalent
        // camera-aligned world canvas for a deterministic 16:9 render and layout check.
        var canvas = control.hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)canvas.transform; rect.SetParent(camera.transform, false);
        rect.sizeDelta = new Vector2(1920, 1080); rect.localPosition = Vector3.forward * .3f;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * (2 * .3f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / 1080);
        Canvas.ForceUpdateCanvases();
        foreach (var text in control.hud.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Text overflow: " + text.name); }
        var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); target.Create();
        var previous = RenderTexture.active;
        try
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderPipeline.SubmitRenderRequest(camera, request); // Warm exposure/history before reading pixels.
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
