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

    [Tooltip("Defterin içindeki ana yazı alanı (Eskiden passwordListText idi)")]
    [SerializeField]
    private TextMeshProUGUI pageContentText;

    [Tooltip("Sayfa numarasını gösterecek ufak yazı (Örn: Sayfa 1 / 3)")]
    [SerializeField]
    private TextMeshProUGUI pageNumberText;

    [Header("Hareket Ayarları")]
    [Tooltip("Defter kapalıyken (Ekran Dışı)")]
    [SerializeField]
    private Vector2 hiddenPosition = new Vector2(0, -800);

    [Tooltip("Defter açıkken (Ekranın yanı - Örneğin X: 400, Y: 0 yapabilirsin)")]
    [SerializeField]
    private Vector2 visiblePosition = new Vector2(400, 0);

    [SerializeField]
    private float moveSpeed = 10f;

    [Header("Bildirim Ayarları")]
    [SerializeField]
    private GameObject notificationPanel;

    [SerializeField]
    private TextMeshProUGUI notificationText;

    [SerializeField]
    private float notificationDuration = 3.0f;

    // --- YENİ: SAYFA SİSTEMİ DEĞİŞKENLERİ ---
    private bool isOpen = false;
    private int currentPage = 0; // 0 = Şifreler Sayfası, 1, 2, 3... = Sembol Notları
    private List<string> lorePages = new List<string>(); // Toplanan sembollerin hikayeleri/notları

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
        if (notebookPanel != null)
        {
            notebookPanel.SetActive(true);
            panelRect = notebookPanel.GetComponent<RectTransform>();
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

        // 1. AÇMA / KAPATMA MANTIĞI (TOGGLE)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isOpen = !isOpen;
            if (isOpen)
                UpdatePageDisplay(); // Defter açılınca mevcut sayfayı ekrana bas
        }

        // Hedef konuma yumuşak geçiş
        Vector2 targetPos = isOpen ? visiblePosition : hiddenPosition;
        panelRect.anchoredPosition = Vector2.Lerp(
            panelRect.anchoredPosition,
            targetPos,
            Time.deltaTime * moveSpeed
        );

        // 2. SAYFA ÇEVİRME MANTIĞI (Sadece defter açıkken)
        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.E)) // İleri Sayfa
            {
                if (currentPage < GetTotalPages() - 1)
                {
                    currentPage++;
                    UpdatePageDisplay();
                }
            }
            else if (Input.GetKeyDown(KeyCode.Q)) // Geri Sayfa
            {
                if (currentPage > 0)
                {
                    currentPage--;
                    UpdatePageDisplay();
                }
            }
        }
    }

    private int GetTotalPages()
    {
        // 1 Ana Şifreler Sayfası + Toplanan Hikaye Sayfaları
        return 1 + lorePages.Count;
    }

    private void UpdatePageDisplay()
    {
        if (pageContentText == null)
            return;

        if (currentPage == 0)
        {
            // SAYFA 0: ŞİFRELER EKRANI
            pageContentText.text = GetPasswordsText();
        }
        else
        {
            // DİĞER SAYFALAR: SEMBOL/HİKAYE EKRANLARI
            int loreIndex = currentPage - 1;
            if (loreIndex >= 0 && loreIndex < lorePages.Count)
            {
                pageContentText.text = lorePages[loreIndex];
            }
        }

        // Sayfa numarasını güncelle (Eğer UI'da atadıysan)
        if (pageNumberText != null)
        {
            pageNumberText.text = $"- Sayfa {currentPage + 1} / {GetTotalPages()} -";
        }
    }

    private string GetPasswordsText()
    {
        if (PasswordManager.Instance == null)
            return "Şifre bulunamadı.";

        List<string> passwords = PasswordManager.Instance.GetDiscoveredClues();
        if (passwords.Count == 0)
            return "Henüz şifre bulunamadı...";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<size=120%><b>Bulunan Şifreler</b></size>\n");

        foreach (string pw in passwords)
        {
            bool isUsed = PasswordManager.Instance.IsPasswordUsed(pw);
            string displayText = pw.Replace("_", " ");

            if (isUsed)
                sb.AppendLine($"<s>{displayText}</s> <color=green>✓</color>");
            else
                sb.AppendLine(displayText);
        }
        return sb.ToString();
    }

    // --- YENİ EKLENEN: DÜNYADAN SEMBOL ALININCA ÇAĞRILACAK FONKSİYON ---
    public void AddLorePage(string newLoreText)
    {
        lorePages.Add(newLoreText); // Listeye yeni sayfayı ekle

        // Eklendiğine dair bildirim çıkar
        ShowNotification("YENİ GÜNLÜK SAYFASI EKLENDİ");

        // Eğer oyuncu deftere o an bakıyorsa ekranı anında tazele
        if (isOpen)
            UpdatePageDisplay();
    }

    public void ShowPasswordNotification(string passwordID)
    {
        ShowNotification($"YENİ İPUCU:\n{passwordID.Replace("_", " ")}");

        // Şifreler sayfasındayken yeni şifre gelirse anında güncelle
        if (isOpen && currentPage == 0)
            UpdatePageDisplay();
    }

    private void ShowNotification(string message)
    {
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(true);
            if (notificationText != null)
                notificationText.text = message;

            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), notificationDuration);
        }
    }

    public void ForceClose()
    {
        isOpen = false;
        if (panelRect != null)
            panelRect.anchoredPosition = hiddenPosition;
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private void HideNotification()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }
}
