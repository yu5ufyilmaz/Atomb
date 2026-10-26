using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the research stations without rebuilding the existing blockout.
public static class ResearchTrialSceneSetup
{
    private const string ScenePath = "Assets/Scenes/scn_SynthesisBlockout.unity";
    private const string PartPath = "Assets/Scripts/Scriptables/Formulas/SynthesisModuleA.asset";

    [MenuItem("Senzora/Araştırma Denemeleri/Test Sahnesine Kur")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Araştırma denemelerini kurmadan önce Play modundan çıkın.");
            return;
        }

        var part = AssetDatabase.LoadAssetAtPath<FormulaItemSO>(PartPath);
        if (part == null)
        {
            Debug.LogError("Araştırma denemesi parça şablonu bulunamadı: " + PartPath);
            return;
        }

        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SynthesisBlockoutScene root = null;
        foreach (var item in scene.GetRootGameObjects())
        {
            root = item.GetComponentInChildren<SynthesisBlockoutScene>(true);
            if (root != null) break;
        }
        if (root == null || root.book == null || root.bookPrefab == null
            || root.book.gameObject.scene != scene || !EditorUtility.IsPersistent(root.bookPrefab))
        {
            Debug.LogError("Test sahnesinde mevcut kitap ve kitap prefabı atanmış bir SynthesisBlockoutScene gerekli.");
            return;
        }

        var sourceTable = root.transform.Find("Ortam/Masa");
        var tableRenderer = sourceTable != null ? sourceTable.GetComponent<Renderer>() : null;
        var tableMaterial = tableRenderer != null ? tableRenderer.sharedMaterial
            : AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SynthesisBlockout/Blockout_Masa.mat");
        if (tableMaterial == null)
        {
            Debug.LogError("Test sahnesinin masa materyali bulunamadı.", root);
            return;
        }
        var sourceLabel = root.GetComponentInChildren<TextMeshPro>(true);
        var tablePosition = sourceTable != null ? sourceTable.position : new Vector3(-1.8f, 0.8f, 2.8f);
        var bookOffset = root.book.transform.position - tablePosition;

        var previousActiveScene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Araştırma Denemelerini Kur");
            var trial = root.GetComponent<ResearchPuzzleTrial>();
            if (trial == null) trial = Undo.AddComponent<ResearchPuzzleTrial>(root.gameObject);
            Undo.RecordObject(trial, "Araştırma Kitaplarını Ata");
            trial.partTemplate = part;
            var books = new InteractableBook[4];
            books[0] = root.book;

            var stations = Child(root.transform, "Araştırma İstasyonları");
            books[1] = Station(stations, "Parça Atlası İstasyonu", "PARÇA ATLASI / 3",
                new Vector3(-3.7f, 0, 0.6f), new Vector3(-0.65f, 1.4f, 0), -90,
                root.bookPrefab, root.book, bookOffset, tableMaterial, sourceLabel);
            books[2] = Station(stations, "Bağ İlkeleri İstasyonu", "BAĞ İLKELERİ / 4",
                new Vector3(-1.8f, 0, -0.9f), new Vector3(0, 1.4f, -0.65f), 180,
                root.bookPrefab, root.book, bookOffset, tableMaterial, sourceLabel);
            books[3] = Station(stations, "Deney Kayıtları İstasyonu", "DENEY KAYITLARI / 5",
                new Vector3(3.4f, 0, 0.5f), new Vector3(0.65f, 1.4f, 0), 90,
                root.bookPrefab, root.book, bookOffset, tableMaterial, sourceLabel);
            trial.books = books;
            ResearchTrialBookTitles.Apply(trial);
            EditorUtility.SetDirty(trial);
            PrefabUtility.RecordPrefabInstancePropertyModifications(trial);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            if (EditorSceneManager.SaveScene(scene))
                Debug.Log("Araştırma denemeleri yalnız scn_SynthesisBlockout sahnesine kuruldu.", trial);
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static InteractableBook Station(Transform parent, string name, string title, Vector3 position,
        Vector3 labelOffset, float labelYaw, InteractableBook prefab, InteractableBook original,
        Vector3 bookOffset, Material material, TextMeshPro sourceLabel)
    {
        var station = Child(parent, name);
        Undo.RecordObject(station, "Araştırma İstasyonunu Yerleştir");
        station.position = position;
        station.rotation = Quaternion.identity;
        station.localScale = Vector3.one;
        Cube(station, "Masa", new Vector3(0, 0.8f, 0), new Vector3(1.35f, 0.12f, 1), material);
        Cube(station, "Kaide", new Vector3(0, 0.37f, 0), new Vector3(0.75f, 0.74f, 0.65f), material);

        var book = station.GetComponentInChildren<InteractableBook>(true);
        if (book == null)
        {
            book = (InteractableBook)PrefabUtility.InstantiatePrefab(prefab, station);
            Undo.RegisterCreatedObjectUndo(book.gameObject, "Araştırma Kitabını Oluştur");
        }
        Undo.RecordObject(book.transform, "Araştırma Kitabını Yerleştir");
        book.transform.localPosition = Vector3.up * 0.8f + bookOffset;
        book.transform.rotation = original.transform.rotation;
        book.transform.localScale = original.transform.localScale;
        Undo.RecordObject(book, "Araştırma Kitabını Ayarla");
        Undo.RecordObject(book.gameObject, "Araştırma Kitabını Adlandır");
        book.name = title;
        book.cameraTransform = original.cameraTransform;
        book.viewPositionOffset = original.viewPositionOffset;
        book.viewRotationOffset = original.viewRotationOffset;
        book.moveDuration = original.moveDuration;
        book.initialState = InteractableBook.BookInitialState.Closed;
        book.startOpen = false;
        book.startPageIndex = 0;
        book.canContainPassword = false;
        book.includeProceduralClues = false;
        book.usePhysicalPageBlocks = true;
        book.clueData = null;
        book.emissionIntensity = 0;
        book.ClearPassword();
        if (book.GetComponent<BookPageBlockSurface>() == null)
            Undo.AddComponent<BookPageBlockSurface>(book.gameObject);
        EditorUtility.SetDirty(book);
        PrefabUtility.RecordPrefabInstancePropertyModifications(book);
        PrefabUtility.RecordPrefabInstancePropertyModifications(book.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(book.transform);

        var labelTransform = Child(station, "İstasyon Etiketi");
        var label = labelTransform.GetComponent<TextMeshPro>();
        if (label == null) label = Undo.AddComponent<TextMeshPro>(labelTransform.gameObject);
        Undo.RecordObject(label.transform, "Araştırma Etiketini Yerleştir");
        label.transform.localPosition = labelOffset;
        // TMP's readable face is local -Z, so these labels face into the room.
        label.transform.localRotation = Quaternion.Euler(0, labelYaw, 0);
        Undo.RecordObject(label, "Araştırma Etiketini Ayarla");
        if (sourceLabel != null) label.font = sourceLabel.font;
        label.text = title;
        label.fontSize = 2.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.rectTransform.sizeDelta = new Vector2(2.8f, 0.45f);
        label.raycastTarget = false;
        EditorUtility.SetDirty(label);
        return book;
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child;
        var item = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(item, "Araştırma İstasyon Nesnesini Oluştur");
        // Parent in the target scene before any configuration; other open scenes are untouched.
        SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
        Undo.SetTransformParent(item.transform, parent, "Araştırma İstasyon Nesnesini Ekle");
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = Vector3.one;
        return item.transform;
    }

    private static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var item = parent.Find(name);
        if (item == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Undo.RegisterCreatedObjectUndo(cube, "Araştırma Masasını Oluştur");
            SceneManager.MoveGameObjectToScene(cube, parent.gameObject.scene);
            Undo.SetTransformParent(cube.transform, parent, "Araştırma Masasını Ekle");
            item = cube.transform;
        }
        Undo.RecordObject(item, "Araştırma Masasını Yerleştir");
        item.localPosition = position;
        item.localRotation = Quaternion.identity;
        item.localScale = scale;
        var renderer = item.GetComponent<Renderer>();
        if (renderer == null) return;
        Undo.RecordObject(renderer, "Araştırma Masası Materyali");
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
    }
}
