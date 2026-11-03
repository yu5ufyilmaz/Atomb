using System.Collections.Generic;
using UnityEngine;

// Sigorta atınca Turing ve osiloskopların ekranı, ışıkları ve uğultusu söner (3 Kasım oyun testi).
// Kontrolleri makinenin kendi Update'i PowerOut ile keser; çıkış her zaman serbesttir. Elektrik gelince her şey geri döner.
public class PoweredDevice : MonoBehaviour
{
    public static bool PowerOut => BreakerBox.Instance != null && BreakerBox.Instance.IsTripped;

    private static MaterialPropertyBlock darkGlow;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Canvas> canvases = new List<Canvas>();
    private readonly List<AudioSource> sources = new List<AudioSource>();
    private readonly HashSet<Renderer> darkened = new HashSet<Renderer>();
    private readonly HashSet<Renderer> hidden = new HashSet<Renderer>();
    private readonly HashSet<Canvas> hiddenScreens = new HashSet<Canvas>();
    private readonly HashSet<AudioSource> paused = new HashSet<AudioSource>();
    private readonly List<Transform> extraParts = new List<Transform>();

    // Parts a machine builds outside its own hierarchy (the Turing card reader avoids the scaled FBX parent).
    public void Include(Transform part)
    {
        if (part != null && !extraParts.Contains(part)) extraParts.Add(part);
    }

    private void Collect<T>(List<T> into) where T : Component
    {
        GetComponentsInChildren(into);
        foreach (Transform part in extraParts)
            if (part != null) into.AddRange(part.GetComponentsInChildren<T>());
    }

    private void LateUpdate()
    {
        if (PowerOut) Darken();
        else if (darkened.Count + hidden.Count + hiddenScreens.Count + paused.Count > 0) Restore();
    }

    // Every dark frame: the machine's own code may start a hum or redraw a line meanwhile.
    private void Darken()
    {
        if (darkGlow == null)
        {
            darkGlow = new MaterialPropertyBlock();
            darkGlow.SetColor("_EmissiveColor", Color.black);
            darkGlow.SetColor("_UnlitColor", Color.black);
        }
        Collect(renderers);
        foreach (Renderer r in renderers)
        {
            // Line traces and world text are unlit: hiding them is the only way they go dark.
            if (r is LineRenderer || r is TrailRenderer || r.GetComponent<TMPro.TMP_Text>() != null)
            {
                if (r.enabled) { r.enabled = false; hidden.Add(r); }
            }
            else if (!darkened.Contains(r) && Glows(r.sharedMaterial))
            {
                r.SetPropertyBlock(darkGlow);
                darkened.Add(r);
            }
        }
        Collect(canvases);
        foreach (Canvas c in canvases)
            if (c.enabled) { c.enabled = false; hiddenScreens.Add(c); }
        Collect(sources);
        foreach (AudioSource s in sources)
            if (s.isPlaying) { s.Pause(); paused.Add(s); }
    }

    private void Restore()
    {
        foreach (Renderer r in darkened) if (r != null) r.SetPropertyBlock(null);
        foreach (Renderer r in hidden) if (r != null) r.enabled = true;
        foreach (Canvas c in hiddenScreens) if (c != null) c.enabled = true;
        foreach (AudioSource s in paused) if (s != null) s.UnPause();
        darkened.Clear(); hidden.Clear(); hiddenScreens.Clear(); paused.Clear();
    }

    private static bool Glows(Material m) => m != null
        && ((m.HasProperty("_EmissiveColor") && m.GetColor("_EmissiveColor").maxColorComponent > 0f) || m.shader.name.Contains("Unlit"));
}
