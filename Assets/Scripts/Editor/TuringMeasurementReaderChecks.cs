using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

// Explicit playtest commands; never called by normal game startup.
public static class TuringMeasurementReaderChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Kağıt Okuyucuyu Doğrula")]
    public static void Run()
    {
        var machine = SecondTraceExperiment.Active?.terminal;
        Check(Application.isPlaying && machine != null && GameManager.Instance.activeInteraction == machine,
            "Collect an output, then open the test terminal first.");
        machine.StartCoroutine(Verify(machine));
    }

    private static IEnumerator Verify(InteractableTuringMachine machine)
    {
        var workspace = NotebookWorkspaceController.Instance;
        var notebook = NotebookUI.Instance;
        Check(workspace.IsActive && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Notebook or pointer unavailable in terminal view.");
        Check(!Code(machine).StartsWith("ECHOECHO"), "The code was still filled automatically at startup.");
        var original = new GameData(); notebook.SaveData(ref original);
        string snapshot = JsonUtility.ToJson(original);
        var slot = (Transform)Field(machine, "measurementReader");
        var ink = (TuringCrtInk)Field(machine, "screenInk");
        Check(ink != null && slot.GetComponentsInChildren<Canvas>().Length == 0,
            "CRT uses an overlay or the paper transport still contains world UI.");
        var body = Array.Find(machine.GetComponentsInChildren<MeshRenderer>(), m => m.name == "turing-machine.1");
        Check(body != null && body.sharedMaterials[1].GetTexture("_BaseColorMap") is RenderTexture
            && body.sharedMaterials[1].GetTexture("_EmissiveColorMap") is RenderTexture,
            "The original CRT material is not receiving its small live readout.");
        Vector2 point = Camera.main.WorldToScreenPoint(slot.position);
        Check(point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height, "Paper reader is outside the camera view.");
        Check(!machine.TryInsertMeasurement(new NotebookBlockData { blockType = NotebookBlockType.RuleClue, itemID = "trace-note/test" }, point),
            "A book clue was accepted as a measurement.");
        // Use the archived failed attempt to prove paper reading doesn't turn it into a valid solution.
        typeof(NotebookWorkspaceController).GetField("solutionsArchivePage", Private).SetValue(workspace, 0);
        workspace.ShowSolutions(); Canvas.ForceUpdateCanvases();
        var content = (RectTransform)Field(workspace, "informationContent");
        var notes = content.GetComponentsInChildren<NotebookDraggableBlock>();
        Check(notes.Length > 0, "Collect measurement cards before testing the reader.");
        var note = notes[notes.Length - 1];
        Check(note != null && !note.IsSource && SecondTraceExperiment.Active.TryGetMeasurementCode(note.Data, out _), "No collected measurement is available.");
        SecondTraceExperiment.Active.TryGetMeasurementCode(note.Data, out string wanted);
        var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, note.transform.position) };
        note.OnBeginDrag(drag); drag.position = point; note.OnDrag(drag); note.OnEndDrag(drag);
        Check(machine.IsReadingMeasurement, "Actual notebook drag did not start the paper reader.");
        var insertedPaper = (Transform)Field(machine, "readerPaper");
        var paperRenderer = insertedPaper.GetComponent<MeshRenderer>();
        Check(paperRenderer != null && paperRenderer.sharedMaterial.GetFloat("_SurfaceType") == 0
            && paperRenderer.sharedMaterial.GetColor("_BaseColor").a == 1,
            "Inserted card is not a solid opaque mesh.");
        Vector3 feedStart = insertedPaper.position;
        Check(!workspace.TryDropBlock(note, point), "A second insertion interrupted an active reading.");
        Call(machine, "CheckPassword");
        Check(!ResearchPuzzleTrial.Active.Completed.Contains(SecondTraceExperiment.Active.CurrentQuestID), "Inserting paper completed the quest before testing.");
        string initial = Code(machine);
        yield return new WaitForSeconds(.22f);
        ScreenCapture.CaptureScreenshot("Temp/ResearchTrialChecks/KeyboardCardFeed.png");
        var print = paperRenderer.sharedMaterial.GetTexture("_BaseColorMap") as RenderTexture;
        Check(print != null, "The physical card has no printed measurement texture.");
        var previousTarget = RenderTexture.active;
        var capture = new Texture2D(print.width, print.height, TextureFormat.RGB24, false);
        RenderTexture.active = print;
        capture.ReadPixels(new Rect(0, 0, print.width, print.height), 0, 0);
        RenderTexture.active = previousTarget;
        System.IO.File.WriteAllBytes("Temp/ResearchTrialChecks/CardPrint.png", capture.EncodeToPNG());
        UnityEngine.Object.Destroy(capture);
        yield return new WaitForSeconds(1.58f);
        Check(machine.IsReadingMeasurement && Code(machine) != initial && Code(machine) != wanted, "Code did not enter progressively.");
        Check(Vector3.Distance(insertedPaper.position, feedStart) > .14f && (float)Field(machine, "readerFeedProgress") >= .999f,
            "Physical card did not travel into its transport.");
        GameManager.Instance.isGamePaused = true;
        string paused = Code(machine);
        var wheel = ((Transform[])Field(machine, "wordWheelModels"))[0];
        Quaternion rotation = wheel.localRotation;
        float pausedClock = (float)Field(machine, "readerSeconds");
        yield return new WaitForSecondsRealtime(.4f);
        bool pauseWorked = Code(machine) == paused && (float)Field(machine, "readerSeconds") == pausedClock && Quaternion.Angle(rotation, wheel.localRotation) < .001f;
        GameManager.Instance.isGamePaused = false;
        Check(pauseWorked, "Reading or physical wheels continued while paused.");
        machine.ForceExit();
        yield return new WaitForSeconds(1.8f);
        Check(machine.IsReadingMeasurement && !workspace.IsActive && slot.gameObject.activeSelf
            && GameManager.Instance.activeInteraction == null, "Leaving the view stopped the physical machine or left the notebook open.");
        Quaternion outside = wheel.localRotation;
        yield return new WaitForSeconds(.3f);
        Check(Quaternion.Angle(outside, wheel.localRotation) > 1, "Wheels stopped moving outside the machine view.");
        SecondTracePlayChecks.OpenTerminal();
        yield return new WaitForSeconds(1.8f);
        Check(machine.IsReadingMeasurement && workspace.IsActive && slot.gameObject.activeSelf
            && ((Transform)Field(machine, "readerPaper")) == insertedPaper, "Re-entering reset the ongoing reading or its inserted paper.");
        content = (RectTransform)Field(workspace, "informationContent");
        note = Array.Find(content.GetComponentsInChildren<NotebookDraggableBlock>(), b => b.Data.measurementCode == wanted);
        Check(note != null, "Re-entry lost the archived measurement card.");
        point = Camera.main.WorldToScreenPoint(slot.position);
        float deadline = Time.realtimeSinceStartup + 45;
        while (machine.IsReadingMeasurement && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!machine.IsReadingMeasurement && Code(machine) == wanted, "Reader did not enter the full letter/symbol/number code.");
        Check((float)Field(machine, "readerSeconds") == 0 && insertedPaper.gameObject.activeSelf
            && insertedPaper.localPosition.y > .08f && paperRenderer.sharedMaterial.GetColor("_BaseColor").a == 1,
            "The card was faded/hidden instead of physically transported inside the body.");
        var after = new GameData(); notebook.SaveData(ref after);
        Check(JsonUtility.ToJson(after) == snapshot, "Reading consumed or modified the notebook source.");
        Check(!ResearchPuzzleTrial.Active.Completed.Contains(SecondTraceExperiment.Active.CurrentQuestID), "Reader submitted the code without Enter.");
        bool expected = false;
        foreach (var reading in (System.Collections.Generic.Dictionary<string, SecondTraceExperiment.Reading>)Field(SecondTraceExperiment.Active, "readings"))
            if (reading.Key == wanted) expected = reading.Value.MeetsTarget;
        Call(machine, "CheckPassword");
        Check(ResearchPuzzleTrial.Active.Completed.Contains(SecondTraceExperiment.Active.CurrentQuestID) == expected,
            "Manual Enter did not validate the loaded measurement correctly.");
        Check(!expected, "Collect a failed measurement on the first solutions page for the rejection check.");
        // Retain the existing manual rotation path after the automatic reading.
        string beforeManual = Code(machine);
        typeof(InteractableTuringMachine).GetMethod("RotateActiveWheel", Private).Invoke(machine, new object[] { 1 });
        Check(Code(machine) != beforeManual, "Manual wheel controls stopped working after reading.");
        Check(workspace.TryDropBlock(note, point), "Reader cannot accept another output.");
        yield return new WaitForSeconds(.2f);
        machine.ForceExit();
        yield return new WaitForSeconds(1.8f);
        Check(machine.IsReadingMeasurement && !workspace.IsActive && slot.gameObject.activeSelf && GameManager.Instance.activeInteraction == null,
            "Leaving the terminal cancelled the second reading.");
        deadline = Time.realtimeSinceStartup + 35;
        while (machine.IsReadingMeasurement && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!machine.IsReadingMeasurement && Code(machine) == wanted && GameManager.Instance.activeInteraction == null,
            "Reading did not finish correctly while the player was away.");
        Check(!ResearchPuzzleTrial.Active.Completed.Contains(SecondTraceExperiment.Active.CurrentQuestID), "Background reading submitted a failed measurement.");
        after = new GameData(); notebook.SaveData(ref after);
        Check(JsonUtility.ToJson(after) == snapshot, "Background reading changed the notebook source.");
        Check(!((AudioSource)Field(machine, "readerMotor")).isPlaying, "Motor audio kept looping after completion.");
        Debug.Log("TURING READER PASS: original CRT atlas readout; solid opaque physical card; real geometry occlusion; immutable note; geared full-code motion; pause; manual Enter/rotation; continuous operation outside view; re-entry and background completion.");
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Okuyucu Test Kartlarını Hazırla")]
    public static void PrepareReaderCards()
    {
        Check(Application.isPlaying && ChalkboardManager.Instance != null && ChalkboardManager.Instance.isMachineActive,
            "Open the test board first.");
        var graph = typeof(SignalTrialSetup).GetMethod("Graph", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (int variant in new[] { 0, 4, 0 })
        {
            SecondTraceExperiment.Active.Measure((SynthesisGraphData)graph.Invoke(null, new object[] { variant }));
            SecondTracePlayChecks.CollectOutput();
        }
    }

    [MenuItem("Senzora/Çift Kayıt/Canlı/Çözüm Kağıdını Okuyucuya Bırak")]
    public static void InsertVisibleOutput()
    {
        var machine = SecondTraceExperiment.Active.terminal;
        var workspace = NotebookWorkspaceController.Instance;
        workspace.ShowSolutions(); Canvas.ForceUpdateCanvases();
        var note = ((RectTransform)Field(workspace, "informationContent")).GetComponentInChildren<NotebookDraggableBlock>();
        var slot = (Transform)Field(machine, "measurementReader");
        Check(note != null && workspace.TryDropBlock(note, Camera.main.WorldToScreenPoint(slot.position)), "Drop into the actual reader failed.");
    }

    private static string Code(InteractableTuringMachine machine)
    {
        var result = new StringBuilder();
        string letters = (string)Field(machine, "wordChars"), digits = (string)Field(machine, "numberChars");
        foreach (int index in (int[])Field(machine, "wordWheelIndices")) result.Append(letters[index]);
        result.Append('_');
        var symbols = (string[])Field(machine, "symbolChars");
        result.Append(symbols[(int)typeof(InteractableTuringMachine).GetMethod("GetCorrectSymbolIndex", Private).Invoke(machine, null)]);
        result.Append('_');
        foreach (int index in (int[])Field(machine, "numberWheelIndices")) result.Append(digits[index]);
        return result.ToString();
    }

    private static object Field(object instance, string name) => instance.GetType().GetField(name, Private).GetValue(instance);
    private static void Call(object instance, string name) => instance.GetType().GetMethod(name, Private).Invoke(instance, null);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
