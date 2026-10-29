using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TutorialVoiceChecks
{
    [MenuItem("Senzora/Doğrulama/Tutorial İç Ses")]
    public static void Core()
    {
        var type = Type.GetType("TutorialThoughtClock, Assembly-CSharp");
        Require(type != null, "Tutorial düşüncelerini ilerlemeye göre hatırlatan zamanlayıcı yok.");
        object clock = Activator.CreateInstance(type);
        var method = type.GetMethod("Advance");
        int Tick(string step, int progress, float dt, float delay = 10, bool blocked = false)
            => (int)method.Invoke(clock, new object[] { step, progress, dt, delay, blocked });

        Require(Tick("book", 0, .4f) == 0, "İlk düşünce gösterilmedi.");
        Require(Tick("book", 0, 60, 10, true) == -1, "Başka konuşma/duraklama sırasında hatırlatma çıktı.");
        Require(Tick("book", 0, 9) == -1 && Tick("book", 0, 1) == 1, "İlk hatırlatma konuşma bittikten 10 saniye sonra gelmedi.");
        Require(Tick("book", 1, 0) == -1 && Tick("book", 1, 9) == -1, "Gerçek ilerleme beklemeyi yenilemedi.");
        Require(Tick("book", 1, 1) == 2, "İlerleme ilk düşünceyi tekrar oynattı veya farklı hatırlatmayı atladı.");
        Require(Tick("board", 0, .4f, 30) == 0, "Yeni adım eski hatırlatmayı taşıdı.");
        Require(Tick("board", 0, 29, 30) == -1 && Tick("board", 0, 1, 30) == 1, "Deney adımının uzun beklemesi korunmadı.");
        Require(Tick("board", 0, 14, 30) == -1, "Hatırlatmalar arası süre uzamadı.");
        Require(Tick(null, 0, 200) == -1 && Tick("book", 0, .4f) == 0, "Eğitim kapatılıp açıldığında eski sayaç devam etti.");

        var database = AssetDatabase.LoadAssetAtPath<SubtitleData>("Assets/Scripts/Scriptables/Subtitles/MainSubtitles.asset");
        string[] steps = { "book", "collect", "leave_book", "board", "pin", "clear_board", "build", "store", "leave_board", "lock",
            "hide", "wait_hidden", "leave_hiding", "notice_lees", "avoid_lees", "turing", "insert", "reader", "confirm", "valve_room",
            "valve", "leave_valve", "wait_power", "breaker", "retry_measurement" };
        foreach (string step in steps)
        {
            var variants = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                string id = "thought_" + step + "_" + i;
                Require(database.entries.Count(e => e.id == id) == 1, "Eksik veya yinelenen replik: " + id);
                var entry = database.GetEntry(id);
                Require(entry.segments.Count > 0 && entry.segments.All(s => s.duration >= 4
                    && !string.IsNullOrWhiteSpace(s.textTR) && !string.IsNullOrWhiteSpace(s.textEN) && !string.IsNullOrWhiteSpace(s.textDE)),
                    "Süresi veya çevirisi eksik replik: " + id);
                Require(variants.Add(entry.segments[0].textTR), "Aynı cümleyi tekrar eden hatırlatma: " + id);
            }
        }
        Require(SceneManager.GetActiveScene().path == LaboratorySynthesisSetup.ScenePath, "Laboratuvar araştırma sahnesini aç.");
        TutorialFlowChecks.Core();
        var manager = Object.FindFirstObjectByType<TutorialManager>();
        var actions = manager.tutorialEventsParent.GetComponentsInChildren<DialogAction>(true).Where(d => d.followConditions).ToArray();
        Require(actions.Length == 25, "Konuşmalar sahnedeki 25 event düğümüne bağlı değil.");
        foreach (var action in actions)
        {
            Require(action.contextOwner == manager && action.GetComponent<EventLogicController>() != null
                && action.GetComponents<PrerequisiteCondition>().Length == 2, "Konuşmanın başlangıç/bitiş event koşulları eksik: " + action.name);
            Require(database.GetEntry(action.subtitleID) != null && action.reminderSubtitleIDs.Length == 2
                && action.reminderSubtitleIDs.All(id => database.GetEntry(id) != null), "Konuşma ID veya hatırlatmaları eksik: " + action.name);
        }
        Debug.Log("TUTORIAL VOICE CORE PASS: real clock and 25 steps / 75 distinct TR-EN-DE thoughts.");
    }

    [MenuItem("Senzora/Doğrulama/Tutorial İç Ses Gerçek Oyun")]
    public static void RunPlay()
    {
        Require(Application.isPlaying && TutorialManager.Instance != null, "Laboratuvarda Play gerekli.");
        TutorialManager.Instance.StartCoroutine(Play());
    }

    private static IEnumerator Play()
    {
        yield return TutorialRegressionChecks.Run();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
