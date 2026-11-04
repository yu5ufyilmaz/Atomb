using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// Plays Guderian's behind-the-player death in the real game, saves a frame every 0.25 s and where his head and gun are
// in the camera keys' space of GuderianBehindTimelineSetup (player local: x right, y up, z forward, 1 unit = 3.75 m).
public static class GuderianBehindCaptureChecks
{
    const string Folder = "outputs/guderian-behind-2026-11-04/capture";
    const float FrameStep = .25f, SampleStep = .05f;
    static readonly Vector3 Muzzle = new Vector3(0, .1156f, -.1368f); // Same point as GunAimDriver.Muzzle.
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static bool raw;
    static float frameFrom, frameTo, frameStep;
    const string ArmKey = "GuderianBehindCapture.SessionOnly";

    [MenuItem("Senzora/Doğrulama/Guderian Arka Jumpscare Kareleri")]
    public static void Run() => Start(false, 0, 99, FrameStep);

    // GunAimDriver off: the mocap's own arm, aim line and walk, to lay the camera keys on.
    [MenuItem("Senzora/Doğrulama/Guderian Arka Jumpscare Ham Animasyon")]
    public static void RunRaw() => Start(true, 0, 99, FrameStep);

    // The crawl's end, the look-up and the lock-on, frame by frame.
    [MenuItem("Senzora/Doğrulama/Guderian Arka Jumpscare Kareleri 3-4.5 sn")]
    public static void RunLockOn() => Start(false, 3, 4.5f, 1f / 12);

    // The crawl, frame by frame, against the acted reference.
    [MenuItem("Senzora/Doğrulama/Guderian Arka Jumpscare Kareleri 1.25-3.75 sn")]
    public static void RunCrawl() => Start(false, 1.25f, 3.75f, 1f / 12);

    // Run before entering Play: the next Play turns the save off before any menu can be clicked.
    [MenuItem("Senzora/Doğrulama/Guderian Arka Jumpscare Sonraki Play Kayıtsız")]
    public static void ArmSessionOnly() => SessionState.SetBool(ArmKey, true);

    [InitializeOnLoadMethod]
    static void WatchPlay() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ArmKey, false) || SaveManager.Instance == null) return;
        SessionState.EraseBool(ArmKey);
        SaveManager.Instance.sessionOnly = true;
        Debug.Log("GUDERIAN BEHIND CAPTURE: sessionOnly açık.");
    };

    static void Start(bool rawAnimation, float from, float to, float step)
    {
        raw = rawAnimation; frameFrom = from; frameTo = to; frameStep = step;
        Require(Application.isPlaying && GameManager.Instance != null && SaveManager.Instance != null, "Laboratuvarda Play gerekli.");
        SaveManager.Instance.sessionOnly = true; // gerçek Yeni Oyun yolu kayıt dosyasını silmesin
        GameManager.Instance.StartCoroutine(Flow());
    }

    static IEnumerator Flow()
    {
        yield return EnterGameWithoutTutorial();
        var guderian = GuderianAI.Instance;
        Require(guderian != null && guderian.timelineBehindPlayer != null, "GuderianAI veya arka timeline yok.");
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = true; // başka saldırı araya girmesin
        var player = GameObject.FindGameObjectWithTag("Player").transform;
        PlaceInOpenSpot(player);
        float settle = Time.realtimeSinceStartup + 1;
        while (Time.realtimeSinceStartup < settle) yield return null;

        if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        Directory.CreateDirectory(Folder);
        var director = guderian.timelineBehindPlayer;
        // The view tilted down, as in play: the cinematic must start from exactly this frame (the body follows any yaw within frames).
        player.GetComponent<StarterAssets.CharacterController>().ForceCameraRotation(player.eulerAngles.y, 12);
        for (int i = 0; i < 60; i++) yield return null; // Let the body and the camera come to rest.
        ScreenCapture.CaptureScreenshot($"{Folder}/pre0.png"); // Two gameplay frames apart: the noise floor of the pixel diff.
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot($"{Folder}/pre.png");
        Debug.Log($"GUDERIAN BEHIND CAPTURE: tetik öncesi FOV {Camera.main.fieldOfView:0.##}, bakış {Camera.main.transform.eulerAngles}");
        yield return null;
        Time.captureDeltaTime = 1f / 60; // Every run samples the same moments, whatever the editor's frame rate.
        var type = typeof(GuderianAI).GetNestedType("JumpscareType", BindingFlags.NonPublic);
        typeof(GuderianAI).GetMethod("TriggerPositionedJumpscare", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(guderian, new[] { Enum.Parse(type, "BehindPlayer"), "Saklanmadın." });
        Require(director.state == PlayState.Playing, "Arka timeline başlamadı.");
        Debug.Log($"GUDERIAN BEHIND CAPTURE: tetik anında oyuncu {player.position} yön {player.eulerAngles.y:0.#}");

        var dummy = director.playableAsset.outputs.Select(output => director.GetGenericBinding(output.sourceObject) as Animator)
            .First(animator => animator != null && animator.isHuman);
        var head = dummy.GetBoneTransform(HumanBodyBones.Head);
        var hand = dummy.GetBoneTransform(HumanBodyBones.RightHand);
        var pistol = hand.Find("911pistol");
        var eye = Camera.main.transform;
        var shoulders = (dummy.GetBoneTransform(HumanBodyBones.LeftUpperArm), dummy.GetBoneTransform(HumanBodyBones.RightUpperArm));
        var walk = ((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AnimationTrack>().First(item => director.GetGenericBinding(item) == dummy);
        var body = new StringBuilder("# t | clip time | root pos | root yaw | chest yaw | muzzle pos | barrel yaw pitch | arm reach\n");
        var track = new StringBuilder("# t | camera pos | camera pitch yaw roll | head pos | head yaw pitch from camera | muzzle pos | muzzle yaw pitch | right hand pos | cornered | fov\n");
        var feet = new StringBuilder("# t | floor lift cm | sole above floor L R cm (mesh) | ankle-to-sole L R cm | toes-to-sole L R cm | player root above floor cm\n");
        var extraFrames = new System.Collections.Generic.Queue<float>(new[] { .1f, .2f, .3f, .4f });
        var feetTimes = new System.Collections.Generic.Queue<float>(new[] { .5f, 2, 2.5f, 3.5f, 4.5f });
        bool lineChecked = false;
        var lineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Guderian_GuteNacht_placeholder.wav");
        float nextFrame = frameFrom, nextSample = 0;
        int frames = 0;
        bool everCornered = false;
        while (director.state == PlayState.Playing && Time.timeScale > 0)
        {
            float time = (float)director.time;
            bool cornered = Cornered();
            if (cornered && !everCornered) LogCorner(eye.position, dummy.GetBoneTransform(HumanBodyBones.RightUpperArm).position, time);
            everCornered |= cornered;
            if (raw) foreach (var driver in Resources.FindObjectsOfTypeAll<GunAimDriver>()) driver.enabled = false;
            if (time >= nextSample - .001f)
            {
                var camera = player.InverseTransformPoint(eye.position);
                var euler = (Quaternion.Inverse(player.rotation) * eye.rotation).eulerAngles;
                var headPos = player.InverseTransformPoint(head.position);
                var muzzlePos = player.InverseTransformPoint(pistol.TransformPoint(Muzzle));
                track.AppendLine(string.Join(" | ", F(time), V(camera), V(new Vector3(Signed(euler.x), Signed(euler.y), Signed(euler.z))),
                    V(headPos), Bearing(headPos - camera), V(muzzlePos), Bearing(muzzlePos - camera), V(player.InverseTransformPoint(hand.position)), cornered ? "1" : "0", F(Camera.main.fieldOfView)));
                var clip = walk.GetClips().LastOrDefault(item => item.start <= time + .0001) ?? walk.GetClips().First();
                var chest = Vector3.Cross(player.InverseTransformDirection(shoulders.Item2.position - shoulders.Item1.position), Vector3.up);
                var barrel = player.InverseTransformDirection(-pistol.forward);
                body.AppendLine(string.Join(" | ", F(time), F((float)clip.ToLocalTime(time)), V(player.InverseTransformPoint(dummy.transform.position)),
                    F(Mathf.Atan2(player.InverseTransformDirection(dummy.transform.forward).x, player.InverseTransformDirection(dummy.transform.forward).z) * Mathf.Rad2Deg),
                    F(Mathf.Atan2(chest.x, chest.z) * Mathf.Rad2Deg), V(muzzlePos), Bearing(barrel),
                    F(player.InverseTransformVector(hand.position - shoulders.Item2.position).magnitude)));
                nextSample += SampleStep;
            }
            if (extraFrames.Count > 0 && time >= extraFrames.Peek() - .001f)
                ScreenCapture.CaptureScreenshot($"{Folder}/x{extraFrames.Dequeue().ToString("0.00", Invariant)}.png");
            if (feetTimes.Count > 0 && time >= feetTimes.Peek() - .001f) { feetTimes.Dequeue(); feet.AppendLine(Feet(dummy, player, time)); SideShot(dummy, time); }
            if (!lineChecked && time >= GuderianAI.GuteNachtTime + .3f)
            {
                lineChecked = true;
                var subtitles = GlobalSubtitleManager.Instance;
                Debug.Log($"GUDERIAN BEHIND CAPTURE: {F(time)} sn altyazı {subtitles?.CurrentID} oynuyor={subtitles?.IsPlaying} \"{subtitles?.subtitleText?.text}\", ses klibi mikste={LineAudible(director, lineClip)}");
                ScreenCapture.CaptureScreenshot($"{Folder}/line.png");
            }
            if (time >= nextFrame - .001f && nextFrame <= frameTo + .001f)
            {
                ScreenCapture.CaptureScreenshot($"{Folder}/t{nextFrame.ToString("0.00", Invariant)}.png");
                frames++;
                nextFrame += frameStep;
            }
            yield return null;
        }
        Time.captureDeltaTime = 0;
        File.WriteAllText($"{Folder}/guderian-track.txt", track.ToString());
        File.WriteAllText($"{Folder}/guderian-body.txt", body.ToString());
        File.WriteAllText($"{Folder}/guderian-feet.txt", feet.ToString());

        float end = Time.realtimeSinceStartup + 5;
        while (Time.timeScale != 0 && Time.realtimeSinceStartup < end) yield return null;
        Require(Time.timeScale == 0, "Timeline bitti ama ölüm ekranı açılmadı.");
        end = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < end) yield return null;
        ScreenCapture.CaptureScreenshot($"{Folder}/death-screen.png");
        var reason = DeathUIManager.Instance != null && DeathUIManager.Instance.deathReasonText != null ? DeathUIManager.Instance.deathReasonText.text : "(yok)";
        Debug.Log($"GUDERIAN BEHIND CAPTURE PASS: {(raw ? "ham, " : "")}{frames} kare, süre {director.duration:0.##}, köşeye sıkıştı={everCornered}, ölüm yazısı \"{reason}\".");
    }

    // Same path as ResearchGuidanceChecks.EnterNormalGame, but the tutorial would rewind the death instead of showing the screen.
    static IEnumerator EnterGameWithoutTutorial()
    {
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var game = GameManager.Instance;
        var menu = Object.FindFirstObjectByType<MainMenuManager>();
        var panel = menu != null ? new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject : null;
        if (menu != null && panel != null && panel.activeInHierarchy)
        {
            var sequence = Object.FindFirstObjectByType<InGameMenuController>();
            bool awaitSequence = sequence != null && sequence.enabled;
            menu.PlayGame(); if (menu.IsTutorialChoiceOpen) menu.ChooseTutorial(false);
            float end = Time.realtimeSinceStartup + 90;
            while ((panel.activeInHierarchy || !game.isGameStarted || awaitSequence && sequence.enabled) && Time.realtimeSinceStartup < end) yield return null;
            Require(!panel.activeInHierarchy && game.isGameStarted, "Yeni Oyun geçişi bitmedi.");
        }
        else if (!game.isGameStarted) game.StartGameMode();
    }

    // Open floor with a wall about 3 m ahead: the crawl backs toward it and Guderian walks in from behind.
    static void PlaceInOpenSpot(Transform player)
    {
        int mask = ~(1 << player.gameObject.layer);
        var start = player.position;
        float bestScore = float.MaxValue; Vector3 bestFeet = start; float bestYaw = player.eulerAngles.y;
        var furniture = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(item => item.enabled).Select(item => item.bounds)
            .Where(item => item.size.magnitude > 1 && item.size.magnitude < 8).ToList(); // GunAimDriver's rule
        var lamps = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(item => item.isActiveAndEnabled && item.type != LightType.Directional).ToArray();
        for (int x = -10; x <= 10; x++)
        for (int z = -10; z <= 10; z++)
        {
            var probe = start + new Vector3(x, 0, z) * .6f;
            if (!Physics.Raycast(probe + Vector3.up * 1.5f, Vector3.down, out var floor, 3, mask, QueryTriggerInteraction.Ignore)) continue;
            var feet = floor.point;
            if (feet.y - start.y > .3f || start.y - feet.y > 1 || Physics.CheckCapsule(feet + Vector3.up * .5f, feet + Vector3.up * 1.7f, .4f, mask, QueryTriggerInteraction.Ignore)) continue;
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                float ahead = Clear(feet, forward, mask), behind = Clear(feet, -forward, mask);
                // The crawl ends about 2.5 m out at 30 degrees right, on Guderian's aim line.
                if (behind < 2 || Clear(feet, right, mask) < 1.5f || Clear(feet, -right, mask) < 1 || ahead > 4.5f || ahead < 2.6f
                    || Clear(feet, Quaternion.Euler(0, 30, 0) * forward, mask) < 3.1f) continue;
                // Brightest end of the crawl wins, so the shot, the ceiling and the floor read in the frames.
                float score = -Lit(feet + Quaternion.Euler(0, yaw, 0) * ((GuderianBehindTimelineSetup.ShotPosition + GuderianBehindTimelineSetup.CameraOrigin) * player.lossyScale.x + Vector3.up * .7f), lamps, mask);
                if (score < bestScore && CorridorClear(feet, Quaternion.Euler(0, yaw, 0), player.lossyScale.x, mask, furniture)) { bestScore = score; bestFeet = feet; bestYaw = yaw; }
            }
        }
        Require(bestScore < float.MaxValue, "Açık yer bulunamadı.");
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.SetPositionAndRotation(bestFeet, Quaternion.Euler(0, bestYaw, 0));
        body.enabled = true;
        player.GetComponent<StarterAssets.CharacterController>().ForceCameraRotation(bestYaw, 0); // or the body turns back to the old look
        Debug.Log($"GUDERIAN BEHIND CAPTURE: oyuncu {bestFeet} yön {bestYaw}, ışık {-bestScore:0.#}, öndeki duvar {Clear(bestFeet, player.forward, mask):0.0} m, arka {Clear(bestFeet, -player.forward, mask):0.0} m.");
    }

    // GunAimDriver stops the crawl where the eye touches something or loses sight of Guderian's shoulder; a capture spot must not.
    static bool CorridorClear(Vector3 feet, Quaternion facing, float scale, int mask, System.Collections.Generic.List<Bounds> furniture)
    {
        // The eye along the keyed crawl (it bobs up to ~10 cm on each shove), and his shoulder (metres from the feet) once he aims.
        var shoulder = feet + facing * new Vector3(.6f, 1.39f, .94f);
        var keys = Enumerable.Range(0, 13).Select(step => GuderianBehindTimelineSetup.CrawlPoint(step / 12f)).Append(GuderianBehindTimelineSetup.ShotPosition);
        foreach (var key in keys)
        {
            var eye = feet + facing * ((key + GuderianBehindTimelineSetup.CameraOrigin) * scale);
            var sight = new Ray(eye, shoulder - eye);
            float reach = (shoulder - eye).magnitude;
            if (Physics.CheckCapsule(eye + Vector3.down * .05f, eye + Vector3.up * .12f, .15f, mask, QueryTriggerInteraction.Ignore) || Physics.Linecast(eye, shoulder, mask, QueryTriggerInteraction.Ignore)
                || furniture.Any(item => item.SqrDistance(eye) < .0225f || item.IntersectRay(sight, out float distance) && distance < reach)) return false;
        }
        return true;
    }

    // Guderian's soles from the skinned mesh itself, against the floor under them; and the player's root against its floor.
    static string Feet(Animator dummy, Transform player, float time)
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        foreach (var skin in dummy.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            skin.BakeMesh(mesh); // Comes out at world scale (checked: with the transform's scale it was 0.47 m tall).
            var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            vertices.AddRange(mesh.vertices.Select(vertex => toWorld.MultiplyPoint3x4(vertex)));
            Object.DestroyImmediate(mesh);
        }
        string Column(HumanBodyBones ankleBone, HumanBodyBones toesBone, out float ankleToSole, out float toesToSole)
        {
            var ankle = dummy.GetBoneTransform(ankleBone).position;
            var toes = dummy.GetBoneTransform(toesBone);
            var near = vertices.Where(vertex => vertex.y < ankle.y + .05f && new Vector2(vertex.x - ankle.x, vertex.z - ankle.z).magnitude < .22f).ToList();
            if (near.Count == 0)
            {
                ankleToSole = toesToSole = 0;
                var low = vertices.OrderBy(vertex => vertex.y).First();
                var center = vertices.Aggregate(Vector3.zero, (sum, vertex) => sum + vertex) / vertices.Count;
                return $"? (en alt köşe {low:F3}, köşe ortası {center:F3}, kalça {dummy.GetBoneTransform(HumanBodyBones.Hips).position:F3}, ayak {ankle:F3}, boyut {vertices.Max(v => v.y) - low.y:0.###})";
            }
            var sole = near.OrderBy(vertex => vertex.y).First();
            ankleToSole = (ankle.y - sole.y) * 100; toesToSole = toes != null ? (toes.position.y - sole.y) * 100 : 0;
            return FloorUnder(sole, dummy.transform, player, out float floor) ? F((sole.y - floor) * 100) : "?";
        }
        string left = Column(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, out float leftAnkle, out float leftToes);
        string right = Column(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, out float rightAnkle, out float rightToes);
        var driver = Resources.FindObjectsOfTypeAll<GunAimDriver>().FirstOrDefault();
        string root = FloorUnder(player.position, dummy.transform, player, out float rootFloor) ? F((player.position.y - rootFloor) * 100) : "?";
        return string.Join(" | ", F(time), F((driver != null ? driver.FloorLift : 0) * 100), left + " " + right, F(leftAnkle) + " " + F(rightAnkle), F(leftToes) + " " + F(rightToes), root);
    }

    // A low side view of his feet from a temporary camera: the cinematic camera rarely shows them.
    static void SideShot(Animator dummy, float time)
    {
        var feet = (dummy.GetBoneTransform(HumanBodyBones.LeftFoot).position + dummy.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2;
        var side = Vector3.Cross(Vector3.up, dummy.GetBoneTransform(HumanBodyBones.Hips).forward).normalized;
        var host = new GameObject("Feet Camera") { hideFlags = HideFlags.HideAndDontSave };
        var lens = host.AddComponent<Camera>();
        lens.fieldOfView = 40; lens.nearClipPlane = .05f;
        host.transform.position = feet + side * 1.6f + Vector3.up * .25f;
        host.transform.LookAt(feet + Vector3.up * .15f);
        var target = new RenderTexture(960, 540, 24);
        lens.targetTexture = target;
        lens.Render();
        RenderTexture.active = target;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes($"{Folder}/feet-{time.ToString("0.00", Invariant)}.png", image.EncodeToPNG());
        Object.DestroyImmediate(image); lens.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(host);
    }

    static bool FloorUnder(Vector3 point, Transform dummy, Transform player, out float height)
    {
        var hits = Physics.RaycastAll(point + Vector3.up * .4f, Vector3.down, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .Where(hit => !hit.transform.IsChildOf(dummy) && !hit.transform.IsChildOf(player)).ToArray();
        height = hits.Length > 0 ? point.y + .4f - hits.Min(hit => hit.distance) : 0;
        return hits.Length > 0;
    }

    // Whether the timeline's audio mix is playing the line right now (an input of an audio output, weighted in).
    static bool LineAudible(PlayableDirector director, AudioClip line)
    {
        var graph = director.playableGraph;
        for (int i = 0; i < graph.GetOutputCountByType<AudioPlayableOutput>(); i++)
            if (Audible(graph.GetOutputByType<AudioPlayableOutput>(i).GetSourcePlayable(), line, 0)) return true;
        return false;
    }

    static bool Audible(Playable playable, AudioClip line, int depth)
    {
        if (!playable.IsValid() || depth > 6) return false;
        for (int i = 0; i < playable.GetInputCount(); i++)
        {
            var input = playable.GetInput(i);
            if (!input.IsValid() || playable.GetInputWeight(i) <= 0) continue;
            if (input.IsPlayableOfType<AudioClipPlayable>() && ((AudioClipPlayable)input).GetClip() == line) return true;
            if (Audible(input, line, depth + 1)) return true;
        }
        return false;
    }

    // Rough light reaching a point from the lamps that can see it (relative only).
    static float Lit(Vector3 point, Light[] lamps, int mask) => lamps.Sum(lamp =>
    {
        var toLamp = lamp.transform.position - point;
        float distance = toLamp.magnitude;
        return distance < lamp.range && !Physics.Linecast(point, point + toLamp * Mathf.Max(0, 1 - .3f / distance), mask, QueryTriggerInteraction.Ignore)
            ? lamp.intensity * lamp.color.grayscale / Mathf.Max(distance * distance, 1) : 0;
    });

    static float Clear(Vector3 feet, Vector3 direction, int mask) => new[] { .25f, 1f, 1.6f }
        .Min(height => Physics.Raycast(feet + Vector3.up * height, direction, out var hit, 6, mask, QueryTriggerInteraction.Ignore) ? hit.distance : 6);

    static bool Cornered()
    {
        var driver = Resources.FindObjectsOfTypeAll<GunAimDriver>().FirstOrDefault();
        return driver != null && (bool)typeof(GunAimDriver).GetField("cornered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
    }

    // What GunAimDriver.KeepClearOfRoom hit, so a capture spot can be told apart from a real wall.
    static void LogCorner(Vector3 eye, Vector3 arm, float time)
    {
        // The driver tripped a frame of crawl further on than the clamped eye: probe a few centimetres around it.
        var probes = Enumerable.Range(0, 27).Select(index => eye + new Vector3(index % 3 - 1, index / 3 % 3 - 1, index / 9 - 1) * .02f).ToList();
        var near = probes.SelectMany(probe => Physics.OverlapSphere(probe, .12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            .Select(item => $"{item.name}/{LayerMask.LayerToName(item.gameObject.layer)} {item.bounds.center} {item.bounds.size} {item.GetType().Name} oyuncu-yerel {GameObject.FindGameObjectWithTag("Player").transform.InverseTransformPoint(item.bounds.center)}").Distinct();
        var line = string.Join(",", probes.Select(probe => Physics.Linecast(probe, arm, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? $"{hit.collider.name}/{LayerMask.LayerToName(hit.collider.gameObject.layer)}" : null).Where(name => name != null).Distinct());
        var sight = new Ray(eye, arm - eye);
        var blocking = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(item => item.enabled && item.bounds.size.magnitude > 1 && item.bounds.size.magnitude < 8 && item.bounds.SqrDistance(eye) < 16
                && (item.bounds.SqrDistance(eye) < .0144f || item.bounds.IntersectRay(sight, out float distance) && distance < (arm - eye).magnitude))
            .Select(item => $"{item.name} {item.bounds.size.magnitude:0.0}m");
        // The driver's own survey, taken when the crawl started.
        var driver = Resources.FindObjectsOfTypeAll<GunAimDriver>().First();
        var surveyed = (System.Collections.Generic.List<Bounds>)typeof(GunAimDriver).GetField("furniture", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hits = surveyed.Where(item => item.SqrDistance(driver.Eye.position) < .0144f || item.IntersectRay(new Ray(driver.Eye.position, driver.Arm.position - driver.Eye.position), out float distance) && distance < (driver.Arm.position - driver.Eye.position).magnitude)
            .Select(item => (renderers.FirstOrDefault(renderer => renderer.bounds == item)?.name ?? "?") + $" {item.size.magnitude:0.0}m");
        Debug.Log($"GUDERIAN BEHIND CAPTURE: köşe {F(time)} sn; küre [{string.Join(",", near)}] çizgi {line} eşya [{string.Join(",", blocking)}] sürücü [{string.Join(",", hits)}]");
    }

    static string Bearing(Vector3 direction) =>
        $"{F(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg)} {F(-Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg)}";
    static float Signed(float angle) => angle > 180 ? angle - 360 : angle;
    static string F(float value) => value.ToString("0.###", Invariant);
    static string V(Vector3 value) => $"{F(value.x)} {F(value.y)} {F(value.z)}";
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
