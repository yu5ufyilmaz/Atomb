using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class NotebookWorkspaceController
{
    private RectTransform notebookWindow, notebookToggle;
    private RectTransform researchControls, documentToggle;
    private CanvasGroup notebookVisibility;
    private TextMeshProUGUI notebookToggleText;
    private TextMeshProUGUI documentToggleText;
    private bool notebookExpanded = true;
    private float notebookReveal = 1;
    private Vector2 notebookOpenPosition;
    private Vector2 workstationNotebookPosition;
    private bool standalonePositioned;
    private float NotebookScale => notebook != null && notebook.useScreenSpaceNotebook ? .8f : 1f;

    private void RestoreWorkstationNotebookPosition()
    {
        if (!standalonePositioned || notebookWindow == null) return;
        standalonePositioned = false;
        notebookOpenPosition = workstationNotebookPosition;
        notebookWindow.anchoredPosition = notebookOpenPosition;
        notebookWindow.localScale = Vector3.one * NotebookScale;
        notebookVisibility.alpha = notebookReveal = 1;
        notebookExpanded = true;
    }

    public void ToggleStandaloneNotebook()
    {
        if (!IsArchiveNotebook || IsPaused || notebook == null || notebook.isOnMachine
            || (GameManager.Instance != null && GameManager.Instance.activeInteraction != null)) return;
        if (notebook.useScreenSpaceNotebook && GameManager.Instance != null && !GameManager.Instance.isGameStarted) return;
        if (IsStandaloneOpen) { Close(); return; }
        Open(null);
        // A notebook folded at a workstation must still open when requested with Tab.
        if (!notebookExpanded) ToggleNotebook();
        IsStandaloneOpen = true;
        if (notebook.useScreenSpaceNotebook) notebook.isNotebookOpen = true;
        if (!standaloneUnlocksCursor)
        {
            workstationNotebookPosition = notebookOpenPosition;
            standalonePositioned = true;
            notebookOpenPosition = new Vector2(-bounds.rect.width + notebookWindow.sizeDelta.x * NotebookScale * .5f + 45,
                -bounds.rect.height * .5f + notebookWindow.sizeDelta.y * NotebookScale * .5f + 28);
            notebookReveal = 0;
            notebookVisibility.alpha = 0;
            notebookVisibility.interactable = notebookVisibility.blocksRaycasts = false;
            notebookWindow.anchoredPosition = notebookOpenPosition + Vector2.down * (notebookWindow.sizeDelta.y + 40);
        }
        if (boardControls != null) boardControls.SetActive(false);
        archiveFooter.text = "TAB: KAPAT   /   Q E: BÖLÜM   /   TEKERLEK: SAYFA";
        Cursor.lockState = standaloneUnlocksCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = standaloneUnlocksCursor;
    }

    public void BrowseStandaloneNotebook(int sectionDelta, int pageDelta)
    {
        if (!IsStandaloneOpen || IsPaused || !notebookExpanded || notebook.isOnMachine
            || (GameManager.Instance != null && GameManager.Instance.activeInteraction != null)) return;
        if (sectionDelta != 0)
        {
            var sections = new[] { NotebookView.Notes, NotebookView.Information, NotebookView.Targets, NotebookView.Solutions };
            int current = System.Array.IndexOf(sections, currentNotebookView);
            ShowNotebookView(sections[(current + (sectionDelta > 0 ? 1 : 3)) % 4]);
        }
        else if (pageDelta != 0) ChangeArchivePage(pageDelta > 0 ? 1 : -1);
    }

    private void BuildNotebookPopup(RectTransform window)
    {
        notebookWindow = window;
        notebookOpenPosition = window.anchoredPosition;
        notebookVisibility = window.gameObject.AddComponent<CanvasGroup>();
        if (IsArchiveNotebook)
        {
            researchControls = Panel("Araştırma Araçları", bounds, new Vector2(456, 84), new Color(.11f, .15f, .13f, .98f));
            researchControls.anchorMin = researchControls.anchorMax = new Vector2(.5f, 1);
            researchControls.anchoredPosition = new Vector2(0, -54);
            var heading = Label(researchControls, "ARAŞTIRMA ARAÇLARI", new Vector2(0, 25), new Vector2(440, 22), 12);
            heading.color = new Color(.93f, .89f, .74f);
        }
        notebookToggle = Panel("Defter Aç Kapat", researchControls != null ? researchControls : bounds,
            new Vector2(IsArchiveNotebook ? 212 : 158, IsArchiveNotebook ? 48 : 44), ink);
        if (researchControls != null) notebookToggle.anchoredPosition = new Vector2(-110, -10);
        else
        {
            notebookToggle.anchorMin = notebookToggle.anchorMax = new Vector2(1, 0);
            notebookToggle.anchoredPosition = new Vector2(-100, 34);
        }
        notebookToggle.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(ToggleNotebook);
        notebookToggleText = Label(notebookToggle, IsArchiveNotebook ? "DEFTERİ KAPAT [TAB]" : "DEFTERİ KAPAT  ›", Vector2.zero,
            new Vector2(IsArchiveNotebook ? 204 : 152, 44), 15);
        notebookToggleText.color = paper;
        if (researchControls != null)
        {
            documentToggle = Panel("Belgeler Aç Kapat", researchControls, new Vector2(212, 48), new Color(.71f, .57f, .31f));
            documentToggle.anchoredPosition = new Vector2(110, -10);
            documentToggleText = Label(documentToggle, "BELGELERİ AÇ", Vector2.zero, new Vector2(204, 44), 15);
            documentToggleText.color = ink;
            documentToggle.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                var documents = ChalkboardManager.Instance?.EnsureDocuments();
                if (documents == null || IsPaused) return;
                documents.SetDocumentPanelOpen(!documents.IsDocumentPanelOpen);
                RefreshResearchControls();
            });
        }
        Button(window, "×", new Vector2(window.sizeDelta.x * .5f - 20, window.sizeDelta.y * .5f - 18),
            new Vector2(30, 28), ToggleNotebook);
        if (IsArchiveNotebook)
        {
            var close = (RectTransform)window.GetChild(window.childCount - 1);
            close.anchorMin = close.anchorMax = new Vector2(.5f, 1);
            close.anchoredPosition = new Vector2(window.sizeDelta.x * .5f - 20, -18);
        }
    }

    public void ToggleNotebook()
    {
        if (IsPaused || notebookWindow == null) return;
        ResetNotebookPageTurn();
        if (IsStandaloneOpen) { Close(); return; }
        if (notebookExpanded && notebookReveal >= 1) notebookOpenPosition = notebookWindow.anchoredPosition;
        notebookExpanded = !notebookExpanded;
        drawer?.EndStroke();
        notebookVisibility.interactable = notebookVisibility.blocksRaycasts = notebookExpanded && notebookReveal >= 1;
        notebookWindow.gameObject.SetActive(true);
        notebookToggleText.text = IsArchiveNotebook ? notebookExpanded ? "DEFTERİ KAPAT [TAB]" : "DEFTERİ AÇ [TAB]"
            : notebookExpanded ? "DEFTERİ KAPAT  ›" : "‹  DEFTERİ AÇ";
        notebookToggle.gameObject.SetActive(true);
    }

    private void RefreshResearchControls()
    {
        if (researchControls == null) return;
        researchControls.SetAsLastSibling();
        notebookToggleText.text = notebookExpanded ? "DEFTERİ KAPAT [TAB]" : "DEFTERİ AÇ [TAB]";
        bool atBoard = !IsStandaloneOpen && activeBook == null && ChalkboardManager.Instance != null && ChalkboardManager.Instance.isMachineActive;
        documentToggle.gameObject.SetActive(atBoard);
        researchControls.sizeDelta = new Vector2(atBoard ? 456 : 236, 84);
        notebookToggle.anchoredPosition = new Vector2(atBoard ? -110 : 0, -10);
        if (!atBoard) return;
        var documents = ChalkboardManager.Instance.EnsureDocuments();
        documents.SetScreenControlsActive(true);
        documentToggleText.text = documents.IsDocumentPanelOpen ? "BELGELERİ TOPLA" : "BELGELERİ AÇ";
    }

    private void UpdateNotebookPopup(float delta)
    {
        if (notebookWindow == null) return;
        float target = notebookExpanded ? 1 : 0;
        if (Mathf.Approximately(notebookReveal, target)) return;
        notebookReveal = Mathf.MoveTowards(notebookReveal, target, delta / .24f);
        float t = Mathf.SmoothStep(0, 1, notebookReveal);
        // Both positions use the notebook's right/center anchor, including after a resize.
        Vector2 dock = bounds.InverseTransformPoint(notebookToggle.position);
        dock -= new Vector2(bounds.rect.xMax, bounds.rect.center.y);
        if (standalonePositioned) dock = notebookOpenPosition + Vector2.down * (notebookWindow.sizeDelta.y + 40);
        notebookWindow.anchoredPosition = Vector2.Lerp(dock, notebookOpenPosition, t);
        notebookWindow.localScale = Vector3.one * NotebookScale * (standalonePositioned ? 1 : Mathf.Lerp(.08f, 1, t));
        notebookVisibility.alpha = t;
        notebookVisibility.interactable = notebookVisibility.blocksRaycasts = notebookReveal >= 1;
        if (notebookReveal <= 0) notebookWindow.gameObject.SetActive(false);
    }
}
