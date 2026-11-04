using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lists scene renderers whose imported mesh is empty (0 vertices). Unity 6000.3 empties a mesh when
// "Generate Lightmap UVs" fails on it (tiny cm-scale meshes with Calculate margin), so it silently stops rendering.
public static class MissingModelChecks
{
    const string LogPath = "outputs/missing-models-2026-11-04/log.txt";

    [MenuItem("Senzora/Doğrulama/Kayıp Modeller")]
    public static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var lines = all.Where(r => r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && mf.sharedMesh.vertexCount == 0)
            .Select(r => $"EMPTY {r.name} active={r.gameObject.activeInHierarchy} mesh={r.GetComponent<MeshFilter>().sharedMesh.name} asset={AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)}")
            .ToList();
        lines.Add($"SCAN renderers={all.Length} emptyMeshes={lines.Count}");
        File.WriteAllLines(LogPath, lines);
        foreach (var l in lines) Debug.Log("MISSING MODELS " + l);
    }

    // Play, after the game has started: shots of the valve gauge and the oscilloscope from each machine's own interaction camera.
    [MenuItem("Senzora/Doğrulama/Kayıp Modeller Görüntü (Play)")]
    public static void Capture()
    {
        if (!Application.isPlaying || Camera.main == null) throw new System.InvalidOperationException("Oyun içinde Play gerekli.");
        Camera.main.GetComponent<MonoBehaviour>().StartCoroutine(CaptureFlow(Path.GetDirectoryName(LogPath)));
    }

    static System.Collections.IEnumerator CaptureFlow(string dir)
    {
        Application.runInBackground = true;
        var main = Camera.main;
        var cam = new GameObject("MissingModelsCam").AddComponent<Camera>();
        cam.CopyFrom(main); cam.depth = main.depth + 10; main.enabled = false;
        var lines = new System.Collections.Generic.List<string>();
        foreach (var (label, target) in new[] { ("needle", "DM_PressureLeveler"), ("oscilloscope", "OsiloskopTennis") })
        {
            var go = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == target && t.gameObject.activeInHierarchy);
            // The interactable owning this model keeps its close-up pose in a public fixedCameraTransform field.
            var pose = go == null ? null : go.GetComponentsInParent<MonoBehaviour>(true).Concat(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                .Select(b => b.GetType().GetField("fixedCameraTransform")?.GetValue(b) as Transform)
                .FirstOrDefault(t => t != null && Vector3.Distance(t.position, go.transform.position) < 3f);
            if (pose == null) { lines.Add($"{label} SKIP target={go} pose=none"); continue; }
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            for (int i = 0; i < 10; i++) yield return null;
            string file = $"{dir}/{label}.png";
            ScreenCapture.CaptureScreenshot(file);
            for (int i = 0; i < 3; i++) yield return null;
            lines.Add($"{label} shot={file} occlusion={cam.useOcclusionCulling}");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                lines.Add($"  {r.name} isVisible={r.isVisible} verts={(r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh ? mf.sharedMesh.vertexCount : -1)} bounds={r.bounds.center}/{r.bounds.size}");
        }
        main.enabled = true; Object.Destroy(cam.gameObject);
        File.WriteAllLines($"{dir}/capture.txt", lines);
        Debug.Log("MISSING MODELS CAPTURE DONE");
    }
}
