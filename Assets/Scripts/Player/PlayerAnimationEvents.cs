using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    // Açılma animasyonunda defterin görünmesini istediğin kareye bu eventi koy
    public void NoteBookVisual()
    {
        if (NotebookUI.Instance != null)
        {
            NotebookUI.Instance.SetNotebookVisibility(true);
        }
    }

    // Kapanma animasyonunda defterin ekrandan çıktığı kareye bu eventi koy
    public void HideNotebookEvent()
    {
        if (NotebookUI.Instance != null)
        {
            NotebookUI.Instance.SetNotebookVisibility(false);
        }
    }
}
