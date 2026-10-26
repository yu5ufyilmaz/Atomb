using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class NotebookWorkspaceValidation
{
    [MenuItem("Senzora/Doğrulama/Defter Çalışma Alanı")]
    public static void RunAll()
    {
        ValidateMouseButtons();
        ValidateTargetMigrationAndGuide();
        ValidateBoardDrawing();
        ValidateIndependentCopies();
        ValidateOverloadedMethodSelection();
        ValidateFreehandEraser();
        ValidateDefaultWorkspace();
        ValidateCompletedProductArchive();
        Debug.Log("Defter doğrulaması geçti: bağımsız kopya, JSON, çizgi silgisi, uzun çizim ve varsayılan çalışma alanı kartları.");
    }

    private static void ValidateBoardDrawing()
    {
        var host = new GameObject("Board drawing save validation");
        try
        {
            var board = host.AddComponent<ChalkboardManager>();
            board.boardTransform = host.transform;
            var drawer = board.EnsureDrawing();
            drawer.BeginStroke(Vector2.zero, false);
            drawer.ContinueStroke(new Vector2(60, 30));
            drawer.EndStroke();
            var data = new GameData();
            board.SaveData(ref data);
            Check(data.savedChalkboard.strokes.Count == 1, "Tahta çizimi kayda eklenmedi.");
            drawer.Clear();
            Check(data.savedChalkboard.strokes.Count == 1, "Canlı çizim silinince kayıt kopyası da silindi.");
            board.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(data)));
            Check(drawer.Strokes.Count == 1 && drawer.Strokes[0].points.Count == 2, "Tahta çizimi yüklenemedi.");
            board.ResetBoard();
            Check(drawer.Strokes.Count == 0, "Yeni oyunda eski tahta çizimi kaldı.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void ValidateMouseButtons()
    {
        var host = new GameObject("Mouse drawing validation", typeof(RectTransform));
        try
        {
            var drawer = host.AddComponent<NotebookFreehandDrawer>();
            drawer.rectTransform.sizeDelta = new Vector2(100, 100);
            drawer.Tool = NotebookTool.Pen;
            drawer.SetStrokes(new List<NotebookStrokeData> { new NotebookStrokeData {
                points = new List<Vector2> { new Vector2(0, .5f), new Vector2(1, .5f) } } });
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, host.transform.position),
                button = PointerEventData.InputButton.Right };
            drawer.OnPointerDown(pointer);
            drawer.OnPointerUp(pointer);
            Check(drawer.Strokes.Count == 0, "Sağ fare düğmesi, araç değiştirmeden çizgiyi silmeli.");
            pointer.button = PointerEventData.InputButton.Left;
            drawer.OnPointerDown(pointer);
            drawer.OnPointerUp(pointer);
            Check(drawer.Strokes.Count == 1, "Sol fare düğmesi doğrudan çizmeli.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void ValidateIndependentCopies()
    {
        var original = new NotebookPageSaveData();
        original.blocks.Add(new NotebookBlockData { instanceID = "note-a", title = "Özgün", position = new Vector2(0.2f, 0.7f) });
        original.strokes.Add(new NotebookStrokeData { points = new List<Vector2> { Vector2.zero, Vector2.one } });
        var copy = original.Clone();
        copy.blocks[0].title = "Kopya";
        copy.strokes[0].points[0] = Vector2.one;
        copy.blocks.Add(new NotebookBlockData());
        Check(original.blocks.Count == 1 && original.blocks[0].title == "Özgün", "Blok kopyası kaynak sayfayı değiştirdi.");
        Check(original.strokes[0].points[0] == Vector2.zero, "Çizgi kopyası kaynak noktaları değiştirdi.");
        var restored = JsonUtility.FromJson<NotebookPageSaveData>(JsonUtility.ToJson(original));
        Check(restored.blocks[0].position == original.blocks[0].position && restored.strokes[0].points.Count == 2,
            "JSON, yerleşim veya çizgi noktalarını korumadı.");
    }

    private static void ValidateFreehandEraser()
    {
        var host = new GameObject("Notebook drawing validation", typeof(RectTransform));
        try
        {
            var drawer = host.AddComponent<NotebookFreehandDrawer>();
            Check(drawer.GetComponent<CanvasRenderer>() != null, "Kalem katmanında CanvasRenderer otomatik oluşturulmadı.");
            drawer.rectTransform.sizeDelta = new Vector2(100f, 100f);
            drawer.Tool = NotebookTool.Select;
            Check(!drawer.raycastTarget, "Seçim aracı kağıdın altındaki sürüklemeyi engelliyor.");
            drawer.Tool = NotebookTool.Pen;
            Check(drawer.raycastTarget, "Kalem çizim girdisi almıyor.");
            var saved = new List<NotebookStrokeData>
            {
                new NotebookStrokeData { width = 2f, points = new List<Vector2> { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f) } },
                new NotebookStrokeData { width = 2f, points = new List<Vector2> { new Vector2(0f, 0.95f), new Vector2(1f, 0.95f) } },
            };
            drawer.SetStrokes(saved);
            int changed = 0;
            drawer.Changed += () => changed++;
            // Uç noktalar uzakta olsa da ortadan geçen silgi hattı ilk çizgiyi bulmalı.
            Invoke(drawer, "Erase", new Vector2(0f, -20f), new Vector2(0f, 20f));
            Check(drawer.Strokes.Count == 1 && saved.Count == 2, "Silgi çizginin ortasını atladı veya kayıt kaynağını değiştirdi.");
            Check(changed == 1, "Silgi değişikliği kayıt katmanına bildirilmedi.");
            Invoke(drawer, "Erase", new Vector2(0f, -20f), new Vector2(0f, -20f));
            Check(drawer.Strokes.Count == 1 && changed == 1, "Uzak çizgi silindi veya boş silgi hareketi veri değişikliği üretti.");

            MethodInfo distance = typeof(NotebookFreehandDrawer).GetMethod("SegmentDistanceSquared", BindingFlags.Static | BindingFlags.NonPublic);
            Check(distance != null, "Silgi geometrisi bulunamadı.");
            float crossing = (float)distance.Invoke(null, new object[] { new Vector2(-5, 0), new Vector2(5, 0), new Vector2(0, -5), new Vector2(0, 5) });
            float parallel = (float)distance.Invoke(null, new object[] { Vector2.zero, Vector2.right, new Vector2(0, 3), new Vector2(1, 3) });
            float point = (float)distance.Invoke(null, new object[] { Vector2.zero, Vector2.zero, new Vector2(3, 4), new Vector2(3, 4) });
            Check(Mathf.Approximately(crossing, 0f) && Mathf.Approximately(parallel, 9f) && Mathf.Approximately(point, 25f),
                "Kesişim, paralel veya tek nokta silgi uzaklığı yanlış.");

            var longStroke = new NotebookStrokeData();
            for (int i = 0; i < 9000; i++) longStroke.points.Add(new Vector2(i / 9000f, 0.5f));
            drawer.SetStrokes(new List<NotebookStrokeData> { longStroke });
            var rendered = host.GetComponentsInChildren<MaskableGraphic>();
            Check(rendered.Length > 2, "Uzun kalem izi birden fazla UI ağına bölünmedi.");
            int totalVertices = 0;
            foreach (var graphic in rendered)
            {
                Check(graphic.GetComponent<CanvasRenderer>() != null, "Kalem izinde CanvasRenderer eksik.");
                graphic.SetClipRect(new Rect(0, 0, 100, 100), true);
                if (graphic == drawer) continue;
                using (var vertices = new VertexHelper())
                {
                    Invoke(graphic, "OnPopulateMesh", vertices);
                    Check(vertices.currentVertCount > 0 && vertices.currentVertCount < 65000, "Çizim ağı Unity vertex sınırını aştı veya boş kaldı.");
                    totalVertices += vertices.currentVertCount;
                }
            }
            Check(totalVertices >= 9000 * 4, "Uzun kalem izinin son kısmı görüntülenmedi.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void ValidateDefaultWorkspace()
    {
        // Editörde Awake çalıştırmadan gerçek NotebookUI ve çalışma alanı oluşturucusunu kullan.
        Check(!Application.isPlaying, "Bu doğrulama editör modunda çalıştırılmalı.");
        bool hadEventSystem = EventSystem.current != null;
        var host = new GameObject("Notebook workspace validation");
        GameObject workspaceRoot = null;
        try
        {
            var notebook = host.AddComponent<NotebookUI>();
            var page = notebook.EnsureWorkspacePage();
            page.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue, title = "Test Modülü", position = Vector2.one * 0.5f });
            page.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.Quest, title = "Test Hedefi", position = Vector2.one * 0.5f });
            string longRule = "[UZUN-MODUL-A] ile [UZUN-MODUL-B] doğrudan bağlanamaz. Aralarında mutlaka [MODERATÖR] köprüsü olmalıdır. "
                + "Şemada en az iki çift bağ kullanılmalı ve hiçbir kararsız uç açıkta kalmamalıdır.";
            page.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.RuleClue, title = "Köprü ve kapasite kuralı", description = longRule, position = Vector2.one * 0.5f });
            var workspace = host.GetComponent<NotebookWorkspaceController>() ?? host.AddComponent<NotebookWorkspaceController>();
            typeof(NotebookWorkspaceController).GetField("notebook", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workspace, notebook);
            Invoke(workspace, "BuildWorkspace");
            workspaceRoot = (GameObject)typeof(NotebookWorkspaceController).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
            workspace.RefreshPage();
            Check(Array.Exists(workspaceRoot.GetComponentsInChildren<RectTransform>(true), r => r.name == "Toplanan Hedefler"),
                "Hedefler için adı görünen ayrı bir bırakma alanı yok.");
            var cards = workspaceRoot.GetComponentsInChildren<NotebookDraggableBlock>(true);
            foreach (var graphic in workspaceRoot.GetComponentsInChildren<MaskableGraphic>(true))
            {
                Check(graphic.GetComponent<CanvasRenderer>() != null, "Çalışma alanında CanvasRenderer eksik: " + graphic.name);
                graphic.SetClipRect(new Rect(0, 0, 500, 500), true);
            }
            Check(cards.Length == 3, "Varsayılan çalışma alanı kayıtlı blokları oluşturamadı.");
            foreach (var card in cards)
            {
                Vector2 size = card.GetComponent<RectTransform>().sizeDelta;
                Check(card.Data.blockType == NotebookBlockType.Quest ? size.x >= 300f && size.y >= 170f : size.x >= 190f && size.y >= 110f,
                    "Otomatik kart, varsayılan 100x100 RectTransform boyutunda kaldı.");
                var border = card.transform.Find("El Çizimi Çerçeve");
                Check(border != null, "Sprite atanmamış defter kartında el çizimi çerçevesi bulunamadı.");
                if (card.Data.blockType == NotebookBlockType.RuleClue)
                {
                    var body = card.transform.Find("Açıklama").GetComponent<TMPro.TextMeshProUGUI>();
                    float required = body.GetPreferredValues(longRule, size.x - 20f, 0f).y;
                    Check(size.y - 47f >= required && body.overflowMode != TMPro.TextOverflowModes.Ellipsis,
                        "Uzun kuralın çözüm için zorunlu son cümlesi kesildi.");
                }
            }
            var workspaceProperty = typeof(NotebookWorkspaceController).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            // Yirminci sayfa önceden eklenmeden açılır; yalnız dolu sayfalar etiketlenir.
            workspace.ToggleSynthesisGuide();
            workspace.GoToPage(19);
            var guidePanel = (RectTransform)typeof(NotebookWorkspaceController).GetField("guidePanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
            Check(!guidePanel.gameObject.activeSelf, "Sayfa etiketi seçilince hile görünümü açık kaldı.");
            Check(notebook.currentWorkspacePage == 19 && notebook.workspacePages.Count == 20, "Sayfalar otomatik açılmadı.");
            var tabs = (RectTransform)typeof(NotebookWorkspaceController).GetField("bookmarkContent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
            Check(tabs.childCount == 1, "Boş sayfalar dolu sayfa etiketi oluşturdu.");
            var drawer = workspaceRoot.GetComponentInChildren<NotebookFreehandDrawer>(true);
            drawer.BeginStroke(Vector2.zero, false);
            drawer.EndStroke();
            Check(tabs.childCount == 2 && notebook.workspacePages[19].strokes.Count == 1, "20. sayfanın çizimi veya etiketi kaydedilmedi.");
            drawer.BeginStroke(Vector2.zero, true);
            drawer.EndStroke();
            Check(tabs.childCount == 1, "Son çizim silinince boş sayfanın etiketi kaldırılmadı.");
            workspace.GoToPage(0);
            var restoredPage = new GameData();
            notebook.SaveData(ref restoredPage);
            Check(restoredPage.notebookPages.Count == 20 && restoredPage.notebookPages[0].blocks.Count == 2 && restoredPage.notebookQuests.Count == 1,
                "Otomatik sayfalar eski sayfa içeriğini bozdu.");
            var previousWorkspace = NotebookWorkspaceController.Instance;
            var previousControls = ControlsUIManager.Instance;
            try
            {
                workspaceProperty.SetValue(null, workspace);
                ControlsUIManager.Instance = null;
                workspaceRoot.SetActive(true);
                Invoke(host.AddComponent<DeathUIManager>(), "HideGameplayUI");
                Check(!workspaceRoot.activeSelf, "Ölüm ekranı çalışma alanının altında kaldı.");
            }
            finally
            {
                workspaceProperty.SetValue(null, previousWorkspace);
                ControlsUIManager.Instance = previousControls;
            }
        }
        finally
        {
            if (workspaceRoot != null) UnityEngine.Object.DestroyImmediate(workspaceRoot);
            UnityEngine.Object.DestroyImmediate(host);
            if (!hadEventSystem && EventSystem.current != null)
                UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }

    private static void ValidateCompletedProductArchive()
    {
        bool hadEventSystem = EventSystem.current != null;
        var host = new GameObject("Notebook archive validation");
        var previousGenerator = ProceduralSynthesisGenerator.Instance;
        var generatorProperty = typeof(ProceduralSynthesisGenerator).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        GameObject workspaceRoot = null;
        var definitions = new List<FormulaItemSO>();
        try
        {
            var notebook = host.AddComponent<NotebookUI>();
            var workspace = host.GetComponent<NotebookWorkspaceController>() ?? host.AddComponent<NotebookWorkspaceController>();
            typeof(NotebookWorkspaceController).GetField("notebook", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workspace, notebook);
            var generator = host.GetComponent<ProceduralSynthesisGenerator>() ?? host.AddComponent<ProceduralSynthesisGenerator>();
            generatorProperty.SetValue(null, generator);
            var run = new SynthesisRunData { currentStage = 1 };
            for (int i = 0; i < 5; i++)
            {
                var item = ScriptableObject.CreateInstance<FormulaItemSO>();
                item.itemID = "archive-output-" + i;
                item.itemName = "Ara Ürün " + i;
                generator.RegisterFormula(item);
                definitions.Add(item);
                run.quests.Add(new SynthesisQuestData { questID = "archive-quest-" + i, outputItemID = item.itemID, completed = i == 0 });
            }
            typeof(ProceduralSynthesisGenerator).GetProperty("Run").SetValue(generator, run);
            Invoke(workspace, "BuildWorkspace");
            workspaceRoot = (GameObject)typeof(NotebookWorkspaceController).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
            var book = host.AddComponent<InteractableBook>();
            typeof(NotebookWorkspaceController).GetField("activeBook", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workspace, book);
            workspace.RefreshBook(book);
            int recoverable = 0;
            foreach (var block in workspaceRoot.GetComponentsInChildren<NotebookDraggableBlock>(true))
            {
                if (!block.IsSource || block.Data.blockType != NotebookBlockType.PartClue) continue;
                Check(block.Data.itemID == definitions[0].itemID, "Henüz tamamlanmamış ürün arşivden alınabiliyor.");
                recoverable++;
            }
            Check(recoverable == 1, "Silinen tamamlanmış ara ürün arşivden geri alınamıyor.");
        }
        finally
        {
            generatorProperty.SetValue(null, previousGenerator);
            if (workspaceRoot != null) UnityEngine.Object.DestroyImmediate(workspaceRoot);
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var item in definitions) UnityEngine.Object.DestroyImmediate(item);
            if (!hadEventSystem && EventSystem.current != null) UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }

    private sealed class OverloadedProbe
    {
        private int Draw(int value) { return value; }
        private string Draw(string value) { return value; }
    }

    private static void ValidateTargetMigrationAndGuide()
    {
        var host = new GameObject("Target collection and guide validation");
        var notebookData = ScriptableObject.CreateInstance<NotebookData>();
        var recipe = ScriptableObject.CreateInstance<RecipeSO>();
        try
        {
            var notebook = host.AddComponent<NotebookUI>();
            var oldSave = new GameData();
            oldSave.notebookPages.Add(new NotebookPageSaveData());
            oldSave.notebookPages[0].blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = "legacy-target", title = "Eski hedef" });
            oldSave.notebookPages[0].blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue, title = "Parça" });
            notebook.LoadData(oldSave);
            Check(notebook.collectedQuests.Count == 1 && notebook.workspacePages[0].blocks.Count == 1, "Eski kayıt hedefi ayrı bölüme aktarılmadı.");
            notebook.EnsureCollectedQuests();
            Check(notebook.collectedQuests.Count == 1 && oldSave.notebookPages[0].blocks.Count == 2, "Hedef geçişi yinelendi veya eski kayıt nesnesini değiştirdi.");
            var saved = new GameData(); notebook.SaveData(ref saved);
            notebook.collectedQuests[0].title = "Değişti";
            Check(saved.notebookQuests[0].title == "Eski hedef", "Hedef kaydı canlı kartla aynı nesneyi paylaşıyor.");
            notebook.LoadData(JsonUtility.FromJson<GameData>(JsonUtility.ToJson(saved)));
            Check(notebook.collectedQuests.Count == 1 && notebook.collectedQuests[0].questID == "legacy-target", "Hedef JSON kaydı geri gelmedi.");

            var board = host.AddComponent<ChalkboardManager>();
            board.practiceQuest = AssetDatabase.LoadAssetAtPath<QuestBlueprintSO>(StarterPuzzleValidation.QuestPath);
            recipe.inputs.Add(board.practiceQuest.clueParts[0]);
            recipe.inputs.Add(board.practiceQuest.clueParts[0]);
            recipe.outputs.Add(board.practiceQuest.clueParts[1]);
            notebookData.synthesisRecipes.Add(recipe);
            board.allRecipes.Add(recipe); // Aynı tarif katalogda iki yerde bulunsa da bir kez göster.
            string original = JsonUtility.ToJson(board.practiceQuest);
            string random = JsonUtility.ToJson(UnityEngine.Random.state);
            var entries = NotebookSynthesisGuide.Build(board, null, notebookData);
            Check(entries.Count == 2, "Hile sayfasında tarif eksik veya aynı tarif iki kez listeleniyor.");
            Check(entries[0].solution.nodes.TrueForAll(n => !n.isUnknown), "Hile şeması gerçek parça kimliklerini göstermiyor.");
            Check(entries[0].text.Contains("BE-9") && entries[0].text.Contains("REF") && entries[0].text.Contains("çift bağ")
                && entries[0].text.Contains("SAĞ TIK") && entries[0].text.Contains("Doğru bildin"), "Hile sayfası çözüm veya işlem adımlarını eksik gösteriyor.");
            Check(entries[1].text.Contains("2× U-235") && entries[1].text.Contains("BU TAHTADA KAPALI"), "Tarif miktarları veya çalışma modu yanlış gösteriliyor.");
            Check(original == JsonUtility.ToJson(board.practiceQuest) && random == JsonUtility.ToJson(UnityEngine.Random.state), "Hile sayfası görev verisini veya rastgelelik durumunu değiştirdi.");
            var generator = host.AddComponent<ProceduralSynthesisGenerator>();
            var catalog = AssetDatabase.LoadAssetAtPath<NotebookData>("Assets/Scripts/Scriptables/NotebookPages/NewNotebookData.asset");
            generator.RegisterCatalog(catalog.synthesisCatalog);
            generator.GenerateRun(123456);
            Check(generator.Run != null && generator.Run.quests.Count == 5, "Rehberin koşu testi kurulamadı.");
            string runBefore = JsonUtility.ToJson(generator.Run);
            entries = NotebookSynthesisGuide.Build(board, generator, notebookData);
            Check(entries.Count == 7 && runBefore == JsonUtility.ToJson(generator.Run), "Hile rehberi gelecek sentez aşamalarını atladı veya koşuyu değiştirdi.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(notebookData);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidateOverloadedMethodSelection()
    {
        var probe = new OverloadedProbe();
        Check(Equals(Invoke(probe, "Draw", 5), 5), "Aynı isimli çizim metotlarının int imzası seçilemedi.");
        Check(Equals(Invoke(probe, "Draw", "çizim"), "çizim"), "Aynı isimli çizim metotlarının string imzası seçilemedi.");
    }

    private static object Invoke(object instance, string name, params object[] arguments)
    {
        Type[] parameterTypes = Array.ConvertAll(arguments, argument => argument.GetType());
        MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic,
            null, parameterTypes, null);
        Check(method != null, "Doğrulama hedefi bulunamadı: " + name);
        return method.Invoke(instance, arguments);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
