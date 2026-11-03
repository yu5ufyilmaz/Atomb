using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Sigorta atınca yalnız gerçek zamanlı ışıklar kapanıyordu; pişmiş dolaylı ışık, ışık probları ve
// yansıma probları "ışıklar açık" görüntüsünü koruyordu (3 Kasım oyun testi). Bu volume onları söndürür.
public class PowerBlackout : MonoBehaviour
{
    [Tooltip("Elektrik yokken kalan pişmiş dolaylı ışık (0 = zifiri).")]
    [Range(0f, 1f)] public float indirectLeft = 0f;

    [Tooltip("Elektrik yokken kalan yansıma ışığı (0 = zifiri).")]
    [Range(0f, 1f)] public float reflectionLeft = 0f;

    [Tooltip("Hacimli sisin ortam ışığı (0 = sis de kararır).")]
    [Range(0f, 1f)] public float fogAmbientLeft = 0f;

    public float fadeSeconds = .25f;

    private Volume volume;

    private void Awake()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var indirect = profile.Add<IndirectLightingController>();
        indirect.indirectDiffuseLightingMultiplier.Override(indirectLeft);
        indirect.reflectionLightingMultiplier.Override(reflectionLeft);
        profile.Add<Fog>().globalLightProbeDimmer.Override(fogAmbientLeft);
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000;
        volume.weight = 0f;
        volume.sharedProfile = profile;
    }

    private void Update()
    {
        // Polled, not evented: loading a save sets the tripped state without raising the breaker events.
        bool powerOut = BreakerBox.Instance != null && BreakerBox.Instance.IsTripped;
        volume.weight = Mathf.MoveTowards(volume.weight, powerOut ? 1f : 0f, Time.deltaTime / Mathf.Max(fadeSeconds, .01f));
    }

    private void OnDestroy()
    {
        if (volume != null) Destroy(volume.sharedProfile);
    }
}
