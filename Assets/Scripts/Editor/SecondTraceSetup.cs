using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SecondTraceSetup
{
    private const string ScenePath = "Assets/Scenes/scn_SynthesisBlockout.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/İkinci İz/Test Sahnesine Kur")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        ResearchPuzzleTrial trial = null;
        foreach (var root in scene.GetRootGameObjects())
        { trial = root.GetComponentInChildren<ResearchPuzzleTrial>(true); if (trial != null) break; }
        if (trial == null) throw new InvalidOperationException("Önce araştırma kitapları test sahnesine kurulmalı.");
        var experiment = trial.GetComponent<SecondTraceExperiment>();
        if (experiment == null) experiment = Undo.AddComponent<SecondTraceExperiment>(trial.gameObject);
        Undo.RecordObject(experiment, "İkinci İz'i etkinleştir");
        experiment.enabled = true;
        experiment.newTest = false;
        if (experiment.terminal == null)
        {
            var sourceScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scn_labratory.unity");
            try
            {
                InteractableTuringMachine source = null;
                foreach (var root in sourceScene.GetRootGameObjects())
                { source = root.GetComponentInChildren<InteractableTuringMachine>(true); if (source != null) break; }
                if (source == null) throw new InvalidOperationException("Laboratuvarda Turing makinesi bulunamadı.");
                var copy = Object.Instantiate(source.gameObject);
                copy.name = "Turing / İkinci İz Deneme Kopyası";
                SceneManager.MoveGameObjectToScene(copy, scene);
                Undo.RegisterCreatedObjectUndo(copy, "Turing deneme kopyası");
                copy.transform.SetParent(trial.transform, true);
                copy.transform.localScale = source.transform.lossyScale;
                copy.transform.rotation = Quaternion.identity;
                var machine = copy.GetComponent<InteractableTuringMachine>();
                var serialized = new SerializedObject(machine);
                foreach (string field in new[] { "playerController", "playerLookScript", "playerAnimator", "playerMovementScript", "playerInteractionScript", "_playerFollowCamera", "interactionStandPoint" })
                    serialized.FindProperty(field).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                experiment.terminal = machine;
                var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Turing Deneme Masası";
                SceneManager.MoveGameObjectToScene(table, scene);
                table.transform.SetParent(trial.transform, true);
                table.transform.position = new Vector3(1.35f, .80f, -.65f);
                table.transform.localScale = new Vector3(2, .15f, 1.4f);
                table.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SynthesisBlockout/Blockout_Masa.mat");
                Undo.RegisterCreatedObjectUndo(table, "Turing deneme masası");
            }
            finally { EditorSceneManager.ClosePreviewScene(sourceScene); }
        }
        experiment.terminal.gameObject.SetActive(true);
        PositionTerminal(experiment.terminal);
        var player = Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
        Undo.RecordObject(player, "Deneme terminalini bağla");
        player.trialTerminal = experiment.terminal;
        player.terminalView = experiment.terminal.fixedCameraTransform;
        EditorUtility.SetDirty(player); EditorUtility.SetDirty(experiment);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("SECOND TRACE SETUP: only the test scene was saved; A/B content preserved, one experiment active, original Turing copied without applying prefab changes.");
    }

    private static Bounds ModelBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        Bounds bounds = default; bool first = true;
        foreach (var renderer in renderers)
        {
            if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static void PositionTerminal(InteractableTuringMachine machine)
    {
        var root = machine.gameObject;
        var view = machine.fixedCameraTransform;
        var bounds = ModelBounds(root);
        Vector3 direction = view.position - bounds.center;
        direction.y = 0;
        if (direction.sqrMagnitude > .001f)
            root.transform.rotation = Quaternion.FromToRotation(direction.normalized, Vector3.forward) * root.transform.rotation;
        bounds = ModelBounds(root);
        root.transform.localScale *= Mathf.Min(1.6f / Mathf.Max(bounds.size.x, bounds.size.z), 1.6f / bounds.size.y);
        bounds = ModelBounds(root);
        root.transform.position += new Vector3(1.35f, .9f, -.65f) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = ModelBounds(root);
        float distance = Mathf.Max(bounds.extents.y / Mathf.Tan(30 * Mathf.Deg2Rad),
            bounds.extents.x / (1.5f * Mathf.Tan(30 * Mathf.Deg2Rad))) * 1.15f + bounds.extents.z;
        view.position = bounds.center + Vector3.forward * distance + Vector3.up * .12f;
        view.LookAt(bounds.center);
        var table = root.transform.parent.Find("Turing Deneme Masası");
        if (table != null) table.position = new Vector3(1.35f, .80f, -.65f);
        EditorUtility.SetDirty(root.transform);
        EditorUtility.SetDirty(view);
    }

    [MenuItem("Senzora/İkinci İz/Önceki A-B Denemelerine Dön")]
    public static void RestorePrevious()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var experiment = Object.FindFirstObjectByType<SecondTraceExperiment>();
        if (experiment == null || experiment.gameObject.scene.path != ScenePath) return;
        Undo.RecordObject(experiment, "Önceki araştırmalara dön");
        experiment.enabled = false;
        if (experiment.terminal != null) experiment.terminal.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(experiment.gameObject.scene);
        EditorSceneManager.SaveScene(experiment.gameObject.scene);
    }

    [MenuItem("Senzora/İkinci İz/Doğrula")]
    public static void Validate()
    {
        var baseline = Graph(0);
        var first = SecondTraceExperiment.Simulate(baseline);
        var main = SecondTraceExperiment.Simulate(Graph(1));
        var returned = SecondTraceExperiment.Simulate(Graph(2));
        var alternative = Graph(2);
        alternative.bonds.RemoveAt(alternative.bonds.Count - 1);
        alternative.bonds.RemoveAt(alternative.bonds.Count - 1);
        alternative.bonds.RemoveAt(alternative.bonds.Count - 1);
        alternative.bonds.Add(Bond(1, 3)); alternative.bonds.Add(Bond(3, 4)); alternative.bonds.Add(Bond(4, 2));
        Check(first.Measurable && first.main == 1 && first.echo == .5f && !first.MeetsTarget, "Başlangıç iki iz üretmeli.");
        Check(main.Measurable && main.main < .9f && !main.MeetsTarget, "Ana hat kaybı başarı sayılmamalı.");
        Check(returned.MeetsTarget && SecondTraceExperiment.Simulate(alternative).MeetsTarget, "Dönüş yolundaki iki geçerli yerleşim kabul edilmeli.");
        baseline.bonds.RemoveAt(3);
        Check(!SecondTraceExperiment.Simulate(baseline).Measurable, "Referans kolunu koparmak çözüm sayılmamalı.");
        var content = SecondTraceExperiment.CreateContent(AssetDatabase.LoadAssetAtPath<FormulaItemSO>("Assets/Scripts/Scriptables/Formulas/SynthesisModuleA.asset"));
        try
        {
            Check(content.Quests.Count == 1 && content.Quests[0].questID == SecondTraceExperiment.QuestID && content.Parts.Count == 5, "Yalnız yeni görev etkin olmalı.");
            foreach (var pages in content.Books) Check(pages != null && pages.Count == 2, "Dört kitapta bağımsız bilgi olmalı.");
        }
        finally { content.Dispose(); }
        Debug.Log("SECOND TRACE MODEL PASS: two arrivals, main loss, two valid return placements, disconnected reference rejected, one quest and four independent books.");
    }

    // Editor-only playtest fixtures; these do not collect notes or draw the player's solution automatically.
    public static SynthesisGraphData Graph(int mode)
    {
        var graph = new SynthesisGraphData();
        foreach (string id in new[] { SecondTraceExperiment.Source, SecondTraceExperiment.Sample, SecondTraceExperiment.Detector, SecondTraceExperiment.Reference })
            graph.parts.Add(new SynthesisPartData { itemID = id, capacity = 4 });
        if (mode != 0) graph.parts.Add(new SynthesisPartData { itemID = SecondTraceExperiment.Absorber, capacity = 4 });
        if (mode == 1) { graph.bonds.Add(Bond(0, 4)); graph.bonds.Add(Bond(4, 1)); }
        else graph.bonds.Add(Bond(0, 1));
        graph.bonds.Add(Bond(1, 2));
        if (mode == 2) { graph.bonds.Add(Bond(1, 4)); graph.bonds.Add(Bond(4, 3)); }
        else graph.bonds.Add(Bond(1, 3));
        graph.bonds.Add(Bond(3, 2));
        return graph;
    }

    private static SynthesisBondData Bond(int a, int b) => new SynthesisBondData { fromIndex = a, toIndex = b };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
