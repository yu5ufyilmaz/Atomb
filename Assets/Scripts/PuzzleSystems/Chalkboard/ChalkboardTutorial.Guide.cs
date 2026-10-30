using System.Linq;
using UnityEngine;

// Her ders adımında oyuncuya sıradaki hareketi gösterir; sıra ChalkboardTutorialCondition bağlamlarıyla aynıdır.
public sealed partial class ChalkboardTutorial
{
    private const string ToBoard = "TAHTAYA SÜRÜKLE";

    private void UpdateGuide()
    {
        var workspace = NotebookWorkspaceController.Instance;
        if (workspace == null) return;
        var interaction = GameManager.Instance.activeInteraction;
        if (lessonBook.ExitLocked && ReferenceEquals(interaction, lessonBook)) workspace.GuideBook(lessonBook);
        else if (collectedStep.HasTriggered && !boardStep.HasTriggered && research.Board.isMachineActive) GuideBoard(workspace);
        else if (terminalReadyStep.HasTriggered && !terminalAccepted.IsMet() && ReferenceEquals(interaction, experiment.terminal)) GuideTerminal(workspace);
        else workspace.ClearGuide();
    }

    private void GuideBoard(NotebookWorkspaceController workspace)
    {
        var board = research.Board;
        var documents = board.EnsureDocuments();
        if (board.PinnedQuest?.questID != QuestID)
            workspace.GuideNote(n => n.blockType == NotebookBlockType.Quest && n.questID == QuestID, "Hedefler",
                NotebookWorkspaceController.ScreenPoint(documents.TargetSlot), ToBoard);
        else if (!HasPinnedCards)
            workspace.GuideNote(n => n.itemID == "tutorial/ports", "Bilgiler", BoardPoint(.5f, .6f), ToBoard);
        else if (!panelWasClosed)
            Mark(workspace, documents.PanelHandle, "BELGELERİ TOPLA");
        else if (!HasCorrectReading)
            GuideCircuit(workspace);
        else if (!HasStoredReading)
            workspace.GuideCardToNotebook(experiment.OutputCard);
        else workspace.ClearGuide();
    }

    // Önce üç parça tahtaya iner, sonra zincir soldan sağa bağlanır, en son ölçülür.
    private void GuideCircuit(NotebookWorkspaceController workspace)
    {
        string[] chain = { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter };
        var parts = research.Board.boardTransform.GetComponentsInChildren<DraggableFormula>();
        for (int i = 0; i < chain.Length; i++)
        {
            string id = chain[i];
            if (parts.Any(p => p.formulaData?.itemID == id)) continue;
            workspace.GuideNote(n => n.blockType == NotebookBlockType.PartClue && n.itemID == id, "Parçalar",
                BoardPoint(.76f - i * .25f, .72f), ToBoard);
            return;
        }
        for (int i = 0; i + 1 < chain.Length; i++)
        {
            string firstID = chain[i], secondID = chain[i + 1];
            var first = parts.First(p => p.formulaData?.itemID == firstID);
            var second = parts.First(p => p.formulaData?.itemID == secondID);
            if (first.GetOwnNodes().Any(n => n.connectedNode != null && n.connectedNode.parentFormula == second)) continue;
            FormulaNode output = FreePort(first, true), input = FreePort(second, false);
            if (output == null || input == null) { workspace.ClearGuide(); return; }
            workspace.GuideMark(NotebookWorkspaceController.ScreenPoint(output.transform.position),
                NotebookWorkspaceController.ScreenPoint(input.transform.position), "ÇIKIŞI GİRİŞE BAĞLA", true);
            return;
        }
        Mark(workspace, experiment.MeasureButton, "DENEYİ ÖLÇ");
    }

    private void GuideTerminal(NotebookWorkspaceController workspace)
    {
        var terminal = experiment.terminal;
        if (!terminal.RequiresMeasurementCursor || terminal.IsReadingMeasurement || terminal.MeasurementRejected || terminal.HasReadMeasurement)
        {
            workspace.ClearGuide();
            return;
        }
        workspace.GuideNote(n => SignalExperiment.IsMeasurementNote(n) && n.questID == QuestID, "Ölçümler",
            NotebookWorkspaceController.ScreenPoint(terminal.ReaderPoint), "OKUYUCUYA SÜRÜKLE");
    }

    private static void Mark(NotebookWorkspaceController workspace, Vector3 world, string label)
    {
        Vector3 point = NotebookWorkspaceController.ScreenPoint(world);
        workspace.GuideMark(point, point, label);
    }

    private static FormulaNode FreePort(DraggableFormula part, bool output) => part.GetOwnNodes()
        .FirstOrDefault(n => n.gameObject.activeSelf && !n.isOccupied && n.GetComponent<SignalPort>().output == output);

    private Vector3 BoardPoint(float x, float y)
    {
        var board = research.Board;
        Vector2 local = board.minBounds + Vector2.Scale(board.maxBounds - board.minBounds, new Vector2(x, y));
        return NotebookWorkspaceController.ScreenPoint(board.boardTransform.TransformPoint(new Vector3(local.x, local.y, .02f)));
    }
}
