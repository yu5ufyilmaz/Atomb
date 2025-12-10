using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;

    [Header("Şifre Üretim")]
    [SerializeField]
    private WordPool wordPool;
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

    [Header("Sahne Kitapları")]
    [Tooltip("Sahnedeki tüm InteractableBook'ları buraya sürükle.")]
    [SerializeField]
    private List<InteractableBook> allBooksInLevel;

    [SerializeField]
    private int requiredPasswordCount = 5;

    // Listeler
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

    private void Start() => InitializeNewGame();

    public void InitializeNewGame()
    {
        // Temizlik
        discoveredClues.Clear();
        validatedPasswords.Clear();
        requiredPasswords.Clear();

        // 1. Şifre almaya uygun ve Datası olan kitapları bul
        var eligibleBooks = allBooksInLevel
            .Where(b =>
                b.canContainPassword
                && b.bookIdentity != null
                && b.bookIdentity.possibleLocations.Count > 0
            )
            .ToList();

        if (eligibleBooks.Count < requiredPasswordCount)
        {
            Debug.LogError(
                $"Yeterli sayıda uygun kitap yok! Bulunan: {eligibleBooks.Count}, İstenen: {requiredPasswordCount}"
            );
            return;
        }

        // 2. Rastgele 5 kitap seç
        var selectedBooks = eligibleBooks
            .OrderBy(x => Random.value)
            .Take(requiredPasswordCount)
            .ToList();

        foreach (var book in selectedBooks)
        {
            // Şifre Stringi Oluştur
            string w = (wordPool != null) ? wordPool.GetRandomWord() : "NULL";
            string s = symbols[Random.Range(0, symbols.Length)];
            string n = Random.Range(0, 1000).ToString("D3");

            string finalPass = $"{w}_{s}_{n}";

            // 3. Kitabın kendi datasından rastgele bir konum indeksi seç
            int randomLocIndex = Random.Range(0, book.bookIdentity.possibleLocations.Count);

            // 4. Kitaba emret
            book.AssignPassword(finalPass, randomLocIndex);

            // --- KRİTİK DÜZELTME BURASI ---
            // Bu şifreyi 'Gerekli Şifreler' listesine ekle ki Manager bunu tanısın.
            requiredPasswords.Add(finalPass);
            // ------------------------------
        }

        Debug.Log($"Oyun Başladı! {requiredPasswords.Count} adet şifre dağıtıldı.");
    }

    public void DiscoverClue(string passwordID)
    {
        // Debug: Gelen şifre ne?
        Debug.Log($"Manager: Şifre bildirimi alındı -> {passwordID}");

        // 1. Bu şifre bu oyunda gerekli mi?
        if (!requiredPasswords.Contains(passwordID))
        {
            Debug.LogWarning(
                $"REDDEDİLDİ: '{passwordID}' bu oyunun şifre listesinde yok! (Required Listesi: {string.Join(", ", requiredPasswords)})"
            );
            return;
        }

        // 2. Zaten bulundu mu?
        if (discoveredClues.Contains(passwordID))
        {
            Debug.Log($"BİLGİ: '{passwordID}' zaten daha önce bulunmuştu.");
            return;
        }

        // 3. KABUL ET
        discoveredClues.Add(passwordID);
        Debug.Log(
            $"ONAYLANDI: '{passwordID}' bulundu ve listeye eklendi. Toplam Bulunan: {discoveredClues.Count}"
        );

        // UI Güncelle (Varsa)
        if (NotebookUI.Instance != null)
        {
            NotebookUI.Instance.ShowPasswordNotification(passwordID);
            // NotebookUI içindeki listeyi anlık yenilemek istersen burada bir fonksiyon çağırabilirsin
            // Ancak NotebookUI genelde açıldığında (Toggle) listeyi yeniler.
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

    // Getters
    public List<string> GetDiscoveredClues() => discoveredClues;

    public int GetValidatedPasswordCount() => validatedPasswords.Count;

    public bool HasFoundAllRequiredPasswords() =>
        validatedPasswords.Count == requiredPasswords.Count;

    public int GetTotalRequiredCount() => requiredPasswordCount;

    public int GetFoundCount() => discoveredClues.Count;

    public int GetValidatedCount() => validatedPasswords.Count;

    public List<string> GetFoundPasswordsList() => discoveredClues;
}
