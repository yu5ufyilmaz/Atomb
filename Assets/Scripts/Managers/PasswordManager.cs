using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

// ISaveable eklendi!
public class PasswordManager : MonoBehaviour, ISaveable
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
    public InteractableBook tutorialNoteBook;

    [Header("Oyun Ayarları")]
    [Tooltip("Kazanmak için gereken RANDOM şifre sayısı (Tutorial hariç)")]
    [SerializeField]
    private int totalPasswordsNeeded = 5;

    // Takip Listeleri
    private List<string> requiredPasswords = new List<string>();
    private List<string> discoveredClues = new List<string>();
    private List<string> validatedPasswords = new List<string>();

    // YENİ: Bu oturumda kime hangi şifreyi atadığımızın günlüğü (Kaydetmek çok kolaylaşacak)
    private List<GameData.ObjectPasswordPair> currentSessionPasswords =
        new List<GameData.ObjectPasswordPair>();

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
        currentSessionPasswords.Clear(); // Günlüğü temizle

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

        // --- BULMACA KİTABI ---
        int activeSymbolID =
            SymbolSpawner.Instance != null ? SymbolSpawner.Instance.spawnedSymbolID : -1;
        InteractableBook puzzleBook = allBooksInLevel.FirstOrDefault(b =>
            b != null && b.isSymbolTargetBook && b.requiredSymbolID == activeSymbolID
        );

        if (puzzleBook != null)
        {
            string puzzlePass = GenerateRandomPassword();
            puzzleBook.AssignPuzzlePassword(puzzlePass);
            requiredPasswords.Add(puzzlePass);

            // HAFIZAYA AL
            currentSessionPasswords.Add(
                new GameData.ObjectPasswordPair
                {
                    objectName = puzzleBook.gameObject.name,
                    password = puzzlePass,
                    locationIndex = 0,
                    isPuzzleBook = true,
                }
            );
            Debug.Log($"[Oyun Şifresi - SEMBOL MAKİNESİ] {puzzleBook.name}: {puzzlePass}");
        }

        int machineCount = 2; // Osiloskop + Spektrometre
        int bookCount = totalPasswordsNeeded - machineCount;

        // --- DİĞER MAKİNELER ---
        if (oscilloscope != null)
        {
            string pass1 = GenerateRandomPassword();
            oscilloscope.AssignPassword(pass1);
            requiredPasswords.Add(pass1);

            currentSessionPasswords.Add(
                new GameData.ObjectPasswordPair
                {
                    objectName = oscilloscope.gameObject.name,
                    password = pass1,
                }
            );
        }

        if (spectrometer != null)
        {
            string pass2 = GenerateRandomPassword();
            spectrometer.AssignPassword(pass2);
            requiredPasswords.Add(pass2);

            currentSessionPasswords.Add(
                new GameData.ObjectPasswordPair
                {
                    objectName = spectrometer.gameObject.name,
                    password = pass2,
                }
            );
        }

        // --- NORMAL KİTAPLAR ---
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

        if (puzzleBook != null)
            bookCount--;

        var selectedBooks = eligibleBooks.OrderBy(x => Random.value).Take(bookCount).ToList();

        foreach (var book in selectedBooks)
        {
            string bookPass = GenerateRandomPassword();
            int randomLocIndex = Random.Range(0, book.bookIdentity.possibleLocations.Count);

            book.AssignPassword(bookPass, randomLocIndex);
            requiredPasswords.Add(bookPass);

            // HAFIZAYA AL
            currentSessionPasswords.Add(
                new GameData.ObjectPasswordPair
                {
                    objectName = book.gameObject.name,
                    password = bookPass,
                    locationIndex = randomLocIndex,
                    isPuzzleBook = false,
                }
            );
            Debug.Log($"[Oyun Şifresi - RASTGELE KİTAP] {book.name}: {bookPass}");
        }
    }

    // ==========================================
    // ISAVEABLE ARAYÜZÜ ENTEGRASYONU (YENİ)
    // ==========================================

    public void LoadData(GameData data)
    {
        // Eğer kaydedilmiş şifre yoksa (Yeni Oyun ise) Start()'ın ürettiği şifrelerle devam et
        if (data.savedPasswords == null || data.savedPasswords.Count == 0)
            return;

        Debug.Log("PasswordManager: Kayıtlı veriler yükleniyor. Rastgele şifreler eziliyor...");

        // 1. Listeleri Kayıttan Çek
        this.requiredPasswords = new List<string>(data.requiredPasswords);
        this.discoveredClues = new List<string>(data.discoveredClues);
        this.validatedPasswords = new List<string>(data.validatePasswords);
        this.currentSessionPasswords = new List<GameData.ObjectPasswordPair>(data.savedPasswords);

        // 2. Sahnedeki Objeleri Bul ve Doğru Şifreleri Geri Ata
        foreach (var pair in currentSessionPasswords)
        {
            if (oscilloscope != null && oscilloscope.gameObject.name == pair.objectName)
            {
                oscilloscope.AssignPassword(pair.password);
                continue;
            }

            if (spectrometer != null && spectrometer.gameObject.name == pair.objectName)
            {
                spectrometer.AssignPassword(pair.password);
                continue;
            }

            // Kitapları isminden bul
            InteractableBook foundBook = allBooksInLevel.FirstOrDefault(b =>
                b != null && b.gameObject.name == pair.objectName
            );
            if (foundBook != null)
            {
                if (pair.isPuzzleBook)
                    foundBook.AssignPuzzlePassword(pair.password);
                else
                    foundBook.AssignPassword(pair.password, pair.locationIndex);
            }
        }

        // 3. Defter (Notebook) Arayüzüne Bulunan İpuçlarını Geri Ekle
        if (NotebookUI.Instance != null)
        {
            foreach (string clue in discoveredClues)
            {
                NotebookUI.Instance.ShowPasswordNotification(clue);
            }
        }

        // 4. Kazanma Durumunu Kontrol Et (Eğer oyunu son şifreyi girip kaydettiyse)
        if (validatedPasswords.Count >= totalPasswordsNeeded)
        {
            OnGameReadyToFinish?.Invoke();
        }
    }

    public void SaveData(ref GameData data)
    {
        // Elimizdeki tüm listeleri oyunun kayıt dosyasına geçir
        data.requiredPasswords = new List<string>(this.requiredPasswords);
        data.discoveredClues = new List<string>(this.discoveredClues);
        data.validatePasswords = new List<string>(this.validatedPasswords);
        data.savedPasswords = new List<GameData.ObjectPasswordPair>(this.currentSessionPasswords);
    }

    // ==========================================

    private string GenerateRandomPassword()
    {
        string w = (wordPool != null) ? wordPool.GetRandomWord() : "NULL";
        string s = symbols[Random.Range(0, symbols.Length)];
        string n = Random.Range(0, 1000).ToString("D3");
        return $"{w}_{s}_{n}";
    }

    public void DiscoverClue(string passwordID)
    {
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

            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnTutorialSolved();

            if (PlayerInteraction.Instance != null)
            {
                PlayerInteraction.Instance.DisableTutorialMode();
                Debug.Log("🔓 Tutorial Modu Kapatıldı. Tüm etkileşimler açık.");
            }
            return true;
        }

        if (!requiredPasswords.Contains(passwordID))
        {
            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnFirstMistake();
            return false;
        }

        if (validatedPasswords.Contains(passwordID))
            return true;

        validatedPasswords.Add(passwordID);
        Debug.Log($"✅ OYUN ŞİFRESİ ONAYLANDI: {validatedPasswords.Count}/{totalPasswordsNeeded}");

        if (validatedPasswords.Count >= totalPasswordsNeeded)
        {
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
