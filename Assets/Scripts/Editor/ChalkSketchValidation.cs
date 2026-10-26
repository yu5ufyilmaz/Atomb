using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class ChalkSketchValidation
{
    [MenuItem("Senzora/Doğrulama/Tebeşir Eskizleri %#j")]
    public static void Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            int checkedItems = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:FormulaItemSO", new[] { "Assets/Scripts/Scriptables/Formulas" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<FormulaItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                Check(data.chalkPrefab != null && data.chalkPrefab.GetComponent("ChalkFormulaSketch") != null, "Missing chalk sketch: " + data.name);
                Check(data.chalkPrefab.GetComponentsInChildren<MeshFilter>(true).Length == 0, "Solid model remains: " + data.name);
                string suffix = data.chalkPrefab.name.Substring(data.chalkPrefab.name.Length - 1);
                var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Formulas/P_Formula" + suffix + ".prefab");
                var oldNodes = original.GetComponent<DraggableFormula>().GetOwnNodes();
                var newNodes = data.chalkPrefab.GetComponent<DraggableFormula>().GetOwnNodes();
                Check(oldNodes.Length == newNodes.Length, "Node count changed: " + data.name);
                for (int i = 0; i < oldNodes.Length; i++)
                {
                    Check(oldNodes[i].connectionType == newNodes[i].connectionType && oldNodes[i].acceptAnyFormula == newNodes[i].acceptAnyFormula,
                        "Node rule changed: " + data.name);
                    Check(oldNodes[i].allowedFormulas.Count == newNodes[i].allowedFormulas.Count, "Allowed formula count changed.");
                    for (int n = 0; n < oldNodes[i].allowedFormulas.Count; n++)
                        Check(oldNodes[i].allowedFormulas[n] == newNodes[i].allowedFormulas[n], "Allowed formula changed.");
                }
                var instance = UnityEngine.Object.Instantiate(data.chalkPrefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                var formula = instance.GetComponent<DraggableFormula>();
                string randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
                formula.Initialize(data);
                Check(randomBefore == JsonUtility.ToJson(UnityEngine.Random.state), "Drawing consumed random state.");
                Check(formula.formulaLabel != null && formula.formulaLabel.text.Contains(data.itemName), "Missing item label.");
                var lines = instance.GetComponentsInChildren<LineRenderer>();
                int strokes = 0;
                foreach (var line in lines)
                {
                    if (line.GetComponent<FormulaNode>() != null) continue;
                    strokes++;
                    Check(!line.useWorldSpace && line.alignment == LineAlignment.TransformZ && line.sharedMaterial != null,
                        "Sketch is not attached to the board plane.");
                    Check(line.shadowCastingMode == ShadowCastingMode.Off, "Chalk casts a 3D shadow.");
                    Check(line.sharedMaterial.GetFloat("_DoubleSidedEnable") > 0.5f, "HDRP hides the reverse side of the chalk stroke.");
                    for (int i = 0; i < line.positionCount; i++) Check(Mathf.Abs(line.GetPosition(i).z) < 0.00001f, "Nonplanar chalk stroke.");
                }
                Check(strokes >= 4, "Chalk drawing is empty.");
                formula.Initialize(data);
                Check(instance.GetComponentsInChildren<LineRenderer>().Length == lines.Length, "Reinitialization duplicated strokes.");
                checkedItems++;
            }
            Check(checkedItems >= 3, "Formula catalog was not checked.");
            Debug.Log($"CHALK PASS: {checkedItems} parts; planar strokes, labels, original node rules, stable random state and reinitialization.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Senzora/Doğrulama/Tahtaya Üç Eskiz Yerleştir %&j")]
    public static void PreviewOnBoard()
    {
        var manager = ChalkboardManager.Instance;
        if (!Application.isPlaying || manager == null || !manager.isMachineActive) throw new InvalidOperationException("Önce Play Mode'da tahtayı açın.");
        for (int i = 0; i < Mathf.Min(3, manager.formulaCatalog.Count); i++)
        {
            Vector3 point = manager.boardTransform.TransformPoint(new Vector3(0.15f + i * 0.6f, 0.15f, 0.02f));
            var method = typeof(ChalkboardManager).GetMethod("CreateFormula", BindingFlags.NonPublic | BindingFlags.Instance);
            var formula = method.Invoke(manager, new object[] { manager.formulaCatalog[i], point, false }) as DraggableFormula;
            Check(formula != null, "Preview spawn failed.");
        }
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
