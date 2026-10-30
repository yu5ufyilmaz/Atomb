using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public class ChalkboardManager : MonoBehaviour, ISaveable
{
    public static ChalkboardManager Instance { get; private set; }

    [Header("Tahta Referansları")]
    [Tooltip("Tahtanın merkez ve çalışma düzlemi")]
    public Transform boardTransform;
    [Tooltip("Tahta sınırları (yerel koordinatlar)")]
    public Vector2 minBounds = new Vector2(-1.5f, -1f);
    public Vector2 maxBounds = new Vector2(1.5f, 1f);
    [Header("Sentez Ayarları")]
    public ReactionZone synthesisZone;
    [Tooltip("Kitap ve prosedürel görevlerde kullanılabilecek parça kataloğu")]
    public List<FormulaItemSO> formulaCatalog = new List<FormulaItemSO>();

    [Tooltip("Atanırsa yalnız bu alıştırma doğrulanır; parçalar tüketilmez ve çıktı üretilmez.")]
    public QuestBlueprintSO practiceQuest;
    [Tooltip("Toplanan hedef sabitlenince seçilebilen alıştırmalar.")]
    public List<QuestBlueprintSO> practicePuzzles = new List<QuestBlueprintSO>();
    private ChalkboardFreehandDrawing freehand;
    [Header("Tahtaya Sabitlenen Bilgiler")]
    public List<NotebookBlockData> pinnedInformation = new List<NotebookBlockData>();
    private ChalkboardDocuments documents;
    public bool IndividualPartMovement => ResearchPuzzleTrial.IsResearch(this);

    public ChalkboardDocuments EnsureDocuments()
    {
        if (boardTransform == null) return null;
        if (documents == null) documents = GetComponent<ChalkboardDocuments>() ?? gameObject.AddComponent<ChalkboardDocuments>();
        documents.Initialize(this);
        return documents;
    }

    public bool TryPinInformation(NotebookBlockData note)
    {
        if (!isMachineActive || note == null || note.blockType != NotebookBlockType.RuleClue) return false;
        bool research = ResearchPuzzleTrial.IsResearch(this);
        bool linked = !research && PuzzleInformation.TryResolve(note, this);
        if (!research && PinnedQuest != null && (!linked || note.questID != PinnedQuest.questID))
        {
            Report(linked ? "Bu bilgi " + PuzzleInformation.Title(note.questID, this) + " hedefine ait. Önce o hedefi seç."
                : "Bu bilginin hangi hedefe ait olduğu henüz belirlenemedi.", false);
            return false;
        }
        foreach (var saved in pinnedInformation) PuzzleInformation.TryResolve(saved, this);
        if (!pinnedInformation.Exists(n => linked ? n.questID == note.questID && n.ruleIndex == note.ruleIndex
            : n.questID == note.questID && n.title == note.title && n.description == note.description))
            pinnedInformation.Add(note.Clone());
        EnsureDocuments()?.Refresh();
        Report(research ? "Araştırma kaydı panoya takıldı. İlgili olup olmadığını sen değerlendir." : "Bilgi doğru hedefe bağlandı.", true);
        return true;
    }

    public void RemoveInformation(NotebookBlockData note)
    {
        if (!isMachineActive || (GameManager.Instance != null && GameManager.Instance.isGamePaused)) return;
        pinnedInformation.Remove(note);
        documents?.Refresh();
    }

    public NotebookFreehandDrawer EnsureDrawing()
    {
        if (freehand == null) freehand = GetComponent<ChalkboardFreehandDrawing>() ?? gameObject.AddComponent<ChalkboardFreehandDrawing>();
        freehand.Initialize(this);
        return freehand.Drawer;
    }

    public bool isMachineActive { get; private set; }
    public SynthesisQuestData PinnedQuest { get; private set; }
    public string LastSynthesisMessage { get; private set; }
    public event Action<SynthesisQuestData> QuestPinned;
    public event Action<string, bool> SynthesisFeedback;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
    }

    private void Start()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
        AdoptSceneFormulas();
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.UnregisterSaveable(this);
    }

    public void SetMachineActive(bool state)
    {
        isMachineActive = state;
        if (state) EnsureDrawing();
        if (state) EnsureDocuments();
        else if (freehand != null && freehand.Drawer != null) freehand.Drawer.EndStroke();
        if (state) AdoptSceneFormulas();
        if (state) KeepFormulasInWorkingArea();
        if (NotebookUI.Instance != null) NotebookUI.Instance.SetWorkspaceMode(state);
    }

    private void AdoptSceneFormulas()
    {
        if (boardTransform == null) return;
        // Eski sahnelerde hazır parçalar boardTransform yerine tahta modelinin altındadır.
        foreach (DraggableFormula part in FindObjectsByType<DraggableFormula>(FindObjectsSortMode.None))
        {
            if (part.transform.IsChildOf(boardTransform) || part.GetRootFormula() != part) continue;
            Vector3 local = boardTransform.InverseTransformPoint(part.transform.position);
            if (local.x < minBounds.x - 0.15f || local.x > maxBounds.x + 0.15f
                || local.y < minBounds.y - 0.15f || local.y > maxBounds.y + 0.15f || Mathf.Abs(local.z) > 1f) continue;
            part.transform.SetParent(boardTransform, true);
            part.SyncMovementTarget();
        }
    }

    public bool TryPinQuest(SynthesisQuestData quest)
    {
        if (!isMachineActive || quest == null) return false;
        var practice = FindPracticeQuest(quest.questID);
        if (practiceQuest != null && practice == null) return false;
        if (practice != null) practiceQuest = practice;
        PinnedQuest = quest;
        EnsureDocuments()?.Refresh();
        QuestPinned?.Invoke(quest);
        return true;
    }

    public QuestBlueprintSO FindPracticeQuest(string id)
    {
        if (practiceQuest != null && practiceQuest.CreateQuest().questID == id) return practiceQuest;
        return practicePuzzles != null ? practicePuzzles.Find(q => q != null && q.CreateQuest().questID == id) : null;
    }

    public bool TryGetBoardPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = Camera.main;
        if (!isMachineActive || boardTransform == null || cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (!new Plane(boardTransform.forward, boardTransform.position).Raycast(ray, out float distance)) return false;
        Vector3 local = boardTransform.InverseTransformPoint(ray.GetPoint(distance));
        if (local.x < minBounds.x || local.x > maxBounds.x || local.y < minBounds.y || local.y > maxBounds.y) return false;
        local.z = 0.02f;
        point = boardTransform.TransformPoint(local);
        return true;
    }

    public DraggableFormula SpawnFormula(FormulaItemSO data, Vector2 screenPosition)
    {
        if (!TryGetBoardPoint(screenPosition, out Vector3 point)) return null;
        try { return CreateFormula(data, point, false); }
        catch (Exception exception)
        {
            Report("Parça tahtaya aktarılamadı: " + exception.Message, false);
            return null;
        }
    }

    private DraggableFormula CreateFormula(FormulaItemSO data, Vector3 point, bool synthesized)
    {
        if (!HasValidPrefab(data) || boardTransform == null) return null;
        Vector3 local = ClampToBoardArea(boardTransform.InverseTransformPoint(point));
        local.z = 0.02f;
        GameObject instance = Instantiate(data.chalkPrefab, boardTransform.TransformPoint(local), boardTransform.rotation, boardTransform);
        try
        {
            DraggableFormula formula = instance.GetComponent<DraggableFormula>();
            formula.Initialize(data);
            formula.IsSynthesized = synthesized;
            return formula;
        }
        catch
        {
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            throw;
        }
    }

    private static bool HasValidPrefab(FormulaItemSO data)
    {
        return data != null && data.chalkPrefab != null && data.chalkPrefab.GetComponent<DraggableFormula>() != null;
    }

    public Vector3 ClampToBoardArea(Vector3 localPos)
    {
        localPos.x = Mathf.Clamp(localPos.x, minBounds.x, WorkingMaxX);
        localPos.y = Mathf.Clamp(localPos.y, minBounds.y, maxBounds.y);
        return localPos;
    }

    public Vector3 ClampMoleculeToBoardArea(DraggableFormula root, Vector3 proposedLocalPos, float padding = 0.15f)
    {
        if (IndividualPartMovement)
        {
            proposedLocalPos.x = Mathf.Clamp(proposedLocalPos.x, minBounds.x + padding, WorkingMaxX - padding);
            proposedLocalPos.y = Mathf.Clamp(proposedLocalPos.y, minBounds.y + padding, maxBounds.y - padding);
            return proposedLocalPos;
        }
        if (root == null || boardTransform == null) return ClampToBoardArea(proposedLocalPos);
        Vector3 delta = proposedLocalPos - boardTransform.InverseTransformPoint(root.transform.position);
        Vector2 low = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 high = new Vector2(float.MinValue, float.MinValue);
        foreach (DraggableFormula part in root.GetConnectedParts())
        {
            Vector3 pos = boardTransform.InverseTransformPoint(part.transform.position) + delta;
            low = Vector2.Min(low, new Vector2(pos.x - padding, pos.y - padding));
            high = Vector2.Max(high, new Vector2(pos.x + padding, pos.y + padding));
        }
        if (low.x < minBounds.x) proposedLocalPos.x += minBounds.x - low.x;
        if (high.x > WorkingMaxX) proposedLocalPos.x -= high.x - WorkingMaxX;
        if (low.y < minBounds.y) proposedLocalPos.y += minBounds.y - low.y;
        if (high.y > maxBounds.y) proposedLocalPos.y -= high.y - maxBounds.y;
        return proposedLocalPos;
    }

    private float WorkingMaxX => documents != null ? documents.WorkingMaxX : maxBounds.x;

    private void KeepFormulasInWorkingArea()
    {
        if (boardTransform == null) return;
        foreach (var part in boardTransform.GetComponentsInChildren<DraggableFormula>())
        {
            if (part.GetRootFormula() != part) continue;
            if (!part.CanMoveOnBoard) continue;
            Vector3 current = boardTransform.InverseTransformPoint(part.transform.position);
            Vector3 bounded = ClampMoleculeToBoardArea(part, current);
            part.transform.position += boardTransform.TransformVector(bounded - current);
            part.SyncMovementTarget();
        }
    }

    // Transform yalnızca taşıma grubudur. Grup sahipliğini her zaman gerçek bağlardan yeniden kur.
    public void RebuildFormulaGroups()
    {
        if (boardTransform == null) return;
        DraggableFormula[] parts = boardTransform.GetComponentsInChildren<DraggableFormula>(true);
        HashSet<DraggableFormula> onBoard = new HashSet<DraggableFormula>(parts);
        foreach (DraggableFormula part in parts)
        {
            part.transform.SetParent(boardTransform, true);
            part.SyncMovementTarget();
        }
        if (IndividualPartMovement) return;
        HashSet<DraggableFormula> grouped = new HashSet<DraggableFormula>();
        foreach (DraggableFormula root in parts)
        {
            if (!grouped.Add(root)) continue;
            foreach (DraggableFormula member in root.GetConnectedParts())
            {
                if (member == root || !onBoard.Contains(member)) continue;
                grouped.Add(member);
                member.transform.SetParent(root.transform, true);
                member.SyncMovementTarget();
            }
        }
    }

    public SynthesisGraphData BuildGraph(DraggableFormula root)
    {
        SynthesisGraphData graph = new SynthesisGraphData();
        if (root == null) return graph;
        List<DraggableFormula> parts = root.GetConnectedParts();
        Dictionary<DraggableFormula, int> indices = new Dictionary<DraggableFormula, int>();
        foreach (DraggableFormula part in parts)
        {
            indices.Add(part, graph.parts.Count);
            int open = 0;
            foreach (FormulaNode node in part.GetOwnNodes())
            {
                if (!node.isOccupied) open++;
            }
            graph.parts.Add(new SynthesisPartData
            {
                itemID = part.formulaData != null ? part.formulaData.itemID : string.Empty,
                capacity = part.ConnectionCapacity, openEnds = open, blockedEnds = 0,
            });
        }
        HashSet<FormulaNode> visited = new HashSet<FormulaNode>();
        foreach (DraggableFormula part in parts)
            foreach (FormulaNode node in part.GetOwnNodes())
            {
                if (node.connectedNode == null || visited.Contains(node)) continue;
                FormulaNode other = node.connectedNode;
                if (other.connectedNode != node || other.parentFormula == null || !indices.ContainsKey(other.parentFormula)
                    || node.bondType != other.bondType)
                {
                    // Bozuk bir ucu sessizce yok saymak geçersiz grafiği geçerli hale getirmemeli.
                    graph.bonds.Add(new SynthesisBondData { fromIndex = -1, toIndex = -1 });
                    continue;
                }
                visited.Add(node);
                visited.Add(other);
                graph.bonds.Add(new SynthesisBondData
                {
                    fromIndex = node.GetComponent<SignalPort>() is SignalPort port && !port.output ? indices[other.parentFormula] : indices[part],
                    toIndex = node.GetComponent<SignalPort>() is SignalPort input && !input.output ? indices[part] : indices[other.parentFormula],
                    directed = node.GetComponent<SignalPort>() != null,
                    bondType = node.bondType,
                });
            }
        return graph;
    }

    [Button("Deneyi Ölç")]
    public void AttemptSynthesis()
    {
        var zone = synthesisZone;
        if (!isMachineActive || zone == null || boardTransform == null)
        { Report("Deney alanı hazır değil.", false); return; }
        if (PinnedQuest == null || SignalExperiment.Active == null || PinnedQuest.questID != SignalExperiment.Active.CurrentQuestID)
        { Report("Önce defterindeki HEDEF notunu tahtaya sürükle.", false); return; }
        zone.RefreshItems();
        var roots = new HashSet<DraggableFormula>();
        var visited = new HashSet<DraggableFormula>();
        foreach (var part in zone.currentItems)
        {
            if (part == null || !visited.Add(part)) continue;
            roots.Add(part.GetRootFormula());
            foreach (var connected in part.GetConnectedParts()) visited.Add(connected);
        }
        if (roots.Count == 0) { Report("Deney alanı boş.", false); return; }
        // Bağlanmamış parçalar deneye girmez; başlatıcıdan sonlandırıcıya uzanan en büyük yapı ölçülür.
        DraggableFormula measured = null;
        int bestScore = -1;
        foreach (var root in roots)
        {
            var structure = root.GetConnectedParts();
            int score = structure.Count + (IsCompleteStructure(structure) ? PuzzleSimulation.MaxParts : 0);
            if (score > bestScore) { bestScore = score; measured = root; }
        }
        foreach (var part in measured.GetConnectedParts())
            if (!zone.ContainsFormula(part))
            { Report("Yapının tamamını tahta alanına yerleştir.", false); return; }
        if (SignalExperiment.Active == null || !ResearchPuzzleTrial.IsResearch(this))
        { Report("Deney bağlantıları hazır değil.", false); return; }
        SignalExperiment.Active.Measure(BuildGraph(measured));
    }

    private static bool IsCompleteStructure(List<DraggableFormula> structure)
    {
        bool starts = false, ends = false;
        foreach (var part in structure)
        {
            var info = PuzzleParts.Find(part.formulaData != null ? part.formulaData.itemID : null);
            if (info == null) continue;
            starts |= info.inputs == 0;
            ends |= info.outputs == 0;
        }
        return starts && ends;
    }

    private void Report(string message, bool success)
    {
        LastSynthesisMessage = message;
        if (success) Debug.Log(message); else Debug.LogWarning(message);
        SynthesisFeedback?.Invoke(message, success);
    }

    public void RemoveFormula(DraggableFormula formula)
    {
        if (formula == null) return;
        formula.BreakAllBonds();
        if (synthesisZone != null) synthesisZone.currentItems.Remove(formula);
        formula.gameObject.SetActive(false);
        Destroy(formula.gameObject);
    }

    public void SaveData(ref GameData data)
    {
        if (boardTransform == null) return;
        if (data == null) data = new GameData();
        var snapshot = new SynthesisBoardSaveData { pinnedQuestID = PinnedQuest != null ? PinnedQuest.questID : null };
        foreach (var note in pinnedInformation) if (note != null) snapshot.information.Add(note.Clone());
        documents?.SaveLayout(snapshot);
        if (freehand != null && freehand.Drawer != null)
            foreach (var stroke in freehand.Drawer.Strokes) snapshot.strokes.Add(stroke.Clone());
        DraggableFormula[] parts = boardTransform.GetComponentsInChildren<DraggableFormula>();
        var indices = new Dictionary<DraggableFormula, int>();
        for (int i = 0; i < parts.Length; i++)
        {
            DraggableFormula part = parts[i];
            indices.Add(part, i);
            var saved = new SynthesisBoardPartSaveData
            {
                itemID = part.formulaData != null ? part.formulaData.itemID : string.Empty,
                localPosition = boardTransform.InverseTransformPoint(part.transform.position),
                localRotation = Quaternion.Inverse(boardTransform.rotation) * part.transform.rotation,
                synthesized = part.IsSynthesized,
            };
            foreach (FormulaNode node in part.GetOwnNodes()) saved.nodeStates.Add(FormulaNodeState.Open);
            snapshot.parts.Add(saved);
        }
        var visited = new HashSet<FormulaNode>();
        for (int i = 0; i < parts.Length; i++)
        {
            FormulaNode[] nodes = parts[i].GetOwnNodes();
            for (int n = 0; n < nodes.Length; n++)
            {
                FormulaNode node = nodes[n], other = node.connectedNode;
                if (other == null || visited.Contains(node) || other.parentFormula == null
                    || !indices.TryGetValue(other.parentFormula, out int toPart)) continue;
                visited.Add(node);
                visited.Add(other);
                snapshot.bonds.Add(new SynthesisBoardBondSaveData
                {
                    fromPart = i, fromNode = n, toPart = toPart,
                    toNode = Array.IndexOf(other.parentFormula.GetOwnNodes(), other), bondType = node.bondType,
                });
            }
        }
        data.savedChalkboard = snapshot;
    }

    public void LoadData(GameData data)
    {
        if (data == null || data.savedChalkboard == null || boardTransform == null) return;
        SynthesisBoardSaveData snapshot = data.savedChalkboard;
        if (snapshot.parts == null || snapshot.bonds == null) return;
        FormulaCatalog generator = FormulaCatalog.Instance;
        if (generator != null) generator.RegisterCatalog(formulaCatalog);

        DraggableFormula[] oldParts = boardTransform.GetComponentsInChildren<DraggableFormula>(true);
        var definitions = new List<FormulaItemSO>();
        foreach (SynthesisBoardPartSaveData saved in snapshot.parts)
        {
            FormulaItemSO item = saved != null ? ResolveSavedFormula(saved.itemID, generator, oldParts) : null;
            if (!HasValidPrefab(item)) { Report("Tahta kaydındaki parça bulunamadı; mevcut tahta korundu.", false); return; }
            definitions.Add(item);
        }

        var restored = new List<DraggableFormula>();
        try
        {
            for (int i = 0; i < snapshot.parts.Count; i++)
            {
                SynthesisBoardPartSaveData saved = snapshot.parts[i];
                DraggableFormula formula = CreateFormula(definitions[i], boardTransform.TransformPoint(saved.localPosition), saved.synthesized);
                restored.Add(formula);
                formula.transform.SetPositionAndRotation(boardTransform.TransformPoint(saved.localPosition), boardTransform.rotation * saved.localRotation);
                formula.SyncMovementTarget();
                FormulaNode[] nodes = formula.GetOwnNodes();
                if (saved.nodeStates == null || saved.nodeStates.Count != nodes.Length)
                    throw new InvalidOperationException("Kayıttaki parça uçları prefab ile uyuşmuyor.");
                for (int n = 0; n < nodes.Length; n++) nodes[n].nodeState = FormulaNodeState.Open;
            }
            foreach (SynthesisBoardBondSaveData bond in snapshot.bonds)
            {
                if (bond == null || bond.fromPart < 0 || bond.fromPart >= restored.Count
                    || bond.toPart < 0 || bond.toPart >= restored.Count || bond.fromPart == bond.toPart)
                    throw new InvalidOperationException("Kayıttaki bağın parça indeksi geçersiz.");
                FormulaNode[] fromNodes = restored[bond.fromPart].GetOwnNodes(), toNodes = restored[bond.toPart].GetOwnNodes();
                if (bond.fromNode < 0 || bond.fromNode >= fromNodes.Length || bond.toNode < 0 || bond.toNode >= toNodes.Length
                    || (bond.bondType != BondType.Single && bond.bondType != BondType.Double))
                    throw new InvalidOperationException("Kayıttaki bağın uç indeksi veya türü geçersiz.");
                FormulaNode from = fromNodes[bond.fromNode], to = toNodes[bond.toNode];
                if (!from.CanRestoreConnection(to, bond.bondType))
                    throw new InvalidOperationException("Kayıttaki bağ uç tipi, parça izni veya kapasite kuralına uymuyor.");
                from.connectedNode = to;
                to.connectedNode = from;
                from.bondType = to.bondType = bond.bondType;
                from.isLineOwner = true;
                to.isLineOwner = false;
            }
        }
        catch (Exception exception)
        {
            DestroyParts(restored);
            Report("Tahta yüklenemedi; mevcut tahta korundu: " + exception.Message, false);
            return;
        }
        DestroyParts(oldParts);
        EnsureDrawing()?.SetStrokes(snapshot.strokes);
        RebuildFormulaGroups();
        if (synthesisZone != null) synthesisZone.RefreshItems();
        var practice = FindPracticeQuest(snapshot.pinnedQuestID);
        if (practice != null) practiceQuest = practice;
        PinnedQuest = practice != null ? practice.CreateQuest() : null;
        pinnedInformation = snapshot.information != null
            ? snapshot.information.FindAll(n => n != null && n.blockType == NotebookBlockType.RuleClue).ConvertAll(n => n.Clone())
            : new List<NotebookBlockData>();
        var loadedDocuments = EnsureDocuments();
        loadedDocuments?.LoadLayout(snapshot);
        loadedDocuments?.Refresh();
        if (isMachineActive) KeepFormulasInWorkingArea();
        if (synthesisZone != null) synthesisZone.RefreshItems();
        QuestPinned?.Invoke(PinnedQuest);
    }

    private FormulaItemSO ResolveSavedFormula(string itemID, FormulaCatalog generator, DraggableFormula[] current)
    {
        FormulaItemSO item = generator != null ? generator.ResolveFormula(itemID) : null;
        if (item != null) return item;
        foreach (FormulaItemSO candidate in formulaCatalog) if (candidate != null && candidate.itemID == itemID) return candidate;
        foreach (DraggableFormula part in current) if (part.formulaData != null && part.formulaData.itemID == itemID) return part.formulaData;
        return null;
    }

    public void ResetBoard()
    {
        if (boardTransform != null) DestroyParts(boardTransform.GetComponentsInChildren<DraggableFormula>(true));
        if (freehand != null && freehand.Drawer != null) freehand.Drawer.Clear();
        if (synthesisZone != null) synthesisZone.currentItems.Clear();
        PinnedQuest = null;
        LastSynthesisMessage = string.Empty;
        pinnedInformation.Clear();
        documents?.Refresh();
        QuestPinned?.Invoke(null);
    }

    private static void DestroyParts(IEnumerable<DraggableFormula> parts)
    {
        var roots = new HashSet<DraggableFormula>();
        foreach (DraggableFormula part in parts)
        {
            if (part == null) continue;
            roots.Add(part.GetRootFormula());
            part.gameObject.SetActive(false);
        }
        foreach (DraggableFormula root in roots)
        {
            root.transform.SetParent(null, true);
            if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        if (boardTransform == null) return;
        Gizmos.matrix = boardTransform.localToWorldMatrix;
        Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
        Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0.01f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, size);
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}
