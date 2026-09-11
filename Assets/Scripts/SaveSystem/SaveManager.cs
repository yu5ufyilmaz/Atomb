using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private GameData gameData;
    private List<ISaveable> saveableObjects;
    private string saveFileName = "SenzoraLocalSave.json";

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void RefreshSaveables()
    {
        IEnumerable<ISaveable> saveables = FindObjectsOfType<MonoBehaviour>(true)
            .OfType<ISaveable>();
        saveableObjects = new List<ISaveable>(saveables);
    }

    public void NewGame()
    {
        gameData = new GameData();

        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Yeni Oyun Başlatıldı: Eski kayıt dosyası KÖKÜNDEN silindi!");
        }
    }

    public bool HasSaveFile()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, saveFileName);
        return System.IO.File.Exists(path);
    }

    public bool LoadGame()
    {
        RefreshSaveables();
        string path = Path.Combine(Application.persistentDataPath, saveFileName);

        // Gelecekte buraya Steamworks Load mantığı eklenecek

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            gameData = JsonUtility.FromJson<GameData>(json);

            // Tüm objelere veriyi yükle
            foreach (ISaveable saveable in saveableObjects)
            {
                saveable.LoadData(gameData);
            }
            Debug.Log("Kayıt Yüklendi: " + path);
            return true; // Kayıt var ve yüklendi
        }

        Debug.Log("Kayıt bulunamadı!");
        return false; // Kayıt yok
    }

    public void SaveGame()
    {
        RefreshSaveables();
        if (gameData == null)
            gameData = new GameData();

        gameData.lastSavedTime = System.DateTime.Now.ToString();

        // Sahnedeki tüm objelerden veriyi topla
        foreach (ISaveable saveable in saveableObjects)
        {
            saveable.SaveData(ref gameData);
        }

        // JSON olarak AppData'ya kaydet
        string json = JsonUtility.ToJson(gameData, true);
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        File.WriteAllText(path, json);

        // Gelecekte buraya Steamworks Save mantığı eklenecek

        Debug.Log("Oyun Kaydedildi: " + path);
    }

    // SaveManager.cs içine eklenecek
    private void OnApplicationQuit()
    {
        // Sadece oyun aktif olarak başladıysa kaydet (Ana menüde çıkarsa boşuna kaydetmesin)
        if (GameManager.Instance != null && GameManager.Instance.isGameStarted)
        {
            SaveGame();
            Debug.Log("Oyundan çıkılıyor... Son durum otomatik kaydedildi.");
        }
    }

    // Opsiyonel: Oyun arka plana atılırsa (Özellikle ileride Steam Overlay vs. gelirse iyi olur)
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && GameManager.Instance != null && GameManager.Instance.isGameStarted)
        {
            SaveGame();
        }
    }
}
