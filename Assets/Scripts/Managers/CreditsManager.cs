using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditsManager : MonoBehaviour
{
    [SerializeField]
    private float displayTime = 5.0f; // Ekranda kalma süresi

    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        StartCoroutine(GoToMenuRoutine());
    }

    private IEnumerator GoToMenuRoutine()
    {
        // Kayan yazı animasyonu varsa burada başlatabilirsin.

        yield return new WaitForSeconds(displayTime);

        // Ana menüye dön
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
