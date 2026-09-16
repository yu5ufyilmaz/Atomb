using System.Collections;
using Cinemachine;
using NaughtyAttributes;
using StarterAssets;
using UnityEngine;

public class LookAction : ActionBase
{
    public enum FocusMode
    {
        RotatePlayerHead,
        ActivateVirtualCamera,
    }

    public enum LookOffset
    {
        Direct, // Hedefe tam bakar
        Behind180, // Hedefin tam arkasına (180 derece tersine) bakar
        Right90, // Hedefin 90 derece sağına bakar
        Left90, // Hedefin 90 derece soluna bakar
        Custom, // Manuel açı girilir
    }

    [Tooltip("Kameranın nasıl davranacağı.")]
    public FocusMode focusMode = FocusMode.RotatePlayerHead;

    [ShowIf("focusMode", FocusMode.RotatePlayerHead)]
    [Header("Mod: Rotate Player Head")]
    [Tooltip("Kameranın zorla çevrileceği hedef obje.")]
    public Transform lookTarget;

    [ShowIf("focusMode", FocusMode.RotatePlayerHead)]
    [Header("Açı Sapması (Offset)")]
    [Tooltip("Hedefe bakarken uygulanacak sapma (Örn: Tam arkasına bakmak için Behind180)")]
    public LookOffset lookOffset = LookOffset.Direct;

    [ShowIf("lookOffset", LookOffset.Custom)]
    [Tooltip("Custom seçildiğinde uygulanacak Y ekseni açısı (Örn: 45)")]
    public float customYAngle = 0f;

    [ShowIf("focusMode", FocusMode.RotatePlayerHead)]
    [Tooltip("Kafanın hedefe dönme hızı (Saniye)")]
    public float headTurnDuration = 1.0f;

    [ShowIf("focusMode", FocusMode.ActivateVirtualCamera)]
    [Header("Mod: Activate Virtual Camera")]
    [Tooltip("Geçiş yapılacak Cinemachine Virtual Camera.")]
    public CinemachineVirtualCamera targetVirtualCamera;

    [ShowIf("focusMode", FocusMode.ActivateVirtualCamera)]
    [Tooltip("Kameranın oyuncuya geri dönerken yapacağı yumuşama süresi")]
    public float blendOutDuration = 1.0f;

    [Tooltip("Kamera dönerken oyuncu yerinde kilitlensin mi?")]
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

    protected override void PerformAction()
    {
        if (playerMoveScript == null)
            return;

        StartCoroutine(FocusRoutine());
    }

    private Quaternion GetTargetRotation()
    {
        if (lookTarget == null || mainCam == null)
            return Quaternion.identity;

        Vector3 targetDir = (lookTarget.position - mainCam.transform.position).normalized;
        Quaternion baseRot = Quaternion.LookRotation(targetDir);

        float yOffset = 0f;
        switch (lookOffset)
        {
            case LookOffset.Behind180:
                yOffset = 180f;
                break;
            case LookOffset.Right90:
                yOffset = 90f;
                break;
            case LookOffset.Left90:
                yOffset = -90f;
                break;
            case LookOffset.Custom:
                yOffset = customYAngle;
                break;
        }

        return baseRot * Quaternion.Euler(0f, yOffset, 0f);
    }

    private IEnumerator FocusRoutine()
    {
        // 1. OYUNCU GİRDİSİNİ VE HAREKETİNİ KES
        if (playerInputs != null)
        {
            playerInputs.cursorInputForLook = false;
            playerInputs.look = Vector2.zero; // <-- BURASI KRİTİK: Freeze kapalı olsa bile ivmeyi sıfırlar!
        }

        if (freezePlayer)
        {
            playerMoveScript.SetFrozen(true, lockCameraInput: true, restrictRotation: false);
            if (playerInputs != null)
                playerInputs.move = Vector2.zero;
        }
        // 2. KAMERA ODAKLANMASI
        if (focusMode == FocusMode.RotatePlayerHead && lookTarget != null && mainCam != null)
        {
            Quaternion startRot = mainCam.transform.rotation;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / headTurnDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Quaternion targetLookRot = GetTargetRotation();
                Quaternion currentRot = Quaternion.Slerp(startRot, targetLookRot, smoothT);

                ApplyRotationToPlayer(currentRot);
                yield return null;
            }
        }
        else if (focusMode == FocusMode.ActivateVirtualCamera && targetVirtualCamera != null)
        {
            targetVirtualCamera.Priority = 100;
        }

        // 3. BEKLEME EVRESİ
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
        else
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
            playerInputs.look = Vector2.zero; // <-- Çıkarken de her ihtimale karşı temizle
            playerInputs.cursorInputForLook = true;
        }
    }

    private void TrackTarget()
    {
        Quaternion targetLookRot = GetTargetRotation();
        ApplyRotationToPlayer(targetLookRot);
    }

    private void ApplyRotationToPlayer(Quaternion rot)
    {
        float currentYaw = rot.eulerAngles.y;
        float currentPitch = rot.eulerAngles.x;
        if (currentPitch > 180f)
            currentPitch -= 360f;

        playerMoveScript.ForceCameraRotation(currentYaw, currentPitch);

        if (freezePlayer)
        {
            // BU SATIRI DA YORUM SATIRI YAPIN
            // playerMoveScript.transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }
}
