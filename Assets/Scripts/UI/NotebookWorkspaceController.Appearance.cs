using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class NotebookWorkspaceController
{
    [SerializeField] private Sprite notebookDividerSprite;
    private static readonly Color NotebookInk = new Color(.09f, .11f, .095f);
    private static readonly Color NotebookRule = new Color(.25f, .29f, .25f, .32f);

    private void ApplyNotebookSkin(RectTransform window)
    {
        var cover = window.GetComponent<Image>();
        cover.sprite = notebookSprite; cover.preserveAspect = true; cover.color = Color.clear;
        // The divider roots sit behind the page, so their inner edges disappear under the cover.
        var coverLayer = Panel("Defter Cildi", window, window.sizeDelta, Color.white);
        Stretch(coverLayer); coverLayer.SetAsFirstSibling();
        var coverImage = coverLayer.GetComponent<Image>();
        coverImage.sprite = notebookSprite; coverImage.preserveAspect = true; coverImage.raycastTarget = false;
        LayoutNotebookSkin();
        window.anchoredPosition = notebookOpenPosition = new Vector2(-346, 0);
        var header = (RectTransform)window.Find("Başlıktan Sürükle");
        header.anchorMin = header.anchorMax = header.pivot = new Vector2(.5f, .5f);
        header.sizeDelta = new Vector2(350, 40);
        header.anchoredPosition = new Vector2(-14, 307);
        header.GetComponent<Image>().color = Color.clear;
        header.GetComponent<DraggableWorkspaceItem>().rightEdgeExtension = 44;
        var title = header.GetComponentInChildren<TMP_Text>();
        title.text = "ARAŞTIRMA DEFTERİ"; title.font = notebookHeadingFont;
        title.color = NotebookInk; title.characterSpacing = 1.5f;
        title.rectTransform.sizeDelta = new Vector2(340, 36); title.fontSize = 21;

        foreach (var button in window.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null) continue;
            var image = button.GetComponent<Image>();
            image.sprite = null; image.color = Color.white;
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = new Color(.22f, .27f, .23f, .13f);
            colors.pressedColor = new Color(.22f, .27f, .23f, .22f);
            colors.disabledColor = Color.clear;
            button.colors = colors;
            label.font = notebookHeadingFont; label.color = NotebookInk;
            label.fontSize = 16; label.fontStyle = FontStyles.Normal;
            label.rectTransform.sizeDelta = ((RectTransform)button.transform).sizeDelta - new Vector2(4, 2);
        }
        var close = (RectTransform)window.Find("×");
        close.anchorMin = close.anchorMax = new Vector2(.5f, .5f);
        close.pivot = new Vector2(0, .5f);
        close.anchoredPosition = new Vector2(267, 306); close.sizeDelta = new Vector2(32, 40);
        var closeLabel = close.GetComponentInChildren<TMP_Text>();
        closeLabel.text = "×"; closeLabel.fontSize = 22; closeLabel.rectTransform.sizeDelta = new Vector2(26, 32);
        closeLabel.rectTransform.anchoredPosition = new Vector2(-3, 1);
        StyleNotebookDivider(close, false, true);
        pageLabel.font = notebookHeadingFont; pageLabel.color = NotebookInk;
        statusLabel.font = notebookBodyFont; statusLabel.color = NotebookInk;
        archiveFooter.font = notebookBodyFont; archiveFooter.color = NotebookInk;
        foreach (var rule in new[] { new Vector2(16, 282), new Vector2(16, 238), new Vector2(16, -234), new Vector2(16, -290) })
        {
            var line = Panel("Defter Cetveli", window, new Vector2(450, 1), NotebookRule);
            line.anchoredPosition = rule; line.GetComponent<Image>().raycastTarget = false;
        }
        foreach (string name in new[] { "Parçalar", "Bilgiler", "Hedefler", "Ölçümler" }) StyleNotebookTab(window.Find(name), name == "Parçalar");
    }

    private void LayoutNotebookSkin()
    {
        var window = (RectTransform)page.parent;
        window.sizeDelta = new Vector2(560, 560 * notebookSprite.rect.height / notebookSprite.rect.width);
        pageLabel.rectTransform.anchoredPosition = new Vector2(16, 262);
        pageLabel.rectTransform.sizeDelta = new Vector2(310, 28); pageLabel.fontSize = 16;
        partsPager.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, 262);
        partsPager.GetComponent<RectTransform>().sizeDelta = new Vector2(460, 36);
        partsPager.transform.GetChild(0).GetComponent<RectTransform>().anchoredPosition = new Vector2(-211, 0);
        partsPager.transform.GetChild(1).GetComponent<RectTransform>().anchoredPosition = new Vector2(211, 0);
        string[] names = { "Parçalar", "Bilgiler", "Hedefler", "Ölçümler" };
        for (int i = 0; i < names.Length; i++)
        {
            var tab = (RectTransform)window.Find(names[i]);
            tab.anchorMin = tab.anchorMax = new Vector2(.5f, .5f); tab.pivot = new Vector2(0, .5f);
            tab.anchoredPosition = new Vector2(267, 198 - i * 114);
        }
        foreach (var surface in new[] { page, targetPanel, informationPanel })
        {
            if (surface.GetComponent<CanvasGroup>() == null) surface.gameObject.AddComponent<CanvasGroup>();
            surface.sizeDelta = new Vector2(460, 410); surface.anchoredPosition = new Vector2(16, 8);
            surface.GetComponent<Image>().color = Color.clear;
        }
        statusLabel.rectTransform.anchoredPosition = new Vector2(16, -260);
        statusLabel.rectTransform.sizeDelta = new Vector2(442, 44); statusLabel.fontSize = 15;
        archiveFooter.rectTransform.anchoredPosition = new Vector2(16, -310);
        archiveFooter.rectTransform.sizeDelta = new Vector2(448, 26); archiveFooter.fontSize = 13;
    }

    private void StyleNotebookTab(Transform tab, bool selected)
    {
        var rect = (RectTransform)tab;
        rect.sizeDelta = new Vector2(44, 108);
        rect.anchoredPosition = new Vector2(selected ? 269 : 267, rect.anchoredPosition.y);
        StyleNotebookDivider(rect, selected);
        var label = tab.GetComponentInChildren<TMP_Text>();
        label.font = notebookHeadingFont != null ? notebookHeadingFont : label.font;
        label.color = NotebookInk; label.fontSize = 15;
        label.rectTransform.sizeDelta = new Vector2(90, 24);
        label.rectTransform.anchoredPosition = new Vector2(-3, 3);
        label.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        var underline = tab.Find("Seçili Bölüm");
        if (underline == null)
        {
            // Keep the named selection state; paper tint, print weight and a tiny offset show it visually.
            var line = Panel("Seçili Bölüm", tab, Vector2.zero, Color.clear);
            line.GetComponent<Image>().raycastTarget = false;
            underline = line;
        }
        underline.gameObject.SetActive(selected);
    }

    private void StyleNotebookDivider(RectTransform rect, bool selected, bool close = false)
    {
        rect.SetAsFirstSibling();
        rect.GetComponent<Image>().color = Color.clear;
        var paper = (RectTransform)rect.Find("Ayraç Kağıdı");
        if (paper == null)
        {
            paper = Panel("Ayraç Kağıdı", rect, Vector2.zero, Color.white);
            paper.SetAsFirstSibling();
        }
        paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(0, .5f);
        paper.sizeDelta = close ? new Vector2(80, 92) : new Vector2(112, 270);
        paper.anchoredPosition = new Vector2(close ? -41 : -57, 0);
        var image = paper.GetComponent<Image>();
        image.sprite = notebookDividerSprite; image.raycastTarget = false;
        var button = rect.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = colors.disabledColor = selected ? Color.white : new Color(.90f, .93f, .89f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.85f, .89f, .83f);
        button.colors = colors;
        var outline = rect.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    private void ShowEmptyArchivePaper(RectTransform surface, bool empty)
    {
        surface.GetComponent<Image>().sprite = null;
        surface.GetComponent<Image>().color = Color.clear;
    }

    private bool UsesHandwriting(NotebookDraggableBlock block) => !block.IsSource && notebookHandwritingFont != null
        && !SignalExperiment.IsMeasurementNote(block.Data);

    private void ApplyArchiveNotePose(RectTransform rect, NotebookBlockData note)
    {
        int hash = 17;
        foreach (char c in note.title ?? "") hash = unchecked(hash * 31 + c);
        float angle = UsesNotebookSkin && notebookHandwritingFont != null && !SignalExperiment.IsMeasurementNote(note)
            ? ((hash & 255) / 255f - .5f) * 1.4f : 0;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void StyleNotebookNote(NotebookDraggableBlock block)
    {
        if (!UsesNotebookSkin || block == null || !string.IsNullOrEmpty(block.Data?.clueDataID)) return;
        var background = block.GetComponent<Image>();
        if (background != null) { background.sprite = null; background.color = new Color(.84f, .85f, .81f, .97f); }
        foreach (var border in block.GetComponentsInChildren<NotebookSketchBorder>()) border.enabled = false;
        var outline = block.GetComponent<Outline>();
        if (outline != null) { outline.enabled = true; outline.effectDistance = new Vector2(1, -1); outline.effectColor = new Color(.20f, .24f, .21f, .28f); }
        var tape = block.transform.Find("Bant"); if (tape != null) tape.gameObject.SetActive(false);
        var footer = block.transform.Find("Bulmaca Etiketi"); if (footer != null) footer.gameObject.SetActive(false);
        bool notebookCard = block.GetComponentInParent<Canvas>()?.renderMode == RenderMode.ScreenSpaceOverlay;
        bool bookSource = block.IsSource && block.GetComponentInParent<InteractableBook>() != null;
        bool handwritten = UsesHandwriting(block);
        if (handwritten)
        {
            if (background != null) { background.color = Color.clear; background.canvasRenderer.cullTransparentMesh = false; }
            if (outline != null) outline.enabled = false;
            foreach (var border in block.GetComponentsInChildren<NotebookSketchBorder>())
            {
                border.enabled = true; border.color = new Color(.12f, .18f, .22f, .34f);
            }
        }
        foreach (var label in block.GetComponentsInChildren<TMP_Text>(true))
        {
            bool heading = label.name == "Başlık";
            label.font = handwritten ? notebookHandwritingFont : heading ? notebookHeadingFont : notebookBodyFont;
            label.color = handwritten ? new Color(.10f, .16f, .20f) : NotebookInk;
            label.fontStyle = heading && !handwritten ? FontStyles.Bold : FontStyles.Normal;
            label.fontSizeMin = handwritten ? heading ? 21 : 16 : heading ? bookSource ? 16 : 13 : notebookCard || bookSource ? 14 : 11;
            label.fontSizeMax = handwritten ? heading ? 27 : 24 : bookSource ? heading ? 22 : 18 : 16;
            label.fontSize = label.fontSizeMax; label.enableAutoSizing = true;
            if (heading && handwritten) label.text = block.DisplayTitle;
            if (heading)
            {
                bool part = block.Data.blockType == NotebookBlockType.PartClue && PuzzleParts.Find(block.Data.itemID) != null;
                label.rectTransform.offsetMin = new Vector2(14, part ? -66 : -50);
                label.rectTransform.offsetMax = new Vector2(-14, part ? -10 : -16);
            }
        }
        var body = block.transform.Find("Açıklama")?.GetComponent<TMP_Text>();
        var trace = block.GetComponentInChildren<SignalTrialGraphic>();
        RectTransform drawing = trace != null ? trace.rectTransform : block.GetComponentInChildren<MixtureGraphic>()?.rectTransform;
        var target = block.GetComponentInChildren<QuestDiagramGraphic>();
        if (body != null && drawing != null && block.Data.blockType != NotebookBlockType.Quest)
        {
            bool part = block.Data.blockType == NotebookBlockType.PartClue;
            body.rectTransform.anchorMax = new Vector2(1, part ? .36f : .30f);
            body.rectTransform.offsetMin = new Vector2(12, 10); body.rectTransform.offsetMax = new Vector2(-12, 0);
            drawing.anchorMin = new Vector2(.06f, part ? .42f : .36f); drawing.anchorMax = new Vector2(.94f, part ? .64f : .72f);
        }
        else if (body != null && target != null)
        {
            body.rectTransform.anchorMax = new Vector2(1, .64f);
            body.rectTransform.offsetMin = new Vector2(12, 10); body.rectTransform.offsetMax = new Vector2(-12, 0);
            body.fontSizeMin = 13; body.fontSizeMax = 18;
            body.overflowMode = TextOverflowModes.Ellipsis;
            target.rectTransform.anchorMin = new Vector2(.06f, .68f); target.rectTransform.anchorMax = new Vector2(.94f, .86f);
        }
        else if (body != null)
        {
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(14, 20); body.rectTransform.offsetMax = new Vector2(-14, -58);
        }
    }
}
