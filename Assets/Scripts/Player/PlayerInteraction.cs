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
    public bool isTutorialMode = true;
    public List<GameObject> allowedTutorialObjects;

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

    [Header("Cursor GameObjects (Sürükle Bırak)")]
    [Tooltip("Hiçbir şeye bakmazken")]
    public GameObject defaultCursor;

    [Tooltip("Genel etkileşim (Kitap, Makine, Vana, Açık Kapı vb.)")]
    public GameObject handCursor;

    [Tooltip("Kilitli Durum (Kilitli Kapı veya Kilit)")]
    public GameObject lockedCursor;

    [Tooltip("Kilit Açık Durum (Sadece Kilit objesi açıldığında)")]
    public GameObject unlockedCursor;

    [Tooltip("Işık Açık Sembolü (Lamba Anahtarı Açıkken)")]
    public GameObject lightOnCursor;

    [Tooltip("Işık Kapalı Sembolü (Lamba Anahtarı Kapalıyken)")]
    public GameObject lightOffCursor;

    [SerializeField]
    private GameObject crosshairMainObject;

    private IInteractable currentInteractable;
    private Camera playerCamera;

    // Performans: Collider -> IInteractable önbelleği
    private Dictionary<Collider, IInteractable> interactableCache = new Dictionary<Collider, IInteractable>();

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

        // Başlangıçta sadece default açık olsun
        ActivateCursor(defaultCursor);
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
            // Performans: Önbellekten al veya ara
            if (!interactableCache.TryGetValue(hit.collider, out IInteractable newInteractable))
            {
                newInteractable = hit.collider.GetComponent<IInteractable>();
                if (newInteractable == null)
                    newInteractable = hit.collider.GetComponentInParent<IInteractable>();
                interactableCache[hit.collider] = newInteractable;
            }

            if (newInteractable != null)
            {
                // --- WHITELIST (TUTORIAL) KONTROLÜ ---
                if (isTutorialMode)
                {
                    MonoBehaviour interactableScript = newInteractable as MonoBehaviour;
                    if (
                        interactableScript != null
                        && !allowedTutorialObjects.Contains(interactableScript.gameObject)
                    )
                    {
                        ClearCurrentInteractable();
                        return;
                    }
                }
                // -------------------------------------

                // Yeni objeye odaklanma durumu
                if (currentInteractable != newInteractable)
                {
                    if (currentInteractable != null)
                        currentInteractable.OnLoseFocus();
                    currentInteractable = newInteractable;
                    currentInteractable.OnFocus();
                }

                UpdateUI(true);

                // İMLEÇ GÜNCELLEME (Buradaki mantık değiştirildi)
                UpdateCursorVisuals(newInteractable);

                return;
            }
        }

        // Boşa bakıyorsak
        ClearCurrentInteractable();
    }

    private void ClearCurrentInteractable()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnLoseFocus();
            currentInteractable = null;
        }

        UpdateUI(false);
        ActivateCursor(defaultCursor);
    }

    // --- CURSOR MANTIĞI GÜNCELLENDİ ---
    private void UpdateCursorVisuals(IInteractable interactable)
    {
        // 1. ÖZEL DURUM: LAMBA (ControllableLight)
        if (interactable is ControllableLight lightSwitch)
        {
            if (lightSwitch.IsOn)
                ActivateCursor(lightOnCursor);
            else
                ActivateCursor(lightOffCursor);
            return;
        }

        // 2. ÖZEL DURUM: KİLİT (InteractableDoorLock - Fiziksel Kilit)
        if (interactable is InteractableDoorLock doorLock)
        {
            // Kilit objesinin kendisine bakıyorsak:
            // Kilitliyse Kilit Sembolü, Açıksa Açık Kilit Sembolü
            if (doorLock.targetDoor.IsLocked()) // NOT: InteractableDoorLock scriptinde IsLocked public olmalı
                ActivateCursor(lockedCursor);
            else
                ActivateCursor(unlockedCursor);
            return;
        }

        // 3. ÖZEL DURUM: KAPI (InteractableDoor)
        if (interactable is InteractableDoor door)
        {
            // Eğer kapı KİLİTLİ ise -> Kilit Sembolü
            if (door.IsLocked())
            {
                ActivateCursor(lockedCursor);
            }
            // Eğer kapı KİLİTLİ DEĞİLSE -> El Sembolü (Açıp kapatmak için)
            else
            {
                ActivateCursor(handCursor);
            }
            return;
        }

        // 4. GENEL DURUM: (Kitap, Vana, Makine, Osiloskop, Şalterler vb.)
        // Yukarıdaki özel durumlara girmeyen her şey "EL" olur.
        ActivateCursor(handCursor);
    }

    // Sadece hedef imleci açıp diğerlerini kapatan fonksiyon
    private void ActivateCursor(GameObject targetCursor)
    {
        if (targetCursor == null)
            return;
        if (targetCursor.activeSelf)
            return; // Zaten açıksa işlem yapma

        // Tümünü kapat
        if (defaultCursor)
            defaultCursor.SetActive(false);
        if (handCursor)
            handCursor.SetActive(false);
        if (lockedCursor)
            lockedCursor.SetActive(false);
        if (unlockedCursor)
            unlockedCursor.SetActive(false);
        if (lightOnCursor)
            lightOnCursor.SetActive(false);
        if (lightOffCursor)
            lightOffCursor.SetActive(false);

        // Hedefi aç
        targetCursor.SetActive(true);
    }

    public void DisableTutorialMode()
    {
        isTutorialMode = false;
    }

    public void ToggleCrosshair(bool state)
    {
        if (crosshairMainObject != null)
            crosshairMainObject.SetActive(state);

        if (state)
            ActivateCursor(defaultCursor);
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
