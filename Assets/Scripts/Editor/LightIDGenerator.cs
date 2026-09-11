#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class LightIDGenerator : EditorWindow
{
    [MenuItem("Senzora/Işık ID Sistemini Kontrol Et")]
    public static void CheckAndFixIDs()
    {
        ControllableLight[] lights = FindObjectsOfType<ControllableLight>(true);
        HashSet<string> usedIDs = new HashSet<string>();
        int newIDs = 0;
        int fixedDuplicates = 0;

        foreach (var light in lights)
        {
            // 1. BOŞ OLANLARI DOLDUR
            if (string.IsNullOrEmpty(light.uniqueID))
            {
                light.uniqueID = System.Guid.NewGuid().ToString();
                newIDs++;
            }
            // 2. ÇAKIŞANLARI (DUPLICATE) BUL VE DÜZELT
            else if (usedIDs.Contains(light.uniqueID))
            {
                light.uniqueID = System.Guid.NewGuid().ToString();
                fixedDuplicates++;
            }

            usedIDs.Add(light.uniqueID);
            EditorUtility.SetDirty(light);
        }

        Debug.Log(
            $"💡 Kontrol Tamamlandı: {newIDs} yeni ID atandı, {fixedDuplicates} çakışan ID düzeltildi. LÜTFEN SAHNEYİ KAYDEDİN!"
        );
    }
}
#endif
