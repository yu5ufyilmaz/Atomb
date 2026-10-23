using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// scn_SynthesisBlockout sahnesini Play'e basmadan düzenlenebilir hale getirir:
// zemin, duvarlar, masa, oyuncu, kamera, kitap, tahta vb. sahneye normal GameObject olarak yerleştirilir.
public static class SynthesisBlockoutSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/scn_SynthesisBlockout.unity";
    private const string MaterialFolder = "Assets/Materials/SynthesisBlockout";
    private const string ClueFolder = "Assets/Scripts/Scriptables/BookClues";
    private const string ClueAssetPath = ClueFolder + "/BookClue_Blockout.asset";

    // Sahneyi silmeden test kitabına sayfa görüntü verisi (kırpılan parça bölgeleri) ekler.
    [MenuItem("Senzora/Sentez Blockout/Kitaba Sayfa Görüntü Verisi Ekle (Sahneyi Silmeden)")]
    public static void AddClueDataToExistingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Blockout", "Önce Play modundan çıkın.", "Tamam");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = Object.FindFirstObjectByType<SynthesisBlockoutScene>();
        if (root == null || root.book == null)
        {
            Debug.LogError("Sahnede test kitabı yok. Önce 'Sahneyi Düzenlenebilir Objelerle Oluştur' komutunu çalıştırın.");
            return;
        }
        ApplyClueData(root.book, root.notebookData);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = root.book.clueData;
    }

    // Kitabın kimlik dokusundan yer tutucu bölgeli bir BookClueData oluşturur (varsa mevcut olan kullanılır,
    // elle yapılan bölge ayarları korunur) ve kitaba atar. Bölgeler varsa sarı yer tutucu kartlar kaldırılır.
    private static void ApplyClueData(InteractableBook book, NotebookData notebookData)
    {
        if (book.bookIdentity == null || book.bookIdentity.pageTexture == null)
        {
            Debug.LogWarning("Kitap prefabında Book Identity (sayfa dokusu) yok; sayfa görüntü verisi eklenmedi, sarı kartlar kullanılacak.", book);
            return;
        }
        var clue = AssetDatabase.LoadAssetAtPath<BookClueData>(ClueAssetPath);
        if (clue == null)
        {
            if (!AssetDatabase.IsValidFolder(ClueFolder)) AssetDatabase.CreateFolder("Assets/Scripts/Scriptables", "BookClues");
            var parts = new List<FormulaItemSO>();
            if (notebookData != null)
                foreach (var item in notebookData.synthesisCatalog)
                {
                    if (item == null || item.isDecoy) continue;
                    parts.Add(item);
                    if (parts.Count == 3) break;
                }
            clue = BookClueDataEditor.CreateTestData(ClueAssetPath, book.bookIdentity, parts);
        }
        book.clueData = clue;
        if (book.synthesisPages != null)
            foreach (var page in book.synthesisPages)
                if (page != null) page.blocks.Clear();
        EditorUtility.SetDirty(book);
        PrefabUtility.RecordPrefabInstancePropertyModifications(book);
    }

    [MenuItem("Senzora/Sentez Blockout/Sahneyi Düzenlenebilir Objelerle Oluştur")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Blockout", "Önce Play modundan çıkın.", "Tamam");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = Object.FindFirstObjectByType<SynthesisBlockoutScene>();
        if (root == null)
        {
            Debug.LogError("scn_SynthesisBlockout içinde SynthesisBlockoutScene scripti bulunamadı.");
            return;
        }
        if (root.bookPrefab == null)
        {
            Debug.LogError("SynthesisBlockoutScene > Book Prefab alanına P_Book atanmalı.", root);
            return;
        }
        if (root.transform.childCount > 0 && !EditorUtility.DisplayDialog("Blockout",
            "Sahnede zaten objeler var. Yeniden oluşturmak elle yaptığınız değişiklikleri siler. Devam edilsin mi?",
            "Yeniden Oluştur", "İptal"))
            return;

        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        root.gameObject.name = "Synthesis Blockout";
        Transform parent = root.transform;
        var notebookData = root.notebookData;
        bool procedural = root.enableProceduralQuests;

        Material floorMat = GetMaterial("Blockout_Zemin", new Color(0.2f, 0.23f, 0.25f));
        Material wallMat = GetMaterial("Blockout_Duvar", new Color(0.32f, 0.35f, 0.37f));
        Material tableMat = GetMaterial("Blockout_Masa", new Color(0.37f, 0.28f, 0.2f));
        Material legMat = GetMaterial("Blockout_MasaAyagi", new Color(0.24f, 0.2f, 0.17f));
        Material boardMat = GetMaterial("Blockout_Tahta", new Color(0.04f, 0.12f, 0.09f));

        var light = new GameObject("Kitap Test Işığı", typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(parent);
        light.transform.rotation = Quaternion.Euler(55, -25, 0);
        light.type = LightType.Directional;
        light.intensity = 1000;

        var environment = new GameObject("Ortam").transform;
        environment.SetParent(parent);
        Cube(environment, "Zemin", new Vector3(0, -0.15f, 1), new Vector3(10, 0.3f, 9), floorMat);
        Cube(environment, "Arka Duvar", new Vector3(0, 1.5f, 4.15f), new Vector3(10, 3, 0.2f), wallMat);
        Cube(environment, "Sol Duvar", new Vector3(-5, 1.5f, 1), new Vector3(0.2f, 3, 6), wallMat);
        Cube(environment, "Sağ Duvar", new Vector3(5, 1.5f, 1), new Vector3(0.2f, 3, 6), wallMat);
        Cube(environment, "Ön Duvar", new Vector3(0, 1.5f, -2.1f), new Vector3(10, 3, 0.2f), wallMat);
        Cube(environment, "Masa", new Vector3(-1.8f, 0.8f, 2.8f), new Vector3(1.6f, 0.12f, 1), tableMat);
        foreach (float x in new[] { -2.4f, -1.2f })
            Cube(environment, "Masa Ayağı", new Vector3(x, 0.37f, 2.8f), new Vector3(0.12f, 0.74f, 0.7f), legMat);

        var rig = new GameObject("Test Oyuncusu", typeof(UnityEngine.CharacterController), typeof(SynthesisBlockoutPlayer));
        rig.tag = "Player";
        rig.transform.SetParent(parent);
        rig.transform.position = new Vector3(0, 0.05f, -1);
        var capsule = rig.GetComponent<UnityEngine.CharacterController>();
        capsule.height = 1.8f; capsule.center = Vector3.up * 0.9f; capsule.radius = 0.25f;
        var cameraObject = new GameObject("Test Kamera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = Vector3.up * 1.65f;
        var camera = cameraObject.GetComponent<Camera>();
        camera.nearClipPlane = 0.02f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f);
        var player = rig.GetComponent<SynthesisBlockoutPlayer>();
        player.viewCamera = camera;

        var notebook = new GameObject("Test Defter Verisi").AddComponent<NotebookUI>();
        notebook.transform.SetParent(parent);
        notebook.notebookData = notebookData;
        // Edit modunda Awake çalışmadığı için çalışma alanı bileşenini burada ekliyoruz.
        notebook.gameObject.AddComponent<NotebookWorkspaceController>().generateProceduralRun = procedural;

        var book = (InteractableBook)PrefabUtility.InstantiatePrefab(root.bookPrefab, parent);
        book.name = "Test Kitabı - P_Book";
        book.transform.position = new Vector3(-1.8f, 0.92f, 2.6f);
        FitBookToTable(book);
        book.ClearPassword();
        book.emissionIntensity = 0;
        book.usePhysicalPageBlocks = true;
        book.gameObject.AddComponent<BookPageBlockSurface>();
        book.canContainPassword = false;
        book.includeProceduralClues = procedural;
        book.cameraTransform = camera.transform;
        book.moveDuration = 0.2f;
        book.viewPositionOffset = new Vector3(0, -0.12f, 0.75f);
        var page = new BookWorkspacePage { title = "Etkileşim Denemesi", explanation = "Blokları deftere, defterden tahtaya sürükle. Pencereleri başlıklarından taşı. Bu blockout'ta rastgele görev ve kayıt kullanılmaz." };
        if (notebookData != null)
            foreach (var item in notebookData.synthesisCatalog)
            {
                if (item == null || item.isDecoy) continue;
                page.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = item.itemID, title = item.itemName, description = "Sabit test parçası" });
                if (page.blocks.Count == 3) break;
            }
        book.synthesisPages = new List<BookWorkspacePage> { page };
        ApplyClueData(book, notebookData);
        player.book = book;
        player.bookView = View(parent, "Kitap Bakış Noktası", new Vector3(-1.8f, 1.65f, 1.1f), new Vector3(25, 0, 0));

        var boardObject = Cube(parent, "Test Tahtası", new Vector3(1.65f, 1.7f, 3.85f), new Vector3(3.6f, 2.5f, 0.12f), boardMat);
        var interaction = boardObject.AddComponent<InteractableChalkboard>();
        interaction.promptText = "[Sol Tık] Test Tahtasına Geç";
        var manager = new GameObject("Test ChalkboardManager").AddComponent<ChalkboardManager>();
        manager.transform.SetParent(parent);
        manager.boardTransform = View(parent, "Tahta Çizim Yüzeyi", new Vector3(1.65f, 1.7f, 3.77f), new Vector3(0, 180, 0));
        manager.minBounds = new Vector2(-1.65f, -1.1f);
        manager.maxBounds = new Vector2(1.65f, 1.1f);
        manager.useProceduralQuests = procedural;
        if (notebookData != null) manager.formulaCatalog.AddRange(notebookData.synthesisCatalog);
        var zone = new GameObject("Test Sentez Alanı", typeof(BoxCollider), typeof(ReactionZone));
        zone.transform.SetParent(manager.boardTransform, false);
        var zoneCollider = zone.GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true; zoneCollider.size = new Vector3(3.2f, 2.1f, 0.5f);
        manager.synthesisZone = zone.GetComponent<ReactionZone>();
        player.board = interaction;
        player.boardView = View(parent, "Tahta Bakış Noktası", new Vector3(1.65f, 1.7f, 1.05f), Vector3.zero);
        Label(parent, "KİTAP / 1", new Vector3(-1.8f, 1.4f, 3.92f));
        Label(parent, "TAHTA / 2", new Vector3(1.65f, 3.1f, 3.8f));

        root.book = book;
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        Debug.Log("scn_SynthesisBlockout artık düzenlenebilir objelerle dolu. Hierarchy'den her şeyi taşıyıp değiştirebilirsiniz.", root);
    }

    private static Transform View(Transform parent, string title, Vector3 position, Vector3 angles)
    {
        var item = new GameObject(title).transform;
        item.SetParent(parent); item.position = position; item.rotation = Quaternion.Euler(angles);
        return item;
    }

    private static void FitBookToTable(InteractableBook book)
    {
        var renderers = book.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (size <= 0.001f) return;
        book.transform.localScale *= 0.55f / size;
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        book.transform.position += new Vector3(-1.8f - bounds.center.x, 0.87f - bounds.min.y, 2.6f - bounds.center.z);
    }

    private static GameObject Cube(Transform parent, string title, Vector3 position, Vector3 scale, Material material)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = title; item.transform.SetParent(parent); item.transform.position = position; item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = material;
        return item;
    }

    private static void Label(Transform parent, string title, Vector3 position)
    {
        var label = new GameObject(title).AddComponent<TextMeshPro>();
        label.transform.SetParent(parent); label.transform.position = position;
        label.text = title; label.fontSize = 2; label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(2.5f, 0.4f);
    }

    // Materyaller asset olarak saklanır; varsa yeniden oluşturmada elle yapılan ayarlar korunur.
    private static Material GetMaterial(string materialName, Color color)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "SynthesisBlockout");
        material = new Material(Shader.Find("HDRP/Unlit"));
        material.SetColor("_UnlitColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
