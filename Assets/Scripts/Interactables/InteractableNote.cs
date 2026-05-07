using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public class InteractableNote : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("📄 Not/Kağıt Ayarları")]
    public MeshRenderer paperRenderer;
    public Collider paperCollider;

    [Header("🔊 Ses Efektleri")]
    public AudioSource audioSource;
    public AudioClip pickUpSound;
    public AudioClip putDownSound;
    public AudioClip passwordFoundSound;

    [Header("🎥 Kamera & Pozisyon")]
    public Transform cameraTransform;
    public Vector3 viewPositionOffset = new Vector3(0, 0, 0.5f);
    public Vector3 viewRotationOffset = Vector3.zero;
    public float moveDuration = 0.5f;

    [Header("📘 ŞİFRE KİMLİĞİ")]
    public bool isPasswordNote = false;
    public string passwordID = "";
    public Rect passwordHotspotUV = new Rect(0, 0, 1, 1); // Varsayılan olarak tüm kağıt
    private bool hasPasswordBeenFound = false;

    [Header("✨ Vurgu (Highlight) Ayarları")]
    public HDRPOutlineController outlineController;
    public Color highlightColor = new Color(1f, 0.8f, 0f);

    [Range(0f, 100f)]
    public float emissionIntensity = 10f;

    // --- Runtime Değişkenler ---
    private Material originalMaterial;
    private Material highlightMaterial;
    private bool isFocused = false;

    [HideInInspector]
    public bool isOpen = false;

    [HideInInspector]
    public bool isAnimating = false;

    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator;
    private Camera mainCamera;

    private BoxCollider interactionCollider;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Transform originalParent;

    private void Start()
    {
        if (outlineController == null)
            outlineController = GetComponentInChildren<HDRPOutlineController>();

        // Highlight Hazırlığı
        if (paperRenderer != null && outlineController == null)
        {
            originalMaterial = paperRenderer.material;
            highlightMaterial = new Material(originalMaterial);
            highlightMaterial.EnableKeyword("_EMISSION");
            Color finalEmission = highlightColor * emissionIntensity;
            if (highlightMaterial.HasProperty("_EmissiveColor"))
                highlightMaterial.SetColor("_EmissiveColor", finalEmission);
            else if (highlightMaterial.HasProperty("_EmissionColor"))
                highlightMaterial.SetColor("_EmissionColor", finalEmission);
            highlightMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        interactionCollider = GetComponent<BoxCollider>();
        if (paperCollider == null)
            paperCollider = GetComponentInChildren<Collider>();

        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController != null)
        {
            playerGameScript = playerController.GetComponent<StarterAssets.CharacterController>();
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();
        }

        mainCamera = Camera.main;
        if (cameraTransform == null && mainCamera != null)
            cameraTransform = mainCamera.transform;
    }

    private void Update()
    {
        if (isOpen && !isAnimating)
        {
            if (isPasswordNote && !hasPasswordBeenFound && Input.GetMouseButtonDown(0))
            {
                CheckForPasswordClick();
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                StartCoroutine(CloseNote());
            }

            if (playerGameScript != null)
                playerGameScript.ExternalStaminaRegen(Time.deltaTime);
        }
    }

    public void OnFocus()
    {
        if (isOpen || isAnimating || isFocused)
            return;
        isFocused = true;

        if (outlineController != null)
            outlineController.ToggleOutline(true);
        else if (paperRenderer != null)
            paperRenderer.material = highlightMaterial;
    }

    public void OnLoseFocus()
    {
        if (!isFocused)
            return;
        isFocused = false;

        if (outlineController != null)
            outlineController.ToggleOutline(false);
        else if (paperRenderer != null)
            paperRenderer.material = originalMaterial;
    }

    public void Interact()
    {
        if (isAnimating || isOpen)
            return;
        OnLoseFocus();
        StartCoroutine(OpenNote());
    }

    public string GetInteractionPrompt()
    {
        if (isAnimating)
            return "";
        return isOpen ? "[F] Kapat" : "[Sol Tık] Oku";
    }

    private IEnumerator OpenNote()
    {
        isAnimating = true;
        isOpen = true;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.ShowMachineUI(ControlsUIManager.MachineType.Book);
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        if (interactionCollider != null)
            interactionCollider.enabled = false;
        if (playerGameScript != null)
            playerGameScript.enabled = false;
        if (playerController != null)
            playerController.enabled = false;
        if (playerLookScript != null)
            playerLookScript.enabled = false;
        if (playerAnimator != null)
            playerAnimator.enabled = false;

        GameManager.Instance.UpdateCursorState();
        PlaySound(pickUpSound);

        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        transform.SetParent(cameraTransform, true);

        float t = 0f;
        Vector3 startLocalPos = transform.localPosition;
        Quaternion startLocalRot = transform.localRotation;
        Quaternion targetLocalRot = Quaternion.Euler(viewRotationOffset);

        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.localPosition = Vector3.Lerp(startLocalPos, viewPositionOffset, smoothT);
            transform.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, smoothT);
            yield return null;
        }

        isAnimating = false;
    }

    private IEnumerator CloseNote()
    {
        isAnimating = true;
        isOpen = false;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.HideControls();
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        GameManager.Instance.UpdateCursorState();
        PlaySound(putDownSound);

        Vector3 targetWorldPosition =
            originalParent != null
                ? originalParent.TransformPoint(originalLocalPosition)
                : originalLocalPosition;
        Quaternion targetWorldRotation =
            originalParent != null
                ? originalParent.rotation * originalLocalRotation
                : originalLocalRotation;

        float t = 0f;
        Vector3 startWorldPos = transform.position;
        Quaternion startWorldRot = transform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.position = Vector3.Lerp(startWorldPos, targetWorldPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRotation, smoothT);
            yield return null;
        }

        transform.SetParent(originalParent, true);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;

        if (playerAnimator != null)
            playerAnimator.enabled = true;
        if (playerController != null)
            playerController.enabled = true;
        if (playerGameScript != null)
            playerGameScript.enabled = true;
        if (playerLookScript != null)
        {
            playerLookScript.enabled = true;
            if (playerLookScript is StarterAssetsInputs inputs)
                inputs.cursorInputForLook = true;
        }
        if (interactionCollider != null)
            interactionCollider.enabled = true;

        isAnimating = false;
    }

    private void CheckForPasswordClick()
    {
        if (
            PuzzleInventoryManager.Instance != null
            && PuzzleInventoryManager.Instance.isOverlayActive
        )
            return;

        if (paperCollider == null)
        {
            Debug.LogWarning(
                "InteractableNote: paperCollider atanmamış! Lütfen Inspector'dan kağıdın collider'ını sürükle."
            );
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool isHit = false;

        // 1. Önce doğrudan kağıdın collider'ına ışın gönder
        if (paperCollider.Raycast(ray, out hit, 100f))
        {
            isHit = true;
        }
        // 2. Olmazsa genel bir Scene ışını at ve vurduğu obje kağıt mı diye kontrol et (Build Fix)
        else if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.collider == paperCollider || hit.collider.transform.IsChildOf(transform))
            {
                isHit = true;
            }
        }

        // Eğer fare ışını kağıda çarptıysa...
        if (isHit)
        {
            Debug.Log("📄 Fare kağıda temas etti!");

            // HİLE: Eğer Inspector'dan W:1 ve H:1 yaptıysak (Yani kağıdın tamamı hotspot ise)
            // Hiç UV hesabı yapma, MeshCollider aramaya çalışma, direkt şifreyi tetikle!
            if (passwordHotspotUV.width >= 0.99f && passwordHotspotUV.height >= 0.99f)
            {
                Debug.Log("🔓 Tam sayfa hotspot algılandı, şifre çözüldü!");
                TriggerPasswordFind();
                return;
            }

            // (İleride kullanacağın diğer normal notlar için UV hesaplaması)
            Vector2 uv = hit.textureCoord;
            if (passwordHotspotUV.Contains(uv))
            {
                TriggerPasswordFind();
            }
            else
            {
                Debug.Log($"Kağıda tıklandı ama UV alanının dışında kaldı. Vurulan UV: {uv}");
            }
        }
    }

    public void TriggerPasswordFind()
    {
        if (hasPasswordBeenFound || !isPasswordNote)
            return;

        hasPasswordBeenFound = true;
        PasswordManager.Instance.DiscoverClue(passwordID);
        PlaySound(passwordFoundSound);

        if (NotebookUI.Instance != null)
            NotebookUI.Instance.ShowPasswordNotification(passwordID);
        if (MegaphoneSystem.Instance != null)
            MegaphoneSystem.Instance.OnNotepadPickedUp();

        StartCoroutine(CloseNote());
    }

    // YENİ EKLENEN ŞİFRE ATAMA METODU (PasswordManager burayı çağıracak)
    public void AssignPassword(string newPasswordID)
    {
        isPasswordNote = true;
        passwordID = newPasswordID;
        hasPasswordBeenFound = false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void ForceExit()
    {
        if (isOpen && !isAnimating)
            StartCoroutine(CloseNote());
    }
}
