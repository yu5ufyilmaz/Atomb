using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Senzora/Localization/Language", fileName = "Language")]
public sealed class LocalizationLanguage : ScriptableObject
{
    public string code = "en";
    public string nativeName = "English";
    public int sortOrder;
    public string fallbackCode = "en";
    public TMP_FontAsset defaultFont;
    public bool rightToLeft;
    public TextAsset[] tables;
}
