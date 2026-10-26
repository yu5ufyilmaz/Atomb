using System;
using System.Collections.Generic;
using UnityEngine;

// Yalnız scn_SynthesisBlockout'a takılır; üretim sahnelerinde bu mod bulunmaz.
public sealed class ResearchPuzzleTrial : MonoBehaviour
{
    public static ResearchPuzzleTrial Active { get; private set; }
    public FormulaItemSO partTemplate;
    public InteractableBook[] books = new InteractableBook[4];
    public ResearchTrialContent Content { get; private set; }
    public ChalkboardManager Board { get; private set; }
    public readonly HashSet<string> Completed = new HashSet<string>();

    private readonly Dictionary<string, GameData> workspaces = new Dictionary<string, GameData>();
    private string workingQuest;
    private bool restoring;

    public static bool IsResearch(ChalkboardManager board) => Active != null && Active.Board == board && Active.Content != null;

    public bool Configure(SynthesisBlockoutScene scene)
    {
        if (!enabled || gameObject.scene.name != "scn_SynthesisBlockout" || partTemplate == null || books == null || books.Length != 4)
            return false;
        Board = ChalkboardManager.Instance;
        var generator = ProceduralSynthesisGenerator.Instance;
        if (Board == null || generator == null) return false;
        foreach (var book in books) if (book == null) return false;
        var experiment = GetComponent<SecondTraceExperiment>();
        bool secondTrace = experiment != null && experiment.enabled;
        Content = secondTrace ? experiment.newTest ? SignalTrial.CreateContent(partTemplate) : SecondTraceExperiment.CreateContent(partTemplate)
            : ResearchTrialContent.Create(partTemplate);
        Active = this;
        Board.useProceduralQuests = false;
        Board.practiceQuest = Content.Quests[0];
        Board.practicePuzzles = new List<QuestBlueprintSO>(Content.Quests);
        if (secondTrace) Board.formulaCatalog = new List<FormulaItemSO>();
        foreach (var part in Content.Parts) if (!Board.formulaCatalog.Contains(part)) Board.formulaCatalog.Add(part);
        generator.RegisterCatalog(Content.Parts);
        for (int i = 0; i < books.Length; i++)
        {
            books[i].clueData = null;
            books[i].synthesisPages = Content.Books[i];
            books[i].includeProceduralClues = false;
            books[i].canContainPassword = false;
            books[i].usePhysicalPageBlocks = true;
            books[i].totalPages = secondTrace ? experiment.newTest ? Mathf.Max(2, (Content.Books[i].Count + 1) / 2 * 2) : 2 : 8;
            books[i].ClearPassword();
            books[i].emissionIntensity = 0;
            foreach (var renderer in books[i].GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                    if (material.HasProperty("_EmissiveIntensity")) material.SetFloat("_EmissiveIntensity", 0);
                    material.DisableKeyword("_EMISSION");
                }
        }
        if (NotebookWorkspaceController.Instance != null) NotebookWorkspaceController.Instance.generateProceduralRun = false;
        Board.QuestPinned += OnQuestPinned;
        var player = FindFirstObjectByType<SynthesisBlockoutPlayer>();
        if (player != null) player.researchBooks = books;
        if (secondTrace) experiment.Initialize(this);
        return true;
    }

    public SynthesisQuestData DisplayQuest(SynthesisQuestData quest)
    {
        return Content != null && quest != null && ResearchTrialContent.OwnsQuest(quest.questID) ? Content.DisplayQuest(quest) : quest;
    }

    public void MarkCompleted(string id)
    {
        Completed.Add(id);
    }

    private void OnQuestPinned(SynthesisQuestData quest)
    {
        if (restoring || quest == null) return;
        if (quest.questID == workingQuest)
        {
            quest.completed = Completed.Contains(workingQuest);
            return;
        }
        if (string.IsNullOrEmpty(workingQuest))
        {
            // Hedefi bulmadan kurulan hipotezi ve sabitlenen genel notları sahiplen.
            workingQuest = quest.questID;
            quest.completed = Completed.Contains(workingQuest);
            return;
        }
        restoring = true;
        try
        {
            if (!string.IsNullOrEmpty(workingQuest))
            {
                var previous = new GameData();
                Board.SaveData(ref previous);
                previous.savedChalkboard.pinnedQuestID = workingQuest;
                workspaces[workingQuest] = previous;
            }
            workingQuest = quest.questID;
            if (workspaces.TryGetValue(workingQuest, out var saved)) Board.LoadData(saved);
            else
            {
                Board.ResetBoard();
                Board.TryPinQuest(quest);
            }
            if (Board.PinnedQuest != null) Board.PinnedQuest.completed = Completed.Contains(workingQuest);
        }
        finally { restoring = false; }
    }

    private void OnDestroy()
    {
        if (Board != null) Board.QuestPinned -= OnQuestPinned;
        if (Active == this) Active = null;
        Content?.Dispose();
    }
}
