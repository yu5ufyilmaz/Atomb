using UnityEngine;
using UnityEngine.Video;

// Final perdesi: sahne yüklenirken video başlamasın, ara sahnenin Activation Track'i nesneyi yeniden açınca başlasın.
// VideoPlayer'daki Play On Awake kapalı tutulur; ilk etkinleşme sadece kurar.
[RequireComponent(typeof(VideoPlayer))]
public sealed class PlayVideoOnReactivate : MonoBehaviour
{
    private bool armed;

    private void OnEnable()
    {
        if (armed) GetComponent<VideoPlayer>().Play();
        armed = true;
    }
}
