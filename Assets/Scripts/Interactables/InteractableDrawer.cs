using System.Collections;
using UnityEngine;

public class InteractableDrawer : MonoBehaviour, IInteractable
{
    [Header("Hareket Ayarları")]
    [Tooltip("Çekmecenin ne kadar dışarı çıkacağı")]
    public float openDistance = 0.5f;

    [Tooltip("Çekmecenin açılma/kapanma hızı")]
    public float speed = 5f;

    [Tooltip(
        "Çekmecenin hareket edeceği yerel yön (Genelde Vector3.forward veya Vector3.right olur)"
    )]
    public Vector3 localMoveDirection = Vector3.forward;

    [Header("Durum")]
    public bool isOpen = false;
    private bool isAnimating = false;

    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;

    [Header("Ses Efektleri")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    private void Start()
    {
        // Başlangıç pozisyonunu 'kapalı' pozisyon olarak kaydediyoruz
        closedLocalPosition = transform.localPosition;

        // Açık pozisyonu hesaplıyoruz
        openLocalPosition = closedLocalPosition + (localMoveDirection.normalized * openDistance);

        // Eğer Inspector'dan başlangıçta açık ayarlandıysa pozisyonu direkt oraya al
        if (isOpen)
        {
            transform.localPosition = openLocalPosition;
        }
    }

    public void Interact()
    {
        // Eğer çekmece şu an hareket halindeyse tıklamayı yok say
        if (isAnimating)
            return;

        StartCoroutine(ToggleDrawer());
    }

    public string GetInteractionPrompt()
    {
        if (isAnimating)
            return ""; // Hareket halindeyken yazı çıkmasın
        return isOpen ? "[E] Çekmeceyi Kapat" : "[E] Çekmeceyi Aç";
    }

    private IEnumerator ToggleDrawer()
    {
        isAnimating = true;
        isOpen = !isOpen;

        // Ses çalma
        if (audioSource != null)
        {
            audioSource.PlayOneShot(isOpen ? openSound : closeSound);
        }

        // Hedef pozisyonu belirle
        Vector3 targetPosition = isOpen ? openLocalPosition : closedLocalPosition;

        // Çekmece hedefine çok yaklaşana kadar Lerp ile pürüzsüzce kaydır
        while (Vector3.Distance(transform.localPosition, targetPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetPosition,
                Time.deltaTime * speed
            );
            yield return null; // Bir sonraki frame'i bekle
        }

        // Tam milimetrik olarak hedefe oturt (sarsıntıları veya ufak kaymaları önlemek için)
        transform.localPosition = targetPosition;
        isAnimating = false;
    }

    // Odaklanma anında ekstra bir şey yapmak istersen buraları doldurabilirsin (Örn: Outline açmak)
    public void OnFocus() { }

    public void OnLoseFocus() { }
}
