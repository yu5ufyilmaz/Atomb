using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BoardDocumentsValidation
{
    [MenuItem("Senzora/Doğrulama/Tahta Belgeleri ve Defter Taşıma")]
    public static void Run()
    {
        var host = new GameObject("Board document checks");
        GameObject root = null;
        var previousBoard = ChalkboardManager.Instance;
        try
        {
            var notebook = host.AddComponent<NotebookUI>();
            var workspace = host.GetComponent<NotebookWorkspaceController>() ?? host.AddComponent<NotebookWorkspaceController>();
            Field(workspace, "notebook").SetValue(workspace, notebook);
            Call(workspace, "BuildWorkspace");
            root = (GameObject)Field(workspace, "root").GetValue(workspace);
            root.SetActive(true);
            var window = root.transform.Find("Not Defteri");
            Check(window.Find("Başlıktan Sürükle").GetComponent<DraggableWorkspaceItem>() != null,
                "Defter başlığından taşınamıyor.");
            notebook.EnsureWorkspacePage().blocks.Add(new NotebookBlockData { instanceID = "information-1", blockType = NotebookBlockType.RuleClue,
                title = "İpucu", description = "Test açıklaması", position = Vector2.one * .5f });
            notebook.workspacePages.Add(new NotebookPageSaveData());
            notebook.workspacePages[1].blocks.Add(new NotebookBlockData { instanceID = "information-2", blockType = NotebookBlockType.RuleClue, title = "Diğer ipucu" });
            Check(typeof(NotebookWorkspaceController).GetMethod("ShowInformation") != null, "Defterde ayrı bilgi bölümü yok.");
            Call(workspace, "ShowInformation");
            var info = (RectTransform)Field(workspace, "informationPanel").GetValue(workspace);
            Check(info.gameObject.activeSelf && info.GetComponentsInChildren<NotebookDraggableBlock>().Length == 2,
                "Bilgiler bütün sayfalardaki ipuçlarını göstermiyor.");
            var card = info.GetComponentInChildren<NotebookDraggableBlock>();
            workspace.DeleteBlock(card);
            int count = 0; foreach (var page in notebook.workspacePages) count += page.blocks.Count;
            Check(count == 1, "Bilgi arşivinden silmek asıl notu silmedi.");

            var board = host.AddComponent<ChalkboardManager>();
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, board);
            typeof(ChalkboardManager).GetProperty("isMachineActive").SetValue(board, true);
            board.boardTransform = new GameObject("Board surface").transform;
            board.boardTransform.SetParent(host.transform);
            var documentsMethod = typeof(ChalkboardManager).GetMethod("EnsureDocuments");
            Check(documentsMethod != null, "Fiziksel tahta belge alanı yok.");
            var documents = (Component)documentsMethod.Invoke(board, null);
            var canvas = documents.GetComponentInChildren<Canvas>(true);
            Check(canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.transform.IsChildOf(board.boardTransform),
                "Görev alanı gerçek tahta yüzeyine bağlı değil.");
            Vector3 mounted = board.boardTransform.InverseTransformPoint(canvas.transform.position);
            Check(mounted.x > 0 && mounted.z > 0 && Vector3.Dot(canvas.transform.forward, board.boardTransform.forward) < -.99f,
                "Belgelerin fiziksel tahta önü/solu dolaşan kameranın bakışına göre ters döndü.");
            Check(board.ClampToBoardArea(new Vector3(board.maxBounds.x, 0, 0)).x < mounted.x - .45f,
                "Formüller belge kağıtlarının altına girip erişilemez olabiliyor.");
            Vector3 before = canvas.transform.position;
            board.boardTransform.position += Vector3.right * 2;
            Check(Vector3.Distance(canvas.transform.position, before + Vector3.right * 2) < .001f, "Belge alanı tahta ile taşınmadı.");
            var pinMethod = typeof(ChalkboardManager).GetMethod("TryPinInformation");
            Check(pinMethod != null, "Tahtaya bilgi notu bırakılamıyor.");
            var data = new NotebookBlockData { instanceID = "note", blockType = NotebookBlockType.RuleClue, title = "İpucu", description = "Kalıcı açıklama" };
            Check((bool)pinMethod.Invoke(board, new object[] { data }), "Geçerli bilgi notu kabul edilmedi.");
            pinMethod.Invoke(board, new object[] { data });
            var pinned = (List<NotebookBlockData>)typeof(ChalkboardManager).GetField("pinnedInformation").GetValue(board);
            Check(pinned.Count == 1 && !ReferenceEquals(pinned[0], data), "Tahta notu kopyalanmadı veya iki kez eklendi.");
            var save = new GameData(); board.SaveData(ref save);
            pinned[0].description = "Değiştirildi";
            board.ResetBoard();
            Check(pinned.Count == 0, "Tahta sıfırlanınca bilgi notları kaldı.");
            board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(save)));
            pinned = (List<NotebookBlockData>)typeof(ChalkboardManager).GetField("pinnedInformation").GetValue(board);
            Check(pinned.Count == 1 && pinned[0].description == "Kalıcı açıklama", "Tahta bilgisi bağımsız kaydedilip yüklenmedi.");
            Check(!(bool)pinMethod.Invoke(board, new object[] { new NotebookBlockData { blockType = NotebookBlockType.PartClue } }),
                "Parça kartı bilgi alanına kabul edildi.");
            Debug.Log("BOARD DOCUMENTS PASS: movable notebook, aggregate information, physical board attachment and independent persistent pins.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(host);
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, previousBoard);
        }
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static object Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
