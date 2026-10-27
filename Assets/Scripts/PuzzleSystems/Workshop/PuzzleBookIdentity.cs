using UnityEngine;

[DisallowMultipleComponent]
public sealed class PuzzleBookIdentity : MonoBehaviour
{
    [Tooltip("Kalıcı sahne kimliği; atölye kurulumu atar. Aynı sahnede benzersiz olmalı.")]
    public string bookID;
    [Tooltip("Oyuncu bu kitaba puzzleı çözmeden ulaşabiliyor mu?")]
    public bool accessible = true;
    public string title;
    public string location;
    public PuzzleBookTopic topics = PuzzleBookTopic.All;
    [Min(2)] public int pageCount = 8;

    public PuzzleBookProfile Profile(int index)
    {
        var fallback = PuzzleBookProfile.Default(index, bookID);
        return new PuzzleBookProfile { bookID = bookID,
            title = string.IsNullOrWhiteSpace(title) ? fallback.title : title,
            location = string.IsNullOrWhiteSpace(location) ? fallback.location : location,
            topics = topics, pageCount = pageCount };
    }
}
