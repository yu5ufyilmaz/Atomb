using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;
    
    [Header("Tüm Olası Şifre Verileri")]
    [SerializeField] private List<PasswordData> allPossiblePasswordData; 
    
    [Header("Sahnedeki Potansiyel Şifreli Kitaplar")]
    [SerializeField] private List<InteractableBook> allPasswordBooksInLevel;

    [Header("Mevcut Oyun Durumu")]
    [Tooltip("Turing makinesindeki gösterge (ışık) sayısı ile aynı olmalı.")]
    [SerializeField] private int requiredPasswordCount = 5; // 5 göstergeniz var
    
    // Oyuncunun bu oyunda bulması gereken şifre ID'leri
    private List<string> requiredPasswords = new List<string>(); 
    
    // Oyuncunun kitaplardan "keşfettiği" (not defterine giden) şifreler
    private List<string> discoveredClues = new List<string>();
    
    // Oyuncunun makineye "doğru girdiği" (ışıkları yakan) şifreler
    private List<string> validatedPasswords = new List<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

        var shuffledPasswordData = allPossiblePasswordData.OrderBy(x => Random.value).ToList();
        var shuffledBooks = allPasswordBooksInLevel.OrderBy(x => Random.value).ToList();

        for (int i = 0; i < requiredPasswordCount; i++)
        {
            if (i >= shuffledPasswordData.Count || i >= shuffledBooks.Count)
            {
                Debug.LogWarning("Yeterli sayıda kitap veya şifre verisi yok!", this);
                break;
            }

            PasswordData dataToAssign = shuffledPasswordData[i];
            InteractableBook bookToAssign = shuffledBooks[i];
            
            bookToAssign.AssignPassword(dataToAssign);
            requiredPasswords.Add(dataToAssign.passwordID);
        }

        Debug.Log($"Yeni oyun başlatıldı. {requiredPasswords.Count} adet şifre atandı.");
    }

    // 1. Kitap bu fonksiyonu çağırır (Not defterine ekler)
    public void DiscoverClue(string passwordID)
    {
        // Sadece "gerekli" ve "daha önce keşfedilmemiş" ise ekle
        if (requiredPasswords.Contains(passwordID) && !discoveredClues.Contains(passwordID))
        {
            discoveredClues.Add(passwordID);
            Debug.Log($"İpucu keşfedildi: {passwordID}");
            
            // Not defterine bildirim gönder
            if (NotebookUI.Instance != null)
            {
                NotebookUI.Instance.ShowPasswordNotification(passwordID);
            }
        }
    }

    // 2. Turing Makinesi bu fonksiyonu çağırır (Işıkları yakar)
    public bool ValidatePassword(string passwordID)
    {
        // Sadece "gerekli" ve "daha önce doğrulanmamış" ise ekle
        if (requiredPasswords.Contains(passwordID) && !validatedPasswords.Contains(passwordID))
        {
            validatedPasswords.Add(passwordID);
            Debug.Log($"Şifre DOĞRULANDI: {passwordID}");
            return true; // Başarılı (yeni doğrulandı)
        }
        
        // Ya yanlış ya da zaten doğrulanmış
        return false; // Başarısız
    }

    // Not defteri bu listeyi kullanır
    public List<string> GetDiscoveredClues()
    {
        return discoveredClues;
    }
    
    // Turing makinesi bu sayıyı kullanır
    public int GetValidatedPasswordCount()
    {
        return validatedPasswords.Count;
    }
    
    public bool HasFoundAllRequiredPasswords()
    {
        return validatedPasswords.Count == requiredPasswords.Count;
    }
}