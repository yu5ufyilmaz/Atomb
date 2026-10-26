using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Read-only diagnostic. Vertices/UVs are mesh-local; row-major matrices locate each mesh in the scene.
public static class TuringSurfaceAudit
{
    private const string SceneName = "scn_SynthesisBlockout";

    [Serializable] private sealed class Audit
    {
        public string scene, machinePath, coordinateConvention;
        public Vector3 machinePosition, machineScale;
        public Quaternion machineRotation;
        public List<Surface> surfaces = new List<Surface>();
        public List<Geometry> meshes = new List<Geometry>();
    }

    [Serializable] private sealed class Surface
    {
        public string path, meshRendererName, meshFilterName, meshId;
        public bool active, rendererEnabled;
        public Vector3 localPosition, localScale, worldPosition, worldScale;
        public Quaternion localRotation, worldRotation;
        public float[] localToWorld, localToMachine;
        public Bounds worldBounds, rendererLocalBounds, meshLocalBounds;
        public List<MaterialInfo> materials = new List<MaterialInfo>();
    }

    [Serializable] private sealed class MaterialInfo
    {
        public string name, path, shader;
        public Color baseColor, emissionColor;
        public List<TextureInfo> textures = new List<TextureInfo>();
    }

    [Serializable] private sealed class TextureInfo
    {
        public string property, name, path;
        public int width, height;
        public Vector2 scale, offset;
    }

    [Serializable] private sealed class Geometry
    {
        public string id, name, path, error;
        public bool isReadable;
        public Bounds bounds;
        public Vector3[] positions, normals;
        public Vector2[] uv0;
        public List<Submesh> submeshes = new List<Submesh>();
    }

    [Serializable] private sealed class Submesh
    {
        public int materialIndex;
        public string topology;
        // GetIndices applies baseVertex, so indices directly address positions/normals/uv0.
        public int[] indices;
    }

    [MenuItem("Senzora/Çift Kayıt/Geliştirici/Turing Yüzeylerini İncele")]
    public static void Export()
    {
        var machine = FindTerminal();
        var root = machine.transform;
        var audit = new Audit {
            scene = machine.gameObject.scene.path, machinePath = TransformPath(root),
            machinePosition = root.position, machineRotation = root.rotation, machineScale = root.lossyScale,
            coordinateConvention = "Unity axes; mesh-local positions/normals and UV channel 0. Matrices are row-major, multiplying column vectors. Submesh materialIndex maps to surfaces.materials. Indices include baseVertex."
        };
        var exported = new HashSet<int>();
        var filters = new HashSet<MeshFilter>();
        foreach (var renderer in machine.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null) filters.Add(filter);
            AddSurface(audit, root, renderer.transform, renderer, filter, exported);
        }
        foreach (var filter in machine.GetComponentsInChildren<MeshFilter>(true))
            if (!filters.Contains(filter)) AddSurface(audit, root, filter.transform, null, filter, exported);

        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Temp", "ResearchTrialChecks", "TuringGeometry.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(audit, true), new UTF8Encoding(false));
        int failures = audit.meshes.FindAll(m => !string.IsNullOrEmpty(m.error)).Count;
        Debug.Log($"TURING SURFACE AUDIT: {audit.surfaces.Count} surfaces, {audit.meshes.Count} meshes, {failures} mesh read errors. {path}");
    }

    private static InteractableTuringMachine FindTerminal()
    {
        var active = SecondTraceExperiment.Active;
        if (active != null && active.terminal != null && active.terminal.gameObject.scene.name == SceneName)
            return active.terminal;
        foreach (var experiment in Object.FindObjectsByType<SecondTraceExperiment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (experiment.gameObject.scene.name == SceneName && experiment.terminal != null
                && experiment.terminal.gameObject.scene.name == SceneName) return experiment.terminal;
        InteractableTuringMachine found = null;
        foreach (var terminal in Object.FindObjectsByType<InteractableTuringMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (terminal.gameObject.scene.name != SceneName) continue;
            if (found != null) throw new InvalidOperationException("Test sahnesinde birden fazla bağımsız Turing var; SecondTraceExperiment.terminal referansını seçin.");
            found = terminal;
        }
        if (found == null) throw new InvalidOperationException("scn_SynthesisBlockout açık olmalı ve Turing test kopyasını içermeli.");
        return found;
    }

    private static void AddSurface(Audit audit, Transform root, Transform t, MeshRenderer renderer,
        MeshFilter filter, HashSet<int> exported)
    {
        var mesh = filter != null ? filter.sharedMesh : null;
        var surface = new Surface {
            path = TransformPath(t), meshRendererName = renderer != null ? renderer.name : null,
            meshFilterName = filter != null ? filter.name : null, meshId = mesh != null ? mesh.GetInstanceID().ToString() : null,
            active = t.gameObject.activeInHierarchy, rendererEnabled = renderer != null && renderer.enabled,
            localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale,
            worldPosition = t.position, worldRotation = t.rotation, worldScale = t.lossyScale,
            localToWorld = Matrix(t.localToWorldMatrix), localToMachine = Matrix(root.worldToLocalMatrix * t.localToWorldMatrix),
            worldBounds = renderer != null ? renderer.bounds : default,
            rendererLocalBounds = renderer != null ? renderer.localBounds : default,
            meshLocalBounds = mesh != null ? mesh.bounds : default
        };
        if (renderer != null)
            foreach (var material in renderer.sharedMaterials) surface.materials.Add(ReadMaterial(material));
        audit.surfaces.Add(surface);
        if (mesh != null && exported.Add(mesh.GetInstanceID())) audit.meshes.Add(ReadGeometry(mesh));
    }

    private static Geometry ReadGeometry(Mesh mesh)
    {
        var geometry = new Geometry { id = mesh.GetInstanceID().ToString(), name = mesh.name,
            path = AssetDatabase.GetAssetPath(mesh), isReadable = mesh.isReadable, bounds = mesh.bounds };
        try
        {
            // The editor API explicitly bypasses isReadable without touching the model importer.
            using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var source = data[0];
                using (var positions = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
                {
                    source.GetVertices(positions);
                    geometry.positions = positions.ToArray();
                }
                if (source.HasVertexAttribute(VertexAttribute.Normal))
                    using (var normals = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
                    {
                        source.GetNormals(normals);
                        geometry.normals = normals.ToArray();
                    }
                if (source.HasVertexAttribute(VertexAttribute.TexCoord0))
                    using (var uv = new NativeArray<Vector2>(source.vertexCount, Allocator.Temp))
                    {
                        source.GetUVs(0, uv);
                        geometry.uv0 = uv.ToArray();
                    }
                for (int i = 0; i < source.subMeshCount; i++)
                {
                    var submesh = source.GetSubMesh(i);
                    using (var indices = new NativeArray<int>(submesh.indexCount, Allocator.Temp))
                    {
                        source.GetIndices(indices, i, true);
                        geometry.submeshes.Add(new Submesh {
                            materialIndex = i, topology = submesh.topology.ToString(), indices = indices.ToArray()
                        });
                    }
                }
            }
        }
        catch (Exception error) { geometry.error = error.ToString(); }
        return geometry;
    }

    private static MaterialInfo ReadMaterial(Material material)
    {
        if (material == null) return new MaterialInfo { name = "<null>" };
        var result = new MaterialInfo { name = material.name, path = AssetDatabase.GetAssetPath(material),
            shader = material.shader != null ? material.shader.name : null,
            baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white,
            emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black };
        foreach (string property in material.GetTexturePropertyNames())
        {
            var texture = material.GetTexture(property);
            result.textures.Add(new TextureInfo { property = property,
                name = texture != null ? texture.name : null, path = texture != null ? AssetDatabase.GetAssetPath(texture) : null,
                width = texture != null ? texture.width : 0, height = texture != null ? texture.height : 0,
                scale = material.GetTextureScale(property), offset = material.GetTextureOffset(property) });
        }
        return result;
    }

    private static float[] Matrix(Matrix4x4 matrix)
    {
        var result = new float[16];
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++) result[row * 4 + column] = matrix[row, column];
        return result;
    }

    private static string TransformPath(Transform transform) => transform.parent == null
        ? transform.name : TransformPath(transform.parent) + "/" + transform.name;
}
