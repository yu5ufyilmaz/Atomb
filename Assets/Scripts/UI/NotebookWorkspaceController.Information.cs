using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class NotebookWorkspaceController
{
    private RectTransform informationPanel, informationContent;
    private int informationArchivePage, solutionsArchivePage;
    private bool showingSolutions;
    private readonly Dictionary<string, float> informationGroupPositions = new Dictionary<string, float>();

    private void BuildInformationSection(RectTransform window)
    {
        informationPanel = Panel("Bilgi Arşivi", window, page.sizeDelta, new Color(.64f, .47f, .3f));
        informationPanel.anchoredPosition = page.anchoredPosition;
        informationPanel.gameObject.AddComponent<RectMask2D>();
        informationContent = new GameObject("Toplanan Bilgi Notları", typeof(RectTransform)).GetComponent<RectTransform>();
        informationContent.SetParent(informationPanel, false);
        informationContent.anchorMin = new Vector2(0, 1); informationContent.anchorMax = Vector2.one;
        informationContent.pivot = new Vector2(.5f, 1);
        if (!IsArchiveNotebook)
        {
            var scroll = informationPanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.viewport = informationPanel;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.content = informationContent;
        }
        informationPanel.gameObject.SetActive(false);
    }

    public void ShowInformation()
    {
        ShowNotebookView(NotebookView.Information);
        if (IsArchiveNotebook) return;
        string id = ChalkboardManager.Instance?.PinnedQuest?.questID;
        if (id != null && informationGroupPositions.TryGetValue(id, out float y))
            informationContent.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0, informationContent.sizeDelta.y - informationPanel.rect.height));
    }

    public void ShowSolutions()
    {
        if (!notebookExpanded) ToggleNotebook();
        ShowNotebookView(NotebookView.Solutions);
    }

    private List<NotebookBlockData> InformationNotes(bool solutions = false)
    {
        var notes = PuzzleInformation.NotebookNotes(notebook);
        if (IsArchiveNotebook)
        {
            notes.RemoveAll(n => SecondTraceExperiment.IsMeasurementNote(n) != solutions);
            var unique = new List<NotebookBlockData>();
            foreach (var note in notes) if (!unique.Exists(n => SameInformation(n, note))) unique.Add(note);
            notes = unique;
        }
        foreach (var note in notes)
        {
            if (string.IsNullOrEmpty(note.instanceID)) note.instanceID = Guid.NewGuid().ToString("N");
            PuzzleInformation.TryResolve(note, ChalkboardManager.Instance);
        }
        return notes;
    }

    private static bool SameInformation(NotebookBlockData a, NotebookBlockData b) =>
        a.questID == b.questID && a.title == b.title && a.description == b.description;

    private void RefreshInformation()
    {
        if (informationPanel == null || !informationPanel.gameObject.activeSelf) return;
        for (int i = informationContent.childCount - 1; i >= 0; i--) RemoveObject(informationContent.GetChild(i).gameObject);
        float y = 12;
        var notes = InformationNotes(showingSolutions);
        var board = ChalkboardManager.Instance;
        if (ResearchPuzzleTrial.IsResearch(board))
        {
            informationGroupPositions.Clear();
            int pages = Mathf.Max(1, (notes.Count + 1) / 2);
            int index = Mathf.Clamp(showingSolutions ? solutionsArchivePage : informationArchivePage, 0, pages - 1);
            if (showingSolutions) solutionsArchivePage = index; else informationArchivePage = index;
            pageLabel.text = UsesNotebookSkin ? $"{index + 1} / {pages}" : $"{(showingSolutions ? "ÖLÇÜMLER" : "KAYITLAR")}  {index + 1} / {pages}";
            UpdateArchivePager(index, pages);
            if (notes.Count == 0)
            {
                var empty = Label(informationContent, showingSolutions
                    ? "Tahtada DENEYİ ÖLÇ ile ölçüm kartı üret.\nKartı deftere al; Turing'deki yuvaya tak."
                    : "Kitaplardan aldığın bilgiler burada birikir.\nHedef bulmadan da not toplayabilirsin.\nAynı kayıt farklı sorularda işe yarayabilir.", Vector2.zero, new Vector2(450, 140), 17);
                Top(empty.rectTransform, new Vector2(0, -y)); y += 150;
            }
            for (int slot = 0; slot < 2; slot++)
            {
                int noteIndex = index * 2 + slot;
                if (noteIndex >= notes.Count) break;
                var block = CreateBlock(notes[noteIndex], false, informationContent);
                var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(UsesNotebookSkin ? 218 : 234, UsesNotebookSkin ? 240 : 200);
                Top(rect, new Vector2(slot == 0 ? -(UsesNotebookSkin ? 116 : 125) : (UsesNotebookSkin ? 116 : 125), UsesNotebookSkin ? -58 : -10)); ApplyArchiveNotePose(rect, notes[noteIndex]);
            }
            informationContent.sizeDelta = new Vector2(0, informationPanel.rect.height);
            informationContent.anchoredPosition = Vector2.zero;
            if (UsesNotebookSkin) ShowEmptyArchivePaper(informationPanel, notes.Count == 0);
            return;
        }
        var ids = new List<string>();
        foreach (var target in notebook.EnsureCollectedQuests())
            if (!string.IsNullOrEmpty(target.questID) && !ids.Contains(target.questID)) ids.Add(target.questID);
        foreach (var note in notes)
            if (PuzzleInformation.TryResolve(note, board) && !ids.Contains(note.questID)) ids.Add(note.questID);
        informationGroupPositions.Clear();
        if (notes.Count == 0 && ids.Count == 0)
        {
            var hint = Label(informationContent, "BİLGİ NOTLARI\n\nKitaptaki kural / ipucu notlarını deftere bırak.\nBütün sayfalardaki ipuçları burada görünür.", new Vector2(0, -145), new Vector2(470, 210), 18);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 1);
        }
        foreach (string id in ids)
        {
            informationGroupPositions[id] = y;
            int expected = PuzzleInformation.ExpectedCount(id, board), found = PuzzleInformation.FoundCount(notes, id, board);
            var heading = Panel("Bulmaca Bilgi Grubu", informationContent, new Vector2(474, 62), new Color(.9f, .81f, .57f));
            Top(heading, new Vector2(0, -y));
            var text = Label(heading, PuzzleInformation.Title(id, board) + "\nBİLGİLER " + found + " / " + expected,
                Vector2.zero, new Vector2(452, 58), 15);
            text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = 15;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            float start = y + 62; y += 80;
            for (int slot = 0; slot < expected; slot++)
            {
                var note = PuzzleInformation.Find(notes, id, slot, board);
                var branch = Panel("Bilgi Bağlantısı", informationContent, new Vector2(60, 2), new Color(.6f, .16f, .12f));
                Top(branch, new Vector2(-175, -y - 14)); branch.GetComponent<Image>().raycastTarget = false;
                if (note != null) y += PlaceInformation(note, y);
                else
                {
                    var empty = Panel("Eksik Bilgi " + (slot + 1), informationContent, new Vector2(320, 82), new Color(.85f, .8f, .66f, .65f));
                    Top(empty, new Vector2(15, -y));
                    Label(empty, "İPUCU " + (slot + 1) + " / HENÜZ BULUNMADI\nBu hedef için bir bilgi eksik.", Vector2.zero, new Vector2(306, 74), 14);
                    y += 100;
                }
            }
            var thread = Panel("Hedeften Bilgilere İp", informationContent, new Vector2(2, Mathf.Max(1, y - start - 35)), new Color(.6f, .16f, .12f));
            Top(thread, new Vector2(-205, -start)); thread.GetComponent<Image>().raycastTarget = false;
            thread.SetAsFirstSibling();
            y += 18;
        }
        var unlinked = notes.FindAll(n => !PuzzleInformation.TryResolve(n, board));
        if (unlinked.Count > 0)
        {
            var label = Label(informationContent, "BAĞLANTISI BELİRSİZ BİLGİLER", Vector2.zero, new Vector2(470, 35), 16);
            Top(label.rectTransform, new Vector2(0, -y)); y += 48;
            foreach (var note in unlinked) y += PlaceInformation(note, y);
        }
        informationContent.sizeDelta = new Vector2(0, Mathf.Max(informationPanel.rect.height, y));
    }

    private float PlaceInformation(NotebookBlockData note, float y)
    {
        var block = CreateBlock(note, false, informationContent);
        var rect = (RectTransform)block.transform;
        if (IsArchiveNotebook && SignalTrialGraphic.IsNote(note)) rect.sizeDelta = new Vector2(440, Mathf.Max(170, rect.sizeDelta.y));
        Top(rect, new Vector2(15, -y)); rect.localRotation = Quaternion.identity;
        return rect.sizeDelta.y + 18;
    }

    private static void Top(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = position;
    }

    private bool IsInformationDropArea(Vector2 point)
    {
        if (!Contains(informationPanel, point)) return false;
        if (EventSystem.current == null) return true;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(informationPanel);
    }
}
