using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

// Rebuilds the camera beats of TL_GuderianBehindJS. Values live in the camera track's space:
// x = player right, y = up, z = player forward, 1 unit = 3.75 m. Re-running overwrites the same keys.
public static class GuderianBehindTimelineSetup
{
    const string TimelinePath = "Assets/CutScenes/TL_GuderianBehindJS.playable";
    const float CrawlStart = 1.85f; // Guderian starts walking at 1.88.
    const float CrawlEnd = 4.1f;
    const float ShotTime = 4.3f; // The shot lands on the audio transient and impact frame.
    const float ImpactFrameEnd = 4.3833f;
    const float EyesCloseStart = 5.6f;
    const float End = 7.9f; // He dies on the floor: the eyes fail and close before the death screen.
    const float VoiceLength = 2.654626f; // The last audio clip's full length.
    const float DazeEnd = 1.62f; // Stunned on the floor, then the head snaps back to him.
    // Panic: long shoves first, shorter and faster as he closes in. Durations add up to CrawlEnd - CrawlStart.
    static readonly float[] PushDurations = { .52f, .46f, .4f, .36f, .3f, .21f };
    static readonly float[] PushDistances = { .22f, .2f, .19f, .17f, .14f, .08f };
    const float FloorHeight = -.379f;
    const float PropHeight = -.31f; // Eyes about 45 cm off the floor, propped on the elbows.
    const float PushDip = .014f;
    const float PushLift = .018f;
    const float PushRoll = 6f;
    const float PushNod = 4f;
    static readonly Vector3 LandedPosition = new Vector3(-.165f, FloorHeight, .095f);
    static readonly Vector3 PropPosition = new Vector3(-.165f, PropHeight, .095f);
    static readonly Vector3 ShotPosition = new Vector3(-.06f, -.33f, .4f);
    static readonly Vector3 CameraOrigin = new Vector3(0, .424f, .0587f);
    // Guderian in the same space, from Guderian_behind.fbx: he walks forward until 4.28 with the gun hand up.
    static readonly Vector3 HeadStart = new Vector3(0, .436f, -.095f), HeadEnd = new Vector3(-.045f, .428f, .158f);
    static readonly Vector3 GunStart = new Vector3(.14f, .462f, .19f), GunEnd = new Vector3(.109f, .454f, .252f);
    const float HeadStartTime = 1.88f, HeadEndTime = 4.28f, GunStartTime = 3.5f;
    const float GunFocus = .7f; // Eyes drop from his face to the gun, keeping both in frame.

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
        foreach (var name in new[] { "Activation Track", "Cinemachine Track", "Animation Track" })
        {
            var last = tracks[name].GetClips().OrderBy(clip => clip.end).Last();
            last.duration = End - last.start;
        }
        var voice = tracks["Audio Track"].GetClips().OrderBy(clip => clip.end).Last();
        voice.duration = Mathf.Min(VoiceLength, End - (float)voice.start);
        var damageTrack = timeline.GetOutputTracks().OfType<DamageFxTrack>().FirstOrDefault() ?? timeline.CreateTrack<DamageFxTrack>(null, "Damage FX");
        var damage = damageTrack.GetClips().FirstOrDefault() ?? damageTrack.CreateClip<DamageFxClip>();
        damage.start = 0; damage.duration = End; damage.displayName = "Damage FX";
        WriteDamage((DamageFxClip)damage.asset);
        EditorUtility.SetDirty(damage.asset); EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(camera); EditorUtility.SetDirty(blackBar);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        if (Mathf.Abs((float)timeline.duration - End) > .01f) Debug.LogError($"GUDERIAN BEHIND INVALID: süre {timeline.duration:0.###}, beklenen {End}.");
        else Debug.Log($"GUDERIAN BEHIND APPLIED: {timeline.duration:0.###} sn, atış {ShotTime:0.##}.");
    }

    static List<Pose> BuildPoses()
    {
        var poses = new List<Pose>
        {
            new Pose(0, Vector3.zero, new Vector3(.5f, 0, 0)),
            new Pose(.7f, Vector3.zero, new Vector3(.5f, -156.36f, 0)), // Turned to face him.
            // The right hook crosses the player's face toward -x, so the fall follows it.
            new Pose(.7833f, new Vector3(-.02f, -.012f, .008f), new Vector3(13.6f, -113, -2.9f)),
            new Pose(.8833f, new Vector3(-.06f, -.07f, .02f), new Vector3(6, -104, -14)),
            new Pose(1.05f, new Vector3(-.12f, -.24f, .05f), new Vector3(-22, -96, -26)),
            new Pose(1.2167f, new Vector3(-.16f, FloorHeight - .006f, .085f), new Vector3(-44, -90, -30)),
            new Pose(1.3f, new Vector3(-.165f, -.365f, .09f), new Vector3(-36, -96, -24)),
            new Pose(1.4333f, LandedPosition, new Vector3(-42, -110, -18)),
            new Pose(DazeEnd, LandedPosition, new Vector3(-38, -114, -15)),
            Look(CrawlStart, PropPosition, 0),
        };
        float time = CrawlStart, travelled = 0;
        for (int push = 0; push < PushDurations.Length; push++)
        {
            float duration = PushDurations[push], next = travelled + PushDistances[push];
            var from = Vector3.Lerp(PropPosition, ShotPosition, travelled);
            var to = Vector3.Lerp(PropPosition, ShotPosition, next);
            float side = push % 2 == 0 ? PushRoll : -PushRoll; // Alternating elbows.
            // The elbow plants and the head nods down, then the shove throws him back.
            poses.Add(Look(time + duration * .3f, from + Vector3.down * PushDip, side * .5f, PushNod, -side * .3f));
            poses.Add(Look(time + duration * .7f, Vector3.Lerp(from, to, .85f) + Vector3.up * PushLift, side, -PushNod, side * .5f));
            poses.Add(Look(time + duration, to, 0));
            time += duration; travelled = next;
        }
        // Frozen on the gun for the last beat, then the bullet throws him onto his back.
        var shot = Look(ShotTime, ShotPosition, 0);
        poses.Add(shot);
        poses.Add(new Pose(ImpactFrameEnd, shot.Position + new Vector3(0, -.008f, .014f), shot.Euler + new Vector3(10, 4, 8)));
        poses.Add(new Pose(4.62f, new Vector3(-.066f, -.372f, .43f), new Vector3(-68, -205, 14)));
        poses.Add(new Pose(4.72f, new Vector3(-.068f, FloorHeight - .004f, .44f), new Vector3(-74, -204, 20)));
        poses.Add(new Pose(4.82f, new Vector3(-.068f, -.374f, .44f), new Vector3(-70, -204, 17)));
        poses.Add(new Pose(5, new Vector3(-.07f, -.381f, .443f), new Vector3(-73, -205, 22)));
        // The head goes slack and rolls to the side.
        poses.Add(new Pose(5.6f, new Vector3(-.07f, -.381f, .443f), new Vector3(-66, -222, 34)));
        poses.Add(new Pose(6.3f, new Vector3(-.07f, -.381f, .443f), new Vector3(-58, -236, 42)));
        poses.Add(new Pose(End, new Vector3(-.07f, -.381f, .443f), new Vector3(-57, -238, 43)));
        return poses;
    }

    static Pose Look(float time, Vector3 position, float roll, float pitchOffset = 0, float yawOffset = 0)
    {
        var head = Vector3.Lerp(HeadStart, HeadEnd, Mathf.InverseLerp(HeadStartTime, HeadEndTime, time));
        var gun = Vector3.Lerp(GunStart, GunEnd, Mathf.InverseLerp(GunStartTime, HeadEndTime, time));
        var target = Vector3.Lerp(head, gun, GunFocus * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(GunStartTime - .5f, CrawlEnd, time)));
        var direction = target - (position + CameraOrigin);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        if (yaw > 0) yaw -= 360; // Stay on the negative side the fall ended on.
        float pitch = -Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
        return new Pose(time, position, new Vector3(pitch + pitchOffset, yaw + yawOffset, roll));
    }

    static void WriteCamera(AnimationClip clip)
    {
        var poses = BuildPoses();
        Debug.Assert(poses.Zip(poses.Skip(1), (a, b) => a.Time < b.Time).All(ordered => ordered), "GUDERIAN BEHIND INVALID: kamera anahtarları sıralı değil.");
        Debug.Assert(Mathf.Approximately(PushDurations.Sum(), CrawlEnd - CrawlStart) && Mathf.Approximately(PushDistances.Sum(), 1), "GUDERIAN BEHIND INVALID: itiş tablosu tutmuyor.");
        for (int axis = 0; axis < 3; axis++)
        {
            string name = "xyz"[axis].ToString();
            int index = axis;
            SetCurve(clip, typeof(Transform), "m_LocalPosition." + name, poses.Select(pose => new Keyframe(pose.Time, pose.Position[index])));
            SetCurve(clip, typeof(Transform), "localEulerAnglesRaw." + name, poses.Select(pose => new Keyframe(pose.Time, pose.Euler[index])));
        }
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
        AnimationUtility.SetEditorCurve(clip, binding, Keys(0, 0, EyesCloseStart, 0, 5.95f, .75f, 6.25f, .2f, 6.7f, .95f, 6.95f, .45f, 7.4f, 1, End, 1));
    }

    // Punch: white flash, then concussion that clears as adrenaline takes over. Shot: the same kick, then blood loss.
    static void WriteDamage(DamageFxClip clip)
    {
        const float Hit = .7833f, Windup = .7f, Bullet = ImpactFrameEnd;
        clip.vignette = Keys(0, 0, Windup, 0, Hit, .7f, 1.4f, .5f, 1.85f, .42f, 2.4f, .34f, 3.4f, .3f, 4.1f, .36f, ShotTime, .42f, Bullet, .8f, 5, .72f, 6.2f, .95f, End, 1);
        clip.aberration = Keys(0, 0, Windup, 0, Hit, 1, 1.2f, .4f, 2.4f, .12f, ShotTime, .1f, Bullet, 1, 5, .45f, End, .3f);
        clip.distortion = Keys(0, 0, Windup, 0, Hit, -.45f, .95f, .12f, 1.15f, 0, ShotTime, 0, Bullet, -.5f, 4.6f, .1f, 4.85f, 0, End, 0);
        clip.exposure = Keys(0, 0, Windup, 0, Hit, 2.5f, .9f, -.5f, 1.3f, -.8f, 1.85f, -.2f, 2.6f, 0, ShotTime, 0, Bullet, 2, 4.6f, -.6f, 6, -1.2f, End, -2);
        clip.desaturate = Keys(0, 0, Windup, 0, .9f, .25f, 2.6f, .1f, ShotTime, .1f, 4.7f, .4f, 6.4f, 1, End, 1);
        clip.blur = Keys(0, 0, Windup, 0, Hit, .85f, DazeEnd, .8f, CrawlStart, .3f, 2.8f, .12f, 3.6f, 0, ShotTime, 0, Bullet, .35f, 5.2f, .8f, End, 1);
    }
}
