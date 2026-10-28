using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class NotebookWorkspaceController
{
    private enum NotebookView { Notes, Targets, Information, Solutions, Guide }
    private RectTransform targetDropZone, targetContent, targetPanel, guidePanel, guideViewport, guideContent;
    private TextMeshProUGUI targetCount, guideTitle, guideBody;
    private QuestDiagramGraphic guideDiagram;
    private List<NotebookSynthesisGuide.Entry> guideEntries = new List<NotebookSynthesisGuide.Entry>();
    private int guideIndex, targetArchivePage;
    private NotebookView currentNotebookView;

    private void BuildNotebookSections(RectTransform window)
    {
        targetDropZone = Panel("Toplanan Hedefler", window, new Vector2(508, 86), new Color(.81f, .72f, .43f));
        targetDropZone.anchoredPosition = new Vector2(0, 228);
        targetDropZone.gameObject.AddComponent<Outline>().effectColor = new Color(.43f, .32f, .12f);
        targetCount = Label(targetDropZone, "TOPLANAN HEDEFLER (0)", new Vector2(0, 16), new Vector2(480, 26), 18);
        Label(targetDropZone, "HEDEF NOTUNU BURAYA BIRAK\nTopladıklarını görmek için tıkla", new Vector2(0, -20), new Vector2(480, 36), 13);
        targetDropZone.gameObject.AddComponent<Button>().onClick.AddListener(ShowCollectedTargets);
        if (IsArchiveNotebook) targetDropZone.gameObject.SetActive(false);
        Button(window, IsArchiveNotebook ? "Parçalar" : "Notlar", new Vector2(-192, 158), new Vector2(120, 30), () => ShowNotebookView(NotebookView.Notes));
        Button(window, "Hedefler", new Vector2(IsArchiveNotebook ? 64 : -64, 158), new Vector2(120, 30), ShowCollectedTargets);
        Button(window, "Bilgiler", new Vector2(IsArchiveNotebook ? -64 : 64, 158), new Vector2(120, 30), ShowInformation);
        if (IsArchiveNotebook) Button(window, "Ölçümler", new Vector2(192, 158), new Vector2(120, 30), ShowSolutions);
        else Button(window, "Hile Aç/Kapat", new Vector2(192, 158), new Vector2(120, 30), ToggleSynthesisGuide);
        BuildInformationSection(window);

        targetPanel = Panel("Hedef Arşivi", window, page.sizeDelta, new Color(.97f, .94f, .82f));
        targetPanel.anchoredPosition = page.anchoredPosition;
        targetPanel.gameObject.AddComponent<RectMask2D>();
        targetContent = new GameObject("Toplanan Hedef Kartları", typeof(RectTransform)).GetComponent<RectTransform>();
        targetContent.SetParent(targetPanel, false);
        targetContent.anchorMin = new Vector2(0, 1);
        targetContent.anchorMax = Vector2.one;
        targetContent.pivot = new Vector2(.5f, 1);
        if (!IsArchiveNotebook)
        {
            var scroll = targetPanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.viewport = targetPanel;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.content = targetContent;
        }

        guidePanel = Panel("Sentez Hile Sayfası", window, page.sizeDelta, new Color(.96f, .91f, .73f));
        guidePanel.anchoredPosition = page.anchoredPosition;
        guideTitle = Label(guidePanel, "", new Vector2(0, 169), new Vector2(370, 34), 16);
        guideTitle.enableAutoSizing = true; guideTitle.fontSizeMin = 11; guideTitle.fontSizeMax = 16;
        Button(guidePanel, "<", new Vector2(-227, 169), new Vector2(36, 30), () => ChangeGuidePage(-1));
        Button(guidePanel, ">", new Vector2(227, 169), new Vector2(36, 30), () => ChangeGuidePage(1));
        guideDiagram = new GameObject("Açık Çözüm Şeması", typeof(RectTransform), typeof(QuestDiagramGraphic)).GetComponent<QuestDiagramGraphic>();
        guideDiagram.transform.SetParent(guidePanel, false);
        guideDiagram.rectTransform.sizeDelta = new Vector2(478, 140);
        guideDiagram.rectTransform.anchoredPosition = new Vector2(0, 65);
        guideViewport = Panel("Çözüm Adımları", guidePanel, new Vector2(484, 172), new Color(1, 1, 1, .14f));
        guideViewport.anchoredPosition = new Vector2(0, -100);
        guideViewport.gameObject.AddComponent<RectMask2D>();
        var guideScroll = guideViewport.gameObject.AddComponent<ScrollRect>();
        guideScroll.horizontal = false; guideScroll.viewport = guideViewport;
        guideScroll.movementType = ScrollRect.MovementType.Clamped;
        guideBody = Label(guideViewport, "", Vector2.zero, new Vector2(462, 172), 15);
        guideBody.font = TMP_Settings.defaultFontAsset;
        guideBody.richText = false;
        guideBody.alignment = TextAlignmentOptions.TopLeft;
        guideContent = guideBody.rectTransform;
        guideContent.anchorMin = guideContent.anchorMax = new Vector2(.5f, 1);
        guideContent.pivot = new Vector2(.5f, 1);
        guideScroll.content = guideContent;
        ShowNotebookView(NotebookView.Notes);
    }

    public void ShowCollectedTargets() { ShowNotebookView(NotebookView.Targets); }
    public void ToggleSynthesisGuide()
    {
        ShowNotebookView(guidePanel != null && guidePanel.gameObject.activeSelf ? NotebookView.Notes : NotebookView.Guide);
    }

    private void ShowNotebookView(NotebookView view)
    {
        if (targetPanel == null) return;
        NotebookView previousView = currentNotebookView;
        ResetNotebookPageTurn();
        currentNotebookView = view;
        drawer.EndStroke();
        page.gameObject.SetActive(view == NotebookView.Notes);
        targetPanel.gameObject.SetActive(view == NotebookView.Targets);
        guidePanel.gameObject.SetActive(view == NotebookView.Guide);
        showingSolutions = view == NotebookView.Solutions;
        informationPanel.gameObject.SetActive(view == NotebookView.Information || showingSolutions);
        if (partsPager != null) partsPager.SetActive(view != NotebookView.Guide);
        if (IsArchiveNotebook)
        {
            LayoutArchiveNotebook(view);
            foreach (var tab in new[] { "Parçalar", "Bilgiler", "Hedefler", "Ölçümler" })
            {
                var button = page.parent.Find(tab);
                bool selected = tab == "Parçalar" && view == NotebookView.Notes || tab == "Bilgiler" && view == NotebookView.Information
                    || tab == "Hedefler" && view == NotebookView.Targets || tab == "Ölçümler" && showingSolutions;
                if (button != null)
                {
                    button.GetComponent<Image>().color = UsesNotebookSkin ? Color.clear
                        : selected ? new Color(.81f, .72f, .43f) : new Color(.76f, .73f, .62f);
                    if (UsesNotebookSkin) StyleNotebookTab(button, selected);
                }
            }
            pageLabel.text = view == NotebookView.Targets ? "HEDEF ARŞİVİ" : view == NotebookView.Information ? "BİLGİ ARŞİVİ" : "PARÇA ARŞİVİ";
            if (view == NotebookView.Notes) RefreshPartsArchive();
            if (view == NotebookView.Targets) RefreshCollectedTargets();
        }
        if (view == NotebookView.Information || showingSolutions) RefreshInformation();
        if (view == NotebookView.Guide)
        {
            guideEntries = NotebookSynthesisGuide.Build(ChalkboardManager.Instance, FormulaCatalog.Instance, notebook.notebookData);
            RefreshGuidePage();
            statusLabel.text = "Hile sayfası • Oklar: tarif değiştir • Tekerlek: adımları kaydır";
        }
        else statusLabel.text = showingSolutions ? "Ölçüm kartını Turing'in sol klavyesindeki yuvaya sürükle."
            : view == NotebookView.Information
            ? IsArchiveNotebook ? "Oklarla kayıtları gez. Tahtada istediğin yere raptiyele." : "Tüm sayfalardaki ipuçları. Tahtanın BİLGİLER alanına sürükle."
            : view == NotebookView.Targets
            ? IsArchiveNotebook ? "Hedefi tahtaya sürükle. Kayıt defterde korunur." : "Hedefi tahtaya sürükle. Tekerlek: hedefleri gez. Sağ tık: kaldır."
            : IsArchiveNotebook ? "Üstteki oklar: sayfa değiştir. Parçayı tahtaya sürükle; aslı kalır." : "Notu tahtaya sürükle. Hedefleri üstteki ayrı alana bırak.";
        statusLabel.color = ink;
        if (previousView != view && view != NotebookView.Guide)
        {
            int previous = previousView == NotebookView.Information ? 1 : previousView == NotebookView.Targets ? 2 : (int)previousView;
            int next = view == NotebookView.Information ? 1 : view == NotebookView.Targets ? 2 : (int)view;
            BeginNotebookPageTurn((next - previous + 4) % 4 <= 2 ? 1 : -1);
        }
    }

    private void LayoutArchiveNotebook(NotebookView view)
    {
        if (UsesNotebookSkin) { LayoutNotebookSkin(); return; }
        var window = (RectTransform)page.parent;
        float half = view == NotebookView.Notes || view == NotebookView.Targets ? 300 : 210;
        window.sizeDelta = new Vector2(540, half * 2);
        pageLabel.rectTransform.anchoredPosition = new Vector2(0, half - 56);
        partsPager.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, half - 56);
        foreach (string name in new[] { "Parçalar", "Bilgiler", "Hedefler", "Ölçümler" })
        {
            var tab = (RectTransform)window.Find(name);
            tab.anchoredPosition = new Vector2(tab.anchoredPosition.x, half - 95);
        }
        page.anchoredPosition = targetPanel.anchoredPosition = informationPanel.anchoredPosition = new Vector2(0, -18);
        targetPanel.sizeDelta = new Vector2(508, view == NotebookView.Targets ? 410 : 220);
        informationPanel.sizeDelta = new Vector2(508, 220);
        statusLabel.rectTransform.anchoredPosition = new Vector2(0, -half + 63);
        archiveFooter.rectTransform.anchoredPosition = new Vector2(0, -half + 26);
    }

    private void RefreshCollectedTargets()
    {
        if (targetContent == null) return;
        for (int i = targetContent.childCount - 1; i >= 0; i--) RemoveObject(targetContent.GetChild(i).gameObject);
        var targets = notebook.EnsureCollectedQuests();
        targetCount.text = "TOPLANAN HEDEFLER (" + targets.Count + ")";
        if (IsArchiveNotebook)
        {
            int pages = Mathf.Max(1, targets.Count);
            targetArchivePage = Mathf.Clamp(targetArchivePage, 0, pages - 1);
            if (targetPanel.gameObject.activeSelf)
            {
                pageLabel.text = UsesNotebookSkin ? $"{targetArchivePage + 1} / {pages}" : $"HEDEFLER  {targetArchivePage + 1} / {pages}";
                UpdateArchivePager(targetArchivePage, pages);
            }
            targetContent.sizeDelta = new Vector2(0, targetPanel.rect.height); targetContent.anchoredPosition = Vector2.zero;
            if (targets.Count == 0)
            {
                var empty = Label(targetContent, "Kitaptaki hedef notunu deftere bırak.\nTopladığın hedefler bu sayfalarda saklanır.", Vector2.zero, new Vector2(450, 110), 17);
                Top(empty.rectTransform, new Vector2(0, -20));
            }
            if (targetArchivePage < targets.Count)
            {
                var block = CreateBlock(targets[targetArchivePage], false, targetContent);
                var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(440, 400);
                Top(rect, new Vector2(0, -12)); ApplyArchiveNotePose(rect, targets[targetArchivePage]);
            }
            if (UsesNotebookSkin) ShowEmptyArchivePaper(targetPanel, targets.Count == 0);
            return;
        }
        targetContent.sizeDelta = new Vector2(0, Mathf.Max(page.rect.height, targets.Count * 210 + 16));
        if (targets.Count == 0)
        {
            var empty = Label(targetContent, "Henüz hedef toplanmadı.\nKitaptaki HEDEF notunu üstteki\nTOPLANAN HEDEFLER alanına bırak.", new Vector2(0, -130), new Vector2(470, 120), 18);
            empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(.5f, 1);
        }
        for (int i = 0; i < targets.Count; i++)
        {
            var block = CreateBlock(targets[i], false, targetContent);
            targets[i] = block.Data;
            var rect = (RectTransform)block.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -110 - i * 210);
            rect.localRotation = Quaternion.identity;
        }
    }

    private bool IsTargetDropArea(Vector2 position)
    {
        if (!Contains(targetDropZone, position) && !(targetPanel.gameObject.activeSelf && Contains(targetPanel, position))) return false;
        if (EventSystem.current == null) return true;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        return hits.Count > 0 && (hits[0].gameObject.transform.IsChildOf(targetDropZone)
            || hits[0].gameObject.transform.IsChildOf(targetPanel));
    }

    private void ChangeGuidePage(int direction)
    {
        if (guideEntries.Count == 0) return;
        guideIndex = (guideIndex + direction + guideEntries.Count) % guideEntries.Count;
        RefreshGuidePage();
    }

    private void RefreshGuidePage()
    {
        guideIndex = Mathf.Clamp(guideIndex, 0, Mathf.Max(0, guideEntries.Count - 1));
        var entry = guideEntries.Count > 0 ? guideEntries[guideIndex] : null;
        guideTitle.text = entry != null ? (guideIndex + 1) + "/" + guideEntries.Count + "  " + entry.title : "Sentez rehberi";
        bool diagram = entry != null && entry.solution != null;
        guideDiagram.gameObject.SetActive(diagram);
        if (diagram) guideDiagram.SetQuest(entry.solution, true);
        guideBody.text = entry != null ? entry.text : "Bu oturumda tanımlı bir sentez tarifi bulunamadı.";
        guideViewport.sizeDelta = new Vector2(484, diagram ? 172 : 310);
        guideViewport.anchoredPosition = new Vector2(0, diagram ? -100 : -30);
        guideContent.sizeDelta = new Vector2(462, Mathf.Max(guideViewport.rect.height, guideBody.GetPreferredValues(guideBody.text, 462, 0).y + 12));
        guideContent.anchoredPosition = Vector2.zero;
    }
}
