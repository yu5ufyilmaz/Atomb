using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Only the research scene's copied terminal has a paper reader.
public partial class InteractableTuringMachine
{
    private TuringPaperFeed physicalFeed;
    private TuringCrtInk screenInk;
    private Transform measurementReader, readerPaper;
    private string readerText, readerFeedback, readerStatus, localizedReaderFeedback;
    private object[] readerStatusArguments;
    private float readerSeconds = -1, readerFeedProgress, feedbackStarted;
    private Coroutine measurementRoutine;
    private bool readerNotebookOpen;
    [SerializeField, Range(3, 30)] private float measurementReadSeconds = 8;
    private AudioSource readerMotor, readerTicks;
    private AudioClip readerMotorClip;
    public bool IsReadingMeasurement { get; private set; }
    public bool HasReadMeasurement => readerPaper != null && !IsReadingMeasurement;
    public bool MeasurementRejected => readerFeedback != null;
    private bool IsMeasurementTerminal => SignalExperiment.Active != null
        && SignalExperiment.Active.terminal == this;
    public bool RequiresMeasurementCursor => IsMeasurementTerminal && inMachineMode && !isExiting;
    private bool ReaderPaused => GameManager.Instance != null && GameManager.Instance.isGamePaused;

    private void OpenMeasurementReader()
    {
        if (!IsMeasurementTerminal) return;
        if (physicalFeed == null) BuildMeasurementReader();
        measurementReader.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (NotebookUI.Instance != null)
        {
            NotebookUI.Instance.SetWorkspaceMode(true);
            NotebookWorkspaceController.Instance.ShowSolutions();
            readerNotebookOpen = true;
        }
    }

    private void BuildMeasurementReader()
    {
        Transform body = null;
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
            if (filter.name == "turing-machine.1") { body = filter.transform; break; }
        if (body == null) throw new System.InvalidOperationException("Turing test kopyasının gövdesi bulunamadı.");
        // Measured from the real keyboard bed triangles, not from the viewing camera.
        Vector3 localMouth = new Vector3(.000893095683f, -.010233393055f, .006396707613f);
        Vector3 feed = body.TransformVector(new Vector3(.803639316889f, 0, .595116667847f)).normalized;
        Vector3 normal = body.TransformVector(new Vector3(-.595116667847f, 0, .803639316889f)).normalized;
        Vector3 right = Vector3.Cross(feed, normal).normalized;
        Vector3 mouth = body.TransformPoint(localMouth) + normal * .006f;
        physicalFeed = TuringPaperFeed.Create(transform, mouth, right, feed, normal);
        measurementReader = physicalFeed.Root;
        screenInk = TuringCrtInk.Create(transform);
        SetReaderDisplay("OLCUM KARTI BEKLE");
    }

    public bool TryInsertMeasurement(NotebookBlockData note, Vector2 screenPosition)
    {
        if (!IsMeasurementTerminal || !inMachineMode || isExiting || ReaderPaused
            || physicalFeed == null || !physicalFeed.ContainsPointer(screenPosition, Camera.main)) return false;
        if (IsReadingMeasurement) return false;
        if (!SignalExperiment.Active.TryGetMeasurementCode(note, out string code))
        { SetReaderDisplay("OLCUM KARTI GEREK"); return false; }
        string[] fields = code.Split('_');
        if (fields.Length != 3 || fields[0].Length != wordWheelIndices.Length
            || fields[2].Length != numberWheelIndices.Length || System.Array.IndexOf(symbolChars, fields[1]) < 0)
            return false;
        foreach (char letter in fields[0]) if (wordChars.IndexOf(letter) < 0) return false;
        foreach (char digit in fields[2]) if (numberChars.IndexOf(digit) < 0) return false;
        physicalFeed.Begin(note);
        readerPaper = physicalFeed.Paper;
        readerFeedback = null;
        SignalExperiment.Active.TerminalMessage = "Ölçüm okunuyor. Kod yerleşince Enter ile sına.";
        UpdateIndicators(0);
        IsReadingMeasurement = true;
        measurementRoutine = StartCoroutine(ReadMeasurement(fields));
        return true;
    }

    private IEnumerator ReadMeasurement(string[] fields)
    {
        readerFeedProgress = 0;
        while (readerFeedProgress < 1)
        {
            if (!ReaderPaused)
            {
                readerFeedProgress = Mathf.Min(1, readerFeedProgress + Time.deltaTime / 1.15f);
                physicalFeed.SetFeed(Mathf.SmoothStep(0, 1, readerFeedProgress));
                SetReaderDisplay("KART ALINIYOR", measurementReadSeconds + 1.15f * (1 - readerFeedProgress));
            }
            yield return null;
        }
        yield return DriveMeasurement(fields);
        yield return ReaderWait(.25f);
        SetReaderDisplay("ENTER: SINA", 0);
        SignalExperiment.Active.TerminalMessage = "Ölçüm okundu. Enter ile sına.";
        measurementRoutine = null;
        IsReadingMeasurement = false;
    }

    private static string FormatReaderTime(float seconds)
    {
        int whole = Mathf.CeilToInt(Mathf.Max(0, seconds));
        return (whole / 60).ToString("D2") + ":" + (whole % 60).ToString("D2");
    }

    private void SetReaderDisplay(string canonicalStatus, float seconds = -1, params object[] arguments)
    {
        readerSeconds = seconds;
        readerStatus = canonicalStatus;
        readerStatusArguments = arguments;
        RefreshReaderLanguage();
    }

    private void RefreshReaderLanguage()
    {
        if (screenInk == null || readerStatus == null) return;
        localizedReaderFeedback = readerFeedback == null ? null
            : GameLocalization.Format("{0}    {1}    ", "KAYIT UYUSMUYOR", readerFeedback).Replace('\n', ' ');
        string status = GameLocalization.Format(readerStatus, readerStatusArguments);
        readerText = "> " + (readerSeconds >= 0 ? FormatReaderTime(readerSeconds) + " " : "") + status;
        screenInk.SetReadout(readerText);
    }

    private void ShowMeasurementResult(bool accepted, string message)
    {
        if (screenInk == null) return;
        readerFeedback = accepted ? null : message;
        feedbackStarted = Time.time;
        SetReaderDisplay(accepted ? "KAYIT ONAYLANDI" : "KAYIT UYUSMUYOR");
    }

    private void LateUpdate()
    {
        if (readerFeedback == null || IsReadingMeasurement || ReaderPaused || Time.time - feedbackStarted < 2) return;
        string feedback = localizedReaderFeedback;
        int start = Mathf.FloorToInt((Time.time - feedbackStarted - 2) * 4) % feedback.Length;
        string loop = feedback + feedback;
        screenInk.SetReadout("> " + loop.Substring(start, Mathf.Min(18, feedback.Length)));
    }

    private struct ReaderDrive
    {
        public Transform wheel, lever;
        public Quaternion rest, leverRest;
        public float start, travel, step, leverSign, begin, end;
        public int group, index, size, target;
        public bool latched;
    }

    private ReaderDrive CreateReaderDrive(int slot, int total, string[] fields)
    {
        var drive = new ReaderDrive { leverSign = 1, begin = slot * .025f,
            end = Mathf.Max(3, measurementReadSeconds) * Mathf.Lerp(.72f, 1, (float)slot / (total - 1)) };
        int start;
        if (slot < wordWheelModels.Length)
        {
            int i = slot; drive.group = 0; drive.index = i;
            drive.wheel = wordWheelModels[i]; drive.rest = wordWheelInitialRots[i];
            if (i < wordLeverModels.Length) { drive.lever = wordLeverModels[i]; drive.leverRest = wordLeverInitialRots[i]; }
            drive.leverSign = invertWordLeverRotation ? -1 : 1;
            drive.size = wordChars.Length; start = wordWheelIndices[i]; drive.target = wordChars.IndexOf(fields[0][i]);
        }
        else if (slot == wordWheelModels.Length)
        {
            drive.group = 1; drive.wheel = symbolWheelModel; drive.rest = symbolWheelInitialRot;
            drive.lever = symbolLeverModel; drive.leverRest = symbolLeverInitialRot;
            drive.size = symbolChars.Length; start = symbolVisualStep;
            int symbol = (System.Array.IndexOf(symbolChars, fields[1]) - symbolVisualOffset + drive.size) % drive.size;
            drive.target = isSymbolOrderReversed ? (drive.size - symbol) % drive.size : symbol;
        }
        else
        {
            int i = slot - wordWheelModels.Length - 1; drive.group = 2; drive.index = i;
            drive.wheel = numberWheelModels[i]; drive.rest = numberWheelInitialRots[i];
            if (i < numberLeverModels.Length) { drive.lever = numberLeverModels[i]; drive.leverRest = numberLeverInitialRots[i]; }
            drive.size = numberChars.Length; start = numberWheelIndices[i]; drive.target = numberChars.IndexOf(fields[2][i]);
        }
        drive.step = 360f / drive.size; drive.start = start * drive.step;
        float direction = drive.group == 1 ? -1 : 1;
        // Unwrapped travel gives real revolutions; quaternion interpolation alone takes a short cut.
        drive.travel = direction * (360 * (3 + slot % 3)
            + Mathf.Repeat(direction * (drive.target - start) * drive.step, 360));
        return drive;
    }

    private IEnumerator DriveMeasurement(string[] fields)
    {
        int total = wordWheelModels.Length + 1 + numberWheelModels.Length;
        var drives = new List<ReaderDrive>(total);
        for (int i = 0; i < total; i++) drives.Add(CreateReaderDrive(i, total, fields));
        EnsureReaderAudio();
        readerMotor.Play();
        float elapsed = 0, nextTick = 0;
        int latched = 0, tick = 0;
        bool wasPaused = false;
        while (latched < total)
        {
            if (ReaderPaused)
            {
                if (!wasPaused) { readerMotor.Pause(); readerTicks.Pause(); wasPaused = true; }
                yield return null;
                continue;
            }
            if (wasPaused) { readerMotor.UnPause(); readerTicks.UnPause(); wasPaused = false; }
            elapsed += Time.deltaTime;
            float duration = Mathf.Max(3, measurementReadSeconds);
            float motorLoad = Mathf.Min(Mathf.Clamp01(elapsed / .8f), Mathf.Clamp01((duration - elapsed) / 1.2f));
            readerMotor.pitch = Mathf.Lerp(.55f, 1.08f, motorLoad);
            readerMotor.volume = Mathf.Lerp(.025f, .10f, motorLoad);
            if (elapsed >= nextTick)
            {
                readerTicks.pitch = .86f + tick++ % 3 * .07f;
                if (wheelClickSound != null) readerTicks.PlayOneShot(wheelClickSound, .16f);
                nextTick = elapsed + Mathf.Lerp(.17f, .075f, motorLoad);
            }
            for (int i = 0; i < total; i++)
            {
                var drive = drives[i];
                if (drive.latched || elapsed < drive.begin) continue;
                float t = Mathf.Clamp01((elapsed - drive.begin) / (drive.end - drive.begin));
                float overshoot = drive.step * .16f * Mathf.Sign(drive.travel);
                float angle;
                if (t < .94f) angle = drive.start + (drive.travel + overshoot) * MotorTravel(t / .94f);
                else
                {
                    float settle = (t - .94f) / .06f;
                    angle = drive.start + drive.travel + overshoot * (1 - Mathf.SmoothStep(0, 1, settle)) * Mathf.Cos(settle * Mathf.PI * 2);
                }
                ApplyReaderDrive(drive, angle);
                if (t >= 1)
                {
                    drive.latched = true; latched++;
                    currentGroup = drive.group; currentWordIndex = drive.group == 0 ? drive.index : currentWordIndex;
                    currentNumberIndex = drive.group == 2 ? drive.index : currentNumberIndex;
                    if (inMachineMode && !isExiting) UpdateActiveWheelHighlight();
                    if (wheelClickSound != null) readerTicks.PlayOneShot(wheelClickSound, .45f);
                    drives[i] = drive;
                }
            }
            if (latched == 0) SetReaderDisplay("OKUNUYOR", duration - elapsed);
            else SetReaderDisplay("KILIT {0}/{1}", duration - elapsed, latched, total);
            yield return null;
        }
        readerMotor.Stop();
    }

    private static float MotorTravel(float t)
    {
        // Integrated speed ramp: accelerate, run under load, then brake into a detent.
        const float acceleration = .18f, braking = .24f, area = .79f;
        if (t < acceleration) return t * t / (2 * acceleration * area);
        if (t < 1 - braking) return (t - acceleration * .5f) / area;
        return 1 - (1 - t) * (1 - t) / (2 * braking * area);
    }

    private void ApplyReaderDrive(ReaderDrive drive, float angle)
    {
        Quaternion rotation = drive.rest * Quaternion.AngleAxis(angle, rotationAxis);
        Quaternion lever = drive.leverRest * Quaternion.AngleAxis(angle * drive.leverSign, leverRotationAxis);
        if (drive.wheel != null) drive.wheel.localRotation = rotation;
        if (drive.lever != null) drive.lever.localRotation = lever;
        int index = (Mathf.RoundToInt(angle / drive.step) % drive.size + drive.size) % drive.size;
        if (drive.group == 0)
        { wordWheelIndices[drive.index] = index; wordWheelTargets[drive.index] = rotation; wordLeverTargets[drive.index] = lever; }
        else if (drive.group == 1)
        { symbolVisualStep = index; symbolWheelTarget = rotation; symbolLeverTarget = lever; }
        else
        { numberWheelIndices[drive.index] = index; numberWheelTargets[drive.index] = rotation; numberLeverTargets[drive.index] = lever; }
    }

    private void EnsureReaderAudio()
    {
        if (readerMotor != null) return;
        AudioSource Source(string name)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            var source = obj.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 1; source.minDistance = .7f; source.maxDistance = 12; source.dopplerLevel = 0;
            if (audioSource != null) source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            if(source.outputAudioMixerGroup==null)source.outputAudioMixerGroup=SettingsManager.EnsureInstance().effectsGroup;
            return source;
        }
        readerMotor = Source("Okuyucu Motoru"); readerTicks = Source("Dişli ve Kilit Sesleri");
        const int rate = 22050;
        var samples = new float[rate / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            samples[i] = .45f * Mathf.Sin(2 * Mathf.PI * 72 * t) + .18f * Mathf.Sin(2 * Mathf.PI * 144 * t)
                + .07f * Mathf.Sin(2 * Mathf.PI * 360 * t) * (1 + .3f * Mathf.Sin(2 * Mathf.PI * 24 * t));
        }
        readerMotorClip = AudioClip.Create("Deneme okuyucusu motor yatağı", samples.Length, 1, rate, false);
        readerMotorClip.SetData(samples, 0); readerMotor.clip = readerMotorClip; readerMotor.loop = true;
    }

    private IEnumerator ReaderWait(float seconds)
    {
        while (seconds > 0)
        {
            if (!ReaderPaused) seconds -= Time.deltaTime;
            yield return null;
        }
    }

    private void CloseMeasurementReader(bool cancel = false)
    {
        // Leaving the view closes the notebook; the physical machine keeps its job.
        if (cancel)
        {
            if (measurementRoutine != null) StopCoroutine(measurementRoutine);
            measurementRoutine = null;
            IsReadingMeasurement = false;
            if (readerMotor != null) readerMotor.Stop();
            if (readerTicks != null) readerTicks.Stop();
            if (measurementReader != null) measurementReader.gameObject.SetActive(false);
        }
        if (readerNotebookOpen && NotebookUI.Instance != null) NotebookUI.Instance.SetWorkspaceMode(false);
        readerNotebookOpen = false;
    }

    private void OnEnable() => GameLocalization.LanguageChanged += RefreshReaderLanguage;

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshReaderLanguage;
        CloseMeasurementReader(true);
    }

    public void ResetMeasurementSession()
    {
        CloseMeasurementReader(true);
        physicalFeed?.Dispose(); physicalFeed = null;
        screenInk?.Dispose(); screenInk = null;
        measurementReader = readerPaper = null;
        readerSeconds = -1; readerText = readerFeedback = readerStatus = localizedReaderFeedback = null;
        readerStatusArguments = null;
    }

    private void OnDestroy()
    {
        CloseMeasurementReader(true);
        physicalFeed?.Dispose();
        screenInk?.Dispose();
        if (readerMotorClip != null) Destroy(readerMotorClip);
    }
}
