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

// Play check: main menu hover/click and settings tabs/rows tick; notebook tabs and chalkboard controls paper-tap without hover; chalk, bond and pin sounds play.
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
        if (failure == null) Debug.Log("UI SOUND PLAY PASS: main menu and settings tick through SFX, repeats within 40 ms stay silent; notebook tabs and board controls paper-tap without hover; page flip, notebook fold/unfold, chalk place, bond connect/break, part wipe, target pin and the chalk scribble loop play."); else Debug.LogError(failure);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }

    static void Require(bool value, string message) { if (!value) throw new Exception("UI SOUND PLAY: " + message); }
    static IEnumerator Gap() { yield return new WaitForSecondsRealtime(.06f); }
    static void Heard(int before, int added, UISoundKind kind, string what) => Require(UISoundPlayer.PlayCount == before + added && UISoundPlayer.LastKind == kind,
        what + ": " + kind + " did not play (count " + before + "->" + UISoundPlayer.PlayCount + ", last " + UISoundPlayer.LastKind + ").");
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
        // The workspace builds its own "Senzora Workspace" canvas at the scene root, not under the controller; section tabs are named by their label.
        var tabViews = new Dictionary<string, string> { ["Notlar"] = "Notes", ["Parçalar"] = "Notes", ["Hedefler"] = "Targets", ["Bilgiler"] = "Information" };
        var tabs = UnityEngine.Object.FindObjectsByType<UISoundOnPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(h => tabViews.ContainsKey(h.name) && h.GetComponent<Button>() != null && h.transform.root.name == "Senzora Workspace").ToArray();
        Require(tabs.Length >= 3 && tabs.All(h => h.click == UISoundKind.PaperTap), "Notebook section tabs lack the PaperTap hook: " + string.Join(" | ", tabs.Select(State)));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var expanded = typeof(NotebookWorkspaceController).GetField("notebookExpanded", flags); var shown = typeof(NotebookWorkspaceController).GetField("currentNotebookView", flags);
        if (!(bool)expanded.GetValue(workspace)) workspace.ToggleNotebook();
        bool Live(UISoundOnPointer h) => h.isActiveAndEnabled && h.GetComponent<Selectable>().IsInteractable();
        UISoundOnPointer OtherTab() => tabs.FirstOrDefault(h => Live(h) && tabViews[h.name] != shown.GetValue(workspace).ToString());
        yield return WaitFor(() => OtherTab() != null, "Notebook tab interactable " + string.Join(" | ", tabs.Select(State)), 5);
        // Paper tap on press, then the view change turns the page.
        IEnumerator TabFlip(string what)
        {
            var tab = OtherTab(); Require(tab != null, what + ": no tab for another view.");
            yield return Gap(); int before = UISoundPlayer.PlayCount;
            ExecuteEvents.Execute(tab.gameObject, Pointer(), ExecuteEvents.pointerDownHandler); Heard(before, 1, UISoundKind.PaperTap, what + " press");
            ExecuteEvents.Execute(tab.gameObject, Pointer(), ExecuteEvents.pointerClickHandler);
            yield return null; Heard(before, 2, UISoundKind.PageFlip, what + " page flip");
        }
        yield return TabFlip("notebook tab click");
        // Machine UI: every control live at the chalkboard (board documents, notebook, HUD) carries the hook; the sweep covers code-built panels.
        yield return new WaitForSecondsRealtime(.3f);
        var live = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c.IsInteractable()).ToArray();
        Require(live.Length > 0, "No live control at the chalkboard.");
        var bare = live.Where(c => c.GetComponent<UISoundOnPointer>() == null).Select(c => Path(c.transform)).ToArray();
        Require(bare.Length == 0, "Live controls without hook: " + string.Join(", ", bare));
        Debug.Log("UI SOUND PLAY: " + live.Length + " live controls at the chalkboard all hooked, e.g. " + string.Join(", ", live.Take(6).Select(c => c.name)));
        // In-game paper control: no hover tick, a paper tap on press (pointer down only, so its action does not run).
        var paper = live.Select(c => c.GetComponent<UISoundOnPointer>()).FirstOrDefault(h => h.quiet && h.click == UISoundKind.PaperTap && h.GetComponent<Button>() != null && !tabViews.ContainsKey(h.name));
        Require(paper != null, "No quiet PaperTap control at the chalkboard: " + string.Join(", ", live.Take(12).Select(c => State(c.GetComponent<UISoundOnPointer>()))));
        yield return Gap(); count = UISoundPlayer.PlayCount; ExecuteEvents.Execute(paper.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
        Require(UISoundPlayer.PlayCount == count, "Paper control hover ticked: " + Path(paper.transform));
        ExecuteEvents.Execute(paper.gameObject, Pointer(), ExecuteEvents.pointerDownHandler); Heard(count, 1, UISoundKind.PaperTap, "paper control press " + Path(paper.transform));

        yield return TabFlip("notebook section page flip");
        yield return Gap(); count = UISoundPlayer.PlayCount; workspace.ToggleNotebook(); Heard(count, 1, UISoundKind.NotebookClose, "notebook fold");
        yield return new WaitForSecondsRealtime(.4f); count = UISoundPlayer.PlayCount; workspace.ToggleNotebook(); Heard(count, 1, UISoundKind.NotebookOpen, "notebook unfold");
        yield return new WaitForSecondsRealtime(.4f);

        // Chalk bonds between two free compatible ports, then wiping a part off the board.
        var parts = ResearchPuzzleTrial.Active.Content.Parts; Require(parts.Count > 0, "Research trial has no parts.");
        Vector2 center = Camera.main.WorldToScreenPoint(board.boardTransform.TransformPoint((board.minBounds + board.maxBounds) * .5f));
        yield return Gap(); count = UISoundPlayer.PlayCount;
        var spawned = Enumerable.Range(0, 4).Select(i => board.SpawnFormula(parts[i % parts.Count], center)).Where(f => f != null).ToList();
        Require(spawned.Count > 0 && UISoundPlayer.PlayCount > count && UISoundPlayer.LastKind == UISoundKind.ChalkPlace, "Part spawn: ChalkPlace did not play (last " + UISoundPlayer.LastKind + ").");
        yield return null;
        var pair = (from a in spawned from b in spawned where a != b from x in a.GetOwnNodes() from y in b.GetOwnNodes()
                    where x.CanConnect(b, y) && y.CanConnect(a, x) select new { x, y }).FirstOrDefault();
        Require(pair != null, "No free compatible port pair among " + spawned.Count + " spawned parts.");
        yield return Gap(); count = UISoundPlayer.PlayCount; pair.x.ConnectTo(pair.y); Heard(count, 1, UISoundKind.BondConnect, "bond connect");
        Require(pair.x.connectedNode == pair.y, "Bond did not connect.");
        yield return Gap(); count = UISoundPlayer.PlayCount; pair.x.Disconnect(); Heard(count, 1, UISoundKind.BondBreak, "bond break");
        yield return Gap(); count = UISoundPlayer.PlayCount; board.RemoveFormula(spawned[0]); Heard(count, 1, UISoundKind.BondBreak, "part removal");
        foreach (var part in spawned.Skip(1)) board.RemoveFormula(part);
        // Target card pinned to the cork board: pushpin thunk.
        var quest = ResearchPuzzleTrial.Active.Content.Quests.FirstOrDefault(); Require(quest != null, "Research trial has no target."); var previousQuest = board.PinnedQuest;
        yield return Gap(); count = UISoundPlayer.PlayCount; Require(board.TryPinQuest(quest.CreateQuest()), "Target did not pin."); Heard(count, 1, UISoundKind.Pin, "target pin");
        if (previousQuest != null) board.TryPinQuest(previousQuest);

        // Chalk scribble loop runs only while the stroke moves.
        var chalk = board.EnsureDrawing(); Require(chalk != null, "Chalkboard drawer missing.");
        chalk.BeginStroke(Vector2.zero, false); chalk.ContinueStroke(new Vector2(40, 0)); chalk.ContinueStroke(new Vector2(80, 10));
        yield return null; Require(player.ScribbleActive, "Chalk scribble loop did not start while drawing.");
        chalk.EndStroke(); yield return new WaitForSecondsRealtime(.5f);
        Require(!player.ScribbleActive, "Chalk scribble loop kept playing after the stroke ended.");
        chalk.Clear();
        if (game.activeInteraction != null) game.activeInteraction.ForceExit();
        yield return WaitFor(() => game.activeInteraction == null, "Chalkboard exit");
    }
}
