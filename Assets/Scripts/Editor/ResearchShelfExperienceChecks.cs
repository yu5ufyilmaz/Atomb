using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResearchShelfExperienceChecks
{
    [Serializable] private class Probe
    {
        public string shelf, parent;
        public Vector3 bookCenter, size, signPosition, spineOut;
        public string[] renderers;
    }
    [Serializable] private class Report { public List<Probe> shelves = new List<Probe>(); }

    [MenuItem("Senzora/Book Motion/Shelf Inspect")]
    public static void Inspect()
    {
        var trial = UnityEngine.Object.FindFirstObjectByType<ResearchPuzzleTrial>();
        var report = new Report();
        var furniture = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.GetComponentInParent<InteractableBook>() == null && r.GetComponent<TextMeshPro>() == null && !r.transform.name.Contains("Plaka")).ToArray();
        foreach (var group in trial.books.Where(b => Shelf(b) != null).GroupBy(Shelf))
        {
            var bounds = group.First().bookSkinnedMeshRenderer.bounds;
            foreach (var book in group) bounds.Encapsulate(book.bookSkinnedMeshRenderer.bounds);
            var sign = group.Key.Find("Araştırma Raf İsimliği");
            report.shelves.Add(new Probe { shelf = group.Key.name, parent = group.Key.parent.name,
                bookCenter = bounds.center, size = bounds.size, signPosition = sign != null ? sign.position : Vector3.zero,
                spineOut = -group.First().transform.Find("Armature.001/Root/Binding").forward,
                renderers = furniture.Where(r => (r.bounds.ClosestPoint(bounds.center) - bounds.center).sqrMagnitude < .15f)
                    .Select(r => r.name + " / " + r.bounds).ToArray() });
        }
        Directory.CreateDirectory("UserSettings/PuzzleWorkshop");
        File.WriteAllText("UserSettings/PuzzleWorkshop/shelf-inspect.json", JsonUtility.ToJson(report, true));
        Debug.Log("SHELF INSPECT saved UserSettings/PuzzleWorkshop/shelf-inspect.json");
    }

    [MenuItem("Senzora/Book Motion/Shelf Labels Check")]
    public static void Labels()
    {
        var trial = UnityEngine.Object.FindFirstObjectByType<ResearchPuzzleTrial>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SpecialElite-Regular SDF.asset");
        foreach (var book in trial.books)
        {
            var labels = book.GetComponentsInChildren<TextMeshPro>(true).Where(t => t.transform.parent.name.StartsWith("Araştırma Kitap Adı")).ToArray();
            Require(labels.Length == 2, "Eksik kapak/sırt etiketi: " + book.name);
            foreach (var text in labels)
            {
                Require(text.font == font, book.name + ": kitap adı varsayılan fontta.");
                text.ForceMeshUpdate(true);
                Require(!text.isTextOverflowing && !text.isTextTruncated, book.name + ": başlık taşmış.");
                Require(text.text.All(c => char.IsWhiteSpace(c) || font.HasCharacter(c, true, true)), "Eksik Türkçe harf: " + text.text);
            }
        }
        var cabinets = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name.IndexOf("BookCase", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        int signs = 0;
        foreach (var group in trial.books.Where(b => Shelf(b) != null).GroupBy(Shelf))
        {
            var sign = group.Key.Find("Araştırma Raf İsimliği");
            Require(sign != null, "Eksik raf tabelası: " + group.Key.name);
            Vector3 center = group.First().bookSkinnedMeshRenderer.bounds.center;
            var cabinet = cabinets.OrderBy(r => (r.bounds.ClosestPoint(center) - center).sqrMagnitude).First();
            Vector3 outward = -sign.forward, extents = cabinet.bounds.extents;
            float front = Vector3.Dot(extents, new Vector3(Mathf.Abs(outward.x), 0, Mathf.Abs(outward.z)));
            Require(Vector3.Dot(sign.position - cabinet.bounds.center, outward) > front + .02f,
                group.Key.name + ": tabela raf gövdesinin içinde.");
            var label = sign.GetComponentInChildren<TextMeshPro>(); label.ForceMeshUpdate(true);
            Require(label.font == font && !label.isTextOverflowing && label.transform.localScale.x <= .0011f,
                "Raf fontu veya TMP ölçüsü yanlış: " + group.Key.name);
            Require(label.textBounds.size.y * label.transform.lossyScale.y >= .055f,
                "Raf yazısı fiziksel plakaya göre çok küçük: " + group.Key.name);
            signs++;
        }
        Debug.Log("SHELF LABELS PASS: " + trial.books.Length + " cover/spine pairs, " + signs + " exterior signs, Turkish glyphs and fitted authored typography.");
    }

    [MenuItem("Senzora/Book Motion/Shelf Capture Labels")]
    public static void Capture()
    {
        Require(!Application.isPlaying, "Edit modunda çalıştır.");
        var trial = UnityEngine.Object.FindFirstObjectByType<ResearchPuzzleTrial>();
        var source = Camera.main;
        var obj = new GameObject("Temporary label camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = obj.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false;
        var render = new RenderTexture(1440, 960, 24); var pixels = new Texture2D(1440, 960, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            Directory.CreateDirectory("UserSettings/PuzzleWorkshop/evidence");
            camera.targetTexture = render; camera.aspect = 1.5f; camera.fieldOfView = 45; camera.nearClipPlane = .01f;
            foreach (var group in trial.books.Where(b => Shelf(b) != null).GroupBy(Shelf))
            {
                var sign = group.Key.Find("Araştırma Raf İsimliği");
                camera.transform.SetPositionAndRotation(sign.position - sign.forward * 1.2f + Vector3.down * .2f,
                    Quaternion.LookRotation(sign.forward * 1.2f + Vector3.up * .2f));
                SaveImage(camera, render, pixels, group.Key.name + "-sign.png");
            }
            var label = trial.books.First(b => Shelf(b) != null).GetComponentsInChildren<TextMeshPro>().First(t => t.transform.parent.name.EndsWith("Sırt"));
            var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
            float span = Mathf.Max(Vector3.Distance(corners[0], corners[1]), Vector3.Distance(corners[1], corners[2]));
            camera.transform.SetPositionAndRotation(label.transform.position - label.transform.forward * span * 2,
                Quaternion.LookRotation(label.transform.forward, label.transform.up));
            SaveImage(camera, render, pixels, "book-spine-title.png");
            Debug.Log("SHELF LABEL CAPTURES: UserSettings/PuzzleWorkshop/evidence");
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(pixels); }
    }

    private static void SaveImage(Camera camera, RenderTexture render, Texture2D pixels, string name)
    {
        camera.Render(); RenderTexture.active = render;
        pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); pixels.Apply();
        File.WriteAllBytes("UserSettings/PuzzleWorkshop/evidence/" + name, pixels.EncodeToPNG());
    }

    [MenuItem("Senzora/Book Motion/Shelf Pickup Live Check")]
    public static void Live()
    {
        Require(Application.isPlaying, "Play gerekli.");
        Application.runInBackground = true;
        ResearchPuzzleTrial.Active.StartCoroutine(Pickup());
    }

    private static IEnumerator Pickup()
    {
        yield return ResearchGuidanceChecks.EnterNormalGame();
        var shelves = ResearchPuzzleTrial.Active.books.Where(b => Shelf(b) != null).GroupBy(Shelf).ToArray();
        var selected = new[] { shelves[0].First(), shelves[0].ElementAt(shelves[0].Count()/2), shelves.Last().Last() };
        foreach (var book in selected)
        {
        var home = book.transform.position; var rotation = book.transform.rotation;
        var binding = book.transform.Find("Armature.001/Root/Binding/Page.L"); var cover = binding.localRotation;
        Vector3 direction = book.transform.parent.TransformDirection(book.shelfPullDirection).normalized;
        var extent = book.bookSkinnedMeshRenderer.bounds.extents;
        float depth = 2 * Vector3.Dot(extent, new Vector3(Mathf.Abs(direction.x), 0, Mathf.Abs(direction.z)));
        book.Interact();
        yield return new WaitForSeconds(.08f);
        Require(Quaternion.Angle(rotation, book.transform.rotation) < .1f,
            "RED: kitap raftan kurtulmadan dönmeye başladı.");
        Require(Vector3.Distance(home, book.transform.position) > .005f, "Kitap raftan düz çekilmiyor.");
        Require(Quaternion.Angle(cover, binding.localRotation) < .1f, "Kitap rafın içinde kapağını açtı.");
        Require(Vector3.Cross(book.transform.position - home, direction).magnitude < .0001f, "Raf çıkışı yana veya yukarı sapıyor.");
        var frozen = book.transform.position; GameManager.Instance.isGamePaused = true;
        yield return new WaitForSecondsRealtime(.12f);
        Require(Vector3.Distance(frozen, book.transform.position) < .00001f, "Duraklatmada raftan çıkış sürdü.");
        GameManager.Instance.isGamePaused = false;
        while (book.IsShelfPulling) yield return null;
        Require(Vector3.Dot(book.transform.position - home, direction) >= depth - .002f,
            "Kitabın arka kenarı rafı tamamen geçmeden dönüş başladı.");
        while (book.isAnimating) yield return null;
        Require(book.isOpen, "Kitap okuyucuya ulaşmadı.");
        if (book.GetComponentsInChildren<NotebookDraggableBlock>().Any(n => n.IsSource && ResearchTrialBookChecks.Visible(n)))
            ResearchTrialBookChecks.CheckReadability();
        ScreenCapture.CaptureScreenshot("UserSettings/PuzzleWorkshop/evidence/shelf-pickup-open.png"); yield return null;
        book.ForceExit();
        while (book.isAnimating) yield return null;
        Require(Vector3.Distance(home, book.transform.position) < .0001f && Quaternion.Angle(rotation, book.transform.rotation) < .01f,
            "Kitap rafındaki aynı yere dönmedi.");
        }
        Debug.Log("SHELF PICKUP LIVE PASS: edge/middle/rotated shelves; closed straight full-depth extraction, pause, readable pages and exact return.");
    }

    internal static Transform Shelf(InteractableBook book)
    {
        for (var parent = book.transform.parent; parent != null; parent = parent.parent)
            if (parent.name.StartsWith("RAF", StringComparison.OrdinalIgnoreCase)) return parent;
        return null;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
