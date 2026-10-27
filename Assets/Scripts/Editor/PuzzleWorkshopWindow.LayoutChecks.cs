using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed partial class PuzzleWorkshopWindow
{
    private sealed class LayoutProbe
    {
        public Rect viewport, target;
        public int repaints;
        public string documentJson;
        public List<Vector2> nodes = new List<Vector2>();
    }
    private LayoutProbe layoutProbe;

    // Exercise actual IMGUI clipping and native windows. Model checks cannot catch a
    // GUI.matrix that also scales the enclosing scrollview's clip and screen offset.
    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Şema Yerleşimi")]
    private static void CheckGraphPlacement()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Önce Play'i durdur.");
        Directory.CreateDirectory(Path.GetDirectoryName(DraftPath));
        string[] paths = { DraftPath, DraftPath + ".bak" };
        byte[][] originals = Array.ConvertAll(paths, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        var window = CreateInstance<PuzzleWorkshopWindow>();
        window.titleContent = new GUIContent("Şema Yerleşim Kontrolü");
        window.ShowUtility();
        int stage = 0;
        double start = 0;
        Action configure = () => {
            window.position = new Rect(100, 100, stage == 6 ? 1200 : 900, stage == 6 ? 800 : 640);
            string file = stage == 5 ? "CiftKayit.asset" : "MorKarisim.asset";
            window.Select(AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(PuzzleWorkshopSetup.Folder + "/" + file));
            window.graphZoom = stage == 0 ? .5f : stage == 1 ? .8f : stage == 2 ? 1 : 1.25f;
            window.graphScroll = stage == 3 ? new Vector2(16, 16) : Vector2.zero;
            window.fitGraph = stage >= 4;
            if (stage == 7)
            {
                window.Puzzle.positions.Clear(); window.Puzzle.solution.parts.Clear(); window.Puzzle.solution.bonds.Clear();
                for (int i = 0; i < 64; i++)
                {
                    window.Puzzle.positions.Add(new Vector2(25 + 225 * i, 80));
                    window.Puzzle.solution.parts.Add(new SynthesisPartData { itemID = ColorMixtureLibrary.Red });
                }
                window.cleanJson = JsonUtility.ToJson(window.document);
            }
            window.layoutProbe = new LayoutProbe { documentJson = JsonUtility.ToJson(window.document) };
            start = EditorApplication.timeSinceStartup; window.Focus(); window.Repaint();
        };
        configure();
        EditorApplication.CallbackFunction inspect = null;
        inspect = () => {
            bool finished = false;
            try
            {
                window.Repaint();
                if (window.layoutProbe.repaints < 2 || EditorApplication.timeSinceStartup - start < .35)
                {
                    if (EditorApplication.timeSinceStartup - start > 10) throw new InvalidOperationException("No fresh schema Repaint.");
                    return;
                }
                ValidateGraphPixels(window, stage);
                if (stage == 0 || stage == 3) ValidateGraphInput(window);
                Debug.Log("PUZZLE WORKSHOP GRAPH CASE PASS: " + stage + " zoom=" + window.graphZoom + " scroll=" + window.graphScroll);
                stage++;
                if (stage == 8) { Debug.Log("PUZZLE WORKSHOP GRAPH LAYOUT PASS: 8 rendered cases, both families, resize, zoom, scroll, 64-part fit, unchanged documents and real selection/connect/drag."); finished = true; }
                else configure();
            }
            catch (Exception e) { Debug.LogException(e); finished = true; }
            finally
            {
                if (finished)
                {
                    EditorApplication.update -= inspect;
                    window.Close();
                    for (int i = 0; i < paths.Length; i++)
                        if (originals[i] != null) File.WriteAllBytes(paths[i], originals[i]); else if (File.Exists(paths[i])) File.Delete(paths[i]);
                }
            }
        };
        EditorApplication.update += inspect;
    }

    private static void ValidateGraphPixels(PuzzleWorkshopWindow window, int stage)
    {
        var p = window.layoutProbe;
        if (p.documentJson != JsonUtility.ToJson(window.document)) throw new InvalidOperationException("Viewing/zooming the graph changed the document.");
        if (p.nodes.Count != window.Puzzle.solution.parts.Count || p.viewport.width <= 0 || p.viewport.height < 160)
            throw new InvalidOperationException("Schema did not render all node callbacks.");
        if (p.target.yMax > window.position.yMax + 1 || p.target.yMin < p.viewport.yMax)
            throw new InvalidOperationException("Target panel overlaps the graph or extends below the window.");
        var pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position, (int)window.position.width, (int)window.position.height);
        var image = new Texture2D((int)window.position.width, (int)window.position.height, TextureFormat.RGB24, false);
        try
        {
            image.SetPixels(pixels); image.Apply();
            File.WriteAllBytes("UserSettings/PuzzleWorkshop/SchemaLayout-" + stage + ".png", image.EncodeToPNG());
            Vector2 offset = p.viewport.position - window.position.position;
            AssertMarker(image, Color.magenta, offset + new Vector2(16, p.viewport.height - 24), "canvas origin");
            Vector2 node = offset + (window.Puzzle.positions[0] + Vector2.one * 16) * window.graphZoom - window.graphScroll;
            AssertMarker(image, Color.cyan, node, "native node");
            Vector2 last = p.viewport.position + (window.Puzzle.positions[window.Puzzle.positions.Count - 1] + Vector2.one * 16) * window.graphZoom - window.graphScroll;
            if (p.viewport.Contains(last)) AssertMarker(image, Color.yellow, last - window.position.position, "last native node");
            AssertMarker(image, Color.green, offset + p.viewport.size * .85f, "viewport clipping");
            if (stage >= 4)
                foreach (Vector2 position in window.Puzzle.positions)
                {
                    var rect = new Rect(p.viewport.position + position * window.graphZoom - window.graphScroll, new Vector2(NodeWidth, NodeHeight) * window.graphZoom);
                    if (rect.xMin < p.viewport.xMin || rect.yMin < p.viewport.yMin || rect.xMax > p.viewport.xMax - 12 || rect.yMax > p.viewport.yMax - 12)
                        throw new InvalidOperationException("Fit clips a node: " + rect);
                }
        }
        finally { DestroyImmediate(image); }
    }

    private static void AssertMarker(Texture2D image, Color color, Vector2 expected, string label)
    {
        for (int y = Mathf.RoundToInt(expected.y) - 1; y <= Mathf.RoundToInt(expected.y) + 2; y++)
            for (int x = Mathf.RoundToInt(expected.x) - 1; x <= Mathf.RoundToInt(expected.x) + 2; x++)
            {
                if (x < 0 || x >= image.width || y < 0 || y >= image.height) continue;
                Color c = image.GetPixel(x, image.height - y - 1);
                // Native windows tint inactive controls; small fractional rectangles also
                // blend at pixel edges. Preserve a distinct marker hue while allowing that tint.
                if (Mathf.Abs(c.r - color.r) < .22f && Mathf.Abs(c.g - color.g) < .22f && Mathf.Abs(c.b - color.b) < .22f) return;
            }
        throw new InvalidOperationException("Graph placement/clip mismatch for " + label + " at " + expected);
    }

    private static void ValidateGraphInput(PuzzleWorkshopWindow window)
    {
        Vector2 offset = window.layoutProbe.viewport.position - window.position.position - window.graphScroll;
        Action<EventType, Vector2> send = (type, point) => window.SendEvent(new Event { type = type, mousePosition = point, button = 0 });
        Action<int, Vector2> click = (index, point) => {
            Vector2 at = offset + (window.Puzzle.positions[index] + point) * window.graphZoom;
            send(EventType.MouseDown, at); send(EventType.MouseUp, at);
        };
        click(0, new Vector2(80, 49));
        if (window.selectedNode != 0) throw new InvalidOperationException("Zoomed node selection misses its rendered button.");
        window.Puzzle.solution.bonds.RemoveAll(b => b.fromIndex == 0 && b.toIndex == 1);
        click(0, new Vector2(NodeWidth - 19, 48));
        if (window.connectFrom != 0) throw new InvalidOperationException("Zoomed output port misses its rendered button.");
        click(1, new Vector2(19, 48));
        if (!window.Puzzle.solution.bonds.Exists(b => b.fromIndex == 0 && b.toIndex == 1))
            throw new InvalidOperationException("Zoomed input port misses its rendered button.");
        Vector2 old = window.Puzzle.positions[0];
        Vector2 header = offset + (old + new Vector2(90, 12)) * window.graphZoom;
        Vector2 delta = new Vector2(20, 10);
        send(EventType.MouseDown, header);
        window.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = header + delta, delta = delta, button = 0 });
        send(EventType.MouseUp, header + delta);
        if (Vector2.Distance(window.Puzzle.positions[0], old + delta / window.graphZoom) > .1f)
            throw new InvalidOperationException("Node drag does not match rendered zoom/scroll: old=" + old + " actual=" + window.Puzzle.positions[0] + " expected=" + (old + delta / window.graphZoom));
    }
}
