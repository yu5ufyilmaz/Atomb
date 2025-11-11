using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text; // Şifre string'ini oluşturmak için

public class InteractableTuringMachine : MonoBehaviour, IInteractable
{
    [Header("Player Control")]
    [Tooltip("Oyuncunun ana CharacterController'ı")]
    [SerializeField] private UnityEngine.CharacterController playerController;
    [Tooltip("Oyuncunun fare/kamera kontrol script'i (örn: StarterAssetsInputs)")]
    [SerializeField] private MonoBehaviour playerLookScript;
    [Tooltip("Oturma animasyonu için oyuncunun Animator'ü")]
    [SerializeField] private Animator playerAnimator;
    private bool isInteracting = false;

    // --- YENİ ---
    [Header("Camera Control")]
    [Tooltip("Kameranın etkileşim sırasında kilitleneceği pozisyon ve rotasyonu belirleyen boş bir Transform objesi")]
    [SerializeField] private Transform cameraViewTarget;
    [Tooltip("Kameranın makineye/oyuncuya geçiş süresi (saniye)")]
    [SerializeField] private float cameraMoveDuration = 0.5f;
    private Transform cinemachineCameraTarget; // Genellikle "PlayerFollowCamera"
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    // --- YENİ BİTİŞ ---

    [Header("Makine Bileşenleri (Transforms)")]
    [Tooltip("8 adet Kelime Çarkı'nın Transform'u (Sırayla)")]
    [SerializeField] private Transform[] wordWheelModels; // 8 adet
    [Tooltip("1 adet Sembol Çarkı'nın Transform'u")]
    [SerializeField] private Transform symbolWheelModel; // 1 adet
    [Tooltip("3 adet Sayı Çarkı'nın Transform'u (Sırayla)")]
    [SerializeField] private Transform[] numberWheelModels; // 3 adet

    [Header("Makine Bileşenleri (Highlights)")]
    [Tooltip("Kelime çarkları seçiliyken gösterilecek 8 highlight objesi")]
    [SerializeField] private GameObject[] wordWheelHighlights; // 8 adet
    [Tooltip("Sembol çarkı seçiliyken gösterilecek highlight objesi")]
    [SerializeField] private GameObject symbolWheelHighlight; // 1 adet
    [Tooltip("Sayı çarkları seçiliyken gösterilecek 3 highlight objesi")]
    [SerializeField] private GameObject[] numberWheelHighlights; // 3 adet

    [Header("Çark Verileri (Modelinize göre ayarlayın)")]
    [Tooltip("Kelime çarkındaki karakterler (Modelinizdeki sırayla)")]
    [SerializeField] private string wordChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ-"; // 27 karakter
    [Tooltip("Sembol çarkındaki karakterler (Modelinizdeki sırayla)")]
    [SerializeField] private string[] symbolChars = { ">=", "+", "-", "/", "√", "%", "<=", "=", "<", ">", ".", "," };
    [Tooltip("Sayı çarkındaki karakterler (Modelinizdeki sırayla)")]
    [SerializeField] private string numberChars = "0123456789"; // 10 karakter
    
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // Çarkların dönme ekseni
    [SerializeField] private float rotationSpeed = 10f; // Slerp hızı

    [Header("Göstergeler (5 adet)")]
    [Tooltip("Şifreler doğrulandıkça yeşile dönecek 5 adet obje")]
    [SerializeField] private Renderer[] indicatorRenderers; // İstediğiniz gibi 5 adet
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material greenMaterial;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip wheelClickSound;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip failSound;
    [SerializeField] private AudioClip accessSound; // Makineye oturma
    [SerializeField] private AudioClip exitSound;   // Makineden kalkma

    // Aktif Durum
    private int currentGroup = 0; // 0=Word, 1=Symbol, 2=Number
    private int currentWordIndex = 0; // 0-7
    private int currentNumberIndex = 0; // 0-2

    // Çarkların mevcut pozisyonlarını (index olarak) sakla
    private int[] wordWheelIndices = new int[8];
    private int symbolWheelIndex = 0;
    private int[] numberWheelIndices = new int[3];

    // Animasyon için hedef rotasyonlar
    private Quaternion[] wordWheelTargets;
    private Quaternion symbolWheelTarget;
    private Quaternion[] numberWheelTargets;
    
    private Quaternion[] wordWheelInitialRots;
    private Quaternion symbolWheelInitialRot;
    private Quaternion[] numberWheelInitialRots;

    private void Start()
    {
        // Player scriptlerini bul (eğer atanmamışsa)
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerLookScript == null && playerController != null)
            playerLookScript = playerController.GetComponent<StarterAssets.StarterAssetsInputs>();
        if (playerAnimator == null && playerController != null)
            playerAnimator = playerController.GetComponent<Animator>();

        // --- YENİ ---
        // Cinemachine hedefini (PlayerFollowCamera) bul
        if (playerController != null)
        {
            var starterAssetsController = playerController.GetComponent<StarterAssets.CharacterController>();
            if (starterAssetsController != null && starterAssetsController.CinemachineCameraTarget != null)
            {
                cinemachineCameraTarget = starterAssetsController.CinemachineCameraTarget.transform;
            }
        }
        if (cinemachineCameraTarget == null)
        {
            Debug.LogError("CinemachineCameraTarget (PlayerFollowCamera) bulunamadı! Lütfen Player'daki CharacterController script'inden atandığını kontrol edin.");
        }
        if (cameraViewTarget == null)
        {
            Debug.LogError("Camera View Target atanmamış! Lütfen makineye bakacak boş bir obje atayın.");
        }
        // --- YENİ BİTİŞ ---

        wordWheelTargets = new Quaternion[wordWheelModels.Length];
        numberWheelTargets = new Quaternion[numberWheelModels.Length];
        wordWheelInitialRots = new Quaternion[wordWheelModels.Length];
        numberWheelInitialRots = new Quaternion[numberWheelModels.Length];
        for (int i = 0; i < wordWheelModels.Length; i++)
        {
            if (wordWheelModels[i] != null)
            {
                wordWheelInitialRots[i] = wordWheelModels[i].localRotation; // Başlangıcı kaydet
                wordWheelTargets[i] = wordWheelModels[i].localRotation;     // Hedefi başlangıç yap
            }
        }
        for (int i = 0; i < numberWheelModels.Length; i++)
        {
            if (numberWheelModels[i] != null)
            {
                numberWheelInitialRots[i] = numberWheelModels[i].localRotation; // Başlangıcı kaydet
                numberWheelTargets[i] = numberWheelModels[i].localRotation;     // Hedefi başlangıç yap
            }
        }

        if (symbolWheelModel != null)
        {
            symbolWheelInitialRot = symbolWheelModel.localRotation; // Başlangıcı kaydet
            symbolWheelTarget = symbolWheelModel.localRotation;     // Hedefi başlangıç yap
        }

        // Başlangıçta tüm çarkları sıfırla (Data)
        for (int i = 0; i < wordWheelIndices.Length; i++) wordWheelIndices[i] = 0;
        for (int i = 0; i < numberWheelIndices.Length; i++) numberWheelIndices[i] = 0;
        symbolWheelIndex = 0;
        
        // Başlangıçta highlight'ları gizle
        ClearAllHighlights();

        // Başlangıçta göstergeleri ayarla (Tümünü kırmızı yap)
        UpdateIndicators(0);
    }

    public void Interact()
    {
        if (isInteracting) return;
        StartCoroutine(EnterMachineView());
    }

    public string GetInteractionPrompt()
    {
        return isInteracting ? "" : "[Sol Tık] Makineyi Kullan";
    }

    // --- GÜNCELLENDİ (Kamera Animasyonu Eklendi) ---
    private IEnumerator EnterMachineView()
    {
        isInteracting = true;
        if (playerController) playerController.enabled = false;
        if (playerLookScript) playerLookScript.enabled = false;
        if (playerAnimator) 
        {
            // playerAnimator.SetTrigger("SitDownTrigger"); 
        }
            
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        PlaySound(accessSound);

        // --- YENİ Kamera Animasyonu ---
        if (cinemachineCameraTarget != null && cameraViewTarget != null)
        {
            // Kameranın mevcut parent'ını ve local pozisyonunu kaydet
            originalCameraParent = cinemachineCameraTarget.parent;
            originalCameraLocalPos = cinemachineCameraTarget.localPosition;
            originalCameraLocalRot = cinemachineCameraTarget.localRotation;
            
            // Kamerayı oyuncudan ayır
            cinemachineCameraTarget.SetParent(null, true); 

            float t = 0f;
            Vector3 startPos = cinemachineCameraTarget.position;
            Quaternion startRot = cinemachineCameraTarget.rotation;
            
            while (t < 1f)
            {
                t += Time.deltaTime / cameraMoveDuration;
                float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
                cinemachineCameraTarget.position = Vector3.Lerp(startPos, cameraViewTarget.position, smoothT);
                cinemachineCameraTarget.rotation = Quaternion.Slerp(startRot, cameraViewTarget.rotation, smoothT);
                yield return null;
            }
        }
        // --- YENİ BİTİŞ ---

        if (PasswordManager.Instance != null)
        {
            UpdateIndicators(PasswordManager.Instance.GetValidatedPasswordCount());
        }
        
        UpdateActiveWheelHighlight();
    }

    // --- GÜNCELLENDİ (Kamera Animasyonu Eklendi) ---
    private IEnumerator ExitMachineView()
    {
        isInteracting = false;
        
        PlaySound(exitSound);
        ClearAllHighlights();
        
        // --- YENİ Kamera Geri Dönüş Animasyonu ---
        if (cinemachineCameraTarget != null && originalCameraParent != null)
        {
            float t = 0f;
            Vector3 startPos = cinemachineCameraTarget.position;
            Quaternion startRot = cinemachineCameraTarget.rotation;
            
            // Geri dönülecek DÜNYA (world space) pozisyonunu hesapla
            Vector3 targetWorldPos = originalCameraParent.TransformPoint(originalCameraLocalPos);
            Quaternion targetWorldRot = originalCameraParent.rotation * originalCameraLocalRot;

            while (t < 1f)
            {
                t += Time.deltaTime / cameraMoveDuration;
                float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
                cinemachineCameraTarget.position = Vector3.Lerp(startPos, targetWorldPos, smoothT);
                cinemachineCameraTarget.rotation = Quaternion.Slerp(startRot, targetWorldRot, smoothT);
                yield return null;
            }
            
            // Kamerayı tekrar oyuncuya bağla
            cinemachineCameraTarget.SetParent(originalCameraParent, true);
            cinemachineCameraTarget.localPosition = originalCameraLocalPos;
            cinemachineCameraTarget.localRotation = originalCameraLocalRot;
        }
        // --- YENİ BİTİŞ ---

        // Kontrolleri geri ver
        if (playerController) playerController.enabled = true;
        if (playerLookScript) playerLookScript.enabled = true;
        if (playerAnimator)
        {
            // playerAnimator.SetTrigger("StandUpTrigger");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    private void Update()
    {
        if (!isInteracting) return;

        // 1. Çıkış (ESC)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        // 2. Grup Geçişi (Dikey: W/S)
        if (Input.GetKeyDown(KeyCode.W))
        {
            currentGroup = (currentGroup - 1 + 3) % 3; // (0->2, 1->0, 2->1)
            UpdateActiveWheelHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            currentGroup = (currentGroup + 1) % 3; // (0->1, 1->2, 2->0)
            UpdateActiveWheelHighlight();
        }

        // 3. Bireysel Çark Geçişi (Yatay: Q/E)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            HandleIndexChange(-1);
            UpdateActiveWheelHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            HandleIndexChange(1);
            UpdateActiveWheelHighlight();
        }
            
        // 4. Çarkları Çevirme (A/D veya Mouse Scroll)
        float rotationInput = 0f;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) rotationInput = 1f;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) rotationInput = -1f;
        
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f) rotationInput = 1f;
        if (scroll < -0.1f) rotationInput = -1f;

        if (rotationInput != 0)
        {
            RotateActiveWheel((int)rotationInput);
        }

        // 5. Şifreyi Gönderme (Enter)
        if (Input.GetKeyDown(KeyCode.Return))
        {
            CheckPassword();
        }

        // 6. Animasyon (Slerp)
        AnimateWheels();
    }
    
    // Çarkların rotasyonunu yumuşak (Slerp) bir şekilde günceller
    private void AnimateWheels()
    {
        float step = Time.deltaTime * rotationSpeed;
        for (int i = 0; i < wordWheelModels.Length; i++)
        {
            if (wordWheelModels[i] != null)
                wordWheelModels[i].localRotation = Quaternion.Slerp(wordWheelModels[i].localRotation, wordWheelTargets[i], step);
        }
        for (int i = 0; i < numberWheelModels.Length; i++)
        {
            if (numberWheelModels[i] != null)
                numberWheelModels[i].localRotation = Quaternion.Slerp(numberWheelModels[i].localRotation, numberWheelTargets[i], step);
        }
        if (symbolWheelModel != null)
            symbolWheelModel.localRotation = Quaternion.Slerp(symbolWheelModel.localRotation, symbolWheelTarget, step);
    }

    // Aktif olan yatay çark index'ini değiştirir (Q/E)
    private void HandleIndexChange(int direction)
    {
        switch (currentGroup)
        {
            case 0: // Word
                currentWordIndex = (currentWordIndex + direction + wordWheelModels.Length) % wordWheelModels.Length;
                break;
            case 1: // Symbol (Tek çark, Q/E çalışmaz)
                break;
            case 2: // Number
                currentNumberIndex = (currentNumberIndex + direction + numberWheelModels.Length) % numberWheelModels.Length;
                break;
        }
    }

    // Aktif olan çarkı çevirir (Data ve Hedef Rotasyon)
    private void RotateActiveWheel(int direction)
    {
        PlaySound(wheelClickSound);
        
        switch (currentGroup)
        {
            case 0: // Word
                int charCountW = wordChars.Length;
                wordWheelIndices[currentWordIndex] = (wordWheelIndices[currentWordIndex] + direction + charCountW) % charCountW;
                float angleW = (360f / charCountW) * wordWheelIndices[currentWordIndex];
                // Başlangıç rotasyonu ile Y ekseni rotasyonunu birleştir
                wordWheelTargets[currentWordIndex] = wordWheelInitialRots[currentWordIndex] * Quaternion.AngleAxis(angleW, rotationAxis);
                break;
                
            case 1: // Symbol
                int charCountS = symbolChars.Length; 
                symbolWheelIndex = (symbolWheelIndex + direction + charCountS) % charCountS;
                float angleS = (360f / charCountS) * symbolWheelIndex;
                // Başlangıç rotasyonu ile Y ekseni rotasyonunu birleştir
                symbolWheelTarget = symbolWheelInitialRot * Quaternion.AngleAxis(angleS, rotationAxis);
                break;
                
            case 2: // Number
                int charCountN = numberChars.Length;
                numberWheelIndices[currentNumberIndex] = (numberWheelIndices[currentNumberIndex] + direction + charCountN) % charCountN;
                float angleN = (360f / charCountN) * numberWheelIndices[currentNumberIndex];
                // Başlangıç rotasyonu ile Y ekseni rotasyonunu birleştir
                numberWheelTargets[currentNumberIndex] = numberWheelInitialRots[currentNumberIndex] * Quaternion.AngleAxis(angleN, rotationAxis);
                break;
        }
    }

    // Mevcut çark ayarlarını birleştirip şifreyi kontrol eder
    private void CheckPassword()
    {
        if (PasswordManager.Instance == null)
        {
            Debug.LogError("PasswordManager.Instance bulunamadı!");
            return;
        }

        // 1. Mevcut çarklardan şifre ID'sini oluştur
        StringBuilder sbWord = new StringBuilder();
        for (int i = 0; i < wordWheelIndices.Length; i++)
        {
            sbWord.Append(wordChars[wordWheelIndices[i]]);
        }
        string word = sbWord.ToString().TrimEnd('-');
        
        string symbol = symbolChars[symbolWheelIndex]; 
        
        string number = $"{numberChars[numberWheelIndices[0]]}{numberChars[numberWheelIndices[1]]}{numberChars[numberWheelIndices[2]]}";

        string finalPasswordID = $"{word}_{symbol}_{number}";
        
        Debug.Log($"Şifre denemesi: {finalPasswordID}");

        // 2. PasswordManager ile DOĞRULA
        bool success = PasswordManager.Instance.ValidatePassword(finalPasswordID);

        // 3. Sonucu değerlendir
        if (success)
        {
            // BAŞARILI! Yeni bir şifre doğrulandı.
            Debug.Log("Şifre DOĞRU!");
            PlaySound(successSound);
            // Göstergeleri yeni toplam sayıyla güncelle
            UpdateIndicators(PasswordManager.Instance.GetValidatedPasswordCount()); 
        }
        else
        {
            // BAŞARISIZ! (Ya yanlış ya da zaten doğrulanmış)
            Debug.Log("Şifre YANLIŞ veya zaten doğrulanmış.");
            PlaySound(failSound);
        }
    }

    // Başarılı şifre sayısına göre göstergeleri (Kırmızı/Yeşil) günceller
    private void UpdateIndicators(int successCount)
    {
        if (indicatorRenderers == null || redMaterial == null || greenMaterial == null) return;

        for (int i = 0; i < indicatorRenderers.Length; i++)
        {
            if (indicatorRenderers[i] == null) continue;
            
            if (i < successCount)
            {
                indicatorRenderers[i].material = greenMaterial; // Bulundu
            }
            else
            {
                indicatorRenderers[i].material = redMaterial; // Henüz bulunmadı
            }
        }
    }
    
    // Aktif olan çark grubunu ve index'ini görsel olarak vurgular
    private void UpdateActiveWheelHighlight()
    {
        ClearAllHighlights();

        switch (currentGroup)
        {
            case 0: // Word
                if (wordWheelHighlights != null && currentWordIndex < wordWheelHighlights.Length)
                    if (wordWheelHighlights[currentWordIndex] != null) 
                        wordWheelHighlights[currentWordIndex].SetActive(true);
                break;
            case 1: // Symbol
                if (symbolWheelHighlight != null) 
                    symbolWheelHighlight.SetActive(true);
                break;
            case 2: // Number
                if (numberWheelHighlights != null && currentNumberIndex < numberWheelHighlights.Length)
                    if (numberWheelHighlights[currentNumberIndex] != null) 
                        numberWheelHighlights[currentNumberIndex].SetActive(true);
                break;
        }
    }

    // Tüm highlight objelerini kapatır
    private void ClearAllHighlights()
    {
        if (wordWheelHighlights != null)
            foreach (var h in wordWheelHighlights) if (h != null) h.SetActive(false);
            
        if (symbolWheelHighlight != null) 
            symbolWheelHighlight.SetActive(false);
            
        if (numberWheelHighlights != null)
            foreach (var h in numberWheelHighlights) if (h != null) h.SetActive(false);
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}