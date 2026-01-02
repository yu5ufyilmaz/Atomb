using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Oyun sahnesinin Build Settings'deki tam adı")]
    [SerializeField]
    private string gameSceneName = "GameScene";

    // --- YENİ ÖZELLİK: GECİKME ---
    [Tooltip("Yükleme ekranı EN AZ kaç saniye görünsün?")]
    [SerializeField]
    private float minLoadTime = 3.0f;

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject mainMenuPanel;

    [SerializeField]
    private GameObject loadingPanel;

    [SerializeField]
    private GameObject creditsPanel;

    [Header("Loading Görselleri")]
    [Tooltip("Daire şeklinde dolacak resimlerin listesi")]
    [SerializeField]
    private Image[] loadingImages;

    private void Start()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void PlayGame()
    {
        StartCoroutine(LoadLevelAsync());
    }

    public void OpenCredits()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Debug.Log("Oyundan Çıkıldı!");
        Application.Quit();
    }

    private IEnumerator LoadLevelAsync()
    {
        // 1. Panelleri ayarla
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        // Görselleri sıfırla
        if (loadingImages != null)
        {
            foreach (Image img in loadingImages)
                if (img != null)
                    img.fillAmount = 0f;
        }

        // 2. Sahneyi arka planda yüklemeye başla AMA sahneye geçiş iznini kapat
        AsyncOperation operation = SceneManager.LoadSceneAsync(gameSceneName);
        operation.allowSceneActivation = false; // <-- ÖNEMLİ: Dolana kadar geçiş yapma

        float elapsedTime = 0f;

        // 3. Döngü: Hem gerçek yüklemeyi hem de bizim yapay süreyi kontrol et
        while (!operation.isDone)
        {
            elapsedTime += Time.deltaTime;

            // A. Gerçek Yükleme Durumu (Unity'de 0.9'da durur, onu 0-1 yapıyoruz)
            float realProgress = Mathf.Clamp01(operation.progress / 0.9f);

            // B. Bizim Yapay Zamanlayıcı (Geçen süre / Hedef süre)
            float fakeProgress = Mathf.Clamp01(elapsedTime / minLoadTime);

            // C. HANGİSİ DAHA AZ İSE ONU GÖSTER
            // Böylece oyun hemen yüklense bile 'fakeProgress' yavaş olduğu için onu bekleriz.
            // Oyun yavaş yüklenirse 'realProgress' yavaş kalır, onu bekleriz.
            float finalProgress = Mathf.Min(realProgress, fakeProgress);

            // UI Güncelle
            if (loadingImages != null)
            {
                foreach (Image img in loadingImages)
                {
                    if (img != null)
                        img.fillAmount = finalProgress;
                }
            }

            // Eğer bar tamamen dolduysa (%100) artık sahneyi açabiliriz
            if (finalProgress >= 1.0f)
            {
                // Küçük bir bekleme daha ekleyip (göz zevki için) sahneyi aktif et
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
