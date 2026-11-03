using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Kitap isimlikleri ışıksız (Unlit/SDF) materyalle karanlıkta parlıyordu (3 Kasım oyun testi).
// Plaka ve yazı için ışıktan etkilenen kopyalar; arayüz yazıları yazı tipinin varsayılan materyalinde kalır.
public static class BookTitleLighting
{
    const string Folder = "Assets/Materials/BookTitles";
    const string UnlitPlatePath = "Assets/Materials/SynthesisBlockout/Blockout_Tahta.mat";
    const string PlatePath = Folder + "/BookTitlePlate_Lit.mat";

    public static Material PlateMaterial()
    {
        var lit = AssetDatabase.LoadAssetAtPath<Material>(PlatePath);
        if (lit != null) return lit;
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPlatePath);
        lit = new Material(Shader.Find("HDRP/Lit")) { name = "BookTitlePlate_Lit" };
        lit.SetColor("_BaseColor", unlit.GetColor("_UnlitColor"));
        lit.SetFloat("_Smoothness", .25f);
        HDMaterial.ValidateMaterial(lit);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Materials", "BookTitles");
        AssetDatabase.CreateAsset(lit, PlatePath);
        return lit;
    }

    public static Material FontMaterial(TMP_FontAsset font)
    {
        string path = $"{Folder}/{font.name} Lit.mat";
        var lit = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (lit != null) return lit;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Materials", "BookTitles");
        // Same atlas and SDF settings as the font's own material, on TMP's HDRP lit shader graph.
        lit = new Material(font.material) { name = font.name + " Lit", shader = Shader.Find("TextMeshPro/SRP/TMP_SDF-HDRP LIT") };
        HDMaterial.ValidateMaterial(lit);
        AssetDatabase.CreateAsset(lit, path);
        return lit;
    }

    [MenuItem("Senzora/Oyun Testi/Kitap İsimlerini Işığa Bağla")]
    public static void ApplyToOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPlatePath);
        var plate = PlateMaterial();
        int plates = 0, labels = 0;
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.sharedMaterial != unlit || renderer.name != "Plaka") continue;
            Undo.RecordObject(renderer, "Kitap İsimliği Işığı");
            renderer.sharedMaterial = plate; plates++;
        }
        foreach (var text in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font == null || text.fontSharedMaterial != text.font.material || !(text.name == "Başlık" || text.transform.parent.name.Contains("İsimli"))) continue;
            Undo.RecordObject(text, "Kitap İsimliği Işığı");
            text.fontSharedMaterial = FontMaterial(text.font); labels++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"BOOK TITLE LIGHTING: {plates} plates, {labels} labels now lit.");
    }
}
