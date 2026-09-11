using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ControlsUIManager : MonoBehaviour
{
    public static ControlsUIManager Instance;

    public enum MachineType
    {
        Generic, // Sadece yazı gösteren basit mod
        MassSpectrometer, // Kütle Spektrometresi
        TuringMachine, // Turing Makinesi
        Oscilloscope, // Osiloskop
        PressureValve, // Basınç Vanası
        Book, // Kitap Okuma
    }

    [Header("Ana UI Referansları")]
    [SerializeField]
    private GameObject mainCanvasObj; // Sol alttaki panelin ana objesi

    [SerializeField]
    private float fadeDuration = 0.3f;

    [Header("HUD Yönetimi (Otomatik Gizleme)")]
    [Tooltip("Makine paneli açıldığında gizlenecek diğer UI ögeleri (Progress Bar, Stamina vb.)")]
    [SerializeField]
    private List<GameObject> hudElementsToHide; // Buraya Progress Bar vb. sürükle

    [Header("Özel Makine Panelleri")]
    [SerializeField]
    private GameObject genericTextPanel;

    [SerializeField]
    private TextMeshProUGUI genericText;

    [SerializeField]
    private GameObject massSpectrometerPanel;

    [SerializeField]
    private GameObject turingMachinePanel;

    [SerializeField]
    private GameObject oscilloscopePanel;

    [SerializeField]
    private GameObject pressureValvePanel;

    [SerializeField]
    private GameObject bookPanel;

    private CanvasGroup canvasGroup;
    private GameObject currentActivePanel;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (mainCanvasObj != null)
        {
            canvasGroup = mainCanvasObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = mainCanvasObj.AddComponent<CanvasGroup>();

            // Başlangıçta her şeyi gizle
            canvasGroup.alpha = 0;
            mainCanvasObj.SetActive(false);

            HideAllSubPanels();
        }
    }

    /// <summary>
    /// İstenilen makineye ait özel UI panelini açar ve HUD'u gizler.
    /// </summary>
    public void ShowMachineUI(MachineType type, string optionalText = "")
    {
        if (mainCanvasObj == null)
            return;

        // 1. Önce HUD (Progress barlar, Crosshair) GİZLE
        ToggleHUD(false);

        // 2. Alt panelleri sıfırla
        HideAllSubPanels();

        // 3. İstenen paneli belirle
        switch (type)
        {
            case MachineType.Generic:
                currentActivePanel = genericTextPanel;
                if (genericText != null)
                    genericText.text = optionalText;
                break;

            case MachineType.MassSpectrometer:
                currentActivePanel = massSpectrometerPanel;
                break;

            case MachineType.TuringMachine:
                currentActivePanel = turingMachinePanel;
                break;

            case MachineType.Oscilloscope:
                currentActivePanel = oscilloscopePanel;
                break;

            case MachineType.PressureValve:
                currentActivePanel = pressureValvePanel;
                break;

            case MachineType.Book:
                currentActivePanel = bookPanel;
                break;

            default:
                Debug.LogWarning("ControlsUI: Tanımlanmamış makine tipi!");
                return;
        }

        // 4. Seçilen paneli aktif et ve Fade In başlat
        if (currentActivePanel != null)
            currentActivePanel.SetActive(true);

        mainCanvasObj.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f));
    }

    /// <summary>
    /// Panelleri gizler ve HUD'u geri açar (Oyuncu kalkınca).
    /// </summary>
    public void HideControls()
    {
        if (mainCanvasObj == null)
            return;

        // 1. HUD ve Crosshair GERİ AÇ
        ToggleHUD(true);

        // 2. Fade Out başlat
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f));
    }

    /// <summary>
    /// HUD objelerini (Progress Bar vb.) ve Crosshair'i açıp kapatır.
    /// </summary>
    private void ToggleHUD(bool state)
    {
        // Listeye eklediğin objeleri (Progress Bar vb.) aç/kapat
        if (hudElementsToHide != null)
        {
            foreach (var obj in hudElementsToHide)
            {
                if (obj != null)
                    obj.SetActive(state);
            }
        }

        // Crosshair ve Cursorları PlayerInteraction üzerinden yönet
        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.ToggleCrosshair(state);
        }
    }

    private void HideAllSubPanels()
    {
        // Hata almamak için null kontrolü yaparak kapatıyoruz
        if (genericTextPanel != null)
            genericTextPanel.SetActive(false);
        if (massSpectrometerPanel != null)
            massSpectrometerPanel.SetActive(false);
        if (turingMachinePanel != null)
            turingMachinePanel.SetActive(false);
        if (oscilloscopePanel != null)
            oscilloscopePanel.SetActive(false);
        if (pressureValvePanel != null)
            pressureValvePanel.SetActive(false);
        if (bookPanel != null)
            bookPanel.SetActive(false);
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0.01f)
        {
            mainCanvasObj.SetActive(false);
            HideAllSubPanels();
        }
    }
}
