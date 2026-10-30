using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class LocalizationPresentationChecks
{
    [MenuItem("Tools/Localization/Validate Presentation")]
    public static void All()
    {
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var fixtureScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(fixtureScene);
        string previousCode = GameLocalization.CurrentCode;
        var failures = new List<string>();
        try
        {
            GameLocalization.SetLanguage("en");
            foreach (Action check in new Action[] { TurkishGlyphs, MenuAlignment, SettingsLayout, SceneSettings, Artwork, TextOnlyOverride, AutoSizingOverride, Parts, Goals, Tutorial })
                try { check(); }
                catch (Exception error) { failures.Add(check.Method.Name + ": " + error.GetBaseException().Message); }
        }
        finally { GameLocalization.SetLanguage(previousCode);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(fixtureScene,true);if(originalScene.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene); }
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
        Debug.Log("LOCALIZATION PRESENTATION PASS: Turkish glyphs and atlas overflow, menu alignment, both actual settings panels and EN/TR style restoration, artwork, overrides, 19 parts, 10 authored goals, observation notes and generated tutorial pages.");
    }

    private static TMP_FontAsset MenuFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        AssetDatabase.GUIDToAssetPath("50a5b623ac891d941ab489e703732cd8"));

    private static void TurkishGlyphs()
    {
        string[] paths = {
            "Assets/Sprites/UI/MainMenu/Font/static/DMSans-Thin SDF.asset",
            "Assets/Sprites/UI/MainMenu/Font/static/DMSans-ExtraLight SDF.asset",
            "Assets/Sprites/UI/MainMenu/Font/static/DMSans-Medium SDF.asset",
            "Assets/TextMesh Pro/Fonts/SpecialElite-Regular SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"
        };
        foreach (string path in paths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Require(font != null && font.sourceFontFile != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic,
                path + ": Turkish glyphs cannot be added to the selected font.");
            Require(font.HasCharacters("ÇĞİÖŞÜçğıöşü", out uint[] missingActual, false, true),
                path + ": the actual serialized atlas could not supply Turkish glyphs without fallback: "
                + (missingActual == null ? "" : string.Join(",", missingActual)));
            var crowded = TMP_FontAsset.CreateFontAsset(font.sourceFontFile, 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 256, 256, AtlasPopulationMode.Dynamic, font.isMultiAtlasTexturesEnabled);
            try
            {
                Require(crowded.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789ÇĞİÖŞÜçğıöşü", out string missing),
                    path + ": atlas overflow would switch glyphs to a fallback font: " + missing);
            }
            finally
            {
                foreach (var texture in crowded.atlasTextures) if (texture != null) Object.DestroyImmediate(texture);
                Object.DestroyImmediate(crowded.material);
                Object.DestroyImmediate(crowded);
            }
        }
    }

    private static void MenuAlignment()
    {
        GameLocalization.SetLanguage("en");
        var root = new GameObject("Menu alignment check", typeof(RectTransform), typeof(Canvas));
        try
        {
            var label = new GameObject("TextThin", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(root.transform, false);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Sprites/UI/MainMenu/Font/static/DMSans-ExtraLight SDF.asset");
            label.fontSize = 48;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(382, 56);
            label.text = "CREDITS";
            label.ForceMeshUpdate(true, true);
            float sourceLeft = label.textInfo.lineInfo[0].lineExtents.min.x;
            var binding = label.gameObject.AddComponent<LocalizedText>();
            typeof(LocalizedText).GetField("preserveSourceLeftEdge")?.SetValue(binding, true);
            binding.SetSource("CREDITS");
            GameLocalization.SetLanguage("tr");
            binding.Apply();
            label.ForceMeshUpdate(true, true);
            Require(label.textInfo.lineCount == 1 && !label.isTextOverflowing && label.fontSize <= 48
                && Mathf.Abs(label.textInfo.lineInfo[0].lineExtents.min.x - sourceLeft) < 3,
                "Turkish menu title wraps or shifts away from its English left edge: lines=" + label.textInfo.lineCount
                + ", overflow=" + label.isTextOverflowing + ", fontSize=" + label.fontSize
                + ", left=" + label.textInfo.lineInfo[0].lineExtents.min.x + ", sourceLeft=" + sourceLeft);
            foreach (var character in label.textInfo.characterInfo)
                if (character.isVisible) Require(character.fontAsset == label.font, "A Turkish menu character switched font.");
            GameLocalization.SetLanguage("en");
            binding.Apply();
            label.ForceMeshUpdate(true, true);
            Require(label.alignment == TextAlignmentOptions.Center && label.fontSize == 48 && !label.enableAutoSizing
                && Mathf.Abs(label.textInfo.lineInfo[0].lineExtents.min.x - sourceLeft) < .1f,
                "Returning to English did not restore the authored menu layout.");
        }
        finally { Object.DestroyImmediate(root); GameLocalization.SetLanguage("en"); }
    }

    private static void SettingsLayout()
    {
        var root = new GameObject("Settings check", typeof(RectTransform));
        try
        {
            root.SetActive(false);
            var manager = root.AddComponent<SettingsUIManager>();
            var row = new GameObject("SFX", typeof(RectTransform));
            row.transform.SetParent(root.transform, false);
            var slider = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            slider.transform.SetParent(row.transform, false);
            ((RectTransform)slider.transform).anchoredPosition = new Vector2(180, 0);
            var label = new GameObject("Label", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(row.transform, false);
            label.font = MenuFont;
            label.fontSize = 27;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchoredPosition = new Vector2(-73, 9);
            label.rectTransform.sizeDelta = new Vector2(105, 29);
            manager.EnsureLanguageSelector();
            manager.EnsureLanguageSelector();
            Require(label.rectTransform.anchoredPosition == new Vector2(-73, 9)
                && label.rectTransform.sizeDelta == new Vector2(105, 29)
                && label.alignment == TextAlignmentOptions.Center && label.fontSize == 27 && label.font == MenuFont,
                "Opening settings overwrote the authored label layout/style.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Rect InPanel(RectTransform rect, Transform panel)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 min = panel.InverseTransformPoint(corners[0]);
        Vector3 max = panel.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static string Style(TMP_Text label)
    {
        var rect = label.rectTransform;
        return label.font.GetInstanceID() + "/" + label.fontSharedMaterial.GetInstanceID() + "/" + label.fontSize
            + "/" + label.fontStyle + "/" + label.enableAutoSizing + "/" + label.alignment + "/" + rect.anchorMin + "/" + rect.anchorMax
            + "/" + rect.pivot + "/" + rect.anchoredPosition + "/" + rect.sizeDelta;
    }

    private static void SceneSettings()
    {
        const string path = "Assets/Scenes/scn_LaboratorySynthesis.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        var sources = new List<SettingsUIManager>();
        foreach (var root in scene.GetRootGameObjects()) sources.AddRange(root.GetComponentsInChildren<SettingsUIManager>(true));
        Require(sources.Count >= 2, "Actual normal and pause settings panels are missing.");
        try
        {
            foreach (var source in sources)
            {
                var host = new GameObject("Settings canvas check", typeof(RectTransform), typeof(Canvas));
                host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)host.transform).sizeDelta = new Vector2(1920,1080);
                try
                {
                    var clone = Object.Instantiate(source.gameObject,host.transform).GetComponent<SettingsUIManager>();
                    clone.gameObject.SetActive(true); clone.EnsureLanguageSelector();
                    Require(clone.View != null, "Shared settings view not attached.");
                    foreach (string code in new[]{"en","tr","en"})
                    {
                        GameLocalization.SetLanguage(code);
                        SettingsMenuChecks.ValidateView(clone.View);
                    }
                }
                finally { Object.DestroyImmediate(host); }
            }
        }
        finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);GameLocalization.SetLanguage("en"); }
    }
    private static void Artwork()
    {
        GameLocalization.SetLanguage("en");
        var root = new GameObject("Artwork check", typeof(RectTransform));
        try
        {
            var image = new GameObject("Sound", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(root.transform, false);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/UI3/EscMenuScreen/Sound.png");
            Require(image.sprite != null, "The real menu title artwork was not loaded.");
            image.rectTransform.sizeDelta = new Vector2(95, 22);
            var reference = new GameObject("Reference", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            reference.transform.SetParent(root.transform, false);
            reference.font = MenuFont;
            reference.fontSize = 27;
            typeof(GameLocalization).GetMethod("LocalizeSettingsArtwork", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { root });
            var label = image.GetComponentInChildren<TMP_Text>(true);
            Require(label != null && image.enabled && !label.enabled, "English replaced the original title artwork: label="
                + label?.text + ", image=" + image.enabled + ", label enabled=" + label?.enabled + ", language=" + GameLocalization.CurrentCode
                + ", sprite=" + image.sprite?.name);
            GameLocalization.SetLanguage("tr");
            label.GetComponent<LocalizedText>().Apply();
            string translatedTitle = GameLocalization.Get("ui.settings.sound");
            Require(!image.enabled && label.enabled && label.text == translatedTitle && translatedTitle != "Sound" && label.font == MenuFont,
                "Translated artwork lost its menu font or did not update: actual=" + label.text + ", expected=" + translatedTitle + ", font=" + label.font?.name);
            GameLocalization.SetLanguage("en");
            label.GetComponent<LocalizedText>().Apply();
            Require(image.enabled && !label.enabled && image.rectTransform.sizeDelta == new Vector2(95, 22),
                "Returning to English did not restore the artwork/layout.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void TextOnlyOverride()
    {
        GameLocalization.SetLanguage("en");
        var label = new GameObject("Override check", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        try
        {
            var binding = label.gameObject.AddComponent<LocalizedText>();
            binding.overrides = new[] { new LocalizedText.LocaleOverride { languageCode = "en", overrideText = true, text = "Example" } };
            binding.SetSource("Atım Kaynağı");
            label.font = MenuFont;
            label.fontSize = 19;
            label.rectTransform.anchoredPosition = new Vector2(42, -17);
            label.rectTransform.sizeDelta = new Vector2(320, 65);
            binding.Apply();
            Require(label.font == MenuFont && label.fontSize == 19
                && label.rectTransform.anchoredPosition == new Vector2(42, -17)
                && label.rectTransform.sizeDelta == new Vector2(320, 65),
                "A text-only translation restored an obsolete font/position.");
        }
        finally { Object.DestroyImmediate(label.gameObject); }
    }

    private static void AutoSizingOverride()
    {
        GameLocalization.SetLanguage("en");
        var root = new GameObject("Auto-size check", typeof(RectTransform), typeof(Canvas));
        try
        {
            var label = new GameObject("Label", typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(root.transform, false);
            label.font = MenuFont;
            label.enableAutoSizing = false;
            label.fontSize = 36;
            label.fontSizeMin = 8;
            label.fontSizeMax = 36;
            label.rectTransform.sizeDelta = new Vector2(100, 30);
            var binding = label.gameObject.AddComponent<LocalizedText>();
            binding.overrides = new[] { new LocalizedText.LocaleOverride { languageCode = "en",
                overrideAutoSizing = true, autoSizing = true } };
            binding.SetSource("A long translated title that must shrink to fit this small label");
            label.ForceMeshUpdate(true, true);
            Require(label.fontSize < 36, "Auto-size fixture did not exercise shrinking.");
            GameLocalization.SetLanguage("tr");
            binding.Apply();
            Require(!label.enableAutoSizing && label.fontSize == 36, "Leaving an auto-size override lost the authored fixed font size.");
        }
        finally { Object.DestroyImmediate(root); GameLocalization.SetLanguage("en"); }
    }

    private static void Parts()
    {
        GameLocalization.SetLanguage("en");
        var missing = new List<string>();
        foreach (var kind in new[] { PuzzleKind.Signal, PuzzleKind.ColorMixture })
            foreach (var part in PuzzleParts.ForKind(kind))
            {
                if (GameLocalization.Translate(part.title) == part.title) missing.Add(part.id + " title");
                if (GameLocalization.Translate(part.description) == part.description) missing.Add(part.id + " description");
            }
        Require(missing.Count == 0, "Untranslated current part data: " + string.Join(", ", missing));

        var source = SignalPartLibrary.Find("pulse-n12");
        var note = new GameObject("Part note", typeof(RectTransform)).AddComponent<NotebookDraggableBlock>();
        try
        {
            note.Initialize(new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = source.id,
                title = source.title, description = "Gelen akışa uygulanır. " + source.description }, false);
            var heading = note.transform.Find("Başlık").GetComponent<TMP_Text>();
            var body = note.transform.Find("Açıklama").GetComponent<TMP_Text>();
            Require(heading.text.Contains("Sample N-12") && !body.text.Contains("Kısa tepki"), "Part note was only partly translated.");
            GameLocalization.SetLanguage("tr");
            foreach (var binding in note.GetComponentsInChildren<LocalizedText>(true)) binding.Apply();
            Require(heading.text.Contains("Numune N-12"), "Existing part note did not return to Turkish.");
            GameLocalization.SetLanguage("en");
            foreach (var binding in note.GetComponentsInChildren<LocalizedText>(true)) binding.Apply();
            Require(heading.text.Contains("Sample N-12") && note.Data.title == "Numune N-12", "Language switch changed the canonical part data.");
        }
        finally { Object.DestroyImmediate(note.gameObject); }
    }

    private static void Goals()
    {
        GameLocalization.SetLanguage("en");
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:PuzzleDefinitionSO", new[] { "Assets/Scripts/Scriptables/Puzzles" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var observation in PuzzleBookDistribution.Notes(asset.definition))
            {
                Require(GameLocalization.Translate(observation.title) != observation.title
                    && GameLocalization.Translate(observation.description) != observation.description
                    && !System.Text.RegularExpressions.Regex.IsMatch(GameLocalization.Translate(observation.title), "[çğıöşüÇĞİÖŞÜ]")
                    && !System.Text.RegularExpressions.Regex.IsMatch(GameLocalization.Translate(observation.description), "[çğıöşüÇĞİÖŞÜ]"),
                    asset.name + ": an authored observation note stayed Turkish: " + observation.title);
            }
            var data = PuzzleBookDistribution.Notes(asset.definition)[0];
            var note = new GameObject("Goal note", typeof(RectTransform)).AddComponent<NotebookDraggableBlock>();
            try
            {
                note.Initialize(data, true);
                var title = note.transform.Find("Başlık").GetComponent<TMP_Text>();
                var body = note.transform.Find("Açıklama").GetComponent<TMP_Text>();
                Require(title.text.Contains(GameLocalization.Translate(data.title)) && !title.text.Contains(data.title),
                    asset.name + ": authored goal title stayed Turkish in the actual note.");
                Require(!System.Text.RegularExpressions.Regex.IsMatch(body.text, "[çğıöşüÇĞİÖŞÜ]"),
                    asset.name + ": authored goal description stayed Turkish in the actual note.");
                Require(note.Data.title == data.title && note.Data.description == data.description,
                    asset.name + ": displaying the goal changed its canonical data.");
                count++;
            }
            finally { Object.DestroyImmediate(note.gameObject); }
        }
        Require(count >= 10, "Authored goal checks did not exercise the complete puzzle and archive pools.");
    }

    private static void Tutorial()
    {
        GameLocalization.SetLanguage("en");
        var puzzle = ChalkboardTutorial.CreatePuzzle();
        foreach (var page in ChalkboardTutorial.CreatePages(puzzle))
        {
            Require(GameLocalization.Translate(page.title) != page.title && GameLocalization.Translate(page.explanation) != page.explanation,
                "Generated tutorial page remains Turkish: " + page.title);
            foreach (var note in page.blocks)
                Require(GameLocalization.Translate(note.title) != note.title && GameLocalization.Translate(note.description) != note.description,
                    "Generated tutorial card remains Turkish: " + note.title);
        }
        Require(GameLocalization.Translate(puzzle.title) == "First Trace / Board Practice", "The reported tutorial title is untranslated.");
        var data = ChalkboardTutorial.CreatePages(puzzle)[0].blocks[0];
        var target = new GameObject("Tutorial target check", typeof(RectTransform)).AddComponent<NotebookDraggableBlock>();
        try
        {
            target.Initialize(data, true);
            var title = target.transform.Find("Başlık").GetComponent<TMP_Text>();
            var body = target.transform.Find("Açıklama").GetComponent<TMP_Text>();
            Require(title.text.Contains("First Trace / Board Practice")
                && !System.Text.RegularExpressions.Regex.IsMatch(body.text, "[çğıöşüÇĞİÖŞÜ]"),
                "The generated tutorial target stayed Turkish in the actual notebook card.");
            Require(target.Data.title == puzzle.title && target.Data.description == puzzle.description,
                "Displaying the tutorial target changed its canonical data.");
        }
        finally { Object.DestroyImmediate(target.gameObject); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
