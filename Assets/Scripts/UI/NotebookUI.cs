using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class NotebookUI : MonoBehaviour, ISaveable
{
    public static NotebookUI Instance;

    [Header("Serbest Çalışma Alanı")]
    public List<NotebookPageSaveData> workspacePages = new List<NotebookPageSaveData>();
    public List<NotebookBlockData> collectedQuests = new List<NotebookBlockData>();
    public int currentWorkspacePage;
    public bool IsWorkspaceActive => workspace != null && workspace.IsActive;
    private bool IsDocumentInteraction => IsWorkspaceActive || (GameManager.Instance != null
        && (GameManager.Instance.activeInteraction is InteractableBook || GameManager.Instance.activeInteraction is InteractableChalkboard));
    private NotebookWorkspaceController workspace;
    [Tooltip("Defteri eldeki model yerine ekran çalışma alanında göster.")]
    public bool useScreenSpaceNotebook;

    [Header("  Data Source")]
    public NotebookData notebookData;

    [Header("  UI Elements (World Space)")]
    public GameObject notebookPanel;
    public TextMeshProUGUI categoryTitleText;
    public TextMeshProUGUI contentText;
    public TextMeshProUGUI controlsHintText;

    [Header("  Animation Settings")]
    public Animator playerAnimator;
    public string animatorParameterName = "IsNotebookOpen";

    [Header("Depth of Field Ayarları")]
    [SerializeField]
    private Volume globalVolume;

    [SerializeField]
    private float notebookFocusDistance = 0.3f;

    [SerializeField]
    private float dofTransitionDuration = 0.25f;

    private DepthOfField m_DepthOfField;
    private float baseFocusDistance = 10f;
    private Coroutine dofCoroutine;

    private enum NotebookCategory
    {
        Passwords = 0,
        Logs = 1,
    }

    private int currentCategoryIndex = 0;
    private int currentTutorialPage = 0;

    public bool isNotebookOpen = false;
    public bool isOnMachine = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        workspace = GetComponent<NotebookWorkspaceController>();
        if (workspace == null) workspace = gameObject.AddComponent<NotebookWorkspaceController>();
        EnsureWorkspacePage();
    }

    private void OnEnable()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
        GameLocalization.LanguageChanged += UpdateUI;
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= UpdateUI;
        if (SaveManager.Instance != null) SaveManager.Instance.UnregisterSaveable(this);
        if (workspace != null) workspace.Close();
    }

    private void Start()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
        if (playerAnimator == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerAnimator = player.GetComponent<Animator>();
        }
        if (globalVolume != null && globalVolume.profile != null)
        {
            if (globalVolume.profile.TryGet(out m_DepthOfField))
            {
                baseFocusDistance = m_DepthOfField.focusDistance.value;
            }
        }
        if (notebookPanel != null)
            notebookPanel.SetActive(false);
    }

    private void Update()
    {
        if (workspace != null && workspace.IsArchiveNotebook)
        {
            if ((GameManager.Instance != null && (GameManager.Instance.isGamePaused || GameManager.Instance.activeInteraction != null)) || isOnMachine) return;
            if (GameInput.Pressed("ToggleNotebook")) workspace.ToggleStandaloneNotebook();
            else if (workspace.IsStandaloneOpen && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            {
                int section = GameInput.Pressed("PreviousTab") ? -1 : GameInput.Pressed("NextTab") ? 1 : 0;
                float wheel = Mathf.Sign(GameInput.Scroll) * (Mathf.Abs(GameInput.Scroll) > 0 ? .1f : 0f);
                workspace.BrowseStandaloneNotebook(section, wheel > .01f ? -1 : wheel < -.01f ? 1 : 0);
            }
            return;
        }
        if (IsWorkspaceActive || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return;
        if (GameManager.Instance != null && (GameManager.Instance.activeInteraction is InteractableBook
            || GameManager.Instance.activeInteraction is InteractableChalkboard)) return;
        // Tuşa basıldığında sadece animatörü tetikler, modeli gösterme/gizleme işini EVENT'e bırakır.
        if (GameInput.Pressed("ToggleNotebook") && !isOnMachine)
        {
            ToggleNotebook();
        }

        if (!isNotebookOpen && !isOnMachine)
            return;

        HandleInput();
    }

    public void ToggleNotebook()
    {
        if (workspace != null && workspace.IsArchiveNotebook) { workspace.ToggleStandaloneNotebook(); return; }
        if (IsDocumentInteraction) return;
        isNotebookOpen = !isNotebookOpen;
        if (playerAnimator != null)
            playerAnimator.SetBool(animatorParameterName, isNotebookOpen);

        if (isNotebookOpen)
        {
            // BURADAKİ 0.3f SABİTİNİ DEĞİŞTİRİYORUZ
            if (DoFManager.Instance != null) DoFManager.Instance.SetFocus(notebookFocusDistance);
            UpdateUI();
        }
        else
        {
            if (DoFManager.Instance != null) DoFManager.Instance.ResetFocus();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.UpdateCursorState();
    }

    // EVENT'LERİN KULLANACAĞI FONKSİYON
    public void SetNotebookVisibility(bool isVisible)
    {
        if (useScreenSpaceNotebook) return;
        if (isVisible && IsDocumentInteraction) return;
        // Eğer makinede değilse görünürlüğü değiştir (Makinedeyken defter elde olmamalı)
        if (!isOnMachine && notebookPanel != null)
        {
            notebookPanel.SetActive(isVisible);
        }
    }

    public void PutArmDown()
    {
        isNotebookOpen = false;
        if (playerAnimator != null)
            playerAnimator.SetBool(animatorParameterName, false);
    }

    public void ForceClose()
    {
        if (workspace != null && workspace.IsStandaloneOpen) workspace.Close();
        PutArmDown();
        if (notebookPanel != null)
            notebookPanel.SetActive(false);
    }

    public void SetWorkspaceMode(bool state)
    {
        if (state)
        {
            PutArmDown();
            if (notebookPanel != null) notebookPanel.SetActive(false);
            workspace.Open(null);
        }
        else workspace.Close();
    }

    public void OpenBookWorkspace(InteractableBook book)
    {
        PutArmDown();
        if (notebookPanel != null) notebookPanel.SetActive(false);
        workspace.Open(book);
    }

    public bool TryStoreBoardFormula(DraggableFormula formula, Vector2 screenPosition)
    {
        return workspace != null && workspace.TryStoreBoardFormula(formula, screenPosition);
    }

    public NotebookPageSaveData EnsureWorkspacePage()
    {
        if (workspacePages == null) workspacePages = new List<NotebookPageSaveData>();
        if (workspacePages.Count == 0) workspacePages.Add(new NotebookPageSaveData());
        currentWorkspacePage = Mathf.Max(0, currentWorkspacePage);
        while (workspacePages.Count <= currentWorkspacePage) workspacePages.Add(new NotebookPageSaveData());
        if (workspacePages[currentWorkspacePage] == null) workspacePages[currentWorkspacePage] = new NotebookPageSaveData();
        return workspacePages[currentWorkspacePage];
    }

    public void SaveData(ref GameData data)
    {
        EnsureCollectedQuests();
        data.notebookPages = new List<NotebookPageSaveData>();
        foreach (var page in workspacePages) data.notebookPages.Add(page.Clone());
        data.notebookQuests = collectedQuests.ConvertAll(q => q.Clone());
        data.currentNotebookPage = currentWorkspacePage;
    }

    public void LoadData(GameData data)
    {
        collectedQuests = data.notebookQuests != null
            ? data.notebookQuests.FindAll(q => q != null && q.blockType == NotebookBlockType.Quest).ConvertAll(q => q.Clone())
            : new List<NotebookBlockData>();
        workspacePages = new List<NotebookPageSaveData>();
        if (data.notebookPages != null)
            foreach (var page in data.notebookPages)
                workspacePages.Add(page != null ? page.Clone() : new NotebookPageSaveData());
        currentWorkspacePage = data.currentNotebookPage;
        EnsureWorkspacePage();
        EnsureCollectedQuests();
        if (workspace != null) workspace.RefreshPage();
        UpdateUI();
    }

    public void ResetWorkspace()
    {
        collectedQuests = new List<NotebookBlockData>();
        workspacePages = new List<NotebookPageSaveData> { new NotebookPageSaveData() };
        currentWorkspacePage = 0;
        if (workspace != null) workspace.RefreshPage();
    }

    // Eski kayıtlarda sayfalara yığılmış hedefleri yeni ortak bölüme bir kez taşı.
    public List<NotebookBlockData> EnsureCollectedQuests()
    {
        if (collectedQuests == null) collectedQuests = new List<NotebookBlockData>();
        collectedQuests.RemoveAll(q => q == null);
        EnsureWorkspacePage();
        foreach (var page in workspacePages)
        {
            if (page == null || page.blocks == null) continue;
            for (int i = 0; i < page.blocks.Count;)
            {
                var block = page.blocks[i];
                if (block == null || block.blockType != NotebookBlockType.Quest) { i++; continue; }
                if (string.IsNullOrEmpty(block.questID) || !collectedQuests.Exists(q => q.questID == block.questID))
                    collectedQuests.Add(block);
                page.blocks.RemoveAt(i);
            }
        }
        return collectedQuests;
    }

    private void HandleInput()
    {
        if (GameInput.Pressed("NextTab"))
            SwitchCategory(1);
        if (GameInput.Pressed("PreviousTab"))
            SwitchCategory(-1);

        if ((NotebookCategory)currentCategoryIndex == NotebookCategory.Logs)
        {
            float scroll = GameInput.Scroll;
            if (scroll < -0.1f)
                ChangeTutorialPage(1);
            else if (scroll > 0.1f)
                ChangeTutorialPage(-1);
        }
    }

    private void SwitchCategory(int direction)
    {
        int count = Enum.GetValues(typeof(NotebookCategory)).Length;
        currentCategoryIndex = (currentCategoryIndex + direction + count) % count;
        currentTutorialPage = 0;
        UpdateUI();
    }

    private void ChangeTutorialPage(int direction)
    {
        if (notebookData == null || notebookData.tutorialPages.Count <= 1)
            return;
        int newPage = Mathf.Clamp(
            currentTutorialPage + direction,
            0,
            notebookData.tutorialPages.Count - 1
        );
        if (newPage != currentTutorialPage)
        {
            currentTutorialPage = newPage;
            UpdateUI();
        }
    }

    private void ShowTutorial()
    {
        if (notebookData == null || notebookData.tutorialPages.Count == 0)
        {
            if (categoryTitleText != null)
                GameLocalization.Set(categoryTitleText, "LOGS EMPTY");
            if (contentText != null)
                GameLocalization.Set(contentText, "No operational data found.");
            return;
        }

        TutorialDataSO entry = notebookData.tutorialPages[currentTutorialPage];
        if (categoryTitleText != null)
            GameLocalization.SetFormat(categoryTitleText, "{0} ({1}/{2})", entry.title, currentTutorialPage + 1, notebookData.tutorialPages.Count);
        if (contentText != null)
            GameLocalization.Set(contentText, entry.content);
    }

    public void UpdateUI()
    {
        NotebookCategory currentCat = (NotebookCategory)currentCategoryIndex;
        if (controlsHintText != null)
            GameLocalization.Set(controlsHintText, GetHintText(currentCat));

        switch (currentCat)
        {
            case NotebookCategory.Passwords:
                if (categoryTitleText != null)
                    GameLocalization.Set(categoryTitleText, "DISCOVERED CLUES");
                ShowPasswords();
                break;
            case NotebookCategory.Logs:
                ShowTutorial();
                break;
        }
    }

    public void OpenNotebookToCategory(int categoryIndex)
    {
        if (useScreenSpaceNotebook)
        {
            if (workspace != null && workspace.IsArchiveNotebook && !workspace.IsActive)
                workspace.ToggleStandaloneNotebook();
            return;
        }
        // Eğer oyuncu makinedeyse (osiloskop vb.) defter fiziksel olarak ele gelmesin, sadece arkaplanda sayfa değişsin.
        if (isOnMachine || IsDocumentInteraction)
        {
            currentCategoryIndex = categoryIndex;
            UpdateUI();
            return;
        }

        // İstenen kategoriye geç
        currentCategoryIndex = categoryIndex;
        currentTutorialPage = 0; // Yeni bir sekmeye geçtiğimiz için alt sayfayı sıfırla

        // Eğer defter zaten açık değilse, aç!
        if (!isNotebookOpen)
        {
            isNotebookOpen = true;
            if (playerAnimator != null)
                playerAnimator.SetBool(animatorParameterName, true);

            // Fare imlecini (cursor) defter moduna göre güncelle
            if (GameManager.Instance != null)
                GameManager.Instance.UpdateCursorState();
        }

        UpdateUI();
    }

    private string GetHintText(NotebookCategory cat)
    {
        string baseHint = "[Q][E] Switch Tabs ";
        if (
            cat == NotebookCategory.Logs
            && notebookData != null
            && notebookData.tutorialPages.Count > 1
        )
            return "[Q][E] Switch Tabs  | [Scroll] Browse Logs";
        return baseHint;
    }

    private void ShowPasswords()
    {
        string list = "";
        var values = new List<object>();
        if (PasswordManager.Instance != null)
        {
            var clues = PasswordManager.Instance.GetDiscoveredClues();
            if (clues.Count == 0)
                list = "No data retrieved from the environment...";
            else
                foreach (var clue in clues)
                {
                    list += "> {" + values.Count + "}\n";
                    values.Add(clue);
                }
        }
        if (contentText != null)
            if (list == "No data retrieved from the environment..." || string.IsNullOrEmpty(list)) GameLocalization.Set(contentText, list);
            else GameLocalization.SetFormat(contentText, list, values.ToArray());
    }

    public void ShowPasswordNotification(string password)
    {
        // 0 = Passwords kategorisi
        OpenNotebookToCategory(0);
    }
}
