using UnityEngine;
using System.Collections;
using TMPro;

public class InteractableMassSpectrometer : MonoBehaviour, IInteractable
{
    [Header("--- Components ---")]
    [Tooltip("Mıknatısların bağlı olduğu merkez pivot.")]
    [SerializeField] private Transform magnetPivot;     
    [Tooltip("Tüpün içindeki dönen halkalar.")]
    [SerializeField] private Transform acceleratorRing; 
    [Tooltip("Makineyi açan kol.")]
    [SerializeField] private Transform mainLever;       
    [Tooltip("Başarı durumunda açılacak ışın.")]
    [SerializeField] private GameObject ionBeamObj;     
    [Tooltip("Durum ekranı.")]
    [SerializeField] private TextMeshPro screenText;
    [Tooltip("Bozulunca çıkacak duman.")]
    [SerializeField] private GameObject overheatSmoke;  

    [Header("--- Puzzle Settings ---")]
    [Tooltip("Mıknatısların güvenli olduğu Z açısı.")]
    [SerializeField] private float safeZoneAngle = 0f;      
    [SerializeField] private float safeZoneTolerance = 12f; 
    
    [Tooltip("Halkaların gelmesi gereken Y açısı.")]
    [SerializeField] private float ringTargetAngle = 90f;   
    [Tooltip("Halka toleransı.")]
    [SerializeField] private float ringTolerance = 5f; 
    
    [Header("--- Penalty Logic ---")]
    [SerializeField] private float maxGrindTime = 1.5f; 
    [SerializeField] private float cooldownDuration = 10f; 

    [Header("--- Controls & Audio ---")]
    [SerializeField] private float magnetRotateSpeed = 2f; 
    [SerializeField] private float ringRotateSpeed = 50f; 

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource loopAudioSource; 
    [SerializeField] private AudioClip leverSound;
    [SerializeField] private AudioClip grindSound;      
    [SerializeField] private AudioClip breakSound;      
    [SerializeField] private AudioClip successSound;    
    [SerializeField] private AudioClip overheatSound;   
    [SerializeField] private AudioClip beamHumSound;    

    [Header("--- Camera ---")]
    [SerializeField] private Transform cameraViewTarget;
    [SerializeField] private float cameraMoveDuration = 0.5f;
    
    // --- Referanslar ---
    private UnityEngine.CharacterController playerPhysicsController; // Unity'nin fizik bileşeni
    private MonoBehaviour playerInputScript; // Input scripti (StarterAssetsInputs)
    private MonoBehaviour playerMovementScript; // Karakteri hareket ettiren script (Sorunu çözen kısım)
    
    private Transform cinemachineCameraTarget;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;

    private bool isInteracting = false;
    private bool isPoweredOn = false;   
    private bool isBroken = false;      
    private bool isSolved = false;      
    
    private float currentGrindTimer = 0f;
    private float currentCooldown = 0f;
    
    private Quaternion leverStartRot;
    private Quaternion leverEndRot;
    private float currentRingAngleValue = 0f; 

    private void Start()
    {
        // 1. Player Bileşenlerini Bulma (Daha Kapsamlı)
        playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerPhysicsController != null)
        {
            GameObject playerObj = playerPhysicsController.gameObject;

            // Input scriptini bul
            playerInputScript = playerObj.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            
            // --- KRİTİK DÜZELTME: Karakter Kontrol Scriptini Bul ---
            // Genelde "ThirdPersonController" veya "FirstPersonController" olur.
            // StarterAssets içinde bazen adı "CharacterController" olan bir script de olabilir.
            MonoBehaviour[] allScripts = playerObj.GetComponents<MonoBehaviour>();
            foreach(var script in allScripts)
            {
                // Unity'nin kendi bileşeni değilse ve adı Controller içeriyorsa odur.
                string typeName = script.GetType().Name;
                if ((typeName.Contains("Controller") || typeName.Contains("Character")) 
                    && script.GetType() != typeof(UnityEngine.CharacterController))
                {
                    // CinemachineTarget'ı yöneten scripti arıyoruz
                    var field = script.GetType().GetField("CinemachineCameraTarget");
                    if (field != null)
                    {
                        playerMovementScript = script;
                        break;
                    }
                }
            }

            // Cinemachine Target'ı bul
            Transform camRoot = playerObj.transform.Find("PlayerCameraRoot");
            if (camRoot != null) cinemachineCameraTarget = camRoot;
        }

        // 2. Kol Animasyon Hazırlığı
        if (mainLever != null)
        {
            leverStartRot = mainLever.localRotation;
            leverEndRot = leverStartRot * Quaternion.Euler(45, 0, 0);
        }

        // 3. Başlangıç Açısını Ayarla
        float randomOffset = Random.Range(-40f, 40f); 
        currentRingAngleValue = ringTargetAngle + 180f + randomOffset;
        UpdateRingRotation();

        ResetMachineVisuals();
    }

    private void ResetMachineVisuals()
    {
        if (screenText != null) { screenText.text = "SYSTEM OFF"; screenText.color = Color.white; }
        if (ionBeamObj != null) ionBeamObj.SetActive(false);
        if (overheatSmoke != null) overheatSmoke.SetActive(false);
        if (mainLever != null) mainLever.localRotation = leverStartRot;
        
        isPoweredOn = false;
        isSolved = false;
        currentGrindTimer = 0f;
        
        if (magnetPivot != null) magnetPivot.localPosition = Vector3.zero;
    }

    private void UpdateRingRotation()
    {
        if (acceleratorRing != null)
        {
            acceleratorRing.localRotation = Quaternion.Euler(90, currentRingAngleValue, 0);
        }
    }

    public string GetInteractionPrompt()
    {
        if (isBroken) return $"Sistem Soğuyor... ({currentCooldown:F0}s)";
        if (isSolved) return "Kalibrasyon Tamamlandı";
        if (!isPoweredOn) return "[Sol Tık] Güç Kolunu Çek";
        return isInteracting ? "" : "[Sol Tık] Analiz Ekranı";
    }

    public void Interact()
    {
        if (isBroken) { if(audioSource) audioSource.PlayOneShot(overheatSound); return; }
        if (!isPoweredOn) { StartCoroutine(PullLeverSequence()); return; }
        if (!isInteracting && !isSolved) { StartCoroutine(EnterMachineView()); }
    }

    private void Update()
    {
        if (isBroken)
        {
            currentCooldown -= Time.deltaTime;
            if (screenText != null) screenText.text = $"OVERHEATED\nWAIT: {currentCooldown:F1}s";
            if (currentCooldown <= 0) { isBroken = false; ResetMachineVisuals(); }
            return;
        }

        if (!isInteracting || isSolved) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { StartCoroutine(ExitMachineView()); return; }

        // --- 1. MIKNATIS KONTROLÜ ---
        float mouseX = Input.GetAxis("Mouse X");
        if (magnetPivot != null)
        {
            magnetPivot.Rotate(Vector3.forward * mouseX * magnetRotateSpeed * -1);
        }

        float currentMagAngle = magnetPivot.localEulerAngles.z;
        float magDiff = Mathf.Abs(Mathf.DeltaAngle(currentMagAngle, safeZoneAngle));
        bool isMagnetSafe = magDiff < safeZoneTolerance;

        // --- 2. EKRAN BİLGİSİ ---
        float ringDiff = Mathf.Abs(Mathf.DeltaAngle(currentRingAngleValue, ringTargetAngle));
        float displayRingAngle = currentRingAngleValue % 360;
        if (displayRingAngle < 0) displayRingAngle += 360;

        if (screenText != null)
        {
            if (isMagnetSafe)
            {
                screenText.color = Color.green;
                screenText.text = $"MAGNET STABLE\nRING: {displayRingAngle:F0}° / TARGET: {ringTargetAngle}°";
            }
            else
            {
                screenText.color = Color.red;
                float displayMagAngle = currentMagAngle > 180 ? currentMagAngle - 360 : currentMagAngle;
                screenText.text = $"MAGNET UNSTABLE ({displayMagAngle:F0}°)\nALIGN MAGNET (0°)";
            }
        }

        // --- 3. HALKA KONTROLÜ (A/D) ---
        float ringInput = 0f;
        if (Input.GetKey(KeyCode.D)) ringInput = 1f;
        if (Input.GetKey(KeyCode.A)) ringInput = -1f;

        if (ringInput != 0)
        {
            if (isMagnetSafe)
            {
                currentRingAngleValue += ringInput * ringRotateSpeed * Time.deltaTime;
                UpdateRingRotation();
                ResetPenalty();
                
                if (ringDiff < ringTolerance)
                {
                    StartCoroutine(SuccessSequence());
                }
            }
            else
            {
                ApplyPenaltyLogic();
            }
        }
        else
        {
            ResetPenalty();
        }
    }

    private void ApplyPenaltyLogic()
    {
        currentGrindTimer += Time.deltaTime;
        if (loopAudioSource != null && !loopAudioSource.isPlaying) { loopAudioSource.clip = grindSound; loopAudioSource.Play(); }
        float shake = Random.Range(-0.03f, 0.03f);
        magnetPivot.localPosition = new Vector3(shake, shake, 0);

        if (currentGrindTimer > maxGrindTime) { StartCoroutine(TriggerBreakdown()); }
    }

    private void ResetPenalty()
    {
        currentGrindTimer = 0f;
        if (loopAudioSource != null && loopAudioSource.isPlaying) loopAudioSource.Stop();
        if (magnetPivot != null) magnetPivot.localPosition = Vector3.zero;
    }

    private IEnumerator TriggerBreakdown()
    {
        isBroken = true;
        currentCooldown = cooldownDuration;
        if (audioSource) audioSource.PlayOneShot(breakSound);
        if (overheatSmoke) overheatSmoke.SetActive(true);
        if (screenText) { screenText.color = Color.red; screenText.text = "SYSTEM FAILURE\nCRITICAL ERROR"; }
        yield return StartCoroutine(ExitMachineView());
    }

    private IEnumerator SuccessSequence()
    {
        isSolved = true;
        ResetPenalty();
        if (audioSource) audioSource.PlayOneShot(successSound);
        
        if (screenText) 
        {
            screenText.color = Color.cyan;
            screenText.text = "CALIBRATION COMPLETE\nCODE: 84-12-99";
        }
        
        if (ionBeamObj) 
        {
            ionBeamObj.SetActive(true);
            if(loopAudioSource) { loopAudioSource.clip = beamHumSound; loopAudioSource.Play(); }
        }

        yield return new WaitForSeconds(3.0f);
        StartCoroutine(ExitMachineView());
    }

    private IEnumerator PullLeverSequence()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            mainLever.localRotation = Quaternion.Slerp(leverStartRot, leverEndRot, t);
            yield return null;
        }
        if (audioSource) audioSource.PlayOneShot(leverSound);
        isPoweredOn = true;
        if (screenText) { screenText.color = Color.yellow; screenText.text = "SYSTEM READY\nWAITING INPUT"; }
    }

    private IEnumerator EnterMachineView()
    {
        isInteracting = true;

        // --- TÜM PLAYER KONTROLLERİNİ KAPAT ---
        if (playerPhysicsController) playerPhysicsController.enabled = false;
        if (playerInputScript) playerInputScript.enabled = false;
        
        // Bu komut kamerayı döndüren scripti kapatır:
        if (playerMovementScript) playerMovementScript.enabled = false; 

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cinemachineCameraTarget != null && cameraViewTarget != null)
        {
            originalCameraParent = cinemachineCameraTarget.parent;
            originalCameraLocalPos = cinemachineCameraTarget.localPosition;
            originalCameraLocalRot = cinemachineCameraTarget.localRotation;
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
    }

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;
        ResetPenalty();

        if (cinemachineCameraTarget != null && originalCameraParent != null)
        {
            float t = 0f;
            Vector3 startPos = cinemachineCameraTarget.position;
            Quaternion startRot = cinemachineCameraTarget.rotation;
            Vector3 targetPos = originalCameraParent.TransformPoint(originalCameraLocalPos);
            Quaternion targetRot = originalCameraParent.rotation * originalCameraLocalRot;

            while (t < 1f)
            {
                t += Time.deltaTime / cameraMoveDuration;
                float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
                cinemachineCameraTarget.position = Vector3.Lerp(startPos, targetPos, smoothT);
                cinemachineCameraTarget.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                yield return null;
            }
            cinemachineCameraTarget.SetParent(originalCameraParent, true);
            cinemachineCameraTarget.localPosition = originalCameraLocalPos;
            cinemachineCameraTarget.localRotation = originalCameraLocalRot;
        }

        // --- KONTROLLERİ GERİ AÇ ---
        if (playerPhysicsController) playerPhysicsController.enabled = true;
        if (playerInputScript) playerInputScript.enabled = true;
        if (playerMovementScript) playerMovementScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}