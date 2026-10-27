using UnityEngine;

public class NoteAction : ActionBase
{
    public PasswordData targetPassword;

    protected override void PerformAction()
    {
        if (PasswordManager.Instance != null && targetPassword != null)
        {
            PasswordManager.Instance.DiscoverClue(targetPassword.tutorialPasswordID);
            Debug.Log($"[NoteAction] İpucu deftere eklendi: {targetPassword.tutorialPasswordID}");
        }
        else Debug.LogWarning("[NoteAction] PasswordManager veya targetPassword eksik.");
    }
}
