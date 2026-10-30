using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class LeesResistanceGameplaySetup
{
    public static readonly string[] GameplayScenes = { "Assets/Scenes/scn_LaboratorySynthesis.unity", "Assets/Scenes/scn_labratory.unity" };
    public const string Folder = LeesResistanceWorkshopSetup.Folder;
    public const string LeftMeshPath = Folder + "/LeesLeftGrip.asset";
    public const string RightMeshPath = Folder + "/LeesRightGrip.asset";
    const int HandLayer = 30;
    public const uint HandRenderingLayer = 1u << 7;

    [MenuItem("Senzora/LEES Mücadelesi/Elleri ve Ana Oyun Bağlantısını Kur")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Önce Play modundan çık.");
        EnsureMeshes();
        var prefab = PrefabUtility.LoadPrefabContents(LeesResistanceWorkshopSetup.RigPath);
        try
        {
            var control = prefab.GetComponent<LeesResistanceController>();
            RemoveOldAudio(control); ConfigureHands(control);
            PrefabUtility.SaveAsPrefabAsset(prefab, LeesResistanceWorkshopSetup.RigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        EditScene(LeesResistanceWorkshopSetup.ScenePath, scene =>
        {
            var control = Find<LeesResistanceController>(scene); RemoveOldAudio(control); ConfigureHands(control);
        });
        foreach (string path in GameplayScenes) EditScene(path, InstallInScene);
        Debug.Log("LEES GAMEPLAY INSTALLED: quiet heartbeat + camera hands; LaboratorySynthesis and labratory connected.");
    }
    static void EditScene(string path, Action<Scene> action)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try { action(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
    static void InstallInScene(Scene scene)
    {
        var enemy = Find<LeesEnemyAI>(scene);
        if (enemy == null || enemy.playerCamera == null || enemy.playerTransform == null)
            throw new InvalidOperationException("LEES/player/camera reference missing: " + scene.path);
        var control = enemy.resistance;
        if (control == null)
        {
            var root = new GameObject("LEES / Head Resistance");
            control = root.AddComponent<LeesResistanceController>();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(LeesResistanceWorkshopSetup.RigPath).GetComponent<LeesResistanceController>();
            control.tuning = JsonUtility.FromJson<LeesResistanceModel.Tuning>(JsonUtility.ToJson(template.tuning));
            control.motionIntensity = template.motionIntensity;
            control.hud = UnityEngine.Object.Instantiate(template.hud.gameObject, root.transform, false).GetComponent<LeesResistanceHud>();
            control.hud.name = "LEES Resistance HUD";
            var audio = new GameObject("Heartbeat").AddComponent<AudioSource>(); audio.transform.SetParent(root.transform, false);
            audio.playOnAwake = false; audio.spatialBlend = 0; audio.clip = template.heartbeatAudio.clip;
            control.heartbeatAudio = audio;
            enemy.resistance = control;
        }
        control.viewCamera = enemy.playerCamera; control.headPivot = enemy.playerCamera.transform;
        control.playerMovement = enemy.playerTransform.GetComponent<StarterAssets.CharacterController>();
        control.keepResultVisible = false; control.useGamePause = true;
        if (enemy.audioSource != null) control.heartbeatAudio.outputAudioMixerGroup = enemy.audioSource.outputAudioMixerGroup;
        var canvas = control.hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = enemy.playerCamera; control.hud.gameObject.SetActive(false);
        var workshopLabel = control.hud.transform.Find("Workshop Label"); if (workshopLabel != null) workshopLabel.gameObject.SetActive(false);
        var suspended = new List<Behaviour>();
        void Suspend(Behaviour component) { if (component != null && !suspended.Contains(component)) suspended.Add(component); }
        if (enemy.playerMovementScript != control.playerMovement) Suspend(enemy.playerMovementScript);
        if (enemy.playerLookScript != control.playerMovement) Suspend(enemy.playerLookScript);
        Suspend(enemy.playerCamera.GetComponent<CinemachineBrain>());
        Suspend(enemy.playerTransform.GetComponentInChildren<PlayerInteraction>(true));
        Suspend(Find<NotebookUI>(scene));
        control.suspendDuringResistance = suspended.ToArray();
        bool escapedBound = false, caughtBound = false;
        for (int i = 0; i < control.onEscaped.GetPersistentEventCount(); i++)
            escapedBound |= control.onEscaped.GetPersistentTarget(i) == enemy && control.onEscaped.GetPersistentMethodName(i) == "ResistanceEscaped";
        for (int i = 0; i < control.onCaught.GetPersistentEventCount(); i++)
            caughtBound |= control.onCaught.GetPersistentTarget(i) == enemy && control.onCaught.GetPersistentMethodName(i) == "ResistanceCaught";
        if (!escapedBound) UnityEventTools.AddPersistentListener(control.onEscaped, enemy.ResistanceEscaped);
        if (!caughtBound) UnityEventTools.AddPersistentListener(control.onCaught, enemy.ResistanceCaught);
        ConfigureHands(control);
        EditorUtility.SetDirty(control); EditorUtility.SetDirty(enemy);
    }
    public static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        { var found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
        return null;
    }
    static void RemoveOldAudio(LeesResistanceController control)
    {
        foreach (string name in new[] { "LEES Tinnitus", "Grip Change" })
        {
            var child = control.transform.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
    static void ConfigureHands(LeesResistanceController control)
    {
        if (control.hands == null)
        {
            var root = new GameObject("LEES / Temple Grip"); root.transform.SetParent(control.viewCamera.transform, false);
            control.hands = root.AddComponent<LeesResistanceHands>();
        }
        var hands = control.hands; hands.viewCamera = control.viewCamera;
        hands.depth = .38f; hands.size = .9f;
        hands.leftAnchor = new Vector2(-.025f, .57f); hands.rightAnchor = new Vector2(1.025f, .57f);
        Transform Make(string name, string path)
        {
            var child = hands.transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(hands.transform, false); child = go.transform;
            }
            child.gameObject.layer = HandLayer;
            child.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = HandMaterial();
            renderer.renderingLayerMask = 1u | HandRenderingLayer;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            EditorUtility.SetDirty(renderer);
            return child;
        }
        hands.leftHand = Make("Left Hand", LeftMeshPath); hands.rightHand = Make("Right Hand", RightMeshPath);
        control.viewCamera.cullingMask |= 1 << HandLayer;
        var fill = hands.transform.Find("Hand Fill");
        if (fill == null)
        {
            var go = new GameObject("Hand Fill", typeof(Light), typeof(HDAdditionalLightData)); go.transform.SetParent(hands.transform, false); fill = go.transform;
        }
        fill.localPosition = new Vector3(0, .1f, .04f);
        var light = fill.GetComponent<Light>(); light.type = LightType.Point; light.color = new Color(.7f, .8f, .77f);
        light.lightUnit = LightUnit.Lumen; light.intensity = 20; light.range = 1; light.cullingMask = 1 << HandLayer; light.shadows = LightShadows.None;
        var hdLight = fill.GetComponent<HDAdditionalLightData>();
        var lightLayer = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)HandRenderingLayer;
        hdLight.SetLightLayer(lightLayer, lightLayer);
        hands.fillLight = light;
        hands.Hide(); EditorUtility.SetDirty(hands); EditorUtility.SetDirty(light); EditorUtility.SetDirty(hdLight);
    }
    static Material HandMaterial()
    {
        string path = Folder + "/LeesGripSkin.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Characters/Lees/Material/Lees.mat");
            material = new Material(original); material.name = "LEES Grip Skin";
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", new Color(.24f, .29f, .25f, 1));
        material.SetFloat("_Smoothness", .22f);
        material.SetFloat("_SmoothnessRemapMax", .28f);
        material.SetFloat("_TransmissionEnable", 0);
        material.SetFloat("_SubsurfaceMask", 0);
        material.SetFloat("_DisplacementMode", 0);
        material.SetFloat("_DoubleSidedEnable", 1); material.EnableKeyword("_DOUBLESIDED_ON");
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
    }
    static void EnsureMeshes()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyDummies/Lees_Dummy.prefab"), scene);
            source.transform.position = Vector3.zero; source.transform.rotation = Quaternion.identity;
            var animator = source.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
            SkinnedMeshRenderer skin = null;
            foreach (var candidate in source.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (skin == null || candidate.sharedMesh.vertexCount > skin.sharedMesh.vertexCount) skin = candidate;
            if (skin == null) throw new InvalidOperationException("LEES skin mesh missing.");
            BakeHand(skin, "Left", LeftMeshPath); BakeHand(skin, "Right", RightMeshPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void BakeHand(SkinnedMeshRenderer skin, string side, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        Transform Bone(string suffix)
        {
            foreach (var bone in skin.bones) if (bone.name.EndsWith(":" + side + suffix, StringComparison.Ordinal)) return bone;
            throw new InvalidOperationException("Missing LEES bone: " + side + suffix);
        }
        var wrist = Bone("Hand");
        Vector3 up = (Bone("HandMiddle1").position - wrist.position).normalized;
        Vector3 across = (Bone("HandIndex1").position - Bone("HandPinky1").position).normalized;
        Quaternion basis = Quaternion.LookRotation(Vector3.Cross(across, up).normalized, up);
        Quaternion inverse = Quaternion.Inverse(basis); Vector3 origin = wrist.position;
        // Curl the existing finger chains before baking; leave the source prefab untouched.
        foreach (string finger in new[] { "Index", "Middle", "Ring", "Pinky" })
            for (int joint = 1; joint <= 3; joint++)
            {
                var bone = Bone("Hand" + finger + joint);
                float curl = joint == 1 ? 28 : joint == 2 ? 44 : 26;
                if (side == "Right") curl = -curl;
                bone.rotation = Quaternion.AngleAxis(curl, across) * bone.rotation;
            }
        // This .01-scale rig needs useScale=true before converting renderer-local vertices to world space.
        var baked = new Mesh(); skin.BakeMesh(baked, true);
        try
        {
            var weights = skin.sharedMesh.boneWeights; var vertices = baked.vertices; var normals = baked.normals; var uv = baked.uv;
            var keep = new bool[vertices.Length];
            bool Owns(int index) { return skin.bones[index] == wrist || skin.bones[index].IsChildOf(wrist); }
            for (int i = 0; i < keep.Length; i++)
            {
                var w = weights[i]; float influence = (Owns(w.boneIndex0) ? w.weight0 : 0) + (Owns(w.boneIndex1) ? w.weight1 : 0)
                    + (Owns(w.boneIndex2) ? w.weight2 : 0) + (Owns(w.boneIndex3) ? w.weight3 : 0);
                Vector3 wristLocal = inverse * (skin.transform.TransformPoint(vertices[i]) - origin);
                keep[i] = influence > .5f && wristLocal.y > -.012f;
            }
            var points = new List<Vector3>(); var directions = new List<Vector3>(); var texcoords = new List<Vector2>();
            var indices = new List<int>(); var map = new Dictionary<int, int>();
            int Add(int index)
            {
                if (map.TryGetValue(index, out int existing)) return existing;
                int next = points.Count; map.Add(index, next);
                points.Add(inverse * (skin.transform.TransformPoint(vertices[index]) - origin));
                directions.Add(inverse * skin.transform.TransformDirection(normals[index]));
                texcoords.Add(uv[index]); return next;
            }
            var triangles = baked.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                if (keep[triangles[i]] && keep[triangles[i + 1]] && keep[triangles[i + 2]])
                { indices.Add(Add(triangles[i])); indices.Add(Add(triangles[i + 1])); indices.Add(Add(triangles[i + 2])); }
            if (points.Count < 100) throw new InvalidOperationException("LEES hand extraction was empty: " + side);
            var mesh = new Mesh { name = "LEES " + side + " Grip" };
            mesh.SetVertices(points); mesh.SetNormals(directions); mesh.SetUVs(0, texcoords); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); AssetDatabase.SaveAssetIfDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); }
            Debug.Log("LEES HAND BAKED: " + side + "; vertices=" + points.Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); }
    }

}
