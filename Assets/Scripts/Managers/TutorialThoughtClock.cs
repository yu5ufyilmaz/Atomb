// Counts only quiet gameplay time; each real step owns its own reminder sequence.
public sealed class TutorialThoughtClock
{
    private string currentStep;
    private int lastProgress, nextLine, spoken;
    private float remaining;

    public void Postpone(float reminderDelay) { if (spoken > 0) remaining = reminderDelay; }

    public int Advance(string step, int progress, float deltaTime, float reminderDelay, bool blocked)
    {
        if (step != currentStep)
        {
            currentStep = step;
            lastProgress = progress;
            nextLine = spoken = 0;
            remaining = .25f;
        }
        if (string.IsNullOrEmpty(step)) return -1;
        if (progress != lastProgress)
        {
            lastProgress = progress;
            if (spoken > 0) remaining = reminderDelay;
        }
        if (blocked) return -1;
        remaining -= deltaTime;
        if (remaining > .0001f) return -1;
        int line = nextLine;
        nextLine = line == 1 ? 2 : 1;
        spoken++;
        remaining = reminderDelay * (spoken == 1 ? 1 : spoken == 2 ? 1.5f : 3);
        return line;
    }
}
