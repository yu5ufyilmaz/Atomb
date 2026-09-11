using UnityEngine;

[System.Serializable]
public class JumpscareProfile
{
    [Header("Süre ve Hedef")]
    public float duration = 2.5f;

    [Tooltip("Kameranın düşmanın neresine odaklanacağı (Yükseklik)")]
    public float enemyEyeHeightOffset = 1.3f;

    [Header("Kamera Efektleri")]
    public float targetFOV = 40f;
    public float tiltAngle = 10f; // Sola/Sağa yatma

    [Header("Sarsıntı (Shake)")]
    public float shakeIntensity = 0.5f;
    public float shakeFrequency = 20f;
}
