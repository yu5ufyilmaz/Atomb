using TMPro; // TextMeshPro için
using UnityEngine;

public interface IInteractable
{
    void Interact();
    string GetInteractionPrompt();
    void OnFocus(); // Üzerine bakınca çalışır
    void OnLoseFocus(); // Bakışı çekince çalışır
}

public class PlayerInteraction : MonoBehaviour
{
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

    private IInteractable currentInteractable;
    private Camera playerCamera;

    private void Start()
    {
        playerCamera = Camera.main;
        if (raycastOrigin == null && playerCamera != null)
        {
            raycastOrigin = playerCamera.transform;
        }

        if (interactionUI != null)
            interactionUI.SetActive(false);
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
            IInteractable newInteractable = hit.collider.GetComponent<IInteractable>();

            // Eğer yeni bir objeye bakıyorsak
            if (newInteractable != null)
            {
                if (currentInteractable != newInteractable)
                {
                    // Eski objenin highlightını kapat
                    if (currentInteractable != null)
                        currentInteractable.OnLoseFocus();

                    // Yeni objeyi kaydet ve highlightını aç
                    currentInteractable = newInteractable;
                    currentInteractable.OnFocus();

                    UpdateUI(true);
                }
                return; // Bulduk, fonksiyondan çık
            }
        }

        // Hiçbir şeye bakmıyorsak veya mesafe dışındaysak
        if (currentInteractable != null)
        {
            currentInteractable.OnLoseFocus();
            currentInteractable = null;
            UpdateUI(false);
        }
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
        {
            currentInteractable.Interact();
        }
    }
}
