using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SanityGlitchAction : ActionBase
{
    [Header("Şok Ayarları")]
    [Tooltip("Efektin toplam ekranda kalma ve sönme süresi (0.5 saniye idealdir)")]
    public float glitchDuration = 0.5f;

    [Tooltip("Şok anında patlayacak yüksek ses (Glitch, çığlık veya bas sesi)")]
    public AudioClip shockSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    [Header("Görsel Bozulma Seviyeleri")]
    public float peakVignette = 0.65f;
    public float peakLensDistortion = -0.7f;
    public float peakAberration = 1f; // Ekranda kırmızı/mavi ayrışması (titreme hissi verir)

    [Header("Ekstra Davranış")]
    [Tooltip("Fark edildiği an bu obje (hayalet) anında yok olsun mu?")]
    public bool destroyTargetOnNotice = true;

    private Volume globalVolume;
    private Vignette vignette;
    private LensDistortion lensDistortion;
    private ChromaticAberration aberration;

    // Asıl ayarlarını bozmamak için hafızada tutuyoruz
    private float baseVignette;
    private float baseLens;
    private float baseAberration;

    private void Start()
    {
        globalVolume = Object.FindFirstObjectByType<Volume>();
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out lensDistortion);
            globalVolume.profile.TryGet(out aberration);

            if (vignette != null)
                baseVignette = vignette.intensity.value;
            if (lensDistortion != null)
                baseLens = lensDistortion.intensity.value;
            if (aberration != null)
                baseAberration = aberration.intensity.value;
        }
    }

    protected override void PerformAction()
    {
        // 1. Sesi tam kameranın içinde (oyuncunun kafasında) çal
        if (shockSound != null)
        {
            AudioSource.PlayClipAtPoint(shockSound, Camera.main.transform.position, soundVolume);
        }

        // 2. Görsel Şok Rutinini Başlat
        StartCoroutine(GlitchRoutine());

        // 3. Hayaleti (Kendini) Yok Et
        if (destroyTargetOnNotice)
        {
            // Modeli anında görünmez yap ki kaybolma hissi verilsin
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.enabled = false;

            // Scriptin çalışmasını bitirebilmesi için objenin kendisini efekt bitince siliyoruz
            Destroy(gameObject, glitchDuration + 0.1f);
        }
    }

    private IEnumerator GlitchRoutine()
    {
        float elapsed = 0f;

        // Kamera FOV'unu hızlıca daraltıp açmak "Jump-Punch" denilen devasa sarsıntıyı hissettirir
        Camera mainCam = Camera.main;
        float baseFOV = mainCam.fieldOfView;
        float targetFOV = baseFOV - 15f; // İleriye ani odaklanma

        while (elapsed < glitchDuration)
        {
            // Zaman yavaşlatmalarından (Time.timeScale) etkilenmemesi için unscaledDeltaTime
            elapsed += Time.unscaledDeltaTime;

            // Mathf.Sin ile yarım daire dalgası yaratıyoruz.
            // Bu sayede efektler sıfırdan aniden tepeye vurur ve sürenin sonunda tekrar sıfıra yumuşakça iner.
            float t = elapsed / glitchDuration;
            float shockCurve = Mathf.Sin(t * Mathf.PI);

            // FOV Sarsıntısı (Zoom in/out)
            if (mainCam != null)
                mainCam.fieldOfView = Mathf.Lerp(baseFOV, targetFOV, shockCurve);

            // Post Process Efektlerini Zirveye Vur ve Geri Çek
            if (vignette != null)
                vignette.intensity.Override(Mathf.Lerp(baseVignette, peakVignette, shockCurve));

            if (lensDistortion != null)
                lensDistortion.intensity.Override(
                    Mathf.Lerp(baseLens, peakLensDistortion, shockCurve)
                );

            if (aberration != null)
                aberration.intensity.Override(
                    Mathf.Lerp(baseAberration, peakAberration, shockCurve)
                );

            yield return null;
        }

        // Efekt bittiğinde her şeyi garanti olarak senin asıl ayarlarına sıfırla
        if (mainCam != null)
            mainCam.fieldOfView = baseFOV;
        if (vignette != null)
            vignette.intensity.Override(baseVignette);
        if (lensDistortion != null)
            lensDistortion.intensity.Override(baseLens);
        if (aberration != null)
            aberration.intensity.Override(baseAberration);
    }
}
