using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DoFFocusChecks
{
    const float Frame = 1f / 60f;
    static void Require(bool condition, string message) { if (!condition) throw new Exception("DOF CHECK: " + message); }
    [MenuItem("Senzora/Doğrulama/Odak Filtresi")]
    public static void Run()
    {
        try { FilterChecks(); File.WriteAllText("Temp/dof-focus.result", "PASS " + DateTime.Now.ToString("s")); Debug.Log("DOF FOCUS PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/dof-focus.result", "FAIL " + e); Debug.LogException(e); }
    }
    public static void FilterChecks()
    {
        // Production failure: smoothing in metres made 1m to 90m take 2 frames and 90m to 1m take 1.3 seconds.
        float toFar = SecondsToSettle(1, 90, Frame), toNear = SecondsToSettle(90, 1, Frame);
        Require(Mathf.Abs(toFar - toNear) < .05f, "Near and far focus changes must take the same time: " + toFar + " vs " + toNear);
        Require(toFar > .6f && toFar < 1.3f, "Focus change should match the speed of the eye: " + toFar);
        Require(Mathf.Abs(SecondsToSettle(90, 1, 1f / 30) - SecondsToSettle(90, 1, 1f / 144)) < .1f, "Focus speed depends on frame rate.");

        var filter = Settled(2);
        for (int i = 0; i < 6; i++) filter.Step(90, Frame, false);
        Require(Near(filter.Step(2, Frame, false), 2), "A short glance moved the focus.");

        filter = Settled(8);
        for (int i = 0; i < 300; i++) Require(Near(filter.Step(90, Frame, false), 8), "Far distances must count as one zone.");

        filter = Settled(2);
        for (int i = 0; i < 300; i++) Require(Near(filter.Step(90, Frame, true), 2), "Focus followed a fast camera turn.");

        filter = Settled(1);
        Require(Near(filter.Step(90, .5f, false), 1), "A long frame snapped the focus.");

        filter = new FocusFilter(); filter.Reset(.3f);
        float start = filter.Step(3, Frame, false), resumed = start;
        for (int i = 0; i < 6; i++) resumed = filter.Step(3, Frame, false);
        Require(resumed > start, "Leaving an interaction must release the focus without waiting.");
    }
    static bool Near(float distance, float expected) => Mathf.Abs(distance - expected) < .001f;
    static FocusFilter Settled(float distance)
    {
        var filter = new FocusFilter(); filter.Reset(distance); filter.Step(distance, Frame, false);
        return filter;
    }
    public static float SecondsToSettle(float from, float to, float frame)
    {
        var filter = Settled(from); float seconds = 0, total = Mathf.Abs(1 / to - 1 / from);
        while (seconds < 10)
        {
            seconds += frame;
            if (Mathf.Abs(1 / to - 1 / filter.Step(to, frame, false)) <= total * .1f) break;
        }
        return seconds;
    }
}
