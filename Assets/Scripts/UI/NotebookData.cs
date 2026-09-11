using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct NotebookTutorialEntry
{
    public string title;

    [TextArea(5, 10)]
    public string content;
}

[CreateAssetMenu(fileName = "NewNotebookData", menuName = "Atomb/Notebook Data")]
public class NotebookData : ScriptableObject
{
    [Header("📖 General Mechanics (Static)")]
    public List<NotebookTutorialEntry> tutorialPages;

    [Header("🧩 Symbol Research (Dynamic)")]
    // Burası, PasswordManager'daki sembol sırasıyla (index) eşleşecek açıklamalar
    [TextArea(3, 10)]
    public string[] symbolDescriptions;
}
