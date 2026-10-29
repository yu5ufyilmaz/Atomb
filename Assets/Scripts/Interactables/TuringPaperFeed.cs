using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using TMPro;

// A world-space paper transport for the research terminal. Nothing is clipped or faded:
// the opaque housing hides the part of the card that has passed its mouth.
public sealed class TuringPaperFeed : IDisposable
{
    private const float Width = .21f, Length = .15f, Thickness = .0006f;
    private static readonly Color PaperColor = new Color(.88f, .84f, .72f, 1);
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private Material paperFront;
    private RenderTexture print;
    private GameObject captureRoot;
    private Camera captureCamera;
    private bool disposed;

    public Transform Root { get; private set; }
    public Transform Paper { get; private set; }

    public static TuringPaperFeed Create(Transform machine, Vector3 mouth, Vector3 right,
        Vector3 feed, Vector3 normal)
    {
        if (machine == null) throw new ArgumentNullException(nameof(machine));
        var result = new TuringPaperFeed();
        result.Root = new GameObject("Fiziksel Kart Okuyucu").transform;
        // Keep metre dimensions even when the FBX has a non-uniform parent scale.
        result.Root.SetPositionAndRotation(mouth, Quaternion.LookRotation(normal.normalized, feed.normalized));
        result.Root.localScale = Vector3.one;
        var basisRight = result.Root.right;
        if (Vector3.Dot(basisRight, right) < .9f)
            Debug.LogWarning("Kart okuyucu eksenleri sağ el koordinat sisteminde olmalı.", machine);

        Material source = null;
        float largest = -1;
        foreach (var renderer in machine.GetComponentsInChildren<Renderer>())
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || material.shader.name != "HDRP/Lit"
                    || material.GetFloat("_SurfaceType") != 0) continue;
                float size = renderer.bounds.size.sqrMagnitude;
                if (size <= largest) continue;
                source = material; largest = size;
            }
        }
        if (source == null)
        {
            result.Dispose();
            throw new InvalidOperationException("Kart okuyucu için opak HDRP/Lit makine materyali bulunamadı.");
        }

        var metal = result.Material(source, "Okuyucu / eskitilmiş çelik", new Color(.24f, .26f, .24f), .72f, .38f);
        var edge = result.Material(source, "Okuyucu / işlenmiş kenar", new Color(.38f, .40f, .36f), .8f, .46f);
        var dark = result.Material(source, "Okuyucu / iç hacim", new Color(.014f, .017f, .014f), .05f, .12f);
        var back = result.Material(source, "Ölçüm kağıdı / arka ve kesit", PaperColor, 0, .17f);
        result.paperFront = result.Material(source, "Ölçüm kağıdı / basılı yüz", Color.white, 0, .17f);
        result.paperFront.SetColor("_BaseColor", PaperColor);

        // The feed travels along +Y, between a real bed and a roof. The opening at
        // Y=0 remains empty; a thin line drawn over the card cannot hide it.
        result.Box("Kart yatağı", new Vector3(0, .083f, -.0032f), new Vector3(.232f, .18f, .005f), metal);
        result.Box("İç arka duvar", new Vector3(0, .171f, .002f), new Vector3(.232f, .009f, .014f), dark);
        result.Box("Sol kılavuz", new Vector3(-.1135f, .083f, .002f), new Vector3(.007f, .18f, .014f), metal);
        result.Box("Sağ kılavuz", new Vector3(.1135f, .083f, .002f), new Vector3(.007f, .18f, .014f), metal);
        result.Box("Okuyucu üst gövde", new Vector3(0, .091f, .0064f), new Vector3(.232f, .166f, .006f), metal);
        result.Box("Üst giriş dudağı", new Vector3(0, -.005f, .0041f), new Vector3(.232f, .028f, .004f), edge);
        result.Box("Alt giriş dudağı", new Vector3(0, -.003f, -.0026f), new Vector3(.232f, .012f, .004f), edge);
        result.Box("İç tavan gölgesi", new Vector3(0, .087f, .0028f), new Vector3(.218f, .162f, .001f), dark);
        var label = new GameObject("Ölçüm kartı giriş etiketi", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(result.Root, false);
        label.transform.localPosition = new Vector3(0, -.005f, .00625f);
        label.transform.localRotation = Quaternion.Euler(0, 180, 0);
        label.rectTransform.sizeDelta = new Vector2(.15f, .01f);
        label.fontSize = .08f;
        label.alignment = TextAlignmentOptions.Center; label.color = new Color(.035f, .04f, .035f);
        label.enableAutoSizing = true; label.fontSizeMin = .04f; label.fontSizeMax = .08f;
        var ink = label.fontMaterial;
        ink.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
        result.owned.Add(ink);
        GameLocalization.Set(label, "ÖLÇÜM KARTI");

        var card = new GameObject("Ölçüm Kağıdı", typeof(MeshFilter), typeof(MeshRenderer));
        result.Paper = card.transform;
        result.Paper.SetParent(result.Root, false);
        var mesh = result.PaperMesh();
        card.GetComponent<MeshFilter>().sharedMesh = mesh;
        var cardRenderer = card.GetComponent<MeshRenderer>();
        cardRenderer.sharedMaterials = new[] { result.paperFront, back };
        cardRenderer.shadowCastingMode = ShadowCastingMode.On;
        cardRenderer.receiveShadows = true;
        result.SetFeed(0);
        card.SetActive(false);
        return result;
    }

    public bool ContainsPointer(Vector2 screen, Camera camera)
    {
        if (disposed || Root == null || camera == null || !Root.gameObject.activeInHierarchy) return false;
        var plane = new Plane(Root.forward, Root.position);
        if (!plane.Raycast(camera.ScreenPointToRay(screen), out float distance)) return false;
        Vector3 point = Root.InverseTransformPoint(camera.ScreenPointToRay(screen).GetPoint(distance));
        // Include the exposed card and the mouth, so small notebook drag offsets are harmless.
        return Mathf.Abs(point.x) <= .145f && point.y >= -.175f && point.y <= .065f;
    }

    public void Begin(NotebookBlockData note)
    {
        if (disposed || note == null) return;
        ReleaseCapture();
        if (print != null) { print.Release(); UnityEngine.Object.Destroy(print); }
        print = new RenderTexture(672, 480, 24, RenderTextureFormat.ARGB32)
        { name = "Ölçüm Kağıdı Baskısı", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        print.Create();
        var previous = RenderTexture.active;
        RenderTexture.active = print; GL.Clear(true, true, PaperColor); RenderTexture.active = previous;
        paperFront.SetTexture("_BaseColorMap", print);
        paperFront.SetColor("_BaseColor", Color.white);
        HDMaterial.ValidateMaterial(paperFront);
        SetFeed(0);
        Paper.gameObject.SetActive(true);

        // Render the same immutable note drawing once. This camera is outside the
        // play space, and normal HDRP rendering handles it; Camera.Render is not used.
        captureRoot = new GameObject("Kart Baskı Atölyesi");
        captureRoot.transform.position = new Vector3(10000, 10000, 10000);
        var cameraObject = new GameObject("Kart Baskı Kamerası", typeof(Camera), typeof(HDAdditionalCameraData));
        cameraObject.transform.SetParent(captureRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, -2);
        captureCamera = cameraObject.GetComponent<Camera>();
        captureCamera.orthographic = true; captureCamera.orthographicSize = .1142857f;
        captureCamera.aspect = 1.4f; captureCamera.nearClipPlane = .01f; captureCamera.farClipPlane = 3;
        captureCamera.cullingMask = 1 << 31; captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = PaperColor; captureCamera.targetTexture = print;
        captureCamera.allowHDR = false; captureCamera.allowMSAA = false;
        var hd = cameraObject.GetComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = PaperColor; hd.volumeLayerMask = 0;
        hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;

        var canvasObject = new GameObject("Kağıt Baskı Yüzeyi", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(captureRoot.transform, false);
        var canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(320, 228.5714f); canvasRect.localScale = Vector3.one * .001f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = captureCamera;
        var drawingObject = new GameObject("Kaydedilen Ölçüm", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
        drawingObject.transform.SetParent(canvasObject.transform, false);
        var drawing = drawingObject.GetComponent<NotebookDraggableBlock>();
        drawing.Initialize(note.Clone(), false); drawing.enabled = false;
        var drawingRect = (RectTransform)drawingObject.transform;
        drawingRect.sizeDelta = canvasRect.sizeDelta; drawingRect.localRotation = Quaternion.identity;
        drawingObject.GetComponent<Image>().color = PaperColor;
        var group = drawingObject.GetComponent<CanvasGroup>();
        group.alpha = 1; group.interactable = group.blocksRaycasts = false;
        foreach (var child in captureRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases();
        captureRoot.AddComponent<TuringPaperCapture>().Capture(captureCamera, () =>
        {
            captureCamera = null;
            var completed = captureRoot; captureRoot = null;
            if (completed != null) UnityEngine.Object.Destroy(completed);
        });
    }

    public void SetFeed(float progress)
    {
        if (disposed || Paper == null) return;
        // At one, even the trailing edge has cleared the mouth. Keep the card alive
        // inside its opaque housing instead of disappearing it with a visual trick.
        Paper.localPosition = new Vector3(0, Mathf.Lerp(-Length * .5f - .012f, Length * .5f + .012f,
            Mathf.Clamp01(progress)), 0);
    }

    private Material Material(Material source, string name, Color color, float metallic, float smoothness)
    {
        var material = new Material(source) { name = name };
        owned.Add(material);
        foreach (string property in material.GetTexturePropertyNames()) material.SetTexture(property, null);
        material.SetColor("_BaseColor", color);
        material.SetColor("_EmissiveColor", Color.black);
        material.SetColor("_EmissiveColorLDR", Color.black);
        material.SetFloat("_UseEmissiveIntensity", 0);
        material.SetFloat("_EmissiveIntensity", 0);
        material.SetFloat("_MaterialID", 1); material.SetFloat("_DisplacementMode", 0); material.SetFloat("_UVBase", 0);
        material.SetFloat("_SurfaceType", 0); material.SetFloat("_AlphaCutoffEnable", 0);
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_DoubleSidedEnable", 0); material.SetFloat("_ZWrite", 1);
        material.SetFloat("_CullMode", 2); material.SetFloat("_CullModeForward", 2);
        material.SetTextureScale("_BaseColorMap", Vector2.one);
        material.SetTextureOffset("_BaseColorMap", Vector2.zero);
        HDMaterial.ValidateMaterial(material);
        return material;
    }

    private void Box(string name, Vector3 position, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.transform.SetParent(Root, false);
        box.transform.localPosition = position; box.transform.localScale = size;
        UnityEngine.Object.Destroy(box.GetComponent<Collider>());
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private Mesh PaperMesh()
    {
        var vertices = new List<Vector3>(24); var uv = new List<Vector2>(24);
        var front = new List<int>(6); var sides = new List<int>(30);
        float x = Width * .5f, y = Length * .5f, z = Thickness * .5f;
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> triangles)
        {
            int start = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        Face(new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z), front);
        // The printed face is viewed from +normal, whose screen-right is the local -X direction.
        uv[0] = Vector2.right; uv[1] = Vector2.zero; uv[2] = Vector2.up; uv[3] = Vector2.one;
        Face(new Vector3(x, -y, -z), new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), sides);
        Face(new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(-x, -y, z), sides);
        Face(new Vector3(x, y, -z), new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z), sides);
        Face(new Vector3(-x, y, -z), new Vector3(-x, -y, -z), new Vector3(-x, -y, z), new Vector3(-x, y, z), sides);
        Face(new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(x, -y, z), sides);
        var mesh = new Mesh { name = "İnce opak ölçüm kağıdı", subMeshCount = 2 };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
        mesh.SetTriangles(front, 0); mesh.SetTriangles(sides, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
        return mesh;
    }

    private void ReleaseCapture()
    {
        if (captureCamera != null) { captureCamera.enabled = false; captureCamera.targetTexture = null; }
        captureCamera = null;
        if (captureRoot != null) UnityEngine.Object.Destroy(captureRoot);
        captureRoot = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ReleaseCapture();
        if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        if (print != null) { print.Release(); UnityEngine.Object.Destroy(print); print = null; }
        foreach (var resource in owned) if (resource != null) UnityEngine.Object.Destroy(resource);
        owned.Clear(); Root = Paper = null;
    }
}

internal sealed class TuringPaperCapture : MonoBehaviour
{
    public void Capture(Camera camera, Action completed) => StartCoroutine(Finish(camera, completed));

    private static IEnumerator Finish(Camera camera, Action completed)
    {
        // Allow canvas meshes and the normal HDRP camera loop to finish one frame.
        yield return null;
        yield return new WaitForEndOfFrame();
        if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
        completed();
    }
}
