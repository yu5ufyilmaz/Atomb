using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class JumpscareProfile
{
    [Header("Ara Sahne (Timeline)")]
    [Tooltip("Atanırsa jumpscare sırasında bu Timeline oynatılır ve Experiment Failed ekrana gelmeden önce bitmesi beklenir.")]
    public PlayableDirector cutsceneDirector;

    [Tooltip("Açıksa Timeline kamera/karakter/kapı gibi tüm hareketi yönetir; otomatik kamera takibi ve camera clip devre dışı kalır.")]
    public bool cutsceneTakesFullControl = false;

    [Tooltip("Açıksa Timeline oynarken de jumpscare post-process efektleri uygulanır.")]
    public bool applyProfileEffectsDuringCutscene = false;

    [Tooltip("Guderian jumpscare başında Animator trigger'ını tetiklesin mi? Timeline Guderian'ı yönetiyorsa kapat.")]
    public bool triggerEnemyAnimationOnStart = true;

    [Header("Süre ve Hedef")]
    public float duration = 2.5f;

    [Tooltip("Kameranın düşmanın neresine odaklanacağı (Yükseklik)")]
    public float enemyEyeHeightOffset = 1.3f;

    [Header("Kamera Efektleri")]
    public float targetFOV = 40f;
    public float tiltAngle = 10f; // Sola/Sağa yatma

    [Header("Kamera Hareketi")]
    [Tooltip("Kamera oyuncunun head bone'una bağlandığında kullanılacak yerel pozisyon.")]
    public Vector3 cameraLocalOffset = new Vector3(0f, 0.1f, 0.15f);

    [Tooltip("Kamera düşmana dönmeye başlamadan önce beklenecek süre.")]
    public float cameraRotationDelay = 0.35f;

    [Tooltip("0 veya daha düşükse JumpScareManager'ın varsayılan dönüş hızı kullanılır.")]
    public float cameraTurnSpeedOverride = 0f;

    [Header("Kamera Animasyonu")]
    [Tooltip("Atanırsa jumpscare sırasında Main Camera üzerinde bu animasyon oynatılır.")]
    public AnimationClip cameraAnimationClip;

    [Tooltip("Açıksa kamera transform'unu bu clip yönetir; kapalıysa clip üstüne procedural bakış/sarsıntı eklenir.")]
    public bool cameraAnimationOverridesLookAt = true;

    [Tooltip("Main Camera üzerinde Animator Controller varsa bu trigger jumpscare başında tetiklenir.")]
    public string cameraAnimatorTrigger;

    [Tooltip("Camera Animator trigger ile oynayan klibi buraya ver; Experiment Failed bundan önce açılmaz.")]
    public AnimationClip cameraAnimatorClipToWaitFor;

    [Header("Sarsıntı (Shake)")]
    public float shakeIntensity = 0.5f;
    public float shakeFrequency = 20f;

    [Header("Ölüm Ekranı")]
    [Tooltip("Experiment Failed ekranı açılmadan önce bitmesi beklenecek düşman animasyonu.")]
    public AnimationClip enemyAnimationClipToWaitFor;

    [Tooltip("Düşman/kamera animasyonu bittikten sonra ekstra bekleme süresi.")]
    public float deathScreenExtraDelay = 0f;
}
