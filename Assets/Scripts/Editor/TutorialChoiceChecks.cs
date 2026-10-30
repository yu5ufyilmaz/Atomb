using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TutorialChoiceChecks
{
    static int assertions;
    const string Result = "outputs/tutorial-choice-checks.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message)
    {
        assertions++;
        if (!value) { Directory.CreateDirectory("outputs"); File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("TUTORIAL CHOICE: " + message); }
    }
    [MenuItem("Senzora/Doğrulama/Öğretici Seçimi - Arayüz")]
    public static void Contract()
    {
        assertions = 0; File.WriteAllText(Result, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
        Require(Type.GetType("TutorialChoicePrompt, Assembly-CSharp") != null, "New Game must offer an explicit tutorial choice.");
        Require(typeof(MainMenuManager).GetMethod("ChooseTutorial") != null && typeof(MainMenuManager).GetMethod("CancelTutorialChoice") != null,
            "Both choices and returning to the menu must be wired to the real new-game flow.");
        File.AppendAllText(Result, "PASS contract " + assertions + "\n");
    }
    [MenuItem("Senzora/Doğrulama/Öğretici Seçimi - Evet Play")]
    public static void Yes() => Start(true);
    [MenuItem("Senzora/Doğrulama/Öğretici Seçimi - Hayır Play")]
    public static void No() => Start(false);
    static void Start(bool tutorial)
    {
        Require(Application.isPlaying && !GameManager.Instance.isGameStarted, "Fresh Play at the main menu is required.");
        assertions = 0; File.WriteAllText(Result, "RUNNING " + (tutorial ? "YES" : "NO") + " " + DateTime.UtcNow.ToString("O") + "\n");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        menu.StartCoroutine(Flow(menu, tutorial));
    }
    static IEnumerator Flow(MainMenuManager menu, bool tutorial)
    {
        Application.runInBackground = true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        SaveManager.Instance.sessionOnly = true;
        var game = GameManager.Instance; var manager = TutorialManager.Instance;
        var menuPanel = (GameObject)typeof(MainMenuManager).GetField("mainMenuPanel", Private).GetValue(menu);
        var selected = typeof(MainMenuManager).GetMethod("ChooseTutorial");
        var cancel = typeof(MainMenuManager).GetMethod("CancelTutorialChoice");
        var open = typeof(MainMenuManager).GetProperty("IsTutorialChoiceOpen");
        Require(selected != null && cancel != null && open != null, "Choice must be exposed by the actual menu.");
        bool initialTutorial = manager.isTutorialEnabled;
        menu.PlayGame(); yield return null;
        Require((bool)open.GetValue(menu) && !game.isGameStarted && menuPanel.activeInHierarchy, "New Game must wait at the choice before starting.");
        var promptType = Type.GetType("TutorialChoicePrompt, Assembly-CSharp");
        var prompt = (Component)UnityEngine.Object.FindFirstObjectByType(promptType);
        Require(prompt != null, "Visible choice prompt.");
        menu.PlayGame();
        Require(UnityEngine.Object.FindObjectsByType(promptType, FindObjectsSortMode.None).Length == 1, "Repeated New Game must not duplicate the prompt.");
        string language = GameLocalization.CurrentCode;
        foreach (string code in new[] { "tr", "en" })
        {
            GameLocalization.SetLanguage(code); Canvas.ForceUpdateCanvases();
            foreach (var text in prompt.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Prompt text overflow: " + text.text); }
            ScreenCapture.CaptureScreenshot("outputs/tutorial-choice-" + code + ".png");
            yield return new WaitForEndOfFrame(); yield return null;
        }
        GameLocalization.SetLanguage(language);
        prompt.GetComponentsInChildren<Button>().Single(button => button.name == "Back").onClick.Invoke(); yield return null;
        Require(!(bool)open.GetValue(menu) && !game.isGameStarted && manager.isTutorialEnabled == initialTutorial,
            "Back must preserve the tutorial setting and leave the game at the menu.");
        Require(menuPanel.GetComponent<CanvasGroup>().interactable, "Back must restore menu interaction.");
        menu.PlayGame(); yield return null;
        var movement = UnityEngine.Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        prompt.GetComponentsInChildren<Button>().Single(button => button.name == (tutorial ? "Yes" : "No")).onClick.Invoke();
        Require(!(bool)open.GetValue(menu) && manager.isTutorialEnabled == tutorial, "Selected answer must control the tutorial.");
        Require(movement.IsFrozen, "Skipping the tutorial must not unlock walking during the opening cutscene.");
        if (ResearchPuzzleTrial.Active != null)
            Require(ResearchPuzzleTrial.Active.Puzzles.Any(puzzle => puzzle.id == ChalkboardTutorial.QuestID) == tutorial,
                "Only the tutorial choice may add the tutorial puzzle to research books.");
        selected.Invoke(menu, new object[] { !tutorial });
        Require(manager.isTutorialEnabled == tutorial, "Double submission must not change the accepted answer.");
        float deadline = Time.realtimeSinceStartup + 90;
        while ((!game.isGameStarted || movement.IsFrozen) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(game.isGameStarted && !movement.IsFrozen, "Opening must finish with normal player control.");
        Require(manager.IsRunning == tutorial && PlayerInteraction.Instance.isTutorialMode == tutorial, "Real tutorial events and interaction restrictions must follow the answer.");
        if (!tutorial)
            Require(BreakerBox.Instance.isSystemActive && PressureSystemManager.Instance.isSystemActive && !GlobalEnemyManager.Instance.stopAllEnemies,
                "Skipping must enable the normal game's systems.");
        File.AppendAllText(Result, "PASS " + (tutorial ? "YES" : "NO") + " " + assertions + " Play assertions\n" + DateTime.UtcNow.ToString("O"));
        File.Copy(Result, "outputs/tutorial-choice-" + (tutorial ? "yes" : "no") + "-" + game.gameObject.scene.name + "-checks.txt", true);
        Debug.Log("TUTORIAL CHOICE PLAY PASS: " + tutorial + "; " + assertions + " assertions.");
    }
}
