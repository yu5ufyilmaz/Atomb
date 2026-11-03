using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Koridordaki SM_PictureFrame3 tek mesh içinde 5 tabloydu (3 Kasım oyun testi); ileride her birine etkileşim eklenecek.
// Mesh'i bağlı parçalarına böler; FBX'e dokunmaz, parçaları Assets/Models/Revisyons/CorridorPaintings altına yazar.
public static class CorridorPaintingSplit
{
    const string Folder = "Assets/Models/Revisyons/CorridorPaintings";

    [MenuItem("Senzora/Oyun Testi/Koridor Tablolarını Ayır")]
    public static void Split()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var source = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .FirstOrDefault(f => f.name == "SM_PictureFrame3" && f.gameObject.activeInHierarchy && f.sharedMesh != null);
        if (source == null) { Debug.LogError("Etkin SM_PictureFrame3 bulunamadı (zaten ayrılmış olabilir)."); return; }
        Mesh mesh = source.sharedMesh;
        List<List<int>> paintings = Group(mesh);
        if (paintings.Count != 5) { Debug.LogError($"Beklenen 5 tablo yerine {paintings.Count} parça çıktı; sahneye dokunulmadı."); return; }

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Models/Revisyons", "CorridorPaintings");
        var renderer = source.GetComponent<MeshRenderer>();
        Transform frame = source.transform;
        // Order along the corridor so the names follow the wall.
        Vector3 axis = (mesh.bounds.size.x >= mesh.bounds.size.z) ? Vector3.right : Vector3.forward;
        var parts = paintings.Select(tris => Extract(mesh, tris)).OrderBy(p => Vector3.Dot(p.center, axis)).ToList();

        Undo.SetCurrentGroupName("Koridor Tablolarını Ayır");
        for (int i = 0; i < parts.Count; i++)
        {
            string name = $"Tablo_{i + 1:00}";
            parts[i].mesh.name = name;
            AssetDatabase.CreateAsset(parts[i].mesh, $"{Folder}/{name}.asset");
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Tablo");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(frame, false);
            go.transform.localPosition = parts[i].center;
            go.GetComponent<MeshFilter>().sharedMesh = parts[i].mesh;
            go.AddComponent<BoxCollider>(); // added after the mesh so it fits the painting (for later interaction)
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterials = renderer.sharedMaterials;
            r.shadowCastingMode = renderer.shadowCastingMode;
            // The scene's lightmaps are baked without these parts (and no re-bake): light them from probes.
            r.receiveGI = ReceiveGI.LightProbes;
            var flags = GameObjectUtility.GetStaticEditorFlags(source.gameObject) & ~StaticEditorFlags.ContributeGI;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }
        Undo.DestroyObjectImmediate(renderer);
        Undo.DestroyObjectImmediate(source);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(frame.gameObject.scene);
        Debug.Log($"CORRIDOR PAINTINGS SPLIT: {parts.Count} paintings under {frame.name}; vertices {string.Join("/", parts.Select(p => p.mesh.vertexCount))}.");
    }

    // Triangles connected through shared (welded) positions, then pieces whose bounds touch merged into one painting.
    static List<List<int>> Group(Mesh mesh)
    {
        Vector3[] v = mesh.vertices; int[] t = mesh.triangles;
        var parent = Enumerable.Range(0, v.Length).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
        var weld = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var key = Vector3Int.RoundToInt(v[i] * 10000f);
            if (weld.TryGetValue(key, out int other)) Union(i, other); else weld[key] = i;
        }
        for (int i = 0; i < t.Length; i += 3) { Union(t[i], t[i + 1]); Union(t[i], t[i + 2]); }
        var pieces = new Dictionary<int, List<int>>();
        for (int i = 0; i < t.Length; i += 3)
        {
            int root = Find(t[i]);
            if (!pieces.TryGetValue(root, out var list)) pieces[root] = list = new List<int>();
            list.Add(i);
        }
        var groups = pieces.Values.Select(tris => (tris, bounds: Bounds(v, t, tris))).ToList();
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int a = 0; a < groups.Count && !merged; a++)
                for (int b = a + 1; b < groups.Count && !merged; b++)
                {
                    var grown = groups[a].bounds; grown.Expand(.02f * grown.size.magnitude + 1e-4f);
                    if (!grown.Intersects(groups[b].bounds)) continue;
                    var tris = groups[a].tris.Concat(groups[b].tris).ToList();
                    var box = groups[a].bounds; box.Encapsulate(groups[b].bounds);
                    groups[a] = (tris, box); groups.RemoveAt(b); merged = true;
                }
        }
        return groups.Select(g => g.tris).ToList();
    }

    static Bounds Bounds(Vector3[] v, int[] t, List<int> tris)
    {
        var b = new Bounds(v[t[tris[0]]], Vector3.zero);
        foreach (int i in tris) { b.Encapsulate(v[t[i]]); b.Encapsulate(v[t[i + 1]]); b.Encapsulate(v[t[i + 2]]); }
        return b;
    }

    // A self-contained mesh for one painting, its vertices re-centred on the painting.
    static (Mesh mesh, Vector3 center) Extract(Mesh mesh, List<int> tris)
    {
        int[] t = mesh.triangles;
        var map = new Dictionary<int, int>(); var order = new List<int>();
        foreach (int i in tris) for (int k = 0; k < 3; k++) if (!map.ContainsKey(t[i + k])) { map[t[i + k]] = order.Count; order.Add(t[i + k]); }
        Vector3[] v = mesh.vertices;
        Vector3 center = Bounds(v, t, tris).center;
        var part = new Mesh { indexFormat = order.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        part.vertices = order.Select(i => v[i] - center).ToArray();
        Vector3[] normals = mesh.normals; Vector4[] tangents = mesh.tangents; Color[] colors = mesh.colors;
        if (normals.Length == v.Length) part.normals = order.Select(i => normals[i]).ToArray();
        if (tangents.Length == v.Length) part.tangents = order.Select(i => tangents[i]).ToArray();
        if (colors.Length == v.Length) part.colors = order.Select(i => colors[i]).ToArray();
        for (int channel = 0; channel < 4; channel++)
        {
            var uv = new List<Vector2>(); mesh.GetUVs(channel, uv);
            if (uv.Count == v.Length) part.SetUVs(channel, order.Select(i => uv[i]).ToList());
        }
        part.triangles = tris.SelectMany(i => new[] { map[t[i]], map[t[i + 1]], map[t[i + 2]] }).ToArray();
        part.RecalculateBounds();
        return (part, center);
    }
}
