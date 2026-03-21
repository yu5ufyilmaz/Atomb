using System.Collections;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Geçiş Ayarları")]
    [Tooltip("Menünün kaybolma (Fade Out) süresi")]
    [SerializeField]
    private float fadeDuration = 1.5f;

    [SerializeField]
    private GameObject settingsPanel; // <--- YENİ EKLENDİ

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject mainMenuPanel;

    [SerializeField]
    private GameObject creditsPanel;

    // CanvasGroup, panelin şeffaflığını (Alpha) kodla kısmamızı sağlar
    private CanvasGroup _menuCanvasGroup;

    private void Start()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false); // <--- YENİ EKLENDİ
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);

            // Eğer MainMenuPanel'de CanvasGroup yoksa otomatik ekle
            _menuCanvasGroup = mainMenuPanel.GetComponent<CanvasGroup>();
            if (_menuCanvasGroup == null)
            {
                _menuCanvasGroup = mainMenuPanel.AddComponent<CanvasGroup>();
            }
            _menuCanvasGroup.alpha = 1f; // Tamamen görünür
        }

        // Oyun başlarken fare serbest ve görünür olmalı
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Play Butonuna tıklandığında bu çalışacak
    public void PlayGame()
    {
        // Tıklamayı aldıktan sonra menüdeki butonlara tekrar basılmasını engelle
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = false;
        }

        StartCoroutine(StartGameTransitionRoutine());
    }

    public void OpenSettings()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
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

    // --- YENİ: AYNI SAHNEDE OYUNA GEÇİŞ (FADE OUT) ---
    // MainMenuManager.cs İÇİNDEKİ YENİ STARTGAME FONKSİYONU
    private IEnumerator StartGameTransitionRoutine()
    {
        float timeElapsed = 0f;

        // 1. 2D UI Menüyü yavaşça şeffaflaştır (Fade Out)
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

        // 2. DOĞRUDAN OYUNU BAŞLATMAK YERİNE STAND-UP SEKANSINI TETİKLE!
        InGameMenuController menuController = FindObjectOfType<InGameMenuController>();
        if (menuController != null)
        {
            // Eğer sahnedeki masada InGameMenuController varsa, ayağa kalkma sekansını başlat
            menuController.PlayStartSequence();
        }
        else
        {
            // Eğer hata olur da controller bulunamazsa, direkt oyunu başlat (Güvenlik önlemi)
            GameManager.Instance.StartGameMode();
        }
    }
}
