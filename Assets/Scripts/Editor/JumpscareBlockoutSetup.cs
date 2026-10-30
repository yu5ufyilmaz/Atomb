using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class JumpscareBlockoutSetup
{
    public const string ScenePath = "Assets/Scenes/scn_JumpscareBlockout.unity";
    public const string DefaultStart = "Blockout Start / Default";
    public const string HideStart = "Blockout Start / Hiding Spot";
    const string SourcePath = "Assets/Scenes/scn_LaboratorySynthesis.unity";
    const string PlayerName = "Senekas_Player";
    const string Workshop = "Assets/LeesResistanceWorkshop/";
    const float WallHeight = 3.5f;

    [MenuItem("Senzora/Jumpscare Blockout/Sahneyi Aç")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
        EditorSceneManager.OpenScene(ScenePath);
        JumpscareBlockoutWindow.Open();
    }

    // Creates ONLY the blockout scene. The source scene is changed in memory and never saved.
    // Delete the scene to rebuild it; running this again preserves blockout edits.
    static void Build()
    {
        var source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var player = source.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerName);
            if (player == null) throw new InvalidOperationException(PlayerName + " bulunamadı: " + SourcePath);
            var copies = RebindOutsideObjects(player);
            SceneManager.MoveGameObjectToScene(player, scene);
            var door = copies.Values.FirstOrDefault(copy => copy.GetComponentInChildren<InteractableDoor>(true) != null);
            var hide = copies.Values.FirstOrDefault(copy => copy.GetComponentInChildren<InteractableHidingSpot>(true) != null);
            BuildEnvironment(player.transform, door, hide);
            AddStart(DefaultStart, player.transform.position, player.transform.rotation);
            if (hide != null)
                AddStart(HideStart, hide.transform.Find("Pos_Enter").position, hide.transform.Find("Pos_Exit").rotation);
            // Only presentation is previewed here; gameplay scripts would look for managers this scene lacks.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script != null && !IsEngineScript(script)) script.enabled = false;
            // Closing first drops the Canvas bindings instead of saving cross-scene references.
            EditorSceneManager.CloseScene(source, true);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException(ScenePath + " kaydedilemedi.");
            Debug.Log("JUMPSCARE BLOCKOUT CREATED: " + ScenePath);
        }
        catch
        {
            EditorSceneManager.OpenScene(SourcePath);
            throw;
        }
    }

    // Director bindings are per scene while clips stay in the shared .playable assets, so only
    // bindings outside the player need replacing. Canvas targets stay unbound on purpose.
    static Dictionary<GameObject, GameObject> RebindOutsideObjects(GameObject player)
    {
        var copies = new Dictionary<GameObject, GameObject>();
        foreach (var director in player.GetComponentsInChildren<PlayableDirector>(true))
        {
            if (!(director.playableAsset is TimelineAsset timeline)) continue;
            foreach (var track in timeline.GetOutputTracks())
            {
                var bound = director.GetGenericBinding(track);
                var target = bound is Component component ? component.gameObject : bound as GameObject;
                if (target == null || target.transform.IsChildOf(player.transform) || target.GetComponentInParent<Canvas>(true) != null) continue;
                if (!copies.TryGetValue(target, out var copy)) copies[target] = copy = Copy(target);
                director.SetGenericBinding(track, bound is GameObject ? copy : (Object)copy.GetComponent(bound.GetType()));
            }
        }
        return copies;
    }

    static GameObject Copy(GameObject original)
    {
        var copy = Object.Instantiate(original);
        copy.name = original.name;
        copy.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
        copy.transform.localScale = original.transform.lossyScale;
        return copy;
    }

    static bool IsEngineScript(MonoBehaviour script)
    {
        string space = script.GetType().Namespace ?? "";
        return space.StartsWith("Cinemachine") || space.StartsWith("UnityEngine");
    }

    static void BuildEnvironment(Transform player, GameObject door, GameObject hide)
    {
        var parent = new GameObject("Blockout Environment").transform;
        var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(Workshop + "Floor.mat");
        var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(Workshop + "Concrete.mat");
        var area = new Bounds(player.position, Vector3.zero);
        if (door != null) area.Encapsulate(door.transform.position);
        if (hide != null) area.Encapsulate(hide.transform.position);
        float floor = door != null ? WorldBounds(door).min.y : player.position.y;
        Cube(parent, "Floor", new Vector3(area.center.x, floor - .12f, area.center.z), new Vector3(area.size.x + 24, .24f, area.size.z + 24), floorMaterial);
        if (door != null) BuildDoorWall(parent, WorldBounds(door), floor, wallMaterial);
        PointLight(parent, "Player Light", player.position + Vector3.up * 2.6f);
        if (door != null) PointLight(parent, "Door Light", door.transform.position + Vector3.up * 2f);
        if (hide != null) PointLight(parent, "Hiding Spot Light", hide.transform.position + Vector3.up * 2.6f);
        var volume = new GameObject("Blockout Exposure").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Workshop + "WorkshopExposure.asset");
    }

    static void BuildDoorWall(Transform parent, Bounds door, float floor, Material material)
    {
        bool alongX = door.size.x >= door.size.z;
        var axis = alongX ? Vector3.right : Vector3.forward;
        float half = Mathf.Max(door.size.x, door.size.z) * .5f;
        var scale = alongX ? new Vector3(6, WallHeight, .2f) : new Vector3(.2f, WallHeight, 6);
        for (int side = -1; side <= 1; side += 2)
        {
            var center = door.center + axis * side * (half + 3);
            center.y = floor + WallHeight * .5f;
            Cube(parent, "Door Wall", center, scale, material);
        }
        float header = floor + WallHeight - door.max.y;
        if (header <= 0) return;
        var top = door.center; top.y = door.max.y + header * .5f;
        Cube(parent, "Door Header", top, alongX ? new Vector3(half * 2, header, .2f) : new Vector3(.2f, header, half * 2), material);
    }

    static Bounds WorldBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void AddStart(string name, Vector3 position, Quaternion rotation) =>
        new GameObject(name).transform.SetPositionAndRotation(position, rotation);

    static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void PointLight(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name, typeof(Light), typeof(HDAdditionalLightData)); go.transform.SetParent(parent, false); go.transform.position = position;
        var light = go.GetComponent<Light>(); light.type = LightType.Point; light.color = new Color(.85f, .88f, .9f);
        light.lightUnit = LightUnit.Lumen; light.intensity = 900; light.range = 10; light.shadows = LightShadows.Soft;
    }

    [MenuItem("Senzora/Jumpscare Blockout/Sahneyi Doğrula")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) { Debug.LogError("Önce " + ScenePath + " sahnesini aç."); return; }
        var directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // The notebook pages in the player's hands stay: they are world-space and cannot cover the view.
        int canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(canvas => canvas.renderMode != RenderMode.WorldSpace);
        bool valid = directors.Length == 5 && canvases == 0;
        foreach (var director in directors)
        {
            var tracks = ((TimelineAsset)director.playableAsset).GetOutputTracks().Where(track => !(track is GroupTrack)).ToList();
            var unbound = tracks.Where(track => director.GetGenericBinding(track) == null).Select(track => track.name).ToList();
            Debug.Log($"{director.name}: {tracks.Count - unbound.Count}/{tracks.Count} bağlı; boş: {string.Join(", ", unbound)}");
        }
        if (valid) Debug.Log("JUMPSCARE BLOCKOUT VALID: 5 timeline, ekran Canvas'ı yok.");
        else Debug.LogError($"JUMPSCARE BLOCKOUT INVALID: {directors.Length} timeline, {canvases} ekran Canvas'ı.");
    }
}
