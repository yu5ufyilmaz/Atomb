using UnityEngine;
using System.Collections;

// PlayerInteraction.cs'teki IInteractable arayüzünü kullanır
public class InteractableOscilloscope : MonoBehaviour, IInteractable
{
    [Header("Player Control (Turing Machine'den Kopyala)")]
    [Tooltip("Oyuncunun ana CharacterController'ı")]
    [SerializeField] private UnityEngine.CharacterController playerController;
    [Tooltip("Oyuncunun fare/kamera kontrol script'i (örn: StarterAssetsInputs)")]
    [SerializeField] private MonoBehaviour playerLookScript;
    [Tooltip("Kameranın kilitleneceği pozisyon (Boş bir obje)")]
    [SerializeField] private Transform cameraViewTarget;
    [SerializeField] private float cameraMoveDuration = 0.5f;
    private Transform cinemachineCameraTarget;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isInteracting = false;
    
    [Header("Waveform Visuals (Target Values)")]
    [SerializeField] private float correctAmplitude = 1.5f;
    [SerializeField] private float correctFrequency = 1.0f;
    [Space]
    [SerializeField] private float incorrectAmplitude = 2.5f;
    [SerializeField] private float incorrectFrequency = 3.0f;
    [SerializeField] private float visualLerpSpeed = 5f;

    [Header("Oscilloscope Settings")]
    [Tooltip("Döngü halindeki 'bozuk' sesi çalan AudioSource")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Doğru ayar bulunduğunda çalacak bildirim sesi")]
    [SerializeField] private AudioClip successSound;

    [Space]
    [Tooltip("Olması gereken (doğru) pitch/hız (Örn: 90 BPM için 1.0)")]
    [SerializeField] private float basePitch = 1.0f;
    [Tooltip("Başlangıçtaki 'bozuk' pitch/hız (Örn: 120 BPM için 1.33)")]
    [SerializeField] private float incorrectPitch = 1.33f;

    [Header("Knob Visuals")]
    [Tooltip("Döndürülecek VOLTS/DIV düğmesinin Transform'u")]
    [SerializeField] private Transform voltsKnob;
    [Tooltip("Döndürülecek TIME/DIV düğmesinin Transform'u")]
    [SerializeField] private Transform timeKnob;
    [Tooltip("Her 'tık' kaç derece dönsün? (360 / adım sayısı)")]
    [SerializeField] private float rotationPerStep = 36f; // Örn: 10 adım için

    [Header("Puzzle Logic")]
    [SerializeField] [Range(0, 10)] private int maxVoltsSetting = 10;
    [SerializeField] [Range(0, 10)] private int maxTimeSetting = 10;
    
    [Space]
    [Tooltip("Bulmacanın çözümü için gereken Volts ayarı (0-10)")]
    [SerializeField] private int correctVoltsSetting = 3;
    [Tooltip("Bulmacanın çözümü için gereken Time ayarı (0-10)")]
    [SerializeField] private int correctTimeSetting = 5;
    
    [SerializeField] private WaveformGenerator waveformScript;

    // Mevcut durum
    private int currentVoltsSetting = 0;
    private int currentTimeSetting = 0;
    private bool isSolved = false;
    
    // Çarkların başlangıç rotasyonları
    private Quaternion voltsKnobInitialRot;
    private Quaternion timeKnobInitialRot;

    void Start()
    {
        // Player scriptlerini bul (Turing makinesindeki gibi)
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerLookScript == null && playerController != null)
            playerLookScript = playerController.GetComponent<StarterAssets.StarterAssetsInputs>();

        // Cinemachine hedefini bul (Turing makinesindeki gibi)
        if (playerController != null)
        {
            var starterAssetsController = playerController.GetComponent<StarterAssets.CharacterController>();
            if (starterAssetsController != null && starterAssetsController.CinemachineCameraTarget != null)
            {
                cinemachineCameraTarget = starterAssetsController.CinemachineCameraTarget.transform;
            }
        }
        
        if (voltsKnob != null) voltsKnobInitialRot = voltsKnob.localRotation;
        if (timeKnob != null) timeKnobInitialRot = timeKnob.localRotation;
        
        if (audioSource != null) audioSource.Stop();
    }

    public string GetInteractionPrompt()
    {
        if (isSolved) return "Sistem Ayarlandı";
        return isInteracting ? "" : "[Sol Tık] Osiloskopu Kullan";
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
        
        // Kamera Animasyonu (Turing Machine'den)
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
        
        // "Bozuk" sesi başlat
        if (audioSource != null)
        {
            audioSource.pitch = incorrectPitch;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        UpdateKnobVisuals();
    }
    
    private void UpdateWaveformVisuals()
    {
        if (waveformScript == null) return;

        // Hedef değerleri belirle
        float targetAmplitude;
        float targetFrequency;

        if (isSolved)
        {
            targetAmplitude = correctAmplitude;
            targetFrequency = correctFrequency;
        }
        else
        {
            targetAmplitude = incorrectAmplitude;
            targetFrequency = incorrectFrequency;
        }
        
        waveformScript.amplitude = Mathf.Lerp(
            waveformScript.amplitude, 
            targetAmplitude, 
            Time.deltaTime * visualLerpSpeed
        );
        
        waveformScript.frequency = Mathf.Lerp(
            waveformScript.frequency, 
            targetFrequency, 
            Time.deltaTime * visualLerpSpeed
        );
    }

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;
        
        // "Bozuk" sesi durdur (eğer çözülmediyse)
        if (audioSource != null && !isSolved)
        {
            audioSource.Stop();
        }
        
        // Kamera Geri Dönüş Animasyonu (Turing Machine'den)
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

        // Kontrolleri geri ver
        if (playerController) playerController.enabled = true;
        if (playerLookScript) playerLookScript.enabled = true;

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
        
        if (isSolved) return;
        
        bool settingChanged = false;
        
        // Volts Ayarı (W/S)
        if (Input.GetKeyDown(KeyCode.W))
        {
            currentVoltsSetting = (currentVoltsSetting + 1) % maxVoltsSetting;
            settingChanged = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            currentVoltsSetting = (currentVoltsSetting - 1 + maxVoltsSetting) % maxVoltsSetting;
            settingChanged = true;
        }
        
        // Time Ayarı (A/D)
        if (Input.GetKeyDown(KeyCode.D))
        {
            currentTimeSetting = (currentTimeSetting + 1) % maxTimeSetting;
            settingChanged = true;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            currentTimeSetting = (currentTimeSetting - 1 + maxTimeSetting) % maxTimeSetting;
            settingChanged = true;
        }

        if (settingChanged)
        {
            UpdateKnobVisuals();
            CheckForSolution();
        }
        UpdateWaveformVisuals();
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
        if (isSolved) return; // Zaten çözülmüş

        if (currentVoltsSetting == correctVoltsSetting && currentTimeSetting == correctTimeSetting)
        {
            Debug.Log("Osiloskop Ayarı DOĞRU!");
            isSolved = true;
            
            if (audioSource != null)
            {
                audioSource.pitch = basePitch;
                audioSource.loop = false;
                    
                if (successSound != null)
                {
                    audioSource.volume *= 0.3f; 
                    AudioSource.PlayClipAtPoint(successSound, transform.position, 1.0f);
                }
            }
            
            StartCoroutine(AutoExit(1.5f));
        }
        else
        {
            if (audioSource != null)
            {
                audioSource.pitch = incorrectPitch;
            }
        }
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