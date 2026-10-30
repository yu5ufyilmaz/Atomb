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
    const float ImpactFrameEnd = 4.3833f; // The shot lands at 4.30, on the audio transient and impact frame.
    const float End = 5.3f; // Black hold that lets the gunshot ring out before the death screen.
    const int Pushes = 5;
    const float FloorHeight = -.379f;
    const float PushLift = .013f;
    const float PushRoll = 4f;
    static readonly Vector3 LandedPosition = new Vector3(-.165f, FloorHeight, .095f);
    static readonly Vector3 ShotPosition = new Vector3(-.06f, FloorHeight, .4f);
    // Guderian's head in the same space, from Guderian_behind.fbx: he walks forward until 4.28.
    const float HeadStartZ = -.133f, HeadEndZ = .11f, HeadStartTime = 1.88f, HeadEndTime = 4.28f;
    const float HeadX = .035f, HeadY = .02f;

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
        active.start = ImpactFrameEnd; active.duration = End - ImpactFrameEnd;
        foreach (var track in tracks.Values)
            foreach (var clip in track.GetClips())
                if (clip.end > End) clip.duration = End - clip.start;
        EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(camera); EditorUtility.SetDirty(blackBar);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        if (Mathf.Abs((float)timeline.duration - End) > .01f) Debug.LogError($"GUDERIAN BEHIND INVALID: süre {timeline.duration:0.###}, beklenen {End}.");
        else Debug.Log($"GUDERIAN BEHIND APPLIED: {timeline.duration:0.###} sn, atış 4.30.");
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
            Look(CrawlStart, LandedPosition, 0),
        };
        float step = (CrawlEnd - CrawlStart) / Pushes;
        for (int push = 0; push < Pushes; push++)
        {
            float start = CrawlStart + step * push;
            var from = Vector3.Lerp(LandedPosition, ShotPosition, push / (float)Pushes);
            var to = Vector3.Lerp(LandedPosition, ShotPosition, (push + 1) / (float)Pushes);
            float side = push % 2 == 0 ? PushRoll : -PushRoll; // Alternating elbows.
            poses.Add(Look(start + step * .45f, Vector3.Lerp(from, to, .5f) + Vector3.up * PushLift, side));
            poses.Add(Look(start + step * .8f, to, side * .4f));
            if (push < Pushes - 1) poses.Add(Look(start + step, to, 0));
        }
        var final = Look(CrawlEnd, ShotPosition, 0);
        poses.Add(final);
        poses.Add(new Pose(End, final.Position, final.Euler));
        return poses;
    }

    static Pose Look(float time, Vector3 position, float roll)
    {
        float head = Mathf.Lerp(HeadStartZ, HeadEndZ, Mathf.InverseLerp(HeadStartTime, HeadEndTime, time));
        var direction = new Vector3(HeadX, HeadY + .424f, head) - new Vector3(position.x, position.y + .424f, position.z + .0587f);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        if (yaw > 0) yaw -= 360; // Stay on the negative side the fall ended on.
        float pitch = -Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
        return new Pose(time, position, new Vector3(pitch, yaw, roll));
    }

    static void WriteCamera(AnimationClip clip)
    {
        var poses = BuildPoses();
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
        var curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), curve);
    }

    // Hard cut: the frame after the muzzle flash is black.
    static void WriteBlackBar(AnimationClip clip)
    {
        var binding = AnimationUtility.GetCurveBindings(clip).First(item => item.propertyName == "m_Color.a");
        var curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(ImpactFrameEnd, 1), new Keyframe(End, 1));
        AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }
}
