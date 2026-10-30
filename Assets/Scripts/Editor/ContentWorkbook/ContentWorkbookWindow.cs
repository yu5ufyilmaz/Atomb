using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ContentWorkbookWindow : EditorWindow
{
    string report = "Excel'i düzenleyip kapatın. Ardından doğrulayıp oyuna aktarın.";
    Vector2 scroll;
    [MenuItem("Senzora/İçerik Tablosu/Aç")]
    public static void Open() => GetWindow<ContentWorkbookWindow>("İçerik Tablosu");
    void OnGUI()
    {
        EditorGUILayout.LabelField("Metinler, altyazılar ve kontroller", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(ContentWorkbookProject.WorkbookPath);
        string stale;
        try { stale = ContentWorkbookProject.StaleReason(); } catch (Exception e) { stale = e.Message; }
        if (stale != null) EditorGUILayout.HelpBox(stale, MessageType.Warning);
        else EditorGUILayout.HelpBox("Excel ve üretilen oyun verileri güncel.", MessageType.Info);
        if (GUILayout.Button("Excel Çalışma Kitabını Aç")) Run(() => { EditorUtility.OpenWithDefaultApp(Path.GetFullPath(ContentWorkbookProject.WorkbookPath)); });
        if (GUILayout.Button("Doğrula")) Run(() => {
            var book = ContentWorkbookProject.Validate(ContentWorkbookProject.WorkbookPath);
            report = book.Errors.Count == 0 ? "Yapısal hata yok. Eksik çeviri uyarısı: " + book.Warnings.Count : "Hata: " + book.Errors.Count;
            report += "\n" + string.Join("\n", book.Errors.Concat(book.Warnings));
        });
        if (GUILayout.Button("Oyuna Aktar")) Run(() => { ContentWorkbookProject.Import(ContentWorkbookProject.WorkbookPath); report = "Aktarıldı. Yeni varsayılanlar bir sonraki oyun oturumunda kullanılır. Oyuncunun kayıtlı tuş atamaları korundu."; });
        if (GUILayout.Button("Bağlantı Envanterini Yenile")) Run(() => { ContentWorkbookProject.RefreshConnections(); report = "Bağlantılar yenilendi; metinleriniz ve mevcut konuşma seçimleriniz korundu. Doğrulayıp aktarın."; });
        scroll = EditorGUILayout.BeginScrollView(scroll);
        float reportHeight = EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(report), Mathf.Max(100, position.width - 36));
        EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel, GUILayout.Height(Mathf.Max(400, reportHeight + 10)));
        EditorGUILayout.EndScrollView();
    }
    void Run(Action action)
    {
        try { action(); } catch (Exception e) { report = e.Message; Debug.LogError("İçerik tablosu: " + e.Message); }
    }
    [MenuItem("Senzora/İçerik Tablosu/Geliştirici/Doğrula ve Aktar")]
    public static void ImportForChecks() => ContentWorkbookProject.Import(ContentWorkbookProject.WorkbookPath);
}
