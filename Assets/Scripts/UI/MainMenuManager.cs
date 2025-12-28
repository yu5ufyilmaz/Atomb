using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Slider için gerekli

public class MainMenuManager : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Oyun sahnesinin Build Settings'deki tam adı")]
    [SerializeField]
    private string gameSceneName = "GameScene"; // Sahne adını buraya yazacaksın

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject mainMenuPanel;

    [SerializeField]
    private GameObject loadingPanel;

    [SerializeField]
    private Slider loadingSlider;

    private void Start()
    {
        // Menü açıldığında Loading ekranını gizle, ana menüyü aç
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        // Mouse imlecini serbest bırak ve görünür yap (Oyundan çıkıp menüye dönünce önemli)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void PlayGame()
    {
        // Play butonuna atayacağımız fonksiyon
        StartCoroutine(LoadLevelAsync());
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        // Quit butonuna atayacağımız fonksiyon
        Debug.Log("Oyundan Çıkıldı!");
        Application.Quit();
    }

    private IEnumerator LoadLevelAsync()
    {
        // 1. Loading ekranını aç, menüyü kapat
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        // 2. Sahneyi asenkron yüklemeye başla
        AsyncOperation operation = SceneManager.LoadSceneAsync(gameSceneName);

        // 3. Yükleme bitene kadar bekle ve slider'ı güncelle
        while (!operation.isDone)
        {
            // operation.progress 0 ile 0.9 arasında değer döndürür.
            // Bunu 0-1 arasına yaymak için matematiksel işlem yapıyoruz.
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (loadingSlider != null)
                loadingSlider.value = progress;

            yield return null;
        }
    }
}
