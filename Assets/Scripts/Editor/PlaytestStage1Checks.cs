using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

// 3 Kasım oyun testi notları, aşama 1: menü butonu pozları, Turing bakış noktası, Guderian yürüyüş geçişi.
public static class PlaytestStage1Checks
{
    const string Result = "outputs/playtest-stage1-checks.txt";
    const string GuderianController = "Assets/Animations/Adam/Guderian_Enemy.controller";
    static int assertions;

    static void Require(bool value, string message)
    {
        assertions++;
        if (!value) { File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("PLAYTEST STAGE 1: " + message); }
    }
    static void Log(string line) => File.AppendAllText(Result, line + "\n");

    [MenuItem("Senzora/Doğrulama/Oyun Testi Aşama 1 Play")]
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

        foreach (string name in new[] { "PlayButton", "Settings", "QuitButton" })
            yield return MenuButton(name);

        var game = GameManager.Instance;
        var movement = UnityEngine.Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        menu.PlayGame(); yield return null;
        typeof(MainMenuManager).GetMethod("ChooseTutorial").Invoke(menu, new object[] { false });
        float deadline = Time.realtimeSinceStartup + 120;
        while ((!game.isGameStarted || movement.IsFrozen) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(game.isGameStarted && !movement.IsFrozen, "Opening must finish with player control.");
        // Keep the test deterministic: no random spawns or breaker trips while measuring.
        GlobalEnemyManager.Instance.stopAllEnemies = true;
        BreakerBox.Instance.isSystemActive = false;

        yield return TuringLook();
        yield return GuderianWalk(movement.transform, before: true);
        yield return GuderianWalk(movement.transform, before: false);

        Log("PASS " + assertions + " assertions " + DateTime.UtcNow.ToString("O"));
        Debug.Log("PLAYTEST STAGE 1 PASS: " + assertions + " assertions.");
    }

    static IEnumerator MenuButton(string name)
    {
        var go = GameObject.Find("UI/Canvas/MainMenuPanel/" + name);
        Require(go != null && go.activeInHierarchy, name + " must be visible on the title menu.");
        var thin = go.transform.Find("TextThin").GetComponent<TMP_Text>();
        var bold = go.transform.Find("TextBold").GetComponent<TMP_Text>();
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        Require(go.GetComponent<Button>().navigation.mode == Navigation.Mode.None, name + " must not keep mouse selection.");

        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(.8f);
        Log($"{name} hover thin={thin.color.a:0.00} bold={bold.color.a:0.00}");
        Require(bold.color.a > .95f && thin.color.a < .05f, name + " hover must show only the bold label.");
        if (name == "PlayButton") { ScreenCapture.CaptureScreenshot("outputs/playtest-stage1-menu-hover.png"); yield return null; }

        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSecondsRealtime(.4f);
        Log($"{name} pressed thin={thin.color.a:0.00} bold={bold.color.a:0.00}");
        Require(bold.color.a > .95f && thin.color.a < .05f, name + " press must keep the hover pose.");
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
        Require(EventSystem.current.currentSelectedGameObject != go, name + " press must not leave the button selected.");
        yield return new WaitForSecondsRealtime(.3f);
        Require(bold.color.a > .95f, name + " release while hovering keeps the hover pose.");

        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerExitHandler);
        yield return new WaitForSecondsRealtime(.8f);
        Log($"{name} exit thin={thin.color.a:0.00} bold={bold.color.a:0.00}");
        Require(bold.color.a < .05f && thin.color.a > .95f, name + " leaving must restore the thin label.");
    }

    static IEnumerator TuringLook()
    {
        var tutorial = Resources.FindObjectsOfTypeAll<LookAction>().Single(l => l.gameObject.scene.IsValid() && l.name == "Tutorail_LeesGone_009");
        Require(tutorial.lookTarget != null && tutorial.lookTarget.name == "LookPoint" && tutorial.lookTarget.parent.name == "TuringMachine",
            "Turing tutorial look must aim at the machine's raised look point.");
        foreach (var (label, target) in new[] { ("old", tutorial.lookTarget.parent), ("new", tutorial.lookTarget) })
        {
            var probe = new GameObject("Turing Look Probe").AddComponent<LookAction>();
            probe.lookTarget = target; probe.headTurnDuration = 1; probe.waitUntilInput = false; probe.autoReleaseTime = 1.5f;
            probe.Execute();
            yield return new WaitForSeconds(1.4f);
            float pitch = Mathf.DeltaAngle(0, Camera.main.transform.eulerAngles.x);
            Log($"turing look {label}: target y={target.position.y:0.00} camera pitch={pitch:0.0} (positive = down)");
            ScreenCapture.CaptureScreenshot("outputs/playtest-stage1-turing-" + label + ".png");
            yield return null; yield return null;
            UnityEngine.Object.Destroy(probe.gameObject);
            yield return new WaitForSeconds(.4f);
        }
    }

    static IEnumerator GuderianWalk(Transform player, bool before)
    {
        var ai = GuderianAI.Instance; var agent = ai.GetComponent<NavMeshAgent>(); var animator = ai.animator;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GuderianController);
        var blends = controller.layers[0].stateMachine.states.SelectMany(s => s.state.transitions)
            .Where(t => t.destinationState != null && (t.destinationState.name == "AdamWalk" || t.destinationState.name == "New Animation")).ToArray();
        Require(blends.Length == 2, "Guderian idle/walk transitions must exist.");
        float savedBlend = blends[0].duration, savedTurn = agent.angularSpeed;
        if (before) { foreach (var t in blends) t.duration = 1.03f; agent.angularSpeed = 120; }
        ai.enabled = false; animator.gameObject.SetActive(true);
        var cam = new GameObject("Guderian Walk Probe Camera").AddComponent<Camera>(); cam.fieldOfView = 45; cam.enabled = false;
        var target = new RenderTexture(320, 240, 24, RenderTextureFormat.ARGB32); target.Create();
        var frames = new List<Texture2D>(); float maxBlend = 0, minSway = 999, maxSway = -999;
        try
        {
            Require(NavMesh.SamplePosition(player.position + player.forward * 2f, out var a, 3, NavMesh.AllAreas), "NavMesh start point near the player.");
            Vector3 side = Vector3.Cross(Vector3.up, player.forward).normalized;
            Require(NavMesh.SamplePosition(a.position + side * 3.5f, out var b, 3, NavMesh.AllAreas), "NavMesh end point near the player.");
            agent.enabled = true; // Hidden Guderian keeps his agent off.
            agent.Warp(b.position); agent.isStopped = false; agent.speed = ai.walkSpeed;
            animator.SetFloat("Speed", 0); yield return new WaitForSeconds(1.2f);
            Vector3 mid = (a.position + b.position) / 2, normal = Vector3.Cross(Vector3.up, (b.position - a.position).normalized);
            if (Vector3.Dot(normal, player.position - mid) < 0) normal = -normal;
            cam.transform.position = mid + normal * 3.2f + Vector3.up * 1.3f;
            cam.transform.LookAt(mid + Vector3.up * .9f);
            // Leg A: walk 3.5 m and stop, like a patrol leg; 20 frames at 0.2 s.
            agent.SetDestination(a.position);
            for (int i = 0; i < 20; i++)
            {
                float t = 0;
                while (t < .2f)
                {
                    t += Time.deltaTime;
                    animator.SetFloat("Speed", agent.velocity.magnitude);
                    if (animator.IsInTransition(0)) maxBlend = Mathf.Max(maxBlend, animator.GetAnimatorTransitionInfo(0).duration);
                    if (agent.velocity.magnitude > .5f)
                    {
                        float sway = Mathf.DeltaAngle(animator.transform.eulerAngles.y, animator.bodyRotation.eulerAngles.y);
                        minSway = Mathf.Min(minSway, sway); maxSway = Mathf.Max(maxSway, sway);
                    }
                    yield return null;
                }
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(cam, request);
                var previous = RenderTexture.active; RenderTexture.active = target;
                var frame = new Texture2D(320, 240, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, 320, 240), 0, 0); frame.Apply(); RenderTexture.active = previous;
                frames.Add(frame);
            }
            string label = before ? "before" : "after";
            Log($"guderian {label}: longest idle/walk blend={maxBlend:0.00}s, body yaw vs root while walking {minSway:0.0}..{maxSway:0.0} deg, turn speed={agent.angularSpeed}");
            Require(before ? maxBlend > .9f : maxBlend <= .25f, $"Guderian {label} blend must be measured from the controller ({maxBlend:0.00}s).");
            var sheet = new Texture2D(320 * 5, 240 * 4, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++) sheet.SetPixels(i % 5 * 320, (3 - i / 5) * 240, 320, 240, frames[i].GetPixels());
            File.WriteAllBytes("outputs/playtest-stage1-guderian-" + label + ".png", sheet.EncodeToPNG());
        }
        finally
        {
            foreach (var t in blends) t.duration = savedBlend;
            agent.angularSpeed = savedTurn; agent.isStopped = true;
            UnityEngine.Object.Destroy(cam.gameObject); target.Release();
            foreach (var frame in frames) UnityEngine.Object.Destroy(frame);
        }
    }
}
