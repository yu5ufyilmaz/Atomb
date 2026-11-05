using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

// Alan derinliği okunabilirlik karesi: oda, açık kitap ve açık defter 1920x1080 yakalanır, DoF durumu konsola yazılır.
public static class DepthOfFieldPlayChecks
{
    const string PendingKey = "DepthOfFieldPlayChecks.Pending";

    [MenuItem("Senzora/Doğrulama/DoF Okunabilirlik Karesi")]
    public static void Capture()
    {
        if (Application.isPlaying) { Begin(); return; }
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void WatchPlay() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.EraseBool(PendingKey);
        Begin();
    };

    static void Begin()
    {
        if (GameManager.Instance == null || SaveManager.Instance == null) throw new InvalidOperationException("Laboratuvarda Play gerekli.");
        SaveManager.Instance.sessionOnly = true; // Yeni Oyun yolu gerçek kaydı silmesin
        GameManager.Instance.StartCoroutine(Flow(Application.runInBackground));
    }

    static void LogState(string label)
    {
        var cam = Camera.main;
        var dof = DoFManager.Instance;
        DepthOfField profile = null;
        if (dof != null && dof.globalVolume != null) dof.globalVolume.profile.TryGet(out profile);
        var stack = HDCamera.GetOrCreate(cam).volumeStack?.GetComponent<DepthOfField>(); // kameranın gerçekten kullandığı karışım
        string P(DepthOfField d) => d == null ? "yok" :
            $"mode={d.focusMode.value} dist={d.focusDistance.value:0.###} near={d.nearFocusStart.value:0.##}-{d.nearFocusEnd.value:0.##}/{d.nearMaxBlur:0.#} far={d.farFocusStart.value:0.##}-{d.farFocusEnd.value:0.##}/{d.farMaxBlur:0.#}";
        Debug.Log($"DOF {label}: cam phys={cam.usePhysicalProperties} focal={cam.focalLength:0.####} sensor={cam.sensorSize} aperture={cam.aperture} fov={cam.fieldOfView:0.#} scale={cam.transform.lossyScale.x:0.#####}" +
                  $" | manager {P(profile)} | stack {P(stack)} | frameDoF={cam.GetComponent<HDAdditionalCameraData>()?.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.DepthOfField)}");
    }

    static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

    static IEnumerator Flow(bool oldRunInBackground)
    {
        yield return ResearchGuidanceChecks.EnterNormalGame(false);
        var player = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        float deadline = Time.realtimeSinceStartup + 60;
        while (player.IsFrozen && Time.realtimeSinceStartup < deadline) yield return null;
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = true;
        if (PressureSystemManager.Instance != null) PressureSystemManager.Instance.isSystemActive = false;
        if (BreakerBox.Instance != null) BreakerBox.Instance.isSystemActive = false;

        var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        var sizeIndex = view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var oldSize = sizeIndex.GetValue(view);
        PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "DoFCheck");
        string dir = $"outputs/dof/{DateTime.Now:yyyyMMdd-HHmm}";
        Directory.CreateDirectory(dir);
        // Kullanıcı ayarı DoF'u kapatıyorsa kare boyunca aç; ayar ve kayıt değişmez.
        var userOff = typeof(SettingsManager).GetField("depthOfField", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(SettingsManager.Instance) as DepthOfField;
        bool userOffActive = userOff != null && userOff.active;
        if (userOff != null) userOff.active = false;
        try
        {
            yield return Wait(1.5f); // otomatik odak otursun
            LogState("room");
            ScreenCapture.CaptureScreenshot($"{dir}/room.png");
            yield return null; yield return null;
            float yaw = player.IntendedLookYaw; // arkaya dön: uzak duvar/raflarda yumuşak bulanıklık görünsün
            for (int i = 0; i < 30; i++) { player.ForceCameraRotation(yaw + 180, 8f); yield return null; }
            yield return Wait(1.5f);
            LogState("room_far");
            ScreenCapture.CaptureScreenshot($"{dir}/room_far.png");
            yield return null; yield return null;
            for (int i = 0; i < 30; i++) { player.ForceCameraRotation(yaw, 8f); yield return null; }

            InteractableBook book = null; float best = float.MaxValue;
            foreach (var b in Object.FindObjectsByType<InteractableBook>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(b.transform.position, player.transform.position);
                if (b.isActiveAndEnabled && d < best) { best = d; book = b; }
            }
            if (book == null) throw new InvalidOperationException("Sahnede InteractableBook yok.");
            book.Interact();
            deadline = Time.realtimeSinceStartup + 15;
            while ((!book.isOpen || book.isAnimating) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Wait(0.6f); // 0.3 sn odak geçişi
            LogState($"book {book.name} ({best:0.0} m) open={book.isOpen}");
            ScreenCapture.CaptureScreenshot($"{dir}/book.png");
            yield return null; yield return null;
            book.ForceExit();
            deadline = Time.realtimeSinceStartup + 15;
            while (GameManager.Instance.activeInteraction != null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Wait(1f);

            var notebook = NotebookUI.Instance;
            if (notebook == null) throw new InvalidOperationException("NotebookUI yok.");
            notebook.ToggleNotebook();
            yield return Wait(1.5f);
            LogState($"notebook open={notebook.isNotebookOpen}");
            ScreenCapture.CaptureScreenshot($"{dir}/notebook.png");
            yield return null; yield return null;
            notebook.ToggleNotebook();
            yield return Wait(0.5f);
            Debug.Log($"DOF KARESİ BİTTİ: {Screen.width}x{Screen.height} -> {dir}");
        }
        finally
        {
            if (userOff != null) userOff.active = userOffActive;
            sizeIndex.SetValue(view, oldSize);
            Application.runInBackground = oldRunInBackground;
            EditorApplication.isPlaying = false;
        }
    }
}
