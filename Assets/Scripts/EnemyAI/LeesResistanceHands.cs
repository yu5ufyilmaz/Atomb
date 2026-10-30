using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Two baked LEES hands gripping the edges of the player's view.</summary>
public sealed class LeesResistanceHands : MonoBehaviour
{
    public Transform leftHand, rightHand;
    public Camera viewCamera;
    public Light fillLight;
    [Range(.2f, .8f)] public float depth = .38f;
    [Range(.5f, 2f)] public float size = .9f;
    public Vector2 leftAnchor = new Vector2(-.025f, .57f);
    public Vector2 rightAnchor = new Vector2(1.025f, .57f);
    HDAdditionalCameraData focusCamera;
    bool ownsFocus, previousCustomSettings, previousFocusEnabled;
    FrameSettingsOverrideMask previousFocusMask;

    public void Present(LeesResistanceModel model, float endingTime = 0)
    {
        if (model == null || viewCamera == null) { Hide(); return; }
        // The gameplay camera lives under a scaled character skeleton; the workshop camera doesn't.
        // Cancel that inherited scale for both the meshes and their local fill light.
        Vector3 inherited = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(1 / inherited.x, 1 / inherited.y, 1 / inherited.z);
        float reveal = model.State == LeesResistanceModel.Phase.Approaching
            ? Mathf.InverseLerp(model.Settings.approachSeconds * .55f, model.Settings.approachSeconds, model.Elapsed) : 1;
        if (model.State == LeesResistanceModel.Phase.Ready) reveal = 0;
        if (model.State == LeesResistanceModel.Phase.Survived) reveal *= 1 - Mathf.Clamp01(endingTime / .65f);
        if (reveal <= .001f) { Hide(); return; }
        KeepHandsInFocus();
        if (fillLight != null) fillLight.enabled = true;
        Pose(leftHand, leftAnchor, -1, reveal, model);
        Pose(rightHand, rightAnchor, 1, reveal, model);
    }
    void Pose(Transform hand, Vector2 anchor, int side, float reveal, LeesResistanceModel model)
    {
        if (hand == null) return;
        hand.gameObject.SetActive(reveal > .001f);
        if (reveal <= .001f) return;
        float pressure = model.State == LeesResistanceModel.Phase.Struggling && model.PullDirection == side
            ? model.PullStrength / Mathf.Max(1, model.Settings.pullDegreesPerSecond) : .1f;
        float pulse = Mathf.Sin(model.Elapsed * 18) * pressure;
        Vector3 point = viewCamera.ViewportToWorldPoint(new Vector3(anchor.x, anchor.y, depth));
        Vector3 offset = new Vector3(side * ((1 - reveal) * .2f - pressure * .018f),
            (1 - reveal) * .04f + pulse * .002f, -pressure * .012f);
        hand.position = point + viewCamera.transform.rotation * offset;
        hand.rotation = viewCamera.transform.rotation * Quaternion.Euler(side * (20 + pressure * 9),
            side > 0 ? 180 : 0, -(78 + pressure * 5 + pulse));
        // Read the actual projection: the main camera uses physical lens / gate-fit settings.
        float viewHeight = Vector3.Distance(viewCamera.ViewportToWorldPoint(new Vector3(.5f, 0, depth)),
            viewCamera.ViewportToWorldPoint(new Vector3(.5f, 1, depth)));
        float projectionScale = viewHeight / (2 * .38f * Mathf.Tan(68 * Mathf.Deg2Rad * .5f));
        hand.localScale = Vector3.one * (size * projectionScale);
    }
    public void Hide()
    {
        if (leftHand != null) leftHand.gameObject.SetActive(false);
        if (rightHand != null) rightHand.gameObject.SetActive(false);
        if (fillLight != null) fillLight.enabled = false;
        if (ownsFocus && focusCamera != null)
        {
            focusCamera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.DepthOfField, previousFocusEnabled);
            focusCamera.renderingPathCustomFrameSettingsOverrideMask = previousFocusMask;
            focusCamera.customRenderingSettings = previousCustomSettings;
        }
        ownsFocus = false;
    }
    void KeepHandsInFocus()
    {
        if (ownsFocus) return;
        focusCamera = viewCamera.GetComponent<HDAdditionalCameraData>();
        if (focusCamera == null) return;
        previousCustomSettings = focusCamera.customRenderingSettings;
        previousFocusEnabled = focusCamera.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.DepthOfField);
        previousFocusMask = focusCamera.renderingPathCustomFrameSettingsOverrideMask;
        // Dormant Inspector overrides must stay dormant when custom rendering was previously off.
        if (!previousCustomSettings) focusCamera.renderingPathCustomFrameSettingsOverrideMask = new FrameSettingsOverrideMask();
        // The game's autofocus follows center-screen world geometry, not these near, collider-free hands.
        focusCamera.customRenderingSettings = true;
        focusCamera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.DepthOfField] = true;
        focusCamera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.DepthOfField, false);
        ownsFocus = true;
    }
    void OnDisable() { Hide(); }
}
