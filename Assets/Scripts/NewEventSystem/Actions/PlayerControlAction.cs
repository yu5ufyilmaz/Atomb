using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public class PlayerControlAction : MonoBehaviour, IAction
{
    [Header("Hareket ve Kamera Kontrolü")]
    public bool modifyMovement = false;
    [Tooltip("modifyMovement tikliyse: Oyuncu yürüyemesin mi?")]
    public bool freezePlayer = false; 

    public bool modifyLook = false;
    [Tooltip("modifyLook tikliyse: Oyuncunun faresi (kamerası) kilitlensin mi?")]
    public bool lockCameraInput = false; 

    [Header("Etkileşim Sınırlamaları (Tutorial Mode)")]
    public bool modifyInteractionRules = false;
    [Tooltip("False yaparsan Tutorial Mode tamamen kapanır, oyuncu her şeye tıklayabilir.")]
    public bool enableTutorialMode = true; 
    [Tooltip("Oyuncu SADECE bu listedeki objelerle etkileşime girebilir.")]
    public List<GameObject> allowedInteractables = new List<GameObject>();

    public void Execute()
    {
        // 1. OYUNCU HAREKET VE KAMERA AYARLARI
        if (modifyMovement || modifyLook)
        {
            var playerScript = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
            var playerInputs = Object.FindFirstObjectByType<StarterAssetsInputs>();

            if (playerScript != null && playerInputs != null)
            {
                if (modifyLook)
                {
                    playerInputs.cursorInputForLook = !lockCameraInput;
                }

                if (modifyMovement)
                {
                    playerScript.SetFrozen(freezePlayer, lockCameraInput, false);
                    if (freezePlayer) playerInputs.move = Vector2.zero;
                }
            }
        }

        // 2. ETKİLEŞİM İZİNLERİ (WHITELIST)
        if (modifyInteractionRules && PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.isTutorialMode = enableTutorialMode;
            
            if (enableTutorialMode)
            {
                PlayerInteraction.Instance.allowedTutorialObjects.Clear();
                if (allowedInteractables != null && allowedInteractables.Count > 0)
                {
                    PlayerInteraction.Instance.allowedTutorialObjects.AddRange(allowedInteractables);
                }
                Debug.Log($"[PlayerControlAction] Kısıtlı Etkileşim Aktif. İzin verilen obje sayısı: {allowedInteractables.Count}");
            }
            else
            {
                Debug.Log("[PlayerControlAction] Kısıtlamalar Kaldırıldı. Oyuncu artık her şeye tıklayabilir.");
            }
        }
    }
}