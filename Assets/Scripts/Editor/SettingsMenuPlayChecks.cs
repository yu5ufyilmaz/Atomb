using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class SettingsMenuPlayChecks
{
    const string Pending="Senzora.Settings.PlayChecks", Restore="Senzora.Settings.RestoreScenes";
    [Serializable] class SceneState { public SceneInfo[] scenes; }
    [Serializable] class SceneInfo { public string path;public bool loaded,active; }
    static SettingsMenuPlayChecks(){EditorApplication.playModeStateChanged+=StateChanged;}
    [MenuItem("Senzora/Doğrulama/Sekmeli Ayarlar Gerçek Oyun")]
    public static void Start()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Start settings checks outside Play mode.");
        var setup=EditorSceneManager.GetSceneManagerSetup();foreach(var info in setup)if(SceneManager.GetSceneByPath(info.path).isDirty)throw new InvalidOperationException("Save open scenes before settings checks.");
        SessionState.SetString(Restore,JsonUtility.ToJson(new SceneState{scenes=setup.Select(s=>new SceneInfo{path=s.path,loaded=s.isLoaded,active=s.isActive}).ToArray()}));
        SessionState.SetBool(Pending,true);EditorSceneManager.OpenScene("Assets/Scenes/scn_MainMenu.unity");EditorApplication.isPlaying=true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))EditorApplication.delayCall+=()=>SettingsManager.EnsureInstance().StartCoroutine(Watch());
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending,false))
        {
            SessionState.SetBool(Pending,false);var saved=JsonUtility.FromJson<SceneState>(SessionState.GetString(Restore,"{}"));
            if(saved?.scenes!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(s=>new SceneSetup{path=s.path,isLoaded=s.loaded,isActive=s.active}).ToArray());
        }
    }
    static IEnumerator Watch()
    {
        var stack=new Stack<IEnumerator>();stack.Push(Checks());string failure=null;
        while(stack.Count>0)
        {
            var iterator=stack.Peek();bool next=false;try{next=iterator.MoveNext();}catch(Exception error){failure=error.ToString();}
            if(failure!=null)break;if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
            if(iterator.Current is IEnumerator child)stack.Push(child);else yield return iterator.Current;
        }
        while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
        File.WriteAllText("Temp/settings-menu-play.result",failure==null?"PASS "+DateTime.Now.ToString("s"):"FAIL "+failure);
        if(failure==null)Debug.Log("SETTINGS PLAY PASS: main/pause menus, screen transactions, real rebind, mix/volume/subtitle consumers.");else Debug.LogError(failure);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    static void Require(bool value,string message){if(!value)throw new Exception("SETTINGS PLAY: "+message);}
    static void Click(SettingsMenuView view,string name)=>view.transform.Find("SettingsStage/"+name).GetComponent<Button>().onClick.Invoke();
    static FieldInfo Field(string name)=>typeof(SettingsManager).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
    static IEnumerator Checks()
    {
        yield return null;yield return null;
        var manager=SettingsManager.EnsureInstance();var original=manager.currentSettings.Copy();string originalPath=(string)Field("saveFilePath").GetValue(manager);
        const string prefs="Senzora.InputOverrides.v1";bool had=PlayerPrefs.HasKey(prefs);string saved=PlayerPrefs.GetString(prefs);
        float timeScale=Time.timeScale;bool audioPause=AudioListener.pause;string language=GameLocalization.CurrentCode;
        var keyboard=InputSystem.AddDevice<Keyboard>();var inputSettings=InputSystem.settings;var editorBehavior=inputSettings.editorInputBehaviorInPlayMode;inputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Field("saveFilePath").SetValue(manager,"Temp/SettingsMenu/play-settings.json");
        try
        {
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();Require(menu!=null,"Real main menu missing.");menu.OpenSettings();yield return null;
            var view=SettingsMenuView.Open;Require(view!=null&&GameInput.GameplayBlocked,"Main menu did not open shared settings or block gameplay.");
            view.ShowSection("audio");yield return null;Canvas.ForceUpdateCanvases();
            var slider=view.Rows.GetComponentInChildren<Slider>();var sliderRect=(RectTransform)slider.transform;var canvas=slider.GetComponentInParent<Canvas>();
            var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,sliderRect.TransformPoint(new Vector3(sliderRect.rect.xMin+sliderRect.rect.width*.25f,sliderRect.rect.center.y,0)));
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Slider>()==slider,"Slider track misses pointer raycasts away from narrow handle. Point="+point+", rect="+sliderRect.rect+", canvas="+canvas.name+"/"+canvas.renderMode+", graphic="+slider.GetComponent<Image>().depth+"/cull="+slider.GetComponent<Image>().canvasRenderer.cull+", hits="+string.Join(", ",hits.Select(h=>h.gameObject.name+"/"+h.module.name)));
            pointer.pointerPressRaycast=hits[0];ExecuteEvents.Execute(slider.gameObject,pointer,ExecuteEvents.pointerDownHandler);Require(manager.currentSettings.masterVolume>.15f&&manager.currentSettings.masterVolume<.35f,"Clicking slider track did not change volume.");
            manager.SetMasterVolume(original.masterVolume);
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                manager.currentSettings.gamepadDeadzone=.4f;manager.ChangedAndSave();
                var lookAction=GameInput.Action("Look");var moveAction=GameInput.Action("Move");yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(.5f,0),rightStick=new Vector2(1,0)});yield return null;yield return null;
                Require(Math.Abs(lookAction.ReadValue<Vector2>().x-1)<.01f,"Processed stick look has a second rate multiplier: "+lookAction.ReadValue<Vector2>());
                float move=GameInput.ReadVectorInput(moveAction,moveAction.ReadValue<Vector2>(),false).x;Require(move>.15f&&move<.25f,"Configured deadzone is applied twice: "+move);
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightShoulder));yield return null;yield return null;
                Require(view.Section=="video","Gamepad shoulder did not switch settings tab.");InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            }
            finally{InputSystem.RemoveDevice(pad);}
            SettingsMenuChecks.ValidateView(view);
            GameLocalization.SetLanguage("tr");view.ShowSection("video");Time.timeScale=0;yield return null;
            var dropdown=view.Rows.GetComponentsInChildren<TMPro.TMP_Dropdown>().First(d=>d.interactable);dropdown.Show();yield return null;
            Require(dropdown.IsExpanded,"Video dropdown could not open.");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.25f);Require(SettingsMenuView.Open==view&&!dropdown.IsExpanded,"Escape from dropdown also closed settings.");
            var display=manager.currentSettings.display.Copy();var next=display.Copy();next.mode=(int)FullScreenMode.Windowed;
            manager.BeginDisplayTest(next);Require(manager.DisplayTestRemaining>14&&manager.DisplayTestRemaining<=15,"Screen timeout is not 15 real seconds.");
            manager.currentSettings.musicVolume=.42f;manager.SaveSettings();var stored=SettingsData.Parse(File.ReadAllText("Temp/SettingsMenu/play-settings.json"),Screen.resolutions);
            Require(stored.display.SameAs(display),"Trial display leaked into unrelated settings save.");
            Field("displayDeadline").SetValue(manager,Time.realtimeSinceStartupAsDouble+.1);yield return new WaitForSecondsRealtime(.2f);
            Require(!manager.IsTestingDisplay&&manager.currentSettings.display.SameAs(display),"Paused screen timeout did not revert.");
            Click(view,"Apply");Require(manager.IsTestingDisplay&&view.HasModal,"Apply did not show screen confirmation.");Click(view,"Modal/Action_0");Require(!manager.IsTestingDisplay&&!view.HasModal,"Keep failed.");
            Click(view,"Apply");Click(view,"Modal/Action_1");Require(!manager.IsTestingDisplay,"Explicit revert failed.");
            view.ShowSection("controls");yield return null;var controlScroll=view.Rows.GetComponentInParent<ScrollRect>();Require(controlScroll.verticalScrollbar.size>0&&controlScroll.verticalScrollbar.size<1,"Long control list has no usable scroll indicator.");
            var policy=GameInput.BindingPolicies.First(p=>p.action=="Cancel"&&GameInput.BindingPath(p.id).StartsWith("<Keyboard>"));
            view.BeginCapture(policy.id);yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G));yield return null;yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            if(view.HasModal)Click(view,"Modal/Action_0");
            Require(GameInput.BindingPath(policy.id).ToLowerInvariant().EndsWith("/g"),"Real interactive G rebinding failed.");
            Require(GameLocalization.Get("subtitle.thought_book_0.0", "{input:Cancel}").Contains(GameInput.BindingName("Cancel"))||GameInput.ResolveHints("{input:Cancel}")=="G","Live binding hint not refreshed.");
            string binding=GameInput.BindingPath(policy.id);view.BeginCapture(policy.id);yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Require(GameInput.BindingPath(policy.id)==binding&&!view.HasModal&&SettingsMenuView.Open==view,"Escape changed binding or closed the settings menu during capture.");
            view.BeginCapture(policy.id);yield return new WaitForSecondsRealtime(10.6f);Require(GameInput.BindingPath(policy.id)==binding&&!view.HasModal,"Capture timeout changed binding or kept modal open.");
            var conflict=GameInput.BindingPolicies.First(p=>p.action=="Jump"&&GameInput.BindingPath(p.id)=="<Keyboard>/space");
            view.BeginCapture(policy.id);yield return new WaitForSecondsRealtime(.3f);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return new WaitForSecondsRealtime(.25f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Require(view.HasModal,"Shared-context binding conflict was not shown.");
            foreach(string code in new[]{"en","tr"}){GameLocalization.SetLanguage(code);string body=view.transform.Find("SettingsStage/Modal/DialogBody").GetComponent<TMPro.TMP_Text>().text;Require(body.Contains(code=="tr"?policy.textTR:policy.textEN)&&body.Contains(code=="tr"?conflict.textTR:conflict.textEN),"Conflict lost action names after language change.");}
            Click(view,"Modal/Action_1");Require(GameInput.BindingPath(policy.id)==binding,"Canceling conflict changed original binding.");
            var last=view.Rows.GetComponentsInChildren<Selectable>().Last(s=>s.IsInteractable());EventSystem.current.SetSelectedGameObject(last.gameObject);Canvas.ForceUpdateCanvases();
            var viewport=view.Rows.GetComponentInParent<ScrollRect>().viewport;var focusBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,last.transform);Require(focusBounds.min.y>=viewport.rect.yMin-1&&focusBounds.max.y<=viewport.rect.yMax+1,"Focused control was not scrolled into view.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G));yield return null;Require(!GameInput.Pressed("Cancel"),"Gameplay input leaked through settings.");InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            view.RequestClose(menu.CloseSettings);yield return null;Require(SettingsMenuView.Open==null&&!GameInput.GameplayBlocked,"Main menu settings could not close.");
            Require(EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.activeInHierarchy,"Main menu focus was not restored after settings.");
            Time.timeScale=1;yield return SceneManager.LoadSceneAsync("scn_labratory");yield return null;yield return null;
            if(SaveManager.Instance!=null)SaveManager.Instance.sessionOnly=true;
            var pause=UnityEngine.Object.FindFirstObjectByType<PauseManager>();Require(pause!=null,"Real pause menu missing.");
            if(GameManager.Instance!=null)GameManager.Instance.isGameStarted=true;pause.PauseGame();pause.OpenSettings();yield return null;
            view=SettingsMenuView.Open;Require(view!=null&&Time.timeScale==0&&AudioListener.pause,"Pause settings resumed the world.");
            manager.currentSettings.voiceVolume=.25f;manager.currentSettings.brightness=-1;manager.currentSettings.motionBlur=false;manager.currentSettings.filmGrain=false;manager.currentSettings.vSync=false;manager.currentSettings.frameRateLimit=60;manager.ChangedAndSave();
            Require(manager.mainAudioMixer.GetFloat("Voice",out float voice)&&Math.Abs(voice+12.0412f)<.1f,"Real Voice mixer does not respond.");
            manager.SetMasterVolume(.5f);manager.SetMusicVolume(.25f);manager.SetSFXVolume(.125f);
            Require(manager.mainAudioMixer.GetFloat("Master",out float master)&&Math.Abs(master+6.0206f)<.1f&&manager.mainAudioMixer.GetFloat("Music",out float music)&&Math.Abs(music+12.0412f)<.1f&&manager.mainAudioMixer.GetFloat("SFX",out float sfx)&&Math.Abs(sfx+18.0618f)<.1f,"Independent Master/Music/SFX mixer channels do not respond.");
            manager.SetMasterVolume(0);Require(AudioListener.volume==0,"Zero master volume does not mute audio.");manager.SetMasterVolume(original.masterVolume);
            var volume=manager.GetComponentInChildren<Volume>();Require(volume!=null&&volume.sharedProfile.TryGet<ColorAdjustments>(out var color)&&color.postExposure.value==-1,"Runtime exposure not applied.");
            Require(volume.sharedProfile.TryGet<MotionBlur>(out var blur)&&blur.active&&blur.intensity.value==0,"Motion blur preference not applied.");
            manager.SetQuality(0);Require(QualitySettings.vSyncCount==0&&Application.targetFrameRate==60,"Quality change lost FPS/vSync preferences.");
            var inputs=UnityEngine.Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();var update=typeof(PauseManager).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            inputs.pause=true;update.Invoke(pause,null);view.RequestClose(pause.CloseSettings);Require(Time.timeScale==0,"Pause update before closing settings resumed world.");
            Require(EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.activeInHierarchy,"Pause menu focus was not restored.");
            pause.OpenSettings();yield return null;view=SettingsMenuView.Open;view.RequestClose(pause.CloseSettings);inputs.pause=true;update.Invoke(pause,null);Require(Time.timeScale==0&&!inputs.pause,"Late Escape resumed world in the settings closing frame.");
            pause.OpenSettings();yield return null;view=SettingsMenuView.Open;
            var subtitles=GlobalSubtitleManager.Instance;Require(subtitles!=null,"Actual subtitle manager missing.");
            var database=subtitles.database;var fixture=ScriptableObject.CreateInstance<SubtitleData>();fixture.entries.Add(new SubtitleEntry{id="settings_fixture",segments=new List<SubtitleSegment>{new SubtitleSegment{duration=.2f,textTR="İlk satır",textEN="First line"},new SubtitleSegment{duration=1,textTR="İkinci satır",textEN="Second line"}}});
            try
            {
                subtitles.database=fixture;manager.currentSettings.subtitlesEnabled=false;manager.ChangedAndSave();subtitles.Show("settings_fixture");yield return null;Require(!subtitles.subtitlePanel.activeSelf,"Optional subtitle remained visible when disabled.");
                subtitles.Show("settings_fixture",required:true);yield return null;Require(subtitles.subtitlePanel.activeSelf,"Required unvoiced tutorial was hidden.");
                subtitles.HideSubtitle();manager.currentSettings.subtitlesEnabled=true;manager.currentSettings.subtitleSize=1;manager.ChangedAndSave();subtitles.Show("settings_fixture");yield return null;float normal=subtitles.subtitleText.fontSize;
                manager.currentSettings.subtitleSize=2;manager.currentSettings.subtitleBackground=.8f;manager.ChangedAndSave();Require(Mathf.Approximately(subtitles.subtitleText.fontSize,normal*1.25f),"Live subtitle scale incorrect.");
                view.RequestClose(pause.CloseSettings);yield return null;Require(Time.timeScale==0&&AudioListener.pause&&SettingsMenuView.Open==null,"Back from settings bypassed pause.");pause.ResumeGame();yield return new WaitForSeconds(.3f);
                Require(subtitles.subtitleText.text.Contains("İkinci")||subtitles.subtitleText.text.Contains("Second"),"Subtitle sequence lost its position after pause/settings.");
            }
            finally{subtitles.HideSubtitle();subtitles.database=database;UnityEngine.Object.Destroy(fixture);}
        }
        finally
        {
            if(SettingsMenuView.Open!=null)SettingsMenuView.Open.gameObject.SetActive(false);
            manager.RevertDisplayTest();manager.SaveSettings();manager.currentSettings=original;manager.ApplyAllSettings();Field("saveFilePath").SetValue(manager,originalPath);
            GameLocalization.SetLanguage(language);if(had)PlayerPrefs.SetString(prefs,saved);else PlayerPrefs.DeleteKey(prefs);PlayerPrefs.Save();GameInput.ReloadBindings();
            inputSettings.editorInputBehaviorInPlayMode=editorBehavior;InputSystem.RemoveDevice(keyboard);Time.timeScale=timeScale;AudioListener.pause=audioPause;
        }
    }
}
