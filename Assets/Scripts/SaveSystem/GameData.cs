using UnityEngine;

using System.Collections.Generic;

[System.Serializable]
public class GameData
{
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    
    public string lastSavedTime;

    public List<string> triggeredEvents = new List<string>(); 
    public List<string> solvedPuzzles = new List<string>();

    public GameData()
    {
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        lastSavedTime = "";
    }
}