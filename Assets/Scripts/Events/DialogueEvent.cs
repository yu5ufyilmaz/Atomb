using UnityEngine;

[System.Serializable]
public class DialogueEvent
{
    public AudioClip clip;

    [SubtitleIDSelection] // Dropdown büyüsü
    public string subtitleID;

    public void Play()
    {
        if (MegaphoneSystem.Instance != null)
            MegaphoneSystem.Instance.PlayEvent(this);
    }
}

public class SubtitleIDSelectionAttribute : PropertyAttribute { }
