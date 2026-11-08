using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// Plays the desk intro (StartCutSceneMangerTimeline) from the title screen, saves a game frame every 0.5 s plus a side
// view of the player's head, and logs hand-to-head distances and the in-hand notebook's active state.
public static class IntroCaptureChecks
{
    const string Folder = "outputs/intro-capture-2026-11-07";
    const string Result = "Temp/intro-capture.result";
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static bool dense; // real time, a game frame every 0.1 s between 6.5 and 11 s, no side shots

    [MenuItem("Senzora/Doğrulama/Başlangıç Sinematiği Kareleri (gerçek zaman, sık)")]
    public static void RunDense() { dense = true; Run(); }

    [MenuItem("Senzora/Doğrulama/Başlangıç Sinematiği Kareleri")]
    public static void Run()
    {
        Require(Application.isPlaying && GameManager.Instance != null && SaveManager.Instance != null, "Laboratuvarda Play gerekli.");
        SaveManager.Instance.sessionOnly = true; // gerçek Yeni Oyun yolu kayıt dosyasını silmesin
        File.Delete(Result);
        GameManager.Instance.StartCoroutine(Flow());
    }

    static IEnumerator Flow()
    {
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var menu = Object.FindFirstObjectByType<MainMenuManager>();
        var sequence = Object.FindFirstObjectByType<InGameMenuController>();
        Require(menu != null && sequence != null && sequence.introTimeline != null, "MainMenuManager / InGameMenuController / introTimeline yok.");
        var director = sequence.introTimeline;
        var player = sequence.playerAnimator;
        Require(player != null && player.isHuman, "playerAnimator humanoid değil.");
        var timeline = (TimelineAsset)director.playableAsset;
        var notebook = timeline.GetOutputTracks().OfType<ActivationTrack>()
            .Select(track => director.GetGenericBinding(track) as GameObject).FirstOrDefault(go => go != null && go.name == "Placeholder");

        if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        Directory.CreateDirectory(Folder);
        var log = new StringBuilder("# t | clip | L wrist-skull | R wrist-skull | L wrist front | R wrist front | notebook active | camera pos | L wrist pos | R wrist pos\n");
        var ashtray = GameObject.Find("PI_Cigarette_Setup");
        if (ashtray != null)
        {
            var bounds = ashtray.GetComponent<Renderer>().bounds;
            log.AppendLine($"# ashtray bounds centre {V(bounds.center)} size {V(bounds.size)} pos {V(ashtray.transform.position)} euler {V(ashtray.transform.eulerAngles)}");
            CloseUp(bounds, Vector3.up, "ashtray-top");
            CloseUp(bounds, (Vector3.up + Vector3.left).normalized, "ashtray-left");
            CloseUp(bounds, (Vector3.up + Vector3.back).normalized, "ashtray-back");
        }

        if (ashtray != null) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot($"{Folder}/menu.png"); yield return null; }
        menu.PlayGame(); if (menu.IsTutorialChoiceOpen) menu.ChooseTutorial(false);
        float deadline = Time.realtimeSinceStartup + 15;
        while (director.state != PlayState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
        Require(director.state == PlayState.Playing, "Intro timeline başlamadı.");
        Time.captureDeltaTime = dense ? 0 : 1f / 30;

        var head = player.GetBoneTransform(HumanBodyBones.Head);
        var lHand = player.GetBoneTransform(HumanBodyBones.LeftHand);
        var rHand = player.GetBoneTransform(HumanBodyBones.RightHand);
        var animTrack = timeline.GetOutputTracks().OfType<AnimationTrack>().First(track => director.GetGenericBinding(track) == player);
        float nextFrame = 0, nextSample = 0, nextSide = 0;
        var penTimes = new System.Collections.Generic.Queue<float>(new[] { 9.2f, 11f, 12.5f, 13.5f }); // right hand with the pen
        var closeTimes = new System.Collections.Generic.Queue<float>(new[] { 0.2f, 9.5f, 10.4f, 11f, 12.5f, 14.5f });
        var boneTimes = new System.Collections.Generic.Queue<float>(new[] { 7.1f, 7.2f, 7.3f, 7.4f, 7.5f, 7.6f, 7.7f, 9f, 9.5f, 11f });
        var handTimes = new System.Collections.Generic.Queue<float>(new[] { 7.3f, 7.5f, 7.7f, 7.9f, 8.1f, 8.3f, 8.5f, 8.8f, 9.3f, 9.8f, 10.3f, 10.8f, 12f, 14.9f });
        while (director.state == PlayState.Playing)
        {
            float time = (float)director.time;
            if (time >= nextSample - .001f)
            {
                var facing = IntroHandClearance.Facing(player);
                var centre = IntroHandClearance.SkullCentre(player);
                var clip = animTrack.GetClips().LastOrDefault(item => item.start <= time + .0001);
                log.AppendLine(string.Join(" | ", F(time), clip != null ? clip.displayName : "-",
                    F(Vector3.Distance(lHand.position, centre)), F(Vector3.Distance(rHand.position, centre)),
                    F(Vector3.Dot(lHand.position - centre, facing)), F(Vector3.Dot(rHand.position - centre, facing)),
                    notebook != null && notebook.activeInHierarchy ? "1" : "0", V(Camera.main.transform.position), V(lHand.position), V(rHand.position),
                    "cam euler " + V(Camera.main.transform.eulerAngles), "clamp in view " + V(Camera.main.transform.InverseTransformPoint(LeftClamp(player))),
                    "sfx rms " + F(SfxLevel(sequence.sfxSource)), "root euler " + V(sequence.cameraTarget.eulerAngles), "clamp in root cm " + V(sequence.cameraTarget.InverseTransformDirection(LeftClamp(player) - sequence.cameraTarget.position) * 100)));
                nextSample += .25f;
            }
            if (dense)
            {
                if (time >= 6.5f && time <= 11f && time >= nextFrame - .001f) { ScreenCapture.CaptureScreenshot($"{Folder}/d{time.ToString("00.00", Invariant)}.png"); nextFrame = time + .1f; }
                yield return null; continue;
            }
            if (time >= nextFrame - .001f) { ScreenCapture.CaptureScreenshot($"{Folder}/t{time.ToString("00.0", Invariant)}.png"); nextFrame += time >= 13.5f ? .25f : .5f; }
            if (time >= nextSide - .001f) { SideShot(player, head, time); nextSide += 1f; }
            if (boneTimes.Count > 0 && time >= boneTimes.Peek() - .001f)
            {
                boneTimes.Dequeue();
                string B(HumanBodyBones bone) { var t = player.GetBoneTransform(bone); return t != null ? V(t.position) : "null"; }
                var facing = IntroHandClearance.Facing(player); var centre = IntroHandClearance.SkullCentre(player);
                log.AppendLine($"# bones {F(time)} | hand {B(HumanBodyBones.LeftHand)} | indexP {B(HumanBodyBones.LeftIndexProximal)} indexI {B(HumanBodyBones.LeftIndexIntermediate)} indexD {B(HumanBodyBones.LeftIndexDistal)} | middleP {B(HumanBodyBones.LeftMiddleProximal)} middleI {B(HumanBodyBones.LeftMiddleIntermediate)} middleD {B(HumanBodyBones.LeftMiddleDistal)} | head {B(HumanBodyBones.Head)} jaw {B(HumanBodyBones.Jaw)} eyeL {B(HumanBodyBones.LeftEye)} eyeR {B(HumanBodyBones.RightEye)} | facing {V(facing)} mouthEst {V(centre + facing * 0.085f + Vector3.up * -0.06f)}");
            }
            if (handTimes.Count > 0 && time >= handTimes.Peek() - .001f)
            {
                var facing = IntroHandClearance.Facing(player);
                var from = (facing * 0.8f + Vector3.up * 0.4f - Vector3.Cross(Vector3.up, facing) * 0.6f).normalized;
                string stamp = handTimes.Dequeue().ToString("00.0", Invariant);
                CloseUp(new Bounds(lHand.position, Vector3.one * 0.22f), from, $"hand-{stamp}");
                CloseUp(new Bounds(lHand.position, Vector3.one * 0.3f), (-facing * 0.6f + Vector3.up * 0.8f - Vector3.Cross(Vector3.up, facing) * 0.5f).normalized, $"handtop-{stamp}"); // like camera 2: behind-left, above
            }
            if (penTimes.Count > 0 && time >= penTimes.Peek() - .001f)
            {
                var facing = IntroHandClearance.Facing(player); var side = IntroHandClearance.Side(player);
                string stamp = penTimes.Dequeue().ToString("00.0", Invariant);
                CloseUp(new Bounds(rHand.position, Vector3.one * 0.25f), (facing * 0.8f + Vector3.up * 0.5f + side * 0.6f).normalized, $"pen-front-{stamp}");
                CloseUp(new Bounds(rHand.position, Vector3.one * 0.25f), (-facing * 0.3f + Vector3.up * 0.8f + side * 0.6f).normalized, $"pen-top-{stamp}");
                var pen = GameObject.Find("IntroPen"); // tight shots on the grip: from the player's eye and from the thumb side
                if (pen != null)
                {
                    var grip = pen.GetComponentInChildren<Renderer>().bounds.center;
                    CloseUp(new Bounds(grip, Vector3.one * 0.08f), (Camera.main.transform.position - grip).normalized, $"grip-eye-{stamp}");
                    CloseUp(new Bounds(grip, Vector3.one * 0.08f), (-side * 0.7f + Vector3.up * 0.5f + facing * 0.3f).normalized, $"grip-thumb-{stamp}");
                    CloseUp(new Bounds(grip, Vector3.one * 0.08f), (side * 0.7f + Vector3.up * 0.3f + facing * 0.5f).normalized, $"grip-out-{stamp}");
                }
            }
            if (ashtray != null && closeTimes.Count > 0 && time >= closeTimes.Peek() - .001f)
                CloseUp(ashtray.GetComponent<Renderer>().bounds, (Vector3.up * 0.6f + Vector3.back).normalized, $"ashtray-{closeTimes.Dequeue().ToString("00.0", Invariant)}");
            yield return null;
        }
        Time.captureDeltaTime = 0;
        for (int i = 0; i < 12; i++) // stand-up every 0.25 s: the camera must glide, not cut
        {
            float wait = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < wait) yield return null;
            ScreenCapture.CaptureScreenshot($"{Folder}/after{i + 1:00}.png");
            if (ashtray != null && i % 4 == 3) CloseUp(ashtray.GetComponent<Renderer>().bounds, (Vector3.up * 0.6f + Vector3.back).normalized, $"ashtray-after{i + 1:00}");
            log.AppendLine($"after {(i + 1) * .25f:0.00} s | camera {V(Camera.main.transform.position)} {V(Camera.main.transform.eulerAngles)} | game started {GameManager.Instance.isGameStarted}");
        }
        yield return null;
        File.WriteAllText($"{Folder}/intro-track.txt", log.ToString());
        dense = false;
        File.WriteAllText(Result, "DONE");
        Debug.Log("INTRO CAPTURE DONE");
    }

    // Right-side view of the head and hands, independent of the cinematic camera.
    static void SideShot(Animator player, Transform head, float time)
    {
        var side = Vector3.Cross(Vector3.up, player.transform.forward).normalized;
        var host = new GameObject("Head Camera") { hideFlags = HideFlags.HideAndDontSave };
        var lens = host.AddComponent<Camera>();
        lens.fieldOfView = 45; lens.nearClipPlane = .05f;
        host.transform.position = head.position + side * 1.3f + player.transform.forward * .4f;
        host.transform.LookAt(head.position - Vector3.up * .2f);
        var target = new RenderTexture(960, 540, 24);
        lens.targetTexture = target;
        lens.Render();
        RenderTexture.active = target;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes($"{Folder}/side-{time.ToString("00.0", Invariant)}.png", image.EncodeToPNG());
        Object.DestroyImmediate(image); lens.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(host);
    }

    static void CloseUp(Bounds bounds, Vector3 from, string name)
    {
        var host = new GameObject("Close Camera") { hideFlags = HideFlags.HideAndDontSave };
        var lens = host.AddComponent<Camera>();
        lens.fieldOfView = 30; lens.nearClipPlane = .02f;
        host.transform.position = bounds.center + from * (bounds.size.magnitude * 1.6f);
        host.transform.LookAt(bounds.center, Vector3.forward);
        var target = new RenderTexture(960, 960, 24);
        lens.targetTexture = target;
        lens.Render();
        RenderTexture.active = target;
        var image = new Texture2D(960, 960, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 960), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes($"{Folder}/{name}.png", image.EncodeToPNG());
        Object.DestroyImmediate(image); lens.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(host);
    }

    static string F(float value) => value.ToString("0.00", Invariant);
    static float SfxLevel(AudioSource source)
    {
        if (source == null) return -1;
        var samples = new float[512]; source.GetOutputData(samples, 0);
        return Mathf.Sqrt(samples.Average(sample => sample * sample));
    }
    static Vector3 LeftClamp(Animator player) { IntroHandClearance.Grip(player, out var clamp, out _, out _); return clamp; }
    static string V(Vector3 value) => $"({F(value.x)} {F(value.y)} {F(value.z)})";
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
