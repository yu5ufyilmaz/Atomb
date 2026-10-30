using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class SettingsMenuView : MonoBehaviour
{
    public SettingsMenuStyle style;
    public static SettingsMenuView Open { get; private set; }
    public static int LastClosedFrame { get; private set; } = -1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Open=null;LastClosedFrame=-1;}
    public string Section { get; private set; } = "audio";
    public RectTransform Rows { get; private set; }
    public bool HasModal => modal != null && modal.gameObject.activeSelf;
    SettingsManager manager;
    readonly SettingsData editorData = new SettingsData();
    SettingsData Data => manager != null ? manager.currentSettings : SettingsManager.Current ?? editorData;
    RectTransform stage, modal, viewport;
    ScrollRect scroll;
    TMP_Text title, sectionTitle, status, modalTitle, modalBody, intro, sample;
    Button back, reset, apply;
    readonly List<Button> tabs = new List<Button>();
    readonly List<Action> refresh = new List<Action>();
    readonly List<Action> modalRefresh = new List<Action>();
    readonly List<InputAction> disabledUIActions = new List<InputAction>();
    readonly string[] sections = { "audio", "video", "controls", "language" };
    DisplaySettings draft;
    Action closeAction, modalCancel;
    InputActionRebindingExtensions.RebindingOperation capture;
    InputAction captureAction;
    bool built, gamepad, rebuilding, captureWaiting;
    float captureDeadline;
    float ignoreCancelUntil;
    GameObject returnFocus;
    public static string Text(string key) => GameLocalization.Get("settings." + key, key);
    void Awake() { EnsureBuilt(); }
    void OnEnable()
    {
        if (Application.isPlaying)
        {
            manager = SettingsManager.EnsureInstance(); Open = this; GameInput.GameplayBlocked = true;
            returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            manager.Changed += RefreshValues; manager.SaveFailed += SaveError;
            if (style != null) { manager.mainAudioMixer = style.mixer; manager.voiceGroup = style.voice; manager.effectsGroup = style.effects; manager.ApplyAudioSettings(); }
            foreach (var inputs in FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None)) { inputs.look = inputs.move = Vector2.zero; inputs.pause = inputs.interact = inputs.cancel = inputs.jump = inputs.sprint = false; }
        }
        GameLocalization.LanguageChanged += LanguageChanged; GameInput.BindingsChanged += BindingChanged;
        EnsureBuilt(); draft = Data.display?.Copy() ?? DisplaySettings.Current(); ShowSection(Section);
    }
    void OnDisable()
    {
        CancelCapture(); GameLocalization.LanguageChanged -= LanguageChanged; GameInput.BindingsChanged -= BindingChanged;
        if (manager != null) { manager.Changed -= RefreshValues; manager.SaveFailed -= SaveError; manager.RevertDisplayTest(); manager.SaveSettings(); }
        if (Open == this)
        {
            LastClosedFrame=Time.frameCount;Open = null; GameInput.GameplayBlocked = false;
            foreach(var inputs in FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None))inputs.pause=false;
        }
        if (returnFocus != null && returnFocus.activeInHierarchy && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(returnFocus);
    }
    public void EnsureBuilt()
    {
        if (built) return;
        if (style == null) style = Resources.Load<SettingsMenuStyle>("Settings/SettingsMenuStyle");
        if (style == null) return;
        built = true;
        var shade = style.Image(transform, "Shade", 0, 0, 1920, 1080, null, new Color(.012f,.018f,.014f,.92f)); SettingsMenuStyle.Stretch(shade.rectTransform); shade.raycastTarget = true;
        stage = SettingsMenuStyle.Rect(transform, "SettingsStage", 0, 0, 1920, 1080); stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f,.5f); stage.anchoredPosition = Vector2.zero;
        title = style.Label(stage, "Title", 130, 90, 1500, 90, Text("title"), 64); if (style.headingFont != null) title.font = style.headingFont;
        style.Image(stage, "Divider", 130, 200, 1660, 1, null, new Color(1,1,1,.35f));
        for (int i=0;i<sections.Length;i++) { string id=sections[i]; var b=style.Button(stage,"Tab_"+id,130,250+i*90,320,76,Text(id),()=>ChangeSection(id),29); tabs.Add(b); }
        sectionTitle = style.Label(stage,"SectionTitle",520,250,1100,60,"",38); intro=style.Label(stage,"Description",520,315,1150,55,"",21); intro.color=new Color(.6f,.64f,.58f);
        viewport=SettingsMenuStyle.Rect(stage,"Viewport",520,390,1150,490); var mask=viewport.gameObject.AddComponent<RectMask2D>();
        var target=viewport.gameObject.AddComponent<Image>(); target.color=Color.clear;
        Rows=SettingsMenuStyle.Rect(viewport,"Rows",0,0,1120,490); scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=Rows; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=40;
        var track=style.Image(stage,"ScrollTrack",1660,390,10,490,null,new Color(1,1,1,.08f));track.raycastTarget=true;
        var thumb=style.Image(track.transform,"Thumb",0,0,10,0,null,new Color(.8f,.82f,.74f,.65f));SettingsMenuStyle.Stretch(thumb.rectTransform);thumb.raycastTarget=true;
        var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=thumb.rectTransform;scrollbar.targetGraphic=thumb;scrollbar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        style.Image(stage,"FooterLine",130,945,1660,1,null,new Color(1,1,1,.2f));
        back=style.Button(stage,"Back",130,970,260,65,Text("back"),()=>RequestClose(closeAction));
        reset=style.Button(stage,"Reset",480,970,550,65,Text("reset"),AskReset,24);
        apply=style.Button(stage,"Apply",1280,970,510,65,Text("apply"),ApplyDisplay,26);
        status=style.Label(stage,"Status",130,1030,1650,40,"",20); status.color=new Color(.68f,.75f,.58f);
        modal=SettingsMenuStyle.Rect(stage,"Modal",0,0,1920,1080); var blocker=modal.gameObject.AddComponent<Image>(); blocker.color=new Color(0,0,0,.94f); blocker.raycastTarget=true;
        style.Image(modal,"DialogLine",480,300,960,1,null,new Color(1,1,1,.5f));
        modalTitle=style.Label(modal,"DialogTitle",500,340,920,100,"",42); modalBody=style.Label(modal,"DialogBody",500,460,920,210,"",28);
        modal.gameObject.SetActive(false); ScaleStage();
    }
    void OnRectTransformDimensionsChange() { ScaleStage(); }
    void ScaleStage() { if(stage!=null && transform is RectTransform root) stage.localScale=Vector3.one*Mathf.Min(root.rect.width/1920,root.rect.height/1080); }
    public void SetCloseAction(Action callback) { closeAction=callback; }
    public void RestoreFocusTo(GameObject panel)
    {
        if(EventSystem.current==null)return;
        if(returnFocus!=null&&returnFocus.activeInHierarchy){EventSystem.current.SetSelectedGameObject(returnFocus);return;}
        // Selected durumu Highlighted'ı bastırır; fareyle çıkınca hiçbir buton seçili bırakılmaz, yedek odak yalnızca gamepad içindir.
        if(Gamepad.current?.wasUpdatedThisFrame!=true){EventSystem.current.SetSelectedGameObject(null);return;}
        var first=panel!=null?panel.GetComponentsInChildren<Selectable>().FirstOrDefault(s=>s.IsInteractable()):null;
        if(first!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);
    }
    public void RequestClose(Action callback)
    {
        if(HasModal) { modalCancel?.Invoke(); return; }
        GuardDraft(()=>{ manager?.RevertDisplayTest(); manager?.SaveSettings(); callback?.Invoke(); });
    }
    void ChangeSection(string next) { if(next==Section)return; GuardDraft(()=>ShowSection(next)); }
    void GuardDraft(Action proceed)
    {
        if(Section=="video" && draft!=null && !draft.SameAs(Data.display))
            Dialog("unsaved.title","unsaved.body",new[]{("apply",(Action)(()=>{CloseModal(); ApplyDisplay();})),("discard",(Action)(()=>{draft=Data.display.Copy(); CloseModal(); proceed();})),("cancel",(Action)CloseModal)},CloseModal);
        else proceed();
    }
    public void ShowSection(string next)
    {
        EnsureBuilt(); if(!built)return; Section=next; rebuilding=true;
        for(int i=Rows.childCount-1;i>=0;i--) { var child=Rows.GetChild(i).gameObject; child.SetActive(false); if(Application.isPlaying)Destroy(child);else DestroyImmediate(child); }
        refresh.Clear(); rowY=0; title.text=Text("title"); sectionTitle.text=Text(Section); intro.text=Text(Section+".intro");
        back.GetComponentInChildren<TMP_Text>().text=Text("back"); reset.GetComponentInChildren<TMP_Text>().text=Text("reset"); apply.GetComponentInChildren<TMP_Text>().text=Text("apply"); apply.gameObject.SetActive(Section=="video");
        for(int i=0;i<tabs.Count;i++){tabs[i].GetComponentInChildren<TMP_Text>().text=Text(sections[i]);tabs[i].GetComponent<Image>().color=sections[i]==Section?new Color(1,1,1,.1f):new Color(1,1,1,.015f);}
        switch(Section){case "audio": AudioRows();break;case "video":VideoRows();break;case "controls":ControlRows();break;case "language":LanguageRows();break;}
        Rows.sizeDelta=new Vector2(1120,Mathf.Max(490,rowY)); Rows.anchoredPosition=Vector2.zero; scroll.verticalNormalizedPosition=1; rebuilding=false; RefreshValues();
        WireFocus(); if(Application.isPlaying && !HasModal)SelectFirst();
    }
    float rowY;
    RectTransform Row(string key,string detail=null)
    {
        var rect=SettingsMenuStyle.Rect(Rows,"Row_"+key,0,rowY,1120,82);rowY+=82;
        var label=style.Label(rect,"SettingLabel",0,0,600,detail==null?76:44,Text(key),29);
        if(detail!=null){var sub=style.Label(rect,"Detail",0,40,620,35,Text(detail),20);sub.color=new Color(.6f,.65f,.57f);}
        style.Image(rect,"Line",0,80,1090,1,null,new Color(1,1,1,.06f));return rect;
    }
    void Slider(string key,Func<float> get,Action<float> set,float min,float max,Func<float,string> format)
    {
        var row=Row(key);var output=style.Label(row,"Value",1055,0,65,76,"",23);output.alignment=TextAlignmentOptions.MidlineRight;output.textWrappingMode=TextWrappingModes.NoWrap;
        var slider=style.Slider(row,"Control",get(),min,max,v=>{set(v);output.text=format(v);}); refresh.Add(()=>{slider.SetValueWithoutNotify(get());output.text=format(get());});
    }
    void Toggle(string key,Func<bool> get,Action<bool> set){var control=style.Toggle(Row(key),"Control",get(),v=>{if(!rebuilding&&v!=get())set(v);});refresh.Add(()=>control.SetIsOnWithoutNotify(get()));}
    TMP_Dropdown Dropdown(string key,string[] options,int selected,Action<int> set,string detail=null)=>style.Dropdown(Row(key,detail),"Control",options,selected,set);
    void Change(Action<SettingsData> set){if(manager==null)return;set(Data);manager.ChangedAndSave();}
    void AudioRows()
    {
        Slider("master",()=>Data.masterVolume,v=>manager?.SetMasterVolume(v),0,1,Percent);Slider("music",()=>Data.musicVolume,v=>manager?.SetMusicVolume(v),0,1,Percent);
        Slider("sfx",()=>Data.sfxVolume,v=>manager?.SetSFXVolume(v),0,1,Percent);Slider("voice",()=>Data.voiceVolume,v=>Change(d=>d.voiceVolume=v),0,1,Percent);
    }
    static string Percent(float v)=>Mathf.RoundToInt(v*100)+"%";
    void VideoRows()
    {
        if(draft==null)draft=Data.display?.Copy()??DisplaySettings.Current();
        var modes=new List<FullScreenMode>{FullScreenMode.Windowed,FullScreenMode.FullScreenWindow};if(Application.platform==RuntimePlatform.WindowsPlayer||Application.platform==RuntimePlatform.WindowsEditor)modes.Add(FullScreenMode.ExclusiveFullScreen);
        Dropdown("mode",modes.Select(m=>Text(m==FullScreenMode.Windowed?"windowed":m==FullScreenMode.FullScreenWindow?"borderless":"exclusive")).ToArray(),Math.Max(0,modes.IndexOf((FullScreenMode)draft.mode)),i=>{draft.mode=(int)modes[i];ShowSection("video");});
        var resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Distinct().ToList();if(resolutions.Count==0)resolutions.Add(new Vector2Int(draft.width,draft.height));
        Dropdown("resolution",resolutions.Select(r=>r.x+" × "+r.y).ToArray(),Math.Max(0,resolutions.FindIndex(r=>r.x==draft.width&&r.y==draft.height)),i=>{draft.width=resolutions[i].x;draft.height=resolutions[i].y;ShowSection("video");});
        var rates=Screen.resolutions.Where(r=>r.width==draft.width&&r.height==draft.height).Select(r=>r.refreshRateRatio).Where(r=>r.denominator>0&&r.numerator>0&&r.value>=20&&r.value<=1000).GroupBy(r=>Math.Round(r.value,2)).Select(g=>g.First()).ToList();if(rates.Count==0)rates.Add(draft.RefreshRate);
        var rate=Dropdown("refresh",rates.Select(r=>r.value.ToString("0.##")+" Hz").ToArray(),Math.Max(0,rates.FindIndex(r=>Math.Abs(r.value-draft.Hz)<.1)),i=>{draft.refreshNumerator=rates[i].numerator;draft.refreshDenominator=rates[i].denominator;},draft.mode==(int)FullScreenMode.FullScreenWindow?"desktop.refresh":null);rate.interactable=draft.mode!=(int)FullScreenMode.FullScreenWindow;
        var quality=Dropdown("quality",QualitySettings.names.Select(n=>Text(n=="High Fidelity"?"high":n=="Balanced"?"balanced":n=="Performant"?"performance":n)).ToArray(),Data.qualityIndex,i=>manager?.SetQuality(i));
        Toggle("vsync",()=>Data.vSync,v=>{Change(d=>d.vSync=v);ShowSection("video");});
        int[] limits={30,60,90,120,144,165,240,-1};var fps=Dropdown("fps",limits.Select(i=>i<0?Text("unlimited"):i.ToString()).ToArray(),Array.IndexOf(limits,Data.frameRateLimit),i=>Change(d=>d.frameRateLimit=limits[i]),Data.vSync?"vsync.note":null);fps.interactable=!Data.vSync;
        Slider("brightness",()=>Data.brightness,v=>manager?.SetBrightness(v),-2,2,v=>v.ToString("0.0"));Toggle("blur",()=>Data.motionBlur,v=>Change(d=>d.motionBlur=v));Toggle("grain",()=>Data.filmGrain,v=>Change(d=>d.filmGrain=v));Slider("bob",()=>Data.headBob,v=>Change(d=>d.headBob=v),0,1,Percent);
    }
    void ControlRows()
    {
        Slider("mouse",()=>Data.mouseSensitivity,v=>manager?.SetMouseSensitivity(v),.1f,5,v=>v.ToString("0.0")+"×");Toggle("invert.mouse",()=>Data.invertMouseY,v=>Change(d=>d.invertMouseY=v));
        Slider("pad",()=>Data.gamepadSensitivity,v=>Change(d=>d.gamepadSensitivity=v),.1f,5,v=>v.ToString("0.0")+"×");Toggle("invert.pad",()=>Data.invertGamepadY,v=>Change(d=>d.invertGamepadY=v));Slider("deadzone",()=>Data.gamepadDeadzone,v=>Change(d=>d.gamepadDeadzone=v),.05f,.4f,Percent);
        Dropdown("device",new[]{Text("keyboard"),Text("gamepad")},gamepad?1:0,i=>{gamepad=i==1;ShowSection("controls");});
        if(gamepad){style.Label(Rows,"DeviceStatus",0,rowY,1100,55,Text(Gamepad.current!=null?"pad.connected":"pad.disconnected"),22);rowY+=60;}
        foreach(var policy in GameInput.BindingPolicies)
        {
            var action=GameInput.Action(policy.action);if(action==null)continue;int index=-1;for(int i=0;i<action.bindings.Count;i++)if(action.bindings[i].id.ToString()==policy.id){index=i;break;}if(index<0)continue;
            var binding=action.bindings[index];bool isPad=(binding.path??"").Contains("Gamepad");if(binding.isComposite||gamepad!=isPad)continue;
            string name=GameLocalization.CurrentCode=="tr"?policy.textTR:policy.textEN;if(string.IsNullOrWhiteSpace(name))name=policy.action;
            var row=Row("binding","context."+GameInput.ContextOf(policy));row.name="Binding_"+policy.id;row.Find("SettingLabel").GetComponent<TMP_Text>().text=name+(binding.isPartOfComposite?" · "+Text("part."+binding.name):"");
            var button=style.Button(row,"Binding",650,10,350,58,GameInput.PathDisplayName(binding.effectivePath),()=>BeginCapture(policy.id),24);button.interactable=policy.rebindable;
            if(policy.rebindable)style.Button(row,"BindingReset",1010,10,100,58,Text("reset.short"),()=>{GameInput.ResetBinding(policy.id);ShowSection("controls");},20);
        }
    }
    void LanguageRows()
    {
        var languages=GameLocalization.Languages;Dropdown("language.choice",languages.Select(l=>l.nativeName).ToArray(),Math.Max(0,languages.ToList().FindIndex(l=>l.code==GameLocalization.CurrentCode)),i=>manager?.SetLanguage(languages[i].code));
        Toggle("subtitles",()=>Data.subtitlesEnabled,v=>{manager?.SetSubtitlesEnabled(v);UpdateSample();});Dropdown("subtitle.size",new[]{Text("small"),Text("normal"),Text("large")},Data.subtitleSize,i=>{Change(d=>d.subtitleSize=i);UpdateSample();});
        Slider("subtitle.background",()=>Data.subtitleBackground,v=>{Change(d=>d.subtitleBackground=v);UpdateSample();},0,1,Percent);
        var bg=style.Image(Rows,"SubtitlePreviewBackground",0,rowY+25,1100,150,null,Color.black);sample=style.Label(bg.transform,"Preview",25,10,1050,130,Text("subtitle.sample"),36);sample.alignment=TextAlignmentOptions.Center;
        rowY+=200;style.Label(Rows,"RequiredSubtitleNote",0,rowY,1100,65,Text("subtitle.required"),22);rowY+=75;UpdateSample();
    }
    void UpdateSample(){if(sample==null)return;sample.fontSize=36*(Data.subtitleSize==0?.85f:Data.subtitleSize==2?1.25f:1);sample.color=new Color(.9f,.9f,.86f,Data.subtitlesEnabled?1:.35f);sample.transform.parent.GetComponent<Image>().color=new Color(0,0,0,Data.subtitleBackground);}
    void RefreshValues(){if(!built||rebuilding)return;foreach(var callback in refresh)callback();UpdateSample();}
    void LanguageChanged(){if(!built)return;ShowSection(Section);foreach(var callback in modalRefresh)callback();if(HasModal)SetUnderlyingInteractable(false);}
    void BindingChanged(){if(built&&Section=="controls"&&!captureWaiting)ShowSection(Section);}
    void SaveError(){if(status!=null)status.text=Text("save.error");}
    void AskReset(){Dialog("reset.title",Section=="controls"?"reset.controls":"reset.body",new[]{("reset.short",(Action)(()=>{manager?.ResetSection(Section);draft=Data.display.Copy();CloseModal();ShowSection(Section);})),("cancel",(Action)CloseModal)},CloseModal);}
    void ApplyDisplay(){if(manager==null)return;manager.BeginDisplayTest(draft);Dialog("display.title","display.body",new[]{("keep",(Action)(()=>{manager.ConfirmDisplayTest();draft=Data.display.Copy();CloseModal();})),("revert",(Action)RevertDisplay)},RevertDisplay);}
    void RevertDisplay(){manager?.RevertDisplayTest();draft=Data.display.Copy();CloseModal();ShowSection(Section);}
    void Dialog(string heading,string body,(string,Action)[] actions,Action cancel)
    {
        modalRefresh.Clear();for(int i=modal.childCount-1;i>=0;i--){var child=modal.GetChild(i);if(child.name.StartsWith("Action_")){child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}}
        modalCancel=cancel;modalRefresh.Add(()=>{modalTitle.text=Text(heading);modalBody.text=Text(body);});
        for(int i=0;i<actions.Length;i++){var pair=actions[i];var button=style.Button(modal,"Action_"+i,500+i*310,730,290,70,Text(pair.Item1),pair.Item2,26);modalRefresh.Add(()=>button.GetComponentInChildren<TMP_Text>().text=Text(pair.Item1));}
        foreach(var callback in modalRefresh)callback();modal.gameObject.SetActive(true);if(EventSystem.current!=null){var first=modal.GetComponentsInChildren<Button>().FirstOrDefault();if(first!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);}
        SetUnderlyingInteractable(false);
    }
    void SetUnderlyingInteractable(bool enabled){if(stage==null)return;foreach(var selectable in stage.GetComponentsInChildren<Selectable>(true)){if(selectable.transform.IsChildOf(modal))continue;var group=selectable.GetComponent<CanvasGroup>();if(group==null)group=selectable.gameObject.AddComponent<CanvasGroup>();group.interactable=enabled;group.blocksRaycasts=enabled;}}
    void CloseModal(){if(modal==null)return;modal.gameObject.SetActive(false);modalCancel=null;modalRefresh.Clear();SetUnderlyingInteractable(true);SelectFirst();}
    void Update()
    {
        if(!Application.isPlaying||!built||Open!=this)return;
        if(manager!=null&&manager.IsTestingDisplay&&HasModal)modalBody.text=Text("display.body")+"\n"+Mathf.CeilToInt(manager.DisplayTestRemaining)+" "+Text("seconds");
        else if(manager!=null&&!manager.IsTestingDisplay&&HasModal&&modalTitle.text==Text("display.title")){draft=Data.display.Copy();CloseModal();ShowSection(Section);}
        if(captureWaiting&&HasModal)modalBody.text=Text("capture.body")+"\n"+Mathf.Max(0,Mathf.CeilToInt(captureDeadline-Time.unscaledTime))+" "+Text("seconds");
        if(captureWaiting||Time.unscaledTime<ignoreCancelUntil)return;
        bool cancel=Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true;
        if(cancel)
        {
            var dropdown=stage.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(d=>d.IsExpanded);
            if(dropdown!=null){dropdown.Hide();ignoreCancelUntil=Time.unscaledTime+.2f;return;}
            if(HasModal)modalCancel?.Invoke();else RequestClose(closeAction);
        }
        if(!HasModal&&Gamepad.current!=null){if(Gamepad.current.leftShoulder.wasPressedThisFrame)ChangeSection(sections[(Array.IndexOf(sections,Section)+3)%4]);if(Gamepad.current.rightShoulder.wasPressedThisFrame)ChangeSection(sections[(Array.IndexOf(sections,Section)+1)%4]);}
    }
    public void BeginCapture(string id){if(!Application.isPlaying)return;StartCoroutine(CaptureAfterClick(id));}
    IEnumerator CaptureAfterClick(string id)
    {
        captureWaiting=true;captureDeadline=Time.unscaledTime+10.2f;Dialog("capture.title","capture.body",new[]{("cancel",(Action)CancelCapture)},CancelCapture);
        yield return new WaitForSecondsRealtime(.2f);if(!captureWaiting)yield break;
        var policy=GameInput.BindingPolicies.First(p=>p.id==id);var source=GameInput.Action(policy.action);int index=source.bindings.ToList().FindIndex(b=>b.id.ToString()==id);var binding=source.bindings[index];
        captureAction=source.Clone();capture=captureAction.PerformInteractiveRebinding(index).WithCancelingThrough("<Keyboard>/escape").WithTimeout(10).OnMatchWaitForAnother(.1f);
        if(gamepad)capture.WithControlsHavingToMatchPath("<Gamepad>");else capture.WithControlsExcluding("<Gamepad>").WithControlsExcluding("<Joystick>");
        if(binding.isPartOfComposite||source.type==InputActionType.Button)capture.WithExpectedControlType("Button");
        foreach(var module in FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None))foreach(var action in new[]{module.move?.action,module.submit?.action,module.cancel?.action})if(action!=null&&action.enabled){disabledUIActions.Add(action);action.Disable();}
        capture.OnCancel(_=>CancelCapture()).OnComplete(op=>
        {
            string path=captureAction.bindings[index].overridePath;
            var canvas=GetComponentInParent<Canvas>();var cancelRect=modal.Find("Action_0") as RectTransform;
            bool clickedCancel=op.selectedControl?.device is Mouse&&cancelRect!=null&&RectTransformUtility.RectangleContainsScreenPoint(cancelRect,Mouse.current.position.ReadValue(),canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null);
            CancelCapture();if(!clickedCancel)OfferBinding(policy,path);
        });capture.Start();
    }
    void OfferBinding(GameInput.BindingPolicy policy,string path)
    {
        if(string.IsNullOrEmpty(path))return;
        var conflicts=GameInput.BindingPolicies.Where(p=>p.action!=policy.action&&GameInput.ContextsOverlap(p,policy)&&string.Equals(GameInput.BindingPath(p.id),path,StringComparison.OrdinalIgnoreCase)).ToArray();
        Action accept=()=>{try{GameInput.RebindBinding(policy.id,path);status.text=Text("binding.saved");}catch(Exception){status.text=Text("binding.invalid");}CloseModal();ShowSection("controls");};
        if(conflicts.Length==0){accept();return;}
        Dialog("conflict.title","conflict.body",new[]{("assign.anyway",accept),("cancel",(Action)CloseModal)},CloseModal);
        Action names=()=>modalBody.text=Text("conflict.body")+"\n"+(GameLocalization.CurrentCode=="tr"?policy.textTR:policy.textEN)+" / "+string.Join(", ",conflicts.Select(p=>GameLocalization.CurrentCode=="tr"?p.textTR:p.textEN));
        modalRefresh.Add(names);names();
    }
    public void CancelCapture()
    {
        captureWaiting=false;var op=capture;capture=null;if(op!=null)op.Dispose();captureAction?.Dispose();captureAction=null;
        ignoreCancelUntil=Time.unscaledTime+.2f;
        foreach(var action in disabledUIActions)action.Enable();disabledUIActions.Clear();if(built&&HasModal)CloseModal();
    }
    void SelectFirst(){if(!Application.isPlaying||EventSystem.current==null)return;var control=Rows.GetComponentsInChildren<Selectable>().FirstOrDefault(s=>s.IsInteractable());EventSystem.current.SetSelectedGameObject(control!=null?control.gameObject:tabs[Array.IndexOf(sections,Section)].gameObject);}
    void WireFocus(){foreach(var control in Rows.GetComponentsInChildren<Selectable>()){var focus=control.gameObject.GetComponent<SettingsMenuFocus>();if(focus==null)focus=control.gameObject.AddComponent<SettingsMenuFocus>();focus.view=this;}}
    public void ScrollTo(RectTransform target)
    {
        if(Rows.rect.height<=viewport.rect.height)return;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,target);float delta=0;if(bounds.min.y<viewport.rect.yMin)delta=viewport.rect.yMin-bounds.min.y;else if(bounds.max.y>viewport.rect.yMax)delta=viewport.rect.yMax-bounds.max.y;Rows.anchoredPosition+=new Vector2(0,delta);
    }
}

public sealed class SettingsMenuFocus:MonoBehaviour,ISelectHandler
{
    public SettingsMenuView view;
    public void OnSelect(BaseEventData data){if(view!=null)view.ScrollTo((RectTransform)transform);}
}
