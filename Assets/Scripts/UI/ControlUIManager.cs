using System.Collections;
using TMPro;
using UnityEngine;

public class ControlsUIManager : MonoBehaviour
{
    public static ControlsUIManager Instance;

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject controlsPanel; // Sol alttaki panelin kendisi

    [SerializeField]
    private TextMeshProUGUI controlsText; // İçindeki yazı

    [SerializeField]
    private float fadeDuration = 0.3f; // Açılış/Kapanış hızı

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (controlsPanel != null)
        {
            canvasGroup = controlsPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = controlsPanel.AddComponent<CanvasGroup>();

            // Başlangıçta gizle
            canvasGroup.alpha = 0;
            controlsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Ekrana kontrol listesini basar.
    /// Örnek: "A/D: Çevir | F: Çık"
    /// </summary>
    public void ShowControls(string text)
    {
        if (controlsPanel == null)
            return;

        controlsText.text = text;
        controlsPanel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f));
    }

    /// <summary>
    /// Kontrol listesini gizler.
    /// </summary>
    public void HideControls()
    {
        if (controlsPanel == null)
            return;
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (targetAlpha <= 0.01f)
            controlsPanel.SetActive(false);
    }
}
