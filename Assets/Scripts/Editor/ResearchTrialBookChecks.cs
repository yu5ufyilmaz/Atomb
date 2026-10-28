using TMPro;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ResearchTrialBookChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Senzora/Doğrulama/Araştırma Kitabı Açık Sayfayı Topla")]
    public static void CollectVisible()
    {
        Check(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Araştırma sahnesinde Play gerekli.");
        var book = GameManager.Instance.activeInteraction as InteractableBook;
        Check(book != null && book.isOpen && !book.isAnimating, "Bir araştırma kitabını açıp animasyonu bekle.");
        var workspace = NotebookWorkspaceController.Instance;
        var notebook = NotebookUI.Instance;
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var targetArea = (RectTransform)typeof(NotebookWorkspaceController).GetField("targetDropZone", Private).GetValue(workspace);
        var sources = new List<NotebookDraggableBlock>();
        foreach (var note in UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None))
            if (note.IsSource && Visible(note) && note.GetComponentInParent<InteractableBook>() == book) sources.Add(note);
        Check(sources.Count > 0, "Fiziksel kitap yüzeyinde kaynak notlar üretilmedi.");
        foreach (var source in sources)
        {
            workspace.GoToPage(notebook.workspacePages.Count);
            Canvas.ForceUpdateCanvases();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
                source.Data.blockType == NotebookBlockType.Quest ? targetArea.position : page.position);
            Check(workspace.TryDropBlock(source, point), "Gerçek kitap notu deftere alınamadı: " + source.Data.title);
        }
        workspace.GoToPage(0);
        Debug.Log("RESEARCH BOOK COLLECT PASS: " + sources.Count + " physical page blocks passed through the real notebook drop path. Targets: " + notebook.collectedQuests.Count);
    }

    [MenuItem("Senzora/Doğrulama/Araştırma Kitabı Okunabilirlik")]
    public static void CheckReadability()
    {
        Canvas.ForceUpdateCanvases();
        var book = GameManager.Instance != null ? GameManager.Instance.activeInteraction as InteractableBook : null;
        Check(Application.isPlaying && book != null && !book.isAnimating, "Kitap açılışını bekle.");
        int blocks = 0;
        foreach (var source in book.GetComponentsInChildren<NotebookDraggableBlock>())
        {
            if (!source.IsSource || !Visible(source)) continue;
            blocks++;
            foreach (var text in source.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Kitap metni karta sığmıyor: " + source.Data.title + " / " + text.text);
            }
        }
        Check(blocks > 0, "Açık kitapta görünür not yok.");
        Debug.Log("RESEARCH BOOK READABILITY PASS: " + blocks + " visible source blocks fit their text regions.");
    }

    public static bool Visible(NotebookDraggableBlock note)
    {
        if (!note.isActiveAndEnabled) return false;
        foreach (var group in note.GetComponentsInParent<CanvasGroup>()) if (group.alpha == 0) return false;
        return true;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
