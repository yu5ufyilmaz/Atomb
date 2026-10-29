using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TutorialRegressionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run()
    {
        var manager = TutorialManager.Instance;
        var lesson = manager.tutorialEventsParent.GetComponent<ChalkboardTutorial>();
        var game = GameManager.Instance;
        manager.tutorialEventsParent.SetActive(false);
        SaveManager.Instance.sessionOnly = true;
        var failures = new List<string>();
        foreach (var check in new Func<ChalkboardTutorial, IEnumerator>[] { CameraOwnership, DialogueReentry })
        {
            var routine = check(lesson);
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception error) { failures.Add(check.Method.Name + ": " + error.GetBaseException().Message); break; }
                if (!next) break;
                yield return routine.Current;
            }
            if (game.activeInteraction != null) game.activeInteraction.ForceExit();
            yield return new WaitForSeconds(1.5f);
        }
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
        Debug.Log("TUTORIAL REGRESSION PASS: intro/focus interaction ownership, book access after focus, scene-condition dialogue reentry, reminders, pause and external speech.");
    }

    private static IEnumerator CameraOwnership(ChalkboardTutorial lesson)
    {
        var game = GameManager.Instance;
        var book = lesson.lessonBook;
        book.Interact();
        Require(game.activeInteraction == null && !book.IsOpening, "The book opened before the opening camera sequence finished.");
        Object.FindFirstObjectByType<InGameMenuController>().InstantSetupForLoad();
        game.StartGameMode();
        var host = new GameObject("Camera ownership regression");
        var focus = host.AddComponent<LookAction>();
        focus.lookTarget = book.transform;
        focus.delay = .3f;
        focus.headTurnDuration = .3f;
        focus.waitUntilInput = false;
        focus.autoReleaseTime = .3f;
        yield return null;
        try
        {
            focus.Execute();
            book.Interact();
            Require(game.activeInteraction == null && !book.IsOpening, "The book opened during the focus action delay.");
            yield return new WaitForSeconds(.4f);
            book.Interact();
            Require(game.activeInteraction == null && !book.IsOpening, "The book opened during camera rotation.");
            yield return new WaitForSeconds(.7f);
            book.Interact();
            Require(game.activeInteraction == book, "The book stayed blocked after focus finished.");
            book.ForceExit();
        }
        finally { Object.Destroy(host); }
        yield return new WaitForSeconds(2);
        Require(game.activeInteraction == null, "The book did not close after the focus action.");
    }

    private static IEnumerator DialogueReentry(ChalkboardTutorial lesson)
    {
        Require(typeof(DialogAction).GetField("followConditions") != null,
            "DialogAction cannot own condition-driven dialogue; tutorial speech still bypasses scene actions.");
        var game = GameManager.Instance;
        Object.FindFirstObjectByType<InGameMenuController>().InstantSetupForLoad();
        game.StartGameMode();
        var subtitles = GlobalSubtitleManager.Instance;
        var original = subtitles.database;
        var database = Object.Instantiate(original);
        foreach (var entry in database.entries) foreach (var segment in entry.segments) segment.duration = .15f;
        database.entries.Add(new SubtitleEntry { id = "regression_external", segments = new List<SubtitleSegment> {
            new SubtitleSegment { duration = .8f, textTR = "Dış konuşma", textEN = "External speech" } } });
        var bookCue = new GameObject("Book cue regression", typeof(BoxCollider), typeof(TriggerCondition), typeof(EventLogicController), typeof(DialogAction));
        var collectCue = new GameObject("Collect cue regression", typeof(BoxCollider), typeof(TriggerCondition), typeof(EventLogicController), typeof(DialogAction));
        try
        {
            subtitles.database = database;
            foreach (var host in new[] { bookCue, collectCue })
            {
                var action = host.GetComponent<DialogAction>();
                string context = host == bookCue ? "book" : "collect";
                action.subtitleID = "thought_" + context + "_0";
                typeof(DialogAction).GetField("followConditions").SetValue(action, true);
                typeof(DialogAction).GetField("reminderSubtitleIDs").SetValue(action, new[] { "thought_" + context + "_1", "thought_" + context + "_2" });
                typeof(DialogAction).GetField("reminderDelay").SetValue(action, .7f);
                host.GetComponent<BoxCollider>().isTrigger = true;
                host.transform.position = Vector3.one * 10000;
            }
            yield return null;
            var player = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
            void Set(GameObject host, bool inside) => typeof(TriggerCondition).GetMethod(inside ? "OnTriggerEnter" : "OnTriggerExit", Private)
                .Invoke(host.GetComponent<TriggerCondition>(), new object[] { player });
            Set(bookCue, true);
            yield return Until(() => subtitles.CurrentID == "thought_book_0", "First book instruction");
            Set(bookCue, false); Set(collectCue, true);
            yield return Until(() => subtitles.CurrentID == "thought_collect_0", "First collection instruction");
            Set(collectCue, false); Set(bookCue, true);
            yield return new WaitForSeconds(.4f);
            Require(subtitles.CurrentID != "thought_book_0", "Closing the book replayed its opening instruction.");
            yield return Until(() => subtitles.CurrentID == "thought_book_1", "Book reminder instead of replayed introduction");
            Set(bookCue, false); Set(collectCue, true);
            yield return new WaitForSeconds(.4f);
            Require(subtitles.CurrentID != "thought_collect_0", "Reopening the book replayed its collection introduction.");
            subtitles.Show("regression_external", required: true);
            Set(collectCue, false);
            yield return null;
            Require(subtitles.CurrentID == "regression_external", "Ending a tutorial context hid someone else's subtitle.");
            game.isGamePaused = true;
            yield return new WaitForSeconds(1);
            Require(subtitles.CurrentID == "regression_external", "Paused dialogue was consumed or replaced.");
            game.isGamePaused = false;
        }
        finally
        {
            game.isGamePaused = false;
            Object.Destroy(bookCue); Object.Destroy(collectCue);
            subtitles.HideSubtitle(); subtitles.database = original;
            Object.Destroy(database);
        }
    }

    private static IEnumerator Until(Func<bool> condition, string label)
    {
        float end = Time.realtimeSinceStartup + 6;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        Require(condition(), label + " did not occur.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
