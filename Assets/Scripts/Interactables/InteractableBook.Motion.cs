using UnityEngine;

public partial class InteractableBook
{
    public bool IsOpening { get; private set; }
    public bool IsClosing { get; private set; }
    private AnimationClip coverMotion;
    private bool coverAnimatorEnabled, coverSoundPlayed;
    private bool closeRequested;

    private void BeginCoverMotion(bool opening, bool skip)
    {
        coverMotion = null;
        coverSoundPlayed = false;
        if (bookAnimator == null || bookAnimator.runtimeAnimatorController == null) return;
        coverAnimatorEnabled = bookAnimator.enabled;
        if (skip) return;
        string preferred = opening ? "newBookOpening" : "newBookClosing";
        foreach (var clip in bookAnimator.runtimeAnimatorController.animationClips)
            if (clip.name == preferred) { coverMotion = clip; break; }
        if (coverMotion == null) foreach (var clip in bookAnimator.runtimeAnimatorController.animationClips)
            if (clip.name.ToLowerInvariant().Contains(opening ? "opening" : "closing"))
            { coverMotion = clip; break; }
        bookAnimator.ResetTrigger(OpenTrigger); bookAnimator.ResetTrigger(CloseTrigger);
        if (coverMotion != null) bookAnimator.enabled = false;
        else { bookAnimator.SetBool(IsOpenBool, opening); bookAnimator.SetTrigger(opening ? OpenTrigger : CloseTrigger); }
    }

    private void UpdateCoverMotion(float progress)
    {
        if (coverMotion != null)
            coverMotion.SampleAnimation(bookAnimator.gameObject, Mathf.SmoothStep(0, 1, progress) * coverMotion.length);
    }

    private void EndCoverMotion(bool open)
    {
        if (bookAnimator == null || bookAnimator.runtimeAnimatorController == null) return;
        UpdateCoverMotion(1);
        bookAnimator.enabled = coverAnimatorEnabled;
        bookAnimator.SetBool(IsOpenBool, open);
        foreach (string name in open ? new[] { "newBookOpened", "BookOpened" } : new[] { "newBookClosed", "BookClosed" })
            if (bookAnimator.HasState(0, Animator.StringToHash(name)))
            { bookAnimator.Play(name, 0, 0); bookAnimator.Update(0); break; }
        coverMotion = null;
    }

    private void SetVisiblePage(string property, int page)
    {
        if (bookPagesMaterial != null) bookPagesMaterial.SetFloat(property, page);
        if (bookSkinnedMeshRenderer == null || pagePropertyBlock == null) return;
        bookSkinnedMeshRenderer.GetPropertyBlock(pagePropertyBlock, bookMaterialIndex);
        pagePropertyBlock.SetFloat(property, page);
        bookSkinnedMeshRenderer.SetPropertyBlock(pagePropertyBlock, bookMaterialIndex);
    }
}
