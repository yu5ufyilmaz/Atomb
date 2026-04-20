using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public Vector3 playerPosition;
    public Quaternion playerRotation;

    public string lastSavedTime;

    public List<string> triggeredEvents = new List<string>();
    public List<string> solvedPuzzles = new List<string>();
    public List<string> discoveredClues = new List<string>();
    public List<string> requiredPasswords = new List<string>();
    public List<string> validatePasswords = new List<string>();

    public List<ObjectPasswordPair> savedPasswords = new List<ObjectPasswordPair>();

    public bool hasSymbol;
    public int currentSymbolID;
    public bool wasAttackInProgress;
    public float remainingGlobalCooldown;
    public float savedPressure;

    // ==========================================
    // YENİ EKLENENLER: ELEKTRİK VE IŞIK SİSTEMİ
    // ==========================================
    public bool isBreakerTripped;
    public int breakerCycleCount;
    public List<LightSaveData> savedLights = new List<LightSaveData>();

    public GameData()
    {
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        lastSavedTime = "";
        hasSymbol = false;
        currentSymbolID = -1;
        wasAttackInProgress = false;
        remainingGlobalCooldown = 0f;
        isBreakerTripped = false;
        breakerCycleCount = 0;
        savedPressure = 0f;
    }

    [System.Serializable]
    public struct ObjectPasswordPair
    {
        public string objectName;
        public string password;
        public int locationIndex;
        public bool isPuzzleBook;
    }

    [System.Serializable]
    public struct LightSaveData
    {
        public string lightID; // ARTIK İSİM DEĞİL, ID TUTUYORUZ
        public bool isOn;
    }
}
