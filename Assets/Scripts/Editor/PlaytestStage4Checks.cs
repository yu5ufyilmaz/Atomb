using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// 3 Kasım oyun testi notları, aşama 4: sigorta atınca karanlık, ampuller, makineler; sıfırlayınca geri dönüş.
public static class PlaytestStage4Checks
{
    const string Result = "outputs/playtest-stage4-checks.txt";
    static int assertions;

    static void Require(bool value, string message)
    {
        assertions++;
        if (!value) { File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("PLAYTEST STAGE 4: " + message); }
    }
    static void Log(string line) => File.AppendAllText(Result, line + "\n");

    [MenuItem("Senzora/Doğrulama/Oyun Testi Aşama 4 Play")]
    public static void Run()
    {
        Require(Application.isPlaying && !GameManager.Instance.isGameStarted, "Fresh Play at the main menu is required.");
        assertions = 0; Directory.CreateDirectory("outputs"); File.WriteAllText(Result, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        menu.StartCoroutine(Flow(menu));
    }

    struct View { public string name; public Vector3 feet; public Transform target; }

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
        var breaker = BreakerBox.Instance;
        breaker.isSystemActive = false; // no random trips while measuring
        Require(!breaker.IsTripped, "Power must start on.");

        var turing = UnityEngine.Object.FindFirstObjectByType<InteractableTuringMachine>();
        var lamp = GameObject.Find("ENVIRONMENT/WorkTableTestAssets/Luminaria-Lowpoly/Case-T.003").transform;
        var frame = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).First(f => f.name == "SM_PictureFrame3" && f.gameObject.activeInHierarchy).transform;
        var body = movement.transform; var physics = body.GetComponent<CharacterController>();
        Vector3 start = body.position;
        var views = new List<View>
        {
            new View { name = "desk", feet = start, target = lamp },
            new View { name = "turing", feet = start, target = turing.transform.Find("LookPoint") },
            new View { name = "corridor", feet = Stand(frame.GetComponent<Renderer>().bounds.center), target = frame },
        };
        // Lit book titles: the shelf nearest the start; the board behind its darkness veil.
        var title = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None)
            .Where(t => t.name == "Başlık" && t.isActiveAndEnabled).OrderBy(t => Vector3.Distance(t.transform.position, start)).First();
        Require(title.fontSharedMaterial.shader.name.Contains("HDRP LIT"), "Book titles must use the lit TMP material.");
        var boardDark = UnityEngine.Object.FindFirstObjectByType<BoardDarkness>();
        var board = boardDark.GetComponent<ChalkboardManager>().boardTransform;
        var veil = board.Find("Karanlık Perdesi").GetComponent<UnityEngine.UI.Image>();
        views.Add(new View { name = "books", feet = Stand(title.transform.position), target = title.transform });
        views.Add(new View { name = "board", feet = Stand(veil.transform.position + board.forward * 2.2f), target = veil.transform });
        var dynamo = UnityEngine.Object.FindFirstObjectByType<DynomaFlashLight>();
        var energy = typeof(DynomaFlashLight).GetField("currentEnergy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        var blackout = UnityEngine.Object.FindFirstObjectByType<PowerBlackout>();
        var volume = blackout.GetComponent<Volume>();
        var bulbs = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.sharedMaterial != null && r.sharedMaterial.name == "DenemeLamb").ToArray();
        Require(bulbs.Length > 10, "Lamp bulbs must be in the scene.");
        Require(volume.weight == 0 && bulbs.All(b => !b.HasPropertyBlock()), "With power, no blackout and every bulb glows.");

        foreach (string phase in new[] { "on", "off", "flashlight", "restored" })
        {
            if (phase == "off") { breaker.ForceTrip(); yield return new WaitForSeconds(.6f); }
            if (phase == "restored") { energy.SetValue(dynamo, 0f); breaker.Interact(); yield return new WaitForSeconds(.6f); }
            foreach (var view in views)
            {
                if (phase == "flashlight" && view.name != "books" && view.name != "board") continue;
                if (phase == "flashlight") energy.SetValue(dynamo, 100f);
                physics.enabled = false; body.position = view.feet; physics.enabled = true;
                yield return null;
                Vector3 look = Quaternion.LookRotation(view.target.position - Camera.main.transform.position).eulerAngles;
                for (int i = 0; i < 20; i++) { movement.ForceCameraRotation(look.y, look.x); yield return null; }
                ScreenCapture.CaptureScreenshot($"outputs/playtest-stage4-{view.name}-{phase}.png");
                yield return null; yield return null;
                if (view.name == "board") Log($"  board veil alpha in {phase}: {veil.color.a:0.00}");
            }
            Log($"{phase}: tripped={breaker.IsTripped} blackout weight={volume.weight:0.00} dark bulbs={bulbs.Count(b => b.HasPropertyBlock())}/{bulbs.Length}");
            if (phase == "off")
            {
                foreach (var lit in bulbs.Where(b => !b.HasPropertyBlock())) Log("  bulb still lit: " + Path(lit.transform));
                foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.isActiveAndEnabled && light.intensity > 0f && !light.transform.IsChildOf(body))
                        Log($"  light still on: {Path(light.transform)} ({light.type}, {light.intensity:0.#}, {Vector3.Distance(light.transform.position, start):0.#} m from start)");
            }
        }
        physics.enabled = false; body.position = start; physics.enabled = true;

        // Bulbs follow their own switch too, not only the breaker.
        var office = GameObject.Find("ENVIRONMENT/Office_Room/InteractablesProbs/SWITCHES/OR_Switch_05").GetComponent<ControllableLight>();
        office.SetLightState(false); yield return null;
        int darkAfterSwitch = bulbs.Count(b => b.HasPropertyBlock());
        office.SetLightState(true); yield return null;
        Log($"office switch off: dark bulbs={darkAfterSwitch}");
        Require(darkAfterSwitch > 0 && bulbs.All(b => !b.HasPropertyBlock()), "An office switch must darken its own bulbs and relight them.");
        Require(volume.weight == 0 && !breaker.IsTripped, "Resetting the breaker must lift the blackout.");
        Require(veil.color.a < .05f, "With the room lit again, the board veil must clear.");

        // Inside the Turing machine when the power goes: the player stays, the screen goes dark, power brings it back.
        var inMachine = typeof(InteractableTuringMachine).GetField("inMachineMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        turing.Interact();
        float enterBy = Time.time + 10;
        while (!(bool)inMachine.GetValue(turing) && Time.time < enterBy) yield return null;
        Require((bool)inMachine.GetValue(turing), "The Turing machine must accept the player.");
        yield return new WaitForSeconds(1f);
        ScreenCapture.CaptureScreenshot("outputs/playtest-stage4-turing-inside-on.png"); yield return null; yield return null;
        breaker.ForceTrip(); yield return new WaitForSeconds(.6f);
        int darkParts = turing.GetComponentsInChildren<Renderer>().Count(r => r.HasPropertyBlock());
        ScreenCapture.CaptureScreenshot("outputs/playtest-stage4-turing-inside-off.png"); yield return null; yield return null;
        Log($"turing inside, power off: still in machine={(bool)inMachine.GetValue(turing)}, darkened parts={darkParts}");
        // Unlit text/lines around the machine (its card reader is built outside its hierarchy) must not glow.
        var glowing = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.isVisible && !r.HasPropertyBlock() && r.sharedMaterial != null
                && Vector3.Distance(r.bounds.center, turing.transform.position) < 3f && !r.sharedMaterial.shader.name.StartsWith("HDRP/Lit")).ToList();
        foreach (var r in glowing) Log($"  visible non-Lit at Turing: {Path(r.transform)} [{r.sharedMaterial.name} / {r.sharedMaterial.shader.name}]");
        Require(glowing.Count == 0, "Nothing unlit may stay visible at the Turing machine in a blackout.");
        Require((bool)inMachine.GetValue(turing) && darkParts > 0, "A blackout must keep the player at the machine with its screen dark.");
        breaker.Interact(); yield return new WaitForSeconds(.6f);
        ScreenCapture.CaptureScreenshot("outputs/playtest-stage4-turing-inside-restored.png"); yield return null; yield return null;
        Require(turing.GetComponentsInChildren<Renderer>().All(r => !r.HasPropertyBlock()), "Power back must relight the machine.");
        turing.ForceExit();
        float exitBy = Time.time + 10;
        while (game.activeInteraction != null && Time.time < exitBy) yield return null;
        Require(game.activeInteraction == null, "Leaving the machine must work.");

        Log("PASS " + assertions + " assertions " + DateTime.UtcNow.ToString("O"));
        Debug.Log("PLAYTEST STAGE 4 PASS: " + assertions + " assertions.");
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    // The nearest walkable floor in front of a point, stepped back a little to see it whole.
    static Vector3 Stand(Vector3 point)
    {
        if (!NavMesh.SamplePosition(point, out var near, 4f, NavMesh.AllAreas))
            throw new InvalidOperationException("No walkable floor near " + point);
        Vector3 away = near.position - point; away.y = 0;
        if (away.sqrMagnitude < .01f) return near.position;
        return NavMesh.SamplePosition(near.position + away.normalized * 1.2f, out var back, 1f, NavMesh.AllAreas) ? back.position : near.position;
    }
}
