using System;
using UnityEngine;

// Changes only the original CRT's small printed-number strip, on a private material.
public sealed class TuringCrtInk : IDisposable
{
    private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789>:/- ";
    private static readonly byte[,] Glyphs =
    {
        {0x7E,0x11,0x11,0x11,0x7E}, {0x7F,0x49,0x49,0x49,0x36},
        {0x3E,0x41,0x41,0x41,0x22}, {0x7F,0x41,0x41,0x22,0x1C},
        {0x7F,0x49,0x49,0x49,0x41}, {0x7F,0x09,0x09,0x09,0x01},
        {0x3E,0x41,0x49,0x49,0x7A}, {0x7F,0x08,0x08,0x08,0x7F},
        {0x00,0x41,0x7F,0x41,0x00}, {0x20,0x40,0x41,0x3F,0x01},
        {0x7F,0x08,0x14,0x22,0x41}, {0x7F,0x40,0x40,0x40,0x40},
        {0x7F,0x02,0x0C,0x02,0x7F}, {0x7F,0x04,0x08,0x10,0x7F},
        {0x3E,0x41,0x41,0x41,0x3E}, {0x7F,0x09,0x09,0x09,0x06},
        {0x3E,0x41,0x51,0x21,0x5E}, {0x7F,0x09,0x19,0x29,0x46},
        {0x46,0x49,0x49,0x49,0x31}, {0x01,0x01,0x7F,0x01,0x01},
        {0x3F,0x40,0x40,0x40,0x3F}, {0x1F,0x20,0x40,0x20,0x1F},
        {0x3F,0x40,0x38,0x40,0x3F}, {0x63,0x14,0x08,0x14,0x63},
        {0x07,0x08,0x70,0x08,0x07}, {0x61,0x51,0x49,0x45,0x43},
        {0x3E,0x51,0x49,0x45,0x3E}, {0x00,0x42,0x7F,0x40,0x00},
        {0x42,0x61,0x51,0x49,0x46}, {0x21,0x41,0x45,0x4B,0x31},
        {0x18,0x14,0x12,0x7F,0x10}, {0x27,0x45,0x45,0x45,0x39},
        {0x3C,0x4A,0x49,0x49,0x30}, {0x01,0x71,0x09,0x05,0x03},
        {0x36,0x49,0x49,0x49,0x36}, {0x06,0x49,0x49,0x29,0x1E},
        {0x00,0x41,0x22,0x14,0x08}, {0x00,0x36,0x36,0x00,0x00},
        {0x20,0x10,0x08,0x04,0x02}, {0x08,0x08,0x08,0x08,0x08},
        {0x00,0x00,0x00,0x00,0x00}
    };

    private MeshRenderer screen;
    private int materialIndex;
    private Material originalMaterial, material, patchMaterial;
    private Texture originalBase, originalEmission;
    private RenderTexture baseAtlas, emissionAtlas;
    private Texture2D baseStrip, emissionStrip;
    private Color32[] background, basePixels, emissionPixels;
    private RectInt band;
    private Vector4 bandUV;
    private string currentText;
    private bool disposed;

    public static TuringCrtInk Create(Transform machine)
    {
        TuringCrtInk ink = null;
        try
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            MeshRenderer target = null;
            int slot = -1;
            foreach (var renderer in machine.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "turing-machine.1") continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && materials[i].name.Replace(" (Instance)", "") == "M_TM2")
                    { target = renderer; slot = i; break; }
                if (target != null) break;
            }
            if (target == null)
                throw new InvalidOperationException("turing-machine.1 üzerindeki M_TM2 ekran materyali bulunamadı.");
            var shader = Shader.Find("Hidden/TuringCrtPatch");
            if (shader == null) throw new InvalidOperationException("Hidden/TuringCrtPatch shader bulunamadı.");
            ink = new TuringCrtInk { screen = target, materialIndex = slot };
            ink.originalMaterial = target.sharedMaterials[slot];
            ink.originalBase = ink.originalMaterial.GetTexture("_BaseColorMap");
            ink.originalEmission = ink.originalMaterial.GetTexture("_EmissiveColorMap");
            if (ink.originalBase == null || ink.originalEmission == null)
                throw new InvalidOperationException("M_TM2 base/emissive atlası eksik.");
            ink.material = new Material(ink.originalMaterial) { name = "M_TM2 / Canlı CRT", hideFlags = HideFlags.DontSave };
            ink.patchMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            int width = ink.originalBase.width, height = ink.originalBase.height;
            ink.baseAtlas = MakeAtlas(width, height, "Turing CRT Base");
            ink.emissionAtlas = MakeAtlas(width, height, "Turing CRT Emission");
            int left = Mathf.FloorToInt(116f / 4096 * width);
            int bottom = Mathf.FloorToInt(1843f / 4096 * height);
            ink.band = new RectInt(left, bottom,
                Mathf.CeilToInt(166f / 4096 * width) - left,
                Mathf.CeilToInt(2388f / 4096 * height) - bottom);
            ink.bandUV = new Vector4((float)left / width, (float)bottom / height,
                (float)ink.band.width / width, (float)ink.band.height / height);
            ink.ReadBackground();
            ink.baseStrip = MakeStrip(ink.band.width, ink.band.height);
            ink.emissionStrip = MakeStrip(ink.band.width, ink.band.height);
            ink.basePixels = new Color32[ink.background.Length];
            ink.emissionPixels = new Color32[ink.background.Length];
            ink.SetReadout("> KART BEKLENIYOR");
            ink.material.SetTexture("_BaseColorMap", ink.baseAtlas);
            ink.material.SetTexture("_MainTex", ink.baseAtlas);
            ink.material.SetTexture("_EmissiveColorMap", ink.emissionAtlas);
            if (ink.material.HasProperty("_EmissionMap")) ink.material.SetTexture("_EmissionMap", ink.emissionAtlas);
            var updated = target.sharedMaterials;
            updated[slot] = ink.material;
            target.sharedMaterials = updated;
            return ink;
        }
        catch (Exception exception)
        {
            ink?.Dispose();
            Debug.LogWarning("Turing CRT yazısı oluşturulamadı: " + exception.Message, machine);
            return null;
        }
    }

    private static RenderTexture MakeAtlas(int width, int height, string name)
    {
        var atlas = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = name, hideFlags = HideFlags.DontSave, useMipMap = true,
            autoGenerateMips = false, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat
        };
        if (!atlas.Create()) { Release(atlas); throw new InvalidOperationException(name + " GPU dokusu açılamadı."); }
        return atlas;
    }

    private static Texture2D MakeStrip(int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false, false)
    { name = "CRT Yazı Şeridi", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

    private void ReadBackground()
    {
        var previous = RenderTexture.active;
        bool previousSRGB = GL.sRGBWrite;
        Texture2D sample = null;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(originalBase, baseAtlas);
            RenderTexture.active = baseAtlas;
            // Include a neighbouring ink-free column, to retain the screen's original grey/grime.
            int sampleWidth = band.width + Mathf.CeilToInt(48f / 4096 * baseAtlas.width);
            sample = MakeStrip(sampleWidth, band.height);
            sample.ReadPixels(new Rect(band.x, band.y, sampleWidth, band.height), 0, 0, false);
            var source = sample.GetPixels32();
            background = new Color32[band.width * band.height];
            for (int y = 0; y < band.height; y++)
                for (int x = 0; x < band.width; x++)
                {
                    Color32 pixel = source[y * sampleWidth + x];
                    if (pixel.g > pixel.r + 4 && pixel.g > pixel.b + 4)
                        pixel = source[y * sampleWidth + sampleWidth - 2];
                    background[y * band.width + x] = pixel;
                }
        }
        finally
        {
            RenderTexture.active = previous;
            GL.sRGBWrite = previousSRGB;
            Release(sample);
        }
    }

    public void SetReadout(string text)
    {
        if (disposed) return;
        text = (text ?? "").ToUpperInvariant().Replace('İ', 'I').Replace('Ş', 'S')
            .Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
        if (text.Length > 20) text = text.Substring(0, 20);
        if (text == currentText) return;
        currentText = text;
        float scaleY = Mathf.Min(4.5f * baseAtlas.height / 4096,
            (band.height - 20f * baseAtlas.height / 4096) / Mathf.Max(1, text.Length * 6 - 1));
        float scaleX = scaleY * baseAtlas.width / baseAtlas.height;
        float startX = (band.width - 7 * scaleX) * .5f;
        float top = 10f * baseAtlas.height / 4096;
        var green = new Color32(52, 163, 33, 255);
        var black = new Color32(0, 0, 0, 255);
        for (int y = 0; y < band.height; y++)
            for (int x = 0; x < band.width; x++)
            {
                // Clockwise in the atlas: its downward strip is the model's horizontal line.
                float coverage = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        int row = 6 - Mathf.FloorToInt((x + .25f + sx * .5f - startX) / scaleX);
                        int advance = Mathf.FloorToInt((band.height - y - .25f - sy * .5f - top) / scaleY);
                        int character = advance / 6, column = advance % 6;
                        if (advance < 0 || character >= text.Length || column >= 5 || row < 0 || row >= 7) continue;
                        int glyph = Characters.IndexOf(text[character]);
                        if (glyph >= 0 && (Glyphs[glyph, column] & (1 << row)) != 0) coverage += .25f;
                    }
                int index = y * band.width + x;
                basePixels[index] = Color32.Lerp(background[index], green, coverage);
                emissionPixels[index] = Color32.Lerp(black, green, coverage);
            }
        baseStrip.SetPixels32(basePixels); baseStrip.Apply(false, false);
        emissionStrip.SetPixels32(emissionPixels); emissionStrip.Apply(false, false);
        bool previousSRGB = GL.sRGBWrite;
        var previousTarget = RenderTexture.active;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            patchMaterial.SetVector("_PatchRect", bandUV);
            patchMaterial.SetTexture("_Patch", baseStrip);
            Graphics.Blit(originalBase, baseAtlas, patchMaterial);
            patchMaterial.SetTexture("_Patch", emissionStrip);
            Graphics.Blit(originalEmission, emissionAtlas, patchMaterial);
            baseAtlas.GenerateMips(); emissionAtlas.GenerateMips();
        }
        finally { GL.sRGBWrite = previousSRGB; RenderTexture.active = previousTarget; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (screen != null)
        {
            var materials = screen.sharedMaterials;
            if (materialIndex < materials.Length && materials[materialIndex] == material)
            { materials[materialIndex] = originalMaterial; screen.sharedMaterials = materials; }
        }
        Release(baseAtlas); Release(emissionAtlas); Release(baseStrip); Release(emissionStrip);
        Release(material); Release(patchMaterial);
    }

    private static void Release(UnityEngine.Object resource)
    {
        if (resource == null) return;
        if (resource is RenderTexture render) render.Release();
        if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
        else UnityEngine.Object.DestroyImmediate(resource);
    }
}
