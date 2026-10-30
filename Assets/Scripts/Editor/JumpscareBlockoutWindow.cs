using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class JumpscareBlockoutWindow : EditorWindow
{
    [MenuItem("Senzora/Jumpscare Blockout/Timeline Seçici")]
    public static void Open() => GetWindow<JumpscareBlockoutWindow>("Jumpscare Blockout");

    void OnGUI()
    {
        if (SceneManager.GetActiveScene().path != JumpscareBlockoutSetup.ScenePath)
        {
            EditorGUILayout.HelpBox("Bu pencere blockout sahnesinde çalışır.", MessageType.Info);
            if (GUILayout.Button("Sahneyi Aç")) JumpscareBlockoutSetup.Open();
            return;
        }
        foreach (var director in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(item => item.name))
            if (GUILayout.Button(director.name)) Select(director);
    }

    static void Select(PlayableDirector director)
    {
        // The hiding-spot timeline never moves the player; the game seats them in the cabinet first.
        var start = GameObject.Find(director.name.Contains("Hide") ? JumpscareBlockoutSetup.HideStart : JumpscareBlockoutSetup.DefaultStart);
        var player = director.transform.parent;
        if (start != null)
        {
            Undo.RecordObject(player, "Jumpscare Başlangıcı");
            player.SetPositionAndRotation(start.transform.position, start.transform.rotation);
        }
        Selection.activeGameObject = director.gameObject;
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
    }
}
