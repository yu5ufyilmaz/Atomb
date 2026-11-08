using UnityEngine;

// A cigarette left burning on the ashtray: glowing tip and a thin smoke wisp while the title menu is up.
// Keeps burning through the desk intro and on the ashtray afterwards; cold at once when a save is loaded.
public class CigaretteEmber : MonoBehaviour
{
    [Tooltip("Kor rengi (HDR)")]
    [ColorUsage(false, true)] public Color emberColor = new Color(1f, 0.28f, 0.05f) * 6f;
    [Tooltip("Senzora/Soft Particle Unlit; referans build'e dahil etmek için")]
    public Shader particleShader;

    Material ember, smokeMaterial;
    ParticleSystem smoke;
    float fade = -1; // seconds since stubbing; negative while burning

    void Awake()
    {
        var shader = particleShader != null ? particleShader : Shader.Find("Senzora/Soft Particle Unlit");
        ember = new Material(shader) { color = emberColor };
        ember.SetFloat("_Softness", 0.5f);
        var tip = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tip.name = "Tip";
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(transform, false);
        tip.transform.localScale = Vector3.one * 0.009f;
        tip.GetComponent<MeshRenderer>().sharedMaterial = ember;
        tip.AddComponent<FaceCamera>();

        smokeMaterial = new Material(shader);
        var host = new GameObject("Smoke");
        host.transform.SetParent(transform, false);
        host.transform.localPosition = Vector3.up * 0.004f;
        smoke = host.AddComponent<ParticleSystem>();
        var main = smoke.main;
        main.loop = true; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
        main.startColor = new Color(0.3f, 0.3f, 0.33f, 0.3f); // dim: unlit, must not outshine the lamp-lit desk
        main.gravityModifier = -0.006f; // warm smoke drifts up
        var emission = smoke.emission; emission.rateOverTime = 28;
        var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 6; shape.radius = 0.001f;
        shape.rotation = new Vector3(-90, 0, 0); // emit upward
        var size = smoke.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.4f, 1, 2.5f));
        var color = smoke.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var noise = smoke.noise; noise.enabled = true; noise.strength = 0.03f; noise.frequency = 0.5f; noise.scrollSpeed = 0.3f; noise.damping = true;
        var renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = smokeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameStarted) { Stub(); fade = 10; } // loaded save: already cold
    }

    void Update()
    {
        if (fade < 0)
        {
            float glow = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 2.3f, 0.5f); // slow ember breathing
            ember.color = emberColor * glow;
            return;
        }
        fade += Time.deltaTime;
        ember.color = emberColor * Mathf.Lerp(0.8f, 0, fade / 1.2f);
        if (fade > 6) enabled = false;
    }

    void Stub()
    {
        fade = 0;
        smoke.Emit(12); // the last puff as it is pressed out
        smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    void OnDestroy() { Destroy(ember); Destroy(smokeMaterial); }

    class FaceCamera : MonoBehaviour
    {
        void LateUpdate() { if (Camera.main != null) transform.rotation = Camera.main.transform.rotation; }
    }
}
