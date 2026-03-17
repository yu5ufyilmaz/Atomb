using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;

    [Header("Şifre Üretim Ayarları")]
    [SerializeField]
    private WordPool wordPool;

    [Header("Sembol Ayarları")]
    public string[] symbols = { "+", "-", "/", "√", "%", "<=", "=", "<", ">", ".", ",", ">=" };

    [Header("Makineler")]
    [SerializeField]
    private InteractableOscilloscope oscilloscope;

    [SerializeField]
    private InteractableMassSpectrometer spectrometer;

    [Header("Kitaplar")]
    [SerializeField]
    private List<InteractableBook> allBooksInLevel;

    [Header("Tutorial Ayarları")]
    public string tutorialPassword = "AAAAAAAA_+_999";

    // YENİ: Tutorial notunu buraya sürükleyeceksin
    public InteractableBook tutorialNoteBook;

    [Header("Oyun Ayarları")]
    [Tooltip("Kazanmak için gereken RANDOM şifre sayısı (Tutorial hariç)")]
    [SerializeField]
    private int totalPasswordsNeeded = 5;

    // Takip Listeleri
    private List<string> requiredPasswords = new List<string>();
    private List<string> discoveredClues = new List<string>();
    private List<string> validatedPasswords = new List<string>();

    public event Action OnGameReadyToFinish;

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

        if (SymbolSpawner.Instance != null)
        {
            SymbolSpawner.Instance.SpawnRandomSymbol();
        }

        if (tutorialNoteBook != null)
        {
            tutorialNoteBook.canContainPassword = true;
            tutorialNoteBook.AssignPassword(tutorialPassword, 0);
        }

        // --- YENİ MANTIK: BULMACA KİTABINI BİR MAKİNE GİBİ BUL VE ŞİFREYİ ÇAK ---
        int activeSymbolID =
            SymbolSpawner.Instance != null ? SymbolSpawner.Instance.spawnedSymbolID : -1;

        // Tüm kitaplar içinde, bizim aktif sembolümüzü bekleyen o özel kitabı bul (Data'sı var mı yok mu bakmadan!)
        InteractableBook puzzleBook = allBooksInLevel.FirstOrDefault(b =>
            b != null && b.isSymbolTargetBook && b.requiredSymbolID == activeSymbolID
        );

        if (puzzleBook != null)
        {
            string puzzlePass = GenerateRandomPassword();
            puzzleBook.AssignPuzzlePassword(puzzlePass); // UV istemeyen yeni fonksiyonu kullandık!
            requiredPasswords.Add(puzzlePass);
            Debug.Log($"[Oyun Şifresi - SEMBOL MAKİNESİ] {puzzleBook.name}: {puzzlePass}");
        }
        // ----------------------------------------------------------------------

        int machineCount = 2; // Osiloskop + Spektrometre
        int bookCount = totalPasswordsNeeded - machineCount;

        // A) DİĞER MAKİNELER
        if (oscilloscope != null)
        {
            string pass1 = GenerateRandomPassword();
            oscilloscope.AssignPassword(pass1);
            requiredPasswords.Add(pass1);
        }

        if (spectrometer != null)
        {
            string pass2 = GenerateRandomPassword();
            spectrometer.AssignPassword(pass2);
            requiredPasswords.Add(pass2);
        }

        // B) GERİ KALAN NORMAL KİTAPLAR (Tıklamalı olanlar)
        // Burada bulmaca kitabını (puzzleBook) listeye katmıyoruz ki ona 2. kez şifre gitmesin
        var eligibleBooks = allBooksInLevel
            .Where(b =>
                b != null
                && b.canContainPassword
                && b.bookIdentity != null
                && b.bookIdentity.possibleLocations.Count > 0
                && b != tutorialNoteBook
                && b != puzzleBook
            )
            .ToList();

        // Eğer sembol kitabına şifre verdiysek, dağıtılacak rastgele kitap şifresi sayısını 1 düşür
        if (puzzleBook != null)
            bookCount--;

        var selectedBooks = eligibleBooks.OrderBy(x => Random.value).Take(bookCount).ToList();

        foreach (var book in selectedBooks)
        {
            string bookPass = GenerateRandomPassword();
            int randomLocIndex = Random.Range(0, book.bookIdentity.possibleLocations.Count);

            book.AssignPassword(bookPass, randomLocIndex);
            requiredPasswords.Add(bookPass);
            Debug.Log($"[Oyun Şifresi - RASTGELE KİTAP] {book.name}: {bookPass}");
        }
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
        // Sadece geçerli şifreleri (veya tutorial şifresini) deftere kaydet
        bool isGamePass = requiredPasswords.Contains(passwordID);
        bool isTutorialPass = (passwordID == tutorialPassword);

        if (!isGamePass && !isTutorialPass)
            return;

        if (discoveredClues.Contains(passwordID))
            return;

        discoveredClues.Add(passwordID);

        if (NotebookUI.Instance != null)
            NotebookUI.Instance.ShowPasswordNotification(passwordID);
    }

    private bool isTutorialPasswordUsed = false;

    public bool ValidatePassword(string passwordID)
    {
        if (passwordID == tutorialPassword)
        {
            Debug.Log("📘 TUTORIAL ŞİFRESİ GİRİLDİ (Sayaca eklenmiyor).");
            isTutorialPasswordUsed = true;

            // [SES ENTEGRASYONU] Tutorial şifresi çözüldü
            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnTutorialSolved();

            // --- ÇÖZÜM BURADA: TUTORIAL MODUNU KAPAT ---
            if (PlayerInteraction.Instance != null)
            {
                PlayerInteraction.Instance.DisableTutorialMode();
                Debug.Log("🔓 Tutorial Modu Kapatıldı. Tüm etkileşimler açık.");
            }
            // -------------------------------------------

            return true;
        }

        // 2. DURUM: YANLIŞ ŞİFRE (HATA)
        if (!requiredPasswords.Contains(passwordID))
        {
            // [SES ENTEGRASYONU] İlk hata yapıldığında çal
            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnFirstMistake();

            return false; // Yanlış
        }

        // 3. DURUM: ZATEN GİRİLMİŞ OYUN ŞİFRESİ
        if (validatedPasswords.Contains(passwordID))
        {
            return true; // Zaten girilmiş
        }

        // 4. DURUM: YENİ DOĞRU OYUN ŞİFRESİ
        validatedPasswords.Add(passwordID);
        Debug.Log($"✅ OYUN ŞİFRESİ ONAYLANDI: {validatedPasswords.Count}/{totalPasswordsNeeded}");

        // KAZANMA KONTROLÜ (SON ŞİFRE)
        if (validatedPasswords.Count >= totalPasswordsNeeded)
        {
            // [SES ENTEGRASYONU] Final şifresi girildi, oyun bitiyor
            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnFinalCodeEntered();

            OnGameReadyToFinish?.Invoke();
        }

        return true;
    }

    public bool IsPasswordUsed(string passwordID)
    {
        if (passwordID == tutorialPassword)
            return isTutorialPasswordUsed;

        return validatedPasswords.Contains(passwordID);
    }

    // Getter Metotları
    public int GetValidatedPasswordCount() => validatedPasswords.Count;

    public int GetTotalRequiredCount() => totalPasswordsNeeded;

    public int GetFoundCount() => discoveredClues.Count;

    public List<string> GetDiscoveredClues() => discoveredClues;

    public bool HasFoundAllRequiredPasswords() =>
        validatedPasswords.Count == requiredPasswords.Count;
}
