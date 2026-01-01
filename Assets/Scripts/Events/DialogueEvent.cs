using UnityEngine;

[System.Serializable]
public class DialogueEvent
{
    public AudioClip clip;

    [SubtitleIDSelection]
    public string subtitleID;

    // Parametreyi güncelledik: İsteğe bağlı bir Transform alıyor.
    public void Play(Transform soundOrigin = null)
    {
        if (MegaphoneSystem.Instance != null)
            MegaphoneSystem.Instance.PlayEvent(this, soundOrigin);
    }
}

public class SubtitleIDSelectionAttribute : PropertyAttribute { }
