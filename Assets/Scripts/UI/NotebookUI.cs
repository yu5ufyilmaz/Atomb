using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;

public class NotebookUI : MonoBehaviour
{
    public static NotebookUI Instance;

    [Header("📄 Data Source")]
    public NotebookData notebookData;

    [Header("🖼️ UI Elements")]
    public GameObject notebookPanel;

    [Tooltip("Kayma animasyonu için hareket edecek panelin (RectTransform) kendisini sürükle")]
    public RectTransform notebookRect;
    public TextMeshProUGUI categoryTitleText;
    public TextMeshProUGUI contentText;
    public TextMeshProUGUI controlsHintText;

    [Header("🎬 Animation Settings")]
    public float slideDuration = 0.35f;
    public Vector2 hiddenPosition = new Vector2(0, -1200); // Ekran dışı (Aşağıda)
    public Vector2 visiblePosition = new Vector2(0, 0); // Ekran ortası

    private enum NotebookCategory
    {
        Passwords = 0,
        Research = 1,
        Logs = 2,
    }

    private int currentCategoryIndex = 0;
    private int currentTutorialPage = 0;

    private string currentSymbolInfo = "No active research found in the field.";

    private bool isNotebookOpen = false;
    private bool isAnimating = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        // Başlangıçta paneli gizli pozisyona al ve kapat
        if (notebookRect != null)
            notebookRect.anchoredPosition = hiddenPosition;

        notebookPanel.SetActive(false);
    }

    private void Update()
    {
        // TAB tuşu ile aç/kapat (Animasyon tetikleyici)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleNotebook();
        }

        // Defter tam açık değilse veya animasyon oynuyorsa içerideki tuşları dinleme
        if (!isNotebookOpen || isAnimating)
            return;

        HandleInput();
    }

    public void ToggleNotebook()
    {
        // Animasyon sırasında tuşa art arda basılmasını engelle
        if (isAnimating)
            return;

        isNotebookOpen = !isNotebookOpen;
        StartCoroutine(SlideNotebook(isNotebookOpen));
    }

    private IEnumerator SlideNotebook(bool show)
    {
        isAnimating = true;

        if (show)
        {
            notebookPanel.SetActive(true);
            //TogglePlayerControls(false); // FPS bakışını kilitle
            UpdateUI();
        }

        // GameManager imleç güncellemesi
        if (GameManager.Instance != null)
            GameManager.Instance.UpdateCursorState();

        // Pürüzsüz Kayma (Smooth Slide) Animasyonu
        if (notebookRect != null)
        {
            float elapsed = 0f;
            Vector2 startPos = notebookRect.anchoredPosition;
            Vector2 targetPos = show ? visiblePosition : hiddenPosition;

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDuration;

                // Ease Out (Yumuşak yavaşlama) formülü
                t = t * t * (3f - 2f * t);

                notebookRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            notebookRect.anchoredPosition = targetPos;
        }

        // Kapanış animasyonu bittikten sonra objeyi tamamen kapat

        isAnimating = false;
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
            SwitchCategory(1);
        if (Input.GetKeyDown(KeyCode.Q))
            SwitchCategory(-1);

        if ((NotebookCategory)currentCategoryIndex == NotebookCategory.Logs)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll < 0f)
                ChangeTutorialPage(1);
            else if (scroll > 0f)
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

    public void UnlockSymbolResearch(int symbolID)
    {
        if (
            notebookData != null
            && symbolID >= 0
            && symbolID < notebookData.symbolDescriptions.Length
        )
        {
            currentSymbolInfo = notebookData.symbolDescriptions[symbolID];
        }
        else
        {
            currentSymbolInfo = "Unknown signal detected. Calculations failed.";
        }
        if (isNotebookOpen)
            UpdateUI();
    }

    public void UpdateUI()
    {
        NotebookCategory currentCat = (NotebookCategory)currentCategoryIndex;
        if (controlsHintText != null)
            controlsHintText.text = GetHintText(currentCat);

        switch (currentCat)
        {
            case NotebookCategory.Passwords:
                if (categoryTitleText != null)
                    categoryTitleText.text = "DISCOVERED CLUES";
                ShowPasswords();
                break;
            case NotebookCategory.Research:
                if (categoryTitleText != null)
                    categoryTitleText.text = "SYMBOL ANALYSIS";
                if (contentText != null)
                    contentText.text = currentSymbolInfo;
                break;
            case NotebookCategory.Logs:
                ShowTutorial();
                break;
        }
    }

    private string GetHintText(NotebookCategory cat)
    {
        string baseHint = "[Q][E] Switch Tabs ";
        if (
            cat == NotebookCategory.Logs
            && notebookData != null
            && notebookData.tutorialPages.Count > 1
        )
            return baseHint + " | [Scroll] Browse Logs";
        return baseHint;
    }

    private void ShowPasswords()
    {
        string list = "";
        if (PasswordManager.Instance != null)
        {
            var clues = PasswordManager.Instance.GetDiscoveredClues();
            if (clues.Count == 0)
                list = "No data retrieved from the environment...";
            else
            {
                foreach (var clue in clues)
                    list += $"> {clue}\n";
            }
        }
        if (contentText != null)
            contentText.text = list;
    }

    private void ShowTutorial()
    {
        if (notebookData == null || notebookData.tutorialPages.Count == 0)
        {
            if (categoryTitleText != null)
                categoryTitleText.text = "LOGS EMPTY";
            if (contentText != null)
                contentText.text = "No operational data found.";
            return;
        }
        var entry = notebookData.tutorialPages[currentTutorialPage];
        if (categoryTitleText != null)
            categoryTitleText.text =
                $"{entry.title.ToUpper()} ({currentTutorialPage + 1}/{notebookData.tutorialPages.Count})";
        if (contentText != null)
            contentText.text = entry.content;
    }

    public void ShowPasswordNotification(string password)
    {
        if (isNotebookOpen)
            UpdateUI();
    }
}
