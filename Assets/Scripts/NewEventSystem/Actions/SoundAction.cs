using UnityEngine;

public class SoundAction : ActionBase
{
    [Header("Ses Ayarları")]
    [Tooltip("Sesin çalınacağı kaynak. Boş bırakırsan bu objedeki AudioSource'u kullanır.")]
    public AudioSource audioSource;
    
    [Tooltip("Çalınacak ses dosyası (AudioClip).")]
    public AudioClip soundClip;

    [Header("Çalma Seçenekleri")]
    [Tooltip("True: Sesi PlayOneShot ile çalar (üst üste binebilir, kesilmez). False: Mevcut sesi kesip baştan çalar.")]
    public bool playOneShot = true;
    
    [Tooltip("Sesin yüksekliği (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Rastgelelik (Opsiyonel)")]
    [Tooltip("Pitch (ses inceliği/kalınlığı) değerini hafifçe değiştirerek mekanik hissi azaltır (Korku oyunları için idealdir).")]
    public bool randomizePitch = false;
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    private void Start()
    {
        // Eğer Inspector'dan kaynak atanmadıysa, bu objede AudioSource var mı diye otomatik kontrol et
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    protected override void PerformAction()
    {
        if (audioSource == null)
        {
            Debug.LogWarning($"[SoundAction] {gameObject.name} üzerinde AudioSource bulunamadı! Lütfen objeye AudioSource ekleyin veya Inspector'dan atayın.");
            return;
        }

        if (soundClip == null)
        {
            Debug.LogWarning($"[SoundAction] {gameObject.name} için çalınacak bir AudioClip atanmamış!");
            return;
        }

        // Pitch rastgeleliği aktifse, sesi her çaldığında hafifçe değiştir
        if (randomizePitch)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
        }
        else
        {
            audioSource.pitch = 1f; // Rastgelelik kapalıysa normal değerine sıfırla
        }

        // Sesi Çal
        if (playOneShot)
        {
            audioSource.PlayOneShot(soundClip, volume);
        }
        else
        {
            audioSource.clip = soundClip;
            audioSource.volume = volume;
            audioSource.Play();
        }
    }
}