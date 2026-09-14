using System.Collections;
using Cinemachine;
using NaughtyAttributes;
using StarterAssets;
using UnityEngine;

public class LookAction : MonoBehaviour, IAction
{
    public enum FocusMode
    {
        RotatePlayerHead,
        ActivateVirtualCamera,
    }

    [Tooltip("Kameranın nasıl davranacağı.")]
    public FocusMode focusMode = FocusMode.RotatePlayerHead;

    [ShowIf("focusMode", FocusMode.RotatePlayerHead)]
    [Header("Mod: Rotate Player Head")]
    [Tooltip("Kameranın zorla çevrileceği hedef obje (Sadece RotatePlayerHead modunda çalışır).")]
    public Transform lookTarget;

    [ShowIf("focusMode", FocusMode.RotatePlayerHead)]
    [Tooltip("Kafanın hedefe dönme hızı (Saniye)")]
    public float headTurnDuration = 1.0f;

    [ShowIf("focusMode", FocusMode.ActivateVirtualCamera)]
    [Header("Mod: Activate Virtual Camera")]
    [Tooltip(
        "Geçiş yapılacak Cinemachine Virtual Camera (Sadece ActivateVirtualCamera modunda çalışır)."
    )]
    public CinemachineVirtualCamera targetVirtualCamera;

    [ShowIf("focusMode", FocusMode.ActivateVirtualCamera)]
    [Tooltip("Kameranın oyuncuya geri dönerken yapacağı geçiş süresi (Bekleme)")]
    public float blendOutDuration = 1.0f;

    [Tooltip(
        "Kamera dönerken oyuncu yerinde kilitlensin mi? (Kapatırsan oyuncu yürümeye devam edebilir)"
    )]
    public bool freezePlayer = true;

    [Header("Çıkış Ayarları")]
    [Tooltip("True: Oyuncu F'ye basana kadar kilitli kalır. False: Süre dolunca kendi bırakır.")]
    public bool waitUntilInput = true;

    [Tooltip("waitUntilInput kapalıysa kaç saniye sonra serbest kalsın?")]
    public float autoReleaseTime = 2.0f;

    private StarterAssets.CharacterController playerMoveScript;
    private StarterAssetsInputs playerInputs;
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMoveScript = player.GetComponent<StarterAssets.CharacterController>();
            playerInputs = player.GetComponent<StarterAssetsInputs>();
        }
    }

    public void Execute()
    {
        if (playerMoveScript == null)
            return;

        StartCoroutine(FocusRoutine());
    }

    private IEnumerator FocusRoutine()
    {
        // 1. OYUNCU GİRDİLERİNİ VE HAREKETİNİ KES
        if (playerInputs != null)
        {
            playerInputs.cursorInputForLook = false; // Mouse'u her halükarda kilitliyoruz
        }

        if (freezePlayer)
        {
            playerMoveScript.SetFrozen(true, lockCameraInput: true, restrictRotation: false);
            if (playerInputs != null)
                playerInputs.move = Vector2.zero;
        }

        // 2. KAMERA ODAKLANMASI (Sürekli Takip / Tracking)
        if (focusMode == FocusMode.RotatePlayerHead && lookTarget != null && mainCam != null)
        {
            // Başlangıç rotasyonunu hafızaya alıyoruz
            Quaternion startRot = mainCam.transform.rotation;
            float t = 0f;

            // --- AŞAMA A: HEDEFE DÖNÜŞ (DİNAMİK) ---
            while (t < 1f)
            {
                t += Time.deltaTime / headTurnDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Oyuncu hareket edebileceği için hedefin yönünü HER KAREDE yeniden hesaplıyoruz
                Vector3 targetDir = (lookTarget.position - mainCam.transform.position).normalized;
                Quaternion targetLookRot = Quaternion.LookRotation(targetDir);

                // Kamerayı mevcut durumdan, sürekli güncellenen hedefe doğru Slerp (küresel yumuşatma) ile çeviriyoruz
                Quaternion currentRot = Quaternion.Slerp(startRot, targetLookRot, smoothT);

                ApplyRotationToPlayer(currentRot);
                yield return null;
            }
        }
        else if (focusMode == FocusMode.ActivateVirtualCamera && targetVirtualCamera != null)
        {
            targetVirtualCamera.Priority = 100;
        }

        // 3. BEKLEME EVRESİ (Oyuncu hareket etse bile kafa hedefe kitli kalacak)
        if (focusMode == FocusMode.RotatePlayerHead && lookTarget != null)
        {
            if (waitUntilInput)
            {
                while (!Input.GetKeyDown(KeyCode.F))
                {
                    TrackTarget();
                    yield return null;
                }
            }
            else
            {
                float holdTimer = 0f;
                while (holdTimer < autoReleaseTime)
                {
                    holdTimer += Time.deltaTime;
                    TrackTarget();
                    yield return null;
                }
            }
        }
        else // Virtual Camera modundaysak sadece bekleriz
        {
            if (waitUntilInput)
            {
                while (!Input.GetKeyDown(KeyCode.F))
                    yield return null;
            }
            else
            {
                yield return new WaitForSeconds(autoReleaseTime);
            }
        }

        // 4. ÇIKIŞ VE SERBEST BIRAKMA
        if (focusMode == FocusMode.ActivateVirtualCamera && targetVirtualCamera != null)
        {
            targetVirtualCamera.Priority = 0;
            yield return new WaitForSeconds(blendOutDuration);
        }

        if (freezePlayer)
        {
            playerMoveScript.SetFrozen(false, lockCameraInput: false, restrictRotation: false);
        }

        if (playerInputs != null)
        {
            playerInputs.cursorInputForLook = true; // Oyuncuya kontrolü geri ver
        }
    }

    // --- YARDIMCI FONKSİYONLAR ---

    private void TrackTarget()
    {
        if (lookTarget == null || mainCam == null)
            return;

        // Hedefe giden yönü bul
        Vector3 targetDir = (lookTarget.position - mainCam.transform.position).normalized;
        Quaternion targetLookRot = Quaternion.LookRotation(targetDir);

        ApplyRotationToPlayer(targetLookRot);
    }

    private void ApplyRotationToPlayer(Quaternion rot)
    {
        float currentYaw = rot.eulerAngles.y;
        float currentPitch = rot.eulerAngles.x;

        // Pitch (X ekseni) ayarını Unity'nin 0-360 mantığından -180 ile 180 mantığına çeviriyoruz
        if (currentPitch > 180f)
            currentPitch -= 360f;

        // 1. KAMERAYI ÇEVİR (Bu iki senaryoda da ortak çalışır)
        playerMoveScript.ForceCameraRotation(currentYaw, currentPitch);

        // 2. EĞER OYUNCU DONDURULMUŞSA VÜCUDUNU DA ÇEVİR (Yalnızca Y ekseninde)
        // Böylece kafa arkaya dönerken vücut sabit kalıp garip bir görüntü oluşturmaz.
        if (freezePlayer)
        {
            playerMoveScript.transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }
}
