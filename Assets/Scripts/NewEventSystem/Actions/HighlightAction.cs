using System.Collections;
using UnityEngine;

public class HighlightAction : ActionBase
{
    public enum HighlightMode
    {
        PermanentOn,
        TimedPulse,
        BlinkUntilInteracted,
    }

    [Tooltip("Parlama/Yanıp sönme modu")]
    public HighlightMode mode = HighlightMode.PermanentOn;

    [Tooltip("Parlatılacak not objesi")]
    public InteractableNote targetNote;

    [Tooltip("Parlatılacak kitap objesi (Not değilse bunu kullan)")]
    public InteractableBook targetBook;

    [Header("Yanıp Sönme (Pulse) Ayarları")]
    [Tooltip("Yanıp sönme hızı (Saniye cinsinden bir döngü süresi)")]
    public float pulseSpeed = 1.0f;

    [Tooltip("TimedPulse modundayken kaç saniye boyunca yanıp sönsün?")]
    public float duration = 5.0f;

    private Coroutine activeRoutine;
    private bool isInteracted = false; // Döngüyü kırmak için global değişken

    protected override void PerformAction()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        switch (mode)
        {
            case HighlightMode.PermanentOn:
                SetHighlight(true);
                break;
            case HighlightMode.TimedPulse:
                activeRoutine = StartCoroutine(PulseRoutine(duration));
                break;
            case HighlightMode.BlinkUntilInteracted:
                isInteracted = false;
                // Oyuncu bir şeye tıkladığında bizi haberdar etmesi için abone oluyoruz
                PlayerInteraction.OnPlayerInteracted += HandleInteraction;
                activeRoutine = StartCoroutine(BlinkUntilInteractedRoutine());
                break;
        }
    }

    private void HandleInteraction(GameObject interactedObj)
    {
        // Tıklanan obje bizim parlatmaya çalıştığımız objeyse
        if (
            (targetNote != null && interactedObj == targetNote.gameObject)
            || (targetBook != null && interactedObj == targetBook.gameObject)
        )
        {
            isInteracted = true; // Döngüyü kır
            PlayerInteraction.OnPlayerInteracted -= HandleInteraction; // İşimizi bitince abonelikten çık
            SetHighlight(false); // Işığı anında kapat
        }
    }

    private void SetHighlight(bool state)
    {
        if (targetNote != null)
        {
            if (state)
                targetNote.OnFocus();
            else
                targetNote.OnLoseFocus();
        }

        if (targetBook != null)
        {
            if (state)
                targetBook.OnFocus();
            else
                targetBook.OnLoseFocus();
        }
    }

    private IEnumerator PulseRoutine(float targetDuration)
    {
        float timer = 0f;
        float halfInterval = pulseSpeed / 2f;

        // Objemizi önceden yaratıyoruz
        WaitForSeconds waitHalf = new WaitForSeconds(halfInterval);

        while (timer < targetDuration)
        {
            SetHighlight(true);
            yield return waitHalf; // Hazır objeyi çağır

            SetHighlight(false);
            yield return waitHalf; // Hazır objeyi çağır

            timer += pulseSpeed;
        }
        SetHighlight(false);
    }

    private IEnumerator BlinkUntilInteractedRoutine()
    {
        float halfInterval = pulseSpeed / 2f;

        // Objemizi önceden yaratıyoruz
        WaitForSeconds waitHalf = new WaitForSeconds(halfInterval);

        while (!isInteracted)
        {
            SetHighlight(true);
            yield return waitHalf;

            // Eğer saniyenin yarısı kadar beklerken oyuncu objeye tıkladıysa hemen çık
            if (isInteracted)
                break;

            SetHighlight(false);
            yield return waitHalf;
        }
        SetHighlight(false); // Garanti kapatma
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        PlayerInteraction.OnPlayerInteracted -= HandleInteraction; // Güvenlik için aboneliği kaldır
        SetHighlight(false);
    }

    private void OnDestroy()
    {
        // Obje tamamen silinirse hafıza kaçağı (memory leak) olmasın diye aboneliği kaldır
        PlayerInteraction.OnPlayerInteracted -= HandleInteraction;
    }
}
