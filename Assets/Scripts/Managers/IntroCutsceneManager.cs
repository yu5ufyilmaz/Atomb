using UnityEngine;
using UnityEngine.Playables;

public class IntroCutsceneManager : MonoBehaviour
{
    [Header("Cutscene Ayarları")]
    public PlayableDirector introTimeline;

    [Header("Oyuncu Kontrolleri")]
    public MonoBehaviour playerMovementScript; // Karakter hareket scriptini sürükle
    public MonoBehaviour playerLookScript; // Mouse bakış scriptini sürükle

    void Start()
    {
        // 1. Oyun (veya sahne) başladığında kontrolleri kapat
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;
        if (playerLookScript != null)
            playerLookScript.enabled = false;

        // 2. Timeline'ın bitiş anını dinlemeye başla
        if (introTimeline != null)
        {
            introTimeline.stopped += OnCutsceneEnded; // Timeline durduğunda bu metodu çalıştır
        }
    }

    // Timeline bittiğinde otomatik olarak burası çalışır
    private void OnCutsceneEnded(PlayableDirector pd)
    {
        // Kontrolleri geri ver
        if (playerMovementScript != null)
            playerMovementScript.enabled = true;
        if (playerLookScript != null)
            playerLookScript.enabled = true;

        Debug.Log("Intro bitti, oyuncu kontrolü aldı!");
    }

    private void OnDestroy()
    {
        // Hata almamak için obje yok olduğunda dinlemeyi bırak
        if (introTimeline != null)
            introTimeline.stopped -= OnCutsceneEnded;
    }
}
