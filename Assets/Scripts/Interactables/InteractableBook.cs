using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;

public class InteractableBook : MonoBehaviour, IInteractable, IForceExitable
{
    // --- EDİTÖRDE GÖRÜNMESİ GEREKENLER (AYARLAR) ---

    [Header("📖 Kitap Ayarları")]
    public Animator bookAnimator;
    public float pageFlipDuration = 0.8f;
    public int totalPages = 8;
    public bool allowLoop = false;

    [Header("🎨 Görsel & Materyal")]
    [Tooltip("Kitabın olduğu Skinned Mesh Renderer (Tek parça model)")]
    public SkinnedMeshRenderer bookSkinnedMeshRenderer;
    public int bookMaterialIndex = 0;
    public Material bookPagesMaterial;

    [Space(10)]
    public GameObject pageFlipObject;
    public MeshRenderer pageFlipRenderer;
    public Material pageTurnMaterial;

    [Header("🔊 Ses Efektleri")]
    public AudioSource audioSource;
    public AudioClip bookOpenSound;
    public AudioClip bookCloseSound;
    public AudioClip[] pageFlipSounds;
    public AudioClip passwordFoundSound;

    [Header("🎥 Kamera & Pozisyon")]
    public Transform cameraTransform;
    public Vector3 viewPositionOffset = new Vector3(0, 0, 0.5f);
    public Vector3 viewRotationOffset = Vector3.zero;
    public float moveDuration = 0.5f;

    [Header("UI Bağlantıları")]
    public GameObject bookUI;
    public TextMeshProUGUI pageNumberText;

    [Header("Gizmos Settings")]
    [SerializeField]
    private Vector2 singlePageSize = new Vector2(0.16f, 0.32f);

    [SerializeField]
    private float gizmoYOffset = 0.005f;

    [SerializeField]
    private float animationDuration = 1f;

    [Header("📘 KİTAP KİMLİĞİ")]
    public PasswordData bookIdentity;
    public bool canContainPassword = true;

    // --- YENİ: OUTLINE & HIGHLIGHT AYARLARI ---
    [Header("✨ Vurgu (Highlight) Ayarları")]
    [Tooltip("HDRP Outline Scripti (Varsa Emission yerine bu çalışır)")]
    public HDRPOutlineController outlineController;

    [Tooltip("Vurgu Rengi (Sarı, Beyaz vb.)")]
    public Color highlightColor = new Color(1f, 0.8f, 0f);

    [Tooltip("HDRP Şiddeti (Eğer parlamıyorsa bunu 10, 50, 100 yap!)")]
    [Range(0f, 100f)]
    public float emissionIntensity = 10f;

    // --- Runtime Değişkenler ---
    private Material[] originalMaterials;
    private Material[] highlightMaterials;
    private bool isFocused = false;

    // Diğer gizli değişkenler...
    [HideInInspector]
    public bool isOpen = false;

    [HideInInspector]
    public bool isAnimating = false;

    [HideInInspector]
    public int currentPage = 0;

    [HideInInspector]
    public bool isPasswordBook = false;

    [HideInInspector]
    public int passwordPage = -1;

    [HideInInspector]
    public string passwordID = "";

    [HideInInspector]
    public Rect passwordHotspotUV;

    [HideInInspector]
    public bool hasPasswordBeenFound = false;

    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator;
    private Camera mainCamera;
    private BoxCollider interactionCollider;
    public Collider bookCollider;

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Transform originalParent;
    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");
    private static readonly int PageNumber = Animator.StringToHash("PageNumber");
    private int pageIndexL;
    private int pageIndexR;
    private int currentSoundIndex = 0;

    private void Awake()
    {
        // Sayfa materyali kopyalama
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial = new Material(bookPagesMaterial);
            if (bookSkinnedMeshRenderer != null)
            {
                Material[] materials = bookSkinnedMeshRenderer.materials;
                if (bookMaterialIndex < materials.Length)
                {
                    materials[bookMaterialIndex] = bookPagesMaterial;
                    bookSkinnedMeshRenderer.materials = materials;
                }
            }
        }
        else if (bookSkinnedMeshRenderer != null)
        {
            // Fallback
            Material[] materials = bookSkinnedMeshRenderer.sharedMaterials;
            if (bookMaterialIndex < materials.Length && materials[bookMaterialIndex] != null)
            {
                bookPagesMaterial = new Material(materials[bookMaterialIndex]);
                materials[bookMaterialIndex] = bookPagesMaterial;
                bookSkinnedMeshRenderer.materials = materials;
            }
        }

        if (pageTurnMaterial != null)
        {
            pageTurnMaterial = new Material(pageTurnMaterial);
            if (pageFlipRenderer != null)
                pageFlipRenderer.material = pageTurnMaterial;
        }
    }

    private void Start()
    {
        // --- 1. OUTLINE KONTROLÜ (YENİ) ---
        // Eğer editörden atanmadıysa, alt objelerde ara
        if (outlineController == null)
        {
            outlineController = GetComponentInChildren<HDRPOutlineController>();
        }

        // --- 2. HIGHLIGHT (EMISSION) HAZIRLIĞI ---
        if (bookSkinnedMeshRenderer != null)
        {
            originalMaterials = bookSkinnedMeshRenderer.materials;

            if (bookMaterialIndex >= 0 && bookMaterialIndex < originalMaterials.Length)
            {
                bookPagesMaterial = originalMaterials[bookMaterialIndex];
            }

            highlightMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                highlightMaterials[i] = new Material(originalMaterials[i]);
                highlightMaterials[i].EnableKeyword("_EMISSION");

                Color finalEmission = highlightColor * emissionIntensity;
                if (highlightMaterials[i].HasProperty("_EmissiveColor"))
                {
                    highlightMaterials[i].SetColor("_EmissiveColor", finalEmission);
                }
                else if (highlightMaterials[i].HasProperty("_EmissionColor"))
                {
                    highlightMaterials[i].SetColor("_EmissionColor", finalEmission);
                }
                highlightMaterials[i].globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.None;
            }
        }
        else
        {
            Debug.LogError(
                "InteractableBook: 'bookSkinnedMeshRenderer' atanmamış! Highlight çalışmaz."
            );
        }

        // --- DİĞER BAŞLANGIÇ KODLARI ---
        interactionCollider = GetComponent<BoxCollider>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider>();

        if (bookCollider == null)
            bookCollider = GetComponentInChildren<MeshCollider>();

        if (bookAnimator == null)
            bookAnimator = GetComponent<Animator>();
        if (bookAnimator != null)
        {
            bookAnimator.SetBool(IsOpenBool, isOpen);
            bookAnimator.SetInteger(PageNumber, currentPage);
        }

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

        if (bookUI != null)
            bookUI.SetActive(false);
        if (pageFlipObject != null)
            pageFlipObject.SetActive(false);

        InitializePages();
    }

    private void InitializePages()
    {
        pageIndexL = 0;
        pageIndexR = 1;
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial.SetFloat("_PageCount", totalPages);
            bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
            bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        }

        if (pageTurnMaterial != null)
            pageTurnMaterial.SetFloat("_PageCount", totalPages);
    }

    private void Update()
    {
        if (isOpen && !isAnimating)
        {
            HandlePageInput();
            if (isPasswordBook && !hasPasswordBeenFound && Input.GetMouseButtonDown(0))
                CheckForPasswordClick();

            if (playerGameScript != null)
                playerGameScript.ExternalStaminaRegen(Time.deltaTime);
        }

        if (isOpen && Input.GetKeyDown(KeyCode.F))
        {
            if (!isAnimating)
                StartCoroutine(CloseBook());
        }
    }

    // ========================================================================
    // 💡 ODAKLANMA (HIGHLIGHT / OUTLINE) MANTIĞI (GÜNCELLENDİ)
    // ========================================================================
    public void OnFocus()
    {
        // Kitap açıksa veya animasyon oynuyorsa odaklanma yapma
        if (isOpen || isAnimating || isFocused)
            return;

        isFocused = true;

        // 1. ÖNCELİK: Outline Scripti (Varsa bunu kullan)
        if (outlineController != null)
        {
            outlineController.ToggleOutline(true);
        }
        // 2. ÖNCELİK: Eski Emission Sistemi (Outline yoksa bunu kullan)
        else if (bookSkinnedMeshRenderer != null && highlightMaterials != null)
        {
            bookSkinnedMeshRenderer.materials = highlightMaterials;
        }
    }

    public void OnLoseFocus()
    {
        if (!isFocused)
            return;

        isFocused = false;

        // 1. ÖNCELİK: Outline Scripti
        if (outlineController != null)
        {
            outlineController.ToggleOutline(false);
        }
        // 2. ÖNCELİK: Eski Emission Sistemi
        else if (bookSkinnedMeshRenderer != null && originalMaterials != null)
        {
            bookSkinnedMeshRenderer.materials = originalMaterials;
        }
    }

    public void Interact()
    {
        if (isAnimating || isOpen)
            return;
        OnLoseFocus(); // Etkileşime girince söndür
        StartCoroutine(OpenBook());
    }

    public string GetInteractionPrompt()
    {
        if (isAnimating)
            return "";
        return isOpen ? "[F] Kitabı Kapat" : "[Sol Tık] Kitabı Aç";
    }

    private IEnumerator OpenBook()
    {
        isAnimating = true;
        isOpen = true;

        if (ControlsUIManager.Instance != null)
        {
            ControlsUIManager.Instance.ShowMachineUI(ControlsUIManager.MachineType.Book);
        }

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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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

        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(OpenTrigger);
            bookAnimator.SetBool(IsOpenBool, true);
        }

        PlaySound(bookOpenSound);

        yield return new WaitForSeconds(animationDuration);

        pageIndexL = 0;
        pageIndexR = 1;
        currentPage = 1;

        UpdateBookPagesMaterial();
        if (bookUI != null)
        {
            bookUI.SetActive(true);
            UpdatePageUI();
        }

        isAnimating = false;
    }

    private IEnumerator CloseBook()
    {
        isAnimating = true;
        isOpen = false;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.HideControls();
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        if (bookUI != null)
            bookUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(CloseTrigger);
            bookAnimator.SetBool(IsOpenBool, false);
        }
        PlaySound(bookCloseSound);
        yield return new WaitForSeconds(animationDuration);

        Vector3 targetWorldPosition;
        Quaternion targetWorldRotation;
        if (originalParent != null)
        {
            targetWorldPosition = originalParent.TransformPoint(originalLocalPosition);
            targetWorldRotation = originalParent.rotation * originalLocalRotation;
        }
        else
        {
            targetWorldPosition = originalLocalPosition;
            targetWorldRotation = originalLocalRotation;
        }

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

        // --- KONTROLLERİ GERİ AÇMA (DÜZELTİLEN KISIM) ---
        if (playerAnimator != null)
            playerAnimator.enabled = true;
        if (playerController != null)
            playerController.enabled = true;
        if (playerGameScript != null)
            playerGameScript.enabled = true;

        if (playerLookScript != null)
        {
            playerLookScript.enabled = true;

            // ESC'den sonra kilitlenen kamerayı zorla aç
            if (playerLookScript is StarterAssetsInputs inputs)
            {
                inputs.cursorInputForLook = true;
            }
            else
            {
                var inputsComp = playerLookScript.GetComponent<StarterAssetsInputs>();
                if (inputsComp != null)
                {
                    inputsComp.cursorInputForLook = true;
                }
            }
        }

        if (interactionCollider != null)
            interactionCollider.enabled = true;

        currentPage = 0;
        isAnimating = false;
    }

    private void HandlePageInput()
    {
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            NextPage();
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            PreviousPage();
    }

    public void NextPage()
    {
        if (isAnimating)
            return;
        if (!allowLoop && pageIndexR >= totalPages - 1)
            return;
        StartCoroutine(PageFlip(1));
    }

    public void PreviousPage()
    {
        if (isAnimating)
            return;
        if (!allowLoop && pageIndexL <= 0)
            return;
        StartCoroutine(PageFlip(-1));
    }

    private IEnumerator PageFlip(int direction)
    {
        if (direction == 0)
            yield break;

        isAnimating = true;

        if (pageFlipSounds != null && pageFlipSounds.Length > 0)
        {
            AudioClip clipToPlay = pageFlipSounds[currentSoundIndex];
            PlaySound(clipToPlay);
            currentSoundIndex = (currentSoundIndex + 1) % pageFlipSounds.Length;
        }

        if (pageFlipObject != null)
            pageFlipObject.SetActive(true);

        if (pageTurnMaterial != null)
        {
            if (direction > 0)
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexR);
            else
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexL - 1);
        }

        float t = 0f;
        float flipSpeed = 1f / pageFlipDuration;
        bool indicesUpdated = false;

        while (t < 1f)
        {
            t += Time.deltaTime * flipSpeed;
            t = Mathf.Clamp01(t);
            float v = t * t * t * (t * (t * 6f - 15f) + 10f);
            float flipAmount = (direction > 0) ? v : 1f - v;

            if (pageTurnMaterial != null)
                pageTurnMaterial.SetFloat("_PageFlip", flipAmount);

            if (t >= 0.5f && !indicesUpdated)
            {
                UpdatePageIndices(direction);
                UpdateBookPagesMaterial();
                indicesUpdated = true;
            }

            yield return null;
        }

        if (!indicesUpdated)
        {
            UpdatePageIndices(direction);
            UpdateBookPagesMaterial();
        }

        if (pageFlipObject != null)
            pageFlipObject.SetActive(false);

        currentPage = direction > 0 ? currentPage + 1 : currentPage - 1;
        UpdatePageUI();

        isAnimating = false;
    }

    private void UpdatePageIndices(int direction)
    {
        if (direction > 0)
        {
            pageIndexL += 2;
            pageIndexR += 2;
        }
        else
        {
            pageIndexL -= 2;
            pageIndexR -= 2;
        }

        if (allowLoop)
        {
            pageIndexL = (pageIndexL + totalPages) % totalPages;
            pageIndexR = (pageIndexR + totalPages) % totalPages;
        }
    }

    private void UpdateBookPagesMaterial()
    {
        if (bookPagesMaterial == null)
            return;
        bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
        bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
    }

    private void UpdatePageUI()
    {
        if (pageNumberText != null)
            pageNumberText.text = $"{pageIndexL}-{pageIndexR}";
    }

    private void CheckForPasswordClick()
    {
        if (pageIndexL != passwordPage && pageIndexR != passwordPage)
            return;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider == bookCollider)
            {
                Vector2 uv = hit.textureCoord;
                bool hitRightPage = uv.x > 0.5f;
                bool isTargetPage =
                    (hitRightPage && pageIndexR == passwordPage)
                    || (!hitRightPage && pageIndexL == passwordPage);
                if (isTargetPage)
                {
                    float pageLocalU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                    Vector2 pageUV = new Vector2(pageLocalU, uv.y);
                    if (passwordHotspotUV.Contains(pageUV))
                        TriggerPasswordFind();
                }
            }
        }
    }

    private void TriggerPasswordFind()
    {
        if (hasPasswordBeenFound || !isPasswordBook)
            return;
        hasPasswordBeenFound = true;
        PasswordManager.Instance.DiscoverClue(passwordID);
        PlaySound(passwordFoundSound);
        if (NotebookUI.Instance != null)
            NotebookUI.Instance.ShowPasswordNotification(passwordID);
        if (MegaphoneSystem.Instance != null)
            MegaphoneSystem.Instance.OnNotepadPickedUp();
        StartCoroutine(CloseBook());
    }

    public void AssignPassword(string newPasswordID, int locationIndex)
    {
        if (!canContainPassword || bookIdentity == null)
            return;
        if (locationIndex >= bookIdentity.possibleLocations.Count)
            return;
        isPasswordBook = true;
        passwordID = newPasswordID;
        var locEntry = bookIdentity.possibleLocations[locationIndex];
        passwordPage = locEntry.pageIndex;
        passwordHotspotUV = locEntry.hotspotUV;
        hasPasswordBeenFound = false;
    }

    public void ClearPassword()
    {
        isPasswordBook = false;
        passwordID = "";
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
            StartCoroutine(CloseBook());
    }

    private void OnDrawGizmosSelected()
    {
        if (!isPasswordBook || passwordHotspotUV == null)
            return;
        Gizmos.color = new Color(1.0f, 0f, 0f, 0.7f);
        Gizmos.matrix = transform.localToWorldMatrix;
        bool isRightPage = (passwordPage % 2) != 0;
        float pageCenterX = isRightPage ? (-singlePageSize.x / 2.0f) : (singlePageSize.x / 2.0f);
        Vector3 hotspotCenter =
            new Vector3(pageCenterX, 0, 0)
            + new Vector3(
                (passwordHotspotUV.x + passwordHotspotUV.width / 2f - 0.5f) * singlePageSize.x,
                gizmoYOffset,
                (passwordHotspotUV.y + passwordHotspotUV.height / 2f - 0.5f) * singlePageSize.y
            );
        Gizmos.DrawWireCube(
            hotspotCenter,
            new Vector3(
                passwordHotspotUV.width * singlePageSize.x,
                0.001f,
                passwordHotspotUV.height * singlePageSize.y
            )
        );
    }
}
