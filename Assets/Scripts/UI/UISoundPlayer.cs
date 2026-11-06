using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum UISoundKind { Hover, Click, Tab, PageFlip, NotebookOpen, NotebookClose, NoteAdd, NoteRemove, BondConnect, BondBreak, Hint, ChalkPlace, Pin, PaperTap }

// 6 Nov: tiny mechanical ticks for menus, tabs and panels. The clips are generated at start (nothing to import),
// play through the SFX mixer group, and every Selectable in the game gets the pointer hook automatically.
public sealed class UISoundPlayer : MonoBehaviour
{
    public static UISoundPlayer Instance { get; private set; }
    public static int PlayCount { get; private set; }
    public static UISoundKind LastKind { get; private set; }
    public AudioSource Source { get; private set; }
    public bool ScribbleActive => scribbleSource != null && scribbleSource.isPlaying;
    const float MinGap = .04f, RescanEvery = .2f;
    static readonly int KindCount = System.Enum.GetValues(typeof(UISoundKind)).Length;
    static readonly float[] Volumes = { .3f, .55f, .5f, .35f, .3f, .3f, .3f, .3f, .35f, .35f, .15f, .35f, .45f, .25f };
    // Root canvases of the in-game paper UI: the notebook workspace and the board documents (classic and research layouts).
    static readonly string[] PaperCanvases = { "Senzora Workspace", "Tahtaya Takılı Belgeler", "Raptiyeli Araştırma Kartları" };
    readonly AudioClip[] clips = new AudioClip[KindCount];
    readonly float[] lastAt = new float[KindCount];
    float lastAny = -1, nextScan, scribbleUntil, scribbleTarget;
    AudioSource scribbleSource;
    UISoundBank bank;
    int knownSelectables = -1;
    Selectable[] buffer = new Selectable[256];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() { EnsureInstance(); }
    public static UISoundPlayer EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = FindFirstObjectByType<UISoundPlayer>();
        return existing != null ? existing : new GameObject("UISoundPlayer").AddComponent<UISoundPlayer>();
    }
    public static void Play(UISoundKind kind) { if (Application.isPlaying) EnsureInstance().PlayKind(kind); }
    // Called while a pen/eraser moves; the loop fades out 0.1 s after the last call, so a resting pen goes quiet.
    public static void Scribble(bool erase) { if (Application.isPlaying) EnsureInstance().ScribbleKind(erase); }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        Debug.Assert(Volumes.Length == KindCount, "UISoundPlayer: one volume per UISoundKind.");
        System.Array.Fill(lastAt, -1f);
        Source = NewSource(); scribbleSource = NewSource(); scribbleSource.loop = true; scribbleSource.volume = 0; scribbleSource.clip = ScribbleLoop();
        bank = Resources.Load<UISoundBank>("UISoundBank");
        clips[0] = Tick("UI Hover", .018f, 5200, 3.5f, .3f, 0);
        clips[1] = Tick("UI Click", .03f, 2600, 6f, .5f, 0);
        clips[2] = Tick("UI Tab", .044f, 2000, 6f, .55f, .02f);
        // PageFlip/Open/Close normally use the recordings in Resources/UISoundBank; these swishes only stand in without the bank.
        clips[3] = Tick("UI Page Flip", .12f, 1800, 40f, 0, 0);
        clips[4] = Tick("UI Notebook Open", .09f, 900, 30f, 0, 0);
        clips[5] = Tick("UI Notebook Close", .09f, 900, 30f, 0, 0);
        // In-game sounds are diegetic noise: pencil on paper, crumpled paper, chalk, a pushpin in cork. No tones above the pin's thump.
        clips[6] = Scratch("UI Pencil Write", .4f, 700, 3500, 7, .01f, .08f);
        clips[7] = Scratch("UI Paper Crumple", .18f, 500, 3000, 0, .002f, .03f, .55f);
        clips[8] = Scratch("UI Chalk Tap", .1f, 1800, 7000, 20, .002f, .01f);
        clips[9] = Scratch("UI Chalk Drag", .12f, 1200, 5000, 4.2f, .002f, .02f);
        clips[10] = Scratch("UI Pencil Tap", .05f, 700, 3500, 0, .001f, .03f);
        clips[11] = ChalkPlace();
        clips[12] = Pin();
        clips[13] = Scratch("UI Paper Tap", .05f, 400, 2500, 0, .001f, .03f);
    }
    AudioSource NewSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.ignoreListenerPause = true; source.bypassReverbZones = true; source.bypassEffects = true;
        return source;
    }

    void PlayKind(UISoundKind kind)
    {
        int i = (int)kind; float now = Time.unscaledTime;
        // The same tick never repeats within 40 ms; a hover right after another tick (a row rebuilt under the cursor) stays silent.
        if (now - lastAt[i] < MinGap || kind == UISoundKind.Hover && now - lastAny < MinGap) return;
        if (Source.outputAudioMixerGroup == null) Source.outputAudioMixerGroup = SettingsManager.EnsureInstance().effectsGroup;
        var clip = Recording(kind); if (clip == null) clip = clips[i];
        Source.PlayOneShot(clip, Volumes[i]); lastAt[i] = lastAny = now; PlayCount++; LastKind = kind;
    }
    AudioClip Recording(UISoundKind kind) => bank == null ? null : kind switch
    {
        UISoundKind.PageFlip => bank.pageFlip != null && bank.pageFlip.Length > 0 ? bank.pageFlip[Random.Range(0, bank.pageFlip.Length)] : null,
        UISoundKind.NotebookOpen => bank.notebookOpen, UISoundKind.NotebookClose => bank.notebookClose,
        UISoundKind.NoteAdd => bank.pencilWrite, UISoundKind.NoteRemove => bank.crumple, UISoundKind.BondConnect => bank.chalkTap,
        UISoundKind.BondBreak => bank.chalkDrag, UISoundKind.ChalkPlace => bank.chalkPlace, UISoundKind.Pin => bank.pin, UISoundKind.PaperTap => bank.paperTap,
        _ => null
    };

    void ScribbleKind(bool erase)
    {
        if (scribbleSource.outputAudioMixerGroup == null) scribbleSource.outputAudioMixerGroup = SettingsManager.EnsureInstance().effectsGroup;
        scribbleUntil = Time.unscaledTime + .1f; scribbleTarget = erase ? .14f : .2f; scribbleSource.pitch = erase ? .75f : 1f;
    }
    void UpdateScribble()
    {
        if (scribbleSource == null) return;
        float target = Time.unscaledTime < scribbleUntil ? scribbleTarget : 0;
        scribbleSource.volume = Mathf.MoveTowards(scribbleSource.volume, target, 4 * Time.unscaledDeltaTime);
        if (target > 0 && !scribbleSource.isPlaying) scribbleSource.Play();
        else if (scribbleSource.volume <= 0 && scribbleSource.isPlaying) scribbleSource.Stop();
    }

    // Every Selectable (scene-placed, prefab or built in code) gets the hook; rescans when the registered count changes and every 0.2 s
    // (a rebuilt panel can destroy and create the same number of controls in one frame).
    void Update()
    {
        UpdateScribble();
        int count = Selectable.allSelectableCount;
        if (count == knownSelectables && Time.unscaledTime < nextScan) return;
        knownSelectables = count; nextScan = Time.unscaledTime + RescanEvery;
        if (buffer.Length < count) buffer = new Selectable[Mathf.NextPowerOfTwo(count)];
        int n = Selectable.AllSelectablesNoAlloc(buffer);
        for (int i = 0; i < n; i++)
        {
            // The board documents canvas hangs under the board, so its root canvas (not transform.root) names the paper UI.
            var canvas = buffer[i].GetComponentInParent<Canvas>();
            if (canvas != null && System.Array.IndexOf(PaperCanvases, canvas.rootCanvas.name) >= 0) UISoundOnPointer.Attach(buffer[i].gameObject, UISoundKind.PaperTap, true);
            else UISoundOnPointer.Attach(buffer[i].gameObject);
        }
    }

    // Noise burst through a one-pole low-pass plus a damped tone, exponential decay; echoAt adds a softer second burst (the tab "latch").
    static AudioClip Tick(string name, float seconds, float cutoffHz, float decayMs, float tone, float echoAt)
    {
        int rate = Rate, count = Mathf.CeilToInt(seconds * rate); var data = new float[count];
        var random = new System.Random(name.Length * 977);
        float alpha = LowPass(cutoffHz, rate), low = 0, tau = decayMs * .001f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            low += alpha * ((float)random.NextDouble() * 2 - 1 - low);
            float env = Envelope(t, tau) + (echoAt > 0 && t >= echoAt ? .5f * Envelope(t - echoAt, tau) : 0);
            env *= Mathf.Clamp01((seconds - t) / .002f);
            data[i] = ((1 - tone) * low * 3 + tone * Mathf.Sin(2 * Mathf.PI * cutoffHz * .5f * t)) * env;
        }
        return Normalized(name, data, rate);
    }
    static AudioClip Scratch(string name, float seconds, float lowHz, float highHz, float strokesPerSecond, float attack, float release, float keep = 1)
        => Normalized(name, ScratchData(name, seconds, lowHz, highHz, strokesPerSecond, attack, release, keep), Rate);
    // Band-limited noise like ScribbleLoop, shaped into |sin|^1.5 strokes, an attack ramp and a release fade, peak 1;
    // keep < 1 drops 4 ms windows at random (seeded) so paper crackles instead of hissing.
    static float[] ScratchData(string name, float seconds, float lowHz, float highHz, float strokesPerSecond, float attack, float release, float keep)
    {
        int rate = Rate, count = Mathf.CeilToInt(seconds * rate), window = Mathf.Max(1, rate / 250); var data = new float[count];
        var random = new System.Random(name.Length * 977 + 31); var gate = new System.Random(name.Length * 131);
        float high = LowPass(highHz, rate), low = LowPass(lowHz, rate), a = 0, b = 0; bool open = true;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate, n = (float)random.NextDouble() * 2 - 1; a += high * (n - a); b += low * (n - b);
            if (keep < 1 && i % window == 0) open = gate.NextDouble() < keep;
            float strokes = strokesPerSecond > 0 ? Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * strokesPerSecond * t)), 1.5f) : 1;
            data[i] = open ? (a - b) * strokes * Mathf.Clamp01(t / Mathf.Max(attack, 1e-4f)) * Mathf.Clamp01((seconds - t) / Mathf.Max(release, 1e-4f)) : 0;
        }
        return Peak(data);
    }
    // Chalk dab then a short drag: one stroke hump plus a 4 ms broadband tap at the start.
    static AudioClip ChalkPlace()
    {
        int rate = Rate, tap = rate / 250; var data = ScratchData("UI Chalk Place", .22f, 1800, 7000, 4.5f, .002f, .02f, 1); var random = new System.Random(8191);
        for (int i = 0; i < tap && i < data.Length; i++) data[i] += .8f * ((float)random.NextDouble() * 2 - 1) * (1 - (float)i / tap);
        return Normalized("UI Chalk Place", data, rate);
    }
    // Pushpin into cork: 3 ms broadband click, 25 ms cork crunch (noise low-passed at 2500 Hz) and a damped 90 Hz thump (50 ms, tau 15 ms).
    static AudioClip Pin()
    {
        int rate = Rate, count = Mathf.CeilToInt(.08f * rate); var data = new float[count]; var random = new System.Random(6007);
        float alpha = LowPass(2500, rate), crunch = 0;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate, n = (float)random.NextDouble() * 2 - 1; crunch += alpha * (n - crunch);
            data[i] = (t < .003f ? .7f * n * (1 - t / .003f) : 0) + (t < .025f ? 1.6f * crunch * Mathf.Clamp01((.025f - t) / .008f) : 0)
                + (t < .05f ? .8f * Mathf.Sin(2 * Mathf.PI * 90 * t) * Mathf.Exp(-t / .015f) * Mathf.Clamp01((.05f - t) / .005f) : 0);
        }
        return Normalized("UI Pin", data, rate);
    }
    // Pencil/chalk grain: noise kept between ~600 and ~3500 Hz; the extra tail is crossfaded into the head so the loop has no seam.
    static AudioClip ScribbleLoop()
    {
        int rate = Rate, count = rate / 2, fade = rate / 20; var raw = new float[count + fade]; var random = new System.Random(4711);
        float high = LowPass(3500, rate), low = LowPass(600, rate), a = 0, b = 0;
        for (int i = 0; i < raw.Length; i++) { float n = (float)random.NextDouble() * 2 - 1; a += high * (n - a); b += low * (n - b); raw[i] = a - b; }
        var data = new float[count];
        for (int i = 0; i < count; i++) data[i] = i < fade ? Mathf.Lerp(raw[count + i], raw[i], (float)i / fade) : raw[i];
        return Normalized("UI Scribble", data, rate);
    }
    static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
    static float LowPass(float hz, int rate) => 1 - Mathf.Exp(-2 * Mathf.PI * hz / rate);
    static float Envelope(float t, float tau) => Mathf.Clamp01(t / .0005f) * Mathf.Exp(-t / tau);
    static float[] Peak(float[] data)
    {
        float peak = 0; foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0) for (int i = 0; i < data.Length; i++) data[i] /= peak;
        return data;
    }
    static AudioClip Normalized(string name, float[] data, int rate)
    {
        Peak(data);
        var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
}
