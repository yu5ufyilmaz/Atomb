using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.Rendering;

public static class SettingsMenuChecks
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception("SETTINGS CHECK: " + message); }
    [MenuItem("Senzora/Doğrulama/Sekmeli Ayarlar")]
    public static void Run()
    {
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var fixtureScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(fixtureScene);
        try { GameLocalization.ReloadLanguages(); DataChecks(); StorageChecks(); PresentationChecks(); LayoutChecks(); File.WriteAllText("Temp/settings-menu.result", "PASS " + DateTime.Now.ToString("s")); Debug.Log("SETTINGS DATA AND UI PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/settings-menu.result", "FAIL " + e); Debug.LogException(e); }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(fixtureScene,true); if(originalScene.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene); }
    }
    static void DataChecks()
    {
        // Production failure: replacing default-overwrite parsing with raw FromJson loses new field defaults.
        var parse = typeof(SettingsData).GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        Require(parse != null, "SettingsData.Parse must migrate old saves with explicit defaults.");
        var data = (SettingsData)parse.Invoke(null, new object[] { "{\"musicVolume\":0.35,\"mouseSensitivity\":2,\"isFullscreen\":false}", Array.Empty<Resolution>() });
        Require(Mathf.Approximately(data.musicVolume, .35f) && Mathf.Approximately(data.mouseSensitivity, 2), "Old user preferences lost.");
        Require((float)typeof(SettingsData).GetField("gamepadSensitivity").GetValue(data) == 1f, "Missing gamepad preference must default to 1.");
        Require((bool)typeof(SettingsData).GetField("motionBlur").GetValue(data), "Old saves must retain authored motion blur default.");
        Require((bool)typeof(SettingsData).GetField("depthOfField").GetValue(data), "Old saves must retain authored depth of field default.");
        data.masterVolume = -2; data.brightness = float.NaN; data.mouseSensitivity = float.PositiveInfinity;
        typeof(SettingsData).GetMethod("Normalize").Invoke(data, new object[] { 3 });
        Require(data.masterVolume == 0 && data.brightness == 0 && data.mouseSensitivity == 1, "Invalid values not normalized.");
        bool rejected = false;
        try { parse.Invoke(null, new object[] { "broken json", Array.Empty<Resolution>() }); } catch (TargetInvocationException) { rejected = true; }
        Require(rejected, "Corrupt settings must be detected, not silently treated as a valid record.");
    }
    static void StorageChecks()
    {
        var original = SettingsManager.Instance; var go = new GameObject("Settings transaction fixture");
        string path = "Temp/settings-storage-fixture.json";
        try
        {
            var manager = go.AddComponent<SettingsManager>(); SettingsManager.Instance = manager;
            manager.currentSettings = new SettingsData(); manager.currentSettings.Normalize(3);
            typeof(SettingsManager).GetField("saveFilePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, path);
            manager.SaveSettings(); var confirmed = manager.currentSettings.display.Copy();
            var trial = confirmed.Copy(); trial.mode = (int)FullScreenMode.Windowed;
            manager.BeginDisplayTest(trial); manager.currentSettings.musicVolume = .42f; manager.SaveSettings();
            var saved = SettingsData.Parse(File.ReadAllText(path), Array.Empty<Resolution>());
            Require(saved.display.SameAs(confirmed) && Mathf.Approximately(saved.musicVolume, .42f), "Saving another preference persisted the unconfirmed screen mode.");
            manager.ConfirmDisplayTest(); saved = SettingsData.Parse(File.ReadAllText(path), Array.Empty<Resolution>());
            Require(saved.display.mode == (int)FullScreenMode.Windowed && !manager.IsTestingDisplay, "Confirmed screen mode wasn't saved.");
            typeof(SettingsManager).GetField("saveFilePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, "Temp/missing-settings-dir/settings.json");
            manager.SaveSettings(); Require(!string.IsNullOrEmpty(manager.LastSaveError), "Save failure wasn't surfaced.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); SettingsManager.Instance = original; foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
    }
    static void PresentationChecks()
    {
        var method = typeof(GlobalSubtitleManager).GetMethod("ApplyPresentation");
        Require(method != null, "Subtitle settings must update the actual rendered subtitle, not only saved data.");
        var oldManager = SettingsManager.Instance; var go = new GameObject("Subtitle preference fixture");
        var text = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); text.transform.SetParent(go.transform);
        try
        {
            var manager = go.AddComponent<SettingsManager>(); SettingsManager.Instance = manager; manager.currentSettings = new SettingsData { subtitleSize = 2, subtitleBackground = .75f };
            var subtitle = go.AddComponent<GlobalSubtitleManager>(); subtitle.subtitlePanel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); subtitle.subtitlePanel.transform.SetParent(go.transform);
            subtitle.subtitleText = text; text.fontSize = 40; subtitle.ApplyPresentation();
            Require(text.fontSize == 50 && Mathf.Approximately(subtitle.subtitlePanel.GetComponent<Image>().color.a,.75f), "Actual subtitle size/background ignored preferences.");
            manager.currentSettings.subtitleSize = 0; subtitle.ApplyPresentation(); Require(text.fontSize == 34, "Repeated presentation application compounds the font size.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); SettingsManager.Instance = oldManager; }
    }
    public static void ValidateView(SettingsMenuView view)
    {
        var style = view.style; Require(style != null, "Settings style not assigned.");
        Require(style.barInside != null && style.barOutside != null && style.box != null && style.check != null && style.arrow != null && style.qualityFrame != null, "Existing settings artwork was not loaded as sprites.");
        foreach (string section in new[] { "audio", "video", "controls", "language" })
        {
            view.ShowSection(section); Canvas.ForceUpdateCanvases();
            foreach(var text in view.Rows.GetComponentsInChildren<TMP_Text>())Require(!text.text.Contains("NaN"), "Invalid display refresh label: "+text.text);
            foreach(var slider in view.Rows.GetComponentsInChildren<Slider>())Require(slider.handleRect.rect.height<=((RectTransform)slider.transform).rect.height, "Slider handle extends into the next row.");
            foreach(var toggle in view.Rows.GetComponentsInChildren<Toggle>())Require(Mathf.Approximately(toggle.graphic.canvasRenderer.GetAlpha(),toggle.isOn?1:0), "Checkbox artwork does not match its value: "+toggle.transform.parent.name);
            int count = 0;
            foreach (Transform row in view.Rows)
            {
                var label = row.Find("SettingLabel")?.GetComponent<TMP_Text>(); if (label == null) continue;
                count++; label.ForceMeshUpdate(true,true);
                Require(label.font == style.font && !label.isTextOverflowing, section + ": setting label overflow/font: " + label.text);
                foreach (var character in label.textInfo.characterInfo) if (character.isVisible) Require(character.fontAsset == label.font, "Turkish glyph switched font: " + label.text);
                var control = row.GetComponentsInChildren<Selectable>().FirstOrDefault(); if (control == null) continue;
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners); float labelRight = row.InverseTransformPoint(corners[2]).x;
                ((RectTransform)control.transform).GetWorldCorners(corners); float controlLeft = row.InverseTransformPoint(corners[0]).x;
                Require(labelRight <= controlLeft - 8, "Setting label overlaps control: " + label.text);
                Require(Mathf.Abs(label.rectTransform.anchoredPosition.x) < .1f, "Setting labels lost shared left edge.");
            }
            Require(count >= (section == "controls" ? 15 : section == "video" ? 10 : 4), "Section has missing controls: " + section);
            var scroll=view.Rows.GetComponentInParent<ScrollRect>();Require(scroll.verticalScrollbar!=null, "Scrollable settings need a visible scrollbar.");
        }
    }
    static void LayoutChecks()
    {
        var prefab = Resources.Load<GameObject>("Settings/SettingsMenu"); Require(prefab != null, "Shared menu prefab not installed.");
        var host = new GameObject("Settings render canvas",typeof(RectTransform),typeof(Canvas)); var camObject = new GameObject("Settings render camera",typeof(Camera));
        var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)host.transform).sizeDelta=new Vector2(1920,1080);
        var camera = camObject.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=540; camera.transform.position=new Vector3(0,0,-1000);camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.07f,.055f);camera.cullingMask=1<<31;
        var cameraData=camObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();cameraData.volumeLayerMask=0;
        cameraData.clearColorMode=UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;cameraData.backgroundColorHDR=camera.backgroundColor;
        string language = GameLocalization.CurrentCode;
        try
        {
            var view = UnityEngine.Object.Instantiate(prefab,host.transform).GetComponent<SettingsMenuView>(); view.EnsureBuilt();
            foreach(string code in new[]{"tr","en"}) { GameLocalization.SetLanguage(code);ValidateView(view); }
            Directory.CreateDirectory("Temp/SettingsMenu/renders");
            var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(2560,1080)};
            foreach(string code in new[]{"tr","en"})
            {
                GameLocalization.SetLanguage(code);
                foreach(var size in sizes)
                {
                    foreach(string section in new[]{"audio","video","controls","language"})
                    {
                        view.ShowSection(section);SaveRender(host,camera,size,section+"-"+code);
                    }
                }
                foreach(string dialog in new[]{"reset","display","unsaved","capture","conflict"})
                {
                    var buttons=dialog=="capture"?new[]{("cancel",(Action)(()=>{}))}:new[]{("apply",(Action)(()=>{})),("cancel",(Action)(()=>{}))};
                    typeof(SettingsMenuView).GetMethod("Dialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,new object[]{dialog+".title",dialog+".body",buttons,(Action)(()=>{})});
                    foreach(var size in sizes)SaveRender(host,camera,size,"dialog-"+dialog+"-"+code);
                    typeof(SettingsMenuView).GetMethod("CloseModal",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(camObject);GameLocalization.SetLanguage(language); }
    }
    static void SaveRender(GameObject host,Camera camera,Vector2Int size,string name)
    {
        ((RectTransform)host.transform).sizeDelta=new Vector2(size.x*1080f/size.y,1080);camera.aspect=(float)size.x/size.y;
        Canvas.ForceUpdateCanvases();
        foreach(var scroll in host.GetComponentsInChildren<ScrollRect>())typeof(ScrollRect).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scroll,null);
        foreach(var label in host.GetComponentsInChildren<TMP_Text>())
        {
            label.ForceMeshUpdate(true,true);Require(!label.isTextOverflowing,"Visible text overflow: "+label.text);
            foreach(var character in label.textInfo.characterInfo)if(character.isVisible)Require(character.fontAsset==label.font,"Visible glyph switched font: "+label.text);
        }
        foreach(var child in host.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
        var target=new RenderTexture(size.x,size.y,24);camera.targetTexture=target;var previous=RenderTexture.active;Texture2D png=null;
        try
        {
            var request=new RenderPipeline.StandardRequest{destination=target};
            if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);else camera.Render();
            RenderTexture.active=target;png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();
            File.WriteAllBytes("Temp/SettingsMenu/renders/"+name+"-"+size.x+"x"+size.y+".png",png.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;if(png!=null)UnityEngine.Object.DestroyImmediate(png);UnityEngine.Object.DestroyImmediate(target);}
    }
}
