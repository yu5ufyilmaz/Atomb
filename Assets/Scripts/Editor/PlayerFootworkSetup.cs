using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerFootworkSetup
{
    public const string ControllerPath = "Assets/Animations/Player/LaboratoryFootwork.controller";
    private const string OriginalController = "Assets/StarterAssets/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";

    [MenuItem("Senzora/Player Footwork/Apply to Laboratory Copy")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LaboratorySynthesisSetup.ScenePath)
            throw new InvalidOperationException("Open the laboratory migration copy in Edit mode first.");
        var player = LaboratorySynthesisSetup.Find<StarterAssets.CharacterController>(scene);
        var save = LaboratorySynthesisSetup.Find<SaveManager>(scene);
        if (player == null || save == null || !save.sessionOnly)
            throw new InvalidOperationException("The copied player and session-only save must be configured first.");
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("PlayerFootwork") ?? a.GetType("StarterAssets.PlayerFootwork"))
            .FirstOrDefault(t => t != null && typeof(MonoBehaviour).IsAssignableFrom(t));
        if (type == null) throw new InvalidOperationException("Compile the PlayerFootwork component before applying it.");
        var animator = player.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            throw new InvalidOperationException("The real laboratory player must have its Humanoid Animator.");
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null
            && !AssetDatabase.CopyAsset(OriginalController, ControllerPath))
            throw new InvalidOperationException("Could not create the laboratory-only Animator controller copy.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var layers = controller.layers;
        if (layers.Length == 0) throw new InvalidOperationException("The copied controller has no base layer.");
        Undo.RecordObject(controller, "Enable laboratory foot IK pass");
        layers[0].iKPass = true;
        controller.layers = layers;
        Undo.RecordObject(animator, "Assign laboratory footwork controller");
        animator.runtimeAnimatorController = controller;
        var component = player.GetComponent(type) ?? Undo.AddComponent(player.gameObject, type);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the laboratory copy.");
        Debug.Log("PLAYER FOOTWORK SETUP PASS: migration player uses its own controller with base IK pass; original controller retained.");
    }
}
