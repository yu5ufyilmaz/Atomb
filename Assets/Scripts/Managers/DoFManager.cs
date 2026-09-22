using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class DoFManager : MonoBehaviour
{
    public static DoFManager Instance;

    [Header("Referanslar")]
    [Tooltip("Global Volume objenizi buraya sürükleyin")]
    public Volume globalVolume;

    [Header("Ayarlar")]
    [Tooltip("Varsayılan geçiş süresi (saniye)")]
    public float defaultTransitionDuration = 0.3f;

    private DepthOfField m_DepthOfField;
    private float baseFocusDistance = 10f; // Oyunun normal (uzak) odak mesafesi
    private Coroutine focusCoroutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Başlangıçta Volume içinden DoF bileşenini bul ve default mesafeyi kaydet
        if (globalVolume != null && globalVolume.profile != null)
        {
            if (globalVolume.profile.TryGet(out m_DepthOfField))
            {
                baseFocusDistance = m_DepthOfField.focusDistance.value;
            }
        }
    }

    /// <summary>
    /// Özel bir mesafeye pürüzsüzce odaklanmak için kullanın.
    /// </summary>
    /// <param name="targetDistance">Odaklanılacak mesafe (örn: Defter için 0.3f)</param>
    /// <param name="customDuration">Özel bir süre vermek istersen (boş bırakırsan default süreyi kullanır)</param>
    public void SetFocus(float targetDistance, float customDuration = -1f)
    {
        if (m_DepthOfField == null)
            return;

        float duration = customDuration > 0f ? customDuration : defaultTransitionDuration;

        if (focusCoroutine != null)
            StopCoroutine(focusCoroutine);

        focusCoroutine = StartCoroutine(SmoothFocusRoutine(targetDistance, duration));
    }

    /// <summary>
    /// Odaklamayı oyunun orijinal/eski haline geri döndürür.
    /// </summary>
    public void ResetFocus(float customDuration = -1f)
    {
        SetFocus(baseFocusDistance, customDuration);
    }

    private IEnumerator SmoothFocusRoutine(float target, float duration)
    {
        float startDistance = m_DepthOfField.focusDistance.value;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Zaman durduğunda da çalışması için unscaledDeltaTime kullanıyoruz
            elapsed += Time.unscaledDeltaTime;

            // SmoothStep ile sinematik ve pürüzsüz geçiş
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            m_DepthOfField.focusDistance.Override(Mathf.Lerp(startDistance, target, t));
            yield return null;
        }

        m_DepthOfField.focusDistance.Override(target);
    }
}
