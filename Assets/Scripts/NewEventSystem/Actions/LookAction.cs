using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;

public class LookAction : MonoBehaviour, IAction
{
    public enum FocusMode
    {
        RotatePlayerHead,
        ActivateVirtualCamera,
    }

    [Tooltip("Kameranın nasıl davranacağını seç.")]
    public FocusMode focusMode = FocusMode.RotatePlayerHead;

    [Header("Mod: Rotate Player Head")]
    [Tooltip("Kameranın zorla çevrileceği hedef obje (Sadece RotatePlayerHead modunda çalışır).")]
    public Transform lookTarget;

    [Tooltip("Kafanın hedefe dönme hızı (Saniye)")]
    public float headTurnDuration = 1.0f;

    [Header("Mod: Activate Virtual Camera")]
    [Tooltip(
        "Geçiş yapılacak Cinemachine Virtual Camera (Sadece ActivateVirtualCamera modunda çalışır)."
    )]
    public CinemachineVirtualCamera targetVirtualCamera;

    [Tooltip("Kameranın oyuncuya geri dönerken yapacağı geçiş süresi (Bekleme)")]
    public float blendOutDuration = 1.0f;

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
        // 1. OYUNCUYU DONDUR
        playerMoveScript.SetFrozen(true, lockCameraInput: true, restrictRotation: false);
        if (playerInputs != null)
        {
            playerInputs.cursorInputForLook = false;
            playerInputs.move = Vector2.zero;
        }

        
        if (focusMode == FocusMode.RotatePlayerHead && lookTarget != null && mainCam != null)
        {
            Vector3 startForward = mainCam.transform.forward;
            Quaternion startBodyRot = playerMoveScript.transform.rotation;
            
            // Hedef yön ve açıları önceden hesapla
            Vector3 targetDir = (lookTarget.position - mainCam.transform.position).normalized;
            Quaternion targetLookRot = Quaternion.LookRotation(targetDir);
            
            float targetYaw = targetLookRot.eulerAngles.y;
            float targetPitch = targetLookRot.eulerAngles.x;
            if (targetPitch > 180f) targetPitch -= 360f;

            Quaternion targetBodyRot = Quaternion.Euler(0f, targetYaw, 0f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / headTurnDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // --- 1. AŞAMA: Önce Vücut Dönüyor (0.0 ile 0.6 arası) ---
                // Vücut dönüşü biraz daha hızlı tamamlanır
                float bodyProgress = Mathf.Clamp01(smoothT / 0.7f);
                playerMoveScript.transform.rotation = Quaternion.Slerp(startBodyRot, targetBodyRot, bodyProgress);

                // --- 2. AŞAMA: Kamera Takip Ediyor (0.3 ile 1.0 arası) ---
                // Kamera biraz daha gecikmeli ve yumuşak süzülür
                float camProgress = Mathf.Clamp01((smoothT - 0.2f) / 0.8f);
                
                if (camProgress > 0f)
                {
                    Quaternion currentRot = Quaternion.Slerp(Quaternion.LookRotation(startForward), targetLookRot, camProgress);
                    float yaw = currentRot.eulerAngles.y;
                    float pitch = currentRot.eulerAngles.x;
                    if (pitch > 180f) pitch -= 360f;

                    playerMoveScript.ForceCameraRotation(yaw, pitch);
                }

                yield return null;
            }
        }
        else if (focusMode == FocusMode.ActivateVirtualCamera && targetVirtualCamera != null)
        {
            targetVirtualCamera.Priority = 100;
        }

        // 3. BEKLEME EVRESİ
        if (waitUntilInput)
        {
            while (!Input.GetKeyDown(KeyCode.F))
                yield return null;
        }
        else
        {
            yield return new WaitForSeconds(autoReleaseTime);
        }

        // 4. ÇIKIŞ VE SERBEST BIRAKMA
        if (focusMode == FocusMode.ActivateVirtualCamera && targetVirtualCamera != null)
        {
            targetVirtualCamera.Priority = 0;
            yield return new WaitForSeconds(blendOutDuration);
        }

        playerMoveScript.SetFrozen(false, lockCameraInput: false, restrictRotation: false);
        if (playerInputs != null)
        {
            playerInputs.cursorInputForLook = true;
        }
    }
}
