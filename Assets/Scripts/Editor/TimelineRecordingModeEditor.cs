using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TimelineRecordingMode))]
public class TimelineRecordingModeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        TimelineRecordingMode recordingMode = (TimelineRecordingMode)target;
        if (GUILayout.Button("Listeleri ve Sahne Pozunu Otomatik Doldur"))
        {
            Undo.RecordObject(recordingMode, "Auto-Fill Recording Lists");
            recordingMode.AutoFillRecordingLists();
            EditorUtility.SetDirty(recordingMode);

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(recordingMode.gameObject.scene);
        }
    }
}
