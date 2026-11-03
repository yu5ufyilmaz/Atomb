using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// 3 Kasım oyun testi notları, aşama 5: koridor tabloları ayrı objeler; Lees mücadelesinin hissiyatı (eski/yeni ölçüm).
public static class PlaytestStage5Checks
{
    const string Result = "outputs/playtest-stage5-checks.txt";
    static int assertions;

    static void Require(bool value, string message)
    {
        assertions++;
        if (!value) { File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("PLAYTEST STAGE 5: " + message); }
    }
    static void Log(string line) => File.AppendAllText(Result, line + "\n");
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    [MenuItem("Senzora/Doğrulama/Oyun Testi Aşama 5 Play")]
    public static void Run()
    {
        Require(Application.isPlaying && !GameManager.Instance.isGameStarted, "Fresh Play at the main menu is required.");
        assertions = 0; Directory.CreateDirectory("outputs"); File.WriteAllText(Result, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        menu.StartCoroutine(Flow(menu));
    }

    static IEnumerator Flow(MainMenuManager menu)
    {
        Application.runInBackground = true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        SaveManager.Instance.sessionOnly = true;
        var game = GameManager.Instance;
        var movement = UnityEngine.Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        menu.PlayGame(); yield return null;
        typeof(MainMenuManager).GetMethod("ChooseTutorial").Invoke(menu, new object[] { false });
        float deadline = Time.realtimeSinceStartup + 120;
        while ((!game.isGameStarted || movement.IsFrozen) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(game.isGameStarted && !movement.IsFrozen, "Opening must finish with player control.");
        GlobalEnemyManager.Instance.stopAllEnemies = true;
        BreakerBox.Instance.isSystemActive = false;

        // 1. Corridor paintings: five separate objects, each with its own renderer and collider.
        var frame = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .First(t => t.name == "SM_PictureFrame3" && t.gameObject.activeInHierarchy && t.Find("Tablo_01") != null);
        var paintings = Enumerable.Range(1, 5).Select(i => frame.Find($"Tablo_{i:00}")).ToArray();
        Require(paintings.All(p => p != null && p.GetComponent<MeshRenderer>() != null && p.GetComponent<BoxCollider>() != null), "Five paintings with renderer and collider.");
        var centers = paintings.Select(p => p.GetComponent<Renderer>().bounds.center).ToArray();
        float closest = Enumerable.Range(0, 5).SelectMany(a => Enumerable.Range(a + 1, 4 - a).Select(b => Vector3.Distance(centers[a], centers[b]))).Min();
        Log($"paintings: sizes {string.Join(" ", paintings.Select(p => p.GetComponent<Renderer>().bounds.size.ToString("F2")))}, closest pair {closest:0.00} m");
        Require(closest > .2f && paintings.All(p => p.GetComponent<Renderer>().bounds.size.magnitude < 3f), "Paintings must be separate, painting-sized objects.");
        var all = new Bounds(centers[0], Vector3.zero); foreach (var c in centers) all.Encapsulate(c);
        var body = movement.transform; var physics = body.GetComponent<CharacterController>();
        Vector3 start = body.position;
        // The paintings' thin axis is their facing; stand on the side whose view actually reaches a painting.
        var size = paintings[0].GetComponent<Renderer>().bounds.size;
        Vector3 facing = size.x < size.z ? Vector3.right : Vector3.forward;
        Vector3 stand = Vector3.zero; bool found = false;
        Vector3 middle = centers[2]; // the mean of all five falls on the wall between them
        foreach (Vector3 dir in new[] { facing, -facing })
        foreach (float distance in new[] { 2.2f, 1.6f, 1.1f })
        {
            if (found) break;
            // This corridor isn't in the borrowed NavMesh: find the floor under the spot with a ray.
            if (!Physics.Raycast(middle + dir * distance, Vector3.down, out var ground, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            Vector3 eye = ground.point + Vector3.up * 1.6f, face = middle + dir * .05f;
            bool blocked = Physics.Linecast(eye, face, out var hit, ~0, QueryTriggerInteraction.Ignore) && !paintings.Contains(hit.transform);
            Log($"  stand {dir} at {ground.point:F2}: sight {(blocked ? "blocked by " + hit.transform.name : "clear")}");
            if (!blocked) { stand = ground.point; found = true; }
        }
        Require(found, "A walkable spot that sees the paintings.");
        physics.enabled = false; body.position = stand; physics.enabled = true; yield return null;
        Vector3 look = Quaternion.LookRotation(all.center - Camera.main.transform.position).eulerAngles;
        for (int i = 0; i < 20; i++) { movement.ForceCameraRotation(look.y, look.x); yield return null; }
        ScreenCapture.CaptureScreenshot("outputs/playtest-stage5-paintings.png"); yield return null; yield return null;
        physics.enabled = false; body.position = start; physics.enabled = true; yield return new WaitForSeconds(.3f);

        // 2. LEES struggle with the old feel, then the new one, measured on the same controller.
        var control = UnityEngine.Object.FindFirstObjectByType<LeesResistanceController>();
        Require(control != null && control.grabSound != null, "The grab must have a sound (LEES' shock sound).");
        var volume = control.GetComponents<Volume>().Last();
        // With enemies stopped, LEES' AI despawns and cancels any running struggle; measure the controller alone.
        var lees = UnityEngine.Object.FindFirstObjectByType<LeesEnemyAI>();
        if (lees != null) lees.enabled = false;
        float kick = control.grabKickDegrees, jolt = control.flipJoltDegrees, scale = control.shakeScale, effects = control.dangerEffects;
        var oldPath = new List<Quaternion>();
        foreach (bool newFeel in new[] { false, true })
        {
            control.grabKickDegrees = newFeel ? kick : 0; control.flipJoltDegrees = newFeel ? jolt : 0;
            control.shakeScale = newFeel ? scale : 1; control.dangerEffects = newFeel ? effects : 0;
            UnityEngine.Random.InitState(424242); // same pull sequence for both feels
            control.BeginResistance(false);
            Require(control.IsRunning, $"Struggle must start (enabled={control.isActiveAndEnabled}, pivot={control.headPivot != null}, camera={control.viewCamera != null}, path={Path(control.transform)}).");
            var cam = Camera.main.transform;
            while (control.IsRunning && control.Model.State == LeesResistanceModel.Phase.Approaching) yield return null;
            float grabAt = Time.time; var turns = new List<float>(); float grabPeak = 0, maxWeight = 0; var added = new List<float>();
            Quaternion previous = cam.rotation, atGrab = cam.rotation; bool shotGrab = false, shotDanger = false;
            string label = newFeel ? "new" : "old";
            while (control.IsRunning && control.Model.State == LeesResistanceModel.Phase.Struggling)
            {
                yield return null;
                // Per-frame turn minus the steady pull: what reads as shake and jolts.
                float turn = Quaternion.Angle(previous, cam.rotation); previous = cam.rotation;
                turns.Add(turn);
                if (newFeel) { int i = Mathf.Min(added.Count, oldPath.Count - 1); if (i >= 0) added.Add(Quaternion.Angle(oldPath[i], cam.rotation)); }
                else oldPath.Add(cam.rotation);
                if (Time.time - grabAt < .35f) grabPeak = Mathf.Max(grabPeak, Quaternion.Angle(atGrab, cam.rotation));
                maxWeight = Mathf.Max(maxWeight, volume.weight);
                if (!shotGrab && Time.time - grabAt > .08f) { shotGrab = true; ScreenCapture.CaptureScreenshot($"outputs/playtest-stage5-lees-{label}-grab.png"); }
                if (!shotDanger && control.Model.Danger > .7f) { shotDanger = true; ScreenCapture.CaptureScreenshot($"outputs/playtest-stage5-lees-{label}-danger.png"); }
            }
            Require(turns.Count > 10, $"The {label} struggle must run (ended in {control.Model.State}).");
            turns.Sort();
            if (newFeel && added.Count > 0) { added.Sort(); Log($"lees new vs old, same pulls: extra motion median {added[added.Count / 2]:0.0}°, peak {added.Last():0.0}°"); }
            Log($"lees {label}: struggle {Time.time - grabAt:0.0}s, grab swing {grabPeak:0.0}°, median turn {turns[turns.Count / 2]:0.00}°/frame, 90th {turns[turns.Count * 9 / 10]:0.00}°/frame, strain effect peak {maxWeight:0.00}");
            float waitEnd = Time.time + 3; while (control.IsRunning && Time.time < waitEnd) yield return null;
            if (control.IsRunning) control.CancelResistance();
            yield return new WaitForSeconds(.5f);
        }
        control.grabKickDegrees = kick; control.flipJoltDegrees = jolt; control.shakeScale = scale; control.dangerEffects = effects;
        Require(volume.weight == 0, "The strain effect must clear when the struggle ends.");
        Log("PASS " + assertions + " assertions " + DateTime.UtcNow.ToString("O"));
        Debug.Log("PLAYTEST STAGE 5 PASS: " + assertions + " assertions.");
    }
}
