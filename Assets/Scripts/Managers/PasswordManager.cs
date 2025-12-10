using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;

    [Header("Şifre Üretim Ayarları")]
    [SerializeField]
    private WordPool wordPool;

    // Turing Makinesindeki sembollerin AYNISI olmalı
    private readonly string[] validSymbols =
    {
        ">=",
        "+",
        "-",
        "/",
        "√",
        "%",
        "<=",
        "=",
        "<",
        ">",
        ".",
        ",",
    };

    [Header("Tüm Olası Şifre Konum Verileri")]
    [SerializeField]
    private List<PasswordData> allPossiblePasswordData;

    [Header("Sahnedeki Potansiyel Şifreli Kitaplar")]
    [SerializeField]
    private List<InteractableBook> allPasswordBooksInLevel;

    [Header("Mevcut Oyun Durumu")]
    [Tooltip("Turing makinesindeki gösterge (ışık) sayısı ile aynı olmalı.")]
    [SerializeField]
    private int requiredPasswordCount = 5;

    private List<string> requiredPasswords = new List<string>();
    private List<string> discoveredClues = new List<string>();
    private List<string> validatedPasswords = new List<string>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        InitializeNewGame();
    }

    public void InitializeNewGame()
    {
        discoveredClues.Clear();
        validatedPasswords.Clear();
        requiredPasswords.Clear();

        foreach (var book in allPasswordBooksInLevel)
        {
            book.ClearPassword();
        }

        // Verileri karıştır
        var shuffledPasswordData = allPossiblePasswordData.OrderBy(x => Random.value).ToList();
        var shuffledBooks = allPasswordBooksInLevel.OrderBy(x => Random.value).ToList();

        for (int i = 0; i < requiredPasswordCount; i++)
        {
            if (i >= shuffledPasswordData.Count || i >= shuffledBooks.Count)
            {
                Debug.LogWarning("Yeterli sayıda kitap veya şifre verisi yok!", this);
                break;
            }

            PasswordData locationData = shuffledPasswordData[i];
            InteractableBook bookToAssign = shuffledBooks[i];

            // --- YENİ ŞİFRE ÜRETİM MANTIĞI ---

            // 1. Kelime Havuzundan Kelime Çek
            string randomWord = (wordPool != null) ? wordPool.GetRandomWord() : "ERROR";

            // 2. Sabit Listeden Rastgele Sembol Çek
            string randomSymbol = validSymbols[Random.Range(0, validSymbols.Length)];

            // 3. 3 Haneli Sayı Üret (000 - 999 arası)
            // "D3" formatı sayesinde 5 -> "005", 89 -> "089" olur.
            string randomNumber = Random.Range(0, 1000).ToString("D3");

            // Format: KELIME_SEMBOL_SAYI (Örn: RED_>_012)
            string generatedPasswordID = $"{randomWord}_{randomSymbol}_{randomNumber}";

            // ---------------------------------

            // Kitaba ata
            bookToAssign.AssignPassword(locationData, generatedPasswordID);
            requiredPasswords.Add(generatedPasswordID);
        }

        Debug.Log($"Yeni oyun başlatıldı. {requiredPasswords.Count} adet şifre atandı.");
    }

    // --- DİĞER FONKSİYONLAR AYNEN KALIYOR ---
    public void DiscoverClue(string passwordID)
    {
        if (requiredPasswords.Contains(passwordID) && !discoveredClues.Contains(passwordID))
        {
            discoveredClues.Add(passwordID);
            if (NotebookUI.Instance != null)
                NotebookUI.Instance.ShowPasswordNotification(passwordID);
        }
    }

    public bool ValidatePassword(string passwordID)
    {
        if (requiredPasswords.Contains(passwordID) && !validatedPasswords.Contains(passwordID))
        {
            validatedPasswords.Add(passwordID);
            return true;
        }
        return false;
    }

    public List<string> GetDiscoveredClues() => discoveredClues;

    public int GetValidatedPasswordCount() => validatedPasswords.Count;

    public bool HasFoundAllRequiredPasswords() =>
        validatedPasswords.Count == requiredPasswords.Count;

    public int GetTotalRequiredCount() => requiredPasswordCount;

    public int GetFoundCount() => discoveredClues.Count;

    public int GetValidatedCount() => validatedPasswords.Count;

    public List<string> GetFoundPasswordsList() => discoveredClues;
}
