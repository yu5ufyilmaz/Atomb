using UnityEngine;

public class NoteAction : MonoBehaviour, IAction
{
    public enum NoteOperation
    {
        DiscoverClue,
        UnlockSymbolResearch,
    }

    [Tooltip("Deftere eklenecek verinin türü.")]
    public NoteOperation operation = NoteOperation.DiscoverClue;

    [Tooltip("Eğer DiscoverClue seçildiyse eklenecek şifre/ipucu ID'si (Örn: START_=_001)")]
    public string passwordID;

    [Tooltip("Eğer UnlockSymbolResearch seçildiyse defterde açılacak sembolün ID'si (0, 1, 2...)")]
    public int symbolID;

    public void Execute()
    {
        switch (operation)
        {
            case NoteOperation.DiscoverClue:
                if (PasswordManager.Instance != null && !string.IsNullOrEmpty(passwordID))
                {
                    // Şifreyi bulduğumuzu PasswordManager'a iletiyoruz
                    PasswordManager.Instance.DiscoverClue(passwordID);
                    Debug.Log($"[NoteAction] İpucu/Şifre deftere eklendi: {passwordID}");
                }
                else
                {
                    Debug.LogWarning(
                        "[NoteAction] PasswordManager bulunamadı veya passwordID boş!"
                    );
                }
                break;

            case NoteOperation.UnlockSymbolResearch:
                if (NotebookUI.Instance != null)
                {
                    // Sembol açıklamasını NotebookUI üzerinden açıyoruz
                    NotebookUI.Instance.UnlockSymbolResearch(symbolID);
                    Debug.Log(
                        $"[NoteAction] Sembol araştırması defterde erişime açıldı. Sembol ID: {symbolID}"
                    );
                }
                else
                {
                    Debug.LogWarning("[NoteAction] NotebookUI bulunamadı!");
                }
                break;
        }
    }
}
