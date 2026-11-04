using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Timeline-driven arm override: the mocap aims at standing height, so this swings the arm down onto a player
// on the floor. Finds the rig through the director's bindings, so it needs no scene setup of its own.
public class GunAimClip : PlayableAsset, ITimelineClipAsset
{
    public AnimationCurve weight = new AnimationCurve();
    public AnimationCurve focus = new AnimationCurve();
    public AnimationCurve startView = new AnimationCurve(); // 1: the player's own frame at the trigger (eye, view, lens); fades into the whip turn.
    public float crawlStart, shotTime;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<GunAimBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.Clip = this;
        var director = owner.GetComponent<PlayableDirector>();
        var animators = ((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AnimationTrack>()
            .Select(track => director.GetGenericBinding(track) as Animator).Where(animator => animator != null).ToList();
        var shooter = animators.FirstOrDefault(animator => animator.isHuman);
        behaviour.Arm = shooter != null ? shooter.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
        behaviour.Gun = shooter != null ? shooter.GetBoneTransform(HumanBodyBones.RightHand).Find("911pistol") : null;
        behaviour.Head = shooter != null ? shooter.GetBoneTransform(HumanBodyBones.Head) : null;
        behaviour.Body = shooter;
        behaviour.Lens = animators.Select(animator => animator.GetComponent<CinemachineVirtualCamera>()).FirstOrDefault(lens => lens != null);
        if (behaviour.Arm == null || behaviour.Gun == null || behaviour.Lens == null) Debug.LogError("GunAimClip: kol, silah veya kamera bulunamadı.", owner);
        return playable;
    }
}

public class GunAimBehaviour : PlayableBehaviour
{
    public GunAimClip Clip;
    public Transform Arm, Gun, Head;
    public Animator Body;
    public CinemachineVirtualCamera Lens;
    GunAimDriver driver;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (driver != null || Arm == null || Gun == null || Lens == null) return;
        var host = new GameObject("Gun Aim") { hideFlags = HideFlags.HideAndDontSave };
        host.SetActive(false); // So OnEnable sees the fields.
        driver = host.AddComponent<GunAimDriver>();
        driver.Arm = Arm; driver.Gun = Gun; driver.Head = Head; driver.Body = Body; driver.Eye = Lens.transform; driver.Lens = Lens;
        // The brain has not switched yet this frame: Camera.main still shows exactly what the player sees.
        var view = Camera.main;
        driver.StartPosition = view != null ? view.transform.position : Lens.transform.position;
        driver.StartRotation = view != null ? view.transform.rotation : Lens.transform.rotation;
        // The whole lens of the gameplay camera (aperture and the rest drive HDRP's depth of field), at the FOV it rendered with.
        var gameplay = view != null && view.TryGetComponent(out CinemachineBrain brain) ? brain.ActiveVirtualCamera as CinemachineVirtualCamera : null;
        driver.StartLens = gameplay != null && gameplay != Lens ? gameplay.m_Lens : Lens.m_Lens;
        if (view != null) driver.StartLens.FieldOfView = view.fieldOfView;
        host.SetActive(true);
    }

    public override void PrepareFrame(Playable playable, FrameData info)
    {
        if (driver == null) return;
        float time = (float)playable.GetTime();
        driver.Weight = Clip.weight.Evaluate(time);
        driver.Focus = Clip.focus.Evaluate(time);
        driver.StartView = Clip.startView.Evaluate(time);
        driver.StopAtWalls = time >= Clip.crawlStart;
        driver.Hunted = driver.StopAtWalls && time < Clip.shotTime;
#if UNITY_EDITOR
        // Timeline preview never ticks LateUpdate on its own.
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    public override void OnBehaviourPause(Playable playable, FrameData info) => Release();
    public override void OnPlayableDestroy(Playable playable) => Release();

    void Release()
    {
        if (driver == null) return;
        var host = driver.gameObject;
        driver = null;
        if (Application.isPlaying) Object.Destroy(host);
        else Object.DestroyImmediate(host);
    }
}
