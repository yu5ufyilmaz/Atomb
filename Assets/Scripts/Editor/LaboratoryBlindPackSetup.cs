using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LaboratoryBlindPackSetup
{
    public const string Folder = PuzzleWorkshopSetup.Folder + "/BlindLaboratory";
    public const string SetPath = Folder + "/ArchiveSet.asset";

    [MenuItem("Senzora/Puzzle Atölyesi/Laboratuvarda Üçlü Kör Denemeyi Kur")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Önce Play'i durdur.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(PuzzleWorkshopSetup.Folder, "BlindLaboratory");
        var definitions = CreateDefinitions();
        var set = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<PuzzlePoolSO>();
            for (int i = 0; i < definitions.Count; i++)
            {
                string path = Folder + "/Archive" + (i + 1).ToString("D2") + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); asset.definition = definitions[i];
                    AssetDatabase.CreateAsset(asset, path);
                }
                set.puzzles.Add(asset);
            }
            AssetDatabase.CreateAsset(set, SetPath);
        }
        var playable = set.Playable(out string error);
        if (error != null || playable.Count != 3) throw new InvalidOperationException(error ?? "Deneme kümesi üç hedef içermeli.");
        var scene = EditorSceneManager.OpenScene(PuzzleWorkshopSetup.LabPath);
        var trial = PuzzleWorkshopSetup.FindTrial(scene);
        if (trial == null) throw new InvalidOperationException("Laboratuvar araştırma sistemi eksik.");
        var books = trial.books.Where(b => b != null && b.isActiveAndEnabled).ToList();
        foreach (var book in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InteractableBook>()))
            if (book.isActiveAndEnabled && !books.Contains(book)) books.Add(book);
        if (books.Count < 3) throw new InvalidOperationException("Üç hedef için en az üç erişilebilir kitap gerekli.");
        trial.books = books.ToArray(); trial.simultaneousPuzzles = set; trial.previewPuzzle = null; trial.allowGameplayScene = true;
        foreach (var book in books)
        {
            book.usePhysicalPageBlocks = true; book.canContainPassword = false; book.clueData = null;
            if (book.GetComponent<BookPageBlockSurface>() == null) book.gameObject.AddComponent<BookPageBlockSurface>();
            EditorUtility.SetDirty(book);
        }
        PuzzleWorkshopSetup.AssignBookIDs(trial);
        if (!ResearchTrialBookTitles.Apply(trial)) throw new InvalidOperationException("Kitap başlıkları fiziksel kapaklara yerleştirilemedi.");
        foreach (var save in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)))
        { save.sessionOnly = true; EditorUtility.SetDirty(save); }
        EditorUtility.SetDirty(trial); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssetIfDirty(set);
        LaboratoryBlindPackChecks.Model();
        Debug.Log("LAB BLIND PACK READY: 3 validated targets, all " + books.Count + " accessible books, no preview/solution pinned, normal save isolated.");
    }

    private static List<PuzzleDefinition> CreateDefinitions()
    {
        var a = Definition(PuzzleKind.ColorMixture, "archive-a17", "Kayıt A-17", "Koruyucu kaplamanın arşiv numunesi bulundu. Analizörde bu referansın rengini, yoğunluğunu ve miktarını yeniden elde et.",
            ColorMixtureLibrary.Source, ColorMixtureLibrary.Meter, ColorMixtureLibrary.Blue, ColorMixtureLibrary.Green,
            ColorMixtureLibrary.Red, ColorMixtureLibrary.Concentrator, ColorMixtureLibrary.Solvent);
        Chain(a, ColorMixtureLibrary.Source, ColorMixtureLibrary.Blue, ColorMixtureLibrary.Green, ColorMixtureLibrary.Concentrator, ColorMixtureLibrary.Solvent, ColorMixtureLibrary.Meter);
        Observe(a, "İşlem günlüğü", "Yoğunlaştırma yalnız renksiz taşıyıcıyı azaltır. Çözücü yeniden taşıyıcı ekler. Aynı iki işlemi farklı sırada deneyince farklı numuneler elde edildi.");
        Observe(a, "Referansın kullanımı", "Benzer renk tek başına yeterli değildir. Referans numunenin yoğunluğunu ve kapta kalan miktarı da karşılaştır; küçük denemelerle ilerle.");

        var b = Definition(PuzzleKind.Signal, "archive-b29", "Kayıt B-29", "Uyarı devresinin eski kaydını yeniden üret. Kaydın iki tepkisinin biçimini, gücünü ve aralarındaki aralığı gözlemle.",
            SignalTrial.Source, SignalTrial.Meter, SignalTrial.Splitter, SignalTrial.Delay, SignalTrial.Attenuator,
            SignalTrial.Narrow, SignalTrial.Wide, "pulse-n08");
        foreach (string id in new[] { SignalTrial.Source, SignalTrial.Splitter, "pulse-n08", SignalTrial.Delay, SignalTrial.Wide, SignalTrial.Meter }) Add(b, id);
        Edge(b, 0, 1); Edge(b, 1, 2); Edge(b, 2, 5); Edge(b, 1, 3); Edge(b, 3, 4); Edge(b, 4, 5);
        Observe(b, "İletim gözlemi", "Bir işlem, kendi içinden geçen tepkileri değiştirir. Başka bir hatta ilerleyen tepki, bu işlemden etkilenmez.");
        Observe(b, "Kayıt karşılaştırması", "Önce her numunenin tek başına bıraktığı izi tanı. Sonra eski kayıttaki tepkilerle karşılaştır; hedefin çizdiği bağlantıyı değil, davranışını yeniden oluştur.");

        var c = Definition(PuzzleKind.Signal, "archive-c43", "Kayıt C-43", "Uzak istasyondan kalan bu kaydı geri getir. Referansın başlangıcını, tepki sayısını ve izlerin biçimini birlikte incele.",
            SignalTrial.Source, SignalTrial.Meter, "pulse-n21", "pulse-n46", SignalTrial.Wide, SignalTrial.Narrow, SignalTrial.Delay, SignalTrial.Attenuator);
        Chain(c, SignalTrial.Source, "pulse-n21", SignalTrial.Wide, "pulse-n46", SignalTrial.Attenuator, SignalTrial.Meter);
        Observe(c, "Numune günlüğü", "Numuneler aynı özelliği değiştiriyorsa, daha sonra karşılaşılan numunenin izi son kayıtta belirginleşir. Her işlemin zaman, biçim ve güç üzerindeki etkisini ayrı ayrı gözle.");
        Observe(c, "Deney masası notu", "Birden fazla tepki üreten numune de normal bir hat üzerinde çalışabilir. Tepki sayısıyla hat sayısını aynı şey sanma.");
        var result = new List<PuzzleDefinition> { a, b, c };
        foreach (var puzzle in result)
        {
            var reading = PuzzleSimulation.Measure(puzzle.solution, puzzle);
            if (!reading.Measurable) throw new InvalidOperationException(reading.problem);
            if (puzzle.kind == PuzzleKind.ColorMixture) puzzle.mixtureTarget = reading.mixture.Copy();
            else { puzzle.target = reading.pulses; puzzle.rules.traceCount = puzzle.target.Count; }
            string issue = PuzzleSimulation.Validate(puzzle);
            if (issue != null) throw new InvalidOperationException(issue);
        }
        return result;
    }

    private static PuzzleDefinition Definition(PuzzleKind kind, string id, string title, string description, params string[] allowed)
    {
        var puzzle = new PuzzleDefinition { kind = kind, id = id, title = title, description = description, rules = PuzzleRules.ForKind(kind) };
        puzzle.rules.maxParts = puzzle.rules.maxDepth = 10; puzzle.rules.maxSamples = 3;
        foreach (var rule in puzzle.rules.parts)
        {
            var part = PuzzleParts.Find(rule.itemID);
            rule.allowed = allowed.Contains(rule.itemID); rule.maximum = part.inputs == 0 || part.outputs == 0 ? 1 : 4;
            rule.explanation = Observation(rule.itemID) ?? part.description;
        }
        return puzzle;
    }

    private static string Observation(string id)
    {
        switch (id)
        {
            case "pulse-n12": return "Tek başına denendiğinde ince ve kısa bir iz bırakır. Uyarının geliş anını ve gücünü korur.";
            case "pulse-n34": return "Tepkiyi geniş bir iz haline getirir. Uyarının geliş anını ve gücünü korur.";
            case "pulse-n08": return "Gelen tepkiyi kısa ve sönük bir iz haline getirir. Tek başına N-12'den daha zayıf görünür.";
            case "pulse-n21": return "Kısa bir tepki bırakır; yanıtı normal numuneden sonra gelir. Bu bekleme, geciktiricinin beklemesinden daha kısadır.";
            case "pulse-n46": return "Her uyarıya aralıklı iki yanıt verir. Yanıtların biçimini ve gücünü korur; tek giriş ve tek çıkışı vardır.";
            case "pulse-attenuator": return "İçinden geçen tepkiyi sönükleştirir. İz aynı yerde ve aynı genişlikte kalır; tekrarlamak onu daha da zayıflatır.";
            case "pulse-delay": return "İçinden geçen tepki daha sonra görünür. Genişlik ve güç değişmez; tekrar kullanılırsa bekleme uzar.";
            default: return null;
        }
    }

    private static void Chain(PuzzleDefinition puzzle, params string[] ids)
    { foreach (string id in ids) Add(puzzle, id); for (int i = 1; i < ids.Length; i++) Edge(puzzle, i - 1, i); }
    private static void Add(PuzzleDefinition puzzle, string id)
    { int i = puzzle.solution.parts.Count; puzzle.solution.parts.Add(new SynthesisPartData { itemID = id }); puzzle.positions.Add(new Vector2(25 + i * 225, 80)); }
    private static void Edge(PuzzleDefinition puzzle, int from, int to)
        => puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = from, toIndex = to, directed = true });
    private static void Observe(PuzzleDefinition puzzle, string title, string text)
        => puzzle.notes.Add(new BookWorkspacePage { title = title, explanation = text });
}
