using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 4 Kasım oyun testi notları 3, 6, 8: kapalı hedef kartı özeti, Hedef sayfasından kaydın sayfasına gitme, Parçalar grup başlıkları.
public static class PlaytestNotebookBoardChecks
{
    const string Folder = "outputs/playtest-notebook-board-2026-11-04";
    const string Result = Folder + "/result.txt";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    static void Require(bool value, string message)
    {
        if (!value) { File.AppendAllText(Result, "FAIL " + message + "\n"); throw new InvalidOperationException("PLAYTEST NOTEBOOK BOARD: " + message); }
    }
    static void Log(string line) => File.AppendAllText(Result, line + "\n");
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    static void CheckHeader(ChalkboardDocuments documents, QuestDiagramGraphic diagram, PuzzleKind kind, string code, bool oneLine = true)
    {
        var title = Field<TextMeshProUGUI>(documents, "title"); var toggle = Field<TextMeshProUGUI>(documents, "shadeToggleLabel");
        title.ForceMeshUpdate(); toggle.ForceMeshUpdate();
        // Kısa başlık tek satır; uzun başlık (eğitim: "İlk İz / Tahta Alıştırması") en çok iki satır, kesilmeden ve düğmeye taşmadan.
        Require(title.textInfo.lineCount <= (oneLine ? 1 : 2) && (oneLine || title.fontSize >= 10) && !Enumerable.Range(0, title.textInfo.characterCount).Any(i => title.textInfo.characterInfo[i].character == '…') && title.textBounds.size.x <= title.rectTransform.rect.width + 1,
            code + " title '" + title.text + "' wraps, truncates or overflows (" + title.textInfo.lineCount + " lines, " + title.textBounds.size.x + " > " + title.rectTransform.rect.width + ")");
        Require(title.rectTransform.anchoredPosition.x + title.textBounds.max.x < toggle.rectTransform.anchoredPosition.x + toggle.textBounds.min.x,
            code + " title runs under the toggle label.");
        Require(toggle.textInfo.lineCount <= 2 && WholeWords(toggle), code + " toggle '" + toggle.text + "' breaks mid-word.");
        string values = "";
        if (kind == PuzzleKind.ColorMixture)
        {
            var label = diagram.GetComponentsInChildren<TextMeshProUGUI>().First(t => t.name == "Karışım Değerleri");
            label.ForceMeshUpdate();
            Require(label.textInfo.lineCount == 2 && label.textBounds.size.x <= label.rectTransform.rect.width + 1,
                code + " mixture values not two fitting lines: " + label.textInfo.lineCount + " lines, " + label.textBounds.size.x + " > " + label.rectTransform.rect.width);
            values = ", values " + label.fontSize.ToString("0.#") + "pt";
        }
        Log("header " + kind + " " + code + " PASS: title " + title.textInfo.lineCount + " lines " + title.fontSize.ToString("0.#") + "pt, toggle " + toggle.textInfo.lineCount + " lines " + toggle.fontSize.ToString("0.#") + "pt" + values);
    }

    static bool WholeWords(TMP_Text text)
    {
        var info = text.textInfo;
        for (int i = 0; i < info.lineCount - 1; i++)
        {
            int last = info.lineInfo[i].lastCharacterIndex;
            char next = last + 1 < info.characterCount ? info.characterInfo[last + 1].character : ' ';
            if (!char.IsWhiteSpace(info.characterInfo[last].character) && !char.IsWhiteSpace(next)) return false;
        }
        return true;
    }

    // Fener: bir şarj (15) ≈4.3 sn tam parlak, son %2'de (~0.67 sn) söner.
    [MenuItem("Senzora/Doğrulama/Oyun Testi Fener Sönme Play")]
    public static void Flashlight()
    {
        Require(Application.isPlaying && GameManager.Instance.isGameStarted, "Started game in Play is required.");
        GameManager.Instance.StartCoroutine(FlashlightFlow());
    }

    static IEnumerator FlashlightFlow()
    {
        const string file = Folder + "/flashlight.txt";
        Directory.CreateDirectory(Folder); File.WriteAllText(file, "");
        if (BreakerBox.Instance != null) BreakerBox.Instance.isSystemActive = false;
        var lamp = UnityEngine.Object.FindFirstObjectByType<DynomaFlashLight>();
        var light = Field<Light>(lamp, "targetLight");
        SetField(lamp, "currentEnergy", 15f);
        float start = Time.time, fullUntil = 0, offAt = -1;
        while (Time.time - start < 6)
        {
            float t = Time.time - start;
            File.AppendAllText(file, $"{t:0.0}s intensity {(light.enabled ? light.intensity : 0):0} energy {Field<float>(lamp, "currentEnergy"):0.00}\n");
            for (float next = t + .5f; Time.time - start < next;)
            {
                yield return null;
                if (light.enabled && light.intensity >= Field<float>(lamp, "maxIntensity") - .01f) fullUntil = Time.time - start;
                if (offAt < 0 && !light.enabled) offAt = Time.time - start;
            }
        }
        File.AppendAllText(file, $"full until {fullUntil:0.00}s, off at {offAt:0.00}s\n");
        Require(fullUntil > 4.1f && fullUntil < 4.5f && offAt > 4.9f && offAt < 5.3f, $"Flashlight fade: full until {fullUntil:0.00}s, off at {offAt:0.00}s");
        File.AppendAllText(file, "PASS\n");
    }

    [MenuItem("Senzora/Doğrulama/Oyun Testi Defter Tahta Play")]
    public static void Run()
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Play in the laboratory scene is required.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Result, "RUNNING " + DateTime.UtcNow.ToString("O") + "\n");
        SaveManager.Instance.sessionOnly = true;
        ResearchPuzzleTrial.Active.StartCoroutine(Flow());
    }

    static IEnumerator Shot(string name)
    {
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png");
        yield return null; yield return null;
        Log("shot " + Folder + "/" + name + ".png");
    }

    static IEnumerator Flow()
    {
        Application.runInBackground = true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var game = GameManager.Instance;
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
        float deadline;
        if (!game.isGameStarted && menu != null)
        {
            menu.PlayGame(); yield return null;
            if (menu.IsTutorialChoiceOpen) menu.ChooseTutorial(false);
            deadline = Time.realtimeSinceStartup + 120;
            while (!game.isGameStarted && Time.realtimeSinceStartup < deadline) yield return null;
        }
        Require(game.isGameStarted, "New game did not start.");
        yield return new WaitForSecondsRealtime(2);
        var interaction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        Require(interaction != null, "Research chalkboard missing.");
        if (!ChalkboardManager.Instance.isMachineActive) interaction.Interact();
        deadline = Time.realtimeSinceStartup + 20;
        while ((!ChalkboardManager.Instance.isMachineActive || NotebookWorkspaceController.Instance == null || !NotebookWorkspaceController.Instance.IsActive)
            && Time.realtimeSinceStartup < deadline) yield return null;
        Require(ChalkboardManager.Instance.isMachineActive, "Chalkboard did not open.");
        yield return new WaitForSecondsRealtime(1);

        var trial = ResearchPuzzleTrial.Active;
        var board = ChalkboardManager.Instance;
        var documents = board.EnsureDocuments();
        var card = Field<RectTransform>(documents, "targetArea");
        var diagram = Field<QuestDiagramGraphic>(documents, "diagram");
        var directions = Field<RectTransform>(documents, "goalDirectionsViewport");

        // Not 3: kapalı kartta hedefin şekli/rengi görünür, ipucu metni gizli; açılınca eski büyük kart.
        foreach (var kind in new[] { PuzzleKind.Signal, PuzzleKind.ColorMixture })
        {
            var puzzle = trial.Puzzles.FirstOrDefault(p => p.kind == kind);
            if (puzzle == null) { Log("no " + kind + " puzzle in this session; skipped"); continue; }
            Require(board.TryPinQuest(trial.Content.Quests.Find(q => q.questID == puzzle.id).CreateQuest()), "Could not pin " + puzzle.title);
            documents.SetDocumentPanelOpen(false, true);
            var layout = new SynthesisBoardSaveData(); documents.SaveLayout(layout); documents.LoadLayout(layout); documents.Refresh();
            yield return null; Canvas.ForceUpdateCanvases();
            Require(Mathf.Approximately(card.rect.height, 120), kind + " collapsed card height " + card.rect.height);
            Require(diagram.gameObject.activeInHierarchy && !directions.gameObject.activeSelf, kind + " collapsed card must show the drawing and hide the clue text.");
            var r = diagram.rectTransform; Vector2 lo = (Vector2)card.InverseTransformPoint(r.TransformPoint(r.rect.min)), hi = (Vector2)card.InverseTransformPoint(r.TransformPoint(r.rect.max));
            Require(card.rect.Contains(lo) && card.rect.Contains(hi), kind + " drawing leaves the collapsed card.");
            if (kind == PuzzleKind.ColorMixture)
                Require(diagram.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Karışım Değerleri" && t.text.Length > 0), "Colour target values missing on the collapsed card.");
            else Require(diagram.GetComponentInChildren<SignalTrialGraphic>() != null, "Signal drawing missing on the collapsed card.");
            Log("collapsed " + kind + " PASS: " + puzzle.title);
            // Başlık tek satır, düğme yalnız boşluktan bölünür, karışım değerleri iki sabit satır: iki dilde.
            string language = GameLocalization.CurrentCode;
            foreach (var code in new[] { "tr", "en" })
            {
                GameLocalization.SetLanguage(code); yield return null; Canvas.ForceUpdateCanvases();
                CheckHeader(documents, diagram, kind, GameLocalization.CurrentCode);
                yield return Shot("collapsed-" + kind + "-" + code);
            }
            if (kind == PuzzleKind.Signal)
            {
                var tutorial = trial.Content.Quests.Find(q => q.questID == puzzle.id).CreateQuest();
                tutorial.title = ChalkboardTutorial.CreatePuzzle().title; // eğitim tahtasının en uzun başlığı
                Require(board.TryPinQuest(tutorial), "Could not pin the long-title quest.");
                foreach (var code in new[] { "tr", "en" })
                {
                    GameLocalization.SetLanguage(code); yield return null; Canvas.ForceUpdateCanvases();
                    CheckHeader(documents, diagram, kind, GameLocalization.CurrentCode, false);
                    yield return Shot("collapsed-long-title-" + code);
                }
                Require(board.TryPinQuest(trial.Content.Quests.Find(q => q.questID == puzzle.id).CreateQuest()), "Could not re-pin " + puzzle.title);
            }
            GameLocalization.SetLanguage(language); yield return null;
            documents.SetDocumentPanelOpen(true, true); yield return null;
            Require(Mathf.Approximately(card.rect.height, 246) && directions.gameObject.activeSelf, kind + " expanded card changed.");
            yield return Shot("expanded-" + kind);
            documents.SetDocumentPanelOpen(false, true);
        }

        // Not 6: Hedef sayfasındaki toplanmış numune çipi Parçalar sayfasına, bilgi satırı Bilgiler sayfasına götürür.
        var workspace = NotebookWorkspaceController.Instance;
        var store = typeof(NotebookWorkspaceController).GetMethod("StoreArchiveNote", Private);
        var target = trial.Puzzles.First();
        var notes = trial.Content.Books.Where(b => b != null).SelectMany(b => b).Where(p => p?.blocks != null).SelectMany(p => p.blocks).Where(n => n != null).ToList();
        var quest = notes.First(n => n.blockType == NotebookBlockType.Quest && n.questID == target.id);
        store.Invoke(workspace, new object[] { quest });
        foreach (var note in notes.Where(n => n.blockType == NotebookBlockType.PartClue || n.blockType == NotebookBlockType.RuleClue && n.questID == target.id))
            store.Invoke(workspace, new object[] { note });
        var quests = Field<NotebookUI>(workspace, "notebook").EnsureCollectedQuests();
        SetField(workspace, "targetArchivePage", quests.FindIndex(n => n.questID == target.id));
        workspace.ShowCollectedTargets(); yield return null;
        var targetContent = Field<RectTransform>(workspace, "targetContent");
        var chip = targetContent.GetComponentsInChildren<NotebookDraggableBlock>().FirstOrDefault(b => b.onClick != null && b.Data.blockType == NotebookBlockType.PartClue);
        Require(chip != null, "No clickable collected sample chip on the target page of " + target.title);
        yield return Shot("target-page");
        // Sürükleme tıklama sayılmaz.
        var press = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
        chip.OnBeginDrag(press); press.dragging = true; chip.OnPointerClick(press);
        Require(Field<object>(workspace, "currentNotebookView").ToString() == "Targets", "A drag was treated as a click.");
        chip.GetType().GetMethod("CancelDrag", Private).Invoke(chip, null);
        string itemID = chip.Data.itemID;
        ExecuteEvents.Execute(chip.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        yield return null;
        Require(Field<object>(workspace, "currentNotebookView").ToString() == "Notes", "Chip click did not open the parts page.");
        Require(Field<List<NotebookDraggableBlock>>(workspace, "pageBlocks").Exists(b => b != null && b.Data.itemID == itemID), "Parts page does not show " + itemID);
        Log("chip click PASS: " + itemID + " on parts page " + Field<int>(workspace, "partsArchivePage"));
        yield return new WaitForSecondsRealtime(.6f);
        yield return Shot("chip-to-part");

        workspace.ShowCollectedTargets(); yield return null;
        var row = targetContent.GetComponentsInChildren<NotebookDraggableBlock>().FirstOrDefault(b => b.onClick != null && b.Data.blockType == NotebookBlockType.RuleClue);
        if (row == null) Log("no collected info row on the target page; info click not checked");
        else
        {
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
            Require(Field<object>(workspace, "currentNotebookView").ToString() == "Information", "Info row click did not open the information page.");
            Log("info row click PASS: " + row.Data.title + " on information page " + Field<int>(workspace, "informationArchivePage"));
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("row-to-information");
        }

        // Not 8: Parçalar grup başlığı o grubun ilk sayfasına götürür.
        workspace.GoToPage(0); yield return null;
        var partsContent = Field<RectTransform>(workspace, "partsContent");
        var header = partsContent.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponent<TMP_Text>() != null && b.GetComponent<TMP_Text>().fontStyle != FontStyles.Bold);
        Require(header != null, "No clickable unselected group header on the parts page.");
        string group = header.GetComponent<TMP_Text>().text;
        int before = Field<int>(workspace, "partsArchivePage");
        ExecuteEvents.Execute(header.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        yield return null;
        int after = Field<int>(workspace, "partsArchivePage");
        Require(after != before, "Group header click did not change the parts page.");
        Log("group header PASS: " + group + " page " + before + " -> " + after);
        yield return new WaitForSecondsRealtime(.6f);
        yield return Shot("group-header");
        Log("PASS");
        Debug.Log("PLAYTEST NOTEBOOK BOARD PASS: " + Result);
    }
}
