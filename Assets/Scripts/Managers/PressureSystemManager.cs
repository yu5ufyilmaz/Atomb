using TMPro; // TextMeshPro kullanıyorsanız (UI için)
using UnityEngine;

public class PressureSystemManager : MonoBehaviour
{
    public static PressureSystemManager Instance;

    [Header("Pressure Settings")]
    [Range(0, 100)]
    public float currentPressure = 0f;

    [Tooltip("Saniyede artacak basınç miktarı")]
    [SerializeField]
    private float pressureIncreaseRate = 2.0f;

    [Tooltip("Basınç %90'ı geçince uyarı başlar")]
    [SerializeField]
    private float warningThreshold = 90f;

    [Tooltip("Oyuncu vanayı çevirdiğinde saniyede düşecek basınç miktarı")]
    public float pressureDecreaseRate = 15f;

    [Header("Handheld Device (B Key)")]
    [SerializeField]
    private GameObject handheldDeviceUI; // B'ye basınca açılacak UI paneli

    [SerializeField]
    private TextMeshProUGUI pressureText; // Paneldeki yazı

    [Header("Game Over / Warning")]
    [SerializeField]
    private GameObject explosionEffect; // %100 olunca çıkacak efekt

    [SerializeField]
    private GameObject warningUI; // %90 üstü uyarı ikonu/yazısı

    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (handheldDeviceUI != null)
            handheldDeviceUI.SetActive(false);
        if (warningUI != null)
            warningUI.SetActive(false);
    }

    private void Update()
    {
        if (isGameOver)
            return;

        // 1. Basınç Artışı (Background Counter)
        // PDF: "From the moment the game starts... Pressure Value increases continuously."
        currentPressure += pressureIncreaseRate * Time.deltaTime;

        // 2. Death Check (%100)
        if (currentPressure >= 100f)
        {
            currentPressure = 100f;
            TriggerGameOver();
        }

        // 3. Warning State (%90)
        if (currentPressure > warningThreshold)
        {
            if (warningUI != null && !warningUI.activeSelf)
                warningUI.SetActive(true);
            // Buraya alarm sesi çalma kodu eklenebilir.
        }
        else
        {
            if (warningUI != null && warningUI.activeSelf)
                warningUI.SetActive(false);
        }

        // 4. Handheld Device Check ('B' Key)
        // PDF: "Holding the key reveals the pressure level via the handheld device."
        HandleHandheldDevice();
    }

    private void HandleHandheldDevice()
    {
        if (Input.GetKey(KeyCode.B))
        {
            if (handheldDeviceUI != null)
            {
                handheldDeviceUI.SetActive(true);
                if (pressureText != null)
                {
                    pressureText.text = $"PRESSURE LEVEL\n%{currentPressure:F1}";
                    pressureText.color = currentPressure > 90 ? Color.red : Color.green;
                }
            }
        }
        else
        {
            if (handheldDeviceUI != null && handheldDeviceUI.activeSelf)
            {
                handheldDeviceUI.SetActive(false);
            }
        }
    }

    // Vanadan çağrılacak fonksiyon
    public void ReducePressure(float amount)
    {
        if (isGameOver)
            return;
        currentPressure -= amount;
        if (currentPressure < 0)
            currentPressure = 0;
    }

    private void TriggerGameOver()
    {
        isGameOver = true;
        Debug.LogError("GAME OVER: PRESSURE REACHED 100%!");

        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // Time.timeScale = 0;
    }

    #region GETTERS
    public float GetPressure() => currentPressure;

    public float GetWarningThreshold() => warningThreshold;

    public bool IsWarningActive() => currentPressure > warningThreshold;
    
    #endregion
}
