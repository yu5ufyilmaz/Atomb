using System.Collections;
using StarterAssets;
using UnityEngine;

public class InteractableBook : MonoBehaviour, IInteractable, IForceExitable
{
    // ... (Tüm değişkenler aynı kalsın) ...
    [Header("Book Settings")]
    [SerializeField]
    private Animator bookAnimator;

    [SerializeField]
    private float animationDuration = 1.0f;

    [Header("Book State")]
    [SerializeField]
    private bool isOpen = false;
    private bool isAnimating = false;

    [Header("Pages")]
    [SerializeField]
    private int totalPages = 8;

    [SerializeField]
    private int currentPage = 0;

    [SerializeField]
    private float pageFlipDuration = 0.8f;

    [SerializeField]
    private bool allowLoop = false;

    [Header("Materials & Shaders")]
    [SerializeField]
    private Material bookPagesMaterial;

    [SerializeField]
    private SkinnedMeshRenderer bookSkinnedMeshRenderer;

    [SerializeField]
    private int bookMaterialIndex = 0;

    [Header("Page Flip Effect")]
    [SerializeField]
    private GameObject pageFlipObject;

    [SerializeField]
    private MeshRenderer pageFlipRenderer;

    [SerializeField]
    private Material pageTurnMaterial;

    [Header("Audio")]
    [SerializeField]
    private AudioClip bookOpenSound;

    [SerializeField]
    private AudioClip bookCloseSound;

    [SerializeField]
    private AudioClip pageFlipSound;

    [SerializeField]
    private AudioSource audioSource;

    [Header("Character Controller")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;

    [SerializeField]
    private MonoBehaviour playerLookScript;

    [SerializeField]
    private Animator playerAnimator;

    [Header("UI References")]
    [SerializeField]
    private GameObject bookUI;

    [SerializeField]
    private TMPro.TextMeshProUGUI pageNumberText;

    [Header("View Settings")]
    [SerializeField]
    private Transform cameraTransform;

    [SerializeField]
    private Vector3 viewPositionOffset = new Vector3(0, 0, 0.8f);

    [SerializeField]
    private Vector3 viewRotationOffset = new Vector3(0, 0, 0);

    [SerializeField]
    private float moveDuration = 0.5f;

    [Header("Password Settings")]
    [SerializeField]
    private bool isPasswordBook = false;

    [SerializeField]
    private int passwordPage = 2;

    [SerializeField]
    private string passwordID = "INFINITY_=_123";

    [SerializeField]
    private AudioClip passwordFoundSound;

    [Header("Hotspot Helper")]
    [Range(0, 1)]
    [SerializeField]
    private float hotspot_X = 0.5f;

    [Range(0, 1)]
    [SerializeField]
    private float hotspot_Y = 0.5f;

    [Range(0, 1)]
    [SerializeField]
    private float hotspot_Width = 0.2f;

    [Range(0, 1)]
    [SerializeField]
    private float hotspot_Height = 0.2f;

    [SerializeField]
    private Rect passwordHotspotUV = new Rect(0.5f, 0.5f, 0.2f, 0.2f);

    [Header("Gizmo Settings")]
    [SerializeField]
    private Vector2 singlePageSize = new Vector2(0.16f, 0.32f);

    [SerializeField]
    private float gizmoYOffset = 0.005f;
    private bool hasPasswordBeenFound = false;

    [SerializeField]
    private Collider bookCollider;
    private Camera mainCamera;

    [SerializeField]
    private BoxCollider interactionCollider;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Transform originalParent;
    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");
    private static readonly int PageNumber = Animator.StringToHash("PageNumber");
    private int pageIndexL;
    private int pageIndexR;

    // ... (Awake ve Start aynı kalsın) ...
    private void Awake()
    {
        Material basePagesMaterial = null;
        if (bookPagesMaterial != null)
            basePagesMaterial = bookPagesMaterial;
        else if (bookSkinnedMeshRenderer != null)
        {
            Material[] materials = bookSkinnedMeshRenderer.sharedMaterials;
            if (bookMaterialIndex < materials.Length)
                basePagesMaterial = materials[bookMaterialIndex];
        }
        if (basePagesMaterial != null)
        {
            Material materialInstance = new Material(basePagesMaterial);
            bookPagesMaterial = materialInstance;
            if (bookSkinnedMeshRenderer != null)
            {
                Material[] materials = bookSkinnedMeshRenderer.materials;
                if (bookMaterialIndex < materials.Length)
                {
                    materials[bookMaterialIndex] = materialInstance;
                    bookSkinnedMeshRenderer.materials = materials;
                }
            }
        }
        Material baseTurnMaterial = null;
        if (pageTurnMaterial != null)
            baseTurnMaterial = pageTurnMaterial;
        else if (pageFlipRenderer != null)
            baseTurnMaterial = pageFlipRenderer.sharedMaterial;
        if (baseTurnMaterial != null)
        {
            pageTurnMaterial = new Material(baseTurnMaterial);
            if (pageFlipRenderer != null)
                pageFlipRenderer.material = pageTurnMaterial;
        }
    }

    private void Start()
    {
        interactionCollider = GetComponent<BoxCollider>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider>();
        if (bookAnimator == null)
            bookAnimator = GetComponent<Animator>();
        if (bookAnimator != null)
        {
            bookAnimator.SetBool(IsOpenBool, isOpen);
            bookAnimator.SetInteger(PageNumber, currentPage);
        }
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController != null)
            playerGameScript = playerController.GetComponent<StarterAssets.CharacterController>();
        if (playerLookScript == null && playerController != null)
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
        if (cameraTransform == null)
            if (Camera.main != null)
                cameraTransform = Camera.main.transform;
        mainCamera = Camera.main;
        if (cameraTransform == null && mainCamera != null)
            cameraTransform = mainCamera.transform;
        if (bookUI != null)
            bookUI.SetActive(false);
        if (playerAnimator == null && playerController != null)
            playerAnimator = playerController.GetComponent<Animator>();
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

    // ... (Update aynı kalsın) ...
    private void Update()
    {
        if (isOpen && !isAnimating)
        {
            HandlePageInput();
            if (isPasswordBook && !hasPasswordBeenFound && Input.GetMouseButtonDown(0))
                CheckForPasswordClick();
        }
        if (isOpen && Input.GetKeyDown(KeyCode.F))
        {
            if (!isAnimating)
                StartCoroutine(CloseBook());
        }
    }

    // ... (CheckForPasswordClick, Interact, GetInteractionPrompt aynı) ...
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
                float pageU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                float pageV = uv.y;
                Vector2 pageUV = new Vector2(pageU, pageV);
                if (hitRightPage && pageIndexR == passwordPage)
                {
                    if (passwordHotspotUV.Contains(pageUV))
                        TriggerPasswordFind();
                }
                else if (!hitRightPage && pageIndexL == passwordPage)
                {
                    if (passwordHotspotUV.Contains(pageUV))
                        TriggerPasswordFind();
                }
            }
        }
    }

    public void Interact()
    {
        if (isAnimating || isOpen)
            return;
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

        // --- GM KAYIT ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;
        // ----------------

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

        // --- GM KAYIT SİL ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        // --------------------

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

        if (playerAnimator != null)
            playerAnimator.enabled = true;
        if (playerController != null)
            playerController.enabled = true;
        if (playerGameScript != null)
            playerGameScript.enabled = true;
        if (playerLookScript != null)
            playerLookScript.enabled = true;
        if (interactionCollider != null)
            interactionCollider.enabled = true;

        currentPage = 0;
        isAnimating = false;
    }

    // ... (Sayfa çevirme, password işlemleri vb. AYNEN KALSIN) ...

    private void TriggerPasswordFind()
    {
        if (hasPasswordBeenFound || !isPasswordBook)
            return;
        hasPasswordBeenFound = true;
        PasswordManager.Instance.DiscoverClue(passwordID);
        PlaySound(passwordFoundSound);
        NotebookUI.Instance.ShowPasswordNotification(passwordID);
        StartCoroutine(CloseBook());
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
        PlaySound(pageFlipSound);
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

    public void AssignPassword(PasswordData data)
    {
        isPasswordBook = true;
        passwordID = data.passwordID;
        passwordPage = data.passwordPage;
        passwordHotspotUV = data.passwordHotspotUV;
        hasPasswordBeenFound = false;
        this.totalPages = data.totalPages;
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial.SetTexture("_PagesTex", data.pageTexture);
            bookPagesMaterial.SetFloat("_PageCount", this.totalPages);
        }
        if (pageTurnMaterial != null)
        {
            pageTurnMaterial.SetTexture("_PagesTex", data.pageTexture);
            pageTurnMaterial.SetFloat("_PageCount", this.totalPages);
        }
    }

    public void ClearPassword()
    {
        isPasswordBook = false;
        passwordID = "";
        hasPasswordBeenFound = false;
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

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public int GetCurrentPage() => currentPage;

    public int GetTotalPages() => totalPages;

    public bool IsFlipping() => isAnimating;

    private void OnValidate()
    {
        if (passwordHotspotUV.x != hotspot_X)
            passwordHotspotUV.x = hotspot_X;
        if (passwordHotspotUV.y != hotspot_Y)
            passwordHotspotUV.y = hotspot_Y;
        if (passwordHotspotUV.width != hotspot_Width)
            passwordHotspotUV.width = hotspot_Width;
        if (passwordHotspotUV.height != hotspot_Height)
            passwordHotspotUV.height = hotspot_Height;
        if (bookPagesMaterial != null && Application.isPlaying)
            UpdateBookPagesMaterial();
    }

    private void OnDrawGizmosSelected()
    {
        if (!isPasswordBook || passwordHotspotUV == null)
            return;
        Gizmos.color = new Color(1.0f, 0f, 0f, 0.7f);
        Gizmos.matrix = transform.localToWorldMatrix;
        bool isRightPage = (passwordPage % 2) != 0;
        float pageCenterX = isRightPage ? (-singlePageSize.x / 2.0f) : (singlePageSize.x / 2.0f);
        Vector3 pageCenterLocalPos = new Vector3(pageCenterX, 0, 0);
        float hotspotCenter_UV_X = passwordHotspotUV.x + (passwordHotspotUV.width / 2.0f);
        float hotspotCenter_UV_Y = passwordHotspotUV.y + (passwordHotspotUV.height / 2.0f);
        float hotspotOffsetX_UV = hotspotCenter_UV_X - 0.5f;
        float hotspotOffsetY_UV = hotspotCenter_UV_Y - 0.5f;
        float hotspotOffsetX_Local = hotspotOffsetX_UV * singlePageSize.x;
        float hotspotOffsetZ_Local = hotspotOffsetY_UV * singlePageSize.y;
        Vector3 hotspotCenter =
            pageCenterLocalPos
            + new Vector3(hotspotOffsetX_Local, gizmoYOffset, hotspotOffsetZ_Local);
        float hotspotWidth_Local = passwordHotspotUV.width * singlePageSize.x;
        float hotspotHeight_Local = passwordHotspotUV.height * singlePageSize.y;
        Vector3 hotspotSize = new Vector3(hotspotWidth_Local, 0.001f, hotspotHeight_Local);
        Gizmos.DrawWireCube(hotspotCenter, hotspotSize);
    }

    // --- IFORCEEXITABLE ---
    public void ForceExit()
    {
        if (isOpen && !isAnimating)
            StartCoroutine(CloseBook());
    }
}
