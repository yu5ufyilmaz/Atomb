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

        // 1. Paneli Aç
        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(true);

        // 2. Mouse İmlecini Serbest Bırak
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Arka plandaki oyunu dondur (İsteğe bağlı, jumpscare bittikten sonra mantıklı)
        Time.timeScale = 0f;
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
