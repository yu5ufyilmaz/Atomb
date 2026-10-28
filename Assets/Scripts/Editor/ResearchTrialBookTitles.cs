using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Labels belong to the test-scene instances and follow the existing animated cover bones.
public static class ResearchTrialBookTitles
{
    private const string ScenePath = "Assets/Scenes/scn_SynthesisBlockout.unity";
    private static readonly string[] Titles = { "Araştırma Soruları", "Parça Atlası", "Bağ İlkeleri", "Deney Kayıtları" };

    [MenuItem("Senzora/Araştırma Denemeleri/Açık Sahnenin Kitap İsimliklerini Düzelt")]
    public static void ApplyCurrent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        ResearchPuzzleTrial trial = null;
        foreach (var root in scene.GetRootGameObjects())
        { trial = root.GetComponentInChildren<ResearchPuzzleTrial>(true); if (trial != null) break; }
        if (trial == null) throw new System.InvalidOperationException("Açık sahnede araştırma kitapları bulunamadı.");
        System.IO.Directory.CreateDirectory("UserSettings/PuzzleWorkshop");
        string backup = "UserSettings/PuzzleWorkshop/scene-before-shelf-labels-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true))
            throw new System.InvalidOperationException("Mevcut sahnenin yedeği alınamadı; isimliklere dokunulmadı.");
        if (!Apply(trial)) throw new System.InvalidOperationException("İsimlik güncellemesi tamamlanmadı.");
    }

    [MenuItem("Senzora/Araştırma Denemeleri/Kitap Başlıklarını Görüntüle")]
    public static void CaptureTitles()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) return;
        ResearchPuzzleTrial trial = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            trial = root.GetComponentInChildren<ResearchPuzzleTrial>(true);
            if (trial != null) break;
        }
        if (trial == null || trial.books == null || trial.books.Length < 2) return;
        var camera = Camera.main;
        if (camera == null) return;
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        var target = camera.targetTexture;
        var active = RenderTexture.active;
        float near = camera.nearClipPlane, fov = camera.fieldOfView;
        var render = new RenderTexture(1200, 800, 24);
        var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        try
        {
            System.IO.Directory.CreateDirectory("Temp/ResearchTrialChecks");
            foreach (var label in trial.books[1].GetComponentsInChildren<TextMeshPro>(true))
            {
                if (label.transform.parent.name != "Araştırma Kitap Adı / Kapak"
                    && label.transform.parent.name != "Araştırma Kitap Adı / Sırt") continue;
                label.ForceMeshUpdate();
                var corners = new Vector3[4];
                label.rectTransform.GetWorldCorners(corners);
                float span = Mathf.Max(Vector3.Distance(corners[0], corners[1]), Vector3.Distance(corners[1], corners[2]));
                camera.fieldOfView = 40;
                camera.nearClipPlane = .001f;
                camera.transform.SetPositionAndRotation(label.transform.position - label.transform.forward * span * 2.4f,
                    Quaternion.LookRotation(label.transform.forward, label.transform.up));
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
                pixels.Apply();
                string name = label.transform.parent.name.EndsWith("Kapak") ? "BookCover" : "BookSpine";
                System.IO.File.WriteAllBytes("Temp/ResearchTrialChecks/" + name + ".png", pixels.EncodeToPNG());
            }
            Debug.Log("RESEARCH BOOK TITLES CAPTURE: Temp/ResearchTrialChecks/BookCover.png and BookSpine.png");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.targetTexture = target;
            camera.nearClipPlane = near;
            camera.fieldOfView = fov;
            RenderTexture.active = active;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(render);
        }
    }

    [MenuItem("Senzora/Araştırma Denemeleri/Kitap Kapaklarını Adlandır")]
    public static void ApplyToTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        foreach (var root in scene.GetRootGameObjects())
        {
            var trial = root.GetComponentInChildren<ResearchPuzzleTrial>(true);
            if (trial == null) continue;
            if (Apply(trial)) EditorSceneManager.SaveScene(scene);
            return;
        }
        Debug.LogError("Test sahnesinde araştırma kitapları bulunamadı.");
    }

    public static bool Apply(ResearchPuzzleTrial trial)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || trial == null
            || (trial.gameObject.scene.path != ScenePath && !trial.allowGameplayScene) || trial.books == null) return false;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SynthesisBlockout/Blockout_Tahta.mat");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SpecialElite-Regular SDF.asset");
        if (material == null || font == null) { Debug.LogError("Kitap etiketi için mevcut materyal veya yazı tipi bulunamadı."); return false; }
        for (int i = 0; i < trial.books.Length; i++)
        {
            var book = trial.books[i];
            if (book == null || book.gameObject.scene != trial.gameObject.scene) return false;
            var renderer = book.bookSkinnedMeshRenderer;
            if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.subMeshCount < 2) return false;
            var cover = book.transform.Find("Armature.001/Root/Binding/Page.L");
            var spine = book.transform.Find("Armature.001/Root/Binding");
            if (cover == null || spine == null) return false;
            var shelf = book.transform.parent;
            while (shelf != null && !shelf.name.StartsWith("RAF", System.StringComparison.OrdinalIgnoreCase)) shelf = shelf.parent;
            if (shelf != null)
            {
                Vector3 outward = -spine.forward;
                outward = Mathf.Abs(outward.x) > Mathf.Abs(outward.z) ? Vector3.right * Mathf.Sign(outward.x) : Vector3.forward * Mathf.Sign(outward.z);
                Undo.RecordObject(book, "Raftan Alma Yönü");
                book.shelfPullDirection = book.transform.parent.InverseTransformDirection(outward);
                EditorUtility.SetDirty(book);
            }
            var baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked, true);
                if (!SurfaceBounds(renderer, baked, cover, out var coverBounds)
                    || !SurfaceBounds(renderer, baked, spine, out var spineBounds))
                { Debug.LogError("Kitabın kapak yüzeyi bulunamadı.", book); return false; }
                string title = book.TryGetComponent<PuzzleBookIdentity>(out var identity) && !string.IsNullOrWhiteSpace(identity.title) ? identity.title : Titles[i % Titles.Length];
                Plate(cover, "Araştırma Kitap Adı / Kapak", title, coverBounds,
                    new Vector2(coverBounds.size.y * 0.78f, coverBounds.size.x * 0.55f), 90, material, font);
                Plate(spine, "Araştırma Kitap Adı / Sırt", title, spineBounds,
                    new Vector2(spineBounds.size.x * 0.80f, spineBounds.size.y * 0.65f), 0, material, font);
                if (i == 0) Debug.Log($"RESEARCH BOOK TITLES: cover bone bounds {coverBounds}; spine bone bounds {spineBounds}.", book);
            }
            finally { Object.DestroyImmediate(baked); }
        }
        EditorSceneManager.MarkSceneDirty(trial.gameObject.scene);
        ApplyShelfSigns(trial, material, font);
        foreach (var book in trial.books) foreach (var label in book.GetComponentsInChildren<TextMeshPro>(true))
            if (label.font == font) label.ForceMeshUpdate(true);
        AssetDatabase.SaveAssetIfDirty(font);
        Debug.Log("Araştırma kitaplarının kapak ve sırt başlıkları test sahnesine eklendi.", trial);
        return true;
    }

    private static void ApplyShelfSigns(ResearchPuzzleTrial trial, Material material, TMP_FontAsset font)
    {
        var cabinets = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        var groups = new Dictionary<Transform, List<InteractableBook>>();
        foreach (var book in trial.books)
        {
            var shelf = book.transform.parent;
            while (shelf != null && !shelf.name.StartsWith("RAF")) shelf = shelf.parent;
            if (shelf == null) continue;
            if (!groups.ContainsKey(shelf)) groups[shelf] = new List<InteractableBook>();
            groups[shelf].Add(book);
        }
        foreach (var group in groups)
        {
            var bounds = new Bounds(group.Value[0].transform.position, Vector3.zero);
            foreach (var book in group.Value) foreach (var collider in book.GetComponentsInChildren<Collider>()) bounds.Encapsulate(collider.bounds);
            var holder = Child(group.Key, "Araştırma Raf İsimliği");
            Vector3 facing = group.Value[0].transform.parent.TransformDirection(group.Value[0].shelfPullDirection).normalized;
            Bounds front = bounds;
            float nearest = float.PositiveInfinity;
            foreach (var cabinet in cabinets)
            {
                if (cabinet.gameObject.scene != trial.gameObject.scene || cabinet.name.IndexOf("BookCase", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                float distance = (cabinet.bounds.ClosestPoint(bounds.center) - bounds.center).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; front = cabinet.bounds;
            }
            float reach = Vector3.Dot(front.extents, new Vector3(Mathf.Abs(facing.x), 0, Mathf.Abs(facing.z)));
            Vector3 point = front.center + facing * (reach + .035f);
            point.y = front.max.y - .115f;
            holder.SetPositionAndRotation(point, Quaternion.LookRotation(-facing));
            holder.localScale = Vector3.one;
            var plate = Child(holder, "Plaka");
            var filter = plate.GetComponent<MeshFilter>();
            if (filter == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                filter = plate.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(primitive);
                plate.gameObject.AddComponent<MeshRenderer>();
            }
            plate.localPosition = Vector3.zero; plate.localRotation = Quaternion.identity; plate.localScale = new Vector3(.65f, .16f, .012f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = material;
            var textHolder = Child(holder, "Başlık");
            var text = textHolder.GetComponent<TextMeshPro>() ?? textHolder.gameObject.AddComponent<TextMeshPro>();
            text.transform.localPosition = new Vector3(0, 0, -.008f); text.transform.localRotation = Quaternion.identity; text.transform.localScale = Vector3.one * .001f;
            text.rectTransform.sizeDelta = new Vector2(600, 140); text.font = font; text.fontSharedMaterial = font.material;
            text.fontSize = text.fontSizeMax = 1000; text.fontSizeMin = 600; text.enableAutoSizing = true;
            text.fontStyle = FontStyles.Normal; text.characterSpacing = 2; text.extraPadding = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = group.Value[0].GetComponent<PuzzleBookIdentity>().location.ToUpperInvariant();
            text.color = new Color(.96f, .89f, .70f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        }
    }

    private static bool SurfaceBounds(SkinnedMeshRenderer renderer, Mesh baked, Transform bone, out Bounds bounds)
    {
        bounds = default;
        int boneIndex = System.Array.IndexOf(renderer.bones, bone);
        if (boneIndex < 0) return false;
        var vertices = baked.vertices;
        var weights = renderer.sharedMesh.boneWeights;
        bool found = false;
        foreach (int index in new HashSet<int>(renderer.sharedMesh.GetTriangles(1)))
        {
            var weight = weights[index];
            float amount = (weight.boneIndex0 == boneIndex ? weight.weight0 : 0)
                + (weight.boneIndex1 == boneIndex ? weight.weight1 : 0)
                + (weight.boneIndex2 == boneIndex ? weight.weight2 : 0)
                + (weight.boneIndex3 == boneIndex ? weight.weight3 : 0);
            if (amount < 0.95f) continue;
            var point = bone.InverseTransformPoint(renderer.transform.TransformPoint(vertices[index]));
            if (found) bounds.Encapsulate(point);
            else { bounds = new Bounds(point, Vector3.zero); found = true; }
        }
        return found && bounds.size.x > 0 && bounds.size.y > 0;
    }

    private static void Plate(Transform bone, string name, string title, Bounds bounds, Vector2 size,
        float rotation, Material material, TMP_FontAsset font)
    {
        var holder = Child(bone, name);
        Undo.RecordObject(holder, "Kitap Etiketini Yerleştir");
        float lift = Mathf.Max(bounds.size.z * 0.035f, size.y * 0.012f);
        holder.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - lift);
        holder.localRotation = Quaternion.Euler(0, 0, rotation);
        holder.localScale = Vector3.one;
        var plate = FindChild(holder, "Plaka");
        if (plate == null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Plaka";
            SceneManager.MoveGameObjectToScene(obj, bone.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(obj, "Kitap Etiketini Oluştur");
            Undo.SetTransformParent(obj.transform, holder, "Kitap Etiketini Ekle");
            Undo.DestroyObjectImmediate(obj.GetComponent<Collider>());
            plate = obj.transform;
        }
        Undo.RecordObject(plate, "Kitap Etiketi Boyutu");
        plate.localPosition = Vector3.zero;
        plate.localRotation = Quaternion.identity;
        plate.localScale = new Vector3(size.x, size.y, lift);
        var mesh = plate.GetComponent<Renderer>();
        Undo.RecordObject(mesh, "Kitap Etiketi Materyali");
        mesh.sharedMaterial = material;
        var textTransform = Child(holder, "Başlık");
        var text = textTransform.GetComponent<TextMeshPro>();
        if (text == null) text = Undo.AddComponent<TextMeshPro>(textTransform.gameObject);
        Undo.RecordObject(text.transform, "Kitap Başlığını Yerleştir");
        text.transform.localPosition = Vector3.back * lift;
        text.transform.localRotation = Quaternion.identity;
        // Rig units are tiny; keep TMP auto-sizing above its 0.05 point rounding step.
        const float textUnits = 1000;
        text.transform.localScale = Vector3.one / textUnits;
        text.rectTransform.sizeDelta = size * (0.87f * textUnits);
        Undo.RecordObject(text, "Kitap Başlığını Yaz");
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontStyle = FontStyles.Normal;
        text.characterSpacing = 1.5f;
        text.lineSpacing = 4;
        text.extraPadding = true;
        text.text = title;
        text.color = new Color(0.96f, 0.89f, 0.70f, 1);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSize = text.fontSizeMax = size.y * 7 * textUnits;
        text.fontSizeMin = size.y * textUnits;
        text.textWrappingMode = name.EndsWith("Kapak") ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent) if (child.name == name) return child;
        return null;
    }

    private static Transform Child(Transform parent, string name)
    {
        var existing = FindChild(parent, name);
        if (existing != null) return existing;
        var obj = new GameObject(name);
        SceneManager.MoveGameObjectToScene(obj, parent.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(obj, "Kitap Başlığını Oluştur");
        Undo.SetTransformParent(obj.transform, parent, "Kitap Başlığını Ekle");
        return obj.transform;
    }
}
