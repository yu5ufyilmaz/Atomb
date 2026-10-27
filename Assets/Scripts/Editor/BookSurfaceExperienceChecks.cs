#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Temporary objects only. These checks never save or alter the open scene.
public static class BookSurfaceExperienceChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Book Surface/Projection Regression")]
    public static void ProjectionRegression()
    {
        var root = new GameObject("Book surface regression");
        var mesh = new Mesh { name = "Overlapping page UV regression" };
        try
        {
            var book = root.AddComponent<InteractableBook>();
            var paper = root.AddComponent<BookPageBlockSurface>();
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            book.bookSkinnedMeshRenderer = renderer;
            var cameraObject = new GameObject("Regression camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -3);
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.pixelRect = new Rect(0, 0, 1000, 1000);
            var canvasObject = new GameObject("Surface", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            // The hidden underside deliberately comes first and mirrors the same UV island.
            AddQuad(vertices, uvs, indices, .2f, true);
            AddQuad(vertices, uvs, indices, 0, false);
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0);
            Field(paper, "book", book); Field(paper, "surface", canvas); Field(paper, "bakedPages", mesh);
            Call(paper, "BuildPageMaps", renderer, (RectTransform)canvas.transform);
            var failures = new List<string>();
            CheckCase(failures, "UV mapping chooses exposed paper", () =>
            {
                Vector3 point = (Vector3)Call(paper, "MapPageWorld", new Vector2(.2f, .3f), false);
                Require(Vector3.Distance(point, new Vector3(-.6f, -.4f, 0)) < .0001f,
                    "Expected (-.6, -.4, 0); got " + point.ToString("F4"));
            });
            CheckCase(failures, "Region has only one visible layer", () =>
            {
                using (var vh = new VertexHelper())
                {
                    Call(paper, "PopulateRegion", vh, new Rect(0, 0, 1, 1), false, canvas.transform, Color.white, -1);
                    Require(vh.currentVertCount == 6, "Expected 6 visible vertices; got " + vh.currentVertCount);
                }
            });
            CheckCase(failures, "Pointer finds nearest exposed UV", () =>
            {
                Vector2 screen = camera.WorldToScreenPoint(new Vector3(-.6f, -.4f, 0));
                object[] args = { screen, camera, false, Vector2.zero };
                bool hit = (bool)Call(paper, "TryGetPageUV", args);
                Require(hit && Vector2.Distance((Vector2)args[3], new Vector2(.2f, .3f)) < .0001f,
                    "Expected exposed UV (.2,.3); got " + args[3]);
            });
            CheckCase(failures, "Authored ink side survives a camera move", () =>
            {
                try
                {
                    camera.transform.position = new Vector3(0, 0, 3);
                    camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                    Call(paper, "BuildPageMaps", renderer, (RectTransform)canvas.transform);
                    Vector3 point = (Vector3)Call(paper, "MapPageWorld", new Vector2(.2f, .3f), false);
                    Require(Vector3.Distance(point, new Vector3(-.6f, -.4f, 0)) < .0001f,
                        "Camera position changed which physical sheet carries ink: " + point);
                }
                finally
                {
                    camera.transform.position = new Vector3(0, 0, -3);
                    camera.transform.rotation = Quaternion.identity;
                    Call(paper, "BuildPageMaps", renderer, (RectTransform)canvas.transform);
                }
            });
            CheckCase(failures, "Wide graphics follow the curved paper between their corners", () =>
            {
                mesh.Clear();
                mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0), new Vector3(-1, 1, 0), new Vector3(0, 0, -.3f) };
                mesh.uv = new[] { Vector2.zero, new Vector2(.5f, 0), new Vector2(.5f, 1), Vector2.up, new Vector2(.25f, .5f) };
                mesh.triangles = new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0 };
                Call(paper, "BuildPageMaps", renderer, (RectTransform)canvas.transform);
                var graphicObject = new GameObject("Wide graphic", typeof(RectTransform), typeof(Image), typeof(BookPagePrintedGraphic));
                graphicObject.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)graphicObject.transform;
                rect.sizeDelta = new Vector2(2, 2);
                var printed = graphicObject.GetComponent<BookPagePrintedGraphic>();
                Field(printed, "Surface", paper); Field(printed, "RegionRect", rect);
                Field(printed, "RegionUV", new Rect(0, 0, 1, 1));
                using (var vh = new VertexHelper())
                {
                    vh.AddVert(new Vector3(-1, -1, 0), Color.white, Vector2.zero);
                    vh.AddVert(new Vector3(-1, 1, 0), Color.white, Vector2.up);
                    vh.AddVert(new Vector3(1, 1, 0), Color.white, Vector2.one);
                    vh.AddVert(new Vector3(1, -1, 0), Color.white, Vector2.right);
                    vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
                    printed.ModifyMesh(vh);
                    float minimumZ = 0;
                    var vertex = new UIVertex();
                    for (int i = 0; i < vh.currentVertCount; i++)
                    { vh.PopulateUIVertex(ref vertex, i); minimumZ = Mathf.Min(minimumZ, vertex.position.z); }
                    Require(minimumZ < -.29f, "Card geometry bridges over the curved center instead of following it: z=" + minimumZ);
                }
            });
            Require(failures.Count == 0, string.Join("\n", failures));
            Debug.Log("BOOK SURFACE PROJECTION PASS: exposed UV layer, single region layer and nearest pointer hit.");
        }
        finally
        {
            // The component owns its baked mesh.
            UnityEngine.Object.DestroyImmediate(root);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    [MenuItem("Senzora/Book Surface/Layout Regression")]
    public static void LayoutRegression()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/P_Book.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var book = instance.GetComponent<InteractableBook>();
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim").SampleAnimation(instance, 0);
            book.printNotesOnPages = book.usePhysicalPageBlocks = true;
            var upper = new NotebookBlockData { title = "Üst bilgi", description = "Birinci kayıt.", blockType = NotebookBlockType.RuleClue };
            var lower = new NotebookBlockData { title = "Alt bilgi", description = "İkinci kayıt.", blockType = NotebookBlockType.RuleClue };
            Rect topSlot = new Rect(.09f, .53f, .82f, .32f), bottomSlot = new Rect(.09f, .11f, .82f, .32f);
            typeof(NotebookBlockData).GetField("bookRegion").SetValue(upper, topSlot);
            typeof(NotebookBlockData).GetField("bookRegion").SetValue(lower, bottomSlot);
            book.synthesisPages = new List<BookWorkspacePage>
            {
                new BookWorkspacePage { pageIndex = 0, title = "DİZİN", explanation = "Bu kitapta deney kayıtları bulunur." },
                new BookWorkspacePage { pageIndex = 1, title = "KAYITLAR", blocks = new List<NotebookBlockData> { upper, lower } },
            };
            Field(book, "pageIndexL", 0); Field(book, "pageIndexR", 1);
            var paper = instance.GetComponent<BookPageBlockSurface>() ?? instance.AddComponent<BookPageBlockSurface>();
            Field(paper, "book", book);
            paper.PreparePresentation();
            var sources = paper.GetComponentsInChildren<NotebookDraggableBlock>();
            Require(sources.Length == 2, "Frontmatter must not add a collectible block.");
            foreach (var source in sources)
            {
                var projected = source.GetComponent<BookPagePrintedGraphic>();
                Rect actual = (Rect)typeof(BookPagePrintedGraphic).GetField("RegionUV", Private).GetValue(projected);
                Rect expected = source.Data.title == upper.title ? topSlot : bottomSlot;
                Require(Vector2.Distance(actual.position, expected.position) < .00001f && Vector2.Distance(actual.size, expected.size) < .00001f,
                    $"Note ignored its assigned book slot. title='{source.Data.title}', actual={actual.ToString("F6")}, expected={expected.ToString("F6")}, cloned slot={source.Data.bookRegion.ToString("F6")}");
                foreach (var text in source.GetComponentsInChildren<TMP_Text>())
                { text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Text exceeds its assigned book slot: " + text.text); }
            }
            bool hasTitle = false, hasExplanation = false, hasPageNumber = false;
            foreach (var text in paper.GetComponentsInChildren<TMP_Text>())
            {
                if (text.text == "DİZİN") hasTitle = text.GetComponentInParent<NotebookDraggableBlock>() == null;
                if (text.text == "Bu kitapta deney kayıtları bulunur.") hasExplanation = text.GetComponentInParent<NotebookDraggableBlock>() == null;
                if (text.text == "s. 2") hasPageNumber = text.GetComponentInParent<NotebookDraggableBlock>() == null;
            }
            Require(hasTitle && hasExplanation, "An empty collectible page must still print its title and explanation.");
            Require(hasPageNumber, "Printed references need a visible non-collectible page number.");
            book.synthesisPages[1].blocks = new List<NotebookBlockData>
            {
                new NotebookBlockData { title = "Eski kayıt 1", blockType = NotebookBlockType.RuleClue },
                new NotebookBlockData { title = "Eski kayıt 2", blockType = NotebookBlockType.RuleClue },
                new NotebookBlockData { title = "Eski kayıt 3", blockType = NotebookBlockType.RuleClue },
            };
            paper.PreparePresentation();
            var legacyRegions = new List<Rect>();
            foreach (var source in paper.GetComponentsInChildren<NotebookDraggableBlock>())
            {
                Rect actual = (Rect)typeof(BookPagePrintedGraphic).GetField("RegionUV", Private).GetValue(source.GetComponent<BookPagePrintedGraphic>());
                Require(actual.xMin >= .08999f && actual.xMax <= .91001f && actual.yMin >= .10999f && actual.yMax <= .85001f,
                    "Legacy automatic layout escapes the physical page: " + actual);
                foreach (Rect previous in legacyRegions) Require(!actual.Overlaps(previous), "Legacy automatic notes overlap.");
                legacyRegions.Add(actual);
            }
            Require(legacyRegions.Count == 3, "Legacy layout lost a collectible note.");
            Debug.Log("BOOK SURFACE LAYOUT PASS: assigned slots, safe legacy layout, text fit and non-collectible frontmatter.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> indices, float z, bool underside)
    {
        int start = vertices.Count;
        vertices.AddRange(new[] { new Vector3(-1, -1, z), new Vector3(1, -1, z), new Vector3(1, 1, z), new Vector3(-1, 1, z) });
        uvs.AddRange(underside
            ? new[] { new Vector2(.5f, 0), Vector2.zero, Vector2.up, new Vector2(.5f, 1) }
            : new[] { Vector2.zero, new Vector2(.5f, 0), new Vector2(.5f, 1), Vector2.up });
        foreach (int index in underside ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 }) indices.Add(start + index);
    }

    [MenuItem("Senzora/Book Surface/Audit Open Prefab")]
    public static void AuditOpenPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/P_Book.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var book = instance.GetComponent<InteractableBook>();
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim").SampleAnimation(instance, 0);
            var paper = instance.GetComponent<BookPageBlockSurface>() ?? instance.AddComponent<BookPageBlockSurface>();
            Field(paper, "book", book);
            Call(paper, "BuildSurface");
            var canvas = (Canvas)typeof(BookPageBlockSurface).GetField("surface", Private).GetValue(paper);
            var baked = (Mesh)typeof(BookPageBlockSurface).GetField("bakedPages", Private).GetValue(paper);
            var probe = new GameObject("Prefab audit camera", typeof(Camera));
            probe.transform.SetParent(instance.transform, false);
            Vector3 center = book.bookSkinnedMeshRenderer.transform.TransformPoint(baked.bounds.center);
            probe.transform.position = center + book.bookSkinnedMeshRenderer.transform.up * 2;
            probe.transform.LookAt(center, book.bookSkinnedMeshRenderer.transform.forward);
            canvas.worldCamera = probe.GetComponent<Camera>();
            Call(paper, "BuildPageMaps", book.bookSkinnedMeshRenderer, (RectTransform)canvas.transform);
            Vector3[] vertices = baked.vertices;
            int[] triangles = baked.GetTriangles(book.bookMaterialIndex);
            Vector2[] uvs = baked.uv;
            int buried = 0, samples = 0, overlaps = 0;
            foreach (bool right in new[] { false, true })
                foreach (float x in new[] { .15f, .5f, .85f })
                    foreach (float y in new[] { .15f, .5f, .85f })
                    {
                        Vector2 uv = new Vector2((x + (right ? 1 : 0)) * .5f, y);
                        Vector3 mapped = (Vector3)Call(paper, "MapPageWorld", new Vector2(x, y), right);
                        Vector3 normal = book.bookSkinnedMeshRenderer.transform.up;
                        float bestHeight = float.NegativeInfinity;
                        int matches = 0;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                            Vector2 ab = uvs[b] - uvs[a], ac = uvs[c] - uvs[a], ap = uv - uvs[a];
                            float determinant = ab.x * ac.y - ab.y * ac.x;
                            if (Mathf.Abs(determinant) < .00000001f) continue;
                            float v = (ap.x * ac.y - ap.y * ac.x) / determinant;
                            float w = (ab.x * ap.y - ab.y * ap.x) / determinant;
                            if (v < -.00001f || w < -.00001f || v + w > 1.00001f) continue;
                            Vector3 world = book.bookSkinnedMeshRenderer.transform.TransformPoint(vertices[a] + (vertices[b] - vertices[a]) * v + (vertices[c] - vertices[a]) * w);
                            bestHeight = Mathf.Max(bestHeight, Vector3.Dot(world, normal));
                            matches++;
                        }
                        samples++;
                        if (matches > 1) overlaps++;
                        if (bestHeight - Vector3.Dot(mapped, normal) > .0001f) buried++;
                    }
            Debug.Log($"BOOK SURFACE PREFAB AUDIT: {samples} samples, {overlaps} overlapping UV locations, {buried} mappings below the exposed paper; raw triangles={triangles.Length / 3}, mapped triangles={((System.Collections.ICollection)typeof(BookPageBlockSurface).GetField("pageTriangles", Private).GetValue(paper)).Count}.");
            Require(buried == 0, "Printed page mapping selects hidden paper at " + buried + " sample positions.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void Field(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(target, args);
    private static void CheckCase(List<string> failures, string label, Action action)
    {
        try { action(); } catch (Exception exception) { failures.Add(label + ": " + (exception.InnerException ?? exception).Message); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
