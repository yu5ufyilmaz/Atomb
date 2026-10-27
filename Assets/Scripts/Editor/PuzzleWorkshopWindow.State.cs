using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class PuzzleWorkshopWindow
{
    [Serializable] private sealed class DraftEntry
    { public string id, title, json, clean, assetPath; }
    [Serializable] private sealed class DraftStore
    { public List<DraftEntry> entries = new List<DraftEntry>(); }
    private DraftStore drafts = new DraftStore();
    private double lastDraftWrite;
    private static string DraftPath => Path.GetFullPath("UserSettings/PuzzleWorkshop/drafts.json");

    private void LoadDraftStore()
    {
        foreach (string path in new[] { DraftPath, DraftPath + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try { drafts = JsonUtility.FromJson<DraftStore>(File.ReadAllText(path)) ?? new DraftStore(); return; }
            catch (Exception e) when (e is IOException || e is ArgumentException) { Debug.LogWarning("Atölye taslağı okunamadı: " + e.Message); }
        }
    }

    private void RememberDraft(bool force = false)
    {
        if (document == null) return;
        transientJson = JsonUtility.ToJson(document);
        if (!HasDraftChanges)
        {
            if (drafts.entries.RemoveAll(d => d.id == draftID) > 0) WriteDraftStore();
            return;
        }
        if (string.IsNullOrEmpty(draftID)) draftID = Guid.NewGuid().ToString("N");
        var entry = drafts.entries.Find(d => d.id == draftID);
        if (entry == null) { entry = new DraftEntry { id = draftID }; drafts.entries.Add(entry); }
        entry.title = Puzzle.title; entry.json = transientJson; entry.clean = cleanJson;
        entry.assetPath = sourceAsset != null ? AssetDatabase.GetAssetPath(sourceAsset) : "";
        if (force || EditorApplication.timeSinceStartup - lastDraftWrite >= .5) WriteDraftStore();
    }

    private void WriteDraftStore()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DraftPath));
            if (File.Exists(DraftPath)) File.Copy(DraftPath, DraftPath + ".bak", true);
            File.WriteAllText(DraftPath, JsonUtility.ToJson(drafts, true)); lastDraftWrite = EditorApplication.timeSinceStartup;
        }
        catch (IOException e) { SetMessage("Taslak otomatik korunamadı; Kaydet kullan: " + e.Message, MessageType.Warning); }
    }

    public void Select(PuzzleDefinitionSO asset)
    {
        if (asset == null) return;
        RememberDraft(true);
        sourceAsset = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)) ? null : asset;
        var copy = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); copy.hideFlags = HideFlags.HideAndDontSave;
        copy.definition = asset.definition.Clone(); copy.includeInPool = asset.includeInPool;
        document = copy; cleanJson = JsonUtility.ToJson(copy); draftID = Guid.NewGuid().ToString("N");
        transientJson = cleanJson; selectedNode = connectFrom = -1; graphScroll = Vector2.zero;
        tab = 1; fitGraph = true; ValidateDocument();
    }

    public void NewDocument() => CreateForKind(document != null ? Puzzle.kind : PuzzleKind.ColorMixture);

    private void CreateForKind(PuzzleKind kind)
    {
        RememberDraft(true); sourceAsset = null;
        document = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); document.hideFlags = HideFlags.HideAndDontSave;
        document.definition = new PuzzleDefinition { kind = kind, rules = PuzzleRules.ForKind(kind),
            title = kind == PuzzleKind.ColorMixture ? "Yeni Renk Karışımı" : "Yeni Sinyal Deneyi" };
        document.includeInPool = false; cleanJson = JsonUtility.ToJson(document); draftID = Guid.NewGuid().ToString("N");
        transientJson = cleanJson; selectedNode = connectFrom = -1; graphScroll = Vector2.zero;
        SetMessage("Hazır başlangıç. Parça ekle veya Rastgele oluştur'u kullan; ayrıntıları Kurallar sekmesinden değiştirebilirsin.", MessageType.Info);
    }

    private void RecoverDraft(int index)
    {
        if (index < 0 || index >= drafts.entries.Count) return;
        var entry = drafts.entries[index];
        RememberDraft(true);
        document = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); document.hideFlags = HideFlags.HideAndDontSave;
        JsonUtility.FromJsonOverwrite(entry.json, document); cleanJson = entry.clean; transientJson = entry.json; draftID = entry.id;
        sourceAsset = string.IsNullOrEmpty(entry.assetPath) ? null : AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(entry.assetPath);
        selectedNode = connectFrom = -1; tab = 1; fitGraph = true;
        SetMessage("Korunan taslak açıldı. Asıl puzzleı güncellemek için Kaydet kullan.", MessageType.Info);
    }

    private void OpenPicker()
    {
        var menu = new GenericMenu();
        foreach (string guid in AssetDatabase.FindAssets("t:PuzzleDefinitionSO"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) menu.AddItem(new GUIContent(asset.definition.title), sourceAsset == asset, () => Select(asset));
        }
        menu.ShowAsContext();
    }

    public bool Save(bool saveAs)
    {
        string path = sourceAsset != null && !saveAs ? AssetDatabase.GetAssetPath(sourceAsset) : "";
        if (string.IsNullOrEmpty(path))
        {
            PuzzleWorkshopSetup.EnsureAssets();
            string filename = string.Concat(Puzzle.title.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) < 0 && c != '/' && c != '\\' ? c : '_')).Trim();
            if (string.IsNullOrEmpty(filename)) filename = "Puzzle";
            path = AssetDatabase.GenerateUniqueAssetPath(PuzzleWorkshopSetup.Folder + "/" + filename + ".asset");
        }
        return SaveToPath(path, saveAs);
    }

    public bool SaveToPath(string path, bool saveAs)
    {
        var candidate = Puzzle.Clone();
        if (saveAs) candidate.id = Guid.NewGuid().ToString("N");
        string issue = PuzzleSimulation.Validate(candidate);
        if (issue != null && document.includeInPool) { SetMessage("Havuza eklenemez: " + issue + " Taslak olarak kaydedebilirsin.", MessageType.Warning); return false; }
        var existing = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(path);
        if (AssetDatabase.LoadMainAssetAtPath(path) != null && (saveAs || existing != sourceAsset))
        { SetMessage("Bu dosya başka bir assete ait; üzerine yazılmadı.", MessageType.Error); return false; }
        foreach (string guid in AssetDatabase.FindAssets("t:PuzzleDefinitionSO"))
        {
            var other = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (other != null && other != existing && other.definition.id == candidate.id)
            { SetMessage("Aynı kimlikte başka bir puzzle var. Kopyasını Kaydet kullan.", MessageType.Error); return false; }
        }
        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<PuzzleDefinitionSO>(); existing.definition = candidate; existing.includeInPool = document.includeInPool;
            AssetDatabase.CreateAsset(existing, path);
        }
        else
        {
            Undo.RecordObject(existing, "Puzzleı kaydet"); existing.definition = candidate; existing.includeInPool = document.includeInPool;
            EditorUtility.SetDirty(existing);
        }
        AssetDatabase.SaveAssetIfDirty(existing);
        sourceAsset = existing; document.definition = candidate.Clone();
        var pool = AssetDatabase.LoadAssetAtPath<PuzzlePoolSO>(PuzzleWorkshopSetup.PoolPath);
        if (pool != null && document.includeInPool && !pool.puzzles.Contains(existing))
        { Undo.RecordObject(pool, "Puzzleı havuza ekle"); pool.puzzles.Add(existing); EditorUtility.SetDirty(pool); AssetDatabase.SaveAssetIfDirty(pool); }
        drafts.entries.RemoveAll(d => d.id == draftID); WriteDraftStore();
        cleanJson = transientJson = JsonUtility.ToJson(document);
        SetMessage(issue == null ? "Kaydedildi. " + (document.includeInPool ? "Yeni oyun havuzunda etkin." : "Taslak olarak saklandı.") : "Taslak kaydedildi: " + issue,
            issue == null ? MessageType.Info : MessageType.Warning);
        return true;
    }
}
