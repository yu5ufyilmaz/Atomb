using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWordPool", menuName = "Atomb/Word Pool")]
public class WordPool : ScriptableObject
{
    [Header("Kelime Havuzu")]
    [Tooltip(
        "Turing makinesindeki harf çarklarına sığacak kelimeler ekleyin (Örn: RED, KEY, SYSTEM)"
    )]
    public List<string> words = new List<string>();

    public string GetRandomWord()
    {
        if (words.Count == 0)
            return "NULL";
        return words[Random.Range(0, words.Count)];
    }
}
