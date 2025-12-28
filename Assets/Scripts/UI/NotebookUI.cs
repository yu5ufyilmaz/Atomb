using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class NotebookUI : MonoBehaviour
{
    public static NotebookUI Instance;

    [Header("UI Referansları")]
    [SerializeField]
    private GameObject notebookPanel;

    [SerializeField]
    private TextMeshProUGUI passwordListText;

    [Header("Hareket Ayarları")]
    [Tooltip("Defterin kapalıyken duracağı konum (Ekranın altı)")]
    [SerializeField]
    private Vector2 hiddenPosition = new Vector2(0, -800); // Ekran dışı (aşağısı)

    [Tooltip("Defterin açıkken duracağı konum (Ekran ortası)")]
    [SerializeField]
    private Vector2 visiblePosition = Vector2.zero; // Ekran merkezi

    [Tooltip("Kayma hızı (Yüksek değer = Hızlı)")]
    [SerializeField]
    private float moveSpeed = 10f;

    [Header("Bildirim Ayarları")]
    [SerializeField]
    private GameObject notificationPanel;

    [SerializeField]
    private TextMeshProUGUI notificationText;

    [SerializeField]
    private float notificationDuration = 3.0f;

    private RectTransform panelRect;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Paneli başta aktif yapıyoruz (Yoksa hareket edemez)
        if (notebookPanel != null)
        {
            notebookPanel.SetActive(true);
            panelRect = notebookPanel.GetComponent<RectTransform>();

            // Başlangıçta gizli konuma gönder
            if (panelRect != null)
                panelRect.anchoredPosition = hiddenPosition;
        }

        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private void Update()
    {
        HandleNotebookInput();
    }

    private void HandleNotebookInput()
    {
        if (panelRect == null)
            return;

        // TAB tuşuna basılı tutuluyor mu?
        bool isHolding = Input.GetKey(KeyCode.Tab);

        // Hedef konumu belirle (Basılıysa Görünür, Değilse Gizli)
        Vector2 targetPos = isHolding ? visiblePosition : hiddenPosition;

        // Yumuşak geçiş yap (Lerp)
        panelRect.anchoredPosition = Vector2.Lerp(
            panelRect.anchoredPosition,
            targetPos,
            Time.deltaTime * moveSpeed
        );

        // Tuşa ilk basıldığı an listeyi güncelle (Performans için her kare yapmıyoruz)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            UpdatePasswordList();
        }
    }

    private void UpdatePasswordList()
    {
        if (PasswordManager.Instance == null)
            return;

        List<string> passwords = PasswordManager.Instance.GetDiscoveredClues();
        StringBuilder sb = new StringBuilder();

        foreach (string pw in passwords)
        {
            sb.AppendLine(pw.Replace("_", " "));
        }

        if (passwordListText != null)
            passwordListText.text = sb.ToString();
    }

    public void ShowPasswordNotification(string passwordID)
    {
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(true);
            if (notificationText != null)
                notificationText.text = $"YENİ İPUCU:\n{passwordID.Replace("_", " ")}";

            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), notificationDuration);
        }
    }

    private void HideNotification()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }
}
