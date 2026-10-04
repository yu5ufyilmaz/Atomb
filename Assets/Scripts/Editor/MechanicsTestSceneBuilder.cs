using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class MechanicsTestSceneBuilder
{
    public const string SourceScenePath = "Assets/Scenes/scn_labratory.unity";
    public const string TestScenePath = "Assets/Scenes/scn_MechanicsTest.unity";

    [MenuItem("Tools/Atomb/Mechanics Test/Rebuild Test Scene")]
    public static void RebuildTestScene()
    {
        RebuildTestSceneInternal(showDialogs: true);
    }

    [MenuItem("Tools/Atomb/Mechanics Test/Open Test Scene")]
    public static void OpenTestScene()
    {
        if (!System.IO.File.Exists(TestScenePath))
        {
            RebuildTestScene();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
    }

    // Komut satırı/CI doğrulaması için diyalogsuz giriş noktası.
    public static void RebuildTestSceneBatch()
    {
        RebuildTestSceneInternal(showDialogs: false);
    }

    private static void RebuildTestSceneInternal(bool showDialogs)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Test sahnesi Play Mode sırasında yeniden oluşturulamaz.");
            return;
        }

        if (!System.IO.File.Exists(SourceScenePath))
            throw new InvalidOperationException($"Ana sahne bulunamadı: {SourceScenePath}");

        if (showDialogs && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null)
        {
            bool overwrite =
                !showDialogs
                || EditorUtility.DisplayDialog(
                    "Mekanik Test Sahnesi",
                    "Mevcut test sahnesi güncel ana sahneden yeniden üretilecek. Devam edilsin mi?",
                    "Yeniden Oluştur",
                    "Vazgeç"
                );
            if (!overwrite)
                return;

            if (!AssetDatabase.DeleteAsset(TestScenePath))
                throw new InvalidOperationException($"Eski test sahnesi silinemedi: {TestScenePath}");
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, TestScenePath))
            throw new InvalidOperationException("Ana sahne kopyalanamadı.");

        AssetDatabase.ImportAsset(TestScenePath, ImportAssetOptions.ForceSynchronousImport);
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);

        GameObject bootstrap = new GameObject("__MECHANICS_TEST__");
        bootstrap.AddComponent<MechanicsTestController>();

        foreach (SaveManager saveManager in FindSceneObjects<SaveManager>(scene))
            saveManager.gameObject.SetActive(false);

        foreach (TutorialManager tutorial in FindSceneObjects<TutorialManager>(scene))
            tutorial.enabled = false;

        foreach (IntroCutsceneManager intro in FindSceneObjects<IntroCutsceneManager>(scene))
            intro.enabled = false;

        foreach (DebugMenuManager debugMenu in FindSceneObjects<DebugMenuManager>(scene))
            debugMenu.enabled = false;

        foreach (MainMenuManager mainMenu in FindSceneObjects<MainMenuManager>(scene))
            mainMenu.HideAllMenusForMechanicsTest();

        foreach (EventLogicController controller in FindSceneObjects<EventLogicController>(scene))
            controller.enabled = false;

        foreach (WeightedRandomTrigger trigger in FindSceneObjects<WeightedRandomTrigger>(scene))
            trigger.enabled = false;

        foreach (PlayableDirector director in FindSceneObjects<PlayableDirector>(scene))
        {
            director.playOnAwake = false;
            director.Stop();
        }

        foreach (PlayerInteraction interaction in FindSceneObjects<PlayerInteraction>(scene))
            interaction.isTutorialMode = false;

        foreach (GameManager gameManager in FindSceneObjects<GameManager>(scene))
        {
            gameManager.isGameStarted = true;
            gameManager.isGamePaused = false;
        }

        foreach (PressureSystemManager pressure in FindSceneObjects<PressureSystemManager>(scene))
        {
            pressure.isSystemActive = false;
            pressure.currentPressure = 0f;
            pressure.overridePostProcessing = false;
        }

        foreach (BreakerBox breaker in FindSceneObjects<BreakerBox>(scene))
            breaker.isSystemActive = false;

        foreach (GlobalEnemyManager enemies in FindSceneObjects<GlobalEnemyManager>(scene))
            enemies.stopAllEnemies = true;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, TestScenePath))
            throw new InvalidOperationException("Test sahnesi kaydedilemedi.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
        Selection.activeObject = sceneAsset;
        EditorGUIUtility.PingObject(sceneAsset);

        Debug.Log(
            $"[MechanicsTest] Test sahnesi üretildi: {TestScenePath}\n"
                + "F1 ile test panelini açıp kapatabilirsiniz. Sahne Build Settings'e eklenmedi."
        );
    }

    private static T[] FindSceneObjects<T>(Scene scene)
        where T : UnityEngine.Object
    {
        return UnityEngine.Object
            .FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => GetScene(item) == scene)
            .ToArray();
    }

    private static Scene GetScene(UnityEngine.Object item)
    {
        switch (item)
        {
            case Component component:
                return component.gameObject.scene;
            case GameObject gameObject:
                return gameObject.scene;
            default:
                return default;
        }
    }
}
