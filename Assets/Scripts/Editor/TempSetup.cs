using UnityEngine;
using UnityEditor;

public class TempSetup {
    [MenuItem("Tools/Setup Weapon")]
    public static void Setup() {
        GameObject character = GameObject.Find("adam");
        GameObject weapon = GameObject.Find("911pistol");
        if (character == null) { Debug.LogError("Character 'adam' not found"); return; }
        if (weapon == null) { Debug.LogError("Weapon '911pistol' not found"); return; }

        // Try to find the right hand with common names
        Transform rightHand = FindDeepChild(character.transform, "RightHand");
        if (rightHand == null) rightHand = FindDeepChild(character.transform, "mixamorig:RightHand");
        if (rightHand == null) rightHand = FindDeepChild(character.transform, "Hand.R");
        if (rightHand == null) rightHand = FindDeepChild(character.transform, "Hand_R");

        if (rightHand != null) {
            weapon.transform.SetParent(rightHand);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            // Adjust scale if needed, or assume 1,1,1
            Debug.Log($"Weapon parented to {rightHand.name}");
        } else {
            Debug.LogError("Right Hand bone not found in character hierarchy");
        }
    }

    private static Transform FindDeepChild(Transform aParent, string aName) {
        if (aParent.name.IndexOf(aName, System.StringComparison.OrdinalIgnoreCase) >= 0) return aParent;
        foreach(Transform child in aParent) {
            var result = FindDeepChild(child, aName);
            if (result != null) return result;
        }
        return null;
    }
}
