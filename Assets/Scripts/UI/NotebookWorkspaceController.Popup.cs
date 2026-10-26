using TMPro;
using UnityEngine;

public partial class NotebookWorkspaceController
{
    private RectTransform notebookWindow, notebookToggle;
    private CanvasGroup notebookVisibility;
    private TextMeshProUGUI notebookToggleText;
    private bool notebookExpanded = true;
    private float notebookReveal = 1;
    private Vector2 notebookOpenPosition;

    public void ToggleStandaloneNotebook()
    {
        if (!IsArchiveNotebook || IsPaused || notebook == null || notebook.isOnMachine
            || (GameManager.Instance != null && GameManager.Instance.activeInteraction != null)) return;
        if (IsStandaloneOpen) { Close(); return; }
        Open(null);
        // A notebook folded at a workstation must still open when requested with Tab.
        if (!notebookExpanded) ToggleNotebook();
        IsStandaloneOpen = true;
        if (boardControls != null) boardControls.SetActive(false);
        archiveFooter.text = "TAB: KAPAT   /   Q E: BÖLÜM   /   TEKERLEK: SAYFA";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        notebookToggle = Panel("Defter Aç Kapat", bounds, new Vector2(158, 44), ink);
        notebookToggle.anchorMin = notebookToggle.anchorMax = new Vector2(1, 0);
        notebookToggle.anchoredPosition = new Vector2(-100, 34);
        notebookToggle.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(ToggleNotebook);
        notebookToggleText = Label(notebookToggle, "DEFTERİ KAPAT  ›", Vector2.zero, new Vector2(152, 40), 15);
        notebookToggleText.color = paper;
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
        notebookToggleText.text = notebookExpanded ? "DEFTERİ KAPAT  ›" : "‹  DEFTERİ AÇ";
        notebookToggle.gameObject.SetActive(!UsesNotebookSkin || !notebookExpanded);
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
        notebookWindow.anchoredPosition = Vector2.Lerp(dock, notebookOpenPosition, t);
        notebookWindow.localScale = Vector3.one * Mathf.Lerp(.08f, 1, t);
        notebookVisibility.alpha = t;
        notebookVisibility.interactable = notebookVisibility.blocksRaycasts = notebookReveal >= 1;
        if (notebookReveal <= 0) notebookWindow.gameObject.SetActive(false);
    }
}
