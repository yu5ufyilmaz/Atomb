using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Rebuilds the camera beats of TL_GuderianBehindJS. Values live in the camera track's space:
// x = player right, y = up, z = player forward, 1 unit = 3.75 m. Re-running overwrites the same keys.
public static class GuderianBehindTimelineSetup
{
    const string TimelinePath = "Assets/CutScenes/TL_GuderianBehindJS.playable";
    const string GuteNachtVoicePath = "Assets/Sounds/Guderian_GuteNacht_placeholder.wav"; // Placeholder TTS: replace with a real recording at the same path.
    const string PunchPath = "Assets/Sounds/Sound Fx/Guderian/Guderian_Punch_placeholder.wav"; // Generated placeholder: replace with a real recording at the same path.
    const string FootstepPath = "Assets/Sounds/Sound Fx/Guderian/Guderian_Footstep_{0}.wav"; // 1..5
    const string CrawlPath = "Assets/Sounds/Sound Fx/Player/Player_Sit_Stand.wav";
    const float PunchHitIn = .2f; // The generated file's hit sits at 0.20 s.
    const float CrawlPeak = .25f; // Player_Sit_Stand's energy peak: it lands on each shove's effort.
    // Guderian's foot contacts in timeline seconds: foot-contact sampling of Guderian_behind.fbx, 2026-11-06 (R .933 dropped, a flam 17 ms before L .95).
    static readonly float[] StepTimes = { .43f, .95f, 1.57f, 2.53f, 3.2f, 3.67f };
    // Beats follow the acted reference (2026-11-04): calm, whip turn, punch, a hard fall, crawl back, look up, a long held aim, shot.
    // Camera keys are laid on Guderian_behind.fbx as sampled in Play (Senzora/Doğrulama/Guderian Arka Jumpscare Ham Animasyon).
    const float WalkStart = .05f;
    const float WalkClipIn = 2.15f; // Clip time at WalkStart: the fist reaches the face at PunchTime.
    const float FreezeClip = 6.2f; // Arm fully extended at the man on the floor; the clip lifts the gun overhead from 6.3.
    const float TurnStart = .4f, TurnEnd = .55f; // Calm, then a whip turn onto him.
    const float PunchTime = .75f;
    const float PunchLeadIn = .25f; // From the first audio clip's start to its hit.
    const float LandTime = 1.15f; // The head hits the floor.
    const float DazeEnd = 1.42f;
    const float CrawlStart = 1.5f;
    const float CrawlEnd = 3.5f;
    const float LookUpEnd = 3.92f; // Propped up on the elbows to face him.
    const float TorsoDrop = .06f; // Neck and chest, below the sampled head: the chin-tucked look up at him while crawling.
    const float AimEnd = WalkStart + FreezeClip - WalkClipIn; // He stops, aimed; the clip holds this frame until he fires.
    const float AimStart = AimEnd - 1; // The aim correction eases in while the mocap brings the arm up on its own.
    // CrawlEnd to AimEnd (0.6 s): the look slides from his torso onto gun and face while the lens pushes in, one move, no jump.
    const float HoldTowardFace = .8f; // Keyed look between gun (0) and face (1).
    const float FocusHold = .4f; // GunAimDriver's muzzle lock then pulls about 3 degrees and the lens is part way to 38.
    const float ShotTime = 7f; // A long held beat down the barrel, then the shot.
    const float ImpactFrameEnd = ShotTime + .0833f;
    const float EyesCloseStart = ShotTime + .3f;
    const float EyesClosed = ShotTime + 1.3f;
    const float End = ShotTime + 1.8f; // Face down on the floor; the eyes fail before the death screen.
    const float VoiceLength = 2.654626f; // The last audio clip's full length.
    const float VoiceLeadIn = .3f; // From the last audio clip's start to its gunshot transient.
    const float AimHoldSpan = .03f; // Clip seconds the frozen arm drifts through until he fires.
    const float TremorStep = .2f;
    // Scooting away on elbows and heels: a quick shove (head dips and rolls to the pushing side), a slide back,
    // then the head comes up and finds him again. Shares of the ~1.5 m crawl path, one per shove.
    static readonly float[] PushShares = { .36f, .34f, .3f };
    const float PushStart = CrawlStart + .06f, PushEvery = .7f, Effort = .12f, Slide = .3f;
    const float FloorHeight = -.379f;
    const float CrawlHeight = -.32f; // Head propped on the elbows, about 39 cm off the floor.
    const float PropHeight = -.305f; // Pushed up a little more to face the gun, about 45 cm.
    const float EffortDip = .011f; // 4 cm
    const float SlideDip = .004f;
    const float PushRoll = 4f;
    const float PushNod = 6f;
    const float GlanceDown = 45f; // Once, between shoves, down at his own feet (the reference does).
    const float FaceDownHeight = -.382f; // Eyes about 16 cm off the floor: GunAimDriver's 12 cm sphere must not touch it.
    internal static readonly Vector3 CameraOrigin = new Vector3(0, .424f, .0587f);
    // The frozen aim, sampled at FreezeClip without GunAimDriver: muzzle and barrel heading.
    static readonly Vector3 MuzzleFrozen = new Vector3(.221f, .202f, .271f);
    const float BarrelYaw = 20.25f;
    const float AimDistance = .35f; // Muzzle to eye, about 1.3 m: on his aim line the correction is a small dip of the arm.
    internal static readonly Vector3 ShotPosition = OnAimLine(PropHeight);
    static readonly Vector3 CrawlTo = OnAimLine(CrawlHeight) - AimLine() * .03f;
    static readonly Vector3 LandedPosition = new Vector3(.04f, FloorHeight, .215f); // Knocked onto his back, about 1.5 m of crawl short of the aim line.
    static readonly Vector3 CrawlFrom = new Vector3(LandedPosition.x, CrawlHeight, LandedPosition.z + .005f);
    // Bends the crawl: straight back while he walks +z, then off to +x with him, staying ahead of him all the way.
    static readonly Vector3 CrawlBend = new Vector3(.06f, CrawlHeight, .4f);
    static readonly Vector3 HoldDrift = new Vector3(0, -.004f, .012f); // Shrinking away from the barrel.
    static readonly Vector3 MuzzleAimed = new Vector3(.221f, .21f, .271f); // Where GunAimDriver's correction leaves the muzzle.
    // Guderian's head in this space against Guderian_behind.fbx time.
    static readonly (float Clip, Vector3 Head)[] HeadTrack =
    {
        (2.35f, new Vector3(-.035f, .407f, -.145f)), (2.60f, new Vector3(-.004f, .381f, -.158f)),
        (2.85f, new Vector3(-.090f, .426f, -.101f)), (3.10f, new Vector3(-.113f, .368f, -.110f)),
        (3.35f, new Vector3(-.095f, .382f, -.083f)), (3.60f, new Vector3(-.056f, .390f, -.070f)),
        (3.85f, new Vector3(-.022f, .402f, -.069f)), (4.10f, new Vector3(.004f, .395f, -.056f)),
        (4.35f, new Vector3(.023f, .398f, -.039f)), (4.60f, new Vector3(.023f, .387f, -.012f)),
        (4.85f, new Vector3(.008f, .400f, .020f)), (5.10f, new Vector3(.011f, .416f, .044f)),
        (5.35f, new Vector3(.025f, .408f, .066f)), (5.60f, new Vector3(.058f, .390f, .099f)),
        (5.85f, new Vector3(.089f, .390f, .119f)), (6.10f, new Vector3(.106f, .380f, .133f)),
        (6.20f, new Vector3(.109f, .378f, .138f)),
    };

    static Vector3 AimLine() => Quaternion.Euler(0, BarrelYaw, 0) * Vector3.forward;

    // A camera key AimDistance down his barrel's heading, the eyes at the given key height.
    static Vector3 OnAimLine(float height)
    {
        float rise = MuzzleFrozen.y - (CameraOrigin.y + height);
        var eye = MuzzleFrozen + AimLine() * Mathf.Sqrt(AimDistance * AimDistance - rise * rise);
        return new Vector3(eye.x - CameraOrigin.x, height, eye.z - CameraOrigin.z);
    }

    struct Pose
    {
        public float Time; public Vector3 Position, Euler;
        public Pose(float time, Vector3 position, Vector3 euler) { Time = time; Position = position; Euler = euler; }
    }

    [MenuItem("Senzora/Jumpscare Blockout/Guderian Behind Timeline'ını Uygula")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (timeline == null) { Debug.LogError("Timeline bulunamadı: " + TimelinePath); return; }
        var tracks = timeline.GetOutputTracks().ToDictionary(track => track.name);
        var camera = ((AnimationTrack)tracks["Animation Track (1)"]).infiniteClip;
        var blackBar = ((AnimationTrack)tracks["Animation Track (2)"]).infiniteClip;
        var blackBarActive = tracks["Activation Track (2)"];
        WriteCamera(camera);
        WriteBlackBar(blackBar);
        // The track had no clip, so BlackBar never showed in this timeline.
        var active = blackBarActive.GetClips().FirstOrDefault() ?? blackBarActive.CreateDefaultClip();
        active.start = EyesCloseStart; active.duration = End - EyesCloseStart;
        // These hold until the end; the first version of this tool cut them at 5.3.
        foreach (var name in new[] { "Activation Track", "Cinemachine Track" })
        {
            var last = tracks[name].GetClips().OrderBy(clip => clip.end).Last();
            last.duration = End - last.start;
        }
        // A hard cut at 0 to the cinematic camera, which GunAimDriver holds on the player's own frame until the turn:
        // the old blend from the gameplay camera changed lens and view before anything had happened.
        var shots = tracks["Cinemachine Track"].GetClips().OrderBy(clip => clip.end).ToList();
        foreach (var gameplayShot in shots.Take(shots.Count - 1)) timeline.DeleteClip(gameplayShot);
        shots.Last().start = 0; shots.Last().duration = End;
        // The player's own body stays until the whip turn hides it.
        tracks["Activation Track (1)"].GetClips().First().duration = TurnStart + .05f;
        WriteGuderian((AnimationTrack)tracks["Animation Track"]);
        tracks["Activation Track (3)"].GetClips().First().start = ShotTime; // Impact frame.
        var audio = (AudioTrack)tracks["Audio Track"];
        var line = AssetDatabase.LoadAssetAtPath<AudioClip>(GuteNachtVoicePath);
        if (line == null) { Debug.LogError("GUDERIAN BEHIND INVALID: ses yok: " + GuteNachtVoicePath); return; }
        var lineClip = audio.GetClips().FirstOrDefault(clip => ((AudioPlayableAsset)clip.asset).clip == line) ?? audio.CreateClip(line);
        var voices = audio.GetClips().Where(clip => clip != lineClip).OrderBy(clip => clip.start).ToList();
        voices[0].start = PunchTime - PunchLeadIn;
        var voice = voices.Last();
        voice.start = ShotTime - VoiceLeadIn;
        voice.duration = Mathf.Min(VoiceLength, End - (float)voice.start);
        // "Gute Nacht." in the held aim; GuderianAI shows its subtitle at the same time. It must end before the shot clip or the two would crossfade.
        lineClip.start = GuderianAI.GuteNachtTime; lineClip.clipIn = 0; lineClip.displayName = "Gute Nacht (placeholder)";
        lineClip.duration = Mathf.Min(line.length, (float)voice.start - GuderianAI.GuteNachtTime - .02f);
        // Overlapping clips on one AudioTrack crossfade, so the foley sits on its own tracks, played by the same AudioSource.
        var punch = AssetDatabase.LoadAssetAtPath<AudioClip>(PunchPath);
        if (punch == null) { Debug.LogError("GUDERIAN BEHIND INVALID: ses yok: " + PunchPath); return; }
        var crawl = AssetDatabase.LoadAssetAtPath<AudioClip>(CrawlPath);
        var steps = Enumerable.Range(1, 5).Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(string.Format(FootstepPath, n))).ToArray();
        if (crawl == null || steps.Contains(null)) { Debug.LogError("GUDERIAN BEHIND INVALID: ses yok: " + CrawlPath + " / " + FootstepPath); return; }
        var foley = new List<(string, AudioClip, float, float)> { ("Punch", punch, PunchTime - PunchHitIn, 1) };
        foley.AddRange(Enumerable.Range(0, PushShares.Length).Select(i => ($"Crawl {i + 1}", crawl, PushStart + i * PushEvery + Effort - CrawlPeak, .75f)));
        LayAudio(timeline, audio, "Audio Track (Foley)", foley);
        LayAudio(timeline, audio, "Audio Track (Steps)", StepTimes.Select((time, i) => ($"Step {i + 1}", steps[i % 5], time, .6f)).ToList());
        var damageTrack = timeline.GetOutputTracks().OfType<DamageFxTrack>().FirstOrDefault() ?? timeline.CreateTrack<DamageFxTrack>(null, "Damage FX");
        var damage = damageTrack.GetClips().FirstOrDefault() ?? damageTrack.CreateClip<DamageFxClip>();
        damage.start = 0; damage.duration = End; damage.displayName = "Damage FX";
        WriteDamage((DamageFxClip)damage.asset);
        var aimTrack = timeline.GetOutputTracks().OfType<GunAimTrack>().FirstOrDefault() ?? timeline.CreateTrack<GunAimTrack>(null, "Gun Aim");
        var aim = aimTrack.GetClips().FirstOrDefault() ?? aimTrack.CreateClip<GunAimClip>();
        aim.start = 0; aim.duration = End; aim.displayName = "Gun Aim";
        var aimAsset = (GunAimClip)aim.asset;
        aimAsset.crawlStart = CrawlStart; aimAsset.shotTime = ShotTime;
        aimAsset.weight = Keys(0, 0, AimStart, 0, AimEnd + .2f, 1, ShotTime + .05f, 1, ShotTime + .7f, 0, End, 0);
        aimAsset.focus = Keys(0, 0, CrawlEnd, 0, AimEnd, FocusHold, ShotTime, FocusHold, ImpactFrameEnd, 0, End, 0);
        aimAsset.startView = Keys(0, 1, TurnStart, 1, TurnEnd, 0, End, 0);
        EditorUtility.SetDirty(aim.asset); EditorUtility.SetDirty(damage.asset); EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(camera); EditorUtility.SetDirty(blackBar);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        if (Mathf.Abs((float)timeline.duration - End) > .01f) Debug.LogError($"GUDERIAN BEHIND INVALID: süre {timeline.duration:0.###}, beklenen {End}.");
        else Debug.Log($"GUDERIAN BEHIND APPLIED: {timeline.duration:0.###} sn, atış {ShotTime:0.##}.");
    }

    static List<Pose> BuildPoses()
    {
        var facing = Look(TurnEnd, new Vector3(0, -.004f, 0), 2);
        float face = facing.Euler.y; // Straight at him; the hook and the fall turn the head away from here.
        var poses = new List<Pose>
        {
            new Pose(0, Vector3.zero, new Vector3(.5f, 0, 0)),
            new Pose(TurnStart, Vector3.zero, new Vector3(.5f, 0, 0)),
            new Pose(TurnStart + .09f, new Vector3(0, -.003f, 0), new Vector3(4, face * .85f, 4)), // The whip, still blurred.
            facing,
            Look(PunchTime - .05f, new Vector3(0, -.006f, -.004f), 0, -3), // He fills the frame, winding up.
            // The right hook crosses the face toward -x: the head snaps that way and he goes straight down.
            new Pose(PunchTime, new Vector3(-.02f, -.012f, .008f), new Vector3(13, face + 40, -4)),
            new Pose(PunchTime + .11f, new Vector3(-.02f, -.09f, .06f), new Vector3(2, face + 58, -18)),
            new Pose(PunchTime + .25f, new Vector3(0, -.25f, .14f), new Vector3(-24, face + 74, -30)),
            new Pose(LandTime, new Vector3(LandedPosition.x, FloorHeight - .006f, LandedPosition.z), new Vector3(-52, face + 84, -32)),
            new Pose(LandTime + .06f, new Vector3(LandedPosition.x, FloorHeight + .01f, LandedPosition.z), new Vector3(-44, face + 80, -26)), // Bounce.
            new Pose(DazeEnd, LandedPosition, new Vector3(-42, face + 66, -20)),
            Look(CrawlStart, CrawlFrom + Vector3.down * .025f, -6), // Half up on the elbows, eyes back on him.
        };
        float travelled = 0;
        for (int push = 0; push < PushShares.Length; push++)
        {
            float start = PushStart + push * PushEvery, side = push % 2 == 0 ? 1 : -1; // Alternating elbows.
            var from = CrawlPoint(travelled);
            var to = CrawlPoint(travelled += PushShares[push]);
            // Effort: the head dips, rolls to the pushing side, the eyes drop a little but stay on him.
            poses.Add(Look(start + Effort, from + Vector3.down * EffortDip, side * PushRoll, PushNod, side));
            // The slide back, still low.
            poses.Add(Look(start + Effort + Slide, to + Vector3.down * SlideDip, side * PushRoll * .3f, PushNod * .4f));
            if (push == 1) poses.Add(Look(start + Effort + Slide + .1f, to + Vector3.down * SlideDip * .5f, 0, GlanceDown));
            // Settle: the head comes back up and finds him; the last settle runs into the rise.
            poses.Add(Look(start + PushEvery, push == PushShares.Length - 1 ? CrawlTo : to, push == PushShares.Length - 1 ? -12 : 0));
        }
        // Lying tilted, the eyes climb onto his gun and face as he walks in on his aim line.
        poses.Add(Look(LookUpEnd, ShotPosition, -22));
        // Frozen down the barrel: GunAimDriver holds part of the look, the keys give the tremor and the roll.
        int steps = Mathf.RoundToInt((ShotTime - AimEnd) / TremorStep);
        for (int step = 0; step < steps; step++)
        {
            float shake = step % 2 == 0 ? 1 : -1;
            var tremor = new Vector3(.0025f * shake, .0015f * Mathf.Sin(step * 2.1f), 0);
            float when = AimEnd + (ShotTime - AimEnd) * step / steps;
            poses.Add(Look(when, ShotPosition + HoldDrift * step / steps + tremor, -14 + 2 * shake, .8f * shake, -.6f * shake));
        }
        var shot = Look(ShotTime, ShotPosition + HoldDrift, -12);
        poses.Add(shot);
        // The bullet throws the head back onto the ceiling, it rolls over and he ends face down.
        float yaw = shot.Euler.y;
        var down = ShotPosition + HoldDrift;
        poses.Add(new Pose(ImpactFrameEnd, down + new Vector3(0, -.008f, .014f), shot.Euler + new Vector3(-14, 4, 8)));
        poses.Add(new Pose(ShotTime + .25f, down + new Vector3(-.006f, -.04f, .03f), new Vector3(-72, yaw + 8, 18)));
        poses.Add(new Pose(ShotTime + .45f, down + new Vector3(-.01f, -.046f, .036f), new Vector3(-78, yaw + 14, 26)));
        poses.Add(new Pose(ShotTime + .72f, down + new Vector3(-.03f, -.048f, .04f), new Vector3(-24, yaw + 50, 55)));
        poses.Add(new Pose(ShotTime + .9f, down + new Vector3(-.05f, -.048f, .05f), new Vector3(36, yaw + 78, 42)));
        // Face down while the eyes still fight (the bar opens again around ShotTime + 1.05).
        poses.Add(new Pose(ShotTime + 1.08f, new Vector3(down.x - .06f, FaceDownHeight, down.z + .055f), new Vector3(70, yaw + 88, 20)));
        poses.Add(new Pose(End, new Vector3(down.x - .062f, FaceDownHeight - .002f, down.z + .056f), new Vector3(74, yaw + 90, 18)));
        return poses;
    }

    internal static Vector3 CrawlPoint(float along)
    {
        float rest = 1 - along;
        return rest * rest * CrawlFrom + 2 * along * rest * CrawlBend + along * along * CrawlTo;
    }

    // Aims at where Guderian actually is: the sampled head, his neck and chest while crawling, his gun and face once he has us.
    static Pose Look(float time, Vector3 position, float roll, float pitchOffset = 0, float yawOffset = 0)
    {
        var head = HeadAt(time);
        var body = time < CrawlStart ? head : head + Vector3.down * TorsoDrop;
        var gunAndFace = Vector3.Lerp(MuzzleAimed, head, HoldTowardFace);
        var target = Vector3.Lerp(body, gunAndFace, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CrawlEnd, AimEnd, time)));
        var direction = target - (position + CameraOrigin);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        if (yaw > 0) yaw -= 360; // Stay on the negative side the turn and the fall use.
        float pitch = -Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
        return new Pose(time, position, new Vector3(pitch + pitchOffset, yaw + yawOffset, roll));
    }

    static Vector3 HeadAt(float time)
    {
        float clip = Mathf.Min(time, AimEnd) - WalkStart + WalkClipIn;
        int next = System.Array.FindIndex(HeadTrack, sample => sample.Clip >= clip);
        if (next <= 0) return HeadTrack[next < 0 ? HeadTrack.Length - 1 : 0].Head;
        var (from, to) = (HeadTrack[next - 1], HeadTrack[next]);
        return Vector3.Lerp(from.Head, to.Head, Mathf.InverseLerp(from.Clip, to.Clip, clip));
    }

    static void WriteCamera(AnimationClip clip)
    {
        var poses = BuildPoses();
        Debug.Assert(poses.Zip(poses.Skip(1), (a, b) => a.Time < b.Time).All(ordered => ordered), "GUDERIAN BEHIND INVALID: kamera anahtarları sıralı değil.");
        Debug.Assert(Mathf.Abs(PushShares.Sum() - 1) < .001f && PushStart + (PushShares.Length - 1) * PushEvery + Effort + Slide <= CrawlEnd, "GUDERIAN BEHIND INVALID: itiş tablosu tutmuyor.");
        for (int axis = 0; axis < 3; axis++)
        {
            string name = "xyz"[axis].ToString();
            int index = axis;
            SetCurve(clip, typeof(Transform), "m_LocalPosition." + name, poses.Select(pose => new Keyframe(pose.Time, pose.Position[index])));
            SetCurve(clip, typeof(Transform), "localEulerAnglesRaw." + name, poses.Select(pose => new Keyframe(pose.Time, pose.Euler[index])));
        }
    }

    // One clip per cue, reusing the track's clips in order; each is cut at the next cue so nothing crossfades.
    static void LayAudio(TimelineAsset timeline, AudioTrack source, string name, List<(string Name, AudioClip Sound, float Start, float Volume)> cues)
    {
        var track = timeline.GetOutputTracks().OfType<AudioTrack>().FirstOrDefault(item => item.name == name) ?? timeline.CreateTrack<AudioTrack>(null, name);
        var clips = track.GetClips().OrderBy(clip => clip.start).ToList();
        foreach (var stale in clips.Skip(cues.Count)) timeline.DeleteClip(stale);
        for (int i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            var clip = i < clips.Count ? clips[i] : track.CreateClip(cue.Sound);
            var asset = (AudioPlayableAsset)clip.asset;
            asset.clip = cue.Sound;
            clip.displayName = cue.Name; clip.start = cue.Start; clip.clipIn = 0;
            clip.duration = Mathf.Min(cue.Sound.length, (i + 1 < cues.Count ? cues[i + 1].Start : End) - cue.Start);
            var properties = new SerializedObject(asset); // Timeline 1.8 keeps the clip volume internal.
            properties.FindProperty("m_ClipProperties.volume").floatValue = cue.Volume;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
        // The binding lives in the scene: the new track plays through the Audio Track's AudioSource.
        bool bound = false;
        foreach (var director in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(item => item.playableAsset == timeline))
        {
            var output = director.GetGenericBinding(source);
            bound |= output != null;
            if (output == null || director.GetGenericBinding(track) == output) continue;
            director.SetGenericBinding(track, output);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            EditorSceneManager.SaveScene(director.gameObject.scene);
        }
        if (!bound) Debug.LogWarning($"GUDERIAN BEHIND: {name} bağlanmadı, scn_LaboratorySynthesis açıkken yeniden uygula.");
    }

    // Walk, freeze on the extended arm, then the same animation carries on after the shot.
    static void WriteGuderian(AnimationTrack track)
    {
        var clips = track.GetClips().OrderBy(clip => clip.start).ToList();
        var walk = clips[0];
        while (clips.Count < 3)
        {
            var added = track.CreateClip(walk.animationClip);
            EditorUtility.CopySerialized(walk.asset, added.asset);
            clips.Add(added);
        }
        walk.start = WalkStart; walk.clipIn = WalkClipIn; walk.duration = AimEnd - WalkStart;
        var hold = clips[1];
        hold.start = AimEnd; hold.duration = ShotTime - AimEnd; hold.clipIn = walk.clipIn + walk.duration; hold.timeScale = AimHoldSpan / hold.duration;
        var after = clips[2];
        after.start = ShotTime; after.duration = End - ShotTime; after.clipIn = hold.clipIn + AimHoldSpan;
    }

    static void SetCurve(AnimationClip clip, System.Type type, string property, IEnumerable<Keyframe> keys)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), Smooth(new AnimationCurve(keys.ToArray())));
    }

    static AnimationCurve Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    // Time, value pairs.
    static AnimationCurve Keys(params float[] pairs)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i < pairs.Length; i += 2) curve.AddKey(pairs[i], pairs[i + 1]);
        return Smooth(curve);
    }

    // The eyes fight to stay open twice, then close for good.
    static void WriteBlackBar(AnimationClip clip)
    {
        var binding = AnimationUtility.GetCurveBindings(clip).First(item => item.propertyName == "m_Color.a");
        const float Close = EyesCloseStart, Fight = EyesClosed - EyesCloseStart;
        AnimationUtility.SetEditorCurve(clip, binding, Keys(0, 0, Close, 0, Close + Fight * .2f, .75f, Close + Fight * .36f, .2f, Close + Fight * .61f, .95f, Close + Fight * .75f, .45f, EyesClosed, 1, End, 1));
    }

    // Punch: white flash, then concussion that clears as adrenaline takes over. Shot: the same kick, then blood loss.
    static void WriteDamage(DamageFxClip clip)
    {
        const float Hit = PunchTime, Windup = PunchTime - .08f, Shot = ShotTime, Bullet = ImpactFrameEnd;
        clip.vignette = Keys(0, 0, Windup, 0, Hit, .7f, 1.4f, .5f, 1.85f, .42f, 2.4f, .34f, 3.4f, .3f, 4.1f, .36f, Shot, .42f, Bullet, .8f, Shot + .7f, .72f, Shot + 1.5f, .95f, End, 1);
        clip.aberration = Keys(0, 0, Windup, 0, Hit, 1, 1.2f, .4f, 2.4f, .12f, Shot, .1f, Bullet, 1, Shot + .7f, .45f, End, .3f);
        clip.distortion = Keys(0, 0, Windup, 0, Hit, -.45f, Hit + .17f, .12f, Hit + .37f, 0, Shot, 0, Bullet, -.5f, Shot + .3f, .1f, Shot + .55f, 0, End, 0);
        clip.exposure = Keys(0, 0, Windup, 0, Hit, 2.5f, Hit + .12f, -.5f, 1.3f, -.8f, 1.85f, -.2f, 2.6f, 0, Shot, 0, Bullet, 2, Shot + .3f, -.6f, Shot + 1.3f, -1.2f, End, -2);
        clip.desaturate = Keys(0, 0, Windup, 0, Hit + .12f, .25f, 2.6f, .1f, Shot, .1f, Shot + .4f, .4f, Shot + 1.6f, 1, End, 1);
        clip.presence = Keys(0, 0, TurnStart, 0, TurnEnd, 1, End, 1); // Zero values would still override the player's own grading before the turn.
        clip.blur = Keys(0, 0, Windup, 0, Hit, .85f, DazeEnd, .8f, CrawlStart + .3f, .3f, 2.8f, .12f, 3.6f, 0, Shot, 0, Bullet, .35f, Shot + .9f, .8f, End, 1);
    }
}
