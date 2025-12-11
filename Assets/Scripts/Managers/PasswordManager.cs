using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;

    [Header("Şifre Üretim Ayarları")]
    [Tooltip("Kelime havuzu (WordPool) scriptable object'ini buraya ata.")]
    [SerializeField]
    private WordPool wordPool;

    // Sabit sembol listesi (Turing makinesindeki ile aynı olmalı)
    private readonly string[] symbols =
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

    [Header("Makineler (2 Şifre Buraya)")]
    [SerializeField]
    private InteractableOscilloscope oscilloscope;

    [SerializeField]
    private InteractableMassSpectrometer spectrometer;

    [Header("Kitaplar (Kalan Şifreler Buraya)")]
    [Tooltip("Sahnedeki şifre çıkma ihtimali olan TÜM kitapları buraya listele.")]
    [SerializeField]
    private List<InteractableBook> allBooksInLevel;

    [Tooltip("Toplam kaç şifre üretilsin? (Makineler dahil)")]
    [SerializeField]
    private int totalPasswordsNeeded = 5;

    // Takip Listeleri
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

        Debug.Log("PasswordManager: Yeni oyun başlatılıyor...");

        int machineCount = 2;
        int bookCount = totalPasswordsNeeded - machineCount;

        var eligibleBooks = allBooksInLevel
            .Where(b =>
                b.canContainPassword
                && b.bookIdentity != null
                && b.bookIdentity.possibleLocations.Count > 0
            )
            .ToList();

        if (eligibleBooks.Count < bookCount)
        {
            Debug.LogError(
                $"Yeterli sayıda uygun kitap yok! Gereken Kitap Şifresi: {bookCount}, Uygun Kitap: {eligibleBooks.Count}"
            );
            return;
        }

        // A) OSİLOSKOP ŞİFRESİ
        if (oscilloscope != null)
        {
            string pass1 = GenerateRandomPassword();
            oscilloscope.AssignPassword(pass1);
            requiredPasswords.Add(pass1);
            Debug.Log($"[Şifre 1] Osiloskop'a atandı: {pass1}");
        }

        // B) SPEKTROMETRE ŞİFRESİ
        if (spectrometer != null)
        {
            string pass2 = GenerateRandomPassword();
            spectrometer.AssignPassword(pass2);
            requiredPasswords.Add(pass2);
            Debug.Log($"[Şifre 2] Spektrometre'ye atandı: {pass2}");
        }

        // C) KİTAP ŞİFRELERİ
        var selectedBooks = eligibleBooks.OrderBy(x => Random.value).Take(bookCount).ToList();

        foreach (var book in selectedBooks)
        {
            string bookPass = GenerateRandomPassword();
            int randomLocIndex = Random.Range(0, book.bookIdentity.possibleLocations.Count);

            book.AssignPassword(bookPass, randomLocIndex);
            requiredPasswords.Add(bookPass);
            Debug.Log($"[Kitap Şifresi] {book.name} kitabına atandı: {bookPass}");
        }

        Debug.Log($"DAĞITIM TAMAMLANDI! Toplam {requiredPasswords.Count} adet şifre aktif.");
    }

    private string GenerateRandomPassword()
    {
        string w = (wordPool != null) ? wordPool.GetRandomWord() : "NULL";
        string s = symbols[Random.Range(0, symbols.Length)];
        string n = Random.Range(0, 1000).ToString("D3");
        return $"{w}_{s}_{n}";
    }

    public void DiscoverClue(string passwordID)
    {
        if (!requiredPasswords.Contains(passwordID))
            return;
        if (discoveredClues.Contains(passwordID))
            return;

        discoveredClues.Add(passwordID);

        if (NotebookUI.Instance != null)
            NotebookUI.Instance.ShowPasswordNotification(passwordID);

        Debug.Log($"YENİ İPUCU KAYDEDİLDİ: {passwordID}");
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

    // --- GETTERS (DÜZELTİLEN KISIM) ---
    public int GetValidatedPasswordCount() => validatedPasswords.Count;

    public int GetTotalRequiredCount() => totalPasswordsNeeded;

    public int GetFoundCount() => discoveredClues.Count;

    // DÜZELTME: NotebookUI bu ismi arıyor, doğrusu bu:
    public List<string> GetDiscoveredClues() => discoveredClues;

    public bool HasFoundAllRequiredPasswords() =>
        validatedPasswords.Count == requiredPasswords.Count;
}
