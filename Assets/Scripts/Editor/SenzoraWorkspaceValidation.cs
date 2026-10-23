using UnityEditor;
using UnityEngine;

public static class SenzoraWorkspaceValidation
{
    [MenuItem("Senzora/Doğrulama/Tüm Sentez ve Çalışma Alanı")]
    public static void RunAll()
    {
        SynthesisPuzzleValidation.RunAll();
        SynthesisBoardValidation.RunAll();
        NotebookWorkspaceValidation.RunAll();
        Debug.Log("Senzora sentez ve çalışma alanı doğrulamalarının tamamı geçti.");
    }
}
