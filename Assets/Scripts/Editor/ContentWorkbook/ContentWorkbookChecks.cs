using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ContentWorkbookChecks
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception("CONTENT CHECK: " + message); }
    [MenuItem("Senzora/Doğrulama/İçerik Tablosu ve Tuşlar")]
    public static void Run()
    {
        try { RunChecks(); File.WriteAllText("Temp/content-workbook.result", "PASS " + DateTime.Now.ToString("s")); }
        catch (Exception error) { File.WriteAllText("Temp/content-workbook.result", "FAIL " + error); throw; }
    }
    static void RunChecks()
    {
        Require(!EditorApplication.isPlaying, "Run the isolated input checks outside Play mode.");
        var book = ContentWorkbookProject.Validate(ContentWorkbookProject.WorkbookPath);
        Require(book.Errors.Count == 0, string.Join("\n", book.Errors.Take(20)));
        Require(book.Data("Altyazilar").Count() > 75, "Current sequential subtitles were not exported.");
        Require(book.Data("Baglantilar").Any(r => ContentSheet.Cell(r, 2).Contains("Konuşma Eventleri")), "Tutorial dialogue assignments are absent.");
        CheckInputs();
        CheckInvalidImport();
        CheckRollback();
        Debug.Log("CONTENT WORKBOOK UNITY PASS: populated workbook and live bindings/hints. Translation warnings=" + book.Warnings.Count);
    }
    static void CheckInvalidImport()
    {
        var copy = ContentWorkbookFile.Read(ContentWorkbookProject.WorkbookPath);
        copy.Sheet("Metinler").Rows[1][0] += ".renamed";
        const string file = "Temp/content-invalid-import.xlsx";
        if (File.Exists(file)) File.Delete(file); ContentWorkbookFile.WriteNew(file, copy);
        var errors = ContentWorkbookProject.Validate(file).Errors;
        Require(errors.Any(e => e.Contains("mevcut kod silinmiş")), "Renamed text key did not fail validation.");
        string before = File.ReadAllText("Assets/Resources/Localization/Tables/tr.json");
        bool rejected = false;
        try { ContentWorkbookProject.Import(file); } catch (InvalidDataException) { rejected = true; }
        Require(rejected && before == File.ReadAllText("Assets/Resources/Localization/Tables/tr.json"), "Invalid import changed project data.");
    }
    static void CheckRollback()
    {
        const string scenePath = "Assets/Scenes/__ContentWorkbookRegression.unity";
        const string file = "Temp/content-rollback-import.xlsx";
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var database = AssetDatabase.LoadAssetAtPath<SubtitleData>(ContentWorkbookProject.DatabasePath);
        string memory = EditorJsonUtility.ToJson(database);
        string originalText = File.ReadAllText("Assets/Resources/Localization/Tables/tr.json");
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var node = new GameObject("Rollback dialogue"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(node, scene);
            var action = node.AddComponent<DialogAction>(); action.subtitleID = database.entries[0].id;
            Require(EditorSceneManager.SaveScene(scene, scenePath), "Could not create isolated rollback fixture.");
            var copy = ContentWorkbookProject.Export();
            copy.Sheet("Metinler").Rows[1][2] += " rollback fixture";
            copy.Sheet("Altyazilar").Rows[1][2] = "99";
            var link = copy.Data("Baglantilar").Single(r => ContentSheet.Cell(r, 1) == scenePath);
            link[4] = database.entries[1].id;
            if (File.Exists(file)) File.Delete(file); ContentWorkbookFile.WriteNew(file, copy);
            bool failed = false;
            // Final content verification is part of the real import transaction.
            // Fail it after tables, database and scene assignments have all changed.
            try { ContentWorkbookProject.Import(file, _ => throw new InvalidDataException("Regression: reject applied content")); }
            catch (InvalidDataException error) { failed = error.Message == "Regression: reject applied content"; }
            Require(failed, "Late verification did not reject the staged transaction.");
            Require(File.ReadAllText("Assets/Resources/Localization/Tables/tr.json") == originalText, "Rollback left changed translation bytes.");
            Require(EditorJsonUtility.ToJson(database) == memory, "Rollback left modified SubtitleData in memory.");
            var restored = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            Require(restored.IsValid() && restored.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogAction>()).Single().subtitleID == database.entries[0].id, "Rollback left changed scene fields in memory.");
            using (var readLock = new FileStream(scenePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool lockRejected = false;
                try { ContentWorkbookProject.Import(file); } catch (IOException) { lockRejected = true; }
                Require(lockRejected && File.ReadAllText("Assets/Resources/Localization/Tables/tr.json") == originalText, "A pre-existing file lock wasn't rejected before writes.");
            }
        }
        finally
        {
            var fixture = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (fixture.IsValid()) EditorSceneManager.CloseScene(fixture, true);
            AssetDatabase.DeleteAsset(scenePath); EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
    public static void CheckInputs(bool simulateInput = false)
    {
        const string prefs = "Senzora.InputOverrides.v1";
        bool hadSave = PlayerPrefs.HasKey(prefs); string original = PlayerPrefs.GetString(prefs);
        string language = GameLocalization.CurrentCode;
        var originalSettings = InputSystem.settings;
        var isolatedSettings = UnityEngine.Object.Instantiate(originalSettings);
        isolatedSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        isolatedSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = isolatedSettings;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var labelObject = new GameObject("Content input hint regression", typeof(RectTransform), typeof(TextMeshProUGUI));
        LocalizedText binding = null;
        try
        {
            GameInput.ResetBindings();
            var cancel = GameInput.Action("Cancel"); Require(cancel != null, "Cancel action is absent.");
            var sprint = GameInput.Action("Sprint");
            Require(!GameInput.IsBindingPathValid(sprint, sprint.bindings[0], "<Mouse>/delta"), "Sprint incorrectly accepts a Vector2 control.");
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            GameLocalization.Set(label, "[{input:Cancel}] Kapat");
            binding = labelObject.GetComponent<LocalizedText>();
            // This isolated test runs outside Play; normal MonoBehaviour lifecycle isn't invoked.
            typeof(LocalizedText).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(binding, null);
            typeof(LocalizedText).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(binding, null);
            GameInput.Rebind("Cancel", "<Keyboard>/g");
            Require(GameInput.BindingName("Cancel").Equals("G", StringComparison.OrdinalIgnoreCase), "Display name did not follow G.");
            Require(label.text.Contains("G") && !label.text.Contains("{input:"), "Visible LocalizedText did not refresh on rebind.");
            if (simulateInput)
            {
                Require(Application.isPlaying, "Native gameplay input state must be tested in Play mode.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); InputSystem.Update();
                Require(GameInput.Pressed("Cancel") && GameInput.Held("Cancel"), "New G binding is not the real input.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Require(GameInput.Released("Cancel"), "Release edge was lost.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
                Require(!GameInput.Held("Cancel"), "Old F binding still triggers Cancel after rebinding.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            }
            var reloaded = InputActionAsset.FromJson(Resources.Load<TextAsset>("Localization/InputActions").text);
            try
            {
                reloaded.LoadBindingOverridesFromJson(PlayerPrefs.GetString(prefs));
                Require(reloaded.FindAction("Player/Cancel").bindings.Any(b => b.effectivePath == "<Keyboard>/g"), "Saved override did not survive loading into a fresh asset.");
            }
            finally { UnityEngine.Object.DestroyImmediate(reloaded); }
            GameInput.ReloadBindings();
            GameInput.ResetBindings(); Require(GameInput.BindingName("Cancel").Equals("F", StringComparison.OrdinalIgnoreCase), "Reset did not restore the imported default.");
            GameLocalization.SetLanguage("en");
            Require(GameLocalization.Format("[{input:Cancel}] Kapat {0}", 2).Contains("2"), "Input token interfered with numeric formatting.");
        }
        finally
        {
            if (binding != null) typeof(LocalizedText).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(binding, null);
            InputSystem.RemoveDevice(keyboard); UnityEngine.Object.DestroyImmediate(labelObject);
            InputSystem.settings = originalSettings; UnityEngine.Object.DestroyImmediate(isolatedSettings);
            if (hadSave) PlayerPrefs.SetString(prefs, original); else PlayerPrefs.DeleteKey(prefs); PlayerPrefs.Save();
            GameInput.ReloadBindings();
            GameLocalization.SetLanguage(language);
        }
    }
}
