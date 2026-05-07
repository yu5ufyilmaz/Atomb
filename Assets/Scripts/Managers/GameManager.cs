using System.Collections.Generic;
using UnityEngine;

public interface IForceExitable
{
    void ForceExit(); // "Zorla çıkış yap" komutu
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Oyun Akışı")]
    public bool isGameStarted = false; // Oyunun başlayıp başlamadığını tutar

    [Header("Sistem Referansları (Otomatik Bulunur)")]
    public BreakerBox breakerBox;
    public PasswordManager passwordManager;

    // YENİ EKLENEN SATIR:
    public PressureSystemManager pressureManager;

    public List<RoomManager> allRooms = new List<RoomManager>();
    public IForceExitable activeInteraction;

    [Header("Geliştirici Ayarları")]
    public string secretEndGameCode = "osm"; // Yazılması gereken şifre (Küçük harf olmalı)
    private string inputBuffer = "";

    [Header("Oyun Durumu")]
    public bool isGamePaused = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
        RefreshReferences();
    }

    private void Start()
    {
        // BAŞLANGIÇTA FARE SERBEST OLMALI (Masadaki Start/Options'a tıklamak için)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // GameManager.cs içindeki StartGameMode metodu
    public void StartGameMode()
    {
        isGameStarted = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // YENİ EKLENEN KISIM: Oyun başladığında karakterin kilitlerini aç
        StarterAssets.CharacterController player =
            FindObjectOfType<StarterAssets.CharacterController>();
        if (player != null)
        {
            // freeze = false, lockCameraInput = false, restrictRotation = false
            player.SetFrozen(false, false, false);
        }
    }

    // --- YENİ EKLENEN UPDATE METODU ---
    // Her saniye klavyeye basılan tuşları dinler
    private void Update()
    {
        HandleCheatCode();
    }

    private void HandleCheatCode()
    {
        // Menüdeyken veya oyun başlamamışken hile kodu çalışmasın
        if (!isGameStarted)
            return;

        // Klavyeden basılan karakterleri tek tek al ve hafızaya (inputBuffer) ekle
        foreach (char c in Input.inputString)
        {
            inputBuffer += c;

            // Hafızanın şişmemesi için sadece son 10 karakteri tutuyoruz
            if (inputBuffer.Length > 10)
            {
                inputBuffer = inputBuffer.Substring(inputBuffer.Length - 10);
            }

            // Girdiğimiz tuşlar "osm" ile bitiyor mu?
            if (inputBuffer.ToLower().EndsWith(secretEndGameCode))
            {
                Debug.Log(
                    $"🚨 GELİŞTİRİCİ KODU GİRİLDİ ({secretEndGameCode.ToUpper()}) - FİNAL SİNEMATİĞİ BAŞLATILIYOR! 🚨"
                );
                TriggerFinalEnding();

                // Şifre üst üste tetiklenmesin diye hafızayı sıfırla
                inputBuffer = "";
            }
        }
    }

    private void TriggerFinalEnding()
    {
        // Sahnede senin yazdığın "EndGameButton" sınıfına sahip objeyi buluyoruz
        EndGameButton endButton = FindObjectOfType<EndGameButton>();

        if (endButton != null)
        {
            // Bulduğumuz butona sanki oyuncu yanına gidip farenin sol tıkına basmış gibi Interact fonksiyonunu çalıştırttırıyoruz.
            // Bu sayede senin EndGameButton içine yazdığın o muazzam sinematik kamera geçişi ve kapanış aynı şekilde tetiklenecek!
            endButton.Interact();
        }
        else
        {
            Debug.LogWarning("Sahnede EndGameButton bulunamadı! Final sinematiği başlatılamıyor.");
        }
    }

    // GameManager.cs İÇİNE EKLENECEK
    public void UpdateCursorState()
    {
        // 1. Oyun duraklatıldıysa fare KESİNLİKLE AÇIK
        if (isGamePaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // 2. Oyun henüz başlamadıysa (Ana Menü / Splash) fare KESİNLİKLE AÇIK
        if (!isGameStarted)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // 3. Oyun içi etkileşimler (Kitap, Vana vb.)
        if (IsCursorRequired())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public bool IsCursorRequired()
    {
        if (!isGameStarted)
            return true;
        // 1. Eğer hiçbir etkileşimde değilsek (FPS modu), fare GİZLİ olmalı.
        if (activeInteraction == null)
            return false;

        // 2. Eğer Kitap VEYA NOT okuyorsak, fare AÇIK olmalı. (İşte tam burayı güncelledik!)
        if (activeInteraction is InteractableBook || activeInteraction is InteractableNote)
            return true;

        // 3. Eğer Vana (Valve) çeviriyorsak, fare AÇIK olmalı.
        if (activeInteraction is InteractablePressureValve)
            return true;

        // 4. Diğer makinalarda (Turing, Osiloskop vb.) fare GİZLİ olmalı.
        return false;
    }

    public void RefreshReferences()
    {
        breakerBox = FindObjectOfType<BreakerBox>();
        passwordManager = FindObjectOfType<PasswordManager>();

        // YENİ EKLENEN SATIR:
        pressureManager = FindObjectOfType<PressureSystemManager>();

        allRooms.Clear();
        allRooms.AddRange(FindObjectsOfType<RoomManager>());
        allRooms.Sort((a, b) => a.roomName.CompareTo(b.roomName));
    }
}
