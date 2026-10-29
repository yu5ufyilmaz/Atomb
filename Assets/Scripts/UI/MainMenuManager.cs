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
        var panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image), typeof(SettingsUIManager));
        panel.transform.SetParent(canvasTransform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.94f);

        TMPro.TMP_FontAsset font = referenceButton != null
            ? referenceButton.GetComponentInChildren<TMPro.TMP_Text>(true)?.font
            : TMPro.TMP_Settings.defaultFontAsset;

        var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        titleObj.transform.SetParent(panel.transform, false);
        var titleText = titleObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (font != null) titleText.font = font;
        titleText.rectTransform.anchorMin = titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.78f);
        titleText.rectTransform.sizeDelta = new Vector2(420f, 60f);
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.fontSize = 36f;
        titleText.color = Color.white;
        titleText.text = "SETTINGS";
        GameLocalization.Set(titleText, "SETTINGS");

        var langLabelObj = new GameObject("LanguageLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        langLabelObj.transform.SetParent(panel.transform, false);
        var langLabel = langLabelObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (font != null) langLabel.font = font;
        langLabel.rectTransform.anchorMin = langLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.58f);
        langLabel.rectTransform.sizeDelta = new Vector2(360f, 40f);
        langLabel.alignment = TMPro.TextAlignmentOptions.Center;
        langLabel.fontSize = 24f;
        langLabel.color = new Color(0.9f, 0.85f, 0.7f);
        langLabel.text = "LANGUAGE";
        GameLocalization.Set(langLabel, "LANGUAGE");

        var dropdownResources = new TMPro.TMP_DefaultControls.Resources();
        var dropdownObj = TMPro.TMP_DefaultControls.CreateDropdown(dropdownResources);
        dropdownObj.name = "LanguageDropdown";
        dropdownObj.transform.SetParent(panel.transform, false);
        var dropdownRect = (RectTransform)dropdownObj.transform;
        dropdownRect.anchorMin = dropdownRect.anchorMax = new Vector2(0.5f, 0.48f);
        dropdownRect.sizeDelta = new Vector2(280f, 44f);
        var dropdown = dropdownObj.GetComponent<TMPro.TMP_Dropdown>();
        if (font != null)
        {
            foreach (var tmp in dropdownObj.GetComponentsInChildren<TMPro.TMP_Text>(true))
                tmp.font = font;
        }

        var backObj = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
        backObj.transform.SetParent(panel.transform, false);
        var backRect = (RectTransform)backObj.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.25f);
        backRect.sizeDelta = new Vector2(220f, 48f);
        backObj.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f, 0.95f);
        var backBtn = backObj.GetComponent<Button>();
        backBtn.onClick.AddListener(CloseSettings);

        var backTextObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        backTextObj.transform.SetParent(backObj.transform, false);
        var backText = backTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (font != null) backText.font = font;
        backText.rectTransform.anchorMin = Vector2.zero;
        backText.rectTransform.anchorMax = Vector2.one;
        backText.rectTransform.sizeDelta = Vector2.zero;
        backText.alignment = TMPro.TextAlignmentOptions.Center;
        backText.fontSize = 24f;
        backText.color = Color.white;
        backText.text = "Back";
        GameLocalization.Set(backText, "Back");

        var settingsUI = panel.GetComponent<SettingsUIManager>();
        settingsUI.languageDropdown = dropdown;
        settingsUI.EnsureLanguageSelector();
        panel.SetActive(false);
        return panel;
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
        if (loadError != null)
        {
            GameLocalization.Set(loadError, SaveManager.Instance?.LastLoadError ?? "Kayıt yöneticisi bulunamadı.");
            loadError.gameObject.SetActive(true);
        }
    }
}
