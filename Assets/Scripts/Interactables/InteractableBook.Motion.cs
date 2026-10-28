using UnityEngine;

public partial class InteractableBook
{
    public bool IsOpening { get; private set; }
    public bool IsClosing { get; private set; }
    public bool IsShelfPulling { get; private set; }
    [Header("Raftan Alma")]
    [Min(.1f)] public float shelfPullDuration = .28f;
    [Min(.01f)] public float shelfPullClearance = .08f;
    [Tooltip("Raf ebeveynine göre dış yön. Sıfırsa kitap sırtından hesaplanır.")]
    public Vector3 shelfPullDirection;
    private bool shelfPickup;
    private Vector3 shelfExitLocalPosition;
    private AnimationClip coverMotion;
    private bool coverAnimatorEnabled, coverSoundPlayed;
    private bool closeRequested;

    private bool ShelfExit(out Vector3 exit)
    {
        exit = transform.position;
        var shelf = transform.parent;
        while (shelf != null && !shelf.name.StartsWith("RAF", System.StringComparison.OrdinalIgnoreCase)) shelf = shelf.parent;
        if (shelf == null || bookSkinnedMeshRenderer == null) return false;
        Vector3 direction = originalParent.TransformDirection(shelfPullDirection);
        if (direction.sqrMagnitude < .01f)
        {
            var binding = transform.Find("Armature.001/Root/Binding");
            if (binding == null) return false;
            direction = -binding.forward;
        }
        direction.y = 0; direction.Normalize();
        var bounds = bookSkinnedMeshRenderer.bounds;
        float depth = 2 * Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(direction.x), 0, Mathf.Abs(direction.z)));
        exit += direction * (depth + shelfPullClearance);
        return true;
    }

    private System.Collections.IEnumerator SlideShelf(Vector3 from, Vector3 to, Quaternion rotation)
    {
        IsShelfPulling = true;
        float t = 0;
        while (t < 1)
        {
            if (GameManager.Instance != null && GameManager.Instance.isGamePaused) { yield return null; continue; }
            t = Mathf.Clamp01(t + Time.deltaTime / Mathf.Max(.1f, shelfPullDuration));
            transform.SetPositionAndRotation(Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t)), rotation);
            yield return null;
        }
        transform.SetPositionAndRotation(to, rotation);
        IsShelfPulling = false;
    }

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
