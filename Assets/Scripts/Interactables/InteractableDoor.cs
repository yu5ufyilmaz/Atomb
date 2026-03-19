using System.Collections;
using StarterAssets; // Input ve Karakter scriptlerine erişim için şart
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
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockSound;
    public AudioClip unlockSound;
    public AudioClip lockedTryOpenSound;

    public AudioSource DoorAudioSource => audioSource;

    [Header("Player Freeze References")]
    [Tooltip("Oyuncunun üzerindeki CharacterController (Fizik)")]
    [SerializeField]
    private UnityEngine.CharacterController playerPhysics;

    // Kod içinde otomatik bulacağız ama Inspector'dan da bakabilirsin
    private Animator _playerAnimator;
    private StarterAssetsInputs _playerInput;
    private StarterAssets.CharacterController _playerMoveScript;

    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");

    private void Start()
    {
        if (doorAnimator == null)
            doorAnimator = GetComponent<Animator>();

        if (doorAnimator != null)
            doorAnimator.SetBool(IsOpenBool, isOpen);

        // Oyuncuyu bul ve bileşenlerini al
        if (playerPhysics == null)
            playerPhysics = FindObjectOfType<UnityEngine.CharacterController>();

        if (playerPhysics != null)
        {
            _playerAnimator = playerPhysics.GetComponent<Animator>();
            _playerInput = playerPhysics.GetComponent<StarterAssetsInputs>();
            // İsim çakışmasını önlemek için tam namespace kullanıyoruz
            _playerMoveScript = playerPhysics.GetComponent<StarterAssets.CharacterController>();
        }
    }

    // --- YENİ EKLENEN: GELİŞMİŞ DONDURMA FONKSİYONU ---
    private void FreezePlayer(bool freeze)
    {
        if (playerPhysics == null)
            return;

        if (_playerMoveScript != null)
        {
            // ESKİ HATALI KOD: restrictRotation: true (Hep kısıtlı kalıyordu)
            // YENİ DOĞRU KOD: restrictRotation: freeze (Donarsa kısıtla, çözülürse serbest bırak)

            _playerMoveScript.SetFrozen(freeze, lockCameraInput: false, restrictRotation: freeze);
        }
    }

    // ---------------------------------------------------

    public void Interact()
    {
        if (isAnimating)
            return;

        if (isLocked)
        {
            PlaySound(lockedTryOpenSound);
            Debug.Log("Kapı kilitli!");
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

        // Oyuncu artık donmuyor, serbestçe hareket edebilir!

        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger(isOpen ? OpenTrigger : CloseTrigger);
            doorAnimator.SetBool(IsOpenBool, isOpen);
        }

        PlaySound(isOpen ? openSound : closeSound);

        // Animasyonun bitmesini bekle (Spam'i engellemek için gerekli)
        yield return new WaitForSeconds(animationDuration);

        isAnimating = false;
    }

    public void SetLocked(bool locked)
    {
        if (isLocked == locked)
            return;
        isLocked = locked;
        PlaySound(isLocked ? lockSound : unlockSound);

        if (isLocked && isOpen)
        {
            isOpen = false;
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger(CloseTrigger);
                doorAnimator.SetBool(IsOpenBool, false);
                PlaySound(closeSound);
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
