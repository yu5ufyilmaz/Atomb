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
    
    [Header("UI References")]
    [SerializeField] private GameObject bookUI;
    [SerializeField] private TMPro.TextMeshProUGUI pageNumberText;
    
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
        // Animator kontrolü
        if (bookAnimator == null)
        {
            bookAnimator = GetComponent<Animator>();
        }
        
        if (bookAnimator != null)
        {
            bookAnimator.SetBool(IsOpenBool, isOpen);
            bookAnimator.SetInteger(PageNumber, currentPage);
        }
        
        // Player controller bul
        if (playerController == null)
        {
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        }
        
        // UI'yi gizle
        if (bookUI != null)
        {
            bookUI.SetActive(false);
        }
        
        // Skinned Mesh Renderer'dan material'ı al
        if (bookSkinnedMeshRenderer != null && bookPagesMaterial == null)
        {
            Material[] materials = bookSkinnedMeshRenderer.materials;
            if (bookMaterialIndex < materials.Length)
            {
                bookPagesMaterial = materials[bookMaterialIndex];
            }
        }
        
        // Page flip objesini gizle
        if (pageFlipObject != null)
        {
            pageFlipObject.SetActive(false);
        }
        
        // PageTurn material'ını al
        if (pageFlipRenderer != null && pageTurnMaterial == null)
        {
            pageTurnMaterial = pageFlipRenderer.material;
        }
        
        // Shader propertylerini başlat
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
        // Kitap açıkken sayfa çevirme kontrolü
        if (isOpen && !isAnimating)
        {
            HandlePageInput();
        }
        
        // ESC ile kitabı kapat
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Interact();
        }
    }

    public void Interact()
    {
        if (isAnimating)
            return;
            
        if (isOpen)
        {
            StartCoroutine(CloseBook());
        }
        else
        {
            StartCoroutine(OpenBook());
        }
    }

    public string GetInteractionPrompt()
    {
        if (isAnimating)
            return "";
        
        return isOpen ? "[ESC] Kitabı Kapat" : "[E] Kitabı Aç";
    }

    private IEnumerator OpenBook()
    {
        isAnimating = true;
        isOpen = true;
        
        // CharacterController'ı pasif yap
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Fare imlecini göster
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Kitap açma animasyonu
        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(OpenTrigger);
            bookAnimator.SetBool(IsOpenBool, true);
        }
        
        PlaySound(bookOpenSound);
        
        // Animasyon süresince bekle
        yield return new WaitForSeconds(animationDuration);
        
        // İlk sayfaya dön
        pageIndexL = 0;
        pageIndexR = 1;
        currentPage = 1;
        UpdateBookPagesMaterial();
        
        // UI'yi göster
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
        
        // UI'yi gizle
        if (bookUI != null)
        {
            bookUI.SetActive(false);
        }
        
        // Fare imlecini gizle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Kitap kapama animasyonu
        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(CloseTrigger);
            bookAnimator.SetBool(IsOpenBool, false);
        }
        
        PlaySound(bookCloseSound);
        
        // Animasyon süresince bekle
        yield return new WaitForSeconds(animationDuration);
        
        // CharacterController'ı tekrar aktif yap
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        currentPage = 0;
        isAnimating = false;
    }

    private void HandlePageInput()
    {
        // Sonraki sayfa
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextPage();
        }
        // Önceki sayfa
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            PreviousPage();
        }
    }

    public void NextPage()
    {
        if (isAnimating)
            return;
        
        // Sınır kontrolü
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
        
        // Page flip objesini aktif et
        if (pageFlipObject != null)
        {
            pageFlipObject.SetActive(true);
        }
        
        // PageTurn material'ını ayarla
        if (pageTurnMaterial != null)
        {
            if (direction > 0)
            {
                // İleri: Sağ sayfayı (pageIndexR) çevir
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexR);
            }
            else
            {
                // Geri: Sol sayfanın bir öncesini (pageIndexL - 1) çevir
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexL - 1);
            }
        }
        
        // Sayfa çevirme animasyonu
        float t = 0f;
        float flipSpeed = 1f / pageFlipDuration;
        bool indicesUpdated = false; // İndexlerin bir kez güncellenmesini garanti et
        
        while (t < 1f)
        {
            t += Time.deltaTime * flipSpeed;
            t = Mathf.Clamp01(t);
            
            // SmootherStep eğrisi
            float v = t * t * t * (t * (t * 6f - 15f) + 10f);
            float flipAmount = (direction > 0) ? v : 1f - v;
            
            // PageTurn shader'ını güncelle
            if (pageTurnMaterial != null)
            {
                pageTurnMaterial.SetFloat("_PageFlip", flipAmount);
            }
            
            // Animasyonun ortasında BookPages'i güncelle (sadece bir kez)
            if (t >= 0.5f && !indicesUpdated)
            {
                UpdatePageIndices(direction);
                UpdateBookPagesMaterial();
                indicesUpdated = true;
            }
            
            yield return null;
        }
        
        // Eğer hala güncellenmemişse (çok hızlı animasyon durumunda), şimdi güncelle
        if (!indicesUpdated)
        {
            UpdatePageIndices(direction);
            UpdateBookPagesMaterial();
        }
        
        // Page flip objesini gizle
        if (pageFlipObject != null)
        {
            pageFlipObject.SetActive(false);
        }
        
        // Ses ve UI güncelle
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
        if (bookPagesMaterial != null && Application.isPlaying)
        {
            UpdateBookPagesMaterial();
        }
    }
}