using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Keeps the laboratory's player, managers and authored interaction poses.
[DefaultExecutionOrder(-900)]
[RequireComponent(typeof(ResearchPuzzleTrial))]
public sealed class LaboratorySynthesisScene : MonoBehaviour
{
    private Volume precisionVolume;
    private VolumeProfile precisionProfile;
    private float precisionBlend;

    private void Start()
    {
        if (!GetComponent<ResearchPuzzleTrial>().Configure(null))
            Debug.LogError("Laboratuvar araştırma bağlantıları eksik.", this);
        // ScreenPointToRay uses the camera projection before post-processing distortion.
        // A local override keeps printed pages and chalk under the pointer without changing walking effects.
        precisionVolume = gameObject.AddComponent<Volume>();
        precisionVolume.isGlobal = true; precisionVolume.priority = 1000; precisionVolume.weight = 0;
        precisionProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        var lens = precisionProfile.Add<LensDistortion>();
        lens.intensity.Override(0); lens.scale.Override(1);
        precisionVolume.sharedProfile = precisionProfile;
    }

    private void LateUpdate()
    {
        if (precisionVolume == null) return;
        var interaction = GameManager.Instance != null ? GameManager.Instance.activeInteraction : null;
        float target = interaction is InteractableBook || interaction is InteractableChalkboard
            || interaction is InteractableTuringMachine terminal && terminal.RequiresMeasurementCursor ? 1 : 0;
        precisionBlend = Mathf.MoveTowards(precisionBlend, target, Time.unscaledDeltaTime / .2f);
        precisionVolume.weight = Mathf.SmoothStep(0, 1, precisionBlend);
    }

    private void OnDestroy() { if (precisionProfile != null) Destroy(precisionProfile); }
}
