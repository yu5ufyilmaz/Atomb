using System.Collections.Generic;
using System.Text;
using TMPro; // TextMeshPro referansı için şart
using UnityEngine;

public class NotebookUI : MonoBehaviour
{
    public static NotebookUI Instance;

    [Header("UI Panelleri")]
    [SerializeField]
    private GameObject notebookPanel;

    [SerializeField]
    private TextMeshProUGUI passwordListText;

    [Header("Bildirim Ayarları")]
    [SerializeField]
    private GameObject notificationPanel; // Ekranda çıkacak "Şifre Bulundu" kutusu

    [SerializeField]
    private TextMeshProUGUI notificationText; // (Opsiyonel) Kutunun içindeki yazı

    [SerializeField]
    private float notificationDuration = 3.0f; // Bildirimin ekranda kalma süresi

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Başlangıçta panelleri kapat
        if (notebookPanel != null)
            notebookPanel.SetActive(false);
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
    }

    private void Update()
    {
        // TAB tuşu ile not defterini aç/kapat
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleNotebook();
        }
    }

    public void ToggleNotebook()
    {
        if (notebookPanel == null)
            return;

        bool isActive = !notebookPanel.activeSelf;
        notebookPanel.SetActive(isActive);

        if (isActive)
        {
            UpdatePasswordList();

            // Defteri açınca bildirimi kapatabiliriz (temizlik olsun)
            if (notificationPanel != null)
                notificationPanel.SetActive(false);
        }
    }

    private void UpdatePasswordList()
    {
        // PasswordManager yoksa hata vermesin
        if (PasswordManager.Instance == null)
            return;

        List<string> passwords = PasswordManager.Instance.GetDiscoveredClues();
        StringBuilder sb = new StringBuilder();

        foreach (string pw in passwords)
        {
            // Şifreleri daha okunaklı yap (RED_>_005 yerine RED > 005)
            sb.AppendLine(pw.Replace("_", " "));
        }

        if (passwordListText != null)
            passwordListText.text = sb.ToString();
    }

    public void ShowPasswordNotification(string passwordID)
    {
        Debug.Log($"Bildirim: {passwordID} not defterine eklendi.");

        // Bildirim Panelini Aç
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(true);

            // Eğer panelde bir yazı alanı varsa, bulunan şifreyi oraya yaz
            if (notificationText != null)
            {
                notificationText.text = $"YENİ İPUCU:\n{passwordID.Replace("_", " ")}";
            }

            // Süre dolunca kapatmak için zamanlayıcıyı sıfırla ve başlat
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
