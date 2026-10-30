using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class LeesResistanceWorkshopSetup
{
    public const string ScenePath = "Assets/Scenes/scn_LeesResistanceWorkshop.unity";
    public const string Folder = "Assets/LeesResistanceWorkshop";
    public const string RigPath = Folder + "/LeesResistanceRig.prefab";

    [MenuItem("Senzora/LEES Mücadelesi/Test Sahnesini Aç")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
        EditorSceneManager.OpenScene(ScenePath);
    }
    // Creates ONLY new workshop assets. Running it again preserves the edited scene and rig.
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("LEES atölyesi kurulumu için önce Play modundan çık.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "LeesResistanceWorkshop");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.025f, .03f, .035f);
            var environment = new GameObject("Workshop / Editable Corridor").transform;
            var wall = Material("Concrete", new Color(.26f, .3f, .29f));
            var dark = Material("Dark Metal", new Color(.045f, .065f, .067f));
            var floor = Material("Floor", new Color(.11f, .14f, .14f));
            var stripe = Material("Warning Paint", new Color(.42f, .3f, .11f));
            var glow = Material("Cold Light", new Color(.44f, .69f, .67f), true);
            Cube(environment, "Floor", new Vector3(0, -.12f, 0), new Vector3(5, .24f, 25), floor);
            Cube(environment, "Ceiling", new Vector3(0, 3.55f, 0), new Vector3(5, .2f, 25), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(environment, "Concrete Wall", new Vector3(side * 2.5f, 1.7f, 0), new Vector3(.2f, 3.5f, 25), wall);
                Cube(environment, "Base Rail", new Vector3(side * 2.37f, .22f, 0), new Vector3(.12f, .18f, 25), dark);
                Cube(environment, "Wall Safety Line", new Vector3(side * 2.39f, 1.02f, 0), new Vector3(.03f, .055f, 25), stripe);
                for (int z = -10; z <= 10; z += 4)
                {
                    Cube(environment, "Support", new Vector3(side * 2.35f, 1.7f, z), new Vector3(.18f, 3.4f, .18f), dark);
                    Cube(environment, "Utility Panel", new Vector3(side * 2.36f, 1.65f, z + 1.7f), new Vector3(.06f, .65f, 1.1f), dark);
                    Cube(environment, "Ceiling Beam", new Vector3(0, 3.38f, z), new Vector3(4.8f, .2f, .16f), dark);
                }
            }
            for (int z = -10; z <= 10; z += 5)
            {
                Cube(environment, "Fluorescent", new Vector3(0, 3.37f, z), new Vector3(1.6f, .05f, .18f), glow);
                PointLight(environment, "Ceiling Light", new Vector3(0, 2.95f, z), new Color(.58f, .78f, .76f), 550, 6);
                Cube(environment, "Floor Seam", new Vector3(0, .007f, z), new Vector3(4.7f, .01f, .018f), dark);
            }
            for (int z = -12; z <= 12; z += 24)
            {
                Cube(environment, "End Wall", new Vector3(0, 1.7f, z), new Vector3(5, 3.5f, .2f), wall);
                Cube(environment, "Sealed Door", new Vector3(0, 1.4f, z - Mathf.Sign(z) * .15f), new Vector3(1.7f, 2.8f, .12f), dark);
            }
            PointLight(environment, "LEES Rim", new Vector3(.5f, 1.7f, 9.4f), new Color(.8f, .13f, .08f), 380, 5);
            var volume = new GameObject("Workshop Exposure").AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100;
            volume.sharedProfile = WorkshopProfile();

            var rig = CreateRig(); rig.transform.position = Vector3.zero;
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyDummies/Lees_Dummy.prefab"), scene);
            actor.name = "LEES / Visual Only"; actor.transform.SetPositionAndRotation(new Vector3(0, 0, 7.2f), Quaternion.Euler(0, 180, 0));
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            // The dummy contains an Animator and presentation only; keep all enemy logic out of this scene.
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            var bounds = new Bounds(actor.transform.position, Vector3.zero); bool first = true;
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>()) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
            if (bounds.size.y > .01f)
            {
                float factor = 2.15f / bounds.size.y;
                actor.transform.localScale *= factor;
                actor.transform.position -= Vector3.up * bounds.min.y * factor;
            }
            var demo = rig.AddComponent<LeesResistanceDemo>();
            demo.playerRoot = rig.transform; demo.leesVisual = actor.transform;
            demo.resistance = rig.GetComponent<LeesResistanceController>(); demo.viewCamera = demo.resistance.viewCamera;
            demo.resistance.keepResultVisible = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Folder }))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
            Debug.Log("LEES WORKSHOP CREATED: " + ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    static GameObject CreateRig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        if (existing != null) return (GameObject)PrefabUtility.InstantiatePrefab(existing);
        var rig = new GameObject("LEES Resistance Rig");
        var control = rig.AddComponent<LeesResistanceController>();
        var cameraObject = new GameObject("Resistance Camera", typeof(Camera), typeof(AudioListener), typeof(HDAdditionalCameraData));
        cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 1.65f, 0);
        var camera = cameraObject.GetComponent<Camera>(); camera.fieldOfView = 68; camera.nearClipPlane = .05f; camera.farClipPlane = 60;
        camera.GetComponent<HDAdditionalCameraData>().clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        camera.GetComponent<HDAdditionalCameraData>().backgroundColorHDR = new Color(.008f, .012f, .014f);
        control.viewCamera = camera; control.headPivot = camera.transform;
        control.heartbeatAudio = Audio(rig.transform, "Heartbeat", "Assets/Sounds/Sound FX 2/HeartbeatFX_Pressure.mp3");
        var canvasObject = new GameObject("Resistance HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(rig.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = .5f; canvas.sortingOrder = 200;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var hud = canvasObject.AddComponent<LeesResistanceHud>(); control.hud = hud;
        var graphic = new GameObject("Peripheral Pressure", typeof(RectTransform), typeof(CanvasRenderer), typeof(LeesResistanceGraphic)).GetComponent<LeesResistanceGraphic>();
        graphic.transform.SetParent(canvasObject.transform, false); Stretch(graphic.rectTransform); graphic.raycastTarget = false; hud.graphic = graphic;
        var font = Font();
        Text(canvas.transform, font, "Identity", "L E E S   /   0 1", .04f, .93f, .27f, .05f, 23, TextAlignmentOptions.Left);
        Text(canvas.transform, font, "Workshop Label", "D İ R E N Ç   Ç A L I Ş M A S I", .67f, .93f, .29f, .05f, 15, TextAlignmentOptions.Right);
        hud.title = Text(canvas.transform, font, "Action", "ARKANA BAKMA", .24f, .33f, .52f, .07f, 36);
        hud.detail = Text(canvas.transform, font, "Instruction", "", .15f, .235f, .7f, .085f, 19);
        hud.footer = Text(canvas.transform, font, "Controls", "", .14f, .025f, .72f, .04f, 14);
        hud.pressureLabel = Text(canvas.transform, font, "Pressure", "", .34f, .112f, .2f, .04f, 14, TextAlignmentOptions.Left);
        hud.progressLabel = Text(canvas.transform, font, "Survival", "", .51f, .112f, .15f, .04f, 14, TextAlignmentOptions.Right);
        PrefabUtility.SaveAsPrefabAsset(rig, RigPath); return rig;
    }
    static VolumeProfile WorkshopProfile()
    {
        string path = Folder + "/WorkshopExposure.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
        profile.components.RemoveAll(component => component == null);
        if (!profile.TryGet<Exposure>(out var exposure))
        {
            exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(1);
        }
        if (!profile.TryGet<Tonemapping>(out var tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(); tonemapping.mode.Override(TonemappingMode.ACES);
        }
        // The project's default HDRI has daylight-level ambient exposure. This indoor
        // workshop owns its environment, so inherit neither that sky nor global fog.
        if (!profile.TryGet<VisualEnvironment>(out var environment))
        {
            environment = profile.Add<VisualEnvironment>(); environment.skyType.Override(0);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        }
        if (!profile.TryGet<Fog>(out var fog)) { fog = profile.Add<Fog>(); fog.enabled.Override(false); }
        foreach (var component in profile.components)
            if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); return profile;
    }
    static TMP_FontAsset Font()
    {
        string path = Folder + "/Workshop Sans.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Sprites/UI/MainMenu/Font/static/DMSans_36pt-Medium.ttf");
        var asset = TMP_FontAsset.CreateFontAsset(source, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024);
        asset.name = "Workshop Sans";
        string characters = ""; for (int i = 32; i < 127; i++) characters += (char)i;
        characters += "ÇĞİÖŞÜçğıöşü…"; asset.TryAddCharacters(characters);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset, path); AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures) { texture.name = "Workshop Sans Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
        EditorUtility.SetDirty(asset); return asset;
    }
    static TMP_Text Text(Transform parent, TMP_FontAsset font, string name, string value, float x, float y, float w, float h, int size, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.rectTransform.anchorMin = new Vector2(x, y); text.rectTransform.anchorMax = new Vector2(x + w, y + h);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = font; text.fontSize = size; text.alignment = alignment; text.text = value;
        text.color = new Color(.79f, .81f, .76f); text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; return text;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static AudioSource Audio(Transform parent, string name, string path)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var audio = go.AddComponent<AudioSource>(); audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        audio.playOnAwake = false; audio.spatialBlend = 0; return audio;
    }
    static Material Material(string name, Color color, bool emissive = false)
    {
        string path = Folder + "/" + name + ".mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(Shader.Find(emissive ? "HDRP/Unlit" : "HDRP/Lit")); material.name = name;
        material.SetColor(emissive ? "_UnlitColor" : "_BaseColor", color);
        if (emissive) { material.SetColor("_EmissiveColor", color * 3); material.EnableKeyword("_EMISSIVE_COLOR_MAP"); }
        else { material.SetFloat("_Smoothness", .22f); material.SetFloat("_Metallic", name == "Dark Metal" ? .45f : 0); }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
    }
    static void PointLight(Transform parent, string name, Vector3 position, Color color, float lumens, float range)
    {
        var go = new GameObject(name, typeof(Light), typeof(HDAdditionalLightData)); go.transform.SetParent(parent, false); go.transform.position = position;
        var light = go.GetComponent<Light>(); light.type = LightType.Point; light.color = color;
        light.lightUnit = LightUnit.Lumen; light.intensity = lumens; light.range = range; light.shadows = LightShadows.Soft;
    }

    [MenuItem("Senzora/LEES Mücadelesi/Sahneyi Oluştur ve Doğrula")]
    public static void BuildAndCheck()
    {
        Build(); WorkshopProfile(); LeesResistanceWorkshopChecks.Run();
    }

    [MenuItem("Senzora/LEES Mücadelesi/Sert Çekiş Ayarını Uygula")]
    public static void ApplyHardPressure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Önce Play modundan çık.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var control in root.GetComponentsInChildren<LeesResistanceController>(true))
                {
                    Undo.RecordObject(control, "LEES Sert Çekiş"); SetHardPressure(control);
                    EditorUtility.SetDirty(control);
                }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        var prefab = PrefabUtility.LoadPrefabContents(RigPath);
        try { SetHardPressure(prefab.GetComponent<LeesResistanceController>()); PrefabUtility.SaveAsPrefabAsset(prefab, RigPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        Debug.Log("LEES HARD PRESSURE APPLIED: pull 54, mouse .075, limit 52, warning .25; scene + prefab saved.");
    }
    static void SetHardPressure(LeesResistanceController control)
    {
        var defaults = new LeesResistanceModel.Tuning();
        control.tuning.pullDegreesPerSecond = defaults.pullDegreesPerSecond;
        control.tuning.mouseDegreesPerPixel = defaults.mouseDegreesPerPixel;
        control.tuning.failureAngle = defaults.failureAngle;
        control.tuning.directionWarningSeconds = defaults.directionWarningSeconds;
        control.motionIntensity = .9f;
    }
}
