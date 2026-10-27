using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PuzzleWorkshopSetup
{
    public const string ScenePath = "Assets/Scenes/scn_PuzzleWorkshop.unity";
    public const string Folder = "Assets/Scripts/Scriptables/Puzzles";
    public const string PoolPath = Folder + "/PuzzlePool.asset";
    public const string LegacyPath = Folder + "/CiftKayit.asset";
    public const string LabPath = "Assets/Scenes/scn_LaboratorySynthesis.unity";

    [MenuItem("Senzora/Puzzle Atölyesi/Aç")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { PuzzleWorkshopWindow.OpenWindow(); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureAssets();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            if (!AssetDatabase.CopyAsset("Assets/Scenes/scn_SynthesisBlockout.unity", ScenePath))
                throw new InvalidOperationException("Atölye deneme sahnesi kopyalanamadı.");
            var created = EditorSceneManager.OpenScene(ScenePath);
            var trial = FindTrial(created);
            if (trial == null) throw new InvalidOperationException("Atölyede araştırma nesnesi bulunamadı.");
            trial.allowGameplayScene = true;
            trial.puzzlePool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PoolPath);
            trial.previewPuzzle = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(LegacyPath);
            AssignBookIDs(trial);
            EditorSceneManager.MarkSceneDirty(created); EditorSceneManager.SaveScene(created);
        }
        else EditorSceneManager.OpenScene(ScenePath);
        PuzzleWorkshopWindow.OpenWindow();
    }

    public static void EnsureAssets()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scripts/Scriptables", "Puzzles");
        bool newPool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PoolPath) == null;
        if (newPool) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PuzzlePoolSO>(), PoolPath);
        var pool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PoolPath);
        var definitions = new[] { PuzzleDefinition.Legacy(), Sample("late-short", "Gecikmeli Kısa Kayıt", "pulse-n21", SignalTrial.Attenuator),
            Sample("one-line-double", "Tek Hatta Çift Tepki", SignalTrial.Narrow, "pulse-n46", SignalTrial.Attenuator),
            Sample("weak-wide", "Zayıf Geniş Kayıt", "pulse-n08", SignalTrial.Wide) };
        string[] names = { "CiftKayit", "GecikmeliKisaKayit", "TekHattaCiftTepki", "ZayifGenisKayit" };
        for (int i = 0; i < definitions.Length; i++)
        {
            string path = Folder + "/" + names[i] + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); asset.definition = definitions[i];
                string error = PuzzleSimulation.Validate(asset.definition);
                if (error != null) throw new InvalidOperationException(names[i] + ": " + error);
                AssetDatabase.CreateAsset(asset, path);
            }
            if (newPool) pool.puzzles.Add(asset);
        }
        if (newPool) EditorUtility.SetDirty(pool);
        EnsureColorAssets(pool);
        AssetDatabase.SaveAssetIfDirty(pool);
    }

    private static PuzzleDefinition Sample(string id, string title, params string[] ids)
    {
        var puzzle = new PuzzleDefinition { id = "workshop-" + id, title = title, description = "Gözlemlerden yararlanarak hedefteki kaydı üret. Deneyin koşulları ve parça adetleri kitaplarda açıklanır." };
        puzzle.rules.minSamples = puzzle.rules.maxSamples = ids.Count(x => SignalPartLibrary.Find(x).sample);
        int Add(string part)
        {
            int index = puzzle.solution.parts.Count;
            puzzle.solution.parts.Add(new SynthesisPartData { itemID = part }); puzzle.positions.Add(new Vector2(35 + index * 185, 140)); return index;
        }
        Add(SignalTrial.Source);
        foreach (string part in ids) Add(part);
        Add(SignalTrial.Meter);
        for (int i = 1; i < puzzle.solution.parts.Count; i++)
            puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = i - 1, toIndex = i, directed = true });
        if (id == "late-short") puzzle.rules.Find("pulse-n21").minimum = 1;
        if (id == "weak-wide") { puzzle.rules.Find("pulse-n08").minimum = 1; puzzle.rules.Find(SignalTrial.Wide).minimum = 1; }
        if (id == "one-line-double")
        { puzzle.rules.Find("pulse-n46").minimum = 1; puzzle.rules.Find(SignalTrial.Splitter).allowed = false; }
        puzzle.target = PuzzleSimulation.Measure(puzzle.solution, puzzle).pulses;
        puzzle.rules.traceCount = puzzle.target.Count;
        return puzzle;
    }

    private static void EnsureColorAssets(PuzzlePoolSO pool)
    {
        string[] names = { "MorKarisim", "SeyreltilmisMor", "YogunYesil" };
        string[] titles = { "Mor Karışım", "Seyreltilmiş Mor", "Yoğun Yeşil" };
        string[][] sequences = {
            new[] { ColorMixtureLibrary.Red, ColorMixtureLibrary.Blue },
            new[] { ColorMixtureLibrary.Red, ColorMixtureLibrary.Blue, ColorMixtureLibrary.Solvent },
            new[] { ColorMixtureLibrary.Green, ColorMixtureLibrary.Concentrator }
        };
        for (int index = 0; index < names.Length; index++)
        {
            string path = Folder + "/" + names[index] + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) continue;
            var puzzle = new PuzzleDefinition { kind = PuzzleKind.ColorMixture, rules = PuzzleRules.ForKind(PuzzleKind.ColorMixture),
                id = "mixture-" + names[index], title = titles[index], description = "Hedefteki karışımı üret. Renk oranları, pigment yoğunluğu ve toplam miktar birlikte tutmalı; deney koşulları kitaplarda." };
            var ids = new List<string> { ColorMixtureLibrary.Source }; ids.AddRange(sequences[index]); ids.Add(ColorMixtureLibrary.Meter);
            for (int i = 0; i < ids.Count; i++)
            {
                puzzle.solution.parts.Add(new SynthesisPartData { itemID = ids[i] }); puzzle.positions.Add(new Vector2(25 + i * 225, 80));
                if (i > 0) puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = i - 1, toIndex = i, directed = true });
            }
            puzzle.mixtureTarget = PuzzleSimulation.Measure(puzzle.solution, puzzle).mixture;
            string issue = PuzzleSimulation.Validate(puzzle);
            if (issue != null) throw new InvalidOperationException(issue);
            var asset = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); asset.definition = puzzle;
            AssetDatabase.CreateAsset(asset, path); pool.puzzles.Add(asset); EditorUtility.SetDirty(pool);
        }
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Havuzu Laboratuvara Bağla")]
    public static void BindLaboratory()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureAssets();
        var scene = EditorSceneManager.OpenScene(LabPath);
        var trial = FindTrial(scene);
        if (trial == null) throw new InvalidOperationException("Çalışma laboratuvarında araştırma bağlantısı yok.");
        trial.puzzlePool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PoolPath); trial.previewPuzzle = null;
        trial.simultaneousPuzzles = null;
        AssignBookIDs(trial);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("PUZZLE WORKSHOP LAB BOUND: New Game selects from the pool; sessionOnly remains enabled for safe development.");
    }

    public static void AssignBookIDs(ResearchPuzzleTrial trial)
    {
        var unique = new HashSet<string>();
        for (int index = 0; index < trial.books.Length; index++)
        {
            var book = trial.books[index];
            if (book == null) throw new InvalidOperationException("Araştırma kitabı bağlantısı eksik.");
            var identity = book.GetComponent<PuzzleBookIdentity>() ?? book.gameObject.AddComponent<PuzzleBookIdentity>();
            if (string.IsNullOrEmpty(identity.bookID)) identity.bookID = Guid.NewGuid().ToString("N");
            if (!unique.Add(identity.bookID)) throw new InvalidOperationException("Kitap kimliği yineleniyor: " + identity.bookID);
            identity.accessible = true;
            if (string.IsNullOrWhiteSpace(identity.title))
            {
                var profile = PuzzleBookProfile.Default(index, identity.bookID, trial.books.Length > 4);
                identity.title = profile.title; identity.topics = profile.topics; identity.pageCount = profile.pageCount;
            }
            var shelf = book.transform.parent;
            while (shelf != null && !shelf.name.StartsWith("RAF")) shelf = shelf.parent;
            identity.location = shelf != null ? shelf.name.Replace("RAF", "Raf ").Trim() : "Araştırma masası";
            EditorUtility.SetDirty(identity);
        }
        foreach (var save in trial.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)))
            if (trial.gameObject.scene.path == ScenePath) save.sessionOnly = true;
        EditorUtility.SetDirty(trial);
    }

    public static ResearchPuzzleTrial FindTrial(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<ResearchPuzzleTrial>(true)).FirstOrDefault();

    public static void Preview(PuzzleDefinitionSO asset)
    {
        if (asset == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) throw new InvalidOperationException("Önce puzzleı bir asset olarak kaydet.");
        string issue = PuzzleSimulation.Validate(asset.definition);
        if (issue != null) throw new InvalidOperationException(issue);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (SceneManager.GetActiveScene().path != ScenePath) Open();
        var trial = FindTrial(SceneManager.GetActiveScene());
        trial.previewPuzzle = asset; trial.allowGameplayScene = true;
        EditorUtility.SetDirty(trial); EditorSceneManager.MarkSceneDirty(trial.gameObject.scene);
        EditorSceneManager.SaveScene(trial.gameObject.scene); AssetDatabase.SaveAssets();
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Paket Girişini Doğrula")]
    public static void ValidateEntry()
    {
        bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == LabPath && s.enabled);
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scn_PressAnyKeyMenu.unity");
        try
        {
            var splash = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SplashManager>(true)).FirstOrDefault();
            if (!inBuild || splash == null || splash.gameSceneName != Path.GetFileNameWithoutExtension(LabPath))
                Debug.LogWarning("Paket girişi henüz puzzle havuzuna yönlenmiyor. 'Paket Girişini Kur' komutunu kullan. Atölye ve çalışma laboratuvarı doğrudan açılarak oynanabilir.");
            else Debug.Log("PUZZLE WORKSHOP ENTRY PASS: Splash -> pooled laboratory is included in build.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Paket Girişini Kur")]
    public static void InstallEntry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BindLaboratory();
        var lab = SceneManager.GetActiveScene();
        var save = lab.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)).FirstOrDefault();
        if (save != null) { save.sessionOnly = false; save.isolatedSavePath = ""; EditorUtility.SetDirty(save); }
        EditorSceneManager.MarkSceneDirty(lab); EditorSceneManager.SaveScene(lab);
        var build = EditorBuildSettings.scenes.ToList();
        int index = build.FindIndex(s => s.path == LabPath);
        if (index < 0) build.Add(new EditorBuildSettingsScene(LabPath, true)); else build[index] = new EditorBuildSettingsScene(LabPath, true);
        EditorBuildSettings.scenes = build.ToArray();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/scn_PressAnyKeyMenu.unity");
        var splash = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SplashManager>(true)).FirstOrDefault();
        if (splash == null) throw new InvalidOperationException("Splash yükleyici bulunamadı.");
        splash.gameSceneName = Path.GetFileNameWithoutExtension(LabPath);
        EditorUtility.SetDirty(splash); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        ValidateEntry();
    }
}
