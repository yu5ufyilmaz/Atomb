using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
public class CutsceneTimelineEffects : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField]
    private Volume targetVolume;

    [Range(0f, 1f)]
    public float motionBlurIntensity;

    [Range(0f, 1f)]
    public float vignetteIntensity;

    [Range(0f, 1f)]
    public float vignetteSmoothness = 0.8f;

    [Header("Experiment Failed")]
    [SerializeField]
    private CanvasGroup experimentFailedCanvasGroup;

    [Range(0f, 1f)]
    public float experimentFailedAlpha;

    private MotionBlur motionBlur;
    private Vignette vignette;

    private void OnEnable()
    {
        CacheVolumeOverrides();
        Apply();
    }

    private void OnValidate()
    {
        CacheVolumeOverrides();
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void CacheVolumeOverrides()
    {
        if (targetVolume == null)
            return;

        VolumeProfile profile = targetVolume.profile;
        if (profile == null)
            return;

        profile.TryGet(out motionBlur);
        profile.TryGet(out vignette);
    }

    private void Apply()
    {
        if (motionBlur != null)
            motionBlur.intensity.Override(motionBlurIntensity);

        if (vignette != null)
        {
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);
        }

        if (experimentFailedCanvasGroup != null)
        {
            experimentFailedCanvasGroup.alpha = experimentFailedAlpha;
            experimentFailedCanvasGroup.interactable = experimentFailedAlpha > 0.95f;
            experimentFailedCanvasGroup.blocksRaycasts = experimentFailedAlpha > 0.95f;
        }
    }
}
