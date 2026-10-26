using System;
using System.Collections.Generic;
using UnityEngine;

public enum NotebookBlockType
{
    Quest,
    PartClue,
    RuleClue,
}

public enum NotebookTool
{
    Select,
    Pen,
    Eraser,
}

[Serializable]
public class NotebookBlockData
{
    public string instanceID;
    public NotebookBlockType blockType;
    public string itemID;
    public string questID;
    public string title;
    public string description;
    public string measurementCode;
    public int ruleIndex = -1;
    public Vector2 position;
    public float rotation;
    public int sketchVariant;
    [Tooltip("Kitaptan kırpılan gerçek görüntünün kaynağı (BookClueData.ID); boşsa metin kartı çizilir.")]
    public string clueDataID;
    public int clueRegionIndex = -1;

    public NotebookBlockData Clone()
    {
        return (NotebookBlockData)MemberwiseClone();
    }
}

[Serializable]
public class NotebookStrokeData
{
    // Noktalar sayfanın sol alt köşesinden itibaren 0..1 aralığında saklanır.
    public List<Vector2> points = new List<Vector2>();
    public Color color = Color.black;
    public float width = 3f;

    public NotebookStrokeData Clone()
    {
        return new NotebookStrokeData
        {
            points = points == null ? new List<Vector2>() : new List<Vector2>(points),
            color = color,
            width = width,
        };
    }
}

[Serializable]
public class NotebookPageSaveData
{
    public List<NotebookBlockData> blocks = new List<NotebookBlockData>();
    public List<NotebookStrokeData> strokes = new List<NotebookStrokeData>();

    public NotebookPageSaveData Clone()
    {
        var copy = new NotebookPageSaveData();
        if (blocks != null)
            foreach (var block in blocks)
                if (block != null)
                    copy.blocks.Add(block.Clone());
        if (strokes != null)
            foreach (var stroke in strokes)
                if (stroke != null)
                    copy.strokes.Add(stroke.Clone());
        return copy;
    }
}
