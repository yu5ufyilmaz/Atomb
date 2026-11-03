using UnityEngine;
using UnityEngine.UI;

/// <summary>A HUD ring stays quiet while nothing happens and comes forward while it changes or shows a warning colour,
/// so a full stamina ring or an idle flashlight doesn't glow in the corner of a dark room.</summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class HudGaugeFade : MonoBehaviour
{
    public Image ring, track;
    public float idleAlpha = .35f, holdSeconds = 2.5f, fadeSeconds = .6f, trackAlpha = .14f;
    CanvasGroup group;
    float lastFill = -1, lastChange = float.NegativeInfinity;

    void Awake() { group = GetComponent<CanvasGroup>(); lastChange = Time.unscaledTime; }

    void Update()
    {
        if (!Mathf.Approximately(ring.fillAmount, lastFill)) { lastFill = ring.fillAmount; lastChange = Time.unscaledTime; }
        bool warning = ring.color.r - ring.color.g > .3f; // danger tints (pressure) are red
        float target = warning || Time.unscaledTime - lastChange < holdSeconds ? 1 : idleAlpha;
        // Only touch the graphics while fading: the track sits on the root HUD canvas, which FpsPass keeps from rebatching every frame.
        if (group.alpha == target) return;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fadeSeconds);
        if (track != null) { var color = track.color; color.a = trackAlpha * group.alpha; track.color = color; }
    }
}
