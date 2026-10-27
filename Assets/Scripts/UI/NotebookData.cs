using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NotebookDatabase", menuName = "Senzora/Notebook/Database")]
public class NotebookData : ScriptableObject
{
    [Header("Çalışma Alanı Görünümü")]
    [Tooltip("Boş bırakılırsa kağıt blokları çalışma anında oluşturulur.")]
    public NotebookDraggableBlock workspaceBlockPrefab;

    [Tooltip("Kitaplarda ve kayıt geri yüklemesinde kullanılan kurgusal sentez modülleri.")]
    public List<FormulaItemSO> synthesisCatalog = new List<FormulaItemSO>();

    [Tooltip("Eskizlerin kağıda bırakıldığındaki en fazla açı sapması.")]
    [Range(0f, 15f)] public float sketchRotation = 5f;

    [Header("  Tutorials (Bağımsız SO'lar)")]
    public List<TutorialDataSO> tutorialPages = new List<TutorialDataSO>();

}
