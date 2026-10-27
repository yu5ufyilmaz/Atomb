using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

private GameData gameData;
private List<ISaveable> saveableObjects = new List<ISaveable>(); // Liste artık hazır!
private string saveFileName = "SenzoraLocalSave.json";
    [Tooltip("Geçiş denemesinde ana oyunun kayıt dosyasını okumaz veya değiştirmez.")]
    public bool sessionOnly;
    [Tooltip("Geliştirici denemeleri için ayrı dosya yolu; boşsa normal kayıt kullanılır.")]
    public string isolatedSavePath;
    private string SavePath => string.IsNullOrEmpty(isolatedSavePath) ? Path.Combine(Application.persistentDataPath, saveFileName) : isolatedSavePath;
    public bool SaveBlocked { get; private set; }
    public string LastLoadError { get; private set; }
    private ResearchPuzzleTrial SceneTrial => ResearchPuzzleTrial.Active != null ? ResearchPuzzleTrial.Active
        : FindFirstObjectByType<ResearchPuzzleTrial>(FindObjectsInactive.Include);

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // Obje aktif olduğunda listeye yazdırılacak
    public void RegisterSaveable(ISaveable saveable)
    {
        if (!saveableObjects.Contains(saveable))
        {
            saveableObjects.Add(saveable);
        }
    }

    // Obje silindiğinde veya pasife düştüğünde listeden çıkacak
    public void UnregisterSaveable(ISaveable saveable)
    {
        if (saveableObjects.Contains(saveable))
        {
            saveableObjects.Remove(saveable);
        }
    }

    public void NewGame()
    {
        try { SceneTrial?.StartNewSession(); }
        catch (System.Exception exception) { FailLoad("Yeni puzzle başlatılamadı: " + exception.Message); return; }
        gameData = new GameData();
        if (NotebookUI.Instance != null) NotebookUI.Instance.ResetWorkspace();
        if (ChalkboardManager.Instance != null) ChalkboardManager.Instance.ResetBoard();
        SaveBlocked = false; LastLoadError = null;

        if (sessionOnly) return;
        string path = SavePath;
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Yeni Oyun Başlatıldı: Eski kayıt dosyası KÖKÜNDEN silindi!");
        }
    }

    public bool HasSaveFile()
    {
        if (sessionOnly) return false;
        string path = SavePath;
        return System.IO.File.Exists(path);
    }

    public bool LoadGame()
    {
        if (sessionOnly) return FailLoad("Bu deneme sahnesinde kalıcı kayıt yükleme kapalı.");
        string path = SavePath;

        // Gelecekte buraya Steamworks Load mantığı eklenecek

        try
        {
            if (!File.Exists(path)) return FailLoad("Kayıt dosyası bulunamadı.");
            string json = File.ReadAllText(path);
            var loaded = JsonUtility.FromJson<GameData>(json);
            if (loaded == null) return FailLoad("Kayıt verisi okunamadı.");
            var trial = SceneTrial;
            if (loaded.puzzleSession != null && trial == null) return FailLoad("Kayıtlı puzzle için sahne bağlantısı eksik.");
            trial?.RestoreSession(loaded);
            gameData = loaded;

            // Tüm objelere veriyi yükle
            foreach (ISaveable saveable in saveableObjects.ToArray())
            {
                saveable.LoadData(gameData);
            }
            SaveBlocked = false; LastLoadError = null;
            Debug.Log("Kayıt Yüklendi: " + path);
            return true; // Kayıt var ve yüklendi
        }
        catch (System.Exception exception) { return FailLoad("Kayıt yüklenemedi; dosya korundu: " + exception.Message); }
    }

    private bool FailLoad(string message)
    {
        SaveBlocked = true; LastLoadError = message;
        Debug.LogWarning(message); return false;
    }

    public void SaveGame()
    {
        if (sessionOnly || SaveBlocked) return;
        if (SceneTrial != null && SceneTrial.CurrentPuzzle == null) { FailLoad("Puzzle bağlantısı kurulmadı; mevcut kayıt korunuyor."); return; }
        if (gameData == null)
            gameData = new GameData();

        gameData.lastSavedTime = System.DateTime.Now.ToString();
        ResearchPuzzleTrial.Active?.CaptureSession(gameData);

        // Sahnedeki tüm objelerden veriyi topla
        foreach (ISaveable saveable in saveableObjects.ToArray())
        {
            saveable.SaveData(ref gameData);
        }

        // JSON olarak AppData'ya kaydet
        string json = JsonUtility.ToJson(gameData, true);
        string path = SavePath;
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, json);

        // Gelecekte buraya Steamworks Save mantığı eklenecek

        Debug.Log("Oyun Kaydedildi: " + path);
    }

    // SaveManager.cs içine eklenecek
    private void OnApplicationQuit()
    {
        // Sadece oyun aktif olarak başladıysa kaydet (Ana menüde çıkarsa boşuna kaydetmesin)
        if (!sessionOnly && GameManager.Instance != null && GameManager.Instance.isGameStarted)
        {
            SaveGame();
            Debug.Log("Oyundan çıkılıyor... Son durum otomatik kaydedildi.");
        }
    }

    // Opsiyonel: Oyun arka plana atılırsa (Özellikle ileride Steam Overlay vs. gelirse iyi olur)
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!sessionOnly && pauseStatus && GameManager.Instance != null && GameManager.Instance.isGameStarted)
        {
            SaveGame();
        }
    }
}
