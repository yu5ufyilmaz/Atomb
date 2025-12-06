using UnityEngine;
using System.Collections;
using Cinemachine; 

public class HidingSpot : MonoBehaviour, IInteractable
{
    [Header("Pozisyon Ayarları")]
    [Tooltip("Saklanınca kameranın duracağı yer (Dolabın içi)")]
    [SerializeField] private Transform hideCameraPosition;
    
    [Tooltip("Saklanmaktan çıkınca oyuncunun duracağı yer (Dolabın önü)")]
    [SerializeField] private Transform exitPosition;
    
    [Tooltip("Saklanma/Çıkma geçiş süresi")]
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("Ses Efektleri")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hideSound;   
    [SerializeField] private AudioClip unhideSound; 

    private bool isOccupied = false;
    public bool IsOccupied => isOccupied; 

    // Referanslar
    private UnityEngine.CharacterController playerController;
    private StarterAssets.StarterAssetsInputs playerInput; 
    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;

    private void Start()
    {
        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        
        if (playerController)
        {
            playerInput = playerController.GetComponent<StarterAssets.StarterAssetsInputs>();
        }
        
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }
    }

    public void Interact()
    {
        if (isOccupied) ExitHiding();
        else EnterHiding();
    }

    public string GetInteractionPrompt()
    {
        return isOccupied ? "[Sol Tık] Çık" : "[Sol Tık] Saklan";
    }

    private void Update()
    {
        if (isOccupied)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                ExitHiding();
            }
        }
    }

    private void EnterHiding()
    {
        if (isOccupied) return;
        StartCoroutine(TransitionRoutine(true));
    }

    private void ExitHiding()
    {
        if (!isOccupied) return;
        StartCoroutine(TransitionRoutine(false));
    }

    private IEnumerator TransitionRoutine(bool entering)
    {
        isOccupied = entering;
        
        // 1. Oyuncu Kontrollerini ve MODELİNİ Yönet
        if (playerController) 
        {
            playerController.enabled = !entering;

            // --- YENİ EKLENEN KISIM: MODELİ GİZLE ---
            // Oyuncunun üzerindeki tüm görsel parçaları (MeshRenderer, SkinnedMeshRenderer) bul
            Renderer[] playerRenderers = playerController.GetComponentsInChildren<Renderer>();
            foreach (var r in playerRenderers)
            {
                // Saklanıyorsak (entering=true) -> enabled=false olsun
                // Çıkıyorsak (entering=false) -> enabled=true olsun
                r.enabled = !entering;
            }
            // ----------------------------------------
        }

        if (playerInput) 
        {
            playerInput.enabled = !entering;
            playerInput.cursorInputForLook = !entering; 
            playerInput.move = Vector2.zero; 
        }

        PlaySound(entering ? hideSound : unhideSound);

        // 2. Kamera Geçişi
        if (mainCamera != null && hideCameraPosition != null)
        {
            if (cinemachineBrain) cinemachineBrain.enabled = !entering;
            
            float t = 0;
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            
            Vector3 endPos = entering ? hideCameraPosition.position : (playerController.transform.position + Vector3.up * 1.5f);
            Quaternion endRot = entering ? hideCameraPosition.rotation : playerController.transform.rotation;

            while (t < 1f)
            {
                t += Time.deltaTime / transitionDuration;
                float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
                
                mainCamera.position = Vector3.Lerp(startPos, endPos, smoothT);
                mainCamera.rotation = Quaternion.Slerp(startRot, endRot, smoothT);
                yield return null;
            }
            
            if (!entering && cinemachineBrain) cinemachineBrain.enabled = true;
        }

        // 3. Çıkışta Oyuncuyu Güvenli Yere Işınla
        if (!entering && exitPosition != null && playerController != null)
        {
            playerController.enabled = false; 
            playerController.transform.position = exitPosition.position;
            playerController.transform.rotation = exitPosition.rotation;
            playerController.enabled = true;
            
            // Çıkınca modelin geri geldiğinden emin olmak için tekrar açalım (Garanti olsun)
            Renderer[] playerRenderers = playerController.GetComponentsInChildren<Renderer>();
            foreach (var r in playerRenderers) r.enabled = true;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }
}