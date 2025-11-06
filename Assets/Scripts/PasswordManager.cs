using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Listenin içeriğini karıştırmak (shuffle) için eklendi.

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;
    
    [Header("Tüm Olası Şifre Verileri")]
    [SerializeField] private List<PasswordData> allPossiblePasswordData; 
    
    [Header("Sahnedeki Potansiyel Şifreli Kitaplar")]
    [SerializeField] private List<InteractableBook> allPasswordBooksInLevel;

    [Header("Mevcut Oyun Durumu")]
    [SerializeField] private int requiredPasswordCount = 3; // Document 2.pdf'te 4 adet[cite: 14], siz 3 dediniz. Buradan ayarlayabilirsiniz.
    private List<string> requiredPasswords = new List<string>(); // Oyuncunun bu oyunda bulması gereken şifre ID'leri
    private List<string> foundPasswords = new List<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        InitializeNewGame();
    }

    // Oyuncu "yandığında" veya yeni oyun başladığında bu fonksiyon çağrılmalı.
    public void InitializeNewGame()
    {
        foundPasswords.Clear();
        requiredPasswords.Clear();

        // 1. Tüm kitapları önce "şifresiz" olarak ayarla
        foreach (var book in allPasswordBooksInLevel)
        {
            book.ClearPassword();
        }

        // 2. Olası şifreleri ve kitapları rastgele karıştır (Fisher-Yates Shuffle)
        // Linq kullanarak basit bir karıştırma:
        var shuffledPasswordData = allPossiblePasswordData.OrderBy(x => Random.value).ToList();
        var shuffledBooks = allPasswordBooksInLevel.OrderBy(x => Random.value).ToList();

        // 3. Gerekli sayıda kitabı ve şifreyi eşleştir
        for (int i = 0; i < requiredPasswordCount; i++)
        {
            // Eğer yeterli kitap veya şifre yoksa döngüden çık
            if (i >= shuffledPasswordData.Count || i >= shuffledBooks.Count)
            {
                Debug.LogWarning("Yeterli sayıda kitap veya şifre verisi yok!");
                break;
            }

            PasswordData dataToAssign = shuffledPasswordData[i];
            InteractableBook bookToAssign = shuffledBooks[i];

            // Kitaba yeni kimliğini, texture'ını ve hotspot'unu ata
            bookToAssign.AssignPassword(dataToAssign);

            // Bu şifreyi "bulunması gerekenler" listesine ekle
            requiredPasswords.Add(dataToAssign.passwordID);
        }

        Debug.Log($"Yeni oyun başlatıldı. {requiredPasswords.Count} adet şifre atandı.");
    }

    public void AddFoundPassword(string passwordID)
    {
        if (requiredPasswords.Contains(passwordID) && !foundPasswords.Contains(passwordID))
        {
            foundPasswords.Add(passwordID);
            Debug.Log($"Şifre bulundu: {passwordID}");
        }
    }

    public List<string> GetFoundPasswords()
    {
        return foundPasswords;
    }
    
    // (Opsiyonel) Oyuncunun tüm şifreleri bulup bulmadığını kontrol et
    public bool HasFoundAllRequiredPasswords()
    {
        return foundPasswords.Count == requiredPasswords.Count;
    }
}