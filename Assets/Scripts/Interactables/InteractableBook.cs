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
    private class RendererHighlightData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] highlightMaterials;
    }

    private List<RendererHighlightData> allRenderersData = new List<RendererHighlightData>();
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

    // --- COLLIDER FIX VARIABLES ---
    private Mesh originalMesh;
    private Mesh bakedMesh;
    // ------------------------------

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

    // RAM Optimizasyonu: Child collider'lar için önbellek
    private Collider[] cachedChildColliders;

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
        if (outlineController == null)
            outlineController = GetComponentInChildren<HDRPOutlineController>();

        // --- COLLIDER FIX ---
        if (bookCollider != null)
        {
            // Orijinal mesh'i sakla (kapalı hali)
            // Eğer MeshCollider'ın sharedMesh'i null ise (örn. BoxCollider kullanılıyorsa) bu adım atlanabilir,
            // ama kodun geri kalanında MeshCollider varsayılıyor.
            if (bookCollider is MeshCollider mc)
            {
                originalMesh = mc.sharedMesh;
                bakedMesh = new Mesh();
                bakedMesh.name = "BakedBookCollider";
            }
        }
        // --------------------

        // --- 2. HIGHLIGHT (EMISSION) HAZIRLIĞI (GÜNCELLENDİ) ---
        // Kitabın altındaki TÜM Renderer'ları bul (Kapak, Sayfalar, vs.)
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            // Outline/Flip parçalarını veya particle'ları hariç tutmak istersen buraya if ekleyebilirsin
            if (r == pageFlipRenderer)
                continue; // Örn: Sayfa çevirme efektini parlatma

            RendererHighlightData data = new RendererHighlightData();
            data.renderer = r;
            data.originalMaterials = r.materials; // Orijinal materyalleri sakla

            // Highlight materyallerini oluştur
            data.highlightMaterials = new Material[data.originalMaterials.Length];
            for (int i = 0; i < data.originalMaterials.Length; i++)
            {
                Material mat = new Material(data.originalMaterials[i]);
                mat.EnableKeyword("_EMISSION");

                // HDRP Emission Rengi Ayarı
                Color finalEmission = highlightColor * emissionIntensity;
                if (mat.HasProperty("_EmissiveColor"))
                    mat.SetColor("_EmissiveColor", finalEmission);
                else if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", finalEmission);

                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                data.highlightMaterials[i] = mat;
            }

            allRenderersData.Add(data);
        }

        // --- DİĞER BAŞLANGIÇ KODLARI ---
        interactionCollider = GetComponent<BoxCollider>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider>();

        if (bookCollider == null)
            bookCollider = GetComponentInChildren<MeshCollider>();

        // RAM Optimizasyonu: Child collider'ları önbelleğe al
        cachedChildColliders = GetComponentsInChildren<Collider>();

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

    public void OnFocus()
    {
        if (isOpen || isAnimating || isFocused)
            return;
        isFocused = true;

        // 1. ÖNCELİK: Outline Scripti (İstersen bunu tamamen silebilirsin)
        if (outlineController != null)
        {
            outlineController.ToggleOutline(true);
        }
        // 2. ÖNCELİK: Gelişmiş Emission Sistemi (Tüm parçalar parlar)
        else
        {
            foreach (var data in allRenderersData)
            {
                if (data.renderer != null)
                    data.renderer.materials = data.highlightMaterials;
            }
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
        // 2. ÖNCELİK: Gelişmiş Emission Sistemi
        else
        {
            foreach (var data in allRenderersData)
            {
                if (data.renderer != null)
                    data.renderer.materials = data.originalMaterials;
            }
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

        // Animasyonun TAMAMEN bitmesini bekle
        yield return new WaitForSeconds(animationDuration);
        
        // Animator'ın "Open" state'ine geçmesini bekle
        if (bookAnimator != null)
        {
            // Animator'ın güncellenmesi için bir frame bekle
            yield return null;
            
            // Animasyonun normalizedTime'ı 1.0'a ulaşana kadar bekle (tam bitiş)
            AnimatorStateInfo stateInfo = bookAnimator.GetCurrentAnimatorStateInfo(0);
            while (stateInfo.normalizedTime < 1.0f || bookAnimator.IsInTransition(0))
            {
                yield return null;
                stateInfo = bookAnimator.GetCurrentAnimatorStateInfo(0);
            }
        }

        pageIndexL = 0;
        pageIndexR = 1;
        currentPage = 1;

        UpdateBookPagesMaterial();
        if (bookUI != null)
        {
            bookUI.SetActive(true);
            UpdatePageUI();
        }

        // --- COLLIDER FIX: Mesh'i Bake Et ve Engelleyenleri Kapat ---
        if (bookSkinnedMeshRenderer != null && bookCollider is MeshCollider mc)
        {
            if (bakedMesh == null)
            {
                bakedMesh = new Mesh();
                bakedMesh.name = "BakedBookCollider";
            }
            
            bookSkinnedMeshRenderer.BakeMesh(bakedMesh, true); // true = scale dahil et
            mc.sharedMesh = bakedMesh;
            
            // DİĞER COLLIDER'LARI KAPAT (Örn: Kapak veya Statik Mesh Collider raycast'i engellemesin)
            // RAM Optimizasyonu: Önbelleklenmiş collider'ları kullan
            foreach (var col in cachedChildColliders)
            {
                if (col != null && col != bookCollider && col != interactionCollider)
                    col.enabled = false;
            }
        }
        // ------------------------------------

        isAnimating = false;
    }

    private IEnumerator CloseBook()
    {
        // --- COLLIDER FIX: Mesh'i Geri Al ve Colliderları Aç ---
        // Kitap kapanırken eski (kapalı) mesh'e dön
        if (bookCollider is MeshCollider mc && originalMesh != null)
        {
            mc.sharedMesh = originalMesh;
        }

        // Diğer colliderları geri aç
        // RAM Optimizasyonu: Önbelleklenmiş collider'ları kullan
        foreach (var col in cachedChildColliders)
        {
            if (col != null && col != bookCollider && col != interactionCollider)
                col.enabled = true;
        }
        // ------------------------------------

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
        // 1. Senin elindeki değişkeni güncelle (Hafızadaki referans)
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
            bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        }

        // 2. ASIL ÇÖZÜM: O an Renderer'ın üzerinde takılı olan "CANLI" materyali bul ve güncelle
        // Çünkü Highlight sistemi materyalleri sök-tak yaparken referans kopmuş olabilir.
        if (bookSkinnedMeshRenderer != null)
        {
            // .materials çağrısı o anki güncel kopyaları getirir
            Material[] currentMats = bookSkinnedMeshRenderer.materials;

            // İndeks hatası olmasın diye kontrol
            if (bookMaterialIndex >= 0 && bookMaterialIndex < currentMats.Length)
            {
                Material liveMaterial = currentMats[bookMaterialIndex];

                // Eğer canlı materyal boş değilse, değerleri ona da bas
                if (liveMaterial != null)
                {
                    liveMaterial.SetFloat("_PageIndexL", pageIndexL);
                    liveMaterial.SetFloat("_PageIndexR", pageIndexR);
                }
            }
        }
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

        if (bookCollider == null)
        {
            Debug.LogWarning("bookCollider is null!");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        // BUILD FIX: Physics.Raycast kullan (Collider.Raycast build'de güvenilir değil)
        // Tüm collider'lara raycast at ve kitabın collider'ına isabet edeni bul
        RaycastHit hit;
        bool hitBook = false;
        
        // Önce doğrudan bookCollider'a raycast dene
        if (bookCollider.Raycast(ray, out hit, 100f))
        {
            hitBook = true;
        }
        // Fallback: Physics.Raycast ile tüm collider'lara at
        else if (Physics.Raycast(ray, out hit, 100f))
        {
            // Kitabın herhangi bir child collider'ına isabet etti mi kontrol et
            if (hit.collider == bookCollider || hit.collider.transform.IsChildOf(transform))
            {
                hitBook = true;
            }
        }
        
        if (hitBook)
        {
            Debug.Log($"Raycast Hit BOOK: {hit.collider.name}");
            
            Vector2 uv = hit.textureCoord;
            
            // BUILD FIX: Mesh "Read/Write Enabled" kapalıysa textureCoord (0,0) döner
            // Bu durumda dünya koordinatlarından UV hesapla
            if (uv == Vector2.zero)
            {
                Debug.Log("textureCoord is zero, calculating UV from world position...");
                uv = CalculateUVFromWorldPosition(hit.point);
            }
            
            Debug.Log($"Hit Book UV: {uv}");

            // Mesh UV'sinden hangi sayfaya tıklandığını belirle
            // Mesh UV: 0-0.5 = sol sayfa, 0.5-1 = sağ sayfa
            bool hitRightPage = uv.x > 0.5f;
            
            // Şu an görüntülenen sayfa indexleri
            int clickedPageIndex = hitRightPage ? pageIndexR : pageIndexL;
            
            bool isTargetPage = (clickedPageIndex == passwordPage);
            
            if (isTargetPage)
            {
                // Mesh UV'sini sayfa-local UV'ye çevir (0-1 aralığına normalize et)
                // Sol sayfa: uv.x 0-0.5 -> 0-1
                // Sağ sayfa: uv.x 0.5-1 -> 0-1
                float pageLocalU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                Vector2 pageUV = new Vector2(pageLocalU, uv.y);
                
                Debug.Log($"Password Page: {passwordPage} | Clicked Page: {clickedPageIndex}");
                Debug.Log($"Calculated PageUV: {pageUV} | Hotspot: {passwordHotspotUV}");
                Debug.Log($"Hotspot Contains Check: x={pageUV.x} in [{passwordHotspotUV.xMin}-{passwordHotspotUV.xMax}], y={pageUV.y} in [{passwordHotspotUV.yMin}-{passwordHotspotUV.yMax}]");

                if (passwordHotspotUV.Contains(pageUV))
                {
                    Debug.Log("PASSWORD FOUND!");
                    TriggerPasswordFind();
                }
                else
                {
                    Debug.Log("Click missed hotspot.");
                }
            }
            else
            {
                Debug.Log($"Wrong Page clicked. Target: {passwordPage}, Clicked: {clickedPageIndex}");
            }
        }
        else
        {
            Debug.Log("Raycast did not hit bookCollider.");
        }
    }
    
    /// <summary>
    /// Mesh "Read/Write Enabled" kapalı olduğunda dünya koordinatlarından UV hesaplar.
    /// Bu, build'lerde textureCoord'un (0,0) dönmesi sorununu çözer.
    /// </summary>
    private Vector2 CalculateUVFromWorldPosition(Vector3 worldPoint)
    {
        // Dünya noktasını kitabın local koordinat sistemine çevir
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        
        // Kitabın bounds'unu al
        Bounds bounds;
        if (bookSkinnedMeshRenderer != null)
        {
            bounds = bookSkinnedMeshRenderer.localBounds;
        }
        else if (bookCollider != null)
        {
            // Collider bounds'unu local'e çevir
            bounds = bookCollider.bounds;
            bounds.center = transform.InverseTransformPoint(bounds.center);
            bounds.size = transform.InverseTransformVector(bounds.size);
            // Negatif değerleri düzelt
            bounds.size = new Vector3(Mathf.Abs(bounds.size.x), Mathf.Abs(bounds.size.y), Mathf.Abs(bounds.size.z));
        }
        else
        {
            Debug.LogWarning("Cannot calculate UV: No renderer or collider found!");
            return Vector2.zero;
        }
        
        // Local noktayı 0-1 aralığına normalize et
        // X ekseni: Kitabın sol kenarından sağ kenarına (0 = sol, 1 = sağ)
        // Z ekseni: Kitabın alt kenarından üst kenarına (0 = alt, 1 = üst) - Y olarak kullanılacak
        float normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, localPoint.x);
        float normalizedY = Mathf.InverseLerp(bounds.min.z, bounds.max.z, localPoint.z);
        
        // Clamp to 0-1 range
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedY = Mathf.Clamp01(normalizedY);
        
        Debug.Log($"UV Calculation: localPoint={localPoint}, bounds.min=({bounds.min.x}, {bounds.min.z}), bounds.max=({bounds.max.x}, {bounds.max.z}), result=({normalizedX}, {normalizedY})");
        
        return new Vector2(normalizedX, normalizedY);
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
        
        Debug.Log($"[InteractableBook] Password Assigned: {passwordID} at Page {passwordPage}. Hotspot: {passwordHotspotUV}");
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
        if (!isPasswordBook || passwordHotspotUV.width <= 0 || passwordHotspotUV.height <= 0)
            return;

        Gizmos.color = new Color(1.0f, 0f, 0f, 0.7f);

        // Mesh Renderer'ın local bounds'unu al
        Bounds localBounds;
        if (bookSkinnedMeshRenderer != null)
        {
            localBounds = bookSkinnedMeshRenderer.localBounds;
        }
        else if (bookCollider != null)
        {
            // Collider varsa onun bounds'unu kullan
            localBounds = bookCollider.bounds;
            // World bounds'u local'e çevir
            localBounds.center = transform.InverseTransformPoint(localBounds.center);
        }
        else
        {
            return;
        }

        // Kitap açıkken sol ve sağ sayfaların pozisyonlarını hesapla
        // Mesh UV: Sol sayfa = 0-0.5, Sağ sayfa = 0.5-1
        // passwordPage çift ise sol sayfa, tek ise sağ sayfa
        bool isRightPage = (passwordPage % 2) != 0;
        
        // Kitabın x ekseninde yarısı sol, yarısı sağ sayfa
        float halfWidth = localBounds.size.x / 2f;
        
        // Sayfa merkezi (local koordinatlarda)
        float pageCenterX = isRightPage 
            ? localBounds.center.x + halfWidth / 2f  // Sağ tarafa offset
            : localBounds.center.x - halfWidth / 2f; // Sol tarafa offset

        // Hotspot'un sayfa üzerindeki konumu
        // passwordHotspotUV: 0-1 arasında normalize edilmiş koordinatlar
        float hotspotLocalX = (passwordHotspotUV.x + passwordHotspotUV.width / 2f - 0.5f) * halfWidth;
        float hotspotLocalZ = (passwordHotspotUV.y + passwordHotspotUV.height / 2f - 0.5f) * localBounds.size.z;

        Vector3 hotspotCenter = new Vector3(
            pageCenterX + hotspotLocalX,
            localBounds.max.y + gizmoYOffset,
            localBounds.center.z + hotspotLocalZ
        );

        Vector3 hotspotSize = new Vector3(
            passwordHotspotUV.width * halfWidth,
            0.001f,
            passwordHotspotUV.height * localBounds.size.z
        );

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(hotspotCenter, hotspotSize);
        
        // Ek olarak dolu bir cube da çiz (daha görünür olması için)
        Gizmos.color = new Color(1.0f, 0f, 0f, 0.3f);
        Gizmos.DrawCube(hotspotCenter, hotspotSize);
    }
}