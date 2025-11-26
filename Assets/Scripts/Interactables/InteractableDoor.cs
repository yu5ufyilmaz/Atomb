using UnityEngine;
using System.Collections;

public class InteractableDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private float animationDuration = 1.0f; // Animasyon süresi
    
    [Header("Door State")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool isLocked = false;
    private bool isAnimating = false; // Animasyon oynarken etkileşimi engelle
    
    [Header("Audio")]
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;
    [SerializeField] private AudioClip lockedSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Character Controller")]
    [SerializeField] private UnityEngine.CharacterController playerController; // Oyuncunun CharacterController'ı
    
    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");

    private void Start()
    {
        if (doorAnimator == null)
        {
            doorAnimator = GetComponent<Animator>();
            if (doorAnimator == null)
            {
                Debug.LogError("Door'a Animator bileşeni atanmamış!", this);
            }
        }
        
        if (doorAnimator != null)
        {
            doorAnimator.SetBool(IsOpenBool, isOpen);
        }
        
        // Eğer player controller atanmadıysa, sahnede bul
        if (playerController == null)
        {
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
            if (playerController == null)
            {
                Debug.LogWarning("CharacterController bulunamadı! Inspector'dan manuel olarak atayın.");
            }
        }
    }

    public void Interact()
    {
        // Animasyon oynarken etkileşimi engelle
        if (isAnimating)
            return;
            
        if (isLocked)
        {
            PlayLockedSound();
            Debug.Log("Kapı kilitli!");
            return;
        }
        
        StartCoroutine(ToggleDoorWithController());
    }

    public string GetInteractionPrompt()
    {
        if (isLocked)
            return "[E] Kilitli";
        
        if (isAnimating)
            return ""; // Animasyon sırasında prompt gösterme
        
        return isOpen ? "[E] Kapıyı Kapat" : "[E] Kapıyı Aç";
    }

    private IEnumerator ToggleDoorWithController()
    {
        isAnimating = true;
        isOpen = !isOpen;
        
        // CharacterController'ı pasif yap
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Kapı animasyonunu başlat
        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger(isOpen ? OpenTrigger : CloseTrigger);
            doorAnimator.SetBool(IsOpenBool, isOpen);
        }
        
        PlayDoorSound();
        
        // Animasyon süresince bekle
        yield return new WaitForSeconds(animationDuration);
        
        // CharacterController'ı tekrar aktif yap
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        isAnimating = false;
    }
    
    public void SetLocked(bool locked)
    {
        isLocked = locked;
        
        if (isLocked && isOpen)
        {
            isOpen = false;
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger(CloseTrigger);
                doorAnimator.SetBool(IsOpenBool, false);
            }
        }
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    private void PlayDoorSound()
    {
        if (audioSource != null)
        {
            AudioClip soundToPlay = isOpen ? doorOpenSound : doorCloseSound;
            if (soundToPlay != null)
            {
                audioSource.PlayOneShot(soundToPlay);
            }
        }
    }

    private void PlayLockedSound()
    {
        if (audioSource != null && lockedSound != null)
        {
            audioSource.PlayOneShot(lockedSound);
        }
    }
    
    public void OnDoorOpenComplete()
    {
        Debug.Log("Kapı tamamen açıldı");
    }
    
    public void OnDoorCloseComplete()
    {
        Debug.Log("Kapı tamamen kapandı");
    }
}