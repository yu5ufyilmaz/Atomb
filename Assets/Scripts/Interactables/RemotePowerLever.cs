using System.Collections;
using UnityEngine;

public class RemotePowerLever : MonoBehaviour, IInteractable
{
    [Header("Target System")]
    [Tooltip("Bu şalter hangi makineye güç verecek?")]
    [SerializeField]
    private InteractableMassSpectrometer targetMachine;

    [Header("Rotation Settings")]
    [Tooltip("Döndürülecek olan parça (Kolun kendisi)")]
    [SerializeField]
    private Transform leverHandle;

    [Tooltip("Kol ne kadar dönecek? (Örn: X:45, Y:0, Z:0)")]
    [SerializeField]
    private Vector3 pullAngle = new Vector3(45f, 0f, 0f);

    [Tooltip("Dönme işlemi kaç saniye sürsün?")]
    [SerializeField]
    private float duration = 0.5f;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip pullSound;

    private bool isPulled = false;
    private Quaternion startRotation;

    private void Start()
    {
        // Başlangıç rotasyonunu kaydet ki buna göre açı ekleyelim
        if (leverHandle != null)
        {
            startRotation = leverHandle.localRotation;
        }
        else
        {
            Debug.LogError(
                "HATA: Lever Handle atanmamış! Lütfen Inspector'dan kol objesini atayın."
            );
        }
    }

    public void Interact()
    {
        // Eğer zaten çekildiyse veya kol objesi yoksa işlem yapma
        if (isPulled || leverHandle == null)
            return;

        StartCoroutine(AnimateLever());
    }

    private IEnumerator AnimateLever()
    {
        isPulled = true;

        // 1. Sesi Çal
        if (audioSource && pullSound)
            audioSource.PlayOneShot(pullSound);

        // 2. Hedef Rotasyonu Hesapla
        // Başlangıç rotasyonunun üzerine belirlediğimiz açıyı ekliyoruz
        Quaternion endRotation = startRotation * Quaternion.Euler(pullAngle);

        float elapsedTime = 0f;

        // 3. Döngü ile Yumuşak Geçiş (Lerp)
        while (elapsedTime < duration)
        {
            // Zamanı ilerlet
            elapsedTime += Time.deltaTime;

            // 0 ile 1 arasında bir oran bul (Geçen süre / Toplam süre)
            float t = elapsedTime / duration;

            // SmoothStep hareketi daha doğal yapar (Yavaş başlar, hızlanır, yavaş durur)
            t = Mathf.SmoothStep(0f, 1f, t);

            // Kolu döndür
            leverHandle.localRotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null; // Bir sonraki kareye bekle
        }

        // 4. Emin olmak için tam bitiş noktasına sabitle
        leverHandle.localRotation = endRotation;

        // 5. Makineye Gücü Ver
        if (targetMachine != null)
        {
            targetMachine.SetPower(true);
            Debug.Log($"<color=green>GÜÇ AKTİF:</color> {targetMachine.name}");
        }
        else
        {
            Debug.LogWarning("RemotePowerLever: Hedef Makine atanmamış, sadece kol indi.");
        }
    }

    public string GetInteractionPrompt()
    {
        return isPulled ? "Güç Açık" : "[Sol Tık] Güç Şalterini İndir";
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }
}
