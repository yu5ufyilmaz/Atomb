using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Scene cleanup diagnostics live in the Editor assembly and never write gameplay saves.
public static class LaboratoryCleanupChecks
{
    private const string ScenePath = "Assets/Scenes/scn_LaboratorySynthesis.unity";
    private const string Output = "outputs/laboratory-cleanup-2026-10-30";

    [Serializable] private sealed class Pose
    {
        public string id, path;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    [Serializable] private sealed class Baseline
    {
        public string scene, utc;
        public List<Pose> poses = new List<Pose>();
        public string[] books;
        public string goals;
        public bool tutorial, sessionOnly;
    }
    [Serializable] private sealed class Result
    {
        public bool success;
        public int objects, books, catalogs, oldSettingsChildren, missingScripts;
        public List<string> issues = new List<string>();
        public List<string> changes = new List<string>();
        public List<string> removedObjects = new List<string>();
    }

    private static Scene Target()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open the laboratory synthesis scene outside Play mode.");
        return scene;
    }
    private static IEnumerable<T> All<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    private static string ID(Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
    private static string PathOf(Transform value)
        => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
    private static void Write(string name, object value)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/" + name + ".json", JsonUtility.ToJson(value, true));
    }

    [MenuItem("Senzora/Laboratory Cleanup/1 Capture Baseline")]
    public static void CaptureBaseline()
    {
        var scene = Target();
        Directory.CreateDirectory(Output);
        if (File.Exists(Output + "/baseline.json")) throw new InvalidOperationException("Cleanup baseline already exists.");
        // Save a copy first: preserve the user's unsaved scene, active path and dirty state.
        if (!EditorSceneManager.SaveScene(scene, Output + "/before-editor.unity", true))
            throw new IOException("Could not back up the open Editor scene.");
        var trial = All<ResearchPuzzleTrial>(scene).Single();
        var baseline = new Baseline {
            scene = scene.path, utc = DateTime.UtcNow.ToString("O"),
            books = trial.books.Select(book => book.GetComponent<PuzzleBookIdentity>().bookID).ToArray(),
            goals = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(trial.simultaneousPuzzles)),
            tutorial = All<TutorialManager>(scene).Single().isTutorialEnabled,
            sessionOnly = All<SaveManager>(scene).Single().sessionOnly
        };
        foreach (var transform in All<Transform>(scene)) baseline.poses.Add(new Pose {
            id = ID(transform.gameObject), path = PathOf(transform), position = transform.position,
            rotation = transform.rotation, scale = transform.lossyScale
        });
        Write("baseline", baseline);
        Debug.Log($"LAB CLEANUP BASELINE: {baseline.poses.Count} objects, {baseline.books.Length} books; unsaved Editor scene backed up.");
    }

    [MenuItem("Senzora/Laboratory Cleanup/2 Check Scene")]
    public static void CheckScene()
    {
        var scene = Target();
        var result = new Result();
        var transforms = All<Transform>(scene).ToArray();
        result.objects = transforms.Length;
        result.catalogs = All<FormulaCatalog>(scene).Count();
        if (result.catalogs != 1) result.issues.Add($"Expected one FormulaCatalog; found {result.catalogs}.");
        foreach (var ui in All<SettingsUIManager>(scene))
        {
            var view = ui.GetComponentInChildren<SettingsMenuView>(true);
            if (view == null) { result.issues.Add("Missing settings view: " + PathOf(ui.transform)); continue; }
            foreach (Transform child in ui.transform)
                if (child != view.transform) result.oldSettingsChildren += child.GetComponentsInChildren<Transform>(true).Length;
        }
        if (result.oldSettingsChildren > 0) result.issues.Add($"Obsolete settings descendants: {result.oldSettingsChildren}.");
        foreach (var transform in transforms)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            result.missingScripts += count;
            if (count > 0) result.issues.Add($"Missing scripts ({count}): {PathOf(transform)}");
        }
        if (File.Exists(Output + "/baseline.json"))
        {
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(Output + "/baseline.json"));
            var current = transforms.ToDictionary(t => ID(t.gameObject));
            var removed = File.Exists(Output + "/scene-cleanup.json")
                ? JsonUtility.FromJson<Result>(File.ReadAllText(Output + "/scene-cleanup.json")).removedObjects
                : new List<string>();
            foreach (var pose in baseline.poses)
            {
                if (!current.TryGetValue(pose.id, out var transform))
                {
                    if (!removed.Contains(pose.id)) result.issues.Add("Unexpected object removal: " + pose.path);
                    continue;
                }
                if (Vector3.Distance(transform.position, pose.position) > .0001f
                    || Quaternion.Angle(transform.rotation, pose.rotation) > .01f
                    || Vector3.Distance(transform.lossyScale, pose.scale) > .0001f)
                    result.issues.Add("Authored world pose changed: " + pose.path);
            }
            var trial = All<ResearchPuzzleTrial>(scene).Single();
            result.books = trial.books.Length;
            if (!trial.books.Select(b => b.GetComponent<PuzzleBookIdentity>().bookID).SequenceEqual(baseline.books))
                result.issues.Add("Research book identities or order changed.");
            if (trial.books.Any(b => !b.gameObject.activeInHierarchy)) result.issues.Add("A research book is inactive.");
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(trial.simultaneousPuzzles)) != baseline.goals)
                result.issues.Add("Research goal pool changed.");
            if (All<TutorialManager>(scene).Single().isTutorialEnabled != baseline.tutorial)
                result.issues.Add("Authored tutorial choice changed.");
            if (!baseline.sessionOnly || !All<SaveManager>(scene).Single().sessionOnly)
                result.issues.Add("Session-only save protection is missing.");
        }
        result.success = result.issues.Count == 0;
        Write("scene-check", result);
        Debug.Log($"LAB CLEANUP SCENE {(result.success ? "PASS" : "FAIL")}: " + string.Join("; ", result.issues));
    }

    [MenuItem("Senzora/Laboratory Cleanup/3 Clean Verified Leftovers")]
    public static void CleanScene()
    {
        var scene = Target();
        if (!File.Exists(Output + "/baseline.json")) throw new InvalidOperationException("Capture the baseline first.");
        if (File.Exists(Output + "/scene-cleanup.json"))
            throw new InvalidOperationException("Cleanup already recorded. Run Check Scene instead; keep the original removal manifest.");
        var result = new Result();
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clean verified laboratory leftovers");
        var trial = All<ResearchPuzzleTrial>(scene).Single();
        var catalogs = All<FormulaCatalog>(scene).ToArray();
        if (catalogs.Length == 0) throw new InvalidOperationException("Missing formula catalog.");
        foreach (var extra in catalogs.Skip(1))
        {
            if (extra.transform.childCount != 0 || extra.GetComponents<Component>().Length != 2)
                throw new InvalidOperationException("Unexpected data on duplicate catalog: " + ID(extra));
            result.changes.Add("Removed duplicate catalog " + ID(extra.gameObject));
            result.removedObjects.Add(ID(extra.gameObject));
            Undo.DestroyObjectImmediate(extra.gameObject);
        }
        Undo.RecordObject(catalogs[0].gameObject, "Name formula catalog");
        catalogs[0].name = "FormulaCatalog";
        Undo.SetTransformParent(catalogs[0].transform, trial.transform, "Group research catalog");
        foreach (var ui in All<SettingsUIManager>(scene))
        {
            var view = ui.GetComponentInChildren<SettingsMenuView>(true);
            if (view == null || view.transform.parent != ui.transform)
                throw new InvalidOperationException("Expected the existing settings prefab directly under its adapter.");
            foreach (var child in ui.transform.Cast<Transform>().Where(t => t != view.transform).ToArray())
            {
                result.changes.Add("Removed obsolete settings branch " + PathOf(child));
                result.removedObjects.AddRange(child.GetComponentsInChildren<Transform>(true).Select(t => ID(t.gameObject)));
                Undo.DestroyObjectImmediate(child.gameObject);
            }
            var serialized = new SerializedObject(ui);
            foreach (string field in new[] { "masterSlider", "musicSlider", "sfxSlider", "sensitivitySlider", "brightnessSlider",
                "resolutionDropdown", "qualityDropdown", "languageDropdown", "fullscreenToggle", "subtitleToggle" })
                serialized.FindProperty(field).objectReferenceValue = null;
            serialized.ApplyModifiedProperties();
        }
        foreach (var transform in All<Transform>(scene))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            if (count == 0) continue;
            Undo.RegisterCompleteObjectUndo(transform.gameObject, "Remove missing script leftovers");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            result.changes.Add($"Removed {count} missing script(s): {PathOf(transform)}");
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save cleaned laboratory scene.");
        result.success = true;
        Write("scene-cleanup", result);
        CheckScene();
    }

    [MenuItem("Senzora/Laboratory Cleanup/Check Book Resource Lifetime (Play)")]
    public static void CheckBookResources()
    {
        var trial = ResearchPuzzleTrial.Active;
        if (!Application.isPlaying || trial == null) throw new InvalidOperationException("Research scene must be playing.");
        trial.StartCoroutine(BookResourceLifetime(trial.books[0]));
    }

    [MenuItem("Senzora/Laboratory Cleanup/Check Laboratory Settings (Play)")]
    public static void CheckSettings()
    {
        if (!Application.isPlaying || ResearchPuzzleTrial.Active == null || GameManager.Instance.isGameStarted)
            throw new InvalidOperationException("Start fresh laboratory Play at the main menu.");
        ResearchPuzzleTrial.Active.StartCoroutine(SettingsPanels());
    }
    private static IEnumerator SettingsPanels()
    {
        var result = new Result();
        var menu = Object.FindFirstObjectByType<MainMenuManager>();
        var pause = Object.FindFirstObjectByType<PauseManager>();
        try
        {
            menu.OpenSettings();
            yield return null;
            if (SettingsMenuView.Open == null) result.issues.Add("Laboratory main menu settings did not open.");
            else SettingsMenuChecks.ValidateView(SettingsMenuView.Open);
            menu.CloseSettings();
            yield return null;
            pause.PauseGame();
            pause.OpenSettings();
            yield return null;
            if (SettingsMenuView.Open == null) result.issues.Add("Laboratory pause settings did not open.");
            else SettingsMenuChecks.ValidateView(SettingsMenuView.Open);
            pause.CloseSettings();
            yield return null;
            if (Time.timeScale != 0 || !AudioListener.pause)
                result.issues.Add("Closing settings resumed the paused laboratory.");
        }
        finally
        {
            menu.CloseSettings();
            pause.CloseSettings();
            pause.ResumeGame();
        }
        result.success = result.issues.Count == 0;
        Write("laboratory-settings-check", result);
        Debug.Log($"LAB SETTINGS {(result.success ? "PASS" : "FAIL")}: " + string.Join("; ", result.issues));
    }

    [MenuItem("Senzora/Laboratory Cleanup/4 Organize Scene Roots")]
    public static void OrganizeRoots()
    {
        var scene = Target();
        bool savedSceneIsCurrent = !scene.isDirty;
        string serializedScene = File.ReadAllText(scene.path);
        Transform Group(string name)
        {
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
            if (existing != null) return existing.transform;
            var group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, "Group laboratory roots");
            SceneManager.MoveGameObjectToScene(group, scene);
            return group.transform;
        }
        void Move(Transform item, Transform parent) => Undo.SetTransformParent(item, parent, "Group laboratory roots");
        var managers = scene.GetRootGameObjects().Single(go => go.name == "MANAGERS").transform;
        var enemies = scene.GetRootGameObjects().Single(go => go.name == "ENEMIES").transform;
        Move(All<ResearchPuzzleTrial>(scene).Single().transform, managers);
        Move(All<LeesResistanceController>(scene).Single().transform, enemies);
        Move(All<SettingsManager>(scene).Single().transform, null);
        var cameras = Group("CAMERAS");
        var audio = Group("AUDIO");
        var ui = Group("UI");
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "StartSceneCams" || root.name == "FinalSceneCams") Move(root.transform, cameras);
            if (root.name == "AmbianceMusic") Move(root.transform, audio);
            if (root.name == "Canvas" || root.name == "Handheld_Device_Canvas") Move(root.transform, ui);
        }
        // The deleted occlusion asset must not masquerade as a valid bake.
        if (savedSceneIsCurrent && serializedScene.Contains("guid: 1733eafaf8fd3c045ae46d9009bc85be,")
            && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("1733eafaf8fd3c045ae46d9009bc85be")))
            StaticOcclusionCulling.Clear();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save organized laboratory scene.");
        CheckScene();
    }

    [MenuItem("Senzora/Laboratory Cleanup/Check Highlight Lifetime (Play)")]
    public static void CheckHighlightLifetime()
    {
        if (!Application.isPlaying || ResearchPuzzleTrial.Active == null)
            throw new InvalidOperationException("Research scene must be playing.");
        ResearchPuzzleTrial.Active.StartCoroutine(HighlightLifetime());
    }
    private static IEnumerator HighlightLifetime()
    {
        var result = new Result();
        var source = new Material(Shader.Find("HDRP/Lit"));
        var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fixture.name = "Highlight lifetime fixture";
        fixture.transform.position = Vector3.up * 1000;
        var renderer = fixture.GetComponent<Renderer>();
        renderer.sharedMaterial = source;
        var highlight = fixture.AddComponent<UniversalHighlighter>();
        yield return null;
        highlight.EnableHighlight();
        var highlighted = renderer.sharedMaterial;
        if (highlighted == source) result.issues.Add("Universal highlight did not apply its appearance.");
        highlight.DisableHighlight();
        if (renderer.sharedMaterial != source) result.issues.Add("Universal highlight did not restore the borrowed material.");
        Object.Destroy(fixture);
        yield return null;
        yield return null;
        if (highlighted != null) result.issues.Add("Universal highlight material survived its component.");
        if (source == null) result.issues.Add("Universal highlight destroyed a borrowed material.");
        if (source != null) Object.Destroy(source);
        result.success = result.issues.Count == 0;
        Write("highlight-resource-check", result);
        Debug.Log($"LAB HIGHLIGHT RESOURCE {(result.success ? "PASS" : "FAIL")}: " + string.Join("; ", result.issues));
    }

    [MenuItem("Senzora/Laboratory Cleanup/Check Legacy Book Focus (Play)")]
    public static void CheckBookFocus()
    {
        if (!Application.isPlaying || ResearchPuzzleTrial.Active == null)
            throw new InvalidOperationException("Research scene must be playing.");
        ResearchPuzzleTrial.Active.StartCoroutine(BookFocus());
    }
    private static IEnumerator BookFocus()
    {
        var clone = Object.Instantiate(ResearchPuzzleTrial.Active.books[0].gameObject);
        clone.name = "Legacy book focus fixture";
        clone.transform.position += Vector3.up * 1000;
        yield return null;
        var book = clone.GetComponent<InteractableBook>();
        book.outlineController = null;
        book.emissionIntensity = 10;
        var renderer = book.bookSkinnedMeshRenderer;
        var original = renderer.sharedMaterials;
        book.OnFocus();
        var focused = renderer.sharedMaterials;
        var result = new Result();
        if (focused.SequenceEqual(original)) result.issues.Add("Legacy book focus did not apply emission materials.");
        book.OnLoseFocus();
        if (!renderer.sharedMaterials.SequenceEqual(original)) result.issues.Add("Legacy book focus did not restore the page/cover materials.");
        Object.Destroy(clone);
        yield return null;
        yield return null;
        if (focused.Any(material => material != null)) result.issues.Add("Legacy focus material survived the book.");
        result.success = result.issues.Count == 0;
        Write("book-focus-check", result);
        Debug.Log($"LAB BOOK FOCUS {(result.success ? "PASS" : "FAIL")}: " + string.Join("; ", result.issues));
    }

    [MenuItem("Senzora/Laboratory Cleanup/Check Manager Lifetime (Play)")]
    public static void CheckManagerLifetime()
    {
        if (!Application.isPlaying || ResearchPuzzleTrial.Active == null)
            throw new InvalidOperationException("Research scene must be playing.");
        ResearchPuzzleTrial.Active.StartCoroutine(ManagerLifetime());
    }
    private static IEnumerator ManagerLifetime()
    {
        var result = new Result();
        var game = GameManager.Instance;
        var subtitles = GlobalSubtitleManager.Instance;
        var debug = DebugMenuManager.Instance;
        var settings = SettingsManager.Instance;
        var fixture = SceneManager.CreateScene("Laboratory manager lifetime fixture");
        var holder = new GameObject("Scene systems");
        SceneManager.MoveGameObjectToScene(holder, fixture);
        holder.SetActive(false);
        var preferences = new GameObject("Settings lifetime fixture");
        preferences.transform.SetParent(holder.transform);
        SettingsManager.Instance = null;
        var persistent = preferences.AddComponent<SettingsManager>();
        holder.SetActive(true);
        yield return null;
        if (persistent.transform.parent != null)
            result.issues.Add("Settings manager did not detach before requesting persistence.");
        GameManager.Instance = null;
        GlobalSubtitleManager.Instance = null;
        DebugMenuManager.Instance = null;
        var localGame = new GameObject("Scene game manager fixture");
        SceneManager.MoveGameObjectToScene(localGame, fixture);
        localGame.AddComponent<GameManager>();
        var localSubtitles = new GameObject("Scene subtitles fixture");
        SceneManager.MoveGameObjectToScene(localSubtitles, fixture);
        localSubtitles.AddComponent<GlobalSubtitleManager>();
        var localDebug = new GameObject("Scene debug fixture");
        SceneManager.MoveGameObjectToScene(localDebug, fixture);
        localDebug.AddComponent<DebugMenuManager>();
        yield return SceneManager.UnloadSceneAsync(fixture);
        if (localGame != null) result.issues.Add("Scene game manager survived its scene.");
        if (localSubtitles != null) result.issues.Add("Scene subtitle manager survived its UI scene.");
        if (localDebug != null) result.issues.Add("Scene debug manager survived its scene.");
        if (persistent == null) result.issues.Add("Settings manager was destroyed with its old parent scene.");
        if (localGame != null) Object.Destroy(localGame);
        if (localSubtitles != null) Object.Destroy(localSubtitles);
        if (localDebug != null) Object.Destroy(localDebug);
        if (preferences != null) Object.Destroy(preferences);
        yield return null;
        GameManager.Instance = game;
        GlobalSubtitleManager.Instance = subtitles;
        DebugMenuManager.Instance = debug;
        SettingsManager.Instance = settings;
        result.success = result.issues.Count == 0;
        Write("manager-lifetime-check", result);
        Debug.Log($"LAB MANAGER LIFETIME {(result.success ? "PASS" : "FAIL")}: " + string.Join("; ", result.issues));
    }

    private static IEnumerator BookResourceLifetime(InteractableBook source)
    {
        var existing = new HashSet<int>(Resources.FindObjectsOfTypeAll<Material>().Select(m => m.GetInstanceID()));
        var sourcePages = source.bookPagesMaterial;
        var sourceMaterials = source.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().ToArray();
        var clone = Object.Instantiate(source.gameObject);
        clone.name = "Cleanup resource lifetime check";
        clone.transform.position += Vector3.up * 1000;
        yield return null;
        yield return null;
        var book = clone.GetComponent<InteractableBook>();
        book.DisableEmission();
        book.DisableEmission();
        var resources = new List<Object> { book.bookPagesMaterial, book.pageTurnMaterial };
        resources.AddRange(clone.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials));
        var fields = typeof(InteractableBook).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(Mesh) && field.GetValue(book) is Mesh mesh) resources.Add(mesh);
            if (field.Name != "allRenderersData" || field.GetValue(book) is not IEnumerable renderers) continue;
            foreach (var renderer in renderers)
                foreach (var materialField in renderer.GetType().GetFields())
                    if (materialField.FieldType == typeof(Material[]) && materialField.GetValue(renderer) is Material[] materials)
                        resources.AddRange(materials);
        }
        resources = resources.Where(r => r != null && !EditorUtility.IsPersistent(r) && !existing.Contains(r.GetInstanceID())).Distinct().ToList();
        Object.Destroy(clone);
        yield return null;
        yield return null;
        var result = new Result();
        if (resources.Count < 2) result.issues.Add("The fixture did not create runtime book resources.");
        if (sourcePages == null) result.issues.Add("Destroying the clone also destroyed the source book's material.");
        if (sourceMaterials.Any(material => material == null))
            result.issues.Add("Destroying the clone destroyed a borrowed source renderer material.");
        foreach (var resource in resources)
            if (resource != null) result.issues.Add("Owned resource survived destroyed book: " + resource.name);
        result.success = result.issues.Count == 0;
        result.changes.Add($"Checked {resources.Count} runtime materials/meshes.");
        Write("book-resource-check", result);
        Debug.Log($"LAB BOOK RESOURCE {(result.success ? "PASS" : "FAIL")}: {resources.Count} resources, {result.issues.Count} survivors.");
    }
}
