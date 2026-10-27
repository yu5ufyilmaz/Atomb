using UnityEngine;
using UnityEngine.UI;

// The current target is an observed signal record, never a solved connection diagram.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class QuestDiagramGraphic : MaskableGraphic
{
    public void SetQuest(SynthesisQuestData data, bool revealSolution = false)
    {
        raycastTarget = false;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        if (data != null && data.puzzleKind == PuzzleKind.ColorMixture && data.mixtureTarget != null)
            MixtureGraphic.Add(rectTransform, new NotebookBlockData { itemID = data.mixtureTarget.Encode(true) }, Vector2.zero, Vector2.one);
        else if (data != null && data.puzzleKind == PuzzleKind.Signal && data.targetPulses != null && data.targetPulses.Count > 0)
            SignalTrialGraphic.Add(rectTransform, new NotebookBlockData {
                itemID = SignalTrial.Encode(data.targetPulses).Replace("signal-result", "signal-target") }, Vector2.zero, Vector2.one);
        else if (data != null && data.questID == SignalTrial.QuestID)
            SignalTrialGraphic.Add(rectTransform, new NotebookBlockData { itemID = "signal-note/target" }, Vector2.zero, Vector2.one);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
}
