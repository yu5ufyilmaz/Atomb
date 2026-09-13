using System.Collections;
using UnityEngine;

public class HighlightAction : MonoBehaviour, IAction
{
    public enum HighlightMode
    {
        PermanentOn, // Direkt aç veya kapat
        TimedPulse, // Belirli bir süre yanıp sön
        BlinkUntilInteracted, // Etkileşime girilene kadar sürekli yanıp sön
    }

    [Tooltip("Parlama çalışma modu")]
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

    public void Execute()
    {
        // Eğer halihazırda çalışan bir coroutine varsa durdur
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        switch (mode)
        {
            case HighlightMode.PermanentOn:
                SetHighlight(true); // Kod içindeki bool değerine göre aç/kapat yapabiliriz istersen
                break;

            case HighlightMode.TimedPulse:
                activeRoutine = StartCoroutine(PulseRoutine(duration));
                break;

            case HighlightMode.BlinkUntilInteracted:
                activeRoutine = StartCoroutine(BlinkUntilInteractedRoutine());
                break;
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

        while (timer < targetDuration)
        {
            // Aç
            SetHighlight(true);
            yield return new WaitForSeconds(halfInterval);

            // Kapat
            SetHighlight(false);
            yield return new WaitForSeconds(halfInterval);

            timer += pulseSpeed;
        }

        // Süre bitince tamamen kapatarak bırak
        SetHighlight(false);
    }

    private IEnumerator BlinkUntilInteractedRoutine()
    {
        float halfInterval = pulseSpeed / 2f;
        bool isInteracted = false;

        // Burada oyuncunun ilgili objeyle etkileşime girip girmediğini dinleyebiliriz
        // Örnek olması açısından basit bir döngü kuruyoruz, istersen PlayerInteraction event'lerine de bağlayabilirsin
        while (!isInteracted)
        {
            SetHighlight(true);
            yield return new WaitForSeconds(halfInterval);

            SetHighlight(false);
            yield return new WaitForSeconds(halfInterval);

            // Örnek: Eğer not okunduysa veya tıklandıysa döngüyü kırabilirsin
            // Gerçek projede buraya bir kontrol ekleyebiliriz.
        }
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        // Nesne kapandığında sahnede açık parlama kalmasın
        SetHighlight(false);
    }
}
