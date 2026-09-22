using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Image kontrolü için şart

public class DeathUIManager : MonoBehaviour
{
    public static DeathUIManager Instance;

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject deathScreenPanel; // Siyah Arka Plan Paneli

    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [Header("Gizlenecek Arayüzler")]
    [SerializeField]
    private List<GameObject> uiElementsToHide;

    // --- DEĞİŞEN KISIM: Direkt Image'ı alıyoruz ---
    [Header("Ekstra Fade Görseli")]
    [Tooltip("Yavaşça belirecek olan Resmi (Image) buraya sürükle.")]
    [SerializeField]
    private Image fadeImage; // GameObject değil, direkt Image

    [Tooltip("Kaç saniyede belirsin?")]
    [SerializeField]
    private float fadeDuration = 3.0f;

    [Tooltip("Öldükten kaç saniye sonra belirsin?")]
    [SerializeField]
    private float fadeDelay = 1.0f;
    public TMPro.TextMeshProUGUI deathReasonText;

    [Header("Daktilo Ayarları")]
    public float typeSpeed = 0.05f;

    // ----------------------------------------------

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(false);

        // Başlangıçta resmi tamamen şeffaf yap (Alpha = 0)
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true); // Obje açık olsun
            Color c = fadeImage.color;
            c.a = 0f; // Görünmez
            fadeImage.color = c;
        }
    }

    public void ShowDeathScreen(string reason = "")
    {
        Debug.Log("Ölüm Ekranı Açılıyor...");

        HideGameplayUI();

        // Siyah paneli aç
        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(true);

        // Fade işlemini başlat
        if (fadeImage != null)
        {
            StartCoroutine(FadeInImageColorRoutine());
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (deathReasonText != null)
        {
            // Eski kod (direkt yazan): deathReasonText.text = reason;

            // Yeni kod (daktilo efekti):
            StartCoroutine(TypeTextRoutine(reason));
        }
        Time.timeScale = 0f; // Zamanı durdur
    }

   private IEnumerator TypeTextRoutine(string textToType)
{
    deathReasonText.text = ""; // Önce metni tamamen temizle
    
    foreach (char letter in textToType) // Kelimedeki her bir harfi tek tek al
    {
        deathReasonText.text += letter; // Ekrana bir harf daha ekle
        
        // Zaman dursun ya da durmasın, gerçek zamanlı olarak bekle:
        yield return new WaitForSecondsRealtime(typeSpeed); 
    }
}

    private IEnumerator FadeInImageColorRoutine()
    {
        // 1. Bekleme Süresi (Gerçek zamanlı)
        if (fadeDelay > 0)
            yield return new WaitForSecondsRealtime(fadeDelay);

        float timer = 0f;
        Color startColor = fadeImage.color;
        startColor.a = 0f; // 0'dan başla
        fadeImage.color = startColor;

        // 2. Yavaşça Opaklaştır (Alpha'yı 1 yap)
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime; // Oyun donukken zamanı say
            float newAlpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);

            Color c = fadeImage.color;
            c.a = newAlpha;
            fadeImage.color = c;

            yield return null;
        }

        // 3. Garanti olsun diye en son tam 1 yap (255)
        Color finalColor = fadeImage.color;
        finalColor.a = 1f;
        fadeImage.color = finalColor;
    }

    private void HideGameplayUI()
    {
        if (uiElementsToHide != null)
        {
            foreach (var uiObj in uiElementsToHide)
                if (uiObj != null)
                    uiObj.SetActive(false);
        }

        if (ControlsUIManager.Instance != null)
        {
            ControlsUIManager.Instance.HideControls();
            ControlsUIManager.Instance.gameObject.SetActive(false);
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
