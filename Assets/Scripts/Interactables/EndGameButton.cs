using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class EndGameButton : MonoBehaviour, IInteractable
{
    [Header("Player Settings")]
    public GameObject player;
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour playerLookScript;

    [Header("Bitiş Ayarları")]
    [Tooltip("Kameranın kilitleneceği nokta (Mavi ok ekrana baksın!)")]
    [SerializeField]
    private Transform screenViewTarget;

    [SerializeField]
    private VideoPlayer finalVideoPlayer;

    [SerializeField]
    private string creditsSceneName = "CreditsScene";

    [Header("Oturma Animasyonu")]
    [SerializeField]
    private float sitDuration = 2.0f;

    [SerializeField]
    private AnimationCurve sitCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isTriggered = false;
    private Transform mainCamera;

    private void Start()
    {
        if (Camera.main != null)
            mainCamera = Camera.main.transform;

        if (finalVideoPlayer != null)
            finalVideoPlayer.Prepare();
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

        // 1. TÜM KONTROLLERİ KAPAT
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
                rb.isKinematic = true; // Fiziksel düşmeyi engelle

            var ccs = player.GetComponentsInChildren<UnityEngine.CharacterController>();
            foreach (var cc in ccs)
                cc.enabled = false; // Çarpışmayı engelle
        }

        if (mainCamera != null)
        {
            var brain = mainCamera.GetComponent("CinemachineBrain") as MonoBehaviour;
            if (brain != null)
                brain.enabled = false;
        }

        // --- 2. KAMERAYI OYUNCUDAN KOPAR (FIX) ---
        // Kamerayı oyuncunun hiyerarşisinden çıkarıyoruz.
        // Böylece oyuncu dönse bile kamera dönmez.
        if (mainCamera != null)
        {
            mainCamera.SetParent(null);
        }

        // --- 3. OTURMA HAREKETİ ---
        if (screenViewTarget != null && mainCamera != null)
        {
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;

            float timer = 0f;

            while (timer < sitDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / sitDuration;
                float curveValue = sitCurve.Evaluate(progress);

                // Pozisyon
                mainCamera.position = Vector3.Lerp(startPos, screenViewTarget.position, curveValue);

                // Rotasyon (BASİTLEŞTİRİLDİ)
                // "LookRotation" kullanmıyoruz. Direkt hedefin rotasyonuna yumuşak geçiş yapıyoruz.
                // Böylece sonda "küt" diye atlama yapmaz.
                mainCamera.rotation = Quaternion.Slerp(
                    startRot,
                    screenViewTarget.rotation,
                    curveValue
                );

                yield return null;
            }

            // Garanti olsun diye tam oturt ve HEDEFE BAĞLA
            mainCamera.position = screenViewTarget.position;
            mainCamera.rotation = screenViewTarget.rotation;

            // Kamerayı artık hedef objenin çocuğu yapıyoruz. Obje nereye bakıyorsa oraya bakar.
            mainCamera.SetParent(screenViewTarget);
        }

        // --- 4. VİDEO ---
        if (finalVideoPlayer != null)
        {
            finalVideoPlayer.Play();
            yield return new WaitForSeconds((float)finalVideoPlayer.length);
        }
        else
        {
            yield return new WaitForSeconds(5.0f);
        }

        // --- 5. BİTİŞ ---
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(creditsSceneName);
    }
}
