#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LeesRoom))]
public class LeesRoomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LeesRoom room = (LeesRoom)target;

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("LEES ROOM MANAGER", headerStyle);
        EditorGUILayout.Space(10);

        room.roomName = EditorGUILayout.TextField("Oda İsmi", room.roomName);
        EditorGUILayout.Space(5);

        // Renkli Butonlar
        if (room.isDangerous)
        {
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Kırmızı
            if (GUILayout.Button("DURUM: TEHLİKELİ (Lees Gelebilir)", GUILayout.Height(40))) room.isDangerous = false;
        }
        else
        {
            GUI.backgroundColor = new Color(0.4f, 1f, 0.4f); // Yeşil
            if (GUILayout.Button("DURUM: GÜVENLİ", GUILayout.Height(40))) room.isDangerous = true;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Spawn Noktaları", EditorStyles.boldLabel);

        if (room.isDangerous)
        {
            if (GUILayout.Button("+ Yeni Spawn Noktası Ekle", GUILayout.Height(30)))
            {
                CreateSpawnPoint(room);
            }
            EditorGUILayout.Space(5);
            SerializedProperty spawnPointsProp = serializedObject.FindProperty("spawnPoints");
            EditorGUILayout.PropertyField(spawnPointsProp, true);
        }
        else
        {
            EditorGUILayout.HelpBox("Güvenli odalarda spawn noktasına gerek yok.", MessageType.Info);
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(room);
            serializedObject.ApplyModifiedProperties();
        }
    }

    void CreateSpawnPoint(LeesRoom room)
    {
        GameObject newPoint = new GameObject($"SpawnPoint_{room.spawnPoints.Count + 1}");
        newPoint.transform.SetParent(room.transform);
        newPoint.transform.localPosition = Vector3.zero;
        room.spawnPoints.Add(newPoint.transform);
        Debug.Log($"{room.roomName} odasına nokta eklendi. Şimdi yerini ayarla!");
    }
}
#endif