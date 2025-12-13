using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video; // <--- VİDEO İÇİN GEREKLİ KÜTÜPHANE

public class EndGameButton : MonoBehaviour, IInteractable
{
    [Header("Bitiş Ayarları")]
    [Tooltip("Kameranın kilitleneceği ekran noktası")]
    [SerializeField]
    private Transform screenViewTarget;

    [Tooltip("Final videosunun olduğu Video Player bileşeni (Ekran objesi)")]
    [SerializeField]
    private VideoPlayer finalVideoPlayer; // <--- YENİ REFERANS

    [Tooltip("Credits sahnesinin tam adı")]
    [SerializeField]
    private string creditsSceneName = "CreditsScene";

    [Header("Kamera Geçişi")]
    [SerializeField]
    private float moveDuration = 1.0f;

    private bool isTriggered = false;
    private Transform mainCamera;

    // Oyuncu bileşenleri
    private UnityEngine.CharacterController playerController;
    private MonoBehaviour playerInput;

    private void Start()
    {
        if (Camera.main != null)
            mainCamera = Camera.main.transform;

        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController != null)
            playerInput = playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;

        // Videoyu önceden hazırla ki takılmadan başlasın
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

    private IEnumerator EndingSequence()
    {
        isTriggered = true;

        // 1. Oyuncuyu Dondur & UI Kapat
        if (playerController)
            playerController.enabled = false;
        if (playerInput)
            playerInput.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 2. Kamerayı Ekrana Taşı
        if (screenViewTarget != null && mainCamera != null)
        {
            // Cinemachine varsa kapat
            var brain = mainCamera.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain)
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

        // 3. Videoyu Oynat ve Bitmesini Bekle
        if (finalVideoPlayer != null)
        {
            Debug.Log("Final videosu başlatılıyor...");
            finalVideoPlayer.Play();

            // Video süresi kadar bekle (Saniye cinsinden)
            // .length özelliği videonun toplam saniyesini verir
            yield return new WaitForSeconds((float)finalVideoPlayer.length);
        }
        else
        {
            Debug.LogWarning("Video Player atanmamış! Varsayılan olarak 5 saniye bekleniyor.");
            yield return new WaitForSeconds(5.0f);
        }

        // 4. Credits Sahnesine Geç
        Debug.Log("Video bitti. Credits yükleniyor...");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(creditsSceneName);
    }
}
