using System.Collections;
using NaughtyAttributes;
using UnityEngine;

public class UIAction : ActionBase
{
    public enum UIEffectType
    {
        PopUp,
        Heartbeat,
    }

    [Tooltip("Hangi UI objesi etkilenecek?")]
    public GameObject targetUI;

    [Tooltip("UI üzerinde hangi efekt oynatılsın?")]
    public UIEffectType effectType = UIEffectType.PopUp;

    [ShowIf("effectType", UIEffectType.PopUp)]
    [Tooltip("Pop-up ekranda kaç saniye kalsın?")]
    public float popupDuration = 2.5f;

    [ShowIf("effectType", UIEffectType.Heartbeat)]
    [Tooltip("Obje ne kadar büyüsün? (Örn: 1.2 = %20 büyüme)")]
    public float pulseScale = 1.2f;

    [ShowIf("effectType", UIEffectType.Heartbeat)]
    [Tooltip("Bir kalp atışı ne kadar sürsün? (Düşük değer = Daha hızlı atış)")]
    public float pulseSpeed = 0.5f;

    [ShowIf("effectType", UIEffectType.Heartbeat)]
    [Tooltip("Bu atış üst üste kaç kere tekrar etsin?")]
    public int pulseCount = 3;

    protected override void PerformAction()
    {
        if (targetUI == null)
        {
            Debug.LogWarning("[UIAction] Hedef UI objesi atanmamış!");
            return;
        }

        if (effectType == UIEffectType.PopUp)
        {
            StartCoroutine(PopUpRoutine());
        }
        else if (effectType == UIEffectType.Heartbeat)
        {
            StartCoroutine(HeartbeatRoutine());
        }
    }

    private IEnumerator PopUpRoutine()
    {
        targetUI.SetActive(true);
        yield return new WaitForSeconds(popupDuration);
        targetUI.SetActive(false);
    }

    private IEnumerator HeartbeatRoutine()
    {
        // Orijinal boyutu kaydet
        Vector3 originalScale = targetUI.transform.localScale;
        Vector3 targetScale = originalScale * pulseScale;
        float halfDuration = pulseSpeed / 2f;

        // Ekranda belirginleşmesi için objenin açık olduğundan emin ol
        if (!targetUI.activeSelf)
            targetUI.SetActive(true);

        // Belirlenen tekrar sayısı kadar kalp atışı yap
        for (int i = 0; i < pulseCount; i++)
        {
            float t = 0f;

            // Büyüme fazı
            while (t < halfDuration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / halfDuration);
                targetUI.transform.localScale = Vector3.Lerp(originalScale, targetScale, smoothT);
                yield return null;
            }

            t = 0f;

            // Küçülme fazı
            while (t < halfDuration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / halfDuration);
                targetUI.transform.localScale = Vector3.Lerp(targetScale, originalScale, smoothT);
                yield return null;
            }
        }

        // Sapmaları önlemek için döngü bitince son boyutu kesin olarak sabitle
        targetUI.transform.localScale = originalScale;
    }
}
