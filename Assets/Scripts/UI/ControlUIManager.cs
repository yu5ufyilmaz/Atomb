using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        HidingSpot,
        Chalkboard,
        DoorLesson,
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

    [SerializeField]
    private GameObject hidingSpotPanel;

    private CanvasGroup canvasGroup;
    private GameObject currentActivePanel;
    private GameObject chalkboardPanel, doorLessonPanel;

    [Header("Makine Focus (Bulanıklık) Ayarları")]
    public float chalkboardFocus = 0.8f;
    public float genericFocus = 0.5f;
    public float massSpectrometerFocus = 0.95f;
    public float turingMachineFocus = 0.77f;
    public float oscilloscopeFocus = 0.69f;
    public float pressureValveFocus = 1.3f;
    public float bookFocus = 0.32f;

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
                DoFManager.Instance.SetFocus(genericFocus); // Sabit 0.28f yerine değişkeni kullandık
                if (genericText != null)
                    GameLocalization.Set(genericText, optionalText);
                break;
            case MachineType.MassSpectrometer:
                currentActivePanel = massSpectrometerPanel;
                DoFManager.Instance.SetFocus(massSpectrometerFocus);
                break;
            case MachineType.TuringMachine:
                DoFManager.Instance.SetFocus(turingMachineFocus);
                currentActivePanel = turingMachinePanel;
                break;
            case MachineType.Oscilloscope:
                DoFManager.Instance.SetFocus(oscilloscopeFocus);
                currentActivePanel = oscilloscopePanel;
                break;
            case MachineType.PressureValve:
                DoFManager.Instance.SetFocus(pressureValveFocus);
                currentActivePanel = pressureValvePanel;
                break;
            case MachineType.Book:
                DoFManager.Instance.SetFocus(bookFocus);
                currentActivePanel = bookPanel;
                break;
            case MachineType.HidingSpot:
                currentActivePanel = hidingSpotPanel;
                break;
            case MachineType.Chalkboard: // <--- YENİ EKLENEN KISIM
                DoFManager.Instance.SetFocus(chalkboardFocus);
                currentActivePanel = EnsureChalkboardPanel();
                break;
            case MachineType.DoorLesson:
                currentActivePanel = EnsureDoorLessonPanel();
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

    // Sahnede tahta paneli yok; aynı görünsün diye kitap panelinin tuş satırları tahtaya uyarlanır.
    private GameObject EnsureChalkboardPanel()
    {
        if (chalkboardPanel != null || bookPanel == null) return chalkboardPanel;
        chalkboardPanel = Instantiate(bookPanel, bookPanel.transform.parent);
        chalkboardPanel.name = "Chalkboard";
        var panel = chalkboardPanel.transform;
        Destroy(panel.Find("Icon (5)").gameObject);
        var leftClick = panel.Find("Icon (3)");
        SetControlRow(leftClick, -820, "Sürükle / Bağla");
        var rightClick = panel.Find("Icon (4)");
        ((RectTransform)rightClick).sizeDelta = ((RectTransform)leftClick).sizeDelta;
        SetControlRow(rightClick, -590, "Sil");
        SetControlIcon(rightClick, "MouseRightButton", "SecondaryInteract", "<Mouse>/rightButton");
        var notebookKey = panel.Find("Icon (1)");
        ((RectTransform)notebookKey).sizeDelta = new Vector2(46, 36);
        SetControlRow(notebookKey, -480, "Defter");
        SetControlIcon(notebookKey, "Tab", "ToggleNotebook", "<Keyboard>/tab");
        return chalkboardPanel;
    }

    private GameObject EnsureDoorLessonPanel()
    {
        if (doorLessonPanel != null || bookPanel == null) return doorLessonPanel;
        doorLessonPanel = Instantiate(bookPanel, bookPanel.transform.parent);
        doorLessonPanel.name = "DoorLesson";
        var panel = doorLessonPanel.transform;
        foreach (string row in new[] { "Icon", "Icon (1)", "Icon (4)", "Icon (5)" })
            Destroy(panel.Find(row).gameObject);
        SetControlRow(panel.Find("Icon (3)"), -60, "Kapıyı Kapat");
        return doorLessonPanel;
    }

    private static void SetControlRow(Transform row, float x, string label)
    {
        var rect = (RectTransform)row;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, 2);
        var text = row.GetComponentInChildren<TMP_Text>(true);
        text.rectTransform.anchoredPosition = new Vector2(132.3f, 0);
        text.rectTransform.sizeDelta = new Vector2(220.9f, 36);
        text.alignment = TextAlignmentOptions.Left;
        GameLocalization.Set(text, label);
    }

    private static void SetControlIcon(Transform row, string sprite, string action, string control)
    {
        row.GetComponent<Image>().sprite = Resources.Load<Sprite>("ControlIcons/" + sprite);
        var icon = row.GetComponent<InputBindingIcon>();
        if (icon == null) icon = row.gameObject.AddComponent<InputBindingIcon>();
        icon.bindingID = GameInput.Action(action).bindings.FirstOrDefault(b => b.path == control).id.ToString();
        icon.artworkControlPath = control;
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
        DoFManager.Instance.ResetFocus();
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f));
    }

    /// <summary>
    /// HUD objelerini (Progress Bar vb.) ve Crosshair'i açıp kapatır.
    /// </summary>
    private void ToggleHUD(bool state)
    {
        // Listeye eklediğin objeleri (Progress Bar vb.) CanvasGroup ile aç/kapat
        if (hudElementsToHide != null)
        {
            foreach (var obj in hudElementsToHide)
            {
                if (obj == null)
                    continue;

                if (!obj.activeSelf)
                    obj.SetActive(true);

                CanvasGroup group = obj.GetComponent<CanvasGroup>();
                if (group == null)
                    group = obj.AddComponent<CanvasGroup>();

                group.alpha = state ? 1f : 0f;
                group.interactable = state;
                group.blocksRaycasts = state;
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
        if (hidingSpotPanel != null)
            hidingSpotPanel.SetActive(false);
        if (chalkboardPanel != null)
            chalkboardPanel.SetActive(false);
        if (doorLessonPanel != null)
            doorLessonPanel.SetActive(false);
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
