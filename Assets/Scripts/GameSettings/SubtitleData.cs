using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    Turkish,
    English,
    German,
}

[System.Serializable]
public class SubtitleSegment
{
    [Tooltip("Ses başladıktan kaç saniye sonra bu yazı görünsün?")]
    public float startTime = 0f;

    [Tooltip("Bu yazı ekranda kaç saniye kalsın?")]
    public float duration = 3f;

    [TextArea(2, 5)]
    public string textTR;

    [TextArea(2, 5)]
    public string textEN;

    [TextArea(2, 5)]
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

[System.Serializable]
public class SubtitleEntry
{
    public string id; // Çağırmak için kullanacağın ID (örn: Intro_Speech)
    public string note; // Kendine not (örn: Asansördeki konuşma)

    // Tek bir ses klibi içindeki cümle parçacıkları
    public List<SubtitleSegment> segments = new List<SubtitleSegment>();
}

[CreateAssetMenu(fileName = "NewSubtitleDatabase", menuName = "Senzora/Subtitle Database")]
public class SubtitleData : ScriptableObject
{
    public List<SubtitleEntry> entries = new List<SubtitleEntry>();

    public SubtitleEntry GetEntry(string id) => entries.Find(x => x.id == id);
}
