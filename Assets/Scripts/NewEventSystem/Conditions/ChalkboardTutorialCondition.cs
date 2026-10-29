using System;
using System.Linq;
using UnityEngine;

// Live puzzle state for scene events. Dialogue IDs and event order belong to the scene.
public class ChalkboardTutorialCondition : MonoBehaviour, ICondition
{
    public enum Context { Book, Collect, LeaveBook, Board, Pin, ClearBoard, Build, Store, LeaveBoard,
        Turing, Insert, Reader, Confirm, RetryMeasurement, Valve, LeaveValve, WaitPower, Breaker }
    public ChalkboardTutorial lesson;
    public Context context;
    public event Action OnConditionChanged;
    private bool lastMet;
    private int lastProgress;

    public bool IsMet()
    {
        if (lesson == null || !lesson.isActiveAndEnabled || lesson.research?.Content == null) return false;
        var game = GameManager.Instance;
        if (game == null || !game.isGameStarted) return false;
        var interaction = game.activeInteraction;
        var board = lesson.research.Board;
        var terminal = lesson.research.GetComponent<SignalExperiment>().terminal;
        switch (context)
        {
            case Context.Book: return interaction != lesson.lessonBook;
            case Context.Collect: return interaction == lesson.lessonBook;
            case Context.LeaveBook: return interaction == lesson.lessonBook;
            case Context.Board: return interaction != lesson.lessonBook && !board.isMachineActive;
            case Context.Pin: return board.isMachineActive && !lesson.HasPinnedCards;
            case Context.ClearBoard: return board.isMachineActive && lesson.HasPinnedCards && !lesson.PanelWasClosed;
            case Context.Build: return board.isMachineActive && lesson.HasPinnedCards && lesson.PanelWasClosed && !lesson.HasCorrectReading;
            case Context.Store: return board.isMachineActive && lesson.HasPinnedCards && lesson.PanelWasClosed && lesson.HasCorrectReading && !lesson.HasStoredReading;
            case Context.LeaveBoard: return board.isMachineActive && lesson.HasPinnedCards && lesson.PanelWasClosed && lesson.HasCorrectReading && lesson.HasStoredReading;
            case Context.Turing: return interaction != terminal;
            case Context.Insert: return interaction == terminal && !terminal.IsReadingMeasurement && !terminal.MeasurementRejected && !terminal.HasReadMeasurement;
            case Context.Reader: return interaction == terminal && terminal.IsReadingMeasurement;
            case Context.Confirm: return interaction == terminal && !terminal.IsReadingMeasurement && !terminal.MeasurementRejected && terminal.HasReadMeasurement;
            case Context.RetryMeasurement: return interaction == terminal && !terminal.IsReadingMeasurement && terminal.MeasurementRejected;
            case Context.Valve: return !(PressureSystemManager.Instance.GetPressure() <= 20 && interaction is InteractablePressureValve);
            case Context.LeaveValve: return PressureSystemManager.Instance.GetPressure() <= 20 && interaction is InteractablePressureValve;
            case Context.WaitPower: return !lesson.SawBreakerTrip && !BreakerBox.Instance.IsTripped;
            case Context.Breaker: return BreakerBox.Instance.IsTripped;
            default: return false;
        }
    }

    private void Update()
    {
        bool met = IsMet();
        int progress = met ? Progress() : 0;
        if (met == lastMet && progress == lastProgress) return;
        lastMet = met; lastProgress = progress;
        OnConditionChanged?.Invoke();
    }

    private int Progress()
    {
        var notebook = NotebookUI.Instance;
        if (context == Context.Collect && notebook != null)
        {
            int count = notebook.EnsureCollectedQuests().Exists(n => n.questID == ChalkboardTutorial.QuestID) ? 1 : 0;
            var notes = notebook.workspacePages.Where(p => p?.blocks != null).SelectMany(p => p.blocks).Where(n => n != null).ToList();
            if (notes.Exists(n => n.questID == ChalkboardTutorial.QuestID && n.itemID == "tutorial/ports")) count++;
            foreach (string id in new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter })
                if (notes.Exists(n => n.blockType == NotebookBlockType.PartClue && n.itemID == id)) count++;
            return count;
        }
        if (context == Context.Pin) return (lesson.research.Board.PinnedQuest?.questID == ChalkboardTutorial.QuestID ? 1 : 0)
            + (lesson.research.Board.pinnedInformation.Exists(n => n.questID == ChalkboardTutorial.QuestID && n.itemID == "tutorial/ports") ? 1 : 0);
        if (context == Context.Build)
        {
            unchecked
            {
                int signature = lesson.research.GetComponent<SignalExperiment>().LastReading.Code.GetHashCode();
                foreach (var part in lesson.research.Board.boardTransform.GetComponentsInChildren<DraggableFormula>())
                {
                    signature = signature * 31 + part.GetInstanceID();
                    foreach (var node in part.GetOwnNodes()) signature = signature * 31 + (node.connectedNode != null ? node.connectedNode.GetInstanceID() : 0);
                }
                return signature;
            }
        }
        if (context == Context.Valve) return Mathf.FloorToInt(PressureSystemManager.Instance.GetPressure() / 5);
        return 0;
    }
}
