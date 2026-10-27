using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class PuzzleWorkshopWindow
{
    private void AddPart(string id)
    {
        if (Puzzle.solution.parts.Count >= PuzzleSimulation.MaxParts) { SetMessage("En fazla 64 parça eklenebilir.", MessageType.Warning); return; }
        Mutate("Şemaya parça ekle", () => {
            int previous = selectedNode, added = Puzzle.solution.parts.Count;
            Puzzle.solution.parts.Add(new SynthesisPartData { itemID = id });
            Puzzle.positions.Add(new Vector2(35 + (Puzzle.solution.parts.Count - 1) % 5 * 185, 35 + (Puzzle.solution.parts.Count - 1) / 5 * 130));
            var part = PuzzleParts.Find(id);
            if (previous >= 0 && previous < added && part.inputs == 1)
            {
                var before = PuzzleParts.Find(Puzzle.solution.parts[previous].itemID);
                if (before.outputs == 1 && Puzzle.solution.bonds.FindAll(e => e.fromIndex == previous).Count <= 1)
                {
                    var edge = Puzzle.solution.bonds.Find(e => e.fromIndex == previous);
                    if (edge == null) Puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = previous, toIndex = added, directed = true });
                    else if (part.outputs == 1) { int next = edge.toIndex; edge.toIndex = added; Puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = added, toIndex = next, directed = true }); }
                }
                else if (before.outputs == 0 && part.outputs == 1)
                {
                    var edge = Puzzle.solution.bonds.Find(e => e.toIndex == previous);
                    if (edge != null) { edge.toIndex = added; Puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = added, toIndex = previous, directed = true }); }
                }
            }
            selectedNode = added; LayoutGraph(); fitGraph = true;
        });
    }

    private void Graph()
    {
        EditorGUILayout.LabelField("2 / ÖRNEK ÇÖZÜM ŞEMASI", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(connectFrom >= 0 ? "Çıkış seçildi: giriş halkasına tıkla. Esc: iptal." : "Sürükle: parçayı taşı · Ok → halka: bağla", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Otomatik Düzenle", GUILayout.Width(120))) Mutate("Şemayı düzenle", () => { LayoutGraph(); fitGraph = true; });
        if (GUILayout.Button("Sığdır", GUILayout.Width(50))) fitGraph = true;
        graphZoom = GUILayout.HorizontalSlider(graphZoom, graphMinimumZoom, 1.25f, GUILayout.Width(70));
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(selectedNode < 0))
        {
            if (GUILayout.Button("Bağlarını Sil", GUILayout.Width(90))) Mutate("Bağları sil", () => Puzzle.solution.bonds.RemoveAll(e => e.fromIndex == selectedNode || e.toIndex == selectedNode));
            if (GUILayout.Button("Parçayı Sil", GUILayout.Width(85))) DeleteNode();
        }
        EditorGUILayout.EndHorizontal();
        var viewport = GUILayoutUtility.GetRect(300, 160, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (layoutProbe != null && Event.current.type == EventType.Repaint)
        {
            layoutProbe.viewport = new Rect(GUIUtility.GUIToScreenPoint(viewport.position), viewport.size);
            layoutProbe.nodes.Clear();
            layoutProbe.repaints++;
        }
        float width = 80, height = 80;
        while (Puzzle.positions.Count < Puzzle.solution.parts.Count) Puzzle.positions.Add(new Vector2(35 + Puzzle.positions.Count % 5 * 185, 35 + Puzzle.positions.Count / 5 * 130));
        foreach (var p in Puzzle.positions) { width = Mathf.Max(width, p.x + NodeWidth + 70); height = Mathf.Max(height, p.y + NodeHeight + 70); }
        if (fitGraph && Event.current.type == EventType.Repaint && viewport.width > 24 && viewport.height > 24)
        {
            graphZoom = Mathf.Min(1, Mathf.Min((viewport.width - 24) / width, (viewport.height - 24) / height));
            graphMinimumZoom = Mathf.Min(.35f, graphZoom);
            graphScroll = Vector2.zero; fitGraph = false;
        }
        graphScroll = GUI.BeginScrollView(viewport, graphScroll, new Rect(0, 0, width * graphZoom, height * graphZoom));
        // Scale content rectangles only. Scaling GUI.matrix also scales the scrollview's clip,
        // which makes the canvas overlap the toolbar and cuts off nodes when zoomed out.
        EditorGUI.DrawRect(new Rect(0, 0, Mathf.Max(width * graphZoom, viewport.width), Mathf.Max(height * graphZoom, viewport.height)), new Color(.12f, .15f, .16f));
        for (float x = 0; x < width; x += 30) EditorGUI.DrawRect(new Rect(x * graphZoom, 0, 1, height * graphZoom), new Color(.17f, .2f, .21f));
        for (float y = 0; y < height; y += 30) EditorGUI.DrawRect(new Rect(0, y * graphZoom, width * graphZoom, 1), new Color(.17f, .2f, .21f));
        if (layoutProbe != null) EditorGUI.DrawRect(new Rect(graphScroll + new Vector2(16, viewport.height - 24), Vector2.one * 4), Color.magenta);
        Handles.BeginGUI();
        foreach (var edge in Puzzle.solution.bonds)
        {
            if (edge.fromIndex < 0 || edge.toIndex < 0 || edge.fromIndex >= Puzzle.positions.Count || edge.toIndex >= Puzzle.positions.Count) continue;
            var a = (Puzzle.positions[edge.fromIndex] + new Vector2(NodeWidth - 19, 48)) * graphZoom;
            var b = (Puzzle.positions[edge.toIndex] + new Vector2(19, 48)) * graphZoom;
            Handles.DrawBezier(a, b, a + Vector2.right * 65 * graphZoom, b + Vector2.left * 65 * graphZoom, new Color(.5f, .85f, .73f), null, 3);
            Vector2 direction = (b - a).normalized;
            Handles.color = new Color(.5f, .85f, .73f);
            Handles.DrawAAPolyLine(3, b - direction * 13 * graphZoom + new Vector2(-direction.y, direction.x) * 6 * graphZoom, b,
                b - direction * 13 * graphZoom - new Vector2(-direction.y, direction.x) * 6 * graphZoom);
        }
        Handles.EndGUI();
        BeginWindows();
        for (int i = 0; i < Puzzle.solution.parts.Count; i++)
        {
            var old = Puzzle.positions[i];
            var part = PuzzleParts.Find(Puzzle.solution.parts[i].itemID);
            Color background = GUI.backgroundColor;
            if (i == selectedNode) GUI.backgroundColor = new Color(.55f, .9f, .8f);
            var style = new GUIStyle(GUI.skin.window) { fontSize = Mathf.Max(1, Mathf.RoundToInt(11 * graphZoom)) };
            Vector2 scaledPosition = old * graphZoom;
            var moved = GUI.Window(i, new Rect(scaledPosition, new Vector2(NodeWidth, NodeHeight) * graphZoom), DrawNode, (i + 1) + " / " + (part?.title ?? "?"), style);
            GUI.backgroundColor = background;
            if (moved.position != scaledPosition)
            {
                var next = new Vector2(Mathf.Max(8, moved.x / graphZoom), Mathf.Max(8, moved.y / graphZoom));
                if (next != old) Mutate("Şema parçasını taşı", () => Puzzle.positions[i] = next);
            }
        }
        EndWindows();
        if (connectFrom >= 0 && connectFrom < Puzzle.positions.Count)
        {
            Handles.BeginGUI(); Handles.color = new Color(.95f, .78f, .3f);
            Handles.DrawDottedLine((Puzzle.positions[connectFrom] + new Vector2(NodeWidth - 19, 48)) * graphZoom, Event.current.mousePosition, 5); Handles.EndGUI();
            Repaint();
        }
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { connectFrom = -1; Event.current.Use(); }
        if (layoutProbe != null) EditorGUI.DrawRect(new Rect(graphScroll + viewport.size * .85f, Vector2.one * 4), Color.green);
        GUI.EndScrollView();
    }

    private void DrawNode(int index)
    {
        if (layoutProbe != null && Event.current.type == EventType.Repaint) layoutProbe.nodes.Add(GUIUtility.GUIToScreenPoint(Vector2.zero));
        var part = PuzzleParts.Find(Puzzle.solution.parts[index].itemID);
        if (part == null) return;
        var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(1, Mathf.RoundToInt(12 * graphZoom)) };
        var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = Mathf.Max(1, Mathf.RoundToInt(9 * graphZoom)) };
        if (GUI.Button(ZoomRect(39, 34, 85, 30), part.symbol, buttonStyle)) selectedNode = index;
        if (part.inputs > 0 && GUI.Button(ZoomRect(4, 35, 30, 26), new GUIContent("○", "Giriş (" + part.inputs + ")"), buttonStyle))
        {
            selectedNode = index;
            if (connectFrom >= 0) Connect(connectFrom, index);
        }
        if (part.outputs > 0 && GUI.Button(ZoomRect(NodeWidth - 34, 35, 30, 26), new GUIContent("→", "Çıkış (" + part.outputs + ")"), buttonStyle))
        { connectFrom = index; selectedNode = index; }
        GUI.Label(ZoomRect(8, 68, 150, 20), $"{part.inputs} giriş / {part.outputs} çıkış", hintStyle);
        if (layoutProbe != null && index == 0) EditorGUI.DrawRect(new Rect(Vector2.one * 16 * graphZoom, Vector2.one * 2), Color.cyan);
        if (layoutProbe != null && index == Puzzle.solution.parts.Count - 1) EditorGUI.DrawRect(new Rect(Vector2.one * 16 * graphZoom, Vector2.one * 2), Color.yellow);
        GUI.DragWindow(ZoomRect(0, 0, NodeWidth, 30));
    }

    private Rect ZoomRect(float x, float y, float width, float height) => new Rect(x * graphZoom, y * graphZoom, width * graphZoom, height * graphZoom);

    private void Connect(int from, int to)
    {
        connectFrom = -1;
        if (from == to || Puzzle.solution.bonds.Exists(e => e.fromIndex == from && e.toIndex == to)) return;
        int outputs = Puzzle.solution.bonds.FindAll(e => e.fromIndex == from).Count;
        int inputs = Puzzle.solution.bonds.FindAll(e => e.toIndex == to).Count;
        if (outputs >= SignalTrial.Outputs(Puzzle.solution.parts[from].itemID) || inputs >= SignalTrial.Inputs(Puzzle.solution.parts[to].itemID))
        { SetMessage("Bu parçanın giriş veya çıkışları dolu. Önce eski bağını sil.", MessageType.Warning); return; }
        Mutate("Parçaları bağla", () => Puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = from, toIndex = to, directed = true }));
    }

    private void DeleteNode()
    {
        if (selectedNode < 0 || selectedNode >= Puzzle.solution.parts.Count) return;
        Mutate("Şema parçasını sil", () => {
            int index = selectedNode; Puzzle.solution.parts.RemoveAt(index); Puzzle.positions.RemoveAt(index);
            Puzzle.solution.bonds.RemoveAll(e => e.fromIndex == index || e.toIndex == index);
            foreach (var edge in Puzzle.solution.bonds) { if (edge.fromIndex > index) edge.fromIndex--; if (edge.toIndex > index) edge.toIndex--; }
            selectedNode = connectFrom = -1;
        });
    }

    private void Target()
    {
        Rect panel = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(190));
        if (layoutProbe != null && Event.current.type == EventType.Repaint)
            layoutProbe.target = new Rect(GUIUtility.GUIToScreenPoint(panel.position), panel.size);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("3 / HEDEF KAYIT", EditorStyles.boldLabel);
        if (GUILayout.Button("Hedefi Şemadan Al", GUILayout.Width(145)))
        {
            var reading = PuzzleSimulation.Measure(Puzzle.solution, Puzzle);
            if (!reading.Measurable || !string.IsNullOrEmpty(reading.targetFeedback)) SetMessage(reading.problem ?? reading.targetFeedback, MessageType.Warning);
            else Mutate("Hedefi örnek çözümden al", () => {
                if (Puzzle.kind == PuzzleKind.ColorMixture) Puzzle.mixtureTarget = reading.mixture.Copy();
                else { Puzzle.target = reading.pulses; Puzzle.rules.traceCount = reading.pulses.Count; }
                ValidateDocument();
            });
        }
        EditorGUILayout.EndHorizontal();
        if (Puzzle.kind == PuzzleKind.ColorMixture)
        {
            EditorGUILayout.BeginHorizontal();
            MixturePreview("HEDEF", Puzzle.mixtureTarget);
            var reading = PuzzleSimulation.Measure(Puzzle.solution, Puzzle);
            MixturePreview("ŞEMANIN SONUCU", reading.mixture);
            EditorGUILayout.EndHorizontal();
            if (!reading.Measurable || !string.IsNullOrEmpty(reading.targetFeedback)) EditorGUILayout.LabelField(reading.problem ?? reading.targetFeedback, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical(); return;
        }
        var plot = GUILayoutUtility.GetRect(200, 75, GUILayout.ExpandWidth(true)); DrawPlot(plot, Puzzle.target);
        for (int i = 0; i < Puzzle.target.Count; i++)
        {
            var pulse = Puzzle.target[i]; EditorGUILayout.BeginHorizontal();
            GUILayout.Label("İz " + (i + 1), GUILayout.Width(35));
            EditorGUIUtility.labelWidth = 58;
            EditorGUI.BeginChangeCheck();
            float time = EditorGUILayout.FloatField("Zaman", pulse.time);
            float height = EditorGUILayout.FloatField("Yükseklik", pulse.height);
            float width = EditorGUILayout.FloatField("Genişlik", pulse.width);
            if (EditorGUI.EndChangeCheck()) Mutate("Hedef izini düzenle", () => { pulse.time = time; pulse.height = height; pulse.width = width; Puzzle.target[i] = pulse; });
            EditorGUIUtility.labelWidth = 0;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField("Hedef değişince örnek çözümü yeniden doğrula. Ölçüm alanı: zaman 0–1; yükseklik 0–2,1.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    public static void DrawPlot(Rect rect, List<SignalTrial.Pulse> pulses)
    {
        EditorGUI.DrawRect(rect, new Color(.89f, .91f, .84f));
        Rect axes = new Rect(rect.x + 10, rect.y + 6, rect.width - 20, rect.height - 16);
        Handles.BeginGUI(); Handles.color = new Color(.17f, .26f, .23f, .25f);
        for (int i = 0; i < 6; i++) { float x = axes.x + axes.width * i / 5; Handles.DrawLine(new Vector3(x, axes.y), new Vector3(x, axes.yMax)); }
        var points = new Vector3[161];
        for (int i = 0; i < points.Length; i++)
        {
            float time = i / 160f, y = 0;
            if (pulses != null) foreach (var p in pulses) y += p.height * Mathf.Exp(-Mathf.Pow((time - p.time) / Mathf.Max(.01f, p.width), 2));
            points[i] = new Vector3(axes.x + axes.width * time, axes.yMax - y / PuzzleSimulation.MaxHeight * axes.height);
        }
        Handles.color = new Color(.1f, .3f, .24f); Handles.DrawAAPolyLine(2.5f, points); Handles.EndGUI();
    }

    private void Notes()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        notesScroll = EditorGUILayout.BeginScrollView(notesScroll);
        EditorGUILayout.LabelField("4 / KİTAPLAR VE GÖZLEMLER", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Zorunlu parça ve kural bilgileri otomatik eklenir. Örnek çözüm oyuncuya gösterilmez.", EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Gözlem Notu Ekle")) Mutate("Gözlem ekle", () => Puzzle.notes.Add(new BookWorkspacePage { title = "Yeni Gözlem", explanation = "" }));
        for (int i = 0; i < Puzzle.notes.Count; i++)
        {
            var note = Puzzle.notes[i]; if (note == null) continue;
            EditorGUILayout.BeginHorizontal(); EditorGUI.BeginChangeCheck();
            string title = EditorGUILayout.TextField(note.title);
            bool remove = GUILayout.Button("×", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();
            string text = EditorGUILayout.TextArea(note.explanation, GUILayout.MinHeight(50));
            if (EditorGUI.EndChangeCheck()) Mutate("Gözlemi düzenle", () => { note.title = title; note.explanation = text; });
            if (remove) { int index = i; Mutate("Gözlemi kaldır", () => Puzzle.notes.RemoveAt(index)); break; }
        }
        EditorGUILayout.Space(8);
        EditorGUI.BeginChangeCheck(); bool fixedTarget = EditorGUILayout.Toggle("Hedef masa kitabında kalsın", Puzzle.targetInFirstBook);
        if (EditorGUI.EndChangeCheck()) Mutate("Hedef kitabı seçimi", () => Puzzle.targetInFirstBook = fixedTarget);
        assignmentSeed = EditorGUILayout.IntField("Dağıtım önizleme seed'i", assignmentSeed);
        EditorGUILayout.LabelField("4 kitap / sayfada en fazla 2 bilgi", EditorStyles.miniLabel);
        var pages = PuzzleBookDistribution.Build(Puzzle, 4, assignmentSeed);
        for (int book = 0; book < pages.Length; book++)
        {
            EditorGUILayout.Space(6); EditorGUILayout.LabelField("ARAŞTIRMA ARŞİVİ " + (book + 1), EditorStyles.boldLabel);
            foreach (var page in pages[book]) foreach (var note in page.blocks)
                EditorGUILayout.LabelField((page.pageIndex + 1) + " · " + note.title, EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
    }

    public static void MixturePreview(string title, MixtureResult result)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        Rect swatch = GUILayoutUtility.GetRect(80, 42, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(swatch, result?.Swatch ?? new Color(.2f, .23f, .24f));
        EditorGUILayout.LabelField(result?.Describe() ?? "Önce şemayı kur ve Hedefi Şemadan Al'a bas.", EditorStyles.wordWrappedLabel, GUILayout.MinHeight(35));
        EditorGUILayout.EndVertical();
    }

    private void LayoutGraph()
    {
        int count = Puzzle.solution.parts.Count;
        var level = new int[count]; var rows = new Dictionary<int, int>();
        for (int step = 0; step < count; step++)
        {
            bool changed = false;
            foreach (var edge in Puzzle.solution.bonds)
                if (edge.fromIndex >= 0 && edge.toIndex >= 0 && edge.fromIndex < count && edge.toIndex < count && level[edge.toIndex] < level[edge.fromIndex] + 1)
                { level[edge.toIndex] = level[edge.fromIndex] + 1; changed = true; }
            if (!changed) break;
            if (step == count - 1) return; // Keep user positions if a cycle is being edited.
        }
        Puzzle.positions.Clear();
        for (int i = 0; i < count; i++)
        {
            rows.TryGetValue(level[i], out int row); rows[level[i]] = row + 1;
            Puzzle.positions.Add(new Vector2(25 + level[i] * (NodeWidth + 45), 25 + row * (NodeHeight + 45)));
        }
    }
}
