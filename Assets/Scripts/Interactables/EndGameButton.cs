using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class EndGameButton : MonoBehaviour, IInteractable
{
    [Header("Player Settings")]
    [Tooltip("Karakterin Ana Objesi")]
    public GameObject player;

    [Tooltip("Karakterin Hareket Scripti (Örn: FirstPersonController)")]
    public MonoBehaviour playerMovementScript;

    [Tooltip("Karakterin Kamera/Mouse Scripti (Varsa buraya at, yoksa boş kalabilir)")]
    public MonoBehaviour playerLookScript;

    [Header("Bitiş Ayarları")]
    [Tooltip("Kameranın kilitleneceği ekran noktası")]
    [SerializeField]
    private Transform screenViewTarget;

    [Tooltip("Final videosunun olduğu Video Player bileşeni")]
    [SerializeField]
    private VideoPlayer finalVideoPlayer;

    [Tooltip("Credits sahnesinin tam adı")]
    [SerializeField]
    private string creditsSceneName = "CreditsScene";

    [Header("Kamera Geçişi")]
    [SerializeField]
    private float moveDuration = 1.0f;

    private bool isTriggered = false;
    private Transform mainCamera;

    private void Start()
    {
        if (Camera.main != null)
            mainCamera = Camera.main.transform;

        // Videoyu önceden hazırla
        if (finalVideoPlayer != null)
        {
            finalVideoPlayer.Prepare();
        }
    }

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

        // -----------------------------------------------------------
        // ADIM 1: OYUNCUYU TAMAMEN DEVRE DIŞI BIRAK (FIX)
        // -----------------------------------------------------------

        // 1. Hareket Scriptini Kapat
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        // 2. Mouse Look Scriptini Kapat (Dönmeyi engelleyen asıl kısım)
        if (playerLookScript != null)
            playerLookScript.enabled = false;

        // 3. Unity Character Controller'ı Kapat (Fizik çakışmasını önler)
        if (player != null)
        {
            var cc = player.GetComponent<UnityEngine.CharacterController>();
            if (cc != null)
                cc.enabled = false;

            // Eğer Rigidbody varsa onu da dondur
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;
        }

        // Fareyi kilitle ve gizle (Video izlenirken fare görünmesin)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // -----------------------------------------------------------
        // ADIM 2: KAMERAYI EKRANA TAŞI
        // -----------------------------------------------------------
        if (screenViewTarget != null && mainCamera != null)
        {
            // Cinemachine Brain varsa kapat (Kamerayı serbest bırakmak için şart)
            var brain = mainCamera.GetComponent("CinemachineBrain") as MonoBehaviour;
            // Not: Cinemachine namespace hatası almamak için string ile çağırdım,
            // projenin başında "using Cinemachine;" varsa direkt tipi yazabilirsin.
            if (brain != null)
                brain.enabled = false;

            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / moveDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                mainCamera.position = Vector3.Lerp(startPos, screenViewTarget.position, smoothT);
                mainCamera.rotation = Quaternion.Slerp(
                    startRot,
                    screenViewTarget.rotation,
                    smoothT
                );

                yield return null;
            }
        }

        // -----------------------------------------------------------
        // ADIM 3: VİDEOYU OYNAT
        // -----------------------------------------------------------
        if (finalVideoPlayer != null)
        {
            Debug.Log("Final videosu başlatılıyor...");
            finalVideoPlayer.Play();

            // Video uzunluğu kadar bekle
            yield return new WaitForSeconds((float)finalVideoPlayer.length);
        }
        else
        {
            Debug.LogWarning("Video Player atanmamış! 5 sn bekleniyor.");
            yield return new WaitForSeconds(5.0f);
        }

        // -----------------------------------------------------------
        // ADIM 4: SAHNE GEÇİŞİ
        // -----------------------------------------------------------
        Debug.Log("Credits yükleniyor...");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(creditsSceneName);
    }
}
