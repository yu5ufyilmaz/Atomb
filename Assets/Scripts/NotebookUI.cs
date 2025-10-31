using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class NotebookUI : MonoBehaviour
{
    public static NotebookUI Instance;

    [SerializeField] private GameObject notebookPanel; 
    [SerializeField] private TextMeshProUGUI passwordListText; 
    [SerializeField] private GameObject notificationPanel; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        notebookPanel.SetActive(false);
        notificationPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleNotebook();
        }
    }

    public void ToggleNotebook()
    {
        bool isActive = !notebookPanel.activeSelf;
        notebookPanel.SetActive(isActive);

        if (isActive)
        {
            UpdatePasswordList();
        }
    }
    
    private void UpdatePasswordList()
    {
        List<string> passwords = PasswordManager.Instance.GetFoundPasswords();
        StringBuilder sb = new StringBuilder();

        foreach (string pw in passwords)
        {
            sb.AppendLine(pw.Replace("_", " "));
        }

        passwordListText.text = sb.ToString();
    }
    
    public void ShowPasswordNotification(string passwordID)
    {
        Debug.Log($"Bildirim: {passwordID} not defterine eklendi.");
    }
}