using UnityEngine;

// Recorded notebook sounds for UISoundPlayer, loaded from Resources/UISoundBank; missing entries fall back to the generated ticks.
public sealed class UISoundBank : ScriptableObject
{
    public AudioClip[] pageFlip;
    public AudioClip notebookOpen, notebookClose;
    // In-game paper/chalk/cork slots for real recordings; empty ones keep the generated noise clips.
    public AudioClip pencilWrite, crumple, chalkTap, chalkDrag, chalkPlace, pin, paperTap;
}
