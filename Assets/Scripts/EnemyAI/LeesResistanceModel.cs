using System;

/// <summary>Standalone encounter rules. Mouse input is horizontal pixels, not pixels per second.</summary>
public sealed class LeesResistanceModel
{
    public enum Phase { Ready, Approaching, Struggling, Survived, Caught }
    [Serializable]
    public sealed class Tuning
    {
        public float approachSeconds = 2.8f;
        public float survivalSeconds = 16f;
        public float pullDegreesPerSecond = 54f;
        public float mouseDegreesPerPixel = .075f;
        public float failureAngle = 52f;
        public float directionWarningSeconds = .25f;
        public float strainRate = 1f;
    }
    public Phase State { get; private set; }
    public float HeadAngle { get; private set; }
    public float Strain { get; private set; }
    public float Elapsed { get; private set; }
    public int PullDirection { get; private set; }
    public float PullStrength { get; private set; }
    public float DirectionAge { get; private set; }
    public float Progress { get { return Clamp(Elapsed / Math.Max(1, Settings.survivalSeconds), 0, 1); } }
    public float Danger { get { return Math.Max(Strain, Math.Abs(HeadAngle) / Math.Max(10, Settings.failureAngle)); } }
    public Tuning Settings { get; private set; }
    Random random;
    float directionDuration;
    public LeesResistanceModel(Tuning settings = null) { Settings = settings ?? new Tuning(); }
    public void Begin(int seed)
    {
        Reset(); random = new Random(seed); Strain = .12f;
        PullDirection = random.Next(2) == 0 ? -1 : 1;
        directionDuration = NextDuration();
        State = Settings.approachSeconds > 0 ? Phase.Approaching : Phase.Struggling;
    }
    public void Step(float seconds, float mousePixels)
    {
        if (!Finite(seconds) || seconds <= 0) return;
        if (State == Phase.Approaching)
        {
            Elapsed += seconds;
            if (Elapsed >= Settings.approachSeconds) { State = Phase.Struggling; Elapsed = 0; }
            return; // Discard input from the frame in which the head is first grabbed.
        }
        if (State != Phase.Struggling) return;
        Elapsed += seconds; DirectionAge += seconds;
        if (DirectionAge >= directionDuration)
        {
            PullDirection = -PullDirection;
            DirectionAge = 0; directionDuration = NextDuration();
        }
        float ramp = Clamp((DirectionAge - Math.Max(.1f, Settings.directionWarningSeconds)) / .14f, 0, 1);
        float pulse = 1.025f + .175f * (float)Math.Sin(DirectionAge * 7.5f);
        PullStrength = Math.Max(1, Settings.pullDegreesPerSecond) * ramp * pulse * (1 + Progress * .5f);
        float pixels = Finite(mousePixels) ? Clamp(mousePixels, -1400 * seconds, 1400 * seconds) : 0;
        HeadAngle += PullDirection * PullStrength * seconds + pixels * Math.Max(.005f, Settings.mouseDegreesPerPixel);
        float opposing = Math.Max(0, -pixels * PullDirection);
        // A light twitch no longer clears a large amount of accumulated pressure.
        float effort = Clamp(opposing / (430 * seconds), 0, 1);
        Strain = Clamp(Strain + seconds * (Settings.strainRate * (.48f * (1 - effort)
            + .2f * Math.Abs(HeadAngle) / Math.Max(10, Settings.failureAngle)) - .24f * effort), .08f, 1);
        if (Danger >= 1) State = Phase.Caught;
        else if (Progress >= 1) State = Phase.Survived;
    }
    public void Catch() { if (State == Phase.Approaching || State == Phase.Struggling) State = Phase.Caught; }
    public void Reset()
    {
        State = Phase.Ready; HeadAngle = Strain = Elapsed = PullStrength = DirectionAge = 0; PullDirection = 0;
    }
    float NextDuration() { return 1.8f + (float)random.NextDouble() * .8f; }
    static float Clamp(float value, float min, float max) { return Math.Min(max, Math.Max(min, value)); }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
}
