using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathUIManager : MonoBehaviour
{
    public static DeathUIManager Instance;

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject deathScreenPanel;

    [SerializeField]
    private string mainMenuSceneName = "MainMenu"; // Ana menü sahnesinin tam adı

    [Header("Gizlenecek Arayüzler")]
    [Tooltip(
        "Ölünce kapanması gereken objeleri buraya sürükle (Örn: PressureBar, EnergyBar, Crosshair, ControlsUI)"
    )]
    [SerializeField]
    private List<GameObject> uiElementsToHide;

    private void Awake()
    {
        // Singleton yapısı: Her yerden erişebilmek için
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(false);
    }

    // Bu fonksiyonu diğer scriptlerden çağıracağız
    public void ShowDeathScreen()
    {
        Debug.Log("Ölüm Ekranı Açılıyor...");
        HideGameplayUI();
        // 1. Paneli Aç
        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(true);

        // 2. Mouse İmlecini Serbest Bırak
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Arka plandaki oyunu dondur (İsteğe bağlı, jumpscare bittikten sonra mantıklı)
        Time.timeScale = 0f;
    }

    private void HideGameplayUI()
    {
        // A) Listeye eklediğin objeleri kapat (Bar, Crosshair vb.)
        if (uiElementsToHide != null)
        {
            foreach (var uiObj in uiElementsToHide)
            {
                if (uiObj != null)
                    uiObj.SetActive(false);
            }
        }

        // B) Not Defterini Zorla Kapat
        if (NotebookUI.Instance != null)
        {
            NotebookUI.Instance.ForceClose();
        }

        // C) Makine Kontrol Yazılarını Kapat
        if (ControlsUIManager.Instance != null)
        {
            ControlsUIManager.Instance.HideControls();
            // ControlsUI'ın ana objesini de kapatmak istersen:
            ControlsUIManager.Instance.gameObject.SetActive(false);
        }

        // D) Pause Menüsü Açıksa Kapat
        PauseManager pauseMan = FindObjectOfType<PauseManager>();
        if (pauseMan != null)
        {
            // PauseManager içinde public bir kapatma fonksiyonu yoksa panelini kapatabilirsin
            // Ama genelde uiElementsToHide listesine PausePanel'i eklemek daha kolaydır.
        }
    }

    // Butona bağlanacak fonksiyon
    public void LoadMainMenu()
    {
        // Zamanı normale döndür (Yoksa menü donuk başlar)
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // İstersen "Tekrar Dene" butonu için:
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
