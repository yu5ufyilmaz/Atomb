using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Geçiş Ayarları")]
    [Tooltip("Menünün kaybolma (Fade Out) süresi")]
    [SerializeField]
    private float fadeDuration = 1.5f;

    [SerializeField]
    private GameObject settingsPanel;

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject mainMenuPanel;

    [SerializeField]
    private GameObject creditsPanel;

    [SerializeField]
    private Button continueButton;

    private CanvasGroup _menuCanvasGroup;
    private TMPro.TextMeshProUGUI loadError;

    private void Start()
    {
        // ... Orijinal Start içeriğin (Hiç dokunmadım) ...
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            _menuCanvasGroup = mainMenuPanel.GetComponent<CanvasGroup>();
            if (_menuCanvasGroup == null)
                _menuCanvasGroup = mainMenuPanel.AddComponent<CanvasGroup>();
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
        if (SaveManager.Instance != null)
            SaveManager.Instance.NewGame();
        if (SaveManager.Instance != null && SaveManager.Instance.SaveBlocked) { RestoreMenuAfterFailure(); return; }
        if (loadError != null) loadError.gameObject.SetActive(false);
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
    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }

    public void OpenCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(true);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
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
#else
        Application.Quit();
#endif
    }

    private IEnumerator StartGameTransitionRoutine(bool isLoadGame)
    {
        //TutorialManager.Instance.SetupTutorialMode();
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

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        // =======================================================
        // 2. KAYIT YÜKLENİYORSA
        // =======================================================
        if (isLoadGame && (SaveManager.Instance == null || !SaveManager.Instance.LoadGame()))
        {
            RestoreMenuAfterFailure();
            yield break;
        }
        if (isLoadGame)
        {
            // YENİ: Masadan kalkma sistemini iptal et ve kamerayı karaktere tak!
            InGameMenuController menuController =
                Object.FindFirstObjectByType<InGameMenuController>();
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
                Debug.LogError(
                    "HATA: Oyun kaydedildiği yerden başlayacak ama sahnede GameManager objesi yok!"
                );
            }
        }
        // =======================================================
        // 3. YENİ OYUN (Veya eski kayıt yoksa)
        // =======================================================
        else
        {
            InGameMenuController menuController =
                Object.FindFirstObjectByType<InGameMenuController>();
            if (menuController != null)
            {
                // Standart masadan kalkma animasyonu
                menuController.PlayStartSequence();
            }
            else
            {
                if (GameManager.Instance != null)
                {
                    Debug.LogWarning(
                        "UYARI: Masadan kalkma (InGameMenuController) bulunamadı. Direkt oyun başlatılıyor."
                    );
                    GameManager.Instance.StartGameMode();
                }
                else
                {
                    Debug.LogError(
                        "KRİTİK HATA: Ne InGameMenuController (Masa) ne de GameManager bulunabildi! Oyun başlayamıyor."
                    );
                }
            }
        }
    }

    private void RestoreMenuAfterFailure()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (_menuCanvasGroup != null) { _menuCanvasGroup.alpha = 1; _menuCanvasGroup.interactable = true; _menuCanvasGroup.blocksRaycasts = true; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (loadError == null && mainMenuPanel != null)
        {
            loadError = new GameObject("Kayıt Yükleme Hatası", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            loadError.transform.SetParent(mainMenuPanel.transform, false);
            loadError.rectTransform.anchorMin = loadError.rectTransform.anchorMax = new Vector2(.5f, .12f);
            loadError.rectTransform.sizeDelta = new Vector2(550, 90);
            loadError.fontSize = 18; loadError.enableAutoSizing = true; loadError.fontSizeMin = 12; loadError.fontSizeMax = 18;
            loadError.color = new Color(1, .75f, .65f); loadError.alignment = TMPro.TextAlignmentOptions.Center; loadError.raycastTarget = false;
        }
        if (loadError != null) { loadError.text = SaveManager.Instance?.LastLoadError ?? "Kayıt yöneticisi bulunamadı."; loadError.gameObject.SetActive(true); }
    }
}
