using StarterAssets; // Karakter kontrolcüsünü durdurmak için gerekli
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("UI Referansları")]
    [SerializeField]
    private GameObject pauseMenuPanel;

    [SerializeField]
    private string mainMenuSceneName = "MainMenu"; // Ana menü sahnesinin tam adı

    [Header("Oyuncu Referansları")]
    [Tooltip("Karakterin Input Scripti (Mouse kilidini yönetmek için)")]
    [SerializeField]
    private StarterAssetsInputs playerInputs;

    private bool isPaused = false;

    private void Start()
    {
        // Başlangıçta panel kapalı olsun
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        // Oyun başladığında zamanın ve seslerin aktığından emin ol
        Time.timeScale = 1f;
        AudioListener.pause = false; // <--- GARANTİ OLSUN DİYE EKLENDİ

        // Input scriptini otomatik bulmaya çalış
        if (playerInputs == null)
            playerInputs = FindObjectOfType<StarterAssetsInputs>();
    }

    private void Update()
    {
        // YENİ EKLENEN KONTROL: Oyun henüz başlamadıysa ESC tuşunu tamamen yok say!
        if (GameManager.Instance != null && !GameManager.Instance.isGameStarted)
            return;

        // ESC tuşuna basılınca
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;

        // 1. GameManager'a bildir
        if (GameManager.Instance != null)
            GameManager.Instance.isGamePaused = true;

        // 2. Zamanı Durdur
        Time.timeScale = 0f;

        // 3. SESLERİ DURDUR (YENİ EKLENEN KISIM)
        AudioListener.pause = true;

        // 4. Paneli Aç
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);

        // 5. Mouse'u Serbest Bırak ve Görünür Yap
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 6. Karakterin Kamera Dönüşünü Kilitle (StarterAssets için)
        if (playerInputs != null)
        {
            playerInputs.cursorInputForLook = false;
            playerInputs.look = Vector2.zero; // Mevcut ivmeyi sıfırla
        }

        Debug.Log("Oyun Duraklatıldı (Sesler Kesildi).");
    }

    public void ResumeGame()
    {
        isPaused = false;

        // 1. GameManager'a bildir
        if (GameManager.Instance != null)
            GameManager.Instance.isGamePaused = false;

        // 2. Zamanı Devam Ettir
        Time.timeScale = 1f;

        // 3. SESLERİ GERİ AÇ (YENİ EKLENEN KISIM)
        AudioListener.pause = false;

        // 4. Paneli Kapat
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        // 5. Inputları geri aç
        if (playerInputs != null)
            playerInputs.cursorInputForLook = true;

        // --- KRİTİK DÜZELTME BURADA ---
        // Körlemesine fareyi kapatmak yerine, duruma göre karar veriyoruz.

        bool cursorNeeded = false;
        if (GameManager.Instance != null)
        {
            cursorNeeded = GameManager.Instance.IsCursorRequired();
        }

        if (cursorNeeded)
        {
            // Kitapta veya Vanadaysak fare kalsın
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Eğer karakterin kamerasını kilitlemek gerekiyorsa (Kitap okurken kafa dönmesin diye)
            if (playerInputs != null)
                playerInputs.cursorInputForLook = false;
        }
        else
        {
            // Normal oyun veya diğer makinalardaysak fare gitsin
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Debug.Log("Oyun Devam Ediyor. Cursor Durumu: " + (cursorNeeded ? "AÇIK" : "KAPALI"));
    }

    public void LoadMainMenu()
    {
        // Sahne değişirken zamanı mutlaka 1 yapmalıyız, yoksa menü donuk başlar!
        Time.timeScale = 1f;

        // Menüye dönünce sesler geri gelmeli (YENİ EKLENEN KISIM)
        AudioListener.pause = false;

        if (GameManager.Instance != null)
            GameManager.Instance.isGamePaused = false;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Debug.Log("Masaüstüne Çıkılıyor...");
        Application.Quit();
    }
}
