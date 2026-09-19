using UnityEngine;

// Her bir tutorial/log sayfası için ayrı oluşturacağın dosya
[CreateAssetMenu(fileName = "NewTutorial", menuName = "Senzora/Notebook/Tutorial Data")]
public class TutorialDataSO : ScriptableObject
{
    public string title;

    [TextArea(5, 10)]
    public string content;
}
