using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Processors;

// Contextual gameplay input stays active when an interaction disables StarterAssetsInputs.
// Both this reader and PlayerInput use the same imported asset/defaults and saved overrides.
public static class GameInput
{
    [Serializable] public sealed class BindingPolicy
    {
        public string id, action, textTR, textEN, context, part;
        public bool rebindable = true;
    }
    [Serializable] public sealed class BindingSettings { public BindingPolicy[] bindings = Array.Empty<BindingPolicy>(); }
    const string SaveKey = "Senzora.InputOverrides.v1";
    static InputActionAsset actions;
    static BindingSettings policy;
    static float gamepadDeadzone = .125f;
    public static bool GameplayBlocked { get; set; }
    // GameplayBlocked statiktir, sahne yüklense de kalır; oyun sonu bu yüzden sahneyle ölen GameManager'da tutulur.
    public static bool Blocked => GameplayBlocked || (GameManager.Instance != null && GameManager.Instance.IsGameplayOver);
    public static string ContextOf(BindingPolicy binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.context) && binding.context != "Player") return binding.context;
        switch (binding.action)
        {
            case "Pause": case "Cancel": case "ToggleNotebook": return "global";
            case "PreviousTab": case "NextTab": case "PreviousPage": case "NextPage": return "reading";
            case "Move": case "Look": case "Jump": case "Sprint": return "gameplay";
            case "Peek": return "peek";
            default: return "device";
        }
    }
    public static bool ContextsOverlap(BindingPolicy a, BindingPolicy b) => ContextOf(a) == "global" || ContextOf(b) == "global" || ContextOf(a) == ContextOf(b);
    public static System.Collections.Generic.IReadOnlyList<BindingPolicy> BindingPolicies { get { Ensure(); return policy?.bindings ?? Array.Empty<BindingPolicy>(); } }
    public static event Action BindingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        GameplayBlocked = false;
        gamepadDeadzone = .125f;
        if (actions != null) { actions.Disable(); if (Application.isPlaying) UnityEngine.Object.Destroy(actions); else UnityEngine.Object.DestroyImmediate(actions); }
        actions = null; policy = null; BindingsChanged = null;
    }
    static void Ensure()
    {
        if (actions != null) return;
        var json = Resources.Load<TextAsset>("Localization/InputActions");
        if (json == null) return;
        actions = InputActionAsset.FromJson(json.text);
        var settings = Resources.Load<TextAsset>("Localization/InputBindings");
        policy = settings == null ? new BindingSettings() : JsonUtility.FromJson<BindingSettings>(settings.text);
        LoadOverrides(actions); actions.Enable();
        foreach (var player in PlayerInput.all) LoadOverrides(player.actions);
    }
    static void LoadOverrides(InputActionAsset target)
    {
        if (target == null || !PlayerPrefs.HasKey(SaveKey)) return;
        try { target.LoadBindingOverridesFromJson(PlayerPrefs.GetString(SaveKey)); }
        catch (Exception exception) { Debug.LogWarning("Kaydedilmiş tuş atamaları okunamadı: " + exception.Message); }
    }
    public static void Attach(PlayerInput player)
    {
        Ensure(); if (player != null) LoadOverrides(player.actions);
    }
    public static InputAction Action(string code) { Ensure(); return actions != null ? actions.FindAction("Player/" + code) : null; }
    public static bool Pressed(string code) => !Blocked && (Action(code)?.WasPressedThisFrame() ?? false);
    public static bool Released(string code) => !Blocked && (Action(code)?.WasReleasedThisFrame() ?? false);
    public static bool Held(string code) => !Blocked && (Action(code)?.IsPressed() ?? false);
    public static float Scroll => Blocked ? 0 : Action("Scroll")?.ReadValue<Vector2>().y ?? 0f;
    public static void ApplyControlSettings(float deadzone)
    {
        gamepadDeadzone = Mathf.Clamp(deadzone, .05f, .4f);
    }
    public static Vector2 ReadVectorInput(InputAction action, Vector2 fallback, bool look)
    {
        if (!(action?.activeControl is StickControl stick)) return fallback;
        // Read raw stick data so Unity's built-in default deadzone is not applied a second time.
        var value = new StickDeadzoneProcessor { min = gamepadDeadzone, max = InputSystem.settings.defaultDeadzoneMax }.Process(stick.ReadUnprocessedValue());
        if (look) value.y = -value.y;
        return value;
    }

    public static string BindingName(string code)
    {
        var action = Action(code); bool tr = GameLocalization.CurrentCode == "tr";
        if (action == null) return tr ? "Atanmamış" : "Unbound";
        var names = new List<string>();
        foreach (var binding in action.bindings)
        {
            if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
            string path = binding.effectivePath;
            if (!path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("<Pointer>/", StringComparison.OrdinalIgnoreCase)) continue;
            string name;
            switch (path.ToLowerInvariant())
            {
                case "<mouse>/leftbutton": name = tr ? "Sol Tık" : "Left Click"; break;
                case "<mouse>/rightbutton": name = tr ? "Sağ Tık" : "Right Click"; break;
                case "<mouse>/middlebutton": name = tr ? "Orta Tık" : "Middle Click"; break;
                case "<mouse>/scroll": case "<mouse>/scroll/y": name = tr ? "Tekerlek" : "Scroll"; break;
                case "<pointer>/delta": name = tr ? "Fare" : "Mouse"; break;
                default: name = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice); break;
            }
            if (!names.Contains(name)) names.Add(name);
        }
        return names.Count > 0 ? string.Join(" / ", names) : (tr ? "Atanmamış" : "Unbound");
    }
    public static string ResolveHints(string text) => InputHintText.Resolve(text, BindingName);
    public static string BindingPath(string bindingID)
    {
        Ensure();
        if (actions != null) foreach (var action in actions) foreach (var binding in action.bindings) if (binding.id.ToString() == bindingID) return binding.effectivePath;
        return "";
    }
    public static string PathDisplayName(string path)
    {
        bool tr = GameLocalization.CurrentCode == "tr";
        switch ((path ?? "").ToLowerInvariant())
        {
            case "<mouse>/leftbutton": return tr ? "Sol Tık" : "Left Click";
            case "<mouse>/rightbutton": return tr ? "Sağ Tık" : "Right Click";
            case "<mouse>/scroll": return tr ? "Tekerlek" : "Scroll";
            case "": return tr ? "Atanmamış" : "Unbound";
            default: return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }
    public static bool IsBindingPathValid(InputAction action, InputBinding binding, string path)
    {
        try
        {
            string device = InputControlPath.TryGetDeviceLayout(path), control = InputControlPath.TryGetControlLayout(path);
            if (string.IsNullOrEmpty(device) || InputSystem.LoadLayout(device) == null || string.IsNullOrEmpty(control)) return false;
            var layout = InputSystem.LoadLayout(control); if (layout?.type == null) return false;
            bool button = binding.isPartOfComposite || action.type == InputActionType.Button || action.expectedControlType == "Button";
            if (button) return typeof(ButtonControl).IsAssignableFrom(layout.type);
            if (action.expectedControlType == "Vector2") return typeof(Vector2Control).IsAssignableFrom(layout.type);
            if (string.IsNullOrEmpty(action.expectedControlType))
            {
                var originalLayout = InputSystem.LoadLayout(InputControlPath.TryGetControlLayout(binding.path));
                if (originalLayout?.type != null && typeof(AxisControl).IsAssignableFrom(originalLayout.type)) return typeof(AxisControl).IsAssignableFrom(layout.type);
                if (originalLayout?.type != null && typeof(Vector2Control).IsAssignableFrom(originalLayout.type)) return typeof(Vector2Control).IsAssignableFrom(layout.type);
            }
            return true;
        }
        catch { return false; }
    }
    public static void Rebind(string actionCode, string controlPath, int bindingIndex = 0)
    {
        var action = Action(actionCode) ?? throw new ArgumentException("Bilinmeyen eylem: " + actionCode);
        var candidates = action.bindings.Where(b => !b.isComposite && (b.path.StartsWith("<Keyboard>/") || b.path.StartsWith("<Mouse>/"))).ToArray();
        if (bindingIndex < 0 || bindingIndex >= candidates.Length) throw new ArgumentOutOfRangeException(nameof(bindingIndex));
        RebindBinding(candidates[bindingIndex].id.ToString(), controlPath);
    }
    public static void RebindBinding(string bindingID, string controlPath)
    {
        Ensure();
        var permission = policy?.bindings?.FirstOrDefault(p => p.id == bindingID);
        if (permission == null || !permission.rebindable) throw new InvalidOperationException("Bu tuş yeniden atanamaz: " + bindingID);
        var targetAction = Action(permission.action);
        var targetBinding = targetAction.bindings.First(b => b.id.ToString() == bindingID);
        if (!IsBindingPathValid(targetAction, targetBinding, controlPath)) throw new ArgumentException("Eylemle uyumsuz/geçersiz kontrol yolu: " + controlPath);
        ApplyOverride(actions, bindingID, controlPath);
        foreach (var player in PlayerInput.all) ApplyOverride(player.actions, bindingID, controlPath);
        Save();
    }
    static void ApplyOverride(InputActionAsset target, string id, string path)
    {
        if (target == null) return;
        foreach (var action in target)
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].id.ToString() == id) { if (path == null) action.RemoveBindingOverride(i); else action.ApplyBindingOverride(i, path); return; }
    }
    public static void ResetBinding(string bindingID)
    {
        Ensure(); ApplyOverride(actions, bindingID, null); foreach (var player in PlayerInput.all) ApplyOverride(player.actions, bindingID, null); Save();
    }
    public static void ResetBindings()
    {
        Ensure(); actions?.RemoveAllBindingOverrides(); foreach (var player in PlayerInput.all) player.actions.RemoveAllBindingOverrides(); Save();
    }
    public static void ReloadBindings()
    {
        Ensure();
        actions?.RemoveAllBindingOverrides(); LoadOverrides(actions);
        foreach (var player in PlayerInput.all) { player.actions.RemoveAllBindingOverrides(); LoadOverrides(player.actions); }
        BindingsChanged?.Invoke();
    }
    static void Save()
    {
        PlayerPrefs.SetString(SaveKey, actions.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); BindingsChanged?.Invoke();
    }
}
