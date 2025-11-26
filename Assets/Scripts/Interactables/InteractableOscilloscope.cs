using UnityEngine;
using System.Collections;
using CharacterController = StarterAssets.CharacterController;

// PlayerInteraction.cs'teki IInteractable arayüzünü kullanır
public class InteractableOscilloscope : MonoBehaviour, IInteractable
{
    [Header("Player Control")]
    [SerializeField] private UnityEngine.CharacterController playerController;
    [SerializeField] private MonoBehaviour playerLookScript;
    [SerializeField] private Transform cameraViewTarget;
    [SerializeField] private float cameraMoveDuration = 0.5f;
    private Transform cinemachineCameraTarget;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isInteracting = false;
    
    [Header("Audio Settings")]
    [Tooltip("Döngü halinde çalacak TEK ses dosyası (AudioSource'a atayın)")]
    [SerializeField] private AudioSource audioSource;
    
    [Header("Knob Visuals")]
    [SerializeField] private Transform voltsKnob; // Genlik / Ses Şiddeti
    [SerializeField] private Transform timeKnob;  // Frekans / Ses Hızı (Pitch)
    [SerializeField] private float rotationPerStep = 36f; 

    [Header("Puzzle Logic & ranges")]
    [SerializeField] [Range(0, 10)] private int maxVoltsSetting = 10;
    [SerializeField] [Range(0, 10)] private int maxTimeSetting = 10;
    
    [Space]
    [Tooltip("Doğru Volts (Yükseklik) ayarı")]
    [SerializeField] private int correctVoltsSetting = 5;
    [Tooltip("Doğru Time (Hız/Pitch) ayarı")]
    [SerializeField] private int correctTimeSetting = 5;

    [Header("Simulation Ranges")]
    // Pitch: 1.0 normaldir. 0.5 yavaş, 2.0 hızlıdır.
    [SerializeField] private float minPitch = 0.5f; 
    [SerializeField] private float maxPitch = 2.0f;
    
    // Volume: Doğru ayarda 1.0, yanlışlarda daha kısık olabilir.
    [SerializeField] private float minVolume = 0.2f;
    [SerializeField] private float maxVolume = 1.0f;

    [Header("Waveform Settings")]
    [SerializeField] private WaveformGenerator waveformScript;
    [SerializeField] private float visualLerpSpeed = 5f;

    // Mevcut durum
    private int currentVoltsSetting = 0; // Başlangıç değeri (0 yaparsak ses kısık/bozuk başlar)
    private int currentTimeSetting = 0;  // Başlangıç değeri (0 yaparsak çok yavaş başlar)
    
    private bool isSolved = false;
    
    // Çarkların başlangıç rotasyonları
    private Quaternion voltsKnobInitialRot;
    private Quaternion timeKnobInitialRot;

    void Start()
    {
        // Player scriptlerini bul
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        // StarterAssetsInputs tipini kontrol edin, projenizde farklı olabilir
        if (playerLookScript == null && playerController != null)
            playerLookScript = playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
        
        if (playerController != null)
        {
            var starterAssetsController = playerController.GetComponent("ThirdPersonController") as MonoBehaviour; 
            if (starterAssetsController == null) starterAssetsController = playerController.GetComponent<CharacterController>();
            
            Transform camTarget = playerController.transform.Find("PlayerCameraRoot");
            if (camTarget != null) cinemachineCameraTarget = camTarget;
        }
        
        if (voltsKnob != null) voltsKnobInitialRot = voltsKnob.localRotation;
        if (timeKnob != null) timeKnobInitialRot = timeKnob.localRotation;
        
        if (audioSource != null) 
        {
            audioSource.Stop();
            audioSource.loop = true;
        }

        currentVoltsSetting = 2; // Örn: Çok alçak genlik
        currentTimeSetting = 2;  // Örn: Çok yavaş (kalın ses)
        
        UpdateKnobVisuals();
    }

    public string GetInteractionPrompt()
    {
        if (isSolved) return "Sinyal Stabil";
        return isInteracting ? "" : "[Sol Tık] Sinyali Düzelt";
    }

    public void Interact()
    {
        if (isInteracting || isSolved) return;
        StartCoroutine(EnterMachineView());
    }

    private IEnumerator EnterMachineView()
    {
        isInteracting = true;
        if (playerController) playerController.enabled = false;
        if (playerLookScript) playerLookScript.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Kamera Animasyonu
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
        
        // Sesi başlat (Mevcut ayarlara göre bozuk başlayacak)
        if (audioSource != null)
        {
            UpdateAudioAndWaveform(); // İlk değerleri ata
            audioSource.Play();
        }
        
        UpdateKnobVisuals();
    }
    
    private void Update()
    {
        if (!isInteracting) return;

        // Çıkış (ESC)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StartCoroutine(ExitMachineView());
            return;
        }
        
        if (isSolved) 
        {
            // Çözüldüyse bile dalga formunu güncel tut (animasyon için)
            if(waveformScript) waveformScript.frequency = 1.0f; // Sabit normal
            return;
        }
        
        bool settingChanged = false;
        
        // Volts Ayarı (W/S) -> Amplitude & Volume
        if (Input.GetKeyDown(KeyCode.W))
        {
            currentVoltsSetting = Mathf.Min(currentVoltsSetting + 1, maxVoltsSetting);
            settingChanged = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            currentVoltsSetting = Mathf.Max(currentVoltsSetting - 1, 0);
            settingChanged = true;
        }
        
        // Time Ayarı (A/D) -> Pitch & Frequency
        if (Input.GetKeyDown(KeyCode.D))
        {
            currentTimeSetting = Mathf.Min(currentTimeSetting + 1, maxTimeSetting);
            settingChanged = true;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            currentTimeSetting = Mathf.Max(currentTimeSetting - 1, 0);
            settingChanged = true;
        }

        if (settingChanged)
        {
            UpdateKnobVisuals();
            UpdateAudioAndWaveform(); // Sesi ve dalgayı anlık güncelle
            CheckForSolution();
        }
        
        // Dalga formunu her frame'de yumuşak geçişle güncelle (Görsel Smoothness)
        // Ancak gerçek değerleri settingChanged içinde hesapladık.
    }

    // Bu fonksiyon hem sesi hem de dalga formunun hedef değerlerini ayarlar
    private void UpdateAudioAndWaveform()
    {
        // --- 1. Time Knob -> Pitch (Hız) Hesabı ---
        // Knob 0 iken minPitch, Knob Max iken maxPitch olsun.
        float tRatio = (float)currentTimeSetting / maxTimeSetting;
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, tRatio);
        
        // Eğer "Doğru" ayardaysak Pitch tam 1.0 olsun (Floating point hatasını önlemek için)
        if (currentTimeSetting == correctTimeSetting) targetPitch = 1.0f;

        if (audioSource != null)
        {
            audioSource.pitch = targetPitch;
        }

        // --- 2. Volts Knob -> Volume (Şiddet) Hesabı ---
        float vRatio = (float)currentVoltsSetting / maxVoltsSetting;
        float targetVolume = Mathf.Lerp(minVolume, maxVolume, vRatio);
        
        if (currentVoltsSetting == correctVoltsSetting) targetVolume = 1.0f; // İdeal ses seviyesi
        
        if (audioSource != null)
        {
            audioSource.volume = targetVolume;
        }

        // --- 3. Waveform Visuals ---
        if (waveformScript != null)
        {
            // Pitch arttıkça frekans artar (dalgalar sıklaşır)
            waveformScript.frequency = targetPitch; 
            
            // Volume arttıkça genlik artar (dalgalar yükselir)
            // Görsel olarak 0.5 ile 2.0 arasında scale edelim
            waveformScript.amplitude = Mathf.Lerp(0.5f, 2.5f, vRatio);
        }
    }

    private void UpdateKnobVisuals()
    {
        if (voltsKnob != null)
        {
            float angleV = currentVoltsSetting * rotationPerStep;
            voltsKnob.localRotation = voltsKnobInitialRot * Quaternion.Euler(0, angleV, 0); 
        }
        
        if (timeKnob != null)
        {
            float angleT = currentTimeSetting * rotationPerStep;
            timeKnob.localRotation = timeKnobInitialRot * Quaternion.Euler(0, angleT, 0); 
        }
    }
    
    private void CheckForSolution()
    {
        if (isSolved) return;

        if (currentVoltsSetting == correctVoltsSetting && currentTimeSetting == correctTimeSetting)
        {
            Debug.Log("Osiloskop Ayarı DOĞRU!");
            isSolved = true;
            
            // Çözülünce otomatik çıkış yapabilir veya bekleyebiliriz
            StartCoroutine(AutoExit(2.0f));
        }
    }

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;
        
        // Çözülmediyse sesi kapat, çözüldüyse çalmaya devam edebilir (veya kapatabilir)
        if (audioSource != null) // && !isSolved (eğer çıkınca susmasını isterseniz bunu açın)
        {
            audioSource.Stop();
        }
        
        // Kamera Geri Dönüş
        if (cinemachineCameraTarget != null && originalCameraParent != null)
        {
            float t = 0f;
            Vector3 startPos = cinemachineCameraTarget.position;
            Quaternion startRot = cinemachineCameraTarget.rotation;
            
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
            
            cinemachineCameraTarget.SetParent(originalCameraParent, true);
            cinemachineCameraTarget.localPosition = originalCameraLocalPos;
            cinemachineCameraTarget.localRotation = originalCameraLocalRot;
        }

        if (playerController) playerController.enabled = true;
        if (playerLookScript) playerLookScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private IEnumerator AutoExit(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isInteracting) 
        {
            StartCoroutine(ExitMachineView());
        }
    }
}