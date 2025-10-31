using UnityEngine;
using System.Collections.Generic;

public class PasswordManager : MonoBehaviour
{
    public static PasswordManager Instance;
    
    [SerializeField] private List<string> allPossiblePasswords; 
    
    private List<string> requiredPasswords = new List<string>(); 
    
    private List<string> foundPasswords = new List<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
    }

    public void AddFoundPassword(string passwordID)
    {
        if (!foundPasswords.Contains(passwordID))
        {
            foundPasswords.Add(passwordID);
            Debug.Log($"Şifre bulundu: {passwordID}");
        }
    }

    public List<string> GetFoundPasswords()
    {
        return foundPasswords;
    }
}