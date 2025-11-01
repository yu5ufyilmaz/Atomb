using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Şifre konumu için esnek, serileştirilebilir sınıf
[System.Serializable]
public class PasswordLocation
{
    [Tooltip("Şifrenin bulunduğu sayfa numarası (Örn: 2, 3, 5).")]
    public int pageNumber = 2;

    [Tooltip("Sayfanın 0-1 aralığındaki UV koordinatlarında hotspot alanı (X, Y, Width, Height).")]
    public Rect hotspotUV = new Rect(0.5f, 0.5f, 0.2f, 0.2f);
    
    [Tooltip("Bu şifre için kullanılacak özel sayfa dokusu (Texture2D).")]
    public Texture2D specialPageTexture;
}

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
    
    [Tooltip("Bu kitap için olası tüm şifre konumları (sayfa, hotspot, doku).")]
    [SerializeField] private List<PasswordLocation> potentialPasswordLocations;
    
    [SerializeField] private string passwordID = "INFINITY_=_123"; 
    [SerializeField] private AudioClip passwordFoundSound; 
    
    // Çalışma zamanında kullanılan aktif şifre konumu (Rastgele seçilir)
    private int activePasswordPage; 
    private Rect activePasswordHotspotUV;
    private Texture2D activePasswordPageTexture; 
    private Texture originalBookPagesTexture;
    
    // Shader Property ID'leri
    // _PagesTex, BookPages.shadergraph dosyasındaki doku ismine karşılık gelir
    private static readonly int PagesTexID = Shader.PropertyToID("_PagesTex"); 

    [Header("Gizmo Settings")]
    [Tooltip("Tek bir sayfanın modelinizin local space'indeki fiziksel boyutu (Genişlik, Yükseklik)")]
    [SerializeField] private Vector2 singlePageSize = new Vector2(0.16f, 0.32f); 

    [Tooltip("Gizmo'nun Z-fighting (titreşme) yapmaması için sayfadan ne kadar önde çizileceği")]
    [SerializeField] private float gizmoYOffset = 0.005f;
    
    private bool hasPasswordBeenFound = false; 
    [SerializeField] private Collider bookCollider; 
    private Camera mainCamera;
    private BoxCollider interactionCollider;
    
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
    
    private void Start()
    {
        interactionCollider  = GetComponent<BoxCollider>();
        if (interactionCollider == null)
        {
           Debug.LogWarning("Interaction Collider not found"); 
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
            Debug.LogWarning($"InteractableBook ({gameObject.name}): 'Player Look Script' atanmamış.");
        }
        
        if (playerController == null)
        {
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        }
        
        mainCamera = Camera.main;
        if (cameraTransform == null && mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }
        else if (mainCamera == null)
        {
             Debug.LogError("Ana kamera (Main Camera) bulunamadı! Lütfen 'cameraTransform' değişkenini Inspector'dan atayın.");
        }
        
        if (bookCollider == null)
        {
            Debug.LogError("InteractableBook scripti, şifre tıklaması için bir Collider bileşenine ihtiyaç duyuyor!", this);
        }
        
        if (bookUI != null)
        {
            bookUI.SetActive(false);
        }
        
        // Materyali bulma ve orijinal dokuyu kaydetme
        if (bookSkinnedMeshRenderer != null && bookPagesMaterial == null)
        {
            Material[] materials = bookSkinnedMeshRenderer.materials;
            if (bookMaterialIndex < materials.Length)
            {
                bookPagesMaterial = materials[bookMaterialIndex];
            }
        }
        
        if (bookPagesMaterial != null)
        {
            originalBookPagesTexture = bookPagesMaterial.GetTexture(PagesTexID);
        }
        
        // Şifre kitabının rastgele konumunu belirle
        if (isPasswordBook)
        {
            InitializePasswordLocation();
        }
        
        if (pageFlipObject != null)
        {
            pageFlipObject.SetActive(false);
        }
        
        InitializePages();
    }
    
    private void InitializePasswordLocation()
    {
        if (potentialPasswordLocations == null || potentialPasswordLocations.Count == 0)
        {
            Debug.LogError($"InteractableBook ({gameObject.name}): 'potentialPasswordLocations' listesi boş. Şifre bulunamayacak!", this);
            activePasswordPage = -1;
            activePasswordHotspotUV = new Rect(0,0,0,0);
            activePasswordPageTexture = null; 
            return;
        }

        // Listeden rastgele bir konum seç
        int randomIndex = UnityEngine.Random.Range(0, potentialPasswordLocations.Count);
        PasswordLocation selectedLocation = potentialPasswordLocations[randomIndex];

        // Aktif değerleri ata
        activePasswordPage = selectedLocation.pageNumber;
        activePasswordHotspotUV = selectedLocation.hotspotUV;
        activePasswordPageTexture = selectedLocation.specialPageTexture;
        
        if (activePasswordPageTexture == null)
        {
            Debug.LogWarning($"InteractableBook ({gameObject.name}): Seçilen şifre konumu (Sayfa {activePasswordPage}) için 'specialPageTexture' atanmamış!");
        }
        
        Debug.Log($"Kitap ({gameObject.name}) için aktif şifre konumu rastgele seçildi: Sayfa {activePasswordPage}, Hotspot: {activePasswordHotspotUV}");
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
        
        UpdateBookPagesMaterial(); // Başlangıçta dokuları yükle (orijinal doku)
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

        // activePasswordPage kontrolü
        if (pageIndexL != activePasswordPage && pageIndexR != activePasswordPage)
        {
            Debug.Log($"-> Tıklama algılandı, ancak açık olan sayfalar ({pageIndexL}-{pageIndexR}) şifre sayfası ({activePasswordPage}) değil. İşlem durduruldu.");
            return; 
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider == bookCollider)
            {
                Vector2 uv = hit.textureCoord;
                
                // Hangi sayfaya tıklandığını belirle
                bool hitRightPage = uv.x > 0.5f;
                
                // Sayfanın kendi UV aralığına dönüştür (0'dan 1'e)
                float pageU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                float pageV = uv.y;
                Vector2 pageUV = new Vector2(pageU, pageV);

                // Kontrol: Doğru sayfaya mı tıklandı ve hotspot içinde mi?
                if (hitRightPage && pageIndexR == activePasswordPage)
                {
                    if (activePasswordHotspotUV.Contains(pageUV))
                    {
                        Debug.Log("   -> !!! ŞİFRE BULUNDU! Tıklama hotspot'un içinde.");
                        TriggerPasswordFind();
                    }
                }
                else if (!hitRightPage && pageIndexL == activePasswordPage)
                {
                    if (activePasswordHotspotUV.Contains(pageUV))
                    {
                        Debug.Log("   -> !!! ŞİFRE BULUNDU! Tıklama hotspot'un içinde.");
                        TriggerPasswordFind();
                    }
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
        
        return isOpen ? "[ESC] Kitabı Kapat" : "[Sol Tık] Kitabı Aç";
    }

    private IEnumerator OpenBook()
    {
        isAnimating = true;
        isOpen = true;
        
        if (interactionCollider != null) interactionCollider.enabled = false;
        // Oyuncu kontrollerini devre dışı bırak
        if (playerController != null) playerController.enabled = false;
        if (playerLookScript != null) playerLookScript.enabled = false;
        if (playerAnimator != null) playerAnimator.enabled = false;
        
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Kitabı kameraya yaklaştırma animasyonu
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
 
        // Kitap açılma animasyonu
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
        UpdateBookPagesMaterial(); // Doku kontrolü
        
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
        
        // Kitap kapanma animasyonu
        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(CloseTrigger);
            bookAnimator.SetBool(IsOpenBool, false);
        }
        
        PlaySound(bookCloseSound);
        
        yield return new WaitForSeconds(animationDuration);
        
        
        Vector3 targetWorldPosition;
        Quaternion targetWorldRotation;
        
        // Orijinal pozisyona geri dönme (world space'te)
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
        
        transform.SetParent(originalParent, true); // Parent'ı geri ata
        
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.position = Vector3.Lerp(startWorldPos, targetWorldPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRotation, smoothT);
            yield return null;
        }
        
        // Son pozisyon ve rotasyonu tam olarak lokal değerlere ayarla
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        
        // Oyuncu kontrollerini etkinleştir
        if (playerAnimator != null) playerAnimator.enabled = true;
        if (playerController != null) playerController.enabled = true;
        if (playerLookScript != null) playerLookScript.enabled = true;
        if (interactionCollider != null) interactionCollider.enabled = true;
        
        currentPage = 0;
        UpdateBookPagesMaterial(); // Materyali sıfırla (orijinal dokuya geri dön)
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
        if (isAnimating) return;
        
        if (!allowLoop && pageIndexR >= totalPages - 1)
        {
            Debug.Log("Son sayfa!");
            return;
        }
            
        StartCoroutine(PageFlip(1)); 
    }

    public void PreviousPage()
    {
        if (isAnimating) return;
        
        // Sınır kontrolü
        if (!allowLoop && pageIndexL <= 0)
        {
            Debug.Log("İlk sayfa!");
            return;
        }
            
        StartCoroutine(PageFlip(-1)); 
    }

    private IEnumerator PageFlip(int direction)
    {
        if (direction == 0)
            yield break;
            
        isAnimating = true;
        
        if (pageFlipObject != null) pageFlipObject.SetActive(true);
        
        // ... (pageTurnMaterial hazırlık kodları aynı kalır)
        
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
                UpdateBookPagesMaterial(); // Sayfa indexleri değişti, dokuyu güncelle
                indicesUpdated = true;
            }
            
            yield return null;
        }
        
        if (!indicesUpdated)
        {
            UpdatePageIndices(direction);
            UpdateBookPagesMaterial();
        }
        
        if (pageFlipObject != null) pageFlipObject.SetActive(false);
        
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
    
    private void UpdateBookPagesMaterial()
    {
        if (bookPagesMaterial == null)
            return;
        
        bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
        bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        
        // Sayfa Dokusunu Dinamik Olarak Değiştirme
        if (isPasswordBook && activePasswordPage != -1 && 
            (pageIndexL == activePasswordPage || pageIndexR == activePasswordPage) &&
            activePasswordPageTexture != null)
        {
            // Şifre sayfası görünürde: Özel dokuyu kullan
            bookPagesMaterial.SetTexture(PagesTexID, activePasswordPageTexture);
        }
        else
        {
            // Normal sayfa veya kitap kapalı: Orijinal dokuyu geri yükle
            if (bookPagesMaterial.GetTexture(PagesTexID) != originalBookPagesTexture)
            {
                bookPagesMaterial.SetTexture(PagesTexID, originalBookPagesTexture);
            }
        }
        
        // Debug.Log($"Book Pages Updated - Left: {pageIndexL}, Right: {pageIndexR}");
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

    // GIZMO ÇİZİMİ (Sadece Editor'de görsel yardım için)
    private void OnDrawGizmosSelected()
    {
        // Gizmo çizimi potansiyel konumlar listesini kullanır
        if (!isPasswordBook || potentialPasswordLocations == null || potentialPasswordLocations.Count == 0)
            return;
        
        Gizmos.matrix = transform.localToWorldMatrix;
        
        // Oyun oynanıyorsa, sadece aktif olanı yeşil çiz
        if (Application.isPlaying)
        {
            DrawPasswordHotspotGizmo(activePasswordPage, activePasswordHotspotUV, new Color(0.0f, 1.0f, 0.0f, 0.8f)); // Yeşil: Aktif Konum
        }
        else // Editörde ayarlanıyorsa, tüm potansiyel konumları kırmızı çiz
        {
            foreach (var location in potentialPasswordLocations)
            {
                DrawPasswordHotspotGizmo(location.pageNumber, location.hotspotUV, new Color(1.0f, 0f, 0f, 0.5f)); // Kırmızı: Potansiyel Konumlar
            }
        }
    }

    private void DrawPasswordHotspotGizmo(int page, Rect hotspot, Color color)
    {
        Gizmos.color = color; 
        
        if (hotspot.width == 0 || hotspot.height == 0) return;
        
        // Kitap modelinize göre sayfanın sağda mı solda mı olduğunu belirleyin.
        // Kitap sol sayfa indexi çift, sağ sayfa indexi tek (sayfa 0-1, 2-3, 4-5) varsayılıyor.
        bool isRightPage = (page % 2) != 0;
        
        // Hotspot'un bulunacağı sayfanın lokal merkez pozisyonunu hesapla
        // NOT: singlePageSize.x/2 pozisyonu, sayfanın ortasındaki cilt çizgisine göredir.
        // Yönün doğru olması için (Uv.x < 0.5 ise sol sayfa) -singlePageSize.x/2 sol, +singlePageSize.x/2 sağ olmalıdır.
        float pageCenterX = isRightPage ? (singlePageSize.x / 2.0f) : (-singlePageSize.x / 2.0f);
        Vector3 pageCenterLocalPos = new Vector3(pageCenterX, 0, 0);

        // Hotspot'un UV koordinatlarından lokal 3D ofsetini hesapla (UV: 0-1 aralığında)
        float hotspotCenter_UV_X = hotspot.x + (hotspot.width / 2.0f);
        float hotspotCenter_UV_Y = hotspot.y + (hotspot.height / 2.0f);
        
        // 0.5'ten farkı alarak merkezi 0 olan ofseti bul
        float hotspotOffsetX_UV = hotspotCenter_UV_X - 0.5f; 
        float hotspotOffsetY_UV = hotspotCenter_UV_Y - 0.5f;
        
        // Lokal 3D ofsetini fiziksel boyutla çarparak hesapla
        float hotspotOffsetX_Local = hotspotOffsetX_UV * singlePageSize.x;
        float hotspotOffsetZ_Local = hotspotOffsetY_UV * singlePageSize.y; // Z ekseni, sayfa Yüksekliği olarak kullanılıyor
        
        // Sayfa pozisyonu + Hotspot ofseti
        Vector3 hotspotCenter = pageCenterLocalPos + new Vector3(hotspotOffsetX_Local, gizmoYOffset, hotspotOffsetZ_Local);

        // Hotspot'un 3D boyutunu hesapla
        float hotspotWidth_Local = hotspot.width * singlePageSize.x;
        float hotspotHeight_Local = hotspot.height * singlePageSize.y;
        Vector3 hotspotSize = new Vector3(hotspotWidth_Local, 0.001f, hotspotHeight_Local);
        
        Gizmos.DrawWireCube(hotspotCenter, hotspotSize);
    }
}
