using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LaboratorySynthesisSetup
{
    public const string SourcePath = "Assets/Scenes/scn_labratory.unity";
    public const string ScenePath = "Assets/Scenes/scn_LaboratorySynthesis.unity";
    public const string DeskBookPath = "ENVIRONMENT/Office_Room/InteractablesProbs/BOOKS/P_Book_Puzzle_symbol_01";

    [MenuItem("Senzora/Lab Migration/Create Laboratory Copy")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current scene before creating the laboratory copy.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Laboratory copy already exists; open it instead of overwriting edits.");
        if (!AssetDatabase.CopyAsset(SourcePath, ScenePath)) throw new InvalidOperationException("Scene copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var notebook = Find<NotebookUI>(scene);
        var board = Find<ChalkboardManager>(scene);
        var terminal = Find<InteractableTuringMachine>(scene);
        if (notebook == null || board == null || terminal == null) throw new InvalidOperationException("Laboratory references missing.");

        var host = new GameObject("Laboratory Synthesis");
        SceneManager.MoveGameObjectToScene(host, scene);
        var trial = host.AddComponent<ResearchPuzzleTrial>();
        trial.allowGameplayScene = true;
        trial.partTemplate = AssetDatabase.LoadAssetAtPath<FormulaItemSO>("Assets/Scripts/Scriptables/Formulas/SignalPartTemplate.asset");
        var books = All<InteractableBook>(scene);
        for (int i = 0; i < 4; i++)
        {
            string shelf = "RAF" + (i + 1);
            trial.books[i] = books.FirstOrDefault(b => b.gameObject.activeInHierarchy && b.transform.parent.name == shelf);
            if (trial.books[i] == null) throw new InvalidOperationException("Research book shelf missing: " + shelf);
            trial.books[i].usePhysicalPageBlocks = true;
            trial.books[i].canContainPassword = false;
        }
        if (!ResearchTrialBookTitles.Apply(trial)) throw new InvalidOperationException("Research book labels could not be applied.");
        var experiment = host.AddComponent<SignalExperiment>();
        experiment.terminal = terminal;
        host.AddComponent<LaboratorySynthesisScene>();

        notebook.useScreenSpaceNotebook = true;
        if (notebook.notebookPanel != null) notebook.notebookPanel.SetActive(false);
        foreach (var t in notebook.GetComponentsInChildren<Transform>(true))
            if (t.name == "NotebookPanel") t.gameObject.SetActive(false);
        var workspace = notebook.GetComponent<NotebookWorkspaceController>() ?? notebook.gameObject.AddComponent<NotebookWorkspaceController>();
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scn_SynthesisBlockout.unity");
        try
        {
            var source = Find<NotebookWorkspaceController>(preview);
            if (source == null) throw new InvalidOperationException("Test notebook missing.");
            EditorUtility.CopySerialized(source, workspace);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        workspace.standaloneUnlocksCursor = false;
        foreach (var placer in All<MachineNotebookPlacer>(scene)) placer.enabled = false;
        // Let the migrated books and original machines be reached without tutorial gating.
        var interaction = Find<PlayerInteraction>(scene);
        if (interaction != null) interaction.isTutorialMode = false;
        var tutorial = GameObject.Find("PUZZLES/TutorialSequence");
        if (tutorial != null) tutorial.SetActive(false);
        CompleteCopyBindings();
        ApplyPresentationFixes();
        ApplyNotebookAndDeskBook();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save migration scene.");
        Selection.activeGameObject = host;
        Debug.Log("LAB MIGRATION CREATED: " + ScenePath + ". Original player, board and Turing poses retained. Four labeled research books use existing shelf positions.");
    }

    [MenuItem("Senzora/Lab Migration/Update Copy Bindings")]
    public static void CompleteCopyBindings()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath) return;
        var trial = Find<ResearchPuzzleTrial>(scene);
        if (trial == null) throw new InvalidOperationException("Research content missing from the copy.");
        var save = Find<SaveManager>(scene);
        if (save != null) save.sessionOnly = true;
        foreach (var book in trial.books)
            if (book.GetComponent<BookPageBlockSurface>() == null) book.gameObject.AddComponent<BookPageBlockSurface>();
        foreach (var formula in All<DraggableFormula>(scene)) formula.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Senzora/Lab Migration/Apply Presentation Fixes")]
    public static void ApplyPresentationFixes()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open the migration copy outside Play mode.");
        var trial = Find<ResearchPuzzleTrial>(scene);
        foreach (var book in All<InteractableBook>(scene))
        {
            book.usePhysicalPageBlocks = true;
            book.viewPositionOffset = new Vector3(-1.1f, -.1f, 7.5f);
            if (trial.books.Contains(book))
            {
                book.printNotesOnPages = true;
                book.startPageIndex = 0;
            }
            EditorUtility.SetDirty(book);
            PrefabUtility.RecordPrefabInstancePropertyModifications(book);
        }
        var board = Find<ChalkboardManager>(scene);
        var button = board.boardTransform.parent.Find("SynthesisButton");
        if (button != null) Object.DestroyImmediate(button.gameObject);
        var plane = board.boardTransform.parent.Find("Plane").GetComponent<MeshFilter>();
        Bounds meshBounds = plane.sharedMesh.bounds;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int corner = 0; corner < 8; corner++)
        {
            var local = new Vector3((corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
            Vector2 point = board.boardTransform.InverseTransformPoint(plane.transform.TransformPoint(local));
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        board.minBounds = min + Vector2.one * .06f;
        board.maxBounds = max - Vector2.one * .06f;
        var zone = board.synthesisZone;
        if (zone != null && zone.GetComponent<BoxCollider>() is BoxCollider box)
        {
            Vector3 low = Vector3.positiveInfinity, high = Vector3.negativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? board.minBounds.x : board.maxBounds.x,
                    (corner & 2) == 0 ? board.minBounds.y : board.maxBounds.y, (corner & 4) == 0 ? -.05f : .05f);
                Vector3 local = zone.transform.InverseTransformPoint(board.boardTransform.TransformPoint(point));
                low = Vector3.Min(low, local); high = Vector3.Max(high, local);
            }
            box.center = (low + high) * .5f; box.size = high - low;
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("LAB PRESENTATION CONFIGURED: printed research books, quiet other shelves, farther book view, full board bounds and no placeholder button.");
    }

    [MenuItem("Senzora/Lab Migration/Apply Notebook and Desk Book")]
    public static void ApplyNotebookAndDeskBook()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open the migration copy outside Play mode.");
        var trial = Find<ResearchPuzzleTrial>(scene);
        var desk = GameObject.Find(DeskBookPath)?.GetComponent<InteractableBook>();
        if (desk == null) throw new InvalidOperationException("The existing desk book is missing.");
        if (trial.books[0] != desk)
        {
            var previous = trial.books[0];
            foreach (var t in previous.GetComponentsInChildren<Transform>(true))
                if (t != null && (t.name == "Araştırma Kitap Adı / Kapak" || t.name == "Araştırma Kitap Adı / Sırt")) Object.DestroyImmediate(t.gameObject);
            previous.printNotesOnPages = false; EditorUtility.SetDirty(previous);
            trial.books[0] = desk;
        }
        desk.printNotesOnPages = desk.usePhysicalPageBlocks = true;
        desk.canContainPassword = false;
        desk.startPageIndex = 0; desk.initialState = InteractableBook.BookInitialState.Closed;
        if (desk.GetComponent<BookPageBlockSurface>() == null) desk.gameObject.AddComponent<BookPageBlockSurface>();
        EditorUtility.SetDirty(desk); PrefabUtility.RecordPrefabInstancePropertyModifications(desk);
        EditorUtility.SetDirty(trial);
        ResearchTrialBookTitles.Apply(trial);
        foreach (var label in desk.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            if (label.transform.parent.name.StartsWith("Araştırma Kitap Adı")) label.text = "HEDEFLER";

        const string fontPath = "Assets/Fonts/Caveat/Caveat-Regular SDF.asset";
        var handwriting = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
        if (handwriting == null)
        {
            handwriting = TMPro.TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Caveat/Caveat-Regular.ttf"));
            handwriting.name = "Caveat-Regular SDF";
            AssetDatabase.CreateAsset(handwriting, fontPath);
            AssetDatabase.AddObjectToAsset(handwriting.material, handwriting);
            foreach (var texture in handwriting.atlasTextures) AssetDatabase.AddObjectToAsset(texture, handwriting);
            string characters = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + "ÇçĞğİıÖöŞşÜü";
            if (!handwriting.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Handwriting font missing required characters: " + missing);
            EditorUtility.SetDirty(handwriting); AssetDatabase.SaveAssets();
        }
        var workspace = Find<NotebookWorkspaceController>(scene);
        var settings = new SerializedObject(workspace);
        settings.FindProperty("notebookHandwritingFont").objectReferenceValue = handwriting;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("LAB DESK AND HANDWRITING READY: target book on the existing desk; Turkish handwriting configured.");
    }

    public static T Find<T>(Scene scene) where T : Component => All<T>(scene).FirstOrDefault();
    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
}
