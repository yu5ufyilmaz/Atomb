using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ContentWorkbookProject
{
    public const string WorkbookPath = "Content/GameContent.xlsx";
    public const string DatabasePath = "Assets/Scripts/Scriptables/Subtitles/MainSubtitles.asset";
    public const string InputPath = "Assets/StarterAssets/InputSystem/StarterAssets.inputactions";
    const string TablesPath = "Assets/Resources/Localization/Tables/";
    const string StampPath = "Content/GameContent.import.json";
    const string RuntimeInputPath = "Assets/Resources/Localization/InputActions.json";
    const string PolicyPath = "Assets/Resources/Localization/InputBindings.json";
    [Serializable] public class Table { public LocalizationEntry[] entries; }
    [Serializable] class Snapshot { public SheetData[] sheets; }
    [Serializable] class SheetData { public string name; public RowData[] rows; }
    [Serializable] class RowData { public string[] cells; }
    [Serializable] class Stamp { public string workbook; public string[] paths; public string[] hashes; public string[] connectionPaths; public string[] connectionHashes; }
    static readonly Dictionary<string, Tuple<long, long, string>> hashCache = new Dictionary<string, Tuple<long, long, string>>();
    static readonly Dictionary<string, Tuple<string, string>> connectionCache = new Dictionary<string, Tuple<string, string>>();

    static string C(string[] row, int column) => ContentSheet.Cell(row, column);
    static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    static Table ReadTable(string code) => JsonUtility.FromJson<Table>(File.ReadAllText(TablesPath + code + ".json"));
    static string Hash(string path)
    {
        var info = new FileInfo(path);
        if (hashCache.TryGetValue(path, out var cached) && cached.Item1 == info.Length && cached.Item2 == info.LastWriteTimeUtc.Ticks) return cached.Item3;
        string hash;
        using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path)) hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        hashCache[path] = Tuple.Create(info.Length, info.LastWriteTimeUtc.Ticks, hash); return hash;
    }
    static string ConnectionSignature(IEnumerable<string[]> rows)
    {
        string canonical = string.Join("\n", rows.OrderBy(r => C(r, 0), StringComparer.Ordinal).Select(r =>
            string.Join("\t", new[] { C(r, 0), C(r, 4), C(r, 5), C(r, 6), string.IsNullOrEmpty(C(r, 7)) ? "" : Number(float.Parse(C(r, 7), CultureInfo.InvariantCulture)) })));
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
    }
    static string CurrentConnectionSignature(string path)
    {
        string fileHash = Hash(path);
        if (connectionCache.TryGetValue(path, out var cached) && cached.Item1 == fileHash) return cached.Item2;
        var rows = new ContentSheet("Baglantilar");
        if (path.EndsWith(".unity", StringComparison.Ordinal)) VisitScenes(new[] { path }, (asset, components) => ExportConnections(rows, asset, components));
        else
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) throw new InvalidDataException("Bağlantı dosyası yok: " + path);
            ExportConnections(rows, path, root.GetComponentsInChildren<MonoBehaviour>(true));
        }
        string signature = ConnectionSignature(rows.Rows); connectionCache[path] = Tuple.Create(fileHash, signature); return signature;
    }
    public static ContentWorkbookFile Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("İçerik aktarımı için Play modundan çıkın.");
        var book = ContentWorkbookFile.CreateTemplate();
        var tr = ReadTable("tr").entries.ToDictionary(e => e.key);
        var en = ReadTable("en").entries.ToDictionary(e => e.key);
        foreach (string key in en.Keys.Union(tr.Keys))
        {
            if (key.StartsWith("subtitle.", StringComparison.Ordinal)) continue;
            LocalizationEntry a, b; en.TryGetValue(key, out a); tr.TryGetValue(key, out b);
            book.Sheet("Metinler").AddRow(new[] { key, a != null ? a.source : b.source, b?.text ?? "", a?.text ?? "", key.Split('.')[0] });
        }
        var database = AssetDatabase.LoadAssetAtPath<SubtitleData>(DatabasePath);
        foreach (var entry in database.entries)
        {
            book.Sheet("Diyaloglar").AddRow(new[] { entry.id, entry.note ?? "", AssetDatabase.GetAssetPath(entry.voiceClip) });
            for (int i = 0; i < entry.segments.Count; i++)
            {
                var segment = entry.segments[i]; string key = "subtitle." + entry.id + "." + i;
                LocalizationEntry a, b; en.TryGetValue(key, out a); tr.TryGetValue(key, out b);
                book.Sheet("Altyazilar").AddRow(new[] { entry.id, (i + 1).ToString(), Number(segment.duration), b?.text ?? segment.textTR ?? "", a?.text ?? segment.textEN ?? "", segment.textDE ?? "" });
            }
        }
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        var existingPolicy = File.Exists(PolicyPath) ? JsonUtility.FromJson<GameInput.BindingSettings>(File.ReadAllText(PolicyPath)) : new GameInput.BindingSettings();
        foreach (var map in input.actionMaps)
            foreach (var action in map.actions)
                foreach (var binding in action.bindings)
                {
                    if (binding.isComposite) continue;
                    var policy = existingPolicy.bindings?.FirstOrDefault(p => p.id == binding.id.ToString());
                    book.Sheet("Kontroller").AddRow(new[] { action.name, binding.id.ToString(), map.name, binding.name ?? "", binding.path, (policy == null || policy.rebindable) && action.name != "Look" ? "Evet" : "Hayır", policy?.textTR ?? action.name, policy?.textEN ?? action.name });
                }
        VisitScenes(ScenePaths(), (path, components) => ExportConnections(book.Sheet("Baglantilar"), path, components));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null) ExportConnections(book.Sheet("Baglantilar"), path, root.GetComponentsInChildren<MonoBehaviour>(true));
        }
        AddGuide(book.Sheet("Kilavuz"));
        return book;
    }
    static string[] ScenePaths() => AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
    static void VisitScenes(IEnumerable<string> paths, Action<string, MonoBehaviour[]> visitor)
    {
        foreach (string path in paths.Distinct())
        {
            var loaded = SceneManager.GetSceneByPath(path); bool temporary = !loaded.IsValid() || !loaded.isLoaded;
            var scene = temporary ? EditorSceneManager.OpenPreviewScene(path) : loaded;
            try { visitor(path, scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray()); }
            finally { if (temporary) EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
    static void ExportConnections(ContentSheet sheet, string path, IEnumerable<MonoBehaviour> components)
    {
        foreach (var component in components)
        {
            if (component == null) continue;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
            string objectPath = component.name; for (var parent = component.transform.parent; parent != null; parent = parent.parent) objectPath = parent.name + "/" + objectPath;
            if (component is DialogAction action)
            {
                var reminders = action.reminderSubtitleIDs ?? Array.Empty<string>();
                if (reminders.Length > 2) throw new InvalidOperationException(path + "/" + objectPath + ": ikiden fazla hatırlatma var; dışa aktarım veri kaybetmemek için durduruldu.");
                string conditions = string.Join(", ", component.GetComponents<ICondition>().Select(c => {
                    if (c is PrerequisiteCondition prereq) return (prereq.requireTriggered ? "sonra: " : "önce: ") + (prereq.requiredEvent != null ? prereq.requiredEvent.name : "eksik");
                    return c.GetType().Name;
                }));
                sheet.AddRow(new[] { id, path, objectPath, "DialogAction", action.subtitleID ?? "", reminders.Length > 0 ? reminders[0] : "", reminders.Length > 1 ? reminders[1] : "", Number(action.reminderDelay), conditions, AssetDatabase.GetAssetPath(action.dialogClip) });
                continue;
            }
            var serialized = new SerializedObject(component); var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.String && property.name.EndsWith("subtitleID", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(property.stringValue))
                    sheet.AddRow(new[] { id + "#" + property.propertyPath, path, objectPath, component.GetType().Name + "." + property.propertyPath + " (salt okunur)", property.stringValue, "", "", "", "Unity Inspector üzerinden düzenlenir", "" });
        }
    }
    static void AddGuide(ContentSheet sheet)
    {
        string[][] rows = {
            new[] { "Düzenleme kaynağı", "Bu çalışma kitabını düzenleyin. Üretilen JSON ve altyazı assetini ayrıca elle değiştirmeyin. Aktarmadan önce Excel'i kapatın." },
            new[] { "Oyuna aktar", "Unity: Senzora > İçerik Tablosu. Önce Doğrula, sonra Oyuna Aktar. Sahneleri kaydedin ve Play modundan çıkın." },
            new[] { "Metinler", "Kod ve Kaynak mevcut oyun çağrılarıyla bağlantıdır. Türkçe / İngilizce görünen yazılardır. Yeni metin için benzersiz kod ekleyin ve Unity'deki tüketiciye bağlayın." },
            new[] { "Diyaloglar", "Bir konuşmanın sabit kodu, notu ve isteğe bağlı AudioClip proje yolu. Kodları değiştirmek kullanan bağlantıları da güncellemeyi gerektirir." },
            new[] { "Altyazilar", "Diyalog ile bağlanır. Sıra 1, 2, 3...; süre saniye cinsinden pozitif sayıdır. Satırları sıralamak oynatma sırasını değiştirmez. Almanca isteğe bağlıdır." },
            new[] { "Baglantilar", "DialogAction satırlarında Giriş, Hatırlatma 1/2 ve Bekleme düzenlenebilir. Diğer sütunlar ve salt okunur satırlar envanterdir. Event koşulları Unity'de kalır." },
            new[] { "Bağlantı yenileme", "Unity'deki yeni eventleri görmek için Bağlantı Envanterini Yenile. Mevcut hedeflerin Excel'deki giriş/hatırlatma/bekleme değişiklikleri korunur. Kaybolan hedefleri doğrulama bildirir." },
            new[] { "Kontroller", "Varsayılan sütununda <Keyboard>/f veya <Mouse>/leftButton gibi Input System yolu. Bağlama kimliği / Eylem / Bağlam / Parça bağlantı alanıdır; değiştirmeyin. Evet/Hayır çalışma zamanı yeniden atamasını belirler." },
            new[] { "Dinamik tuş", "Metinde [{input:Cancel}] Kitabı kapat kullanın. Cancel sabit eylem kodudur, çevrilmez. Oyun güncel tuş adını gösterir. Aynı kodu TR ve EN içinde koruyun." },
            new[] { "Değişkenler", "{0}, {1} gibi alanları her dilde koruyun. Hücre içi satır sonu kullanabilirsiniz. Formül hücreleri aktarılmaz; hücrelere doğrudan değer yazın." },
            new[] { "Eksik çeviriler", "Boş TR/EN çevirileri uyarı olarak listelenir ve dil geri dönüşü kullanılır. Boş hücre tamamlanmış çeviri sayılmaz." },
            new[] { "Oyuncu atamaları", "Excel varsayılanları oyuncunun kaydedilmiş atamasını sıfırlamaz. Çalışma zamanı API'si GameInput.Rebind / ResetBinding. Oyuncu atamalarını Ayarlar > Kontroller sekmesinden değiştirebilir." },
            new[] { "Kaynaklar", DatabasePath + " ; " + TablesPath + "tr.json ; " + TablesPath + "en.json ; " + InputPath + " ; Assets/Scenes" }
        };
        foreach (var row in rows) sheet.AddRow(row);
    }
    [MenuItem("Senzora/İçerik Tablosu/Geliştirici/Mevcut Veriyi JSON Dışa Aktar")]
    public static void ExportSnapshot()
    {
        var book = Export(); Directory.CreateDirectory("Temp/ContentWorkbook");
        var snapshot = new Snapshot { sheets = book.Sheets.Select(s => new SheetData { name = s.Name, rows = s.Rows.Select(r => new RowData { cells = r }).ToArray() }).ToArray() };
        File.WriteAllText("Temp/ContentWorkbook/snapshot.json", JsonUtility.ToJson(snapshot, true));
        Debug.Log("CONTENT SNAPSHOT PASS: " + string.Join(", ", book.Sheets.Select(s => s.Name + "=" + (s.Rows.Count - 1))));
    }
    public static ContentWorkbookFile Validate(string path)
    {
        var book = ContentWorkbookFile.Read(path); book.Validate();
        if (book.Errors.Count > 0) return book;
        var database = AssetDatabase.LoadAssetAtPath<SubtitleData>(DatabasePath);
        var textKeys = new HashSet<string>(book.Data("Metinler").Select(r => C(r, 0)), StringComparer.Ordinal);
        foreach (var entry in ReadTable("en").entries.Concat(ReadTable("tr").entries).GroupBy(e => e.key).Select(g => g.First()))
            if (!entry.key.StartsWith("subtitle.", StringComparison.Ordinal) && !textKeys.Contains(entry.key)) book.Errors.Add("Metinler: mevcut kod silinmiş/yeniden adlandırılmış: " + entry.key);
        foreach (var row in book.Data("Diyaloglar"))
            if (!string.IsNullOrEmpty(C(row, 2)) && AssetDatabase.LoadAssetAtPath<AudioClip>(C(row, 2)) == null) book.Errors.Add("Diyaloglar: ses dosyası bulunamadı: " + C(row, 2));
        // Removing an existing dialogue can break non-scene references (e.g. scriptable events).
        var ids = new HashSet<string>(book.Data("Diyaloglar").Select(r => C(r, 0)));
        foreach (var entry in database.entries) if (!ids.Contains(entry.id)) book.Errors.Add("Diyaloglar: mevcut kod silinmiş/yeniden adlandırılmış: " + entry.id + ". Bağlantıları Unity'de taşıyın.");
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        var known = input.actionMaps.SelectMany(m => m.actions).SelectMany(a => a.bindings.Select(b => new { action = a, binding = b })).Where(p => !p.binding.isComposite).ToDictionary(p => p.binding.id.ToString());
        foreach (var row in book.Data("Kontroller"))
        {
            if (!known.TryGetValue(C(row, 1), out var item) || item.action.name != C(row, 0) || item.action.actionMap.name != C(row, 2) || (item.binding.name ?? "") != C(row, 3)) { book.Errors.Add("Kontroller: bağlama hedefi/eylem değişmiş: " + C(row, 1)); continue; }
            // Resolve a device layout/control without requiring a physical device attached.
            try
            {
                if (!GameInput.IsBindingPathValid(item.action, item.binding, C(row, 4))) book.Errors.Add("Kontroller: eylemle uyumsuz/geçersiz kontrol yolu: " + C(row, 4));
            }
            catch { book.Errors.Add("Kontroller: geçersiz kontrol yolu: " + C(row, 4)); }
        }
        if (book.Data("Kontroller").Count() != known.Count) book.Errors.Add("Kontroller: mevcut bağlamalar eksik. Satır silmek yerine geçerli bir varsayılan belirleyin.");
        var expected = new HashSet<string>(book.Data("Baglantilar").Select(r => C(r, 0)));
        var found = new Dictionary<string, string[]>(); var current = new ContentSheet("Baglantilar");
        var scenePaths = book.Data("Baglantilar").Select(r => C(r, 1)).Where(p => p.EndsWith(".unity", StringComparison.Ordinal)).Distinct().ToArray();
        foreach (string scene in scenePaths) if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) book.Errors.Add("Baglantilar: sahne yok: " + scene);
        if (book.Errors.Count > 0) return book;
        VisitScenes(scenePaths, (asset, components) => ExportConnections(current, asset, components));
        foreach (string prefab in book.Data("Baglantilar").Select(r => C(r, 1)).Where(p => p.EndsWith(".prefab", StringComparison.Ordinal)).Distinct())
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (root == null) book.Errors.Add("Baglantilar: prefab yok: " + prefab);
            else ExportConnections(current, prefab, root.GetComponentsInChildren<MonoBehaviour>(true));
        }
        foreach (var row in current.Rows) found[C(row, 0)] = row;
        foreach (var row in book.Data("Baglantilar"))
        {
            if (!found.TryGetValue(C(row, 0), out var actual)) { book.Errors.Add("Baglantilar: bileşen bulunamadı: " + C(row, 1) + " / " + C(row, 2)); continue; }
            if (C(actual, 1) != C(row, 1) || C(actual, 3) != C(row, 3)) book.Errors.Add("Baglantilar: dosya/tür kimliği değiştirilmiş: " + C(row, 0));
            if (C(row, 3) != "DialogAction" && C(actual, 4) != C(row, 4)) book.Errors.Add("Baglantilar: salt okunur tüketici Unity'de düzenlenmeli: " + C(row, 2));
        }
        return book;
    }

    public static void Import(string path) => Import(path, VerifyAppliedData);

    // Verification runs before the stamp is committed, inside the rollback boundary.
    internal static void Import(string path, Action<ContentWorkbookFile> verifyApplied)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play modundan çıkın.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Önce açık sahnelerdeki değişiklikleri kaydedin. Aktarım hiçbir sahneyi sizin yerinize kaydetmedi.");
        string importHash = Hash(path);
        var book = Validate(path);
        if (Hash(path) != importHash) throw new InvalidOperationException("Excel doğrulama sırasında değişti. Kaydedip kapatın ve yeniden aktarın.");
        if (book.Errors.Count > 0) throw new InvalidDataException(string.Join("\n", book.Errors));
        var originalDatabase = AssetDatabase.LoadAssetAtPath<SubtitleData>(DatabasePath);
        if (EditorUtility.IsDirty(originalDatabase)) throw new InvalidOperationException("MainSubtitles.asset üzerinde kaydedilmemiş değişiklik var. Önce bilinçli olarak kaydedin veya geri alın.");
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.scene.isDirty) throw new InvalidOperationException("Prefab düzenleme sahnesinde kaydedilmemiş değişiklik var. Aktarım yapılmadı.");
        string databaseMemory = EditorJsonUtility.ToJson(originalDatabase);
        var staged = new Dictionary<string, string>(); var tr = new List<LocalizationEntry>(); var en = new List<LocalizationEntry>();
        foreach (var row in book.Data("Metinler"))
        {
            string english = !string.IsNullOrWhiteSpace(C(row, 3)) ? C(row, 3) : !string.IsNullOrWhiteSpace(C(row, 1)) ? C(row, 1) : C(row, 0);
            tr.Add(new LocalizationEntry { key = C(row, 0), source = C(row, 1), text = string.IsNullOrWhiteSpace(C(row, 2)) ? english : C(row, 2) });
            en.Add(new LocalizationEntry { key = C(row, 0), source = C(row, 1), text = english });
        }
        var entries = new List<SubtitleEntry>();
        foreach (var row in book.Data("Diyaloglar"))
        {
            var entry = new SubtitleEntry { id = C(row, 0), note = C(row, 1), voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(C(row, 2)) };
            foreach (var segment in book.Data("Altyazilar").Where(s => C(s, 0) == entry.id).OrderBy(s => int.Parse(C(s, 1))))
            {
                string key = "subtitle." + entry.id + "." + entry.segments.Count;
                entry.segments.Add(new SubtitleSegment { duration = float.Parse(C(segment, 2), CultureInfo.InvariantCulture), textTR = C(segment, 3), textEN = C(segment, 4), textDE = C(segment, 5) });
                // Preserve canonical source aliases already consumed by legacy text callers.
                string source = ""; var old = ReadTable("en").entries.FirstOrDefault(e => e.key == key); if (old != null) source = old.source;
                string english = string.IsNullOrWhiteSpace(C(segment, 4)) ? C(segment, 3) : C(segment, 4);
                string turkish = string.IsNullOrWhiteSpace(C(segment, 3)) ? english : C(segment, 3);
                if (!string.IsNullOrWhiteSpace(english) || !string.IsNullOrWhiteSpace(turkish))
                {
                    tr.Add(new LocalizationEntry { key = key, source = source, text = turkish }); en.Add(new LocalizationEntry { key = key, source = source, text = english });
                }
            }
            entries.Add(entry);
        }
        staged[TablesPath + "tr.json"] = JsonUtility.ToJson(new Table { entries = tr.ToArray() }, true) + "\n";
        staged[TablesPath + "en.json"] = JsonUtility.ToJson(new Table { entries = en.ToArray() }, true) + "\n";
        var originalInput = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        var input = InputActionAsset.FromJson(originalInput.ToJson());
        try
        {
            foreach (var row in book.Data("Kontroller"))
            {
                var action = input.FindAction(C(row, 2) + "/" + C(row, 0), true); int index = action.bindings.ToList().FindIndex(b => b.id.ToString() == C(row, 1));
                action.ChangeBinding(index).WithPath(C(row, 4));
            }
            staged[InputPath] = input.ToJson(); staged[RuntimeInputPath] = input.ToJson();
        }
        finally { Object.DestroyImmediate(input); }
        staged[PolicyPath] = JsonUtility.ToJson(new GameInput.BindingSettings { bindings = book.Data("Kontroller").Select(r => new GameInput.BindingPolicy { id = C(r, 1), action = C(r, 0), context = C(r, 2), part = C(r, 3), rebindable = C(r, 5) == "Evet", textTR = C(r, 6), textEN = C(r, 7) }).ToArray() }, true);
        var links = book.Data("Baglantilar").Where(r => C(r, 3) == "DialogAction").ToArray();
        var paths = new HashSet<string>(staged.Keys) { DatabasePath, StampPath };
        foreach (var row in links) paths.Add(C(row, 1));
        var backup = paths.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        // Unity's native SaveScene can abort the Editor on a sharing violation. Catch
        // pre-existing locks with managed I/O before changing either files or memory.
        foreach (string target in paths.Where(File.Exists))
            using (var writable = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        var setup = EditorSceneManager.GetSceneManagerSetup(); var opened = new List<Scene>();
        AssetDatabase.DisallowAutoRefresh();
        try
        {
            foreach (var item in staged) if (!File.Exists(item.Key) || File.ReadAllText(item.Key) != item.Value) File.WriteAllText(item.Key, item.Value);
            var database = AssetDatabase.LoadAssetAtPath<SubtitleData>(DatabasePath);
            if (database.entries == null || JsonUtility.ToJson(database) != DatabaseJson(database, entries)) { database.entries = entries; EditorUtility.SetDirty(database); AssetDatabase.SaveAssetIfDirty(database); }
            foreach (var group in links.GroupBy(r => C(r, 1)))
            {
                if (group.Key.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    // Read/write the same persistent prefab objects so exported IDs remain identical.
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                    if (ApplyLinks(group, root.GetComponentsInChildren<DialogAction>(true)) && PrefabUtility.SavePrefabAsset(root) == null) throw new IOException("Prefab kaydedilemedi: " + group.Key);
                }
                else
                {
                    var scene = SceneManager.GetSceneByPath(group.Key); bool temporary = !scene.IsValid() || !scene.isLoaded;
                    if (temporary) { scene = EditorSceneManager.OpenScene(group.Key, OpenSceneMode.Additive); opened.Add(scene); }
                    if (ApplyLinks(group, scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogAction>(true)))) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Sahne kaydedilemedi: " + group.Key); }
                }
            }
            verifyApplied(book);
            var stamp = new Stamp { workbook = importHash, paths = paths.Where(p => p != StampPath && !p.EndsWith(".unity") && !p.EndsWith(".prefab")).OrderBy(p => p, StringComparer.Ordinal).ToArray() };
            stamp.hashes = stamp.paths.Select(Hash).ToArray();
            stamp.connectionPaths = book.Data("Baglantilar").Select(r => C(r, 1)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            stamp.connectionHashes = stamp.connectionPaths.Select(p => ConnectionSignature(book.Data("Baglantilar").Where(r => C(r, 1) == p))).ToArray();
            connectionCache.Clear(); File.WriteAllText(StampPath, JsonUtility.ToJson(stamp, true));
        }
        catch
        {
            foreach (var item in backup)
            {
                if (item.Value == null) { if (File.Exists(item.Key)) File.Delete(item.Key); }
                else if (!File.Exists(item.Key) || !File.ReadAllBytes(item.Key).SequenceEqual(item.Value)) File.WriteAllBytes(item.Key, item.Value);
            }
            EditorJsonUtility.FromJsonOverwrite(databaseMemory, originalDatabase); EditorUtility.ClearDirty(originalDatabase);
            // All loaded scenes were clean before the transaction.
            foreach (var scene in opened) if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            opened.Clear();
            // Force affected clean scenes to reload their restored disk contents, not just layout.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            foreach (string scenePath in links.Select(r => C(r, 1)).Where(p => p.EndsWith(".unity")).Distinct())
            {
                var scene = SceneManager.GetSceneByPath(scenePath); if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            foreach (string prefab in links.Select(r => C(r, 1)).Where(p => p.EndsWith(".prefab")).Distinct()) AssetDatabase.ImportAsset(prefab, ImportAssetOptions.ForceUpdate);
            throw;
        }
        finally
        {
            foreach (var scene in opened) if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.AllowAutoRefresh(); AssetDatabase.Refresh();
        }
        Debug.Log("CONTENT IMPORT PASS: " + entries.Count + " diyalog; " + tr.Count + " metin/dil. Uyarı: " + book.Warnings.Count);
    }
    static void VerifyAppliedData(ContentWorkbookFile book)
    {
        var database = AssetDatabase.LoadAssetAtPath<SubtitleData>(DatabasePath);
        foreach (var group in book.Data("Altyazilar").GroupBy(r => C(r, 0)))
        {
            var segments = database.GetEntry(group.Key)?.segments;
            var expected = group.OrderBy(r => int.Parse(C(r, 1))).ToArray();
            if (segments == null || segments.Count != expected.Length) throw new InvalidDataException("Aktarım sonrası segment sayısı uyuşmuyor: " + group.Key);
            for (int i = 0; i < expected.Length; i++)
                if (!Mathf.Approximately(segments[i].duration, float.Parse(C(expected[i], 2), CultureInfo.InvariantCulture)) || segments[i].textTR != C(expected[i], 3) || segments[i].textEN != C(expected[i], 4)) throw new InvalidDataException("Aktarım sonrası metin/süre uyuşmuyor: " + group.Key);
        }
    }
    static string DatabaseJson(SubtitleData database, List<SubtitleEntry> entries)
    {
        var copy = Object.Instantiate(database); try { copy.entries = entries; return JsonUtility.ToJson(copy); } finally { Object.DestroyImmediate(copy); }
    }
    static bool ApplyLinks(IEnumerable<string[]> rows, IEnumerable<DialogAction> components)
    {
        var lookup = components.ToDictionary(c => GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()); bool changed = false;
        foreach (var row in rows)
        {
            if (!lookup.TryGetValue(C(row, 0), out var action)) throw new InvalidOperationException("Bağlantı hedefi yüklenemedi: " + C(row, 2));
            var reminders = new[] { C(row, 5), C(row, 6) }.Where(s => !string.IsNullOrEmpty(s)).ToArray();
            float delay = string.IsNullOrEmpty(C(row, 7)) ? action.reminderDelay : float.Parse(C(row, 7), CultureInfo.InvariantCulture);
            if (action.subtitleID == C(row, 4) && (action.reminderSubtitleIDs ?? Array.Empty<string>()).SequenceEqual(reminders) && Mathf.Approximately(action.reminderDelay, delay)) continue;
            action.subtitleID = C(row, 4); action.reminderSubtitleIDs = reminders; action.reminderDelay = delay;
            EditorUtility.SetDirty(action); changed = true;
        }
        return changed;
    }
    public static void RefreshConnections()
    {
        var old = ContentWorkbookFile.Read(WorkbookPath); var current = Export().Sheet("Baglantilar");
        var existing = old.Data("Baglantilar").ToDictionary(r => C(r, 0)); var seen = new HashSet<string>();
        foreach (var row in current.Rows.Skip(1))
        {
            seen.Add(C(row, 0));
            if (C(row, 3) == "DialogAction" && existing.TryGetValue(C(row, 0), out var previous)) for (int c = 4; c <= 7; c++) row[c] = C(previous, c);
        }
        // Keep disappeared targets visible until the author explicitly removes the stale row.
        foreach (var row in existing.Values.Where(r => !seen.Contains(C(r, 0)))) current.AddRow(row);
        ContentWorkbookFile.ReplaceSheet(WorkbookPath, current);
        Debug.Log("Bağlantı envanteri yenilendi. Excel'deki metinler ve mevcut konuşma seçimleri korundu.");
    }
    public static string StaleReason()
    {
        if (!File.Exists(WorkbookPath)) return "Content/GameContent.xlsx bulunamadı.";
        if (!File.Exists(StampPath)) return "Excel henüz oyuna aktarılmadı.";
        var stamp = JsonUtility.FromJson<Stamp>(File.ReadAllText(StampPath));
        if (stamp.workbook != Hash(WorkbookPath)) return "Excel son aktarımdan sonra değişti. İçerik Tablosu > Oyuna Aktar kullanın.";
        for (int i = 0; i < stamp.paths.Length; i++) if (!File.Exists(stamp.paths[i]) || stamp.hashes[i] != Hash(stamp.paths[i])) return "Aktarılan veri Unity'de değişmiş: " + stamp.paths[i] + ". Excel'e bilinçli olarak taşıyın veya yeniden aktarın.";
        if (stamp.connectionPaths != null)
            for (int i = 0; i < stamp.connectionPaths.Length; i++)
                if (!File.Exists(stamp.connectionPaths[i]) || stamp.connectionHashes[i] != CurrentConnectionSignature(stamp.connectionPaths[i])) return "Sahne/prefab konuşma bağlantıları değişmiş: " + stamp.connectionPaths[i] + ". Bağlantı envanterini yenileyin.";
        return null;
    }
    [MenuItem("Senzora/İçerik Tablosu/Geliştirici/Aktarım Durumunu Doğrula")]
    public static void VerifySync()
    {
        string reason = StaleReason();
        if (reason != null) throw new InvalidDataException(reason);
        Debug.Log("CONTENT SYNC PASS: workbook, generated assets and semantic dialogue connections agree.");
    }
}

public sealed class ContentWorkbookBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        // Projects that have not adopted the workbook are unaffected.
        if (!File.Exists(ContentWorkbookProject.WorkbookPath)) return;
        string reason = ContentWorkbookProject.StaleReason(); if (reason != null) throw new BuildFailedException(reason);
    }
}
