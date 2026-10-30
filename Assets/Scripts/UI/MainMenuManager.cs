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
    private TutorialChoicePrompt tutorialChoice;
    private bool awaitingTutorialChoice, startingGame;
    public bool IsTutorialChoiceOpen => awaitingTutorialChoice;

    private void Start()
    {
        EnsureSettingsAccess();
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

    public void EnsureSettingsAccess()
    {
        Transform searchRoot = mainMenuPanel != null ? mainMenuPanel.transform : transform.root;
        Button settingsButton = null;
        foreach (var btn in searchRoot.GetComponentsInChildren<Button>(true))
        {
            if (string.Equals(btn.gameObject.name, "Settings", System.StringComparison.OrdinalIgnoreCase))
            {
                settingsButton = btn;
                break;
            }
        }
        if (settingsButton != null)
        {
            settingsButton.enabled = true;
            settingsButton.interactable = true;
            if (settingsButton.onClick.GetPersistentEventCount() == 0)
            {
                settingsButton.onClick.RemoveListener(OpenSettings);
                settingsButton.onClick.AddListener(OpenSettings);
            }
        }

        if (settingsPanel == null)
        {
            var canvas = mainMenuPanel != null ? mainMenuPanel.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var existingUI = canvas.GetComponentInChildren<SettingsUIManager>(true);
                if (existingUI != null)
                    settingsPanel = existingUI.gameObject;
                else
                    settingsPanel = BuildFallbackSettingsPanel(canvas.transform, settingsButton);
            }
        }

        if (settingsPanel != null)
        {
            var ui = settingsPanel.GetComponentInChildren<SettingsUIManager>(true);
            if (ui != null) ui.EnsureLanguageSelector();
        }
    }

    private GameObject BuildFallbackSettingsPanel(Transform canvasTransform, Button referenceButton)
    {
        SettingsManager.EnsureInstance();
        var panel = new GameObject("SettingsPanel", typeof(RectTransform));
        panel.SetActive(false);
        panel.transform.SetParent(canvasTransform, false);
        SettingsMenuStyle.Stretch((RectTransform)panel.transform);
        panel.AddComponent<SettingsUIManager>();
        return panel;
    }
    // =========================================================
    // 1. MEVCUT PLAY BUTONU (YENİ OYUN - HİÇ BOZULMADI)
    // =========================================================
    public void PlayGame()
    {
        if (startingGame || awaitingTutorialChoice) return;
        if (TutorialManager.Instance == null) { BeginNewGame(false); return; }
        awaitingTutorialChoice = true;
        if (tutorialChoice == null)
        {
            var prompt = new GameObject("Tutorial Choice", typeof(RectTransform), typeof(TutorialChoicePrompt));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prompt, gameObject.scene);
            tutorialChoice = prompt.GetComponent<TutorialChoicePrompt>();
        }
        if (_menuCanvasGroup != null) { _menuCanvasGroup.interactable = false; _menuCanvasGroup.blocksRaycasts = false; }
        tutorialChoice.Show(ChooseTutorial, CancelTutorialChoice);
    }

    public void ChooseTutorial(bool enabled)
    {
        if (!awaitingTutorialChoice) return;
        awaitingTutorialChoice = false; tutorialChoice.Hide();
        BeginNewGame(enabled);
    }

    public void CancelTutorialChoice()
    {
        if (!awaitingTutorialChoice) return;
        awaitingTutorialChoice = false;
        if (_menuCanvasGroup != null) { _menuCanvasGroup.interactable = true; _menuCanvasGroup.blocksRaycasts = true; }
        tutorialChoice.Hide();
    }

    private void OnDisable() { CancelTutorialChoice(); }
    private void OnDestroy() { if (tutorialChoice != null) Destroy(tutorialChoice.gameObject); }

    private void BeginNewGame(bool tutorialEnabled)
    {
        startingGame = true;
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = false;
        }

        // Only enabled tutorial events may add lessons to the new research session.
        var tutorialEvents = TutorialManager.Instance != null ? TutorialManager.Instance.tutorialEventsParent : null;
        bool tutorialWasActive = tutorialEvents != null && tutorialEvents.activeSelf;
        if (tutorialEvents != null) tutorialEvents.SetActive(tutorialEnabled);

        // SaveDatasını sıfırla ki yeni oyun verileriyle başlasın
        if (SaveManager.Instance != null)
            SaveManager.Instance.NewGame();
        if (SaveManager.Instance != null && SaveManager.Instance.SaveBlocked)
        {
            if (tutorialEvents != null) tutorialEvents.SetActive(tutorialWasActive);
            RestoreMenuAfterFailure(); return;
        }
        TutorialManager.Instance?.SetTutorialEnabled(tutorialEnabled);
        if (loadError != null) loadError.gameObject.SetActive(false);
        // Geçişi başlat (Kayıt Yükleme = FALSE)
        StartCoroutine(StartGameTransitionRoutine(isLoadGame: false));
    }

    // =========================================================
    // 2. YENİ CONTINUE BUTONU (KAYITTAN DEVAM ET)
    // =========================================================
    public void ContinueGame()
    {
        if (startingGame || awaitingTutorialChoice) return;
        startingGame = true;
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
        if (startingGame || awaitingTutorialChoice) return;
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        var view = settingsPanel != null ? settingsPanel.GetComponentInChildren<SettingsMenuView>(true) : null;
        if (view != null) view.SetCloseAction(CloseSettingsImmediately);
    }

    public void CloseSettings()
    {
        var view = settingsPanel != null ? settingsPanel.GetComponentInChildren<SettingsMenuView>(true) : null;
        if (view != null) view.RequestClose(CloseSettingsImmediately); else CloseSettingsImmediately();
    }

    private void CloseSettingsImmediately()
    {
        var view = settingsPanel != null ? settingsPanel.GetComponentInChildren<SettingsMenuView>(true) : null;
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        view?.RestoreFocusTo(mainMenuPanel);
    }

    public void OpenCredits()
    {
        if (startingGame || awaitingTutorialChoice) return;
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
        startingGame = false;
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
        if (loadError != null)
        {
            GameLocalization.Set(loadError, SaveManager.Instance?.LastLoadError ?? "Kayıt yöneticisi bulunamadı.");
            loadError.gameObject.SetActive(true);
        }
    }
}
