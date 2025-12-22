using System.Collections.Generic; // List için gerekli
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IInteractable
{
    void Interact();
    string GetInteractionPrompt();
    void OnFocus();
    void OnLoseFocus();
}

public class PlayerInteraction : MonoBehaviour
{
    public static PlayerInteraction Instance;

    [Header("TUTORIAL AYARLARI (Whitelist)")]
    public bool isTutorialMode = true; // Oyun başında açık olsun

    [Tooltip("Tutorial sırasında SADECE buradaki objelerle etkileşime geçilebilir.")]
    public List<GameObject> allowedTutorialObjects; // İzinli objeler listesi

    [Header("Interaction Settings")]
    [SerializeField]
    private float interactionDistance = 3f;

    [SerializeField]
    private LayerMask interactionLayer;

    [SerializeField]
    private Transform raycastOrigin;

    [Header("UI References")]
    [SerializeField]
    private GameObject interactionUI;

    [SerializeField]
    private TextMeshProUGUI interactionText;

    [Header("Crosshair Settings")]
    [SerializeField]
    private Image crosshairImage;

    [SerializeField]
    private Sprite defaultIcon;

    [SerializeField]
    private Sprite handIcon;

    [SerializeField]
    private Sprite lockIcon;

    [SerializeField]
    private Sprite unlockIcon;

    [SerializeField]
    private Sprite eyeIcon;

    [SerializeField]
    private GameObject crosshairObject;

    private IInteractable currentInteractable;
    private Camera playerCamera;

    // --- ÖNEMLİ: Instance Hatasını Çözen Kısım ---
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        playerCamera = Camera.main;
        if (raycastOrigin == null && playerCamera != null)
            raycastOrigin = playerCamera.transform;

        if (interactionUI != null)
            interactionUI.SetActive(false);
        if (crosshairImage != null && defaultIcon != null)
            crosshairImage.sprite = defaultIcon;
    }

    private void Update()
    {
        CheckForInteractable();
        HandleInteractionInput();
    }

    private void CheckForInteractable()
    {
        if (raycastOrigin == null)
            return;

        Ray ray = new Ray(raycastOrigin.position, raycastOrigin.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayer))
        {
            // Önce objeyi bulmaya çalış
            IInteractable newInteractable = hit.collider.GetComponent<IInteractable>();
            if (newInteractable == null)
                newInteractable = hit.collider.GetComponentInParent<IInteractable>();

            if (newInteractable != null)
            {
                // --- WHITELIST KONTROLÜ ---
                if (isTutorialMode)
                {
                    // IInteractable bir MonoBehaviour (Script) olduğu için onun GameObject'ine ulaşabiliriz.
                    MonoBehaviour interactableScript = newInteractable as MonoBehaviour;

                    if (interactableScript != null)
                    {
                        // Eğer bu obje İzin Listesinde YOKSA -> Görmezden gel
                        if (!allowedTutorialObjects.Contains(interactableScript.gameObject))
                        {
                            ClearCurrentInteractable();
                            return;
                        }
                    }
                }
                // --------------------------

                // Eğer buraya geldiyse ya tutorial kapalıdır ya da obje listededir.
                if (currentInteractable != newInteractable)
                {
                    if (currentInteractable != null)
                        currentInteractable.OnLoseFocus();
                    currentInteractable = newInteractable;
                    currentInteractable.OnFocus();
                }
                UpdateUI(true);
                UpdateCrosshairIcon(newInteractable);
                return;
            }
        }

        ClearCurrentInteractable();
    }

    private void ClearCurrentInteractable()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnLoseFocus();
            currentInteractable = null;
            UpdateUI(false);
            if (crosshairImage != null)
                crosshairImage.sprite = defaultIcon;
        }
    }

    // Tutorial Bittiğinde MegaphoneSystem Burayı Çağıracak
    public void DisableTutorialMode()
    {
        isTutorialMode = false;
        Debug.Log("🔓 Tutorial Modu Kapandı. Tüm etkileşimler serbest.");
    }

    // ... (Geri kalan UI ve Crosshair fonksiyonların aynen kalıyor) ...
    public void ToggleCrosshair(bool state)
    {
        if (crosshairObject != null)
            crosshairObject.SetActive(state);
    }

    private void UpdateCrosshairIcon(IInteractable interactable)
    {
        if (crosshairImage == null)
            return;
        if (interactable is InteractableDoor door)
            crosshairImage.sprite = door.IsLocked() ? lockIcon : unlockIcon;
        else if (interactable is InteractableDoorLock)
            crosshairImage.sprite = lockIcon;
        else if (interactable is InteractableHidingSpot)
            crosshairImage.sprite = eyeIcon != null ? eyeIcon : handIcon;
        else
            crosshairImage.sprite = handIcon;
    }

    private void UpdateUI(bool isActive)
    {
        if (interactionUI != null)
        {
            interactionUI.SetActive(isActive);
            if (isActive && interactionText != null && currentInteractable != null)
                interactionText.text = currentInteractable.GetInteractionPrompt();
        }
    }

    private void HandleInteractionInput()
    {
        if (currentInteractable != null && Input.GetMouseButtonDown(0))
            currentInteractable.Interact();
    }
}
