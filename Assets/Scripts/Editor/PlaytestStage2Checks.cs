using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 3 Kasım oyun testi notları, aşama 2: kamera zıplamaları. Her karede ekrandaki kameranın dönüşü ve kayması kaydedilir.
public static class PlaytestStage2Checks
{
    const string Result = "outputs/playtest-stage2-checks.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static readonly List<string> jumps = new List<string>();

    static void Require(bool value, string message)
    {
        assertions++;
        if (!value) { File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("PLAYTEST STAGE 2: " + message); }
    }
    static void Log(string line) => File.AppendAllText(Result, line + "\n");

    struct Frame { public float time, turn, move, bodyGap; public string state; }

    // Samples the rendered camera once per frame until done() or timeout.
    static IEnumerator Record(List<Frame> frames, Func<bool> done, float timeout, Transform body = null)
    {
        var cam = Camera.main.transform; var p = cam.position; var r = cam.rotation; float start = Time.time;
        while (!done() && Time.time - start < timeout)
        {
            yield return null;
            frames.Add(new Frame
            {
                time = Time.time, turn = Quaternion.Angle(r, cam.rotation), move = Vector3.Distance(p, cam.position),
                bodyGap = body == null ? 0 : Mathf.Abs(Mathf.DeltaAngle(body.eulerAngles.y, cam.eulerAngles.y)),
                state = Describe(),
            });
            p = cam.position; r = cam.rotation;
        }
    }

    // What the worst frame was showing: active virtual camera, camera and player positions.
    static string Describe()
    {
        var brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
        var player = UnityEngine.Object.FindFirstObjectByType<StarterAssets.CharacterController>().transform;
        string vcam = brain != null && brain.enabled ? brain.ActiveVirtualCamera?.Name : "brain off";
        return $"vcam={vcam} cam={Camera.main.transform.position:F2} player={player.position:F2}";
    }

    static void Report(string label, List<Frame> frames, float from, float zero)
    {
        var window = frames.Where(f => f.time >= from).ToList();
        Require(window.Count > 10, label + " must record frames.");
        float dt = (window.Last().time - window.First().time) / window.Count;
        var turn = window.OrderByDescending(f => f.turn).First(); var move = window.OrderByDescending(f => f.move).First();
        Log($"{label}: {window.Count} frames (~{1 / Mathf.Max(dt, 1e-4f):0} fps); worst turn {turn.turn:0.0}° at {turn.time - zero:+0.00;-0.00}s, " +
            $"worst move {move.move * 100:0.0} cm at {move.time - zero:+0.00;-0.00}s");
        int at = frames.IndexOf(move);
        if (move.move >= .15f && at > 0) { Log("  before jump: " + frames[at - 1].state); Log("  after jump:  " + move.state); }
        // A jump is far beyond what one frame of authored motion moves: 12°, 15 cm. Collected so every case still runs.
        assertions++;
        if (turn.turn >= 12f || move.move >= .15f) jumps.Add(label);
    }

    [MenuItem("Senzora/Doğrulama/Oyun Testi Aşama 2 Play")]
    public static void Run()
    {
        Require(Application.isPlaying && !GameManager.Instance.isGameStarted, "Fresh Play at the main menu is required.");
        assertions = 0; jumps.Clear(); Directory.CreateDirectory("outputs"); File.WriteAllText(Result, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
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

        // 1. Intro -> control handoff: the last 3 s of the opening and 2 s of free play.
        menu.PlayGame(); yield return null;
        typeof(MainMenuManager).GetMethod("ChooseTutorial").Invoke(menu, new object[] { false });
        var intro = new List<Frame>(); float control = 0;
        yield return Record(intro, () =>
        {
            if (control == 0 && game.isGameStarted && !movement.IsFrozen) control = Time.time;
            return control > 0 && Time.time > control + 2;
        }, 150);
        Require(control > 0, "Opening must finish with player control.");
        var player = movement.transform;
        Report("intro handoff", intro, control - 3, control);
        // Keep the rest deterministic: no random spawns or breaker trips.
        GlobalEnemyManager.Instance.stopAllEnemies = true;
        BreakerBox.Instance.isSystemActive = false;
        yield return new WaitForSeconds(.5f);

        // 2. Forced look to a point straight behind the player (door lesson case): no wobble, body follows, no snap on release.
        var cam = Camera.main.transform;
        var behind = new GameObject("Stage2 Behind Point").transform;
        behind.position = cam.position - Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 3;
        var probe = new GameObject("Stage2 Look Probe").AddComponent<LookAction>();
        probe.lookTarget = behind; probe.headTurnDuration = 1.4f; probe.waitUntilInput = false; probe.autoReleaseTime = 1;
        var look = new List<Frame>(); float lookStart = Time.time;
        probe.Execute();
        bool shot = false;
        yield return Record(look, () =>
        {
            if (!shot && Time.time > lookStart + 1.6f) { shot = true; ScreenCapture.CaptureScreenshot("outputs/playtest-stage2-look-behind.png"); }
            return Time.time > lookStart + 4f;
        }, 6, player);
        Log($"look behind: max body/view gap {look.Max(f => f.bodyGap):0.0}°, hold wobble {look.Where(f => f.time > lookStart + 1.5f && f.time < lookStart + 2.3f).Max(f => f.turn):0.00}°/frame");
        Report("look behind", look, lookStart, lookStart);
        assertions += 2;
        if (look.Max(f => f.bodyGap) >= 62) jumps.Add("look behind: body left behind the view");
        if (look.Where(f => f.time > lookStart + 1.5f && f.time < lookStart + 2.3f).Max(f => f.turn) >= .5f) jumps.Add("look behind: hold wobble");
        UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(behind.gameObject);
        yield return new WaitForSeconds(.5f);

        // 3. Hiding spot exit.
        var spot = UnityEngine.Object.FindObjectsByType<InteractableHidingSpot>(FindObjectsSortMode.None)
            .OrderBy(s => Vector3.Distance(s.transform.position, player.position)).First();
        var exit = (Transform)typeof(InteractableHidingSpot).GetField("exitPosition", Private).GetValue(spot);
        var inTransition = typeof(InteractableHidingSpot).GetField("inTransition", Private);
        var physics = player.GetComponent<CharacterController>();
        physics.enabled = false; player.position = exit.position; physics.enabled = true;
        yield return new WaitForSeconds(.3f);
        spot.Interact();
        float deadline = Time.time + 15;
        while ((!spot.IsOccupied || (bool)inTransition.GetValue(spot)) && Time.time < deadline) yield return null;
        Require(spot.IsOccupied, spot.name + " must accept the player.");
        yield return new WaitForSeconds(1);
        var hide = new List<Frame>(); float exitStart = Time.time, done = 0;
        spot.Interact();
        yield return Record(hide, () =>
        {
            if (done == 0 && !spot.IsOccupied && !(bool)inTransition.GetValue(spot)) done = Time.time;
            return done > 0 && Time.time > done + 1.5f;
        }, 15);
        Require(done > 0, spot.name + " exit must finish.");
        Log($"hiding exit ({spot.name}): finished at +{done - exitStart:0.00}s, pitch after {Mathf.DeltaAngle(0, cam.eulerAngles.x):0.0}°");
        Report("hiding exit", hide, exitStart, exitStart);

        Require(jumps.Count == 0, "Camera jumps in: " + string.Join(", ", jumps));
        Log("PASS " + assertions + " assertions " + DateTime.UtcNow.ToString("O"));
        Debug.Log("PLAYTEST STAGE 2 PASS: " + assertions + " assertions.");
    }
}
