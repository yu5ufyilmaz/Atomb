using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class DoFManager : MonoBehaviour
{
    public static DoFManager Instance;

    [Header("Referanslar")]
    public Volume globalVolume;
    private Camera mainCam;

    [Header("Dinamik Odak (Autofocus) Ayarları")]
    [Tooltip("Aktif edilirse oyuncunun baktığı yere otomatik odaklanır.")]
    public bool useAutofocus = true;

    [Tooltip("Odağın ne kadar hızlı değişeceği (Göz adaptasyon hızı)")]
    public float autofocusSpeed = 8f;

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
            }
        }
    }

    private void Update()
    {
        // Eğer kitap okumuyorsak, makinede değilsek ve dinamik odak açıksa:
        if (useAutofocus && !isManualFocusActive && m_DepthOfField != null && mainCam != null)
        {
            float targetDistance = maxFocusDistance;

            // Ekranın tam ortasından ileriye bir ışın yolla
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);

            // Eğer ışın bir yere çarparsa, o mesafeyi hedef al
            if (Physics.SphereCast(ray, 0.1f, out RaycastHit hit, maxFocusDistance, focusLayerMask))
            {
                targetDistance = hit.distance;
            }

            // Gözün o mesafeye odaklanmasını yumuşat (Lerp)
            float currentDist = m_DepthOfField.focusDistance.value;
            m_DepthOfField.focusDistance.Override(
                Mathf.Lerp(currentDist, targetDistance, Time.unscaledDeltaTime * autofocusSpeed)
            );
        }
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

        // Eğer dinamik odak kullanmıyorsak, eski sabit (uzak) odağa yumuşakça geri dön
        if (!useAutofocus)
        {
            float duration = customDuration > 0f ? customDuration : defaultTransitionDuration;
            if (manualFocusCoroutine != null)
                StopCoroutine(manualFocusCoroutine);
            manualFocusCoroutine = StartCoroutine(SmoothManualFocus(baseFocusDistance, duration));
        }
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
