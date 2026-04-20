using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Geçiş Ayarları")]
    [Tooltip("Menünün kaybolma (Fade Out) süresi")]
    [SerializeField]
    private float fadeDuration = 1.5f;

    [SerializeField] private GameObject settingsPanel;
    [Header("UI Referansları")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button continueButton;

    private CanvasGroup _menuCanvasGroup;

    private void Start()
    {
        // ... Orijinal Start içeriğin (Hiç dokunmadım) ...
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            _menuCanvasGroup = mainMenuPanel.GetComponent<CanvasGroup>();
            if (_menuCanvasGroup == null) _menuCanvasGroup = mainMenuPanel.AddComponent<CanvasGroup>();
            _menuCanvasGroup.alpha = 1f;
        }
        
        if (continueButton != null)
        {
            if (SaveManager.Instance != null)
            {
                continueButton.interactable = SaveManager.Instance.HasSaveFile();
            }
            else
            {
                continueButton.interactable = false;
            }
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
    }

    // =========================================================
    // 1. MEVCUT PLAY BUTONU (YENİ OYUN - HİÇ BOZULMADI)
    // =========================================================
    public void PlayGame()
    {
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = false;
        }

        // SaveDatasını sıfırla ki yeni oyun verileriyle başlasın
        if (SaveManager.Instance != null) SaveManager.Instance.NewGame();

        // Geçişi başlat (Kayıt Yükleme = FALSE)
        StartCoroutine(StartGameTransitionRoutine(isLoadGame: false));
    }

    // =========================================================
    // 2. YENİ CONTINUE BUTONU (KAYITTAN DEVAM ET)
    // =========================================================
    public void ContinueGame()
    {
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = false;
        }

        // Geçişi başlat (Kayıt Yükleme = TRUE)
        StartCoroutine(StartGameTransitionRoutine(isLoadGame: true));
    }

    // Diğer UI fonksiyonların (OpenSettings, vs. aynı kalıyor)...
    public void OpenSettings() { /* Orijinal kodun */ }
    public void CloseSettings() { /* Orijinal kodun */ }
    public void OpenCredits() { /* Orijinal kodun */ }
    public void CloseCredits() { /* Orijinal kodun */ }
    public void QuitGame() { /* Orijinal kodun */ }

    private IEnumerator StartGameTransitionRoutine(bool isLoadGame)
    {
        float timeElapsed = 0f;

        // 1. Orijinal Fade Out animasyonun (UI yavaşça kaybolur)
        if (_menuCanvasGroup != null)
        {
            while (timeElapsed < fadeDuration)
            {
                _menuCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timeElapsed / fadeDuration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            _menuCanvasGroup.alpha = 0f;
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

        // =======================================================
        // 2. KAYIT YÜKLENİYORSA
        // =======================================================
        if (isLoadGame && SaveManager.Instance != null && SaveManager.Instance.LoadGame())
        {
            // YENİ: Masadan kalkma sistemini iptal et ve kamerayı karaktere tak!
            InGameMenuController menuController = FindObjectOfType<InGameMenuController>();
            if (menuController != null)
            {
                menuController.InstantSetupForLoad();
            }

            // Oyuncunun kilitlerini aç
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGameMode();
            }
            else
            {
                Debug.LogError("HATA: Oyun kaydedildiği yerden başlayacak ama sahnede GameManager objesi yok!");
            }
        }
        // =======================================================
        // 3. YENİ OYUN (Veya eski kayıt yoksa)
        // =======================================================
        else 
        {
            InGameMenuController menuController = FindObjectOfType<InGameMenuController>();
            if (menuController != null)
            {
                // Standart masadan kalkma animasyonu
                menuController.PlayStartSequence();
            }
            else
            {
                if (GameManager.Instance != null)
                {
                    Debug.LogWarning("UYARI: Masadan kalkma (InGameMenuController) bulunamadı. Direkt oyun başlatılıyor.");
                    GameManager.Instance.StartGameMode();
                }
                else
                {
                    Debug.LogError("KRİTİK HATA: Ne InGameMenuController (Masa) ne de GameManager bulunabildi! Oyun başlayamıyor.");
                }
            }
        }
    }
}