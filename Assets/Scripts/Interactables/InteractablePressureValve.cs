using UnityEngine;
using System.Collections;
using TMPro;

public class InteractablePressureValve : MonoBehaviour, IInteractable
{
    [Header("Components")]
    [Tooltip("Döndürülecek Vana Modeli")]
    [SerializeField] private Transform valveHandleModel;
    [Tooltip("Vanaya odaklanılacak kamera pozisyonu")]
    [SerializeField] private Transform cameraViewTarget;
    [SerializeField] private float cameraMoveDuration = 0.5f;

    [Header("Settings")]
    [SerializeField] private float rotationSpeedMultiplier = 5f; // Fare hassasiyeti
    
    // Referanslar (Sizin mevcut sisteminizden alındı)
    private UnityEngine.CharacterController playerPhysicsController;
    private MonoBehaviour playerInputScript;
    private MonoBehaviour playerMovementScript;
    private Transform cinemachineCameraTarget;
    
    // Kamera kayıtları
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;

    private bool isInteracting = false;
    private Vector2 screenCenter;
    private float lastAngle;

    private void Start()
    {
        // Ekran merkezini hesapla (Dairesel hareket için)
        screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

        // Player referanslarını bul (MassSpectrometer scriptinizdeki mantıkla aynı)
        playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerPhysicsController != null)
        {
            GameObject playerObj = playerPhysicsController.gameObject;
            playerInputScript = playerObj.GetComponent("StarterAssetsInputs") as MonoBehaviour;

            // Karakter hareket scriptini bul
            MonoBehaviour[] allScripts = playerObj.GetComponents<MonoBehaviour>();
            foreach(var script in allScripts)
            {
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

            Transform camRoot = playerObj.transform.Find("PlayerCameraRoot");
            if (camRoot != null) cinemachineCameraTarget = camRoot;
        }
    }

    public void Interact()
    {
        if (isInteracting) return;
        StartCoroutine(EnterValveMode());
    }

    public string GetInteractionPrompt()
    {
        // PDF: "Entry" -> Focus Mode
        return isInteracting ? "" : "[Sol Tık] Basınç Vanası";
    }

    private void Update()
    {
        if (!isInteracting) return;

        // Çıkış (ESC veya Sağ Tık)
        // PDF: "Exit: When the player releases the key or cancels the interaction"
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            StartCoroutine(ExitValveMode());
            return;
        }

        // PDF: "Action: As the player performs a circular motion with the mouse..."
        HandleCircularMotion();
    }

    private void HandleCircularMotion()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 direction = mousePos - screenCenter;

        // Atan2 ile farenin merkeze göre açısını bul (Radyan -> Derece)
        float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Bir önceki karedeki açı ile farkı bul
        float deltaAngle = Mathf.DeltaAngle(lastAngle, currentAngle);

        // Eğer fare saat yönünde (veya tersi) anlamlı bir hareket yaptıysa
        if (Mathf.Abs(deltaAngle) > 0.1f)
        {
            // Görsel olarak vanayı döndür (Z ekseninde)
            // deltaAngle pozitif veya negatif olabilir, vananın dönüş yönünü belirler
            if (valveHandleModel != null)
            {
                valveHandleModel.Rotate(Vector3.forward, deltaAngle, Space.Self);
            }

            // Basıncı düşür (Sadece belli bir yöne çevirince düşsün istiyorsanız if(deltaAngle < 0) diyebilirsiniz)
            // Biz her türlü harekette düşürecek şekilde ayarlayalım veya saat yönüne (negatif delta) zorlayalım:
            
            // Örnek: Sadece Saat Yönünde (Clockwise) çevirince basınç düşsün:
            if (deltaAngle < 0) 
            {
                float reduction = PressureSystemManager.Instance.pressureDecreaseRate * Time.deltaTime * Mathf.Abs(deltaAngle) * 0.1f;
                PressureSystemManager.Instance.ReducePressure(reduction);
            }
        }

        lastAngle = currentAngle;
    }

    private IEnumerator EnterValveMode()
    {
        isInteracting = true;
        
        // İlk açıyı kaydet
        Vector2 mousePos = Input.mousePosition;
        Vector2 direction = mousePos - screenCenter;
        lastAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Kontrolleri Devre Dışı Bırak
        if (playerPhysicsController) playerPhysicsController.enabled = false;
        if (playerInputScript) playerInputScript.enabled = false;
        if (playerMovementScript) playerMovementScript.enabled = false;

        // Mouse'u serbest bırak (Dairesel hareket için görülebilir olması daha iyi olabilir)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; // Dairesel hareketi görmek için imleç açık

        // Kamera Geçişi (MassSpec ile aynı mantık)
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

    private IEnumerator ExitValveMode()
    {
        isInteracting = false;

        // Kamera Geri Dönüş
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

        // Kontrolleri Geri Aç
        if (playerPhysicsController) playerPhysicsController.enabled = true;
        if (playerInputScript) playerInputScript.enabled = true;
        if (playerMovementScript) playerMovementScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}