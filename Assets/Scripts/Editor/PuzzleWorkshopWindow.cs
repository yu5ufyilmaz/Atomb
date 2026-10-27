using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class PuzzleWorkshopWindow : EditorWindow
{
    [SerializeField] private PuzzleDefinitionSO document, sourceAsset;
    [SerializeField] private string transientJson, cleanJson, draftID;
    [SerializeField] private int generationSeed = 271026, assignmentSeed = 100, tab;
    [SerializeField] private bool fixedSeed, advanced;
    private Vector2 settingsScroll, graphScroll, notesScroll, homeScroll;
    private int selectedNode = -1, connectFrom = -1, selectedRule;
    private string search = "", message = "Hazır bir örnek seçebilir, rastgele puzzle üretebilir veya kendi şemanı kurabilirsin.";
    private MessageType messageType = MessageType.Info;
    private const float NodeWidth = 180, NodeHeight = 105;
    private float graphZoom = .8f;
    private float graphMinimumZoom = .35f;
    private bool fitGraph;
    private PuzzleDefinition Puzzle => document.definition;
    public bool HasDraftChanges => document != null && JsonUtility.ToJson(document) != cleanJson;

    public static PuzzleWorkshopWindow OpenWindow()
    {
        var window = GetWindow<PuzzleWorkshopWindow>("Puzzle Atölyesi");
        window.minSize = new Vector2(900, 640); window.fitGraph = true; window.Show(); return window;
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed -= OnUndo;
        Undo.undoRedoPerformed += OnUndo;
        LoadDraftStore();
        if (document != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(document))) { var asset = document; document = null; Select(asset); }
        else if (document == null)
        {
            if (!string.IsNullOrEmpty(transientJson))
            {
                document = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); document.hideFlags = HideFlags.HideAndDontSave;
                JsonUtility.FromJsonOverwrite(transientJson, document);
            }
            else if (drafts.entries.Count > 0) RecoverDraft(drafts.entries.Count - 1);
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(PuzzleWorkshopSetup.Folder + "/MorKarisim.asset");
                if (asset == null) asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(PuzzleWorkshopSetup.LegacyPath);
                if (asset != null) Select(asset); else NewDocument();
                tab = 0;
            }
        }
    }
    private void OnDisable() { RememberDraft(true); Undo.undoRedoPerformed -= OnUndo; }
    private void OnUndo() { RememberDraft(); Repaint(); }
    private void Mutate(string label, Action action)
    {
        Undo.RecordObject(document, label); action(); EditorUtility.SetDirty(document); RememberDraft(); Repaint();
    }

    private void OnGUI()
    {
        if (document == null) OnEnable();
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("PUZZLE ATÖLYESİ", new GUIStyle(EditorStyles.boldLabel) { fontSize = 21 }, GUILayout.Height(30));
        GUILayout.Label(Puzzle.kind == PuzzleKind.ColorMixture ? "RENK VE KARIŞIM" : "SİNYAL DENEYİ", EditorStyles.boldLabel, GUILayout.Width(170));
        EditorGUILayout.EndHorizontal();
        Toolbar();
        tab = GUILayout.Toolbar(tab, new[] { "Başlangıç", "Şema", "Kurallar", "Kitaplar", "Kaydet ve Dene" }, GUILayout.Height(32));
        EditorGUILayout.Space(5);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Oynanış denemesi açık. Düzenlemeye dönmek için Play'i durdur. Taslağın korunur.", MessageType.Info);
        else EditorGUILayout.HelpBox(message, messageType);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            switch (tab)
            {
                case 0: Home(); break;
                case 1:
                    EditorGUILayout.BeginHorizontal(); Palette();
                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true)); Graph(); Target(); EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal(); break;
                case 2: Settings(); break;
                case 3: Notes(); break;
                default: SavePage(); break;
            }
        }
    }

    private void Toolbar()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Yeni", EditorStyles.toolbarButton, GUILayout.Width(50))) { NewDocument(); tab = 0; }
            if (GUILayout.Button("Aç", EditorStyles.toolbarButton, GUILayout.Width(45))) OpenPicker();
            var picked = (PuzzleDefinitionSO)EditorGUILayout.ObjectField(sourceAsset, typeof(PuzzleDefinitionSO), false, GUILayout.MinWidth(100));
            if (picked != sourceAsset && picked != null) Select(picked);
            GUILayout.Label(HasDraftChanges ? "Kaydedilmemiş taslak · otomatik korunur" : "Kaydedildi", EditorStyles.miniLabel);
            if (GUILayout.Button("Kaydet", EditorStyles.toolbarButton, GUILayout.Width(60))) Save(false);
            if (GUILayout.Button("Kopyasını Kaydet", EditorStyles.toolbarButton, GUILayout.Width(115))) Save(true);
            if (GUILayout.Button("Oyunda Dene", EditorStyles.toolbarButton, GUILayout.Width(95)))
                Try(() => { if (Save(false)) PuzzleWorkshopSetup.Preview(sourceAsset); });
            EditorGUILayout.EndHorizontal();
        }
    }

    private void Home()
    {
        homeScroll = EditorGUILayout.BeginScrollView(homeScroll);
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Nasıl başlamak istersin?", new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 }, GUILayout.Height(28));
        EditorGUILayout.BeginHorizontal();
        FamilyCard(PuzzleKind.ColorMixture, "Renk ve karışım", "Pigmentleri birleştir; seyrelt veya yoğunlaştır. Hedef rengi, yoğunluğu ve miktarı üret.", "MorKarisim.asset");
        FamilyCard(PuzzleKind.Signal, "Sinyal deneyi", "Numuneleri ve işlem parçalarını bağla. Hedefteki izlerin zamanını, yüksekliğini ve genişliğini üret.", "CiftKayit.asset");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("1. Örnekle başla veya rastgele üret.  2. Şemada parçaları düzenle.  3. Hedefi şemadan al.  4. Doğrula, kaydet ve oyunda dene.\nKurallar ve kitap notları ayrı sekmelerde; ilk denemede hepsini ayarlaman gerekmez.", MessageType.None);
        EditorGUILayout.LabelField("Hazır puzzlelar", EditorStyles.boldLabel);
        var pool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PuzzleWorkshopSetup.PoolPath);
        if (pool != null) foreach (var asset in pool.puzzles)
        {
            if (asset == null) continue;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(asset.definition.title + "  ·  " + (asset.definition.kind == PuzzleKind.ColorMixture ? "Renk" : "Sinyal"));
            if (GUILayout.Button("Düzenle", GUILayout.Width(85))) Select(asset);
            EditorGUILayout.EndHorizontal();
        }
        if (drafts.entries.Count > 0)
        {
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("Korunan taslaklar", EditorStyles.boldLabel);
            for (int i = drafts.entries.Count - 1; i >= 0; i--)
            {
                var entry = drafts.entries[i]; EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(entry.title, EditorStyles.wordWrappedLabel);
                bool recover = GUILayout.Button("Devam Et", GUILayout.Width(85));
                EditorGUILayout.EndHorizontal();
                if (recover) { RecoverDraft(i); break; }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void FamilyCard(PuzzleKind kind, string title, string body, string sample)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(190));
        EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 }, GUILayout.Height(25));
        EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(45));
        if (GUILayout.Button("Hazır örneği aç", GUILayout.Height(28))) Select(AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(PuzzleWorkshopSetup.Folder + "/" + sample));
        if (GUILayout.Button("Rastgele oluştur", GUILayout.Height(30))) { CreateForKind(kind); Generate(); }
        if (GUILayout.Button("Kendim kuracağım", GUILayout.Height(28))) { CreateForKind(kind); AddPart(PuzzleParts.Source(kind)); AddPart(PuzzleParts.Meter(kind)); tab = 1; fitGraph = true; }
        EditorGUILayout.EndVertical();
    }

    private void Palette()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(240));
        EditorGUILayout.LabelField("Parça ekle", EditorStyles.boldLabel);
        search = EditorGUILayout.TextField("Ara", search);
        settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
        foreach (var part in PuzzleParts.ForKind(Puzzle.kind))
        {
            if (!Puzzle.rules.Allows(part.id) || (!string.IsNullOrEmpty(search) && part.title.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) < 0)) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(part.title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(part.description, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Şemaya Ekle", GUILayout.Height(25))) AddPart(part.id);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("Tek giriş/çıkışlı parça seçiliyken eklediğin parça onun arkasına bağlanır. Diğer bağlar: ok → giriş.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void Settings()
    {
        settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
        EditorGUILayout.LabelField("Üretim ve çözüm kuralları", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("İzinli parçalar hem üreticiyi hem oyuncunun çözümünü sınırlar. Kaynak ve ölçer daima birer adettir.", MessageType.None);
        var rules = Puzzle.rules;
        EditorGUI.BeginChangeCheck();
        int minParts = EditorGUILayout.IntField("En az parça", rules.minParts), maxParts = EditorGUILayout.IntField("En fazla parça", rules.maxParts);
        int minSamples = EditorGUILayout.IntField(Puzzle.kind == PuzzleKind.ColorMixture ? "En az pigment" : "En az numune", rules.minSamples);
        int maxSamples = EditorGUILayout.IntField(Puzzle.kind == PuzzleKind.ColorMixture ? "En fazla pigment" : "En fazla numune", rules.maxSamples);
        int traces = Puzzle.kind == PuzzleKind.Signal ? EditorGUILayout.IntPopup("Hedef iz sayısı", rules.traceCount, new[] { "1 iz", "2 iz" }, new[] { 1, 2 }) : rules.traceCount;
        if (EditorGUI.EndChangeCheck()) Mutate("Üretim kuralları", () => { rules.minParts = minParts; rules.maxParts = maxParts; rules.minSamples = minSamples; rules.maxSamples = maxSamples; rules.traceCount = traces; });
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal(); GUILayout.Label("Kullanılabilecek parça", EditorStyles.boldLabel); GUILayout.Label("En az", GUILayout.Width(65)); GUILayout.Label("En çok", GUILayout.Width(65)); EditorGUILayout.EndHorizontal();
        foreach (var rule in rules.parts)
        {
            var part = PuzzleParts.Find(rule.itemID); if (part == null) continue;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox); EditorGUI.BeginChangeCheck();
            bool locked = part.inputs == 0 || part.outputs == 0;
            bool allowed; int min, max;
            using (new EditorGUI.DisabledScope(locked))
            {
                allowed = GUILayout.Toggle(rule.allowed, part.title, GUILayout.MinWidth(230));
                min = EditorGUILayout.IntField(rule.minimum, GUILayout.Width(65)); max = EditorGUILayout.IntField(rule.maximum, GUILayout.Width(65));
            }
            if (EditorGUI.EndChangeCheck()) Mutate("Parça sınırları", () => { rule.allowed = allowed; rule.minimum = min; rule.maximum = max; });
            if (GUILayout.Button("Kitap açıklaması", GUILayout.Width(120))) selectedRule = rules.parts.IndexOf(rule);
            EditorGUILayout.EndHorizontal();
        }
        selectedRule = Mathf.Clamp(selectedRule, 0, rules.parts.Count - 1);
        if (rules.parts.Count > 0)
        {
            var rule = rules.parts[selectedRule]; var part = PuzzleParts.Find(rule.itemID);
            EditorGUILayout.LabelField(part?.title ?? "Parça", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(part?.description ?? "", EditorStyles.wordWrappedLabel);
            EditorGUI.BeginChangeCheck(); string explanation = EditorGUILayout.TextArea(rule.explanation ?? "", GUILayout.MinHeight(45));
            if (EditorGUI.EndChangeCheck()) Mutate("Kitap açıklaması", () => rule.explanation = explanation);
        }
        advanced = EditorGUILayout.Foldout(advanced, "Gelişmiş: derinlik, tolerans ve seed", true);
        if (advanced)
        {
            EditorGUI.BeginChangeCheck(); int depth = EditorGUILayout.IntField("En fazla hat derinliği", rules.maxDepth);
            float tolerance = EditorGUILayout.FloatField("Karşılaştırma toleransı", Puzzle.tolerance);
            if (EditorGUI.EndChangeCheck()) Mutate("Gelişmiş kurallar", () => { rules.maxDepth = depth; Puzzle.tolerance = tolerance; });
            fixedSeed = EditorGUILayout.Toggle("Seed'i sabitle", fixedSeed); generationSeed = EditorGUILayout.IntField("Üretim seed'i", generationSeed);
        }
        if (GUILayout.Button("Bu Kurallarla Rastgele Üret", GUILayout.Height(34))) Generate();
        EditorGUILayout.EndScrollView();
    }

    private void SavePage()
    {
        EditorGUILayout.LabelField("Puzzleı kaydet ve dene", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck(); string title = EditorGUILayout.TextField("Puzzle adı", Puzzle.title);
        EditorGUILayout.LabelField("Oyuncunun okuyacağı hedef açıklaması");
        string description = EditorGUILayout.TextArea(Puzzle.description, GUILayout.Height(70));
        if (EditorGUI.EndChangeCheck()) Mutate("Puzzle bilgileri", () => { Puzzle.title = title; Puzzle.description = description; });
        string issue = PuzzleSimulation.Validate(Puzzle);
        EditorGUILayout.HelpBox(issue ?? "Örnek çözüm bütün koşulları ve hedefi karşılıyor.", issue == null ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Taslak Olarak Kaydet", GUILayout.Height(35))) { Mutate("Taslak", () => document.includeInPool = false); Save(false); }
        using (new EditorGUI.DisabledScope(issue != null))
        {
            if (GUILayout.Button("Kaydet ve Havuzda Kullan", GUILayout.Height(35))) { Mutate("Havuzda etkinleştir", () => document.includeInPool = true); Save(false); }
            if (GUILayout.Button("Oyunda Dene", GUILayout.Height(35))) Try(() => { if (Save(false)) PuzzleWorkshopSetup.Preview(sourceAsset); });
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Konum: " + (sourceAsset != null ? AssetDatabase.GetAssetPath(sourceAsset) : "Kaydedince proje içindeki Puzzles klasöründe oluşturulur."), EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.HelpBox("Yeni oyun etkin havuzdan seçim yapar. Devam Et, kayıtlı puzzleı ve kitap dağılımını geri getirir. Taslaklar havuzdan seçilmez.", MessageType.None);
    }

    private void Generate()
    {
        if (!fixedSeed) generationSeed = Guid.NewGuid().GetHashCode();
        if (!PuzzleGenerator.TryGenerate(Puzzle, generationSeed, out var generated, out string error)) { SetMessage(error, MessageType.Warning); return; }
        Mutate("Rastgele puzzle üret", () => { document.definition = generated; selectedNode = connectFrom = -1; graphScroll = Vector2.zero; });
        tab = 1; fitGraph = true; ValidateDocument();
    }
    private void ValidateDocument()
    {
        string error = PuzzleSimulation.Validate(Puzzle);
        SetMessage(error ?? "Geçerli: örnek çözüm hedefi karşılıyor. Düzenleyebilir, kaydedebilir veya oynayabilirsin.", error == null ? MessageType.Info : MessageType.Warning);
    }
    private void SetMessage(string text, MessageType type) { message = text; messageType = type; Repaint(); }
    private void Try(Action action) { try { action(); } catch (Exception e) { SetMessage(e.Message, MessageType.Error); } }
}
