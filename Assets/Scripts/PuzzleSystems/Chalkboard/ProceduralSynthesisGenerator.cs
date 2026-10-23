using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public class ProceduralSynthesisGenerator : MonoBehaviour, ISaveable
{
    public static ProceduralSynthesisGenerator Instance { get; private set; }

    [Header("Parça Kataloğu")]
    public List<FormulaItemSO> formulaCatalog = new List<FormulaItemSO>();
    [Tooltip("İsteğe bağlı beş aşama çıktısı. İlk dört ürünün sonraki aşamada bağ kurabilmesi gerekir.")]
    public List<FormulaItemSO> outputCatalog = new List<FormulaItemSO>();
    [Tooltip("Az içerikli eski sahnelerde mevcut çok uçlu prefabdan kurgusal modüller üretir; assetleri değiştirmez.")]
    public bool allowFallbackCatalog = true;
    [ShowIf(nameof(allowFallbackCatalog))]
    [Tooltip("En az dört farklı kurgusal modül. Aynı fiziksel prefabın uç sayısı kapasite sınırını belirler.")]
    [Min(4)] public int fallbackPartCount = 7;

    public SynthesisRunData Run { get; private set; }
    public SynthesisQuestData ActiveQuest => Run != null && Run.quests != null && Run.currentStage >= 0 && Run.currentStage < Run.quests.Count
        ? Run.quests[Run.currentStage] : null;

    private readonly List<FormulaItemSO> runtimeItems = new List<FormulaItemSO>();
    private FormulaItemSO outputTemplate;
    private SaveManager registeredSaveManager;
    private SynthesisRunData loadedSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RegisterSaveable();
    }

    private void OnEnable() { RegisterSaveable(); }
    private void Start() { RegisterSaveable(); }

    private void OnDisable()
    {
        if (registeredSaveManager != null) registeredSaveManager.UnregisterSaveable(this);
        registeredSaveManager = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearRuntimeCatalog();
    }

    private void RegisterSaveable()
    {
        if (SaveManager.Instance == null || registeredSaveManager == SaveManager.Instance) return;
        registeredSaveManager = SaveManager.Instance;
        registeredSaveManager.RegisterSaveable(this);
    }

    public void RegisterFormula(FormulaItemSO formula)
    {
        if (formula == null || string.IsNullOrEmpty(formula.itemID)) return;
        foreach (var existing in formulaCatalog)
            if (existing != null && existing.itemID == formula.itemID) return;
        formulaCatalog.Add(formula);
    }

    public void RegisterCatalog(IEnumerable<FormulaItemSO> catalog)
    {
        if (catalog == null || ReferenceEquals(catalog, formulaCatalog)) return;
        foreach (var formula in catalog) RegisterFormula(formula);
    }

    public FormulaItemSO ResolveFormula(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var formula in formulaCatalog)
            if (formula != null && formula.itemID == id) return formula;
        return null;
    }

    public void EnsureRun()
    {
        RegisterSaveable();
        if (Run == null || Run.quests == null || Run.quests.Count != 5)
            GenerateRun(UnityEngine.Random.Range(1, int.MaxValue));
    }

    [Button("Yeni Rastgele Sentez Koşusu")]
    private void GenerateRandomRun() { GenerateRun(UnityEngine.Random.Range(1, int.MaxValue)); }

    public void GenerateRun(int seed)
    {
        loadedSource = null;
        PrepareCatalog();
        if (outputTemplate == null)
        {
            Run = null;
            Debug.LogWarning("Sentez koşusu kurulamadı: en az iki uyumlu ucu olan bir çizim prefabı/katalog gerekli.", this);
            return;
        }
        var random = new System.Random(seed);
        var run = new SynthesisRunData { seed = seed };
        FormulaItemSO previous = null;
        for (int stage = 0; stage < 5; stage++)
        {
            FormulaItemSO output = CreateOutput(seed, stage);
            SynthesisQuestData quest = null;
            for (int attempt = 0; attempt < 80 && quest == null; attempt++)
                quest = BuildQuest(random, seed, stage, previous, output);
            if (quest == null)
            {
                Run = null;
                Debug.LogWarning("Uyumlu, çözülebilir sentez aşaması üretilemedi. Katalog uç tiplerini ve izin verilen formülleri kontrol edin.", this);
                return;
            }
            run.quests.Add(quest);
            previous = output;
        }
        Run = run;
    }

    public bool CompleteActiveQuest()
    {
        SynthesisQuestData quest = ActiveQuest;
        if (quest == null || quest.completed) return false;
        quest.completed = true;
        Run.currentStage++;
        return true;
    }

    public void LoadData(GameData data)
    {
        // Tahta, kendi ürünlerini kurmadan önce koşuyu yükleyebilir; SaveManager aynı kaydı ikinci kez iletebilir.
        if (data != null && Run != null && ReferenceEquals(loadedSource, data.synthesisRun)) return;
        if (data == null || data.synthesisRun == null || data.synthesisRun.quests == null || data.synthesisRun.quests.Count != 5)
        {
            Run = null;
            loadedSource = null;
            return;
        }
        var loaded = JsonUtility.FromJson<SynthesisRunData>(JsonUtility.ToJson(data.synthesisRun));
        PrepareCatalog();
        if (outputTemplate == null) { Run = null; return; }
        for (int stage = 0; stage < loaded.quests.Count; stage++) CreateOutput(loaded.seed, stage);
        loaded.currentStage = Mathf.Clamp(loaded.currentStage, 0, loaded.quests.Count);
        Run = loaded;
        loadedSource = data.synthesisRun;
    }

    public void SaveData(ref GameData data)
    {
        data.synthesisRun = Run == null ? null : JsonUtility.FromJson<SynthesisRunData>(JsonUtility.ToJson(Run));
    }

    private void PrepareCatalog()
    {
        ClearRuntimeCatalog();
        formulaCatalog.RemoveAll(item => item == null);
        foreach (var formula in Resources.FindObjectsOfTypeAll<FormulaItemSO>())
            if ((formula.hideFlags & HideFlags.DontSave) == 0) RegisterFormula(formula);
        RegisterCatalog(outputCatalog);
        formulaCatalog.Sort((a, b) => string.CompareOrdinal(a.itemID, b.itemID));

        FormulaItemSO donor = null;
        int mostNodes = 0, usableTypes = 0;
        outputTemplate = null;
        foreach (var item in formulaCatalog)
        {
            if (item.isDecoy || item.chalkPrefab == null) continue;
            int nodeCount = OwnNodes(item).Length;
            if (nodeCount > mostNodes) { donor = item; mostNodes = nodeCount; }
            bool isAuthoredOutput = outputCatalog != null && outputCatalog.Contains(item);
            if (!isAuthoredOutput && nodeCount > 0 && item.connectionCapacity > 0) usableTypes++;
            if (!isAuthoredOutput && nodeCount >= 2 && item.connectionCapacity >= 2 && outputTemplate == null) outputTemplate = item;
        }

        // Eski örnek içerikte yalnız A/B ve bağsız C bulunur. Çalışabilir örnek içerik assetleri ezmeden kurulur.
        if (allowFallbackCatalog && donor != null && mostNodes >= 2 && (usableTypes < 4 || outputTemplate == null))
        {
            for (int i = 0; i < Mathf.Max(4, fallbackPartCount); i++)
            {
                var part = CloneRuntime(donor, "MOD-" + (char)('A' + i), "Kurgusal Modül " + (char)('A' + i));
                part.connectionCapacity = mostNodes;
                part.useUniversalNodeRules = true;
                part.description = "Deneysel şema modülü. Uç tiplerini ve kitapta verilen bağlantı kurallarını karşılaştırın.";
                part.initiallyBlockedNodes = i % 3 == 0 ? new List<int> { 0 } : new List<int>();
                part.initiallyUnstableNodes = i % 3 == 1 ? new List<int> { mostNodes - 1 } : new List<int>();
                if (i == 0) outputTemplate = part;
            }
            var decoy = CloneRuntime(donor, "ARIZA", "Bozuk Deney Modülü");
            decoy.isDecoy = true;
            decoy.useUniversalNodeRules = true;
            decoy.description = "Bu deney modülü kararsız kaldı. Hedef şemalarda kullanmayın.";
        }
    }

    private FormulaItemSO CreateOutput(int seed, int stage)
    {
        if (outputCatalog != null && stage < outputCatalog.Count && outputCatalog[stage] != null)
        {
            FormulaItemSO authored = outputCatalog[stage];
            if (authored.chalkPrefab != null && authored.chalkPrefab.GetComponent<DraggableFormula>() != null && !authored.isDecoy
                && (stage == 4 || (authored.connectionCapacity >= 2 && OwnNodes(authored).Length >= 2)))
                return authored;
        }
        string id = "sx_run_" + seed + "_s" + stage;
        FormulaItemSO existing = ResolveFormula(id);
        if (existing != null) return existing;
        var output = CloneRuntime(outputTemplate, id, stage == 4 ? "Senzora Çekirdeği" : "Ara Bileşen " + (stage + 1));
        output.initiallyBlockedNodes = new List<int>();
        output.initiallyUnstableNodes = new List<int>();
        output.description = "Sentez zincirinin " + (stage + 1) + ". aşamasında üretilir.";
        return output;
    }

    private FormulaItemSO CloneRuntime(FormulaItemSO source, string id, string label)
    {
        var item = Instantiate(source);
        item.hideFlags = HideFlags.DontSave;
        item.itemID = id;
        item.itemName = label;
        item.name = label;
        item.connectionAliasID = string.IsNullOrEmpty(source.connectionAliasID) ? source.itemID : source.connectionAliasID;
        runtimeItems.Add(item);
        RegisterFormula(item);
        return item;
    }

    private void ClearRuntimeCatalog()
    {
        foreach (var item in runtimeItems)
        {
            formulaCatalog.Remove(item);
            if (item == null) continue;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
        runtimeItems.Clear();
        outputTemplate = null;
    }

    private SynthesisQuestData BuildQuest(System.Random random, int seed, int stage, FormulaItemSO previous, FormulaItemSO output)
    {
        var choices = new List<FormulaItemSO>();
        foreach (var item in formulaCatalog)
            if (item != null && !item.isDecoy && !item.itemID.StartsWith("sx_run_", StringComparison.Ordinal)
                && (outputCatalog == null || !outputCatalog.Contains(item))
                && item.connectionCapacity > 0 && OwnNodes(item).Length > 0)
                choices.Add(item);
        choices.Sort((a, b) => string.CompareOrdinal(a.itemID, b.itemID));
        if (choices.Count == 0) return null;
        var items = new List<FormulaItemSO> { previous != null ? previous : outputTemplate };
        var usedNodes = new List<bool[]> { new bool[OwnNodes(items[0]).Length] };
        var usedCapacity = new List<int> { 0 };
        var bonds = new List<SynthesisBondData>();
        int targetCount = 3 + random.Next(3) + (stage > 2 ? 1 : 0);
        while (items.Count < targetCount)
        {
            bool added = false;
            for (int attempt = 0; attempt < 120 && !added; attempt++)
            {
                int from = random.Next(items.Count);
                var candidate = choices[random.Next(choices.Count)];
                var own = OwnNodes(items[from]);
                var other = OwnNodes(candidate);
                int first = random.Next(own.Length), second = random.Next(other.Length);
                if (usedNodes[from][first] || !CanPair(items[from], own[first], candidate, other[second])) continue;
                int order = own[first].connectionType == ConnectionType.DoubleBond ? 2 : (random.Next(3) == 0 ? 2 : 1);
                if (usedCapacity[from] + order > items[from].connectionCapacity || order > candidate.connectionCapacity)
                {
                    if (own[first].connectionType == ConnectionType.DoubleBond || usedCapacity[from] + 1 > items[from].connectionCapacity || candidate.connectionCapacity < 1) continue;
                    order = 1;
                }
                usedNodes[from][first] = true;
                usedCapacity[from] += order;
                var candidateUsed = new bool[other.Length];
                candidateUsed[second] = true;
                bonds.Add(new SynthesisBondData { fromIndex = from, toIndex = items.Count, bondType = (BondType)order });
                items.Add(candidate);
                usedNodes.Add(candidateUsed);
                usedCapacity.Add(order);
                added = true;
            }
            if (!added) break;
        }
        if (items.Count < 3) return null;

        // Ek çevrim yalnız fiziksel olarak boş ve uyumlu uçlar varsa eklenir.
        if (random.Next(2) == 0)
        {
            for (int a = 0; a < items.Count; a++)
            {
                bool closed = false;
                for (int b = a + 1; b < items.Count && !closed; b++)
                {
                    if (bonds.Exists(edge => (edge.fromIndex == a && edge.toIndex == b) || (edge.fromIndex == b && edge.toIndex == a))) continue;
                    var own = OwnNodes(items[a]); var other = OwnNodes(items[b]);
                    for (int na = 0; na < own.Length && !closed; na++)
                        for (int nb = 0; nb < other.Length && !closed; nb++)
                        {
                            int order = own[na].connectionType == ConnectionType.DoubleBond ? 2 : 1;
                            if (usedNodes[a][na] || usedNodes[b][nb] || usedCapacity[a] + order > items[a].connectionCapacity || usedCapacity[b] + order > items[b].connectionCapacity || !CanPair(items[a], own[na], items[b], other[nb])) continue;
                            usedNodes[a][na] = usedNodes[b][nb] = true;
                            usedCapacity[a] += order; usedCapacity[b] += order;
                            bonds.Add(new SynthesisBondData { fromIndex = a, toIndex = b, bondType = (BondType)order });
                            closed = true;
                        }
                }
                if (closed) break;
            }
        }

        var quest = new SynthesisQuestData { questID = "run_" + seed + "_quest_" + stage, title = "Aşama " + (stage + 1) + " · " + output.itemName, outputItemID = output.itemID, bonds = bonds };
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        for (int i = 0; i < items.Count; i++)
        {
            float theta = angle + i * 2f * Mathf.PI / items.Count;
            quest.nodes.Add(new QuestNodeData { itemID = items[i].itemID, isUnknown = i != 0 && (i == 1 || random.Next(3) != 0), position = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * 0.7f });
        }
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items) { counts.TryGetValue(item.itemID, out int count); counts[item.itemID] = count + 1; }
        var ids = new List<string>(counts.Keys); ids.Sort(StringComparer.Ordinal);
        foreach (string id in ids)
            quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.ExactPartCount, itemID = id, amount = counts[id] });
        int doubles = 0, singles = 0, openEnds = 0;
        foreach (var bond in bonds) { if (bond.bondType == BondType.Double) doubles++; else singles++; }
        foreach (var used in usedNodes) foreach (bool occupied in used) if (!occupied) openEnds++;
        quest.rules.Add(new SynthesisRuleData { ruleType = doubles > 0 ? SynthesisRuleType.MinDoubleBonds : SynthesisRuleType.MaxSingleBonds, amount = doubles > 0 ? doubles : singles });
        quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.RequireNoBlockedEnds });
        quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.MaxOpenEnds, amount = openEnds });
        foreach (var item in formulaCatalog)
            if (item.isDecoy) { quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.ForbiddenPart, itemID = item.itemID }); break; }

        var solution = new SynthesisGraphData { bonds = bonds };
        for (int i = 0; i < items.Count; i++)
        {
            int free = 0; foreach (bool used in usedNodes[i]) if (!used) free++;
            solution.parts.Add(new SynthesisPartData { itemID = items[i].itemID, capacity = items[i].connectionCapacity, openEnds = free });
        }
        AddBridgeClue(quest, solution);
        if (!SynthesisGraphValidator.Validate(solution, quest, out _) || !CanRealizeQuest(quest)) return null;
        return quest;
    }

    private static void AddBridgeClue(SynthesisQuestData quest, SynthesisGraphData solution)
    {
        for (int mid = 0; mid < quest.nodes.Count; mid++)
            for (int a = 0; a < quest.nodes.Count; a++)
                for (int b = a + 1; b < quest.nodes.Count; b++)
                {
                    if (a == mid || b == mid || quest.nodes[a].itemID == quest.nodes[b].itemID) continue;
                    var rule = new SynthesisRuleData { ruleType = SynthesisRuleType.RequireBridge, itemID = quest.nodes[a].itemID, otherItemID = quest.nodes[b].itemID, bridgeItemID = quest.nodes[mid].itemID };
                    quest.rules.Add(rule);
                    if (SynthesisGraphValidator.Validate(solution, quest, out _)) return;
                    quest.rules.RemoveAt(quest.rules.Count - 1);
                }
    }

    // Doğrulama aracı da aynı gerçek prefab uç kısıtlarını sorgular; kapalı uçlar silinebildiğinden kullanılabilir kabul edilir.
    public bool CanRealizeQuest(SynthesisQuestData quest)
    {
        if (quest == null || quest.nodes == null || quest.bonds == null) return false;
        var items = new List<FormulaItemSO>();
        var used = new List<bool[]>();
        var capacity = new int[quest.nodes.Count];
        foreach (var node in quest.nodes)
        {
            var item = ResolveFormula(node.itemID);
            if (item == null || item.chalkPrefab == null) return false;
            items.Add(item);
            used.Add(new bool[OwnNodes(item).Length]);
        }
        return AssignEndpoints(0, quest.bonds, items, used, capacity);
    }

    private static bool AssignEndpoints(int index, List<SynthesisBondData> bonds, List<FormulaItemSO> items, List<bool[]> used, int[] capacity)
    {
        if (index == bonds.Count) return true;
        var edge = bonds[index];
        if (edge == null || edge.fromIndex < 0 || edge.fromIndex >= items.Count || edge.toIndex < 0 || edge.toIndex >= items.Count || edge.fromIndex == edge.toIndex) return false;
        int a = edge.fromIndex, b = edge.toIndex, order = (int)edge.bondType;
        if ((order != 1 && order != 2) || capacity[a] + order > items[a].connectionCapacity || capacity[b] + order > items[b].connectionCapacity) return false;
        var own = OwnNodes(items[a]); var other = OwnNodes(items[b]);
        for (int na = 0; na < own.Length; na++)
            for (int nb = 0; nb < other.Length; nb++)
            {
                if (used[a][na] || used[b][nb] || (own[na].connectionType == ConnectionType.DoubleBond && order != 2) || !CanPair(items[a], own[na], items[b], other[nb])) continue;
                used[a][na] = used[b][nb] = true;
                capacity[a] += order; capacity[b] += order;
                if (AssignEndpoints(index + 1, bonds, items, used, capacity)) return true;
                capacity[a] -= order; capacity[b] -= order;
                used[a][na] = used[b][nb] = false;
            }
        return false;
    }

    private static FormulaNode[] OwnNodes(FormulaItemSO item)
    {
        if (item == null || item.chalkPrefab == null) return new FormulaNode[0];
        var root = item.chalkPrefab.GetComponent<DraggableFormula>();
        if (root == null) return new FormulaNode[0];
        var nodes = new List<FormulaNode>();
        foreach (var node in item.chalkPrefab.GetComponentsInChildren<FormulaNode>(true))
            // Prefab assetleri activeSelf olsa bile sahnede olmadığından activeInHierarchy=false olur.
            if (node.GetComponentInParent<DraggableFormula>(true) == root) nodes.Add(node);
        return nodes.ToArray();
    }

    private static bool CanPair(FormulaItemSO a, FormulaNode aNode, FormulaItemSO b, FormulaNode bNode)
    {
        return aNode.connectionType == bNode.connectionType && Accepts(a, aNode, b) && Accepts(b, bNode, a);
    }

    private static bool Accepts(FormulaItemSO own, FormulaNode node, FormulaItemSO other)
    {
        if (own.useUniversalNodeRules || node.acceptAnyFormula || node.allowedFormulas == null || node.allowedFormulas.Count == 0) return true;
        foreach (var allowed in node.allowedFormulas)
            if (allowed != null && (allowed.itemID == other.itemID || allowed.itemID == other.connectionAliasID)) return true;
        return false;
    }
}
