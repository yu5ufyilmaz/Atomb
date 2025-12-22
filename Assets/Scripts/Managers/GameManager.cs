using System.Collections.Generic;
using UnityEngine;

public interface IForceExitable
{
    void ForceExit(); // "Zorla çıkış yap" komutu
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Sistem Referansları (Otomatik Bulunur)")]
    public BreakerBox breakerBox;
    public PasswordManager passwordManager;

    // YENİ EKLENEN SATIR:
    public PressureSystemManager pressureManager;

    public List<RoomManager> allRooms = new List<RoomManager>();
    public IForceExitable activeInteraction;

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

    public bool IsCursorRequired()
    {
        // 1. Eğer hiçbir etkileşimde değilsek (FPS modu), fare GİZLİ olmalı.
        if (activeInteraction == null)
            return false;

        // 2. Eğer Kitap okuyorsak, fare AÇIK olmalı.
        if (activeInteraction is InteractableBook)
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
