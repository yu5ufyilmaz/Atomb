using System;
using System.Collections.Generic;

[Serializable]
public sealed class PuzzleBookAssignment
{
    public string bookID;
    public List<BookWorkspacePage> pages = new List<BookWorkspacePage>();
}

[Serializable]
public sealed class PuzzleSessionData
{
    public PuzzleDefinition definition;
    public List<PuzzleDefinition> definitions = new List<PuzzleDefinition>();
    public string sessionID;
    public int distributionSeed;
    public List<PuzzleBookAssignment> books = new List<PuzzleBookAssignment>();
    public List<string> completed = new List<string>();
    public List<SignalExperiment.Reading> measurements = new List<SignalExperiment.Reading>();
}
