using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Sistem Referansları (Otomatik Bulunur)")]
    public BreakerBox breakerBox;
    public PasswordManager passwordManager;

    // YENİ EKLENEN SATIR:
    public PressureSystemManager pressureManager;

    public List<RoomManager> allRooms = new List<RoomManager>();

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
