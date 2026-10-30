using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Bulanıklık 1/mesafe ile değişir; metrede yumuşatınca uzağa geçiş anlık, yakına geçiş çok yavaş kalır.
// Bu yüzden odak dioptride (1/metre) tutulur.
[Serializable]
public class FocusFilter
{
    private const float MinDistance = 0.1f;
    private const float MaxStep = 0.05f; // Takılan bir kare odağı sıçratmasın

    [Tooltip("Yeni mesafe bu kadar süre farklı kalmadan odak oynamaz (sn). Göz tepkisi yaklaşık 0.3 sn.")]
    public float holdTime = 0.25f;

    [Tooltip("Odağın yeni mesafeye geçiş süresi (sn)")]
    public float smoothTime = 0.35f;

    [Tooltip("Bundan küçük farklar yok sayılır (dioptri). 0.15 ile 7 metre ötesi tek bir 'uzak' bölge olur.")]
    public float deadZone = 0.15f;

    private float current, target, velocity, held;

    /// <summary>
    /// Odağı verilen mesafeye sabitler. Sonraki odak değişimi beklemeden başlar (örn. etkileşimden çıkış).
    /// </summary>
    public void Reset(float distance)
    {
        current = target = ToDiopters(distance);
        velocity = 0f;
        held = holdTime;
    }

    /// <summary>
    /// Ölçülen mesafeyi işler ve kullanılacak odak mesafesini döndürür. frozen iken hedef değişmez.
    /// </summary>
    public float Step(float sampledDistance, float deltaTime, bool frozen)
    {
        deltaTime = Mathf.Min(deltaTime, MaxStep);

        if (!frozen)
        {
            float sampled = ToDiopters(sampledDistance);
            if (Mathf.Abs(sampled - target) <= deadZone)
                held = 0f;
            else if ((held += deltaTime) >= holdTime)
            {
                target = sampled;
                held = 0f;
            }
        }

        current = Mathf.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, deltaTime);
        return 1f / current;
    }

    private static float ToDiopters(float distance) => 1f / Mathf.Max(distance, MinDistance);
}

public class DoFManager : MonoBehaviour
{
    public static DoFManager Instance;

    private static readonly Vector2[] FocusArea = { Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    [Header("Referanslar")]
    public Volume globalVolume;
    private Camera mainCam;

    [Header("Dinamik Odak (Autofocus) Ayarları")]
    [Tooltip("Aktif edilirse oyuncunun baktığı yere otomatik odaklanır.")]
    public bool useAutofocus = true;

    public FocusFilter focusFilter = new FocusFilter();

    [Tooltip("Kamera bundan hızlı dönerken odak hedefi değişmez (derece/sn)")]
    public float maxTurnSpeed = 90f;

    [Tooltip("Ekran ortasındaki odak alanının genişliği (0.04, 1080p'de yaklaşık 23 piksel)")]
    public float focusAreaSize = 0.04f;

    [Tooltip("Gökyüzüne veya çok uzağa bakıldığında maksimum netlik mesafesi")]
    public float maxFocusDistance = 50f;

    [Tooltip("Lazerin hangi katmanlara çarpacağı (UI veya görünmez objeleri yoksaymak için)")]
    public LayerMask focusLayerMask = Physics.DefaultRaycastLayers;

    [Header("Manuel Odak Ayarları (Kitap / Menü)")]
    public float defaultTransitionDuration = 0.3f;
    private float baseFocusDistance = 10f; // Autofocus kapalıysa dönülecek temel mesafe

    private DepthOfField m_DepthOfField;
    private Coroutine manualFocusCoroutine;
    private bool isManualFocusActive = false;
    private readonly float[] focusSamples = new float[FocusArea.Length];
    private Vector3 lastForward;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        mainCam = Camera.main;

        if (globalVolume != null && globalVolume.profile != null)
        {
            if (globalVolume.profile.TryGet(out m_DepthOfField))
            {
                baseFocusDistance = m_DepthOfField.focusDistance.value;
                focusFilter.Reset(baseFocusDistance);
            }
        }
    }

    private void Update()
    {
        // Eğer kitap okumuyorsak, makinede değilsek ve dinamik odak açıksa:
        if (useAutofocus && !isManualFocusActive && m_DepthOfField != null && mainCam != null)
        {
            Transform cam = mainCam.transform;
            float deltaTime = Time.unscaledDeltaTime;

            // Oyuncu etrafa bakınırken gözü ekran ortasında değildir, odak o sırada takip etmez
            bool turningFast = Vector3.Angle(lastForward, cam.forward) > maxTurnSpeed * deltaTime;
            lastForward = cam.forward;

            m_DepthOfField.focusDistance.Override(
                focusFilter.Step(SampleFocusDistance(cam), deltaTime, turningFast)
            );
        }
    }

    // Tek ışın yerine alanın ortancası: ince nesneler ve kenarlar odağı zıplatmaz
    private float SampleFocusDistance(Transform cam)
    {
        for (int i = 0; i < FocusArea.Length; i++)
        {
            Vector3 direction = cam.forward + (cam.right * FocusArea[i].x + cam.up * FocusArea[i].y) * focusAreaSize;
            bool didHit = Physics.Raycast(cam.position, direction, out RaycastHit hit, maxFocusDistance, focusLayerMask, QueryTriggerInteraction.Ignore);
            focusSamples[i] = didHit ? hit.distance : maxFocusDistance;
        }

        Array.Sort(focusSamples);
        return focusSamples[focusSamples.Length / 2];
    }

    /// <summary>
    /// Kitap, Not veya Makine arayüzü açıldığında sabit bir noktaya odaklanmak için kullanılır.
    /// </summary>
    public void SetFocus(float targetDistance, float customDuration = -1f)
    {
        isManualFocusActive = true; // Dinamik odağı geçici olarak durdur
        if (m_DepthOfField == null)
            return;

        float duration = customDuration > 0f ? customDuration : defaultTransitionDuration;

        if (manualFocusCoroutine != null)
            StopCoroutine(manualFocusCoroutine);

        manualFocusCoroutine = StartCoroutine(SmoothManualFocus(targetDistance, duration));
    }

    /// <summary>
    /// Arayüz kapandığında gözü tekrar serbest bırakır (veya eski sabit değere döner).
    /// </summary>
    public void ResetFocus(float customDuration = -1f)
    {
        isManualFocusActive = false; // Dinamik odak (Autofocus) tekrar devreye girsin
        if (m_DepthOfField == null)
            return;

        if (manualFocusCoroutine != null)
            StopCoroutine(manualFocusCoroutine);

        if (useAutofocus)
        {
            focusFilter.Reset(m_DepthOfField.focusDistance.value);
            return;
        }

        // Eğer dinamik odak kullanmıyorsak, eski sabit (uzak) odağa yumuşakça geri dön
        float duration = customDuration > 0f ? customDuration : defaultTransitionDuration;
        manualFocusCoroutine = StartCoroutine(SmoothManualFocus(baseFocusDistance, duration));
    }

    private IEnumerator SmoothManualFocus(float target, float duration)
    {
        float startDistance = m_DepthOfField.focusDistance.value;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            m_DepthOfField.focusDistance.Override(Mathf.Lerp(startDistance, target, t));
            yield return null;
        }

        m_DepthOfField.focusDistance.Override(target);
    }
}
