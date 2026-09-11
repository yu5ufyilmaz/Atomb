using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Geçiş yapılacak ana oyun sahnesinin adı")]
    public string gameSceneName = "GameScene";

    [Tooltip("Yükleme ekranı EN AZ kaç saniye görünsün?")]
    public float minLoadTime = 3.0f;

    // --- YENİ EKLENEN AYAR ---
    [Tooltip("Ekranların belirme/kaybolma süresi (Saniye)")]
    public float fadeDuration = 1.0f;

    [Header("UI Referansları")]
    public GameObject pressAnyKeyPanel; // İçinde Press Any Key yazısı olan panel
    public GameObject loadingPanel; // Yükleme ekranı paneli (içinde dolan resimler var)

    [Header("Loading Görselleri")]
    public Image[] loadingImages;

    private bool isKeyPressed = false;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Başlangıçta panelleri ayarla ve tamamen şeffaf (0) yap
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            SetAlpha(loadingPanel, 0f);
        }

        if (pressAnyKeyPanel != null)
        {
            pressAnyKeyPanel.SetActive(true);
            SetAlpha(pressAnyKeyPanel, 0f);

            // Oyun açılır açılmaz "Press Any Key" yazısını yavaşça göster
            StartCoroutine(FadeCanvasGroup(pressAnyKeyPanel, 0f, 1f, fadeDuration));
        }
    }

    void Update()
    {
        // Eğer henüz bir tuşa basılmadıysa ve herhangi bir tuşa basılırsa
        if (!isKeyPressed && Input.anyKeyDown)
        {
            isKeyPressed = true;
            // Direkt yüklemeye geçmek yerine, önce geçiş animasyonlarını (fade) başlat
            StartCoroutine(StartTransitionSequence());
        }
    }

    private IEnumerator StartTransitionSequence()
    {
        // 1. Press Any Key panelini yavaşça kaybet (Alpha 1'den 0'a)
        if (pressAnyKeyPanel != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(pressAnyKeyPanel, 1f, 0f, fadeDuration));
            pressAnyKeyPanel.SetActive(false);
        }

        // 2. Loading panelini yavaşça göster (Alpha 0'dan 1'e)
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            yield return StartCoroutine(FadeCanvasGroup(loadingPanel, 0f, 1f, fadeDuration));
        }

        // 3. UI geçişleri bittikten sonra asıl yükleme ve bar dolum işlemini başlat
        StartCoroutine(LoadLevelAsync());
    }

    private IEnumerator LoadLevelAsync()
    {
        // Görsellerin doluluk oranını sıfırla
        if (loadingImages != null)
        {
            foreach (Image img in loadingImages)
                if (img != null)
                    img.fillAmount = 0f;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(gameSceneName);
        operation.allowSceneActivation = false;

        float elapsedTime = 0f;

        while (!operation.isDone)
        {
            elapsedTime += Time.deltaTime;

            float realProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float fakeProgress = Mathf.Clamp01(elapsedTime / minLoadTime);
            float finalProgress = Mathf.Min(realProgress, fakeProgress);

            if (loadingImages != null)
            {
                foreach (Image img in loadingImages)
                {
                    if (img != null)
                        img.fillAmount = finalProgress;
                }
            }

            if (finalProgress >= 1.0f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    // --- YARDIMCI FONKSİYONLAR ---

    // Panele CanvasGroup ekler ve direkt istenilen şeffaflık değerini atar
    private void SetAlpha(GameObject panel, float alpha)
    {
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    // Panelin şeffaflığını verilen sürede (duration) başlangıçtan (start) hedefe (target) doğru yumuşatır
    private IEnumerator FadeCanvasGroup(
        GameObject panel,
        float startAlpha,
        float targetAlpha,
        float duration
    )
    {
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = panel.AddComponent<CanvasGroup>();

        float elapsedTime = 0f;
        cg.alpha = startAlpha;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            // Mathf.Lerp, iki değer arasında zamana bağlı yumuşak geçiş hesaplar
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }

        // Döngü bitince tam hedef değeri ata (küsurat kalmasın diye)
        cg.alpha = targetAlpha;
    }
}
