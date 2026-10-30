using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class NotebookWorkspaceController
{
    private RectTransform notebookWindow, notebookToggle;
    private CanvasGroup notebookVisibility;
    private TextMeshProUGUI notebookToggleText;
    private bool notebookExpanded = true;
    private float notebookReveal = 1;
    private Vector2 notebookOpenPosition;
    private Vector2 workstationNotebookPosition;
    private bool standalonePositioned;
    // Tahtada .6, defteri tahtanın sağındaki boşluğa sığdırır; ölçüm cihazının üstüne binmez.
    private float NotebookScale => notebook == null || !notebook.useScreenSpaceNotebook ? 1f
        : ChalkboardManager.Instance != null && ChalkboardManager.Instance.isMachineActive ? .6f : .7f;

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
        GameLocalization.Set(archiveFooter, "TAB: KAPAT   /   Q E: BÖLÜM   /   TEKERLEK: SAYFA");
        Cursor.lockState = standaloneUnlocksCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = standaloneUnlocksCursor;
    }

    public void BrowseStandaloneNotebook(int sectionDelta, int pageDelta)
    {
        if (!IsStandaloneOpen || notebook.isOnMachine
            || (GameManager.Instance != null && GameManager.Instance.activeInteraction != null)) return;
        BrowseNotebook(sectionDelta, pageDelta);
    }

    private void BrowseNotebookOnBoard()
    {
        var board = ChalkboardManager.Instance;
        // Turing'de Q/E kod yazar; tahtada sürüklerken aynı girdiler parçayı döndürür.
        if (activeBook != null || board == null || !board.isMachineActive || GameInput.Held("Interact")) return;
        float scroll = GameInput.Scroll;
        // Belge kartlarının kendi kaydırma alanları tekerleği kullanır.
        bool overDocuments = scroll != 0 && board.EnsureDocuments() is ChalkboardDocuments documents
            && documents.ContainsPointer(Input.mousePosition);
        BrowseNotebook(GameInput.Pressed("PreviousTab") ? -1 : GameInput.Pressed("NextTab") ? 1 : 0,
            scroll == 0 || overDocuments ? 0 : scroll > 0 ? -1 : 1);
    }

    private void BrowseNotebook(int sectionDelta, int pageDelta)
    {
        if (IsPaused || !notebookExpanded) return;
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
        if (!IsArchiveNotebook)
        {
            notebookToggle = Panel("Defter Aç Kapat", bounds, new Vector2(158, 44), ink);
            notebookToggle.anchorMin = notebookToggle.anchorMax = new Vector2(1, 0);
            notebookToggle.anchoredPosition = new Vector2(-100, 34);
            notebookToggle.gameObject.AddComponent<Button>().onClick.AddListener(ToggleNotebook);
            notebookToggleText = Label(notebookToggle, "DEFTERİ KAPAT  ›", Vector2.zero, new Vector2(152, 44), 15);
            notebookToggleText.color = paper;
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
        // Kilitli kitapta defter tek bırakma alanıdır; kapanırsa oyuncu kartları taşıyamaz.
        if (activeBook != null && activeBook.ExitLocked && notebookExpanded) return;
        ResetNotebookPageTurn();
        if (IsStandaloneOpen) { Close(); return; }
        if (notebookExpanded && notebookReveal >= 1) notebookOpenPosition = notebookWindow.anchoredPosition;
        notebookExpanded = !notebookExpanded;
        drawer?.EndStroke();
        notebookVisibility.interactable = notebookVisibility.blocksRaycasts = notebookExpanded && notebookReveal >= 1;
        notebookWindow.gameObject.SetActive(true);
        if (notebookToggleText != null)
            GameLocalization.Set(notebookToggleText, notebookExpanded ? "DEFTERİ KAPAT  ›" : "‹  DEFTERİ AÇ");
    }

    private void UpdateNotebookPopup(float delta)
    {
        if (notebookWindow == null) return;
        float target = notebookExpanded ? 1 : 0;
        if (Mathf.Approximately(notebookReveal, target)) return;
        notebookReveal = Mathf.MoveTowards(notebookReveal, target, delta / .24f);
        float t = Mathf.SmoothStep(0, 1, notebookReveal);
        // Both positions use the notebook's right/center anchor, including after a resize.
        Vector2 dock = notebookOpenPosition + Vector2.down * (notebookWindow.sizeDelta.y + 40);
        if (!standalonePositioned && notebookToggle != null)
        {
            dock = bounds.InverseTransformPoint(notebookToggle.position);
            dock -= new Vector2(bounds.rect.xMax, bounds.rect.center.y);
        }
        notebookWindow.anchoredPosition = Vector2.Lerp(dock, notebookOpenPosition, t);
        notebookWindow.localScale = Vector3.one * NotebookScale * (standalonePositioned ? 1 : Mathf.Lerp(.08f, 1, t));
        notebookVisibility.alpha = t;
        notebookVisibility.interactable = notebookVisibility.blocksRaycasts = notebookReveal >= 1;
        if (notebookReveal <= 0) notebookWindow.gameObject.SetActive(false);
    }
}
