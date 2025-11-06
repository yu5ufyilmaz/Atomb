using UnityEngine;
using System.Collections;

public class InteractableBook : MonoBehaviour, IInteractable
{
    [Header("Book Settings")]
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private float animationDuration = 1.0f;
    
    [Header("Book State")]
    [SerializeField] private bool isOpen = false;
    private bool isAnimating = false;
    
    [Header("Pages")]
    [SerializeField] private int totalPages = 8;
    [SerializeField] private int currentPage = 0;
    [SerializeField] private float pageFlipDuration = 0.8f;
    [SerializeField] private bool allowLoop = false;
    
    [Header("Materials & Shaders")]
    [SerializeField] private Material bookPagesMaterial;
    [SerializeField] private SkinnedMeshRenderer bookSkinnedMeshRenderer;
    [SerializeField] private int bookMaterialIndex = 0;
    
    [Header("Page Flip Effect")]
    [SerializeField] private GameObject pageFlipObject;
    [SerializeField] private MeshRenderer pageFlipRenderer;
    [SerializeField] private Material pageTurnMaterial;
    
    [Header("Audio")]
    [SerializeField] private AudioClip bookOpenSound;
    [SerializeField] private AudioClip bookCloseSound;
    [SerializeField] private AudioClip pageFlipSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Character Controller")]
    [SerializeField] private UnityEngine.CharacterController playerController;
    [SerializeField] private MonoBehaviour playerLookScript;
    [SerializeField] private Animator playerAnimator;
    
    [Header("UI References")]
    [SerializeField] private GameObject bookUI;
    [SerializeField] private TMPro.TextMeshProUGUI pageNumberText;
    
    [Header("View Settings")]
    [SerializeField] private Transform cameraTransform; 
    [SerializeField] private Vector3 viewPositionOffset = new Vector3(0, 0, 0.8f); 
    [SerializeField] private Vector3 viewRotationOffset = new Vector3(0, 0, 0); 
    [SerializeField] private float moveDuration = 0.5f; 
    
    [Header("Password Settings")]
    [SerializeField] private bool isPasswordBook = false; 
    [SerializeField] private int passwordPage = 2; 
    [SerializeField] private string passwordID = "INFINITY_=_123"; 
    [SerializeField] private AudioClip passwordFoundSound; 
    
    
    [Space(10)]
    [Header("Hotspot Helper (Sadece Editörde)")]
    [Tooltip("Buradaki slider'ları değiştirerek yandaki Scene penceresinde Gizmo'yu canlı olarak ayarlayın.")]
    [SerializeField] [Range(0, 1)] private float hotspot_X = 0.5f;
    [SerializeField] [Range(0, 1)] private float hotspot_Y = 0.5f;
    [SerializeField] [Range(0, 1)] private float hotspot_Width = 0.2f;
    [SerializeField] [Range(0, 1)] private float hotspot_Height = 0.2f;

    [Tooltip("Ayarlamayı bitirdikten sonra buradaki Rect değerlerini kopyalayıp ScriptableObject'e yapıştırın.")]
    [SerializeField] private Rect passwordHotspotUV = new Rect(0.5f, 0.5f, 0.2f, 0.2f);
    
    [Header("Gizmo Settings")]
    [Tooltip("Tek bir sayfanın modelinizin local space'indeki fiziksel boyutu (Genişlik, Yükseklik)")]
    [SerializeField] private Vector2 singlePageSize = new Vector2(0.16f, 0.32f); 

    [Tooltip("Gizmo'nun Z-fighting (titreşme) yapmaması için sayfadan ne kadar önde çizileceği")]
    [SerializeField] private float gizmoYOffset = 0.005f;
    
    private bool hasPasswordBeenFound = false; 
    [SerializeField] private Collider bookCollider; 
    private Camera mainCamera;
    [SerializeField]private BoxCollider interactionCollider;
    
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Transform originalParent;
    
    // Animator parametreleri
    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");
    private static readonly int PageNumber = Animator.StringToHash("PageNumber");
    
    // Sayfa indexleri (BookPages shader için)
    private int pageIndexL;
    private int pageIndexR;
    
    private static readonly int AnimIDSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIDJump = Animator.StringToHash("Jump");
    private static readonly int AnimIDFreeFall = Animator.StringToHash("FreeFall");

    private void Start()
    {
        interactionCollider  = GetComponent<BoxCollider>();
        if (interactionCollider == null) 
        {
            Debug.LogWarning($"InteractableBook ({gameObject.name}): 'Interaction Collider' (BoxCollider) Inspector'dan atanmamış veya obje üzerinde bulunamadı!", this); 
        }
        
        if (bookAnimator == null)
        {
            bookAnimator = GetComponent<Animator>();
        }
        
        if (bookAnimator != null)
        {
            bookAnimator.SetBool(IsOpenBool, isOpen);
            bookAnimator.SetInteger(PageNumber, currentPage);
        }
        if (playerLookScript == null)
        {
            Debug.LogWarning($"InteractableBook ({gameObject.name}): 'Player Look Script' atanmamış. Kitap açılınca fare (kamera) hareketini tam olarak durdurmak için, oyuncunun 'MouseLook', 'ThirdPersonController' veya 'StarterAssetsInputs' gibi script'ini Inspector'dan bu alana sürüklemeniz önerilir.");
        }
        
        if (playerController == null)
        {
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        }
        
        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogError("Ana kamera (Main Camera) bulunamadı! Lütfen 'cameraTransform' değişkenini Inspector'dan atayın.");
            }
        }
        
        if (bookCollider == null)
        {
            Debug.LogError("InteractableBook scripti, şifre tıklaması için bir Collider bileşenine ihtiyaç duyuyor!", this);
        }
        mainCamera = Camera.main; // Ana kamerayı bul

        if (cameraTransform == null && mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }
        
        if (bookUI != null)
        {
            bookUI.SetActive(false);
        }
        
        if (bookSkinnedMeshRenderer != null && bookPagesMaterial == null)
        {
            Material[] materials = bookSkinnedMeshRenderer.materials;
            if (bookMaterialIndex < materials.Length)
            {
                bookPagesMaterial = materials[bookMaterialIndex];
            }
        }
        if (playerAnimator == null && playerController != null)
        {
            playerAnimator = playerController.GetComponent<Animator>();
        }
        
        if (pageFlipObject != null)
        {
            pageFlipObject.SetActive(false);
        }
        
        if (pageFlipRenderer != null && pageTurnMaterial == null)
        {
            pageTurnMaterial = pageFlipRenderer.material;
        }
        
        InitializePages();
    }

    private void InitializePages()
    {
        // Başlangıç: Kitap kapalı, ilk sayfa
        pageIndexL = 0;
        pageIndexR = 1;
        
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial.SetFloat("_PageCount", totalPages);
            bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
            bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        }
        
        if (pageTurnMaterial != null)
        {
            pageTurnMaterial.SetFloat("_PageCount", totalPages);
        }
        
        Debug.Log($"Pages Initialized - Left: {pageIndexL}, Right: {pageIndexR}");
    }

    private void Update()
    {
        if (isOpen && !isAnimating)
        {
            HandlePageInput();
            if (isPasswordBook && !hasPasswordBeenFound && Input.GetMouseButtonDown(0))
            {
                CheckForPasswordClick();
            }
        }
        
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isAnimating)
            {
                StartCoroutine(CloseBook());
            }
        }
    }

    private void CheckForPasswordClick()
    {
        Debug.Log("CheckForPasswordClick() çağrıldı (Sol Tık Algılandı).");

        if (pageIndexL != passwordPage && pageIndexR != passwordPage)
        {
            Debug.Log("-> Tıklama algılandı, ancak açık olan sayfalar ({pageIndexL}-{pageIndexR}) şifre sayfası ({passwordPage}) değil. İşlem durduruldu.");
            return; 
        }

        Debug.Log("-> Açık sayfa, şifre sayfası. Işın gönderiliyor...");
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow, 5.0f);
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log($"IŞIN ÇARPTI: Obje = {hit.collider.gameObject.name}, Layer = {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

            if (hit.collider == bookCollider)
            {
                Debug.Log("-> BAŞARILI: Işın kitabın kendi collider'ına ('{bookCollider.name}') çarptı. UV koordinatları alınıyor...");
                
                Vector2 uv = hit.textureCoord;
                Debug.Log($"   -> Ham UV (textureCoord): {uv}"); // Ham UV
                
                // Hangi sayfaya tıklandığını belirle
                bool hitRightPage = uv.x > 0.5f;
                
                float pageU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                float pageV = uv.y;
                Vector2 pageUV = new Vector2(pageU, pageV);

                Debug.Log($"   -> Hesaplanmış Sayfa UV: {pageUV} (Sağ Sayfa mı: {hitRightPage})"); // Hesaplanmış UV

                // Kontrol: Doğru sayfaya mı tıklandı?
                if (hitRightPage && pageIndexR == passwordPage)
                {
                    Debug.Log("   -> Kontrol: Sağ sayfa (Doğru şifre sayfası)");
                    if (passwordHotspotUV.Contains(pageUV))
                    {
                        Debug.Log("   -> !!! ŞİFRE BULUNDU! Tıklama hotspot'un içinde.");
                        TriggerPasswordFind();
                    }
                    else
                    {
                        Debug.Log("   -> BAŞARISIZ: Tıklama hotspot'un dışında kaldı.");
                    }
                }
                else if (!hitRightPage && pageIndexL == passwordPage)
                {
                    Debug.Log("   -> Kontrol: Sol sayfa (Doğru şifre sayfası)");
                    if (passwordHotspotUV.Contains(pageUV))
                    {
                        Debug.Log("   -> !!! ŞİFRE BULUNDU! Tıklama hotspot'un içinde.");
                        TriggerPasswordFind();
                    }
                    else
                    {
                        Debug.Log("   -> BAŞARISIZ: Tıklama hotspot'un dışında kaldı.");
                    }
                }
                else
                {
                    // Tıklanan sayfa (sol/sağ) şifre sayfasıyla eşleşmedi
                    Debug.Log("   -> Kontrol: Tıklanan sayfa (Sol/Sağ) beklenen şifre sayfasıyla ({passwordPage}) eşleşmedi.");
                }
            }
            else
            {
                // DEBUG 4: Kitaba çarpmadı
                Debug.Log("-> HATA: Işın bir şeye çarptı, ama bu objenin beklenen collider'ı ('{bookCollider.name}') DEĞİLDİ.");
            }
        }
        else
        {
            // DEBUG 5: Işın boşa gitti
            Debug.Log("IŞIN ÇARPMADI: Tıklama boşa gitti (hiçbir collider'a çarpmadı).");
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
        
        return isOpen ? "[ESC] Kitabı Kapat" : "[Sol Tık] Kitabı Aç";
    }

    private IEnumerator OpenBook()
    {
        isAnimating = true;
        isOpen = true;
        
        if (interactionCollider != null) interactionCollider.enabled = false;
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        if (playerLookScript != null)
        {
            playerLookScript.enabled = false;
        }
        
        if (playerAnimator != null)
        {
            playerAnimator.enabled = false;
        }
        
        
        
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
        
        if (bookUI != null)
        {
            bookUI.SetActive(false);
        }
        
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
        {
            playerAnimator.enabled = true;
        }
        
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        if (playerLookScript != null)
        {
            playerLookScript.enabled = true;
        }
        if (interactionCollider != null) interactionCollider.enabled = true;
        
        currentPage = 0;
        isAnimating = false;
    }
    
    private void TriggerPasswordFind()
    {
        if (hasPasswordBeenFound || !isPasswordBook)
            return;

        hasPasswordBeenFound = true;
        
        PasswordManager.Instance.AddFoundPassword(passwordID);
        
        PlaySound(passwordFoundSound);
        
        NotebookUI.Instance.ShowPasswordNotification(passwordID);
        
        StartCoroutine(CloseBook());
    }

    private void HandlePageInput()
    {
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextPage();
        }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            PreviousPage();
        }
    }

    public void NextPage()
    {
        if (isAnimating)
            return;
        
        if (!allowLoop && pageIndexR >= totalPages - 1)
        {
            Debug.Log("Son sayfa!");
            return;
        }
            
        StartCoroutine(PageFlip(1)); // 1 = ileri
    }

    public void PreviousPage()
    {
        if (isAnimating)
            return;
        
        // Sınır kontrolü
        if (!allowLoop && pageIndexL <= 0)
        {
            Debug.Log("İlk sayfa!");
            return;
        }
            
        StartCoroutine(PageFlip(-1)); // -1 = geri
    }

    private IEnumerator PageFlip(int direction)
    {
        if (direction == 0)
            yield break;
            
        isAnimating = true;
        
        if (pageFlipObject != null)
        {
            pageFlipObject.SetActive(true);
        }
        
        if (pageTurnMaterial != null)
        {
            if (direction > 0)
            {
                // Sağ sayfayı (pageIndexR) çevir
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexR);
            }
            else
            {
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexL - 1);
            }
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
            {
                pageTurnMaterial.SetFloat("_PageFlip", flipAmount);
            }
            
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
        {
            pageFlipObject.SetActive(false);
        }
        
        PlaySound(pageFlipSound);
        currentPage = direction > 0 ? currentPage + 1 : currentPage - 1;
        UpdatePageUI();
        
        
        isAnimating = false;
    }
    
    private void UpdatePageIndices(int direction)
    {
        if (direction > 0)
        {
            // İleri: Her iki index +2
            pageIndexL += 2;
            pageIndexR += 2;
        }
        else
        {
            // Geri: Her iki index -2
            pageIndexL -= 2;
            pageIndexR -= 2;
        }
        
        // Loop kontrolü (opsiyonel)
        if (allowLoop)
        {
            pageIndexL = (pageIndexL + totalPages) % totalPages;
            pageIndexR = (pageIndexR + totalPages) % totalPages;
        }
    }
    
    // PasswordManager tarafından çağrılacak
    public void AssignPassword(PasswordData data)
    {
        isPasswordBook = true;
        passwordID = data.passwordID;
        passwordPage = data.passwordPage;
        passwordHotspotUV = data.passwordHotspotUV; // Hotspot'u atadık!
        hasPasswordBeenFound = false;

        // Texture'ları atama
        // Not: Bu, materyalin bir "instance"ını (kopyasını) oluşturur.
        // Eğer materyali paylaşıyorsanız bu gereklidir.
        // Zaten Start() içinde materyali alıyorsanız, direkt set edebilirsiniz.

        // BookPages shader'ına texture'ı ata
        if (bookPagesMaterial != null)
        {
            // Shader'daki property adı "_PagesTex" 
            bookPagesMaterial.SetTexture("_PagesTex", data.pageTexture); 
        }

        // PageTurn shader'ına texture'ı ata
        if (pageTurnMaterial != null)
        {
            // Shader'daki property adı "_PagesTex" 
            pageTurnMaterial.SetTexture("_PagesTex", data.pageTexture);
        }

        Debug.Log($"{gameObject.name} kitabına {passwordID} şifresi atandı.");
    }

// PasswordManager tarafından çağrılacak
    public void ClearPassword()
    {
        isPasswordBook = false;
        passwordID = "";
        hasPasswordBeenFound = false;

       
        // if (bookPagesMaterial != null && defaultPageTexture != null)
        // {
        //     bookPagesMaterial.SetTexture("_PagesTex", defaultPageTexture);
        // }
        // if (pageTurnMaterial != null && defaultPageTexture != null)
        // {
        //     pageTurnMaterial.SetTexture("_PagesTex", defaultPageTexture);
        // }
    }
    
    private void UpdateBookPagesMaterial()
    {
        if (bookPagesMaterial == null)
            return;
        
        bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
        bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        
        Debug.Log($"Book Pages Updated - Left: {pageIndexL}, Right: {pageIndexR}");
    }

    private void UpdatePageUI()
    {
        if (pageNumberText != null)
        {
            // Sol ve sağ sayfa numaralarını göster
            pageNumberText.text = $"{pageIndexL}-{pageIndexR}";
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Dışarıdan erişim
    public int GetCurrentPage()
    {
        return currentPage;
    }

    public int GetTotalPages()
    {
        return totalPages;
    }

    public bool IsFlipping()
    {
        return isAnimating;
    }

    // Inspector'da değişiklik yapıldığında
    private void OnValidate()
    {
        if (passwordHotspotUV.x != hotspot_X) passwordHotspotUV.x = hotspot_X;
        if (passwordHotspotUV.y != hotspot_Y) passwordHotspotUV.y = hotspot_Y;
        if (passwordHotspotUV.width != hotspot_Width) passwordHotspotUV.width = hotspot_Width;
        if (passwordHotspotUV.height != hotspot_Height) passwordHotspotUV.height = hotspot_Height;
        
        if (bookPagesMaterial != null && Application.isPlaying)
        {
            UpdateBookPagesMaterial();
        }
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
        
        Vector3 hotspotCenter = pageCenterLocalPos + new Vector3(hotspotOffsetX_Local, gizmoYOffset, hotspotOffsetZ_Local);

        
        float hotspotWidth_Local = passwordHotspotUV.width * singlePageSize.x;
        float hotspotHeight_Local = passwordHotspotUV.height * singlePageSize.y;
        Vector3 hotspotSize = new Vector3(hotspotWidth_Local, 0.001f, hotspotHeight_Local);
        
        Gizmos.DrawWireCube(hotspotCenter, hotspotSize);
    }
}