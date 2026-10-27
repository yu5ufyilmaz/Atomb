using System.Collections.Generic;

// Add an experiment's catalog here; graph editing and physical ports share these definitions.
public static class PuzzleParts
{
    public static IReadOnlyList<SignalPartLibrary.Part> ForKind(PuzzleKind kind)
        => kind == PuzzleKind.ColorMixture ? ColorMixtureLibrary.All : SignalPartLibrary.All;
    public static SignalPartLibrary.Part Find(string id)
        => SignalPartLibrary.Find(id) ?? ColorMixtureLibrary.Find(id);
    public static string Source(PuzzleKind kind) => kind == PuzzleKind.ColorMixture ? ColorMixtureLibrary.Source : SignalTrial.Source;
    public static string Meter(PuzzleKind kind) => kind == PuzzleKind.ColorMixture ? ColorMixtureLibrary.Meter : SignalTrial.Meter;
    public static string Splitter(PuzzleKind kind) => kind == PuzzleKind.ColorMixture ? ColorMixtureLibrary.Splitter : SignalTrial.Splitter;
}
