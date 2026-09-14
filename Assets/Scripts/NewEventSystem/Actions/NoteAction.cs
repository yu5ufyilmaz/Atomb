using NaughtyAttributes;
using UnityEngine;

public class NoteAction : MonoBehaviour, IAction
{
    public enum NoteOperation
    {
        DiscoverClue,
        UnlockSymbolResearch,
    }

    public NoteOperation operation = NoteOperation.DiscoverClue;

    [ShowIf("operation", NoteOperation.DiscoverClue)]
    public PasswordData targetPassword;

    [ShowIf("operation", NoteOperation.UnlockSymbolResearch)]
    public SymbolDataSO targetSymbol;

    public void Execute()
    {
        switch (operation)
        {
            case NoteOperation.DiscoverClue:
                if (PasswordManager.Instance != null && targetPassword != null)
                {
                    // SO'nun içindeki ID'yi okuyup manager'a yolluyoruz
                    PasswordManager.Instance.DiscoverClue(targetPassword.tutorialPasswordID);
                    Debug.Log(
                        $"[NoteAction] İpucu deftere eklendi: {targetPassword.tutorialPasswordID}"
                    );
                }
                else
                {
                    Debug.LogWarning("[NoteAction] PasswordManager veya targetPassword eksik.");
                }
                break;

            case NoteOperation.UnlockSymbolResearch:
                if (NotebookUI.Instance != null && targetSymbol != null)
                {
                    // SO'nun içindeki ID'yi okuyup manager'a yolluyoruz
                    NotebookUI.Instance.UnlockSymbolResearch(targetSymbol.symbolID);
                    Debug.Log($"[NoteAction] Sembol açıldı. Sembol ID: {targetSymbol.symbolID}");
                }
                else
                {
                    Debug.LogWarning("[NoteAction] NotebookUI veya targetSymbol eksik.");
                }
                break;
        }
    }
}
