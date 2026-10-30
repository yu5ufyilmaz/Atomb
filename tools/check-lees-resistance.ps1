$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$modelSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/EnemyAI/LeesResistanceModel.cs') -Raw
$checks = @'
public static class LeesResistanceRulesChecks
{
    static int checks;
    static void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static LeesResistanceModel Started(int seed = 1) {
        var m = new LeesResistanceModel(new LeesResistanceModel.Tuning { approachSeconds = 0 });
        m.Begin(seed); m.Step(.01f, 0); return m;
    }
    static LeesResistanceModel Simulate(int fps, int mode, int seed) {
        var m = Started(seed); float dt = 1f / fps;
        for (int i = 0; i < fps * 30 && m.State == LeesResistanceModel.Phase.Struggling; i++) {
            // Human-like correction: cancel the pull, and bring the head toward center.
            float pixels = mode == 0 ? 0 : mode == 1
                ? -(m.PullDirection * m.PullStrength + m.HeadAngle * 2f) / m.Settings.mouseDegreesPerPixel * dt
                : m.PullDirection * 700f * dt;
            m.Step(dt, pixels);
        }
        return m;
    }
    public static string Run() {
        float fastestIdle = float.MaxValue, slowestIdle = 0;
        var m = Started(); Require(m.State == LeesResistanceModel.Phase.Struggling, "Begin must enter the struggle after approach.");
        float elapsed = m.Elapsed; m.Step(0, 999999); Require(m.Elapsed == elapsed && m.HeadAngle == 0, "Paused time must consume no mouse input.");
        for (int seed = 0; seed < 40; seed++) {
            var idle = Simulate(60, 0, seed);
            fastestIdle = Math.Min(fastestIdle, idle.Elapsed); slowestIdle = Math.Max(slowestIdle, idle.Elapsed);
            Require(idle.State == LeesResistanceModel.Phase.Caught, "Idle cannot survive: seed " + seed);
            Require(idle.Elapsed <= 2.25f, "Letting go must lose within 2.25 seconds, not drift safely: seed " + seed + ", actual " + idle.Elapsed);
            Require(Simulate(60, 1, seed).State == LeesResistanceModel.Phase.Survived, "Countering should survive: seed " + seed);
            Require(Simulate(60, 2, seed).State == LeesResistanceModel.Phase.Caught, "Following the pull must lose: seed " + seed);
        }
        foreach (int fps in new[] { 30, 60, 144 }) {
            Require(Simulate(fps, 1, 17).State == LeesResistanceModel.Phase.Survived, "Countering at " + fps + " FPS");
            for (int seed = 0; seed < 20; seed++) {
                var released = Started(seed); float dt = 1f / fps;
                for (int i = 0; i < fps * 4; i++) released.Step(dt, -(released.PullDirection * released.PullStrength + released.HeadAngle * 2) / released.Settings.mouseDegreesPerPixel * dt);
                Require(released.State == LeesResistanceModel.Phase.Struggling, "Active resistance must remain playable.");
                float releaseTime = released.Elapsed;
                while (released.State == LeesResistanceModel.Phase.Struggling && released.Elapsed - releaseTime < 2.25f) released.Step(dt, 0);
                Require(released.State == LeesResistanceModel.Phase.Caught, "Releasing after countering must rapidly lose at " + fps + " FPS, seed " + seed);
                var weak = Started(seed);
                for (int i = 0; i < fps * 4 && weak.State == LeesResistanceModel.Phase.Struggling; i++) weak.Step(dt, -weak.PullDirection * 200 * dt);
                Require(weak.State == LeesResistanceModel.Phase.Caught, "Tiny mouse corrections cannot cancel the struggle at " + fps + " FPS.");
            }
        }
        bool left = false, right = false;
        for (int seed = 0; seed < 20; seed++) {
            var a = Started(seed); var b = Started(seed);
            left |= a.PullDirection < 0; right |= a.PullDirection > 0;
            for(int i = 0; i < 100; i++) { a.Step(.016f, 0); b.Step(.016f, -(b.PullDirection * b.PullStrength + b.HeadAngle * 2) / b.Settings.mouseDegreesPerPixel * .016f); }
            Require(Math.Abs(b.HeadAngle) < Math.Abs(a.HeadAngle), "Opposite input must reduce head turning in both directions.");
        }
        Require(left && right, "Seeds must exercise left and right.");
        var paused = Started(); float before = paused.Elapsed; paused.Step(float.NaN, float.NaN); Require(paused.Elapsed == before, "Invalid time must not poison the model.");
        m.Catch(); float frozen = m.HeadAngle; m.Step(2, 800); Require(m.State == LeesResistanceModel.Phase.Caught && m.HeadAngle == frozen, "Results are terminal.");
        m.Reset(); Require(m.State == LeesResistanceModel.Phase.Ready && m.HeadAngle == 0 && m.Elapsed == 0, "Retry must clear the encounter.");
        m.Begin(4); Require(m.State == LeesResistanceModel.Phase.Struggling, "Zero approach begins immediately.");
        var approach = new LeesResistanceModel(); approach.Begin(2); approach.Step(1, 900);
        Require(approach.State == LeesResistanceModel.Phase.Approaching && approach.HeadAngle == 0, "No head input during approach.");
        var spike = Started(); spike.Step(.016f, 100000); Require(Math.Abs(spike.HeadAngle) < 2, "Mouse warp must be limited.");
        return "LEES RULES PASS: " + checks + " assertions; unattended failure " + fastestIdle.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "-" + slowestIdle.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " seconds";
    }
}
'@
Add-Type -TypeDefinition ($modelSource + [Environment]::NewLine + $checks) -Language CSharp
[LeesResistanceRulesChecks]::Run()
