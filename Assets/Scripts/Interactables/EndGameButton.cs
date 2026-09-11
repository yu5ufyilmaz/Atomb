using System.Collections;
using UnityEngine;
using UnityEngine.Playables; // Timeline (PlayableDirector) için gerekli kütüphane
using UnityEngine.SceneManagement;

public class EndGameButton : MonoBehaviour, IInteractable
{
    [Header("Player Settings")]
    public GameObject player;
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour playerLookScript;

    [Header("Final Sinematik Ayarları")]
    [Tooltip("Oyun sonunda çalışacak Timeline objesini buraya sürükle.")]
    [SerializeField]
    private PlayableDirector finalTimeline;

    [SerializeField]
    private string creditsSceneName = "CreditsScene";

    private bool isTriggered = false;

    public void Interact()
    {
        if (isTriggered)
            return;
        StartCoroutine(EndingSequence());
    }

    public string GetInteractionPrompt()
    {
        return isTriggered ? "" : "[Sol Tık] Sistemi Başlat";
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    private IEnumerator EndingSequence()
    {
        isTriggered = true;

        // 1. OYUNCU KONTROLLERİNİ KAPAT VE DONDUR
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerMovementScript != null)
            playerMovementScript.enabled = false;
        if (playerLookScript != null)
            playerLookScript.enabled = false;

        if (player != null)
        {
            var rbs = player.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
                rb.isKinematic = true;

            var ccs = player.GetComponentsInChildren<UnityEngine.CharacterController>();
            foreach (var cc in ccs)
                cc.enabled = false;
        }

        // 2. TIMELINE'I BAŞLAT VE BİTMESİNİ BEKLE
        if (finalTimeline != null)
        {
            finalTimeline.Play();
            // Timeline'ın kendi süresi kadar bekle
            yield return new WaitForSeconds((float)finalTimeline.duration);
        }
        else
        {
            Debug.LogWarning("Final Timeline atanmamış! Emniyet için 5 saniye bekleniyor...");
            yield return new WaitForSeconds(5.0f);
        }

        // 3. JENERİK (CREDITS) SAHNESİNE GEÇİŞ YAP
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(creditsSceneName);
    }
}
