#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run with -batchmode -executeMethod BookPageBlockSurfaceChecks.Run, or from Tools.
public static class BookPageBlockSurfaceChecks
{
    [MenuItem("Senzora/Doğrulama/Kitap UV Çerçeveleri")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scn_SynthesisBlockout.unity");
        var baked = new Mesh();
        try
        {
            InteractableBook book = null;
            foreach (var root in scene.GetRootGameObjects())
                if (book == null) book = root.GetComponentInChildren<InteractableBook>(true);
            if (book == null) throw new Exception("Test book missing.");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim");
            clip.SampleAnimation(book.gameObject, clip.length);
            var renderer = book.bookSkinnedMeshRenderer;
            var surface = book.GetComponent<BookPageBlockSurface>();
            typeof(BookPageBlockSurface).GetField("book", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(surface, book);
            var probe = new GameObject("UV alignment probe", typeof(RectTransform)).GetComponent<RectTransform>();
            probe.SetParent(book.transform, false);
            CheckMesh(book, baked, surface, probe);
            book.transform.parent.localScale = new Vector3(1.7f, 0.6f, 2.1f);
            book.transform.parent.rotation = Quaternion.Euler(23, 41, 17);
            renderer.transform.localScale = new Vector3(0.6f, 1.4f, 0.8f);
            CheckMesh(book, baked, surface, probe);
            Debug.Log("PASS: Book UV centres, curved frames, spine seam, small regions and nonuniform parent/renderer scales.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(baked);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckMesh(InteractableBook book, Mesh baked, BookPageBlockSurface surface, RectTransform probe)
    {
        var renderer = book.bookSkinnedMeshRenderer;
        probe.SetParent(book.transform, false);
        var previousCanvas = (Canvas)typeof(BookPageBlockSurface).GetField("surface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);
        var previousMesh = (Mesh)typeof(BookPageBlockSurface).GetField("bakedPages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);
        if (previousCanvas != null) UnityEngine.Object.DestroyImmediate(previousCanvas.gameObject);
        if (previousMesh != null) UnityEngine.Object.DestroyImmediate(previousMesh);
        Call(surface, "BuildSurface");
        var canvas = (Canvas)typeof(BookPageBlockSurface).GetField("surface", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);
        probe.SetParent(canvas.transform, false);
        renderer.BakeMesh(baked, true);
        // Oracle is the renderer's bone matrices, independent of BakeMesh's scale option.
        var vertices = RenderedVertices(renderer);
        baked.vertices = vertices;
        var uvs = baked.uv;
        var triangles = baked.GetTriangles(book.bookMaterialIndex);
        float maxError = 0;
        int checkedPoints = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector2 uv = (uvs[a] + uvs[b] + uvs[c]) / 3f;
            if (Mathf.Abs(Vector3.Cross(uvs[b] - uvs[a], uvs[c] - uvs[a]).z) < 1e-8f) continue;
            bool right = uv.x >= 0.5f;
            var region = new BookClueRegion { regionUV = new Rect((uv.x - (right ? 0.5f : 0)) * 2f, uv.y, 0, 0) };
            Call(surface, "PlaceClueRegion", probe, region, right);
            Vector3 expected = renderer.transform.TransformPoint((vertices[a] + vertices[b] + vertices[c]) / 3f);
            maxError = Mathf.Max(maxError, Vector3.Distance(probe.position, expected));
            checkedPoints++;
        }
        Debug.Log($"Book UV check: {checkedPoints} triangle centres, maximum world error={maxError:F6}");
        if (checkedPoints == 0 || maxError > 0.00001f)
            throw new Exception($"UV frame alignment failed: {maxError:F6} world units (limit 0.00001).");
        foreach (bool right in new[] { false, true })
            foreach (Rect region in new[] { new Rect(0.2f, 0.1f, 0.6f, 0.7f), new Rect(right ? 0 : 0.9f, 0.3f, 0.1f, 0.4f), new Rect(0.4f, 0.6f, 0.002f, 0.003f) })
                CheckProjectedRegion(book, surface, probe, baked, region, right);
    }

    private static void CheckProjectedRegion(InteractableBook book, BookPageBlockSurface surface, RectTransform probe, Mesh baked, Rect region, bool right)
    {
        if (probe.GetComponent<Image>() == null) probe.gameObject.AddComponent<Image>();
        Call(surface, "PlaceClueRegion", probe, new BookClueRegion { regionUV = region }, right);
        var effect = probe.GetComponent<BaseMeshEffect>();
        using (var vh = new VertexHelper())
        {
            effect.ModifyMesh(vh);
            if (vh.currentVertCount == 0) throw new Exception("Projected clue region is empty.");
            var mapped = new Vector2[vh.currentVertCount];
            var corners = new[] { region.min, new Vector2(region.xMax, region.yMin), region.max, new Vector2(region.xMin, region.yMax) };
            foreach (Vector2 corner in corners)
            {
                float closest = float.MaxValue;
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    var vertex = new UIVertex(); vh.PopulateUIVertex(ref vertex, i);
                    Vector3 position = book.bookSkinnedMeshRenderer.transform.InverseTransformPoint(probe.TransformPoint(vertex.position));
                    mapped[i] = FindUV(position, baked, book.bookMaterialIndex, right);
                    closest = Mathf.Min(closest, Vector2.Distance(mapped[i], corner));
                    if (mapped[i].x < region.xMin - 0.00001f || mapped[i].x > region.xMax + 0.00001f
                        || mapped[i].y < region.yMin - 0.00001f || mapped[i].y > region.yMax + 0.00001f)
                        throw new Exception("Frame geometry escaped the selected UV region.");
                }
                if (closest > 0.00001f) throw new Exception("A selected UV corner is missing from the frame.");
            }
        }
        var camera = new GameObject("UV raycast check", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(probe.parent, false);
        try
        {
            camera.pixelRect = new Rect(0, 0, 1600, 900);
            Vector3 centre = probe.parent.TransformPoint((Vector3)Call(surface, "MapPagePoint", region.center, right));
            Vector3 normal = book.bookSkinnedMeshRenderer.transform.up;
            camera.transform.position = centre + normal * 2;
            camera.transform.rotation = Quaternion.LookRotation(-normal, book.bookSkinnedMeshRenderer.transform.forward);
            var filter = (ICanvasRaycastFilter)effect;
            Vector2 screenPoint = camera.WorldToScreenPoint(centre);
            if (!filter.IsRaycastLocationValid(screenPoint, camera) || !RectTransformUtility.RectangleContainsScreenPoint(probe, screenPoint, camera))
                throw new Exception("Visible clue centre cannot be clicked.");
            Vector2 outsideUV = new Vector2(region.xMin > 0.05f ? region.xMin - 0.02f : region.xMax + 0.02f, region.center.y);
            Vector3 outside = probe.parent.TransformPoint((Vector3)Call(surface, "MapPagePoint", outsideUV, right));
            if (filter.IsRaycastLocationValid(camera.WorldToScreenPoint(outside), camera))
                throw new Exception("Clue raycast accepts a point outside the selected UV region.");
        }
        finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
    }

    // Independent inverse check: each rendered vertex must lie on an original mesh triangle.
    private static Vector2 FindUV(Vector3 point, Mesh mesh, int submesh, bool right)
    {
        var vertices = mesh.vertices; var uv = mesh.uv; var indices = mesh.GetTriangles(submesh);
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (((uv[a].x + uv[b].x + uv[c].x) / 3f >= 0.5f) != right) continue;
            Vector3 ab = vertices[b] - vertices[a], ac = vertices[c] - vertices[a], ap = point - vertices[a];
            float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac);
            float det = aa * bb - cross * cross;
            if (det < 1e-12f) continue;
            float v = (bb * Vector3.Dot(ap, ab) - cross * Vector3.Dot(ap, ac)) / det;
            float w = (aa * Vector3.Dot(ap, ac) - cross * Vector3.Dot(ap, ab)) / det;
            if (v < -0.0001f || w < -0.0001f || v + w > 1.0001f) continue;
            if (Vector3.Distance(vertices[a] + ab * v + ac * w, point) > 0.00001f) continue;
            Vector2 hit = uv[a] + (uv[b] - uv[a]) * v + (uv[c] - uv[a]) * w;
            return new Vector2(hit.x * 2 - (right ? 1 : 0), hit.y);
        }
        throw new Exception("Projected frame vertex is off the actual page mesh.");
    }

    private static Vector3[] RenderedVertices(SkinnedMeshRenderer renderer)
    {
        Mesh mesh = renderer.sharedMesh;
        var source = mesh.vertices; var result = new Vector3[source.Length];
        var weights = mesh.boneWeights; var binds = mesh.bindposes; var bones = renderer.bones;
        for (int i = 0; i < source.Length; i++)
        {
            var w = weights[i];
            Vector3 world = Skin(w.boneIndex0, w.weight0) + Skin(w.boneIndex1, w.weight1)
                + Skin(w.boneIndex2, w.weight2) + Skin(w.boneIndex3, w.weight3);
            result[i] = renderer.transform.InverseTransformPoint(world);
            Vector3 Skin(int index, float weight) => weight == 0 ? Vector3.zero
                : (bones[index].localToWorldMatrix * binds[index]).MultiplyPoint3x4(source[i]) * weight;
        }
        return result;
    }

    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
#endif
