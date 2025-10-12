using UnityEngine;

public interface IInteractable
{
    void Interact();
    string GetInteractionPrompt(); 
}

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionLayer;
    [SerializeField] private Transform raycastOrigin; // Kamera transform'u (Inspector'dan ata)
    
    [Header("UI References")]
    [SerializeField] private GameObject interactionUI; // UI Canvas'ı
    [SerializeField] private TMPro.TextMeshProUGUI interactionText; // Etkileşim yazısı
    
    private IInteractable currentInteractable;
    private Camera playerCamera;

    private void Start()
    {
        // Ana kamerayı bul
        playerCamera = Camera.main;
        
        // Eğer raycastOrigin atanmadıysa, kamerayı kullan
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
        if (raycastOrigin == null) return;
        
        Ray ray = new Ray(raycastOrigin.position, raycastOrigin.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            
            if (interactable != null)
            {
                SetCurrentInteractable(interactable);
                return;
            }
        }
        
        SetCurrentInteractable(null);
    }

    private void SetCurrentInteractable(IInteractable interactable)
    {
        currentInteractable = interactable;
        
        if (interactionUI != null)
        {
            if (currentInteractable != null)
            {
                interactionUI.SetActive(true);
                if (interactionText != null)
                    interactionText.text = currentInteractable.GetInteractionPrompt();
            }
            else
            {
                interactionUI.SetActive(false);
            }
        }
    }

    private void HandleInteractionInput()
    {
        // Sol tık (Mouse0) ile etkileşim
        if (currentInteractable != null && Input.GetMouseButtonDown(0))
        {
            currentInteractable.Interact();
        }
    }
    
    private void OnDrawGizmos()
    {
        if (raycastOrigin == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(raycastOrigin.position, raycastOrigin.forward * interactionDistance);
    }
}