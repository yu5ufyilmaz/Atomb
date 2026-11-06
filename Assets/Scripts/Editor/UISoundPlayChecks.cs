using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Play check: main menu hover/click, settings tabs and rows, notebook section tabs and the chalkboard document buttons all tick.
[InitializeOnLoad]
public static class UISoundPlayChecks
{
    const string Pending = "Senzora.UISound.PlayChecks", Scene = "Assets/Scenes/scn_LaboratorySynthesis.unity", Result = "Temp/ui-sound-play.result";
    static UISoundPlayChecks() { EditorApplication.playModeStateChanged += StateChanged; }

    [MenuItem("Senzora/Doğrulama/UI Tık Sesleri (Play)")]
    public static void Start()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Start UI sound checks outside Play mode.");
        if (SceneManager.GetActiveScene().path != Scene)
        {
            if (EditorSceneManager.GetSceneManagerSetup().Any(s => SceneManager.GetSceneByPath(s.path).isDirty)) throw new InvalidOperationException("Save open scenes before UI sound checks.");
            EditorSceneManager.OpenScene(Scene);
        }
        File.Delete(Result); SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.delayCall += () => SettingsManager.EnsureInstance().StartCoroutine(Watch());
        }
    }

    static IEnumerator Watch()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Checks()); string failure = null;
        while (stack.Count > 0)
        {
            var iterator = stack.Peek(); bool next = false; try { next = iterator.MoveNext(); } catch (Exception error) { failure = error.ToString(); }
            if (failure != null) break; if (!next) { stack.Pop(); continue; }
            if (iterator.Current is IEnumerator child) stack.Push(child); else yield return iterator.Current;
        }
        File.WriteAllText(Result, failure == null ? "PASS " + DateTime.Now.ToString("s") : "FAIL " + failure);
        if (failure == null) Debug.Log("UI SOUND PLAY PASS: main menu, settings tabs/rows, notebook tabs, chalkboard buttons tick through SFX; repeats within 40 ms stay silent."); else Debug.LogError(failure);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }

    static void Require(bool value, string message) { if (!value) throw new Exception("UI SOUND PLAY: " + message); }
    static IEnumerator Gap() { yield return new WaitForSecondsRealtime(.06f); }
    static PointerEventData Pointer() => new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
    static IEnumerator Expect(GameObject target, bool click, UISoundKind kind, string what)
    {
        yield return Gap();
        int before = UISoundPlayer.PlayCount;
        Require(target.activeInHierarchy, what + ": target inactive: " + Path(target.transform));
        if (click) ExecuteEvents.Execute(target, Pointer(), ExecuteEvents.pointerDownHandler); else ExecuteEvents.Execute(target, Pointer(), ExecuteEvents.pointerEnterHandler);
        Require(UISoundPlayer.PlayCount == before + 1 && UISoundPlayer.LastKind == kind && UISoundPlayer.Instance.Source.isPlaying, what + ": " + kind + " tick did not play (count " + before + "->" + UISoundPlayer.PlayCount + ", last " + UISoundPlayer.LastKind + ").");
        // The tick rides on pointer down; the release click still has to run the button's own action.
        if (click) ExecuteEvents.Execute(target, Pointer(), ExecuteEvents.pointerClickHandler);
    }
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    static string State(UISoundOnPointer h) => Path(h.transform) + "[active=" + h.isActiveAndEnabled + ", interactable=" + h.GetComponent<Selectable>().IsInteractable() + ", kind=" + h.click + "]";
    static IEnumerator WaitFor(Func<bool> done, string what, float seconds = 20)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!done() && Time.realtimeSinceStartup < end) yield return null;
        Require(done(), what + " did not happen within " + seconds + " s.");
    }

    static IEnumerator Checks()
    {
        SaveManager.Instance.sessionOnly = true;
        yield return null; yield return null;
        var player = UISoundPlayer.EnsureInstance(); yield return null;
        Require(player.Source != null && player.Source.spatialBlend == 0 && player.Source.ignoreListenerPause, "2D UI audio source missing.");

        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>(); Require(menu != null, "Main menu missing.");
        var panel = new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject; Require(panel != null && panel.activeInHierarchy, "Main menu panel not open.");
        var buttons = panel.GetComponentsInChildren<Button>().Where(b => b.IsInteractable()).ToArray(); Require(buttons.Length > 0, "No interactable main menu button.");
        foreach (var b in buttons) Require(b.GetComponent<UISoundOnPointer>() != null, "Main menu button without hook: " + b.name);
        yield return Expect(buttons[0].gameObject, false, UISoundKind.Hover, "main menu hover");
        int count = UISoundPlayer.PlayCount; ExecuteEvents.Execute(buttons[0].gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
        Require(UISoundPlayer.PlayCount == count, "Hover repeated within 40 ms.");
        Require(player.Source.outputAudioMixerGroup != null && player.Source.outputAudioMixerGroup == SettingsManager.EnsureInstance().effectsGroup, "UI ticks are not routed through the SFX group.");

        var settings = buttons.FirstOrDefault(b => string.Equals(b.name, "Settings", StringComparison.OrdinalIgnoreCase)); Require(settings != null, "Settings button missing.");
        yield return Expect(settings.gameObject, true, UISoundKind.Click, "settings button click");
        yield return null;
        var view = SettingsMenuView.Open; Require(view != null, "Settings did not open from the click.");
        var tab = view.transform.Find("SettingsStage/Tab_audio"); Require(tab != null && tab.GetComponent<UISoundOnPointer>()?.click == UISoundKind.Tab, "Audio tab lacks the Tab hook.");
        yield return Expect(tab.gameObject, true, UISoundKind.Tab, "settings tab click");
        yield return null; Require(view.Section == "audio", "Tab click did not switch the section.");
        yield return null; yield return null;
        var row = view.Rows.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.IsInteractable()); Require(row != null && row.GetComponent<UISoundOnPointer>() != null, "Settings row control lacks the hook.");
        yield return Expect(row.gameObject, false, UISoundKind.Hover, "settings row hover");
        menu.CloseSettings(); yield return null;

        yield return ResearchGuidanceChecks.EnterNormalGame(false);
        // The free-play standalone notebook is keyboard-only (Q/E, locked cursor); its tabs take the mouse at the chalkboard.
        var game = GameManager.Instance; var boardInteraction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>(); Require(boardInteraction != null, "Chalkboard missing.");
        var board = ResearchPuzzleTrial.Active != null ? ResearchPuzzleTrial.Active.Board : null; Require(board != null, "Chalkboard board missing.");
        boardInteraction.Interact(); yield return WaitFor(() => board.isMachineActive, "Chalkboard entry");
        yield return new WaitForSecondsRealtime(.5f);
        var workspace = NotebookWorkspaceController.Instance; Require(workspace != null && workspace.IsActive, "Notebook workspace not open at the chalkboard.");
        // The workspace builds its own "Senzora Workspace" canvas at the scene root, not under the controller.
        var tabs = UnityEngine.Object.FindObjectsByType<UISoundOnPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(h => h.click == UISoundKind.Tab && h.GetComponent<Button>() != null && h.transform.root.name == "Senzora Workspace").ToArray();
        Require(tabs.Length >= 3, "Notebook section tabs lack the Tab hook (" + tabs.Length + ").");
        var expanded = typeof(NotebookWorkspaceController).GetField("notebookExpanded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (!(bool)expanded.GetValue(workspace)) workspace.ToggleNotebook();
        yield return WaitFor(() => tabs.Any(h => h.isActiveAndEnabled && h.GetComponent<Selectable>().IsInteractable()), "Notebook tab interactable " + string.Join(" | ", tabs.Select(State)), 5);
        var targets = tabs.First(h => h.isActiveAndEnabled && h.GetComponent<Selectable>().IsInteractable());
        yield return Expect(targets.gameObject, true, UISoundKind.Tab, "notebook tab click");
        // Machine UI: every control live at the chalkboard (board documents, notebook, HUD) carries the hook; the sweep covers code-built panels.
        yield return new WaitForSecondsRealtime(.3f);
        var live = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c.IsInteractable()).ToArray();
        Require(live.Length > 0, "No live control at the chalkboard.");
        var bare = live.Where(c => c.GetComponent<UISoundOnPointer>() == null).Select(c => Path(c.transform)).ToArray();
        Require(bare.Length == 0, "Live controls without hook: " + string.Join(", ", bare));
        Debug.Log("UI SOUND PLAY: " + live.Length + " live controls at the chalkboard all hooked, e.g. " + string.Join(", ", live.Take(6).Select(c => c.name)));
        var other = live.FirstOrDefault(c => c.gameObject != targets.gameObject) ?? live[0];
        yield return Expect(other.gameObject, false, UISoundKind.Hover, "chalkboard control hover");
        if (game.activeInteraction != null) game.activeInteraction.ForceExit();
        yield return WaitFor(() => game.activeInteraction == null, "Chalkboard exit");
    }
}
