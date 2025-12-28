using System.Collections;
using UnityEngine;

public class InteractableDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [SerializeField]
    private Animator doorAnimator;

    [SerializeField]
    private float animationDuration = 1.0f;

    [Header("Door State")]
    public bool isOpen = false;

    [SerializeField]
    private bool isLocked = false;
    private bool isAnimating = false;

    [Header("Audio Settings")]
    [SerializeField]
    private AudioSource audioSource;

    [Tooltip("Kapı açılırken çalacak ses")]
    public AudioClip openSound;

    [Tooltip("Kapı kapanırken çalacak ses")]
    public AudioClip closeSound;

    [Tooltip("Kapı kilitlenirken çıkan ses (Anahtar sesi vb.)")]
    public AudioClip lockSound;

    [Tooltip("Kapı kilidi açılırken çıkan ses")]
    public AudioClip unlockSound;

    [Tooltip("Kilitli kapıyı açmaya çalışınca çıkan ses (Zorlama/Rattle)")]
    public AudioClip lockedTryOpenSound;

    // GuderianAI gibi dışarıdan erişmek isteyenler için
    public AudioSource DoorAudioSource => audioSource;

    [Header("Character Controller")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;

    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");

    private void Start()
    {
        if (doorAnimator == null)
            doorAnimator = GetComponent<Animator>();

        if (doorAnimator != null)
            doorAnimator.SetBool(IsOpenBool, isOpen);

        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
    }

    public void Interact()
    {
        if (isAnimating)
            return;

        if (isLocked)
        {
            // KİLİTLİ KAPIYI ZORLAMA SESİ
            PlaySound(lockedTryOpenSound);
            Debug.Log("Kapı kilitli! (Zorlama sesi çaldı)");

            // Opsiyonel: Kilitli kapı animasyonu (hafif sallanma) eklenebilir
            if (doorAnimator != null)
                doorAnimator.SetTrigger("Locked");
            return;
        }

        StartCoroutine(ToggleDoorWithController());
    }

    public string GetInteractionPrompt()
    {
        if (isLocked)
            return "[E] Kilitli";
        if (isAnimating)
            return "";
        return isOpen ? "[E] Kapıyı Kapat" : "[E] Kapıyı Aç";
    }

    private IEnumerator ToggleDoorWithController()
    {
        isAnimating = true;
        isOpen = !isOpen;

        if (playerController != null)
            playerController.enabled = false;

        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger(isOpen ? OpenTrigger : CloseTrigger);
            doorAnimator.SetBool(IsOpenBool, isOpen);
        }

        // AÇMA / KAPAMA SESİ
        PlaySound(isOpen ? openSound : closeSound);

        yield return new WaitForSeconds(animationDuration);

        if (playerController != null)
            playerController.enabled = true;

        isAnimating = false;
    }

    // --- DIŞARIDAN KONTROL (KİLİT SİSTEMİ İÇİN) ---

    public void SetLocked(bool locked)
    {
        // Durum değişti mi kontrol et (Gereksiz ses çalmamak için)
        if (isLocked == locked)
            return;

        isLocked = locked;

        // KİLİTLEME / KİLİT AÇMA SESİ
        // Bu fonksiyonu InteractableDoorLock.cs veya GameManager çağıracak
        PlaySound(isLocked ? lockSound : unlockSound);

        // Eğer kilitlendiyse ve kapı açıksa, kapıyı kapat
        if (isLocked && isOpen)
        {
            isOpen = false;
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger(CloseTrigger);
                doorAnimator.SetBool(IsOpenBool, false);
                PlaySound(closeSound); // Kapanma sesi de çalsın
            }
        }
    }

    public void SetOpen(bool openState)
    {
        isOpen = openState;
        if (doorAnimator != null)
        {
            doorAnimator.SetBool(IsOpenBool, isOpen);
            doorAnimator.SetTrigger(isOpen ? OpenTrigger : CloseTrigger);
        }
    }

    public bool IsLocked() => isLocked;

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }
}
