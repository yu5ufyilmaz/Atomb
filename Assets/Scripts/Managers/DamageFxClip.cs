using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Timeline;

// Timeline-driven first-person damage. Owns its global Volume, so it needs no scene object or binding
// and works the same in the real scene, the blockout scene and timeline scrubbing.
public class DamageFxClip : PlayableAsset, ITimelineClipAsset
{
    public Color vignetteColor = new Color(.3f, 0, 0);
    public AnimationCurve vignette = new AnimationCurve();
    public AnimationCurve aberration = new AnimationCurve();
    public AnimationCurve distortion = new AnimationCurve();
    public AnimationCurve exposure = new AnimationCurve();
    public AnimationCurve desaturate = new AnimationCurve();
    public AnimationCurve blur = new AnimationCurve();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<DamageFxBehaviour>.Create(graph);
        playable.GetBehaviour().Clip = this;
        return playable;
    }
}

public class DamageFxBehaviour : PlayableBehaviour
{
    const int Priority = 1000; // Above every gameplay and settings Volume for the length of the clip.
    public DamageFxClip Clip;
    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    ChromaticAberration aberration;
    LensDistortion distortion;
    ColorAdjustments color;
    DepthOfField focus;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (volume != null) return;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        vignette = profile.Add<Vignette>(true);
        aberration = profile.Add<ChromaticAberration>(true);
        distortion = profile.Add<LensDistortion>(true);
        color = profile.Add<ColorAdjustments>(true);
        focus = profile.Add<DepthOfField>(true);
        vignette.color.value = Clip.vignetteColor;
        vignette.smoothness.value = 1;
        focus.focusMode.value = DepthOfFieldMode.Manual;
        focus.nearFocusStart.value = 0;
        focus.nearFocusEnd.value = 0;
        var host = new GameObject("Damage FX Volume") { hideFlags = HideFlags.HideAndDontSave };
        volume = host.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = Priority;
        volume.sharedProfile = profile;
    }

    public override void PrepareFrame(Playable playable, FrameData info)
    {
        if (volume == null) return;
        float time = (float)playable.GetTime();
        vignette.intensity.value = Clip.vignette.Evaluate(time);
        aberration.intensity.value = Clip.aberration.Evaluate(time);
        distortion.intensity.value = Clip.distortion.Evaluate(time);
        color.postExposure.value = Clip.exposure.Evaluate(time);
        color.saturation.value = -100 * Clip.desaturate.Evaluate(time);
        float blur = Clip.blur.Evaluate(time);
        focus.active = blur > .01f;
        focus.farFocusStart.value = Mathf.Lerp(100, .2f, blur);
        focus.farFocusEnd.value = Mathf.Lerp(200, 1.5f, blur);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info) => Release();
    public override void OnPlayableDestroy(Playable playable) => Release();

    void Release()
    {
        if (volume == null) return;
        var host = volume.gameObject;
        volume = null;
        if (Application.isPlaying) { Object.Destroy(host); Object.Destroy(profile); }
        else { Object.DestroyImmediate(host); Object.DestroyImmediate(profile); }
    }
}
