using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ResearchTrialValidation
{
    private const string ScenePath = "Assets/Scenes/scn_SynthesisBlockout.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Doğrulama/Araştırma Denemeleri")]
    public static void Run()
    {
        Check(!Application.isPlaying, "Araştırma içerik kontrolünü Play Mode kapalıyken çalıştırın.");
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        ResearchTrialContent content = null;
        try
        {
            ResearchPuzzleTrial trial = null;
            foreach (var root in scene.GetRootGameObjects())
                if (trial == null) trial = root.GetComponentInChildren<ResearchPuzzleTrial>(true);
            Check(trial != null && trial.GetComponent<SynthesisBlockoutScene>() != null,
                "Deneme bileşeni test sahnesinin kurucusuna bağlanmadı.");
            ValidateBooks(trial.books);
            content = ResearchTrialContent.Create(trial.partTemplate);
            ValidateContent(content);
            ValidateCompactDiagram(content);
            ValidatePhysicalGraphs(content);
            string scriptGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/PuzzleSystems/Chalkboard/ResearchPuzzleTrial.cs");
            Check(!string.IsNullOrEmpty(scriptGuid), "Araştırma denemesi script GUID'si bulunamadı.");
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Check(path == ScenePath || !File.ReadAllText(path).Contains("guid: " + scriptGuid),
                    "Araştırma denemesi üretim sahnesine eklendi: " + path);
            }
        }
        finally
        {
            content?.Dispose();
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Debug.Log("RESEARCH TRIAL PASS: four independent books, unlinked research notes, hidden answers, both hypotheses, two independent corrections, real prefab ports, JSON board roundtrip and test-scene isolation.");
    }

    private static void ValidateBooks(InteractableBook[] books)
    {
        Check(books != null && books.Length == 4, "Dört araştırma kitabı gerekli.");
        var unique = new HashSet<InteractableBook>();
        foreach (var book in books)
            Check(book != null && unique.Add(book), "Araştırma kitapları boş veya yinelenmiş.");
    }

    private static void ValidateCompactDiagram(ResearchTrialContent content)
    {
        var host = new GameObject("Compact research diagram check", typeof(RectTransform), typeof(QuestDiagramGraphic));
        try
        {
            var diagram = host.GetComponent<QuestDiagramGraphic>();
            diagram.rectTransform.sizeDelta = new Vector2(262, 136);
            var visible = content.DisplayQuest(content.Quests[1].CreateQuest());
            diagram.SetQuest(visible);
            using (var vertices = new VertexHelper())
            {
                typeof(QuestDiagramGraphic).GetMethod("OnPopulateMesh", Private, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(diagram, new object[] { vertices });
                int vertexIndex = 0;
                foreach (var bond in visible.bonds)
                {
                    Vector2 a = (Vector2)typeof(QuestDiagramGraphic).GetMethod("Position", Private).Invoke(diagram, new object[] { bond.fromIndex });
                    Vector2 b = (Vector2)typeof(QuestDiagramGraphic).GetMethod("Position", Private).Invoke(diagram, new object[] { bond.toIndex });
                    var v0 = new UIVertex(); var v1 = new UIVertex(); var v2 = new UIVertex(); var v3 = new UIVertex();
                    vertices.PopulateUIVertex(ref v0, vertexIndex); vertices.PopulateUIVertex(ref v1, vertexIndex + 1);
                    vertices.PopulateUIVertex(ref v2, vertexIndex + 2); vertices.PopulateUIVertex(ref v3, vertexIndex + 3);
                    var start = (v0.position + v1.position) * .5f;
                    var end = (v2.position + v3.position) * .5f;
                    Check(Vector2.Dot((Vector2)(end - start), b - a) > 0, "Sıkıştırılmış D-20 bağı etiketlerin altında ters çizildi.");
                    vertexIndex += bond.bondType == BondType.Double ? 8 : 4;
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void ValidateContent(ResearchTrialContent content)
    {
        Check(content.Quests.Count == 2 && content.Quests[0].questID != content.Quests[1].questID
            && content.Quests[0].nodes.Count == 5 && content.Quests[1].nodes.Count == 6,
            "Araştırma hedefleri farklı büyüklükte iki ayrı düzen olmalı.");
        Check(content.Books.Length == 4 && content.Parts.Count == 6, "Dört kitap veya ortak parça kataloğu eksik.");
        var targets = new HashSet<string>();
        var parts = new HashSet<string>();
        int generalNotes = 0;
        for (int book = 0; book < content.Books.Length; book++)
        {
            Check(content.Books[book] != null && content.Books[book].Count > 0, "Araştırma kitabı boş.");
            var pages = new HashSet<int>();
            foreach (var page in content.Books[book])
            {
                Check(pages.Add(page.pageIndex), "Aynı kitapta sayfa indisleri çakışıyor.");
                foreach (var note in page.blocks)
                {
                    if (note.blockType == NotebookBlockType.Quest)
                        Check(book == 0 && targets.Add(note.questID), "Hedefler yalnız soru kitabında olmalı.");
                    else if (note.blockType == NotebookBlockType.PartClue)
                        Check(book == 1 && parts.Add(note.itemID), "Parça çizimleri ayrı atlas kitabında olmalı.");
                    else
                    {
                        Check(string.IsNullOrEmpty(note.questID) && note.ruleIndex == -1 && !string.IsNullOrEmpty(note.description),
                            "Genel araştırma notu tek hedefin ipucu yuvasına bağlandı.");
                        if (book >= 2) generalNotes++;
                    }
                }
            }
        }
        Check(targets.Count == 2 && parts.Count == 6 && generalNotes >= 6,
            "Hedef, atlas veya bağımsız kanıt dağılımı eksik.");
        foreach (var quest in content.Quests)
        {
            Check(quest.output == null && quest.clueNotes.Count == 0 && !quest.nodes.Exists(n => n.isUnknown),
                "Araştırma, eksik parça kimliği veya bulunacak ipucu yuvalarıyla kilitlenmemeli.");
            var solution = Graph(content, quest, quest.bonds);
            Check(content.Evaluate(quest.questID, solution, out _), "Amaçlanan araştırma düzeni reddedildi.");
            var wrong = Clone(solution); wrong.parts[0].itemID = "research-wrong-part";
            Check(!content.Evaluate(quest.questID, wrong, out _), "Yanlış bileşen kabul edildi.");
            var extra = Clone(solution); extra.parts.Add(new SynthesisPartData { itemID = "extra", capacity = 4 });
            Check(!content.Evaluate(quest.questID, extra, out _), "Fazladan bileşen kabul edildi.");
            var source = quest.CreateQuest();
            var display = content.DisplayQuest(source);
            Check(!ReferenceEquals(display, source) && source.bonds.Count == quest.bonds.Count,
                "Görünüm kopyası gerçek doğrulama hedefini değiştirdi.");
            if (quest.questID == ResearchTrialContent.FirstQuestID)
                Check(display.bonds.Count == 0, "A hedefi bitmiş çözümü gösteriyor.");
            else
            {
                Check(display.title.Contains("BAŞARISIZ") && display.bonds.Count > 0
                    && !content.Evaluate(quest.questID, Graph(content, quest, display.bonds), out _),
                    "B görünümünde başarısız hipotez yerine çözüm gösterildi.");
                var failed = Graph(content, quest, content.FailedBonds);
                Check(!content.Evaluate(quest.questID, failed, out _), "B'nin başarısız kaydı kabul edildi.");
                var orderOnly = Clone(failed);
                orderOnly.bonds.Find(b => Joins(b, 5, 2)).bondType = BondType.Double;
                Check(!content.Evaluate(quest.questID, orderOnly, out _), "Yalnız çizgi türünü düzeltmek B'yi çözdü.");
                var routeOnly = Clone(failed);
                var returnBond = routeOnly.bonds.Find(b => Joins(b, 4, 1));
                returnBond.fromIndex = 4; returnBond.toIndex = 3;
                Check(!content.Evaluate(quest.questID, routeOnly, out _), "Yalnız dönüş yolunu düzeltmek B'yi çözdü.");
            }
        }
    }

    private static void ValidatePhysicalGraphs(ResearchTrialContent content)
    {
        var previousBoard = ChalkboardManager.Instance;
        var previousGenerator = ProceduralSynthesisGenerator.Instance;
        var host = new GameObject("Research trial checks (temporary)");
        try
        {
            SetSingleton(typeof(ChalkboardManager), null);
            SetSingleton(typeof(ProceduralSynthesisGenerator), null);
            var board = host.AddComponent<ChalkboardManager>();
            var generator = host.AddComponent<ProceduralSynthesisGenerator>();
            SetSingleton(typeof(ChalkboardManager), board);
            SetSingleton(typeof(ProceduralSynthesisGenerator), generator);
            board.boardTransform = new GameObject("Temporary research board").transform;
            board.boardTransform.SetParent(host.transform, false);
            board.useProceduralQuests = false;
            board.practicePuzzles = new List<QuestBlueprintSO>(content.Quests);
            board.formulaCatalog.AddRange(content.Parts);
            generator.RegisterCatalog(content.Parts);
            typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, true);
            foreach (var quest in content.Quests)
            {
                board.ResetBoard();
                Check(generator.CanRealizeQuest(quest.CreateQuest()), "Hedef gerçek prefab uçlarıyla gerçekleştirilemiyor.");
                Check(board.TryPinQuest(quest.CreateQuest()), "Araştırma hedefi sabitlenemedi.");
                SpawnSolution(board, content, quest);
                CheckGraph(board, content, quest);
                var saved = new GameData(); board.SaveData(ref saved);
                var json = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(saved));
                Check(json.savedChalkboard.parts.Count == quest.nodes.Count && json.savedChalkboard.bonds.Count == quest.bonds.Count,
                    "Tahta JSON kaydı parça veya bağ kaybetti.");
                board.ResetBoard(); board.LoadData(json);
                Check(board.PinnedQuest != null && board.PinnedQuest.questID == quest.questID, "Tahta kaydında hedef kayboldu.");
                CheckGraph(board, content, quest);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            SetSingleton(typeof(ChalkboardManager), previousBoard);
            SetSingleton(typeof(ProceduralSynthesisGenerator), previousGenerator);
        }
    }

    [MenuItem("Senzora/Doğrulama/Araştırma Denemeleri Canlı Akış")]
    public static void RunLive()
    {
        var trial = ResearchPuzzleTrial.Active;
        Check(Application.isPlaying && trial != null && trial.Content != null && trial.Board != null,
            "Araştırma test sahnesinde Play Mode gerekli.");
        ValidateBooks(trial.books);
        var board = trial.Board;
        var original = new GameData(); board.SaveData(ref original);
        var savedWorkspaces = (Dictionary<string, GameData>)typeof(ResearchPuzzleTrial).GetField("workspaces", Private).GetValue(trial);
        var previousWorkspaces = new Dictionary<string, GameData>(savedWorkspaces);
        var previousQuest = typeof(ResearchPuzzleTrial).GetField("workingQuest", Private).GetValue(trial);
        var completed = new HashSet<string>(trial.Completed);
        var practice = board.practiceQuest;
        bool wasCompleted = board.PinnedQuest != null && board.PinnedQuest.completed;
        var feedback = board.LastSynthesisMessage;
        bool machineActive = board.isMachineActive;
        try
        {
            typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, true);
            savedWorkspaces.Clear();
            typeof(ResearchPuzzleTrial).GetField("workingQuest", Private).SetValue(trial, null);
            trial.Completed.Clear(); board.ResetBoard();
            // B önce denenir: A'nın tamamlanması bir giriş koşulu olmamalı.
            for (int index = 1; index >= 0; index--)
            {
                var quest = trial.Content.Quests[index];
                if (index == 1)
                {
                    // Hedef henüz yokken kurulan çalışma ve genel kayıt kaybolmamalı.
                    SpawnSolution(board, trial.Content, quest);
                    Check(board.TryPinInformation(trial.Content.Books[2][0].blocks[0]), "Hedefsiz genel not sabitlenemedi.");
                }
                Check(board.TryPinQuest(quest.CreateQuest()), "A tamamlanmadan B seçilemedi.");
                if (index == 1)
                    Check(board.pinnedInformation.Count == 1 && board.boardTransform.GetComponentsInChildren<DraggableFormula>().Length == quest.nodes.Count,
                        "İlk hedefi sabitlemek önceden kurulmuş hipotezi veya genel notu sildi.");
                else
                {
                    Check(board.boardTransform.GetComponentsInChildren<DraggableFormula>().Length == 0,
                        "Yeni hedef önceki hedefin grafiğiyle başladı.");
                    SpawnSolution(board, trial.Content, quest);
                }
                CheckGraph(board, trial.Content, quest);
                board.AttemptSynthesis();
                Check(trial.Completed.Contains(quest.questID) && board.PinnedQuest.completed,
                    "Gerçek tahta sınaması araştırma başarısını kaydetmedi: " + board.LastSynthesisMessage);
                Check(board.boardTransform.GetComponentsInChildren<DraggableFormula>().Length == quest.nodes.Count,
                    "Başarılı araştırma parçaları tüketti veya çıktı üretti.");
                Check(board.TryPinQuest(quest.CreateQuest()) && board.PinnedQuest.completed,
                    "Aynı hedefi yeniden sabitlemek tamamlanma durumunu sildi.");
            }
            foreach (var quest in trial.Content.Quests)
            {
                Check(board.TryPinQuest(quest.CreateQuest()) && board.PinnedQuest.completed,
                    "Hedefe dönünce tamamlanma durumu kayboldu.");
                CheckGraph(board, trial.Content, quest);
            }
        }
        finally
        {
            typeof(ResearchPuzzleTrial).GetField("restoring", Private).SetValue(trial, true);
            try
            {
                typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, machineActive);
                board.LoadData(original);
                board.practiceQuest = practice;
                typeof(ChalkboardManager).GetProperty("LastSynthesisMessage").SetValue(board, feedback);
                typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, machineActive);
                savedWorkspaces.Clear();
                foreach (var entry in previousWorkspaces) savedWorkspaces.Add(entry.Key, entry.Value);
                typeof(ResearchPuzzleTrial).GetField("workingQuest", Private).SetValue(trial, previousQuest);
                trial.Completed.Clear(); foreach (string id in completed) trial.Completed.Add(id);
                if (board.PinnedQuest != null) board.PinnedQuest.completed = wasCompleted;
            }
            finally { typeof(ResearchPuzzleTrial).GetField("restoring", Private).SetValue(trial, false); }
        }
        Debug.Log("RESEARCH TRIAL LIVE PASS: B before A, actual board success, non-consuming inputs, separate graph restoration and retained completion. Original board state restored.");
    }

    private static void SpawnSolution(ChalkboardManager board, ResearchTrialContent content, QuestBlueprintSO quest)
    {
        var parts = new List<DraggableFormula>();
        foreach (var node in quest.nodes)
        {
            var data = content.Parts.Find(p => p.itemID == node.itemID);
            var point = board.boardTransform.TransformPoint(new Vector3(node.position.x, node.position.y, .02f));
            var part = (DraggableFormula)typeof(ChalkboardManager).GetMethod("CreateFormula", Private)
                .Invoke(board, new object[] { data, point, false });
            Check(part != null, "Araştırma parçası gerçek prefabdan üretilemedi.");
            foreach (var port in part.GetOwnNodes())
                if (!Application.isPlaying) typeof(FormulaNode).GetMethod("Awake", Private).Invoke(port, null);
            parts.Add(part);
        }
        var next = new int[parts.Count];
        foreach (var bond in quest.bonds)
        {
            var a = parts[bond.fromIndex].GetOwnNodes()[next[bond.fromIndex]++];
            var b = parts[bond.toIndex].GetOwnNodes()[next[bond.toIndex]++];
            a.ConnectTo(b);
            if (bond.bondType == BondType.Double) a.ConnectTo(b);
            Check(a.connectedNode == b && a.bondType == bond.bondType, "Gerçek uç istenen tek / çift bağı kuramadı.");
        }
    }

    private static void CheckGraph(ChalkboardManager board, ResearchTrialContent content, QuestBlueprintSO quest)
    {
        var parts = board.boardTransform.GetComponentsInChildren<DraggableFormula>();
        Check(parts.Length == quest.nodes.Count, "Hedefe ait tahta çalışma alanı eksik veya fazla parça içeriyor.");
        Check(content.Evaluate(quest.questID, board.BuildGraph(parts[0]), out string reason), "Gerçek tahta grafiği reddedildi: " + reason);
    }

    private static SynthesisGraphData Graph(ResearchTrialContent content, QuestBlueprintSO quest, List<SynthesisBondData> bonds)
    {
        var graph = new SynthesisGraphData();
        foreach (var node in quest.nodes)
        {
            var part = content.Parts.Find(p => p.itemID == node.itemID);
            graph.parts.Add(new SynthesisPartData { itemID = part.itemID, capacity = part.connectionCapacity });
        }
        graph.bonds = bonds.ConvertAll(b => new SynthesisBondData { fromIndex = b.fromIndex, toIndex = b.toIndex, bondType = b.bondType });
        return graph;
    }

    private static SynthesisGraphData Clone(SynthesisGraphData graph) =>
        JsonUtility.FromJson<SynthesisGraphData>(JsonUtility.ToJson(graph));
    private static bool Joins(SynthesisBondData bond, int a, int b) =>
        bond.fromIndex == a && bond.toIndex == b || bond.fromIndex == b && bond.toIndex == a;
    private static void SetSingleton(Type type, object value) => type.GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { value });
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
