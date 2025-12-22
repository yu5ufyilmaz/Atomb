using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    Turkish,
    English,
    German,
}

[System.Serializable]
public class SubtitleEntry
{
    public string id;
    public string note;
    public float duration = 3f;
    public string textTR;
    public string textEN;
    public string textDE;

    public string GetText(GameLanguage lang)
    {
        switch (lang)
        {
            case GameLanguage.Turkish:
                return textTR;
            case GameLanguage.English:
                return textEN;
            case GameLanguage.German:
                return textDE;
            default:
                return textEN;
        }
    }
}

[CreateAssetMenu(fileName = "NewSubtitleDatabase", menuName = "Senzora/Subtitle Database")]
public class SubtitleData : ScriptableObject
{
    public List<SubtitleEntry> entries = new List<SubtitleEntry>();

    public SubtitleEntry GetEntry(string id) => entries.Find(x => x.id == id);
}
