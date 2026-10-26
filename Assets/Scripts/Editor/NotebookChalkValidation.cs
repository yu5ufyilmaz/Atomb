using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class NotebookChalkValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Doğrulama/Defter Aç Kapat ve Sabit Tebeşir")]
    public static void Run() { Notebook(); Chalk(); }

    public static void Notebook()
    {
        var host = new GameObject("Popup test"); GameObject root = null;
        try
        {
            var notes = host.AddComponent<NotebookUI>();
            var workspace = host.AddComponent<NotebookWorkspaceController>();
            typeof(NotebookWorkspaceController).GetField("notebook", Private).SetValue(workspace, notes);
            Call(workspace, "BuildWorkspace");
            root = (GameObject)typeof(NotebookWorkspaceController).GetField("root", Private).GetValue(workspace);
            root.SetActive(true); Canvas.ForceUpdateCanvases();
            var window = root.transform.Find("Not Defteri").GetComponent<RectTransform>();
            var toggle = root.transform.Find("Defter Aç Kapat");
            Check(toggle != null, "Ekran kenarında defter aç/kapat düğmesi yok.");
            window.anchoredPosition += new Vector2(-120, 35);
            Vector2 previous = window.anchoredPosition;
            notes.EnsureWorkspacePage().blocks.Add(new NotebookBlockData { title = "korunacak not" });
            Call(workspace, "ToggleNotebook"); Call(workspace, "UpdateNotebookPopup", 1f);
            Check(!window.gameObject.activeSelf && toggle.gameObject.activeInHierarchy, "Kapalı defter gizlenmedi veya açma düğmesi kayboldu.");
            Check(!window.GetComponent<CanvasGroup>().blocksRaycasts, "Gizli defter tahtayı engelliyor.");
            Call(workspace, "ToggleNotebook"); Call(workspace, "UpdateNotebookPopup", 1f);
            Check(window.gameObject.activeSelf && Vector2.Distance(previous, window.anchoredPosition) < .01f
                && window.localScale == Vector3.one && notes.EnsureWorkspacePage().blocks.Count == 1,
                "Açılan defter son konumunu veya içeriğini kaybetti.");
            Call(workspace, "ToggleNotebook"); Call(workspace, "UpdateNotebookPopup", .06f);
            Call(workspace, "ToggleNotebook"); Call(workspace, "UpdateNotebookPopup", 1f);
            Check(window.gameObject.activeSelf && Vector2.Distance(previous, window.anchoredPosition) < .01f,
                "Animasyon sırasında tekrar açma defteri kaydırdı.");
            Call(workspace, "ToggleNotebook"); Call(workspace, "ToggleNotebook"); Call(workspace, "UpdateNotebookPopup", .016f);
            Check(window.GetComponent<CanvasGroup>().blocksRaycasts && window.GetComponent<CanvasGroup>().interactable,
                "Aynı karede aç/kapat defterin etkileşimini kilitledi.");
            Debug.Log("NOTEBOOK POPUP PASS: edge button, hidden input, position, data and interrupted animation.");
        }
        finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(host); }
    }

    public static void Chalk()
    {
        var host = new GameObject("Chalk bond test"); var previous = ChalkboardManager.Instance;
        try
        {
            var board = host.AddComponent<ChalkboardManager>();
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, board);
            board.boardTransform = new GameObject("Board plane").transform; board.boardTransform.SetParent(host.transform);
            board.boardTransform.rotation = Quaternion.Euler(12, 27, 9);
            var quest = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(SecondPracticeValidation.QuestPath);
            board.formulaCatalog = new List<FormulaItemSO>(quest.clueParts);
            var a = Create(board, quest.clueParts[0], new Vector3(-.7f, 0, .02f));
            var b = Create(board, quest.clueParts[1], new Vector3(.7f, .2f, .02f));
            Check(a.GetComponent<ChalkFormulaSketch>() != null, "Test gerçek tebeşir prefabını kullanmıyor.");
            var canMove = typeof(DraggableFormula).GetProperty("CanMoveOnBoard");
            Check(canMove != null && !(bool)canMove.GetValue(a), "Tahtaya yazılmış formül hâlâ taşınabiliyor.");
            var node = a.GetOwnNodes()[0]; var other = b.GetOwnNodes()[0];
            foreach (var p in new[] { a, b }) foreach (var n in p.GetOwnNodes()) Call(n, "Awake");
            Vector3 apos = a.transform.position, bpos = b.transform.position;
            node.ConnectTo(other);
            Check(node.connectedNode == other, "Geçerli uçlar bağlanmadı.");
            Check(!(bool)canMove.GetValue(a) && !(bool)canMove.GetValue(b), "Bağ kurulunca çizimler taşınabilir hâle geldi.");
            Call(node, "DrawConnection");
            var drawing = (Component)typeof(FormulaNode).GetField("chalkConnection", Private).GetValue(node);
            Check(drawing != null && !node.GetComponent<LineRenderer>().enabled, "Tebeşir bağı yerine eski çizgi gösteriliyor.");
            var mesh = drawing.GetComponent<MeshFilter>().sharedMesh;
            var points = mesh.vertices;
            Check(points.Length > 24, "Bağda kuru tebeşir boşlukları ve taneleri yok.");
            Check(drawing.transform.parent == board.boardTransform && drawing.transform.localRotation == Quaternion.identity,
                "Bağ hâlâ formül ucunu veya kamerayı izliyor.");
            foreach (var point in points)
                Check(Mathf.Abs(point.z - .018f) < .0001f, "Bağ çizgisi tahta yüzeyine oturmuyor.");
            node.transform.localScale *= 1.2f;
            Call(node, "DrawConnection");
            var next = mesh.vertices;
            for (int i = 0; i < points.Length; i++) Check(points[i] == next[i], "Uç nabzı bitmiş çizgiyi titretiyor.");
            Check(a.transform.position == apos && b.transform.position == bpos, "Bağ kurulunca çizimler taşındı.");
            var cameraObject = new GameObject("Test camera", typeof(Camera)); cameraObject.transform.SetParent(host.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(board.boardTransform.TransformPoint(new Vector3(0, 0, -3)), board.boardTransform.rotation);
            typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, true);
            typeof(DraggableFormula).GetField("mainCam", Private).SetValue(a, camera);
            a.targetPosition += Vector3.right; a.targetRotation *= Quaternion.Euler(0, 0, 90); a.isDragged = true;
            Quaternion rotation = a.transform.rotation; Call(a, "Update");
            Check(!a.isDragged && a.transform.position == apos && a.transform.rotation == rotation,
                "Sabit tebeşir Update sırasında taşındı/döndü.");
            typeof(FormulaNode).GetField("mainCam", Private).SetValue(node, camera);
            Call(node, "DrawDraggingLine");
            var preview = (Component)typeof(FormulaNode).GetField("chalkPreview", Private).GetValue(node);
            Check(preview != null, "Bağ önizlemesi çizilmedi.");
            foreach (var point in preview.GetComponent<MeshFilter>().sharedMesh.vertices)
                Check(Mathf.Abs(point.z - .018f) < .0001f, "Önizleme tahta düzleminden ayrıldı.");
            next = mesh.vertices;
            for (int i = 0; i < points.Length; i++) Check(points[i] == next[i], "Önizleme bitmiş bağı değiştirdi.");
            Call(node, "CancelDrawing"); Check(!preview.gameObject.activeSelf && node.isOccupied, "Önizleme iptali bağı sildi.");
            node.ConnectTo(other); Call(node, "DrawConnection");
            var second = (Component)typeof(FormulaNode).GetField("chalkSecond", Private).GetValue(node);
            Check(node.bondType == BondType.Double && second != null && second.gameObject.activeSelf,
                "Çift bağın ikinci tebeşir izi eksik.");
            var save = new GameData(); board.SaveData(ref save); board.ResetBoard();
            board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(save)));
            foreach (var formula in board.boardTransform.GetComponentsInChildren<DraggableFormula>())
                Check(!(bool)canMove.GetValue(formula),
                    "Yüklenen tebeşir formülü taşınabilir hâle geldi.");
            a = board.boardTransform.GetComponentsInChildren<DraggableFormula>()[0];
            node = Array.Find(a.GetOwnNodes(), n => n.isOccupied); other = node.connectedNode;
            Call(node, "Awake"); Call(node, "DrawConnection");
            node.Erase(); Call(node, "DrawConnection");
            second = (Component)typeof(FormulaNode).GetField("chalkSecond", Private).GetValue(node);
            Check(node.bondType == BondType.Single && (second == null || !second.gameObject.activeSelf), "Çift bağ silinince ikinci iz kaldı.");
            if (node.isOccupied) { node.Erase(); Call(node, "DrawConnection"); }
            Check(!node.isOccupied && !other.isOccupied, "Tebeşir bağ silinmedi.");
            drawing = (Component)typeof(FormulaNode).GetField("chalkConnection", Private).GetValue(node);
            Check(drawing == null || !drawing.gameObject.activeSelf, "Silinen bağın tebeşir izi kaldı.");
            Debug.Log("FIXED CHALK PASS: stationary parts, flat rough bonds, stable pulse and erase.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, previous); }
    }

    private static DraggableFormula Create(ChalkboardManager board, FormulaItemSO data, Vector3 local) =>
        (DraggableFormula)typeof(ChalkboardManager).GetMethod("CreateFormula", Private).Invoke(board, new object[] { data, board.boardTransform.TransformPoint(local), false });
    private static void Call(object target, string method, params object[] args)
    {
        var found = target.GetType().GetMethod(method, Private | BindingFlags.Public);
        Check(found != null, "Eksik davranış: " + method); found.Invoke(target, args);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
