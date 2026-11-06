using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum UISoundKind { Hover, Click, Tab }

// 6 Nov: tiny mechanical ticks for menus, tabs and panels. The clips are generated at start (nothing to import),
// play through the SFX mixer group, and every Selectable in the game gets the pointer hook automatically.
public sealed class UISoundPlayer : MonoBehaviour
{
    public static UISoundPlayer Instance { get; private set; }
    public static int PlayCount { get; private set; }
    public static UISoundKind LastKind { get; private set; }
    public AudioSource Source { get; private set; }
    const float MinGap = .04f, RescanEvery = .2f;
    static readonly float[] Volumes = { .3f, .55f, .5f };
    readonly AudioClip[] clips = new AudioClip[3];
    readonly float[] lastAt = { -1, -1, -1 };
    float lastAny = -1, nextScan;
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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        Source = gameObject.AddComponent<AudioSource>();
        Source.playOnAwake = false; Source.spatialBlend = 0; Source.ignoreListenerPause = true; Source.bypassReverbZones = true; Source.bypassEffects = true;
        clips[0] = Tick("UI Hover", .018f, 5200, 3.5f, .3f, 0);
        clips[1] = Tick("UI Click", .03f, 2600, 6f, .5f, 0);
        clips[2] = Tick("UI Tab", .044f, 2000, 6f, .55f, .02f);
    }

    void PlayKind(UISoundKind kind)
    {
        int i = (int)kind; float now = Time.unscaledTime;
        // The same tick never repeats within 40 ms; a hover right after another tick (a row rebuilt under the cursor) stays silent.
        if (now - lastAt[i] < MinGap || kind == UISoundKind.Hover && now - lastAny < MinGap) return;
        if (Source.outputAudioMixerGroup == null) Source.outputAudioMixerGroup = SettingsManager.EnsureInstance().effectsGroup;
        Source.PlayOneShot(clips[i], Volumes[i]); lastAt[i] = lastAny = now; PlayCount++; LastKind = kind;
    }

    // Every Selectable (scene-placed, prefab or built in code) gets the hook; rescans when the registered count changes and every 0.2 s
    // (a rebuilt panel can destroy and create the same number of controls in one frame).
    void Update()
    {
        int count = Selectable.allSelectableCount;
        if (count == knownSelectables && Time.unscaledTime < nextScan) return;
        knownSelectables = count; nextScan = Time.unscaledTime + RescanEvery;
        if (buffer.Length < count) buffer = new Selectable[Mathf.NextPowerOfTwo(count)];
        int n = Selectable.AllSelectablesNoAlloc(buffer);
        for (int i = 0; i < n; i++) UISoundOnPointer.Attach(buffer[i].gameObject);
    }

    // Noise burst through a one-pole low-pass plus a damped tone, exponential decay; echoAt adds a softer second burst (the tab "latch").
    static AudioClip Tick(string name, float seconds, float cutoffHz, float decayMs, float tone, float echoAt)
    {
        int rate = AudioSettings.outputSampleRate; if (rate <= 0) rate = 48000;
        int count = Mathf.CeilToInt(seconds * rate); var data = new float[count];
        var random = new System.Random(name.Length * 977);
        float alpha = 1 - Mathf.Exp(-2 * Mathf.PI * cutoffHz / rate), low = 0, peak = 0, tau = decayMs * .001f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            low += alpha * ((float)random.NextDouble() * 2 - 1 - low);
            float env = Envelope(t, tau) + (echoAt > 0 && t >= echoAt ? .5f * Envelope(t - echoAt, tau) : 0);
            env *= Mathf.Clamp01((seconds - t) / .002f);
            data[i] = ((1 - tone) * low * 3 + tone * Mathf.Sin(2 * Mathf.PI * cutoffHz * .5f * t)) * env;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        if (peak > 0) for (int i = 0; i < count; i++) data[i] /= peak;
        var clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    static float Envelope(float t, float tau) => Mathf.Clamp01(t / .0005f) * Mathf.Exp(-t / tau);
}
