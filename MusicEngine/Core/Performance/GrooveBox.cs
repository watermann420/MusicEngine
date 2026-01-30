//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: All-in-one groovebox instrument (like Roland MC-101/707) with pads, step sequencer, and performance features.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAudio.Wave;

namespace MusicEngine.Core.Performance;

#region Enumerations

/// <summary>
/// Pad playback mode
/// </summary>
public enum PadMode
{
    /// <summary>Play once from start to end</summary>
    OneShot,
    /// <summary>Loop continuously while held or until stopped</summary>
    Loop,
    /// <summary>Play only while pad is held</summary>
    Gate
}

/// <summary>
/// Pattern playback direction
/// </summary>
public enum PatternDirection
{
    /// <summary>Play steps forward</summary>
    Forward,
    /// <summary>Play steps backward</summary>
    Backward,
    /// <summary>Play forward then backward</summary>
    PingPong,
    /// <summary>Play steps randomly</summary>
    Random
}

/// <summary>
/// Fill generation type
/// </summary>
public enum FillType
{
    /// <summary>No fill</summary>
    None,
    /// <summary>Simple fill on last bar</summary>
    Simple,
    /// <summary>Build-up fill with increasing density</summary>
    BuildUp,
    /// <summary>Breakdown with fewer hits</summary>
    Breakdown,
    /// <summary>Roll/ratchet fill</summary>
    Roll,
    /// <summary>Random variation fill</summary>
    Random
}

/// <summary>
/// Stutter/roll effect division
/// </summary>
public enum StutterDivision
{
    /// <summary>Eighth notes</summary>
    Eighth = 2,
    /// <summary>Sixteenth notes</summary>
    Sixteenth = 4,
    /// <summary>Thirty-second notes</summary>
    ThirtySecond = 8,
    /// <summary>Sixty-fourth notes</summary>
    SixtyFourth = 16
}

/// <summary>
/// MIDI sync mode
/// </summary>
public enum SyncMode
{
    /// <summary>Internal clock</summary>
    Internal,
    /// <summary>External MIDI clock slave</summary>
    External,
    /// <summary>Internal clock with MIDI clock output</summary>
    InternalWithOutput
}

#endregion

#region Data Classes

/// <summary>
/// Per-step parameter lock (automation snapshot)
/// </summary>
public class ParameterLock
{
    /// <summary>Parameter name</summary>
    public string ParameterName { get; set; } = "";

    /// <summary>Target value for this step</summary>
    public float Value { get; set; }

    /// <summary>Whether this lock is active</summary>
    public bool Active { get; set; } = true;

    public ParameterLock() { }

    public ParameterLock(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }

    public ParameterLock Clone() => new()
    {
        ParameterName = ParameterName,
        Value = Value,
        Active = Active
    };
}

/// <summary>
/// Motion recording point (knob movement over time)
/// </summary>
public class MotionPoint
{
    /// <summary>Beat position relative to pattern start</summary>
    public double Beat { get; set; }

    /// <summary>Parameter value at this point</summary>
    public float Value { get; set; }

    public MotionPoint() { }

    public MotionPoint(double beat, float value)
    {
        Beat = beat;
        Value = value;
    }
}

/// <summary>
/// Motion recording for a parameter
/// </summary>
public class MotionRecording
{
    /// <summary>Parameter name being recorded</summary>
    public string ParameterName { get; set; } = "";

    /// <summary>Recorded motion points</summary>
    public List<MotionPoint> Points { get; set; } = new();

    /// <summary>Whether playback is enabled</summary>
    public bool PlaybackEnabled { get; set; } = true;

    /// <summary>Get interpolated value at beat position</summary>
    public float GetValueAtBeat(double beat, float defaultValue)
    {
        if (!PlaybackEnabled || Points.Count == 0)
            return defaultValue;

        if (Points.Count == 1)
            return Points[0].Value;

        // Find surrounding points
        MotionPoint? before = null;
        MotionPoint? after = null;

        for (int i = 0; i < Points.Count; i++)
        {
            if (Points[i].Beat <= beat)
                before = Points[i];
            if (Points[i].Beat >= beat && after == null)
                after = Points[i];
        }

        if (before == null)
            return Points[0].Value;
        if (after == null)
            return Points[^1].Value;
        if (before == after)
            return before.Value;

        // Linear interpolation
        double t = (beat - before.Beat) / (after.Beat - before.Beat);
        return before.Value + (float)t * (after.Value - before.Value);
    }

    public MotionRecording Clone()
    {
        var clone = new MotionRecording
        {
            ParameterName = ParameterName,
            PlaybackEnabled = PlaybackEnabled
        };
        clone.Points.AddRange(Points.Select(p => new MotionPoint(p.Beat, p.Value)));
        return clone;
    }
}

/// <summary>
/// A single step in the groovebox sequencer
/// </summary>
public class GrooveStep
{
    /// <summary>Step is active (will trigger)</summary>
    public bool Active { get; set; }

    /// <summary>Velocity (0-127)</summary>
    public int Velocity { get; set; } = 100;

    /// <summary>Gate length as percentage of step (0-1)</summary>
    public float Gate { get; set; } = 0.5f;

    /// <summary>Trigger probability (0-1)</summary>
    public float Probability { get; set; } = 1.0f;

    /// <summary>Note offset in semitones from pad note</summary>
    public int NoteOffset { get; set; }

    /// <summary>Micro-timing offset in beats (-0.5 to 0.5)</summary>
    public double MicroTiming { get; set; }

    /// <summary>Ratchet/retrigger count within step (1 = normal)</summary>
    public int Ratchet { get; set; } = 1;

    /// <summary>Per-step parameter locks</summary>
    public List<ParameterLock> ParameterLocks { get; set; } = new();

    /// <summary>Condition for triggering</summary>
    public StepCondition Condition { get; set; } = StepCondition.Always;

    /// <summary>Condition parameter value</summary>
    public int ConditionParam { get; set; } = 2;

    /// <summary>Fill-only step (only plays during fill mode)</summary>
    public bool FillOnly { get; set; }

    public GrooveStep() { }

    public GrooveStep(bool active, int velocity = 100, float gate = 0.5f, float probability = 1.0f)
    {
        Active = active;
        Velocity = velocity;
        Gate = gate;
        Probability = probability;
    }

    public GrooveStep Clone() => new()
    {
        Active = Active,
        Velocity = Velocity,
        Gate = Gate,
        Probability = Probability,
        NoteOffset = NoteOffset,
        MicroTiming = MicroTiming,
        Ratchet = Ratchet,
        ParameterLocks = ParameterLocks.Select(p => p.Clone()).ToList(),
        Condition = Condition,
        ConditionParam = ConditionParam,
        FillOnly = FillOnly
    };
}

/// <summary>
/// Represents a pad with sample assignment and parameters
/// </summary>
public class GroovePad
{
    /// <summary>Pad index (0-15)</summary>
    public int Index { get; set; }

    /// <summary>Display name</summary>
    public string Name { get; set; } = "";

    /// <summary>MIDI note number</summary>
    public int Note { get; set; } = 36;

    /// <summary>Sample data (mono or interleaved stereo)</summary>
    public float[]? SampleData { get; set; }

    /// <summary>Sample rate of loaded sample</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of channels in sample</summary>
    public int Channels { get; set; } = 1;

    /// <summary>Playback mode</summary>
    public PadMode Mode { get; set; } = PadMode.OneShot;

    /// <summary>Pitch offset in semitones</summary>
    public float Pitch { get; set; }

    /// <summary>Volume (0-1)</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>Pan (-1 to 1)</summary>
    public float Pan { get; set; }

    /// <summary>Filter cutoff frequency (20-20000 Hz)</summary>
    public float FilterCutoff { get; set; } = 20000f;

    /// <summary>Filter resonance (0-1)</summary>
    public float FilterResonance { get; set; }

    /// <summary>Decay/release time in seconds</summary>
    public float Decay { get; set; } = 0.5f;

    /// <summary>Attack time in seconds</summary>
    public float Attack { get; set; }

    /// <summary>Sample start offset (0-1)</summary>
    public float StartOffset { get; set; }

    /// <summary>Sample end offset (0-1, 0 = full length)</summary>
    public float EndOffset { get; set; }

    /// <summary>Loop start position (0-1)</summary>
    public float LoopStart { get; set; }

    /// <summary>Loop end position (0-1, 0 = end of sample)</summary>
    public float LoopEnd { get; set; }

    /// <summary>Choke group (0 = none, 1-8 = group number)</summary>
    public int ChokeGroup { get; set; }

    /// <summary>Mute group (0 = none, 1-8 = group number)</summary>
    public int MuteGroup { get; set; }

    /// <summary>Pad is muted</summary>
    public bool Muted { get; set; }

    /// <summary>Pad is soloed</summary>
    public bool Soloed { get; set; }

    /// <summary>Delay send level (0-1)</summary>
    public float DelaySend { get; set; }

    /// <summary>Reverb send level (0-1)</summary>
    public float ReverbSend { get; set; }

    /// <summary>Reverse playback</summary>
    public bool Reverse { get; set; }

    /// <summary>Steps for this pad (in the pattern)</summary>
    public GrooveStep[] Steps { get; set; } = Array.Empty<GrooveStep>();

    /// <summary>Motion recordings for this pad</summary>
    public List<MotionRecording> MotionRecordings { get; set; } = new();

    public GroovePad()
    {
    }

    public GroovePad(int index, string name, int note)
    {
        Index = index;
        Name = name;
        Note = note;
    }

    /// <summary>
    /// Initialize steps array
    /// </summary>
    public void InitializeSteps(int count)
    {
        Steps = new GrooveStep[count];
        for (int i = 0; i < count; i++)
        {
            Steps[i] = new GrooveStep();
        }
    }

    /// <summary>
    /// Clone this pad with all settings
    /// </summary>
    public GroovePad Clone()
    {
        var clone = new GroovePad
        {
            Index = Index,
            Name = Name,
            Note = Note,
            SampleData = SampleData != null ? (float[])SampleData.Clone() : null,
            SampleRate = SampleRate,
            Channels = Channels,
            Mode = Mode,
            Pitch = Pitch,
            Volume = Volume,
            Pan = Pan,
            FilterCutoff = FilterCutoff,
            FilterResonance = FilterResonance,
            Decay = Decay,
            Attack = Attack,
            StartOffset = StartOffset,
            EndOffset = EndOffset,
            LoopStart = LoopStart,
            LoopEnd = LoopEnd,
            ChokeGroup = ChokeGroup,
            MuteGroup = MuteGroup,
            Muted = Muted,
            Soloed = Soloed,
            DelaySend = DelaySend,
            ReverbSend = ReverbSend,
            Reverse = Reverse
        };

        clone.Steps = Steps.Select(s => s.Clone()).ToArray();
        clone.MotionRecordings = MotionRecordings.Select(m => m.Clone()).ToList();

        return clone;
    }
}

/// <summary>
/// Pattern containing step data for all pads
/// </summary>
public class GroovePattern
{
    /// <summary>Pattern index (0-63)</summary>
    public int Index { get; set; }

    /// <summary>Pattern name</summary>
    public string Name { get; set; } = "";

    /// <summary>Number of steps (16, 32, 48, or 64)</summary>
    public int StepCount { get; set; } = 16;

    /// <summary>Length in bars</summary>
    public int LengthBars { get; set; } = 1;

    /// <summary>Step length in beats (0.25 = 16th)</summary>
    public double StepLength { get; set; } = 0.25;

    /// <summary>Swing amount (0-1)</summary>
    public float Swing { get; set; }

    /// <summary>Playback direction</summary>
    public PatternDirection Direction { get; set; } = PatternDirection.Forward;

    /// <summary>Pattern tempo (0 = use global)</summary>
    public double Tempo { get; set; }

    /// <summary>Time signature numerator</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Per-pad step data (copied from pads when saved)</summary>
    [JsonIgnore]
    public Dictionary<int, GrooveStep[]> PadSteps { get; set; } = new();

    /// <summary>Scene variation data (A/B)</summary>
    public GrooveSceneVariation VariationA { get; set; } = new();

    /// <summary>Scene variation data (A/B)</summary>
    public GrooveSceneVariation VariationB { get; set; } = new();

    /// <summary>Active variation (true = A, false = B)</summary>
    public bool ActiveVariationIsA { get; set; } = true;

    public GroovePattern()
    {
    }

    public GroovePattern(int index, string name, int stepCount = 16)
    {
        Index = index;
        Name = name;
        StepCount = stepCount;
        LengthBars = stepCount / 16;
        if (LengthBars < 1) LengthBars = 1;
    }

    /// <summary>
    /// Clone this pattern
    /// </summary>
    public GroovePattern Clone()
    {
        var clone = new GroovePattern
        {
            Index = Index,
            Name = Name + " (Copy)",
            StepCount = StepCount,
            LengthBars = LengthBars,
            StepLength = StepLength,
            Swing = Swing,
            Direction = Direction,
            Tempo = Tempo,
            TimeSignatureNumerator = TimeSignatureNumerator,
            TimeSignatureDenominator = TimeSignatureDenominator,
            ActiveVariationIsA = ActiveVariationIsA,
            VariationA = VariationA.Clone(),
            VariationB = VariationB.Clone()
        };

        foreach (var kvp in PadSteps)
        {
            clone.PadSteps[kvp.Key] = kvp.Value.Select(s => s.Clone()).ToArray();
        }

        return clone;
    }
}

/// <summary>
/// Scene variation (A/B) for a pattern
/// </summary>
public class GrooveSceneVariation
{
    /// <summary>Variation name</summary>
    public string Name { get; set; } = "";

    /// <summary>Per-pad mute states</summary>
    public Dictionary<int, bool> PadMutes { get; set; } = new();

    /// <summary>Per-pad volume adjustments</summary>
    public Dictionary<int, float> PadVolumeOffsets { get; set; } = new();

    /// <summary>Per-pad filter adjustments</summary>
    public Dictionary<int, float> PadFilterOffsets { get; set; } = new();

    /// <summary>Global filter cutoff offset</summary>
    public float GlobalFilterOffset { get; set; }

    /// <summary>Global volume offset</summary>
    public float GlobalVolumeOffset { get; set; }

    public GrooveSceneVariation Clone() => new()
    {
        Name = Name,
        PadMutes = new Dictionary<int, bool>(PadMutes),
        PadVolumeOffsets = new Dictionary<int, float>(PadVolumeOffsets),
        PadFilterOffsets = new Dictionary<int, float>(PadFilterOffsets),
        GlobalFilterOffset = GlobalFilterOffset,
        GlobalVolumeOffset = GlobalVolumeOffset
    };
}

/// <summary>
/// Pattern chain entry for song mode
/// </summary>
public class PatternChainEntry
{
    /// <summary>Pattern index to play</summary>
    public int PatternIndex { get; set; }

    /// <summary>Number of times to repeat</summary>
    public int Repeats { get; set; } = 1;

    /// <summary>Variation to use (true = A, false = B)</summary>
    public bool UseVariationA { get; set; } = true;

    /// <summary>Tempo override (0 = use pattern tempo)</summary>
    public double TempoOverride { get; set; }

    public PatternChainEntry() { }

    public PatternChainEntry(int patternIndex, int repeats = 1, bool useVariationA = true)
    {
        PatternIndex = patternIndex;
        Repeats = repeats;
        UseVariationA = useVariationA;
    }
}

/// <summary>
/// Voice for sample playback
/// </summary>
internal class GrooveVoice
{
    public int PadIndex { get; set; } = -1;
    public int Note { get; set; }
    public int Velocity { get; set; }
    public double Position { get; set; }
    public double PitchRatio { get; set; } = 1.0;
    public float EnvelopeLevel { get; set; } = 1.0f;
    public float EnvelopePhase { get; set; } // 0 = attack, 1 = sustain/decay
    public bool IsActive { get; set; }
    public bool IsReleasing { get; set; }
    public double ReleaseStartTime { get; set; }

    // Simple one-pole lowpass filter state
    public float FilterState { get; set; }

    public void Reset()
    {
        PadIndex = -1;
        Note = 0;
        Velocity = 0;
        Position = 0;
        PitchRatio = 1.0;
        EnvelopeLevel = 0;
        EnvelopePhase = 0;
        IsActive = false;
        IsReleasing = false;
        FilterState = 0;
    }
}

#endregion

#region Events

/// <summary>
/// Event arguments for pad trigger
/// </summary>
public class PadTriggerEventArgs : EventArgs
{
    /// <summary>Pad index</summary>
    public int PadIndex { get; init; }

    /// <summary>MIDI note</summary>
    public int Note { get; init; }

    /// <summary>Velocity</summary>
    public int Velocity { get; init; }

    /// <summary>Step index that triggered this (or -1 for manual)</summary>
    public int StepIndex { get; init; } = -1;
}

/// <summary>
/// Event arguments for step change
/// </summary>
public class StepChangedEventArgs : EventArgs
{
    /// <summary>Current step index</summary>
    public int StepIndex { get; init; }

    /// <summary>Current beat position</summary>
    public double Beat { get; init; }

    /// <summary>Whether this is a bar boundary</summary>
    public bool IsBarStart { get; init; }
}

/// <summary>
/// Event arguments for pattern change
/// </summary>
public class PatternChangedEventArgs : EventArgs
{
    /// <summary>Previous pattern index</summary>
    public int PreviousPattern { get; init; }

    /// <summary>New pattern index</summary>
    public int NewPattern { get; init; }

    /// <summary>Whether this was triggered by song mode</summary>
    public bool FromSongMode { get; init; }
}

#endregion

/// <summary>
/// All-in-one groovebox instrument with 16 pads, step sequencer, and performance features.
/// Inspired by Roland MC-101/707 with comprehensive pattern and performance capabilities.
/// </summary>
public class GrooveBox : ISynth, IDisposable
{
    #region Constants

    private const int PadCount = 16;
    private const int MaxPatterns = 64;
    private const int MaxSteps = 64;
    private const int MaxVoices = 32;
    private const int DefaultSteps = 16;

    // Default MIDI note mapping (GM drum kit)
    private static readonly int[] DefaultPadNotes = {
        36, 38, 42, 46, // Kick, Snare, HiHat Closed, HiHat Open
        39, 45, 48, 51, // Clap, Tom Low, Tom High, Ride
        49, 44, 41, 47, // Crash, Pedal HH, Tom Low 2, Tom Mid
        37, 56, 52, 53  // Rim, Cowbell, China, Bell
    };

    private static readonly string[] DefaultPadNames = {
        "Kick", "Snare", "HH Closed", "HH Open",
        "Clap", "Tom Lo", "Tom Hi", "Ride",
        "Crash", "Pedal HH", "Tom Lo 2", "Tom Mid",
        "Rim", "Cowbell", "China", "Bell"
    };

    #endregion

    #region Fields

    private readonly WaveFormat _waveFormat;
    private readonly GroovePad[] _pads = new GroovePad[PadCount];
    private readonly GroovePattern[] _patterns = new GroovePattern[MaxPatterns];
    private readonly List<PatternChainEntry> _songChain = new();
    private readonly GrooveVoice[] _voices = new GrooveVoice[MaxVoices];
    private readonly Random _random = new();
    private readonly object _lock = new();

    // Sequencer state
    private int _currentPattern;
    private int _currentStep;
    private int _queuedPattern = -1;
    private double _currentBeat;
    private double _lastBeat = -1;
    private int _lastTriggeredStep = -1;
    private bool _pingPongForward = true;
    private int _iterationCount;
    private bool _isPlaying;
    private bool _fillMode;
    private FillType _activeFillType = FillType.None;
    private int _fillBarCount;

    // Stutter/roll state
    private bool _stutterActive;
    private StutterDivision _stutterDivision = StutterDivision.Sixteenth;
    private int _stutterPad = -1;
    private double _stutterLastTrigger;

    // Song mode state
    private bool _songMode;
    private int _songChainIndex;
    private int _songRepeatCount;

    // Tap tempo
    private readonly List<DateTime> _tapTimes = new();
    private const int MaxTapSamples = 4;

    // Motion recording
    private bool _motionRecording;
    private string? _motionRecordingParameter;
    private int _motionRecordingPad = -1;
    private MotionRecording? _activeMotionRecording;

    // Master effects state (simple implementations)
    private float _masterCompressorThreshold = -10f;
    private float _masterCompressorRatio = 4f;
    private float _masterCompressorAttack = 0.01f;
    private float _masterCompressorRelease = 0.1f;
    private float _compressorEnvelope;

    private float _masterEqLow = 1f;
    private float _masterEqMid = 1f;
    private float _masterEqHigh = 1f;

    // Delay effect state
    private float[] _delayBuffer = Array.Empty<float>();
    private int _delayWritePos;

    // Reverb effect state (simple comb filter)
    private float[] _reverbBuffer = Array.Empty<float>();
    private int _reverbWritePos;

    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "GrooveBox";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>Global tempo in BPM</summary>
    public double Bpm { get; set; } = 120.0;

    /// <summary>Swing amount (0-1)</summary>
    public float Swing { get; set; }

    /// <summary>Whether sequencer is playing</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Current step index</summary>
    public int CurrentStep => _currentStep;

    /// <summary>Current pattern index</summary>
    public int CurrentPatternIndex => _currentPattern;

    /// <summary>Current pattern</summary>
    public GroovePattern CurrentPattern => _patterns[_currentPattern];

    /// <summary>All pads</summary>
    public IReadOnlyList<GroovePad> Pads => _pads;

    /// <summary>All patterns</summary>
    public IReadOnlyList<GroovePattern> Patterns => _patterns;

    /// <summary>Song chain</summary>
    public IReadOnlyList<PatternChainEntry> SongChain => _songChain;

    /// <summary>Song mode enabled</summary>
    public bool SongMode
    {
        get => _songMode;
        set
        {
            _songMode = value;
            if (value)
            {
                _songChainIndex = 0;
                _songRepeatCount = 0;
            }
        }
    }

    /// <summary>MIDI sync mode</summary>
    public SyncMode SyncMode { get; set; } = SyncMode.Internal;

    /// <summary>Fill mode active</summary>
    public bool FillMode
    {
        get => _fillMode;
        set => _fillMode = value;
    }

    /// <summary>Active fill type</summary>
    public FillType ActiveFillType
    {
        get => _activeFillType;
        set
        {
            _activeFillType = value;
            _fillMode = value != FillType.None;
        }
    }

    /// <summary>Queued pattern index (-1 if none)</summary>
    public int QueuedPattern => _queuedPattern;

    /// <summary>Delay time in seconds</summary>
    public float DelayTime { get; set; } = 0.375f;

    /// <summary>Delay feedback (0-1)</summary>
    public float DelayFeedback { get; set; } = 0.4f;

    /// <summary>Delay mix (0-1)</summary>
    public float DelayMix { get; set; } = 0.3f;

    /// <summary>Reverb size (0-1)</summary>
    public float ReverbSize { get; set; } = 0.5f;

    /// <summary>Reverb mix (0-1)</summary>
    public float ReverbMix { get; set; } = 0.2f;

    /// <summary>Loop start step</summary>
    public int LoopStart { get; set; }

    /// <summary>Loop end step (0 = use pattern length)</summary>
    public int LoopEnd { get; set; }

    /// <summary>Accent velocity boost</summary>
    public int AccentBoost { get; set; } = 25;

    /// <summary>Global probability multiplier (0-1)</summary>
    public float GlobalProbability { get; set; } = 1.0f;

    #endregion

    #region Events

    /// <summary>Fired when a pad is triggered</summary>
    public event EventHandler<PadTriggerEventArgs>? PadTriggered;

    /// <summary>Fired when step changes</summary>
    public event EventHandler<StepChangedEventArgs>? StepChanged;

    /// <summary>Fired when pattern changes</summary>
    public event EventHandler<PatternChangedEventArgs>? PatternChanged;

    /// <summary>Fired when note should be sent via MIDI</summary>
    public event Action<int, int, float>? NoteTriggered; // note, velocity, gate

    /// <summary>Fired when note ends</summary>
    public event Action<int>? NoteReleased;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new GrooveBox instance
    /// </summary>
    /// <param name="sampleRate">Sample rate (uses Settings.SampleRate if null)</param>
    public GrooveBox(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize pads
        for (int i = 0; i < PadCount; i++)
        {
            _pads[i] = new GroovePad(i, DefaultPadNames[i], DefaultPadNotes[i]);
            _pads[i].InitializeSteps(DefaultSteps);
        }

        // Initialize patterns
        for (int i = 0; i < MaxPatterns; i++)
        {
            _patterns[i] = new GroovePattern(i, $"Pattern {i + 1}", DefaultSteps);
        }

        // Initialize voices
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices[i] = new GrooveVoice();
        }

        // Initialize effect buffers
        int maxDelaysamples = (int)(rate * 2.0); // 2 seconds max delay
        _delayBuffer = new float[maxDelaysamples * 2]; // Stereo
        _reverbBuffer = new float[rate]; // 1 second reverb buffer
    }

    #endregion

    #region Pad Operations

    /// <summary>
    /// Get pad by index
    /// </summary>
    public GroovePad GetPad(int index)
    {
        if (index < 0 || index >= PadCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _pads[index];
    }

    /// <summary>
    /// Load sample data into a pad
    /// </summary>
    public void LoadSample(int padIndex, float[] sampleData, int sampleRate, int channels)
    {
        if (padIndex < 0 || padIndex >= PadCount)
            throw new ArgumentOutOfRangeException(nameof(padIndex));

        var pad = _pads[padIndex];
        pad.SampleData = sampleData;
        pad.SampleRate = sampleRate;
        pad.Channels = channels;
    }

    /// <summary>
    /// Set pad parameter
    /// </summary>
    public void SetPadParameter(int padIndex, string paramName, float value)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;

        var pad = _pads[padIndex];
        switch (paramName.ToLowerInvariant())
        {
            case "volume": pad.Volume = Math.Clamp(value, 0f, 1f); break;
            case "pan": pad.Pan = Math.Clamp(value, -1f, 1f); break;
            case "pitch": pad.Pitch = Math.Clamp(value, -24f, 24f); break;
            case "filter":
            case "filtercutoff": pad.FilterCutoff = Math.Clamp(value, 20f, 20000f); break;
            case "filterresonance": pad.FilterResonance = Math.Clamp(value, 0f, 1f); break;
            case "decay": pad.Decay = Math.Clamp(value, 0.01f, 10f); break;
            case "attack": pad.Attack = Math.Clamp(value, 0f, 2f); break;
            case "delaysend": pad.DelaySend = Math.Clamp(value, 0f, 1f); break;
            case "reverbsend": pad.ReverbSend = Math.Clamp(value, 0f, 1f); break;
            case "startoffset": pad.StartOffset = Math.Clamp(value, 0f, 1f); break;
            case "endoffset": pad.EndOffset = Math.Clamp(value, 0f, 1f); break;
        }

        // Record motion if active
        if (_motionRecording && _motionRecordingPad == padIndex &&
            _motionRecordingParameter == paramName && _activeMotionRecording != null)
        {
            _activeMotionRecording.Points.Add(new MotionPoint(_currentBeat, value));
        }
    }

    /// <summary>
    /// Set choke group for pad
    /// </summary>
    public void SetChokeGroup(int padIndex, int group)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        _pads[padIndex].ChokeGroup = Math.Clamp(group, 0, 8);
    }

    /// <summary>
    /// Set mute group for pad
    /// </summary>
    public void SetMuteGroup(int padIndex, int group)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        _pads[padIndex].MuteGroup = Math.Clamp(group, 0, 8);
    }

    /// <summary>
    /// Toggle pad mute
    /// </summary>
    public void TogglePadMute(int padIndex)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        _pads[padIndex].Muted = !_pads[padIndex].Muted;
    }

    /// <summary>
    /// Toggle pad solo
    /// </summary>
    public void TogglePadSolo(int padIndex)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        _pads[padIndex].Soloed = !_pads[padIndex].Soloed;
    }

    /// <summary>
    /// Clear all solos
    /// </summary>
    public void ClearSolos()
    {
        foreach (var pad in _pads)
        {
            pad.Soloed = false;
        }
    }

    #endregion

    #region Step Sequencer Operations

    /// <summary>
    /// Set step active state
    /// </summary>
    public void SetStep(int padIndex, int stepIndex, bool active)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        _pads[padIndex].Steps[stepIndex].Active = active;
    }

    /// <summary>
    /// Toggle step active state
    /// </summary>
    public void ToggleStep(int padIndex, int stepIndex)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        _pads[padIndex].Steps[stepIndex].Active = !_pads[padIndex].Steps[stepIndex].Active;
    }

    /// <summary>
    /// Set step properties
    /// </summary>
    public void SetStepProperties(int padIndex, int stepIndex,
        bool? active = null, int? velocity = null, float? gate = null,
        float? probability = null, int? noteOffset = null, int? ratchet = null)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        var step = _pads[padIndex].Steps[stepIndex];
        if (active.HasValue) step.Active = active.Value;
        if (velocity.HasValue) step.Velocity = Math.Clamp(velocity.Value, 0, 127);
        if (gate.HasValue) step.Gate = Math.Clamp(gate.Value, 0f, 1f);
        if (probability.HasValue) step.Probability = Math.Clamp(probability.Value, 0f, 1f);
        if (noteOffset.HasValue) step.NoteOffset = Math.Clamp(noteOffset.Value, -24, 24);
        if (ratchet.HasValue) step.Ratchet = Math.Clamp(ratchet.Value, 1, 8);
    }

    /// <summary>
    /// Add parameter lock to step
    /// </summary>
    public void AddParameterLock(int padIndex, int stepIndex, string paramName, float value)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        var step = _pads[padIndex].Steps[stepIndex];
        var existing = step.ParameterLocks.FirstOrDefault(p => p.ParameterName == paramName);
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            step.ParameterLocks.Add(new ParameterLock(paramName, value));
        }
    }

    /// <summary>
    /// Remove parameter lock from step
    /// </summary>
    public void RemoveParameterLock(int padIndex, int stepIndex, string paramName)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        var step = _pads[padIndex].Steps[stepIndex];
        step.ParameterLocks.RemoveAll(p => p.ParameterName == paramName);
    }

    /// <summary>
    /// Clear all parameter locks from step
    /// </summary>
    public void ClearParameterLocks(int padIndex, int stepIndex)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        _pads[padIndex].Steps[stepIndex].ParameterLocks.Clear();
    }

    /// <summary>
    /// Set step condition
    /// </summary>
    public void SetStepCondition(int padIndex, int stepIndex, StepCondition condition, int param = 2)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (stepIndex < 0 || stepIndex >= _pads[padIndex].Steps.Length) return;

        var step = _pads[padIndex].Steps[stepIndex];
        step.Condition = condition;
        step.ConditionParam = param;
    }

    /// <summary>
    /// Set number of steps in current pattern
    /// </summary>
    public void SetStepCount(int count)
    {
        count = Math.Clamp(count, 1, MaxSteps);

        var pattern = _patterns[_currentPattern];
        pattern.StepCount = count;
        pattern.LengthBars = (int)Math.Ceiling(count / 16.0);

        foreach (var pad in _pads)
        {
            var oldSteps = pad.Steps;
            pad.InitializeSteps(count);

            // Copy existing steps
            for (int i = 0; i < Math.Min(oldSteps.Length, count); i++)
            {
                pad.Steps[i] = oldSteps[i];
            }
        }
    }

    #endregion

    #region Pattern Operations

    /// <summary>
    /// Select pattern
    /// </summary>
    public void SelectPattern(int index)
    {
        if (index < 0 || index >= MaxPatterns) return;

        int oldPattern = _currentPattern;
        _currentPattern = index;

        // Load pattern steps to pads
        var pattern = _patterns[index];
        foreach (var pad in _pads)
        {
            if (pattern.PadSteps.TryGetValue(pad.Index, out var steps))
            {
                pad.Steps = steps.Select(s => s.Clone()).ToArray();
            }
            else
            {
                pad.InitializeSteps(pattern.StepCount);
            }
        }

        PatternChanged?.Invoke(this, new PatternChangedEventArgs
        {
            PreviousPattern = oldPattern,
            NewPattern = index,
            FromSongMode = _songMode
        });
    }

    /// <summary>
    /// Queue pattern change (will happen at next bar)
    /// </summary>
    public void QueuePattern(int index)
    {
        if (index < 0 || index >= MaxPatterns) return;
        _queuedPattern = index;
    }

    /// <summary>
    /// Save current pad steps to pattern
    /// </summary>
    public void SavePatternSteps()
    {
        var pattern = _patterns[_currentPattern];
        pattern.PadSteps.Clear();

        foreach (var pad in _pads)
        {
            pattern.PadSteps[pad.Index] = pad.Steps.Select(s => s.Clone()).ToArray();
        }
    }

    /// <summary>
    /// Copy pattern
    /// </summary>
    public void CopyPattern(int sourceIndex, int destIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= MaxPatterns) return;
        if (destIndex < 0 || destIndex >= MaxPatterns) return;
        if (sourceIndex == destIndex) return;

        var source = _patterns[sourceIndex];
        var dest = source.Clone();
        dest.Index = destIndex;
        dest.Name = $"Pattern {destIndex + 1}";
        _patterns[destIndex] = dest;
    }

    /// <summary>
    /// Clear pattern
    /// </summary>
    public void ClearPattern(int index)
    {
        if (index < 0 || index >= MaxPatterns) return;

        _patterns[index] = new GroovePattern(index, $"Pattern {index + 1}", DefaultSteps);

        if (index == _currentPattern)
        {
            foreach (var pad in _pads)
            {
                pad.InitializeSteps(DefaultSteps);
            }
        }
    }

    /// <summary>
    /// Set pattern tempo
    /// </summary>
    public void SetPatternTempo(int index, double tempo)
    {
        if (index < 0 || index >= MaxPatterns) return;
        _patterns[index].Tempo = tempo;
    }

    /// <summary>
    /// Set pattern swing
    /// </summary>
    public void SetPatternSwing(int index, float swing)
    {
        if (index < 0 || index >= MaxPatterns) return;
        _patterns[index].Swing = Math.Clamp(swing, 0f, 1f);
    }

    /// <summary>
    /// Set pattern direction
    /// </summary>
    public void SetPatternDirection(int index, PatternDirection direction)
    {
        if (index < 0 || index >= MaxPatterns) return;
        _patterns[index].Direction = direction;
    }

    /// <summary>
    /// Toggle scene variation (A/B)
    /// </summary>
    public void ToggleVariation()
    {
        _patterns[_currentPattern].ActiveVariationIsA = !_patterns[_currentPattern].ActiveVariationIsA;
    }

    /// <summary>
    /// Set scene variation
    /// </summary>
    public void SetVariation(bool useA)
    {
        _patterns[_currentPattern].ActiveVariationIsA = useA;
    }

    /// <summary>
    /// Save current state to variation
    /// </summary>
    public void SaveToVariation(bool toA)
    {
        var pattern = _patterns[_currentPattern];
        var variation = toA ? pattern.VariationA : pattern.VariationB;

        variation.PadMutes.Clear();
        variation.PadVolumeOffsets.Clear();
        variation.PadFilterOffsets.Clear();

        foreach (var pad in _pads)
        {
            if (pad.Muted)
                variation.PadMutes[pad.Index] = true;
        }
    }

    #endregion

    #region Song Mode Operations

    /// <summary>
    /// Add pattern to song chain
    /// </summary>
    public void AddToSongChain(int patternIndex, int repeats = 1, bool useVariationA = true)
    {
        if (patternIndex < 0 || patternIndex >= MaxPatterns) return;
        _songChain.Add(new PatternChainEntry(patternIndex, repeats, useVariationA));
    }

    /// <summary>
    /// Insert pattern in song chain
    /// </summary>
    public void InsertInSongChain(int position, int patternIndex, int repeats = 1)
    {
        if (patternIndex < 0 || patternIndex >= MaxPatterns) return;
        if (position < 0 || position > _songChain.Count) return;
        _songChain.Insert(position, new PatternChainEntry(patternIndex, repeats));
    }

    /// <summary>
    /// Remove from song chain
    /// </summary>
    public void RemoveFromSongChain(int position)
    {
        if (position < 0 || position >= _songChain.Count) return;
        _songChain.RemoveAt(position);
    }

    /// <summary>
    /// Clear song chain
    /// </summary>
    public void ClearSongChain()
    {
        _songChain.Clear();
        _songChainIndex = 0;
        _songRepeatCount = 0;
    }

    /// <summary>
    /// Move chain entry
    /// </summary>
    public void MoveSongChainEntry(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _songChain.Count) return;
        if (toIndex < 0 || toIndex >= _songChain.Count) return;
        if (fromIndex == toIndex) return;

        var entry = _songChain[fromIndex];
        _songChain.RemoveAt(fromIndex);
        _songChain.Insert(toIndex, entry);
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// Start playback
    /// </summary>
    public void Start()
    {
        _isPlaying = true;
        _currentStep = LoopStart;
        _currentBeat = 0;
        _lastBeat = -1;
        _lastTriggeredStep = -1;
        _pingPongForward = true;
        _iterationCount = 0;

        if (_songMode && _songChain.Count > 0)
        {
            _songChainIndex = 0;
            _songRepeatCount = 0;
            SelectPattern(_songChain[0].PatternIndex);
        }
    }

    /// <summary>
    /// Stop playback
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
        _queuedPattern = -1;
        _fillMode = false;
        _stutterActive = false;

        // Release all voices
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Reset();
            }
        }

        foreach (var pad in _pads)
        {
            NoteReleased?.Invoke(pad.Note);
        }
    }

    /// <summary>
    /// Reset to beginning
    /// </summary>
    public void Reset()
    {
        _currentStep = LoopStart;
        _currentBeat = 0;
        _lastBeat = -1;
        _lastTriggeredStep = -1;
        _pingPongForward = true;
        _iterationCount = 0;

        if (_songMode)
        {
            _songChainIndex = 0;
            _songRepeatCount = 0;
        }
    }

    /// <summary>
    /// Process sequencer at current beat
    /// </summary>
    public void Process(double currentBeat)
    {
        if (!_isPlaying) return;

        var pattern = _patterns[_currentPattern];
        double effectiveTempo = pattern.Tempo > 0 ? pattern.Tempo : Bpm;
        double beatsPerStep = pattern.StepLength;
        int effectiveSteps = (LoopEnd > 0 ? LoopEnd : pattern.StepCount) - LoopStart;
        if (effectiveSteps <= 0) effectiveSteps = pattern.StepCount;

        // Apply swing offset to current step calculation
        float effectiveSwing = pattern.Swing > 0 ? pattern.Swing : Swing;
        double swingOffset = 0;
        if (effectiveSwing > 0 && _currentStep % 2 == 1)
        {
            swingOffset = beatsPerStep * effectiveSwing * 0.5;
        }

        double stepBeat = (_currentStep - LoopStart) * beatsPerStep + swingOffset;
        double loopLength = effectiveSteps * beatsPerStep;
        double normalizedBeat = currentBeat % loopLength;

        // Check for pattern change at bar boundary
        if (_queuedPattern >= 0)
        {
            int beatsPerBar = pattern.TimeSignatureNumerator;
            double barBoundary = Math.Ceiling(_lastBeat / beatsPerBar) * beatsPerBar;

            if (normalizedBeat >= 0 && _lastBeat < barBoundary && currentBeat >= barBoundary)
            {
                SelectPattern(_queuedPattern);
                _queuedPattern = -1;
            }
        }

        // Check if we should trigger current step
        if (_lastTriggeredStep != _currentStep &&
            normalizedBeat >= stepBeat &&
            (_lastBeat < stepBeat || _lastBeat > normalizedBeat))
        {
            TriggerCurrentStep();
            _lastTriggeredStep = _currentStep;

            bool isBarStart = _currentStep % 16 == 0;
            StepChanged?.Invoke(this, new StepChangedEventArgs
            {
                StepIndex = _currentStep,
                Beat = currentBeat,
                IsBarStart = isBarStart
            });
        }

        // Process stutter if active
        if (_stutterActive && _stutterPad >= 0)
        {
            ProcessStutter(currentBeat);
        }

        // Check for step advance
        double nextStepBeat = ((_currentStep - LoopStart + 1) % effectiveSteps) * beatsPerStep;
        if (normalizedBeat >= nextStepBeat && _lastBeat < nextStepBeat)
        {
            AdvanceStep();
        }

        _lastBeat = normalizedBeat;
        _currentBeat = currentBeat;
    }

    private void TriggerCurrentStep()
    {
        bool anySoloed = _pads.Any(p => p.Soloed);
        var pattern = _patterns[_currentPattern];
        var variation = pattern.ActiveVariationIsA ? pattern.VariationA : pattern.VariationB;

        foreach (var pad in _pads)
        {
            // Check mute/solo
            if (pad.Muted) continue;
            if (variation.PadMutes.TryGetValue(pad.Index, out bool muted) && muted) continue;
            if (anySoloed && !pad.Soloed) continue;

            if (_currentStep < 0 || _currentStep >= pad.Steps.Length) continue;

            var step = pad.Steps[_currentStep];

            // Check fill-only steps
            if (step.FillOnly && !_fillMode) continue;

            // Check condition
            if (!CheckStepCondition(step)) continue;

            if (step.Active)
            {
                // Check probability
                float effectiveProbability = step.Probability * GlobalProbability;
                if (_random.NextDouble() > effectiveProbability) continue;

                // Apply parameter locks
                ApplyParameterLocks(pad, step);

                // Calculate velocity
                int velocity = step.Velocity;
                velocity = Math.Clamp(velocity, 1, 127);

                // Handle ratchet
                if (step.Ratchet > 1)
                {
                    for (int r = 0; r < step.Ratchet; r++)
                    {
                        TriggerPad(pad.Index, velocity);
                    }
                }
                else
                {
                    TriggerPad(pad.Index, velocity);
                }
            }
        }

        // Apply fill variations
        if (_fillMode && _activeFillType != FillType.None)
        {
            ApplyFillVariation();
        }
    }

    private bool CheckStepCondition(GrooveStep step)
    {
        switch (step.Condition)
        {
            case StepCondition.Always:
                return true;

            case StepCondition.EveryN:
                return (_iterationCount % step.ConditionParam) == 0;

            case StepCondition.NofM:
                return (_iterationCount % step.ConditionParam) == 0;

            case StepCondition.FirstOnly:
                return _iterationCount == 0;

            case StepCondition.NotFirst:
                return _iterationCount > 0;

            case StepCondition.Random50:
                return _random.NextDouble() < 0.5;

            case StepCondition.Fill:
                return _fillMode;

            default:
                return true;
        }
    }

    private void ApplyParameterLocks(GroovePad pad, GrooveStep step)
    {
        foreach (var pLock in step.ParameterLocks.Where(p => p.Active))
        {
            SetPadParameter(pad.Index, pLock.ParameterName, pLock.Value);
        }

        // Apply motion recordings
        foreach (var motion in pad.MotionRecordings)
        {
            float value = motion.GetValueAtBeat(_currentBeat % (_patterns[_currentPattern].StepCount * _patterns[_currentPattern].StepLength), 0);
            if (motion.PlaybackEnabled)
            {
                SetPadParameter(pad.Index, motion.ParameterName, value);
            }
        }
    }

    private void ApplyFillVariation()
    {
        switch (_activeFillType)
        {
            case FillType.Roll:
                // Add extra triggers on random pads
                int rollPad = _random.Next(PadCount);
                if (!_pads[rollPad].Muted)
                {
                    TriggerPad(rollPad, 90 + _random.Next(30));
                }
                break;

            case FillType.Random:
                // Randomly trigger some pads
                for (int i = 0; i < PadCount; i++)
                {
                    if (_random.NextDouble() < 0.2 && !_pads[i].Muted)
                    {
                        TriggerPad(i, 70 + _random.Next(50));
                    }
                }
                break;
        }
    }

    private void AdvanceStep()
    {
        var pattern = _patterns[_currentPattern];
        int effectiveStart = LoopStart;
        int effectiveEnd = LoopEnd > 0 ? LoopEnd : pattern.StepCount;

        switch (pattern.Direction)
        {
            case PatternDirection.Forward:
                _currentStep++;
                if (_currentStep >= effectiveEnd)
                {
                    _currentStep = effectiveStart;
                    _iterationCount++;
                    OnPatternLoop();
                }
                break;

            case PatternDirection.Backward:
                _currentStep--;
                if (_currentStep < effectiveStart)
                {
                    _currentStep = effectiveEnd - 1;
                    _iterationCount++;
                    OnPatternLoop();
                }
                break;

            case PatternDirection.PingPong:
                if (_pingPongForward)
                {
                    _currentStep++;
                    if (_currentStep >= effectiveEnd)
                    {
                        _currentStep = effectiveEnd - 2;
                        if (_currentStep < effectiveStart) _currentStep = effectiveStart;
                        _pingPongForward = false;
                    }
                }
                else
                {
                    _currentStep--;
                    if (_currentStep < effectiveStart)
                    {
                        _currentStep = effectiveStart + 1;
                        if (_currentStep >= effectiveEnd) _currentStep = effectiveEnd - 1;
                        _pingPongForward = true;
                        _iterationCount++;
                        OnPatternLoop();
                    }
                }
                break;

            case PatternDirection.Random:
                _currentStep = _random.Next(effectiveStart, effectiveEnd);
                break;
        }

        _lastTriggeredStep = -1;
    }

    private void OnPatternLoop()
    {
        // Handle song mode chain advancement
        if (_songMode && _songChain.Count > 0)
        {
            _songRepeatCount++;
            var entry = _songChain[_songChainIndex];

            if (_songRepeatCount >= entry.Repeats)
            {
                _songRepeatCount = 0;
                _songChainIndex++;

                if (_songChainIndex >= _songChain.Count)
                {
                    _songChainIndex = 0; // Loop song
                }

                var nextEntry = _songChain[_songChainIndex];
                SelectPattern(nextEntry.PatternIndex);
                SetVariation(nextEntry.UseVariationA);

                if (nextEntry.TempoOverride > 0)
                {
                    Bpm = nextEntry.TempoOverride;
                }
            }
        }

        // Disable fill mode after one loop
        if (_fillMode)
        {
            _fillBarCount++;
            if (_fillBarCount >= _patterns[_currentPattern].LengthBars)
            {
                _fillMode = false;
                _activeFillType = FillType.None;
                _fillBarCount = 0;
            }
        }
    }

    #endregion

    #region Pad Triggering

    /// <summary>
    /// Trigger pad by index
    /// </summary>
    public void TriggerPad(int padIndex, int velocity = 100, int stepIndex = -1)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        if (velocity <= 0) return;

        var pad = _pads[padIndex];

        // Handle choke groups
        if (pad.ChokeGroup > 0)
        {
            ChokeGroup(pad.ChokeGroup, padIndex);
        }

        // Handle mute groups
        if (pad.MuteGroup > 0)
        {
            MuteGroup(pad.MuteGroup, padIndex);
        }

        // Trigger voice
        TriggerVoice(padIndex, pad.Note, velocity);

        // Fire events
        PadTriggered?.Invoke(this, new PadTriggerEventArgs
        {
            PadIndex = padIndex,
            Note = pad.Note,
            Velocity = velocity,
            StepIndex = stepIndex
        });

        NoteTriggered?.Invoke(pad.Note, velocity, pad.Steps.Length > 0 ? pad.Steps[0].Gate : 0.5f);
    }

    /// <summary>
    /// Release pad
    /// </summary>
    public void ReleasePad(int padIndex)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;

        var pad = _pads[padIndex];

        if (pad.Mode == PadMode.Gate)
        {
            ReleaseVoice(padIndex);
        }

        NoteReleased?.Invoke(pad.Note);
    }

    private void TriggerVoice(int padIndex, int note, int velocity)
    {
        var pad = _pads[padIndex];
        if (pad.SampleData == null || pad.SampleData.Length == 0) return;

        lock (_lock)
        {
            // Find free voice or steal oldest
            GrooveVoice? voice = null;
            for (int i = 0; i < MaxVoices; i++)
            {
                if (!_voices[i].IsActive)
                {
                    voice = _voices[i];
                    break;
                }
            }

            // Voice stealing
            if (voice == null)
            {
                voice = _voices[0];
            }

            // Calculate pitch ratio
            double pitchRatio = Math.Pow(2.0, pad.Pitch / 12.0);
            pitchRatio *= (double)pad.SampleRate / _waveFormat.SampleRate;

            // Calculate start position
            int startSample = (int)(pad.StartOffset * (pad.SampleData.Length / pad.Channels));
            if (pad.Reverse)
            {
                int endSample = pad.EndOffset > 0
                    ? (int)(pad.EndOffset * (pad.SampleData.Length / pad.Channels))
                    : pad.SampleData.Length / pad.Channels;
                startSample = endSample - 1;
            }

            voice.PadIndex = padIndex;
            voice.Note = note;
            voice.Velocity = velocity;
            voice.Position = startSample;
            voice.PitchRatio = pitchRatio;
            voice.EnvelopeLevel = 0;
            voice.EnvelopePhase = 0;
            voice.IsActive = true;
            voice.IsReleasing = false;
            voice.FilterState = 0;
        }
    }

    private void ReleaseVoice(int padIndex)
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.IsActive && voice.PadIndex == padIndex)
                {
                    voice.IsReleasing = true;
                }
            }
        }
    }

    private void ChokeGroup(int group, int exceptPad)
    {
        lock (_lock)
        {
            for (int i = 0; i < PadCount; i++)
            {
                if (i != exceptPad && _pads[i].ChokeGroup == group)
                {
                    foreach (var voice in _voices)
                    {
                        if (voice.IsActive && voice.PadIndex == i)
                        {
                            voice.IsReleasing = true;
                        }
                    }
                }
            }
        }
    }

    private void MuteGroup(int group, int exceptPad)
    {
        for (int i = 0; i < PadCount; i++)
        {
            if (i != exceptPad && _pads[i].MuteGroup == group)
            {
                _pads[i].Muted = true;
            }
        }
    }

    #endregion

    #region Stutter/Roll Effect

    /// <summary>
    /// Activate stutter effect on pad
    /// </summary>
    public void StartStutter(int padIndex, StutterDivision division = StutterDivision.Sixteenth)
    {
        _stutterActive = true;
        _stutterPad = padIndex;
        _stutterDivision = division;
        _stutterLastTrigger = _currentBeat;

        TriggerPad(padIndex, 100);
    }

    /// <summary>
    /// Stop stutter effect
    /// </summary>
    public void StopStutter()
    {
        _stutterActive = false;
        _stutterPad = -1;
    }

    private void ProcessStutter(double currentBeat)
    {
        double stutterInterval = 1.0 / (int)_stutterDivision;
        double timeSinceLastTrigger = currentBeat - _stutterLastTrigger;

        if (timeSinceLastTrigger >= stutterInterval)
        {
            TriggerPad(_stutterPad, 100);
            _stutterLastTrigger = currentBeat;
        }
    }

    #endregion

    #region Motion Recording

    /// <summary>
    /// Start motion recording for a parameter
    /// </summary>
    public void StartMotionRecording(int padIndex, string parameterName)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;

        _motionRecording = true;
        _motionRecordingPad = padIndex;
        _motionRecordingParameter = parameterName;
        _activeMotionRecording = new MotionRecording
        {
            ParameterName = parameterName
        };
    }

    /// <summary>
    /// Stop motion recording
    /// </summary>
    public void StopMotionRecording()
    {
        if (_motionRecording && _activeMotionRecording != null && _motionRecordingPad >= 0)
        {
            _pads[_motionRecordingPad].MotionRecordings.Add(_activeMotionRecording);
        }

        _motionRecording = false;
        _activeMotionRecording = null;
        _motionRecordingPad = -1;
        _motionRecordingParameter = null;
    }

    /// <summary>
    /// Clear motion recordings for pad
    /// </summary>
    public void ClearMotionRecordings(int padIndex)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;
        _pads[padIndex].MotionRecordings.Clear();
    }

    /// <summary>
    /// Toggle motion playback
    /// </summary>
    public void ToggleMotionPlayback(int padIndex, string parameterName)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;

        var motion = _pads[padIndex].MotionRecordings.FirstOrDefault(m => m.ParameterName == parameterName);
        if (motion != null)
        {
            motion.PlaybackEnabled = !motion.PlaybackEnabled;
        }
    }

    #endregion

    #region Tap Tempo

    /// <summary>
    /// Process tap for tap tempo
    /// </summary>
    public void TapTempo()
    {
        var now = DateTime.Now;

        // Clear old taps
        _tapTimes.RemoveAll(t => (now - t).TotalSeconds > 2.0);

        _tapTimes.Add(now);

        if (_tapTimes.Count >= 2)
        {
            // Calculate average interval
            double totalInterval = 0;
            for (int i = 1; i < _tapTimes.Count; i++)
            {
                totalInterval += (_tapTimes[i] - _tapTimes[i - 1]).TotalSeconds;
            }

            double avgInterval = totalInterval / (_tapTimes.Count - 1);
            Bpm = 60.0 / avgInterval;
            Bpm = Math.Clamp(Bpm, 30.0, 300.0);
        }

        // Keep only recent taps
        while (_tapTimes.Count > MaxTapSamples)
        {
            _tapTimes.RemoveAt(0);
        }
    }

    /// <summary>
    /// Reset tap tempo
    /// </summary>
    public void ResetTapTempo()
    {
        _tapTimes.Clear();
    }

    #endregion

    #region Fill Mode

    /// <summary>
    /// Activate fill mode
    /// </summary>
    public void ActivateFill(FillType fillType = FillType.Simple)
    {
        _fillMode = true;
        _activeFillType = fillType;
        _fillBarCount = 0;
    }

    /// <summary>
    /// Deactivate fill mode
    /// </summary>
    public void DeactivateFill()
    {
        _fillMode = false;
        _activeFillType = FillType.None;
    }

    /// <summary>
    /// Generate fill pattern
    /// </summary>
    public void GenerateFill(FillType fillType)
    {
        var pattern = _patterns[_currentPattern];
        int lastBar = (pattern.StepCount / 16) - 1;
        int fillStart = lastBar * 16;

        switch (fillType)
        {
            case FillType.BuildUp:
                // Increasing density
                for (int i = 0; i < PadCount; i++)
                {
                    var pad = _pads[i];
                    for (int s = fillStart; s < pattern.StepCount; s++)
                    {
                        float density = (float)(s - fillStart) / 16;
                        if (_random.NextDouble() < density * 0.5)
                        {
                            pad.Steps[s].Active = true;
                            pad.Steps[s].FillOnly = true;
                            pad.Steps[s].Velocity = 80 + (int)(density * 47);
                        }
                    }
                }
                break;

            case FillType.Roll:
                // Add rolls on last 4 steps
                var rollPad = _pads[_random.Next(PadCount)];
                for (int s = pattern.StepCount - 4; s < pattern.StepCount; s++)
                {
                    rollPad.Steps[s].Active = true;
                    rollPad.Steps[s].FillOnly = true;
                    rollPad.Steps[s].Ratchet = 2 + _random.Next(3);
                }
                break;

            case FillType.Breakdown:
                // Sparse last bar
                for (int i = 0; i < PadCount; i++)
                {
                    var pad = _pads[i];
                    for (int s = fillStart; s < pattern.StepCount; s++)
                    {
                        if (pad.Steps[s].Active)
                        {
                            if (_random.NextDouble() > 0.3)
                            {
                                pad.Steps[s].Active = false;
                            }
                        }
                    }
                }
                break;
        }
    }

    #endregion

    #region ISynth Implementation

    /// <summary>
    /// Note on (MIDI input)
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        // Find pad with matching note
        for (int i = 0; i < PadCount; i++)
        {
            if (_pads[i].Note == note)
            {
                TriggerPad(i, velocity);
                break;
            }
        }
    }

    /// <summary>
    /// Note off (MIDI input)
    /// </summary>
    public void NoteOff(int note)
    {
        for (int i = 0; i < PadCount; i++)
        {
            if (_pads[i].Note == note)
            {
                ReleasePad(i);
                break;
            }
        }
    }

    /// <summary>
    /// All notes off
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Reset();
            }
        }
    }

    /// <summary>
    /// Set parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "bpm":
            case "tempo": Bpm = Math.Clamp(value, 30f, 300f); break;
            case "swing": Swing = Math.Clamp(value, 0f, 1f); break;
            case "delaytime": DelayTime = Math.Clamp(value, 0.01f, 2f); break;
            case "delayfeedback": DelayFeedback = Math.Clamp(value, 0f, 0.95f); break;
            case "delaymix": DelayMix = Math.Clamp(value, 0f, 1f); break;
            case "reverbsize": ReverbSize = Math.Clamp(value, 0f, 1f); break;
            case "reverbmix": ReverbMix = Math.Clamp(value, 0f, 1f); break;
            case "mastereqlow": _masterEqLow = Math.Clamp(value, 0f, 2f); break;
            case "mastereqmid": _masterEqMid = Math.Clamp(value, 0f, 2f); break;
            case "mastereqhigh": _masterEqHigh = Math.Clamp(value, 0f, 2f); break;
            case "compressorthreshold": _masterCompressorThreshold = Math.Clamp(value, -60f, 0f); break;
            case "compressorratio": _masterCompressorRatio = Math.Clamp(value, 1f, 20f); break;
        }
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        // Temporary buffers for effects
        float[] dryBuffer = new float[count];
        float[] delayIn = new float[count];
        float[] reverbIn = new float[count];

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sampleL = 0;
                float sampleR = 0;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    var (voiceL, voiceR) = ProcessVoice(voice, deltaTime);
                    sampleL += voiceL;
                    sampleR += voiceR;
                }

                // Store dry signal
                dryBuffer[n] = sampleL;
                if (channels > 1)
                    dryBuffer[n + 1] = sampleR;

                // Collect effect sends
                float delaySendL = 0, delaySendR = 0;
                float reverbSendL = 0, reverbSendR = 0;

                foreach (var voice in _voices.Where(v => v.IsActive))
                {
                    var pad = _pads[voice.PadIndex];
                    delaySendL += sampleL * pad.DelaySend;
                    delaySendR += sampleR * pad.DelaySend;
                    reverbSendL += sampleL * pad.ReverbSend;
                    reverbSendR += sampleR * pad.ReverbSend;
                }

                delayIn[n] = delaySendL;
                reverbIn[n] = reverbSendL;
                if (channels > 1)
                {
                    delayIn[n + 1] = delaySendR;
                    reverbIn[n + 1] = reverbSendR;
                }
            }
        }

        // Process effects
        ProcessDelay(delayIn, count);
        ProcessReverb(reverbIn, count);

        // Mix dry and wet signals
        for (int n = 0; n < count; n += channels)
        {
            float outL = dryBuffer[n];
            float outR = channels > 1 ? dryBuffer[n + 1] : dryBuffer[n];

            // Add delay
            outL += delayIn[n] * DelayMix;
            if (channels > 1)
                outR += delayIn[n + 1] * DelayMix;

            // Add reverb
            outL += reverbIn[n] * ReverbMix;
            if (channels > 1)
                outR += reverbIn[n + 1] * ReverbMix;

            // Apply master EQ (simple 3-band)
            outL = ApplyMasterEQ(outL);
            outR = ApplyMasterEQ(outR);

            // Apply master compressor
            float compressionGain = ApplyCompressor(Math.Max(Math.Abs(outL), Math.Abs(outR)));
            outL *= compressionGain;
            outR *= compressionGain;

            // Apply master volume
            outL *= Volume;
            outR *= Volume;

            // Soft clip
            outL = MathF.Tanh(outL);
            outR = MathF.Tanh(outR);

            buffer[offset + n] = outL;
            if (channels > 1)
                buffer[offset + n + 1] = outR;
        }

        return count;
    }

    private (float left, float right) ProcessVoice(GrooveVoice voice, double deltaTime)
    {
        var pad = _pads[voice.PadIndex];
        if (pad.SampleData == null) return (0, 0);

        int sampleCount = pad.SampleData.Length / pad.Channels;
        int endSample = pad.EndOffset > 0 ? (int)(pad.EndOffset * sampleCount) : sampleCount;
        int startSample = (int)(pad.StartOffset * sampleCount);

        // Process envelope
        float attackTime = pad.Attack;
        float decayTime = pad.Decay;

        if (voice.EnvelopePhase < 1.0f && attackTime > 0)
        {
            voice.EnvelopeLevel += (float)(deltaTime / attackTime);
            if (voice.EnvelopeLevel >= 1.0f)
            {
                voice.EnvelopeLevel = 1.0f;
                voice.EnvelopePhase = 1.0f;
            }
        }
        else if (voice.IsReleasing || pad.Mode == PadMode.OneShot)
        {
            voice.EnvelopeLevel -= (float)(deltaTime / decayTime);
            if (voice.EnvelopeLevel <= 0)
            {
                voice.IsActive = false;
                return (0, 0);
            }
        }
        else
        {
            voice.EnvelopeLevel = 1.0f;
        }

        // Get sample position
        int pos = (int)voice.Position;
        if (pad.Reverse)
        {
            if (pos < startSample)
            {
                if (pad.Mode == PadMode.Loop)
                {
                    voice.Position = endSample - 1;
                    pos = endSample - 1;
                }
                else
                {
                    voice.IsActive = false;
                    return (0, 0);
                }
            }
        }
        else
        {
            if (pos >= endSample)
            {
                if (pad.Mode == PadMode.Loop)
                {
                    voice.Position = startSample;
                    pos = startSample;
                }
                else
                {
                    voice.IsActive = false;
                    return (0, 0);
                }
            }
        }

        // Read sample with interpolation
        float sample = 0;
        float sampleR = 0;

        if (pad.Channels == 1)
        {
            if (pos >= 0 && pos < pad.SampleData.Length)
            {
                sample = pad.SampleData[pos];
            }
            sampleR = sample;
        }
        else
        {
            int idx = pos * pad.Channels;
            if (idx >= 0 && idx + 1 < pad.SampleData.Length)
            {
                sample = pad.SampleData[idx];
                sampleR = pad.SampleData[idx + 1];
            }
        }

        // Apply filter (simple one-pole lowpass)
        float cutoffNorm = pad.FilterCutoff / (float)_waveFormat.SampleRate;
        cutoffNorm = Math.Clamp(cutoffNorm, 0.001f, 0.499f);
        float filterCoeff = 1.0f - MathF.Exp(-2.0f * MathF.PI * cutoffNorm);

        voice.FilterState += filterCoeff * (sample - voice.FilterState);
        sample = voice.FilterState;

        float filterStateR = voice.FilterState;
        filterStateR += filterCoeff * (sampleR - filterStateR);
        sampleR = filterStateR;

        // Apply velocity and envelope
        float velocityScale = voice.Velocity / 127.0f;
        float level = velocityScale * voice.EnvelopeLevel * pad.Volume;

        sample *= level;
        sampleR *= level;

        // Apply pan
        float panL = 1.0f - Math.Max(0, pad.Pan);
        float panR = 1.0f + Math.Min(0, pad.Pan);

        sample *= panL;
        sampleR *= panR;

        // Advance position
        if (pad.Reverse)
        {
            voice.Position -= voice.PitchRatio;
        }
        else
        {
            voice.Position += voice.PitchRatio;
        }

        return (sample, sampleR);
    }

    private void ProcessDelay(float[] buffer, int count)
    {
        int channels = _waveFormat.Channels;
        int delaySamples = (int)(DelayTime * _waveFormat.SampleRate);
        delaySamples = Math.Min(delaySamples, _delayBuffer.Length / 2 - 1);

        for (int n = 0; n < count; n += channels)
        {
            int readPos = _delayWritePos - delaySamples * channels;
            if (readPos < 0) readPos += _delayBuffer.Length;

            float delayedL = _delayBuffer[readPos];
            float delayedR = channels > 1 ? _delayBuffer[readPos + 1] : delayedL;

            // Write to delay buffer with feedback
            _delayBuffer[_delayWritePos] = buffer[n] + delayedL * DelayFeedback;
            if (channels > 1)
                _delayBuffer[_delayWritePos + 1] = buffer[n + 1] + delayedR * DelayFeedback;

            _delayWritePos += channels;
            if (_delayWritePos >= _delayBuffer.Length)
                _delayWritePos = 0;

            buffer[n] = delayedL;
            if (channels > 1)
                buffer[n + 1] = delayedR;
        }
    }

    private void ProcessReverb(float[] buffer, int count)
    {
        // Simple comb filter reverb
        int reverbDelay = (int)(ReverbSize * _reverbBuffer.Length * 0.8f) + 100;
        reverbDelay = Math.Min(reverbDelay, _reverbBuffer.Length - 1);

        float decay = 0.5f + ReverbSize * 0.4f;

        for (int n = 0; n < count; n++)
        {
            int readPos = _reverbWritePos - reverbDelay;
            if (readPos < 0) readPos += _reverbBuffer.Length;

            float delayed = _reverbBuffer[readPos];
            float input = buffer[n];

            _reverbBuffer[_reverbWritePos] = input + delayed * decay;

            _reverbWritePos++;
            if (_reverbWritePos >= _reverbBuffer.Length)
                _reverbWritePos = 0;

            buffer[n] = delayed;
        }
    }

    private float ApplyMasterEQ(float sample)
    {
        // Very simplified 3-band EQ approximation
        return sample * ((_masterEqLow + _masterEqMid + _masterEqHigh) / 3f);
    }

    private float ApplyCompressor(float input)
    {
        float dbInput = 20f * MathF.Log10(Math.Max(input, 0.0001f));
        float dbOverThreshold = dbInput - _masterCompressorThreshold;

        float gain = 1f;
        if (dbOverThreshold > 0)
        {
            float dbReduction = dbOverThreshold * (1f - 1f / _masterCompressorRatio);
            gain = MathF.Pow(10f, -dbReduction / 20f);
        }

        // Envelope follower
        float targetEnv = gain;
        if (targetEnv < _compressorEnvelope)
        {
            _compressorEnvelope += (_masterCompressorAttack * (targetEnv - _compressorEnvelope));
        }
        else
        {
            _compressorEnvelope += (_masterCompressorRelease * (targetEnv - _compressorEnvelope));
        }

        return _compressorEnvelope;
    }

    #endregion

    #region Export Operations

    /// <summary>
    /// Export current pattern as MIDI
    /// </summary>
    public Pattern ExportPatternAsMidi(ISynth? targetSynth)
    {
        var pattern = _patterns[_currentPattern];
        var midiPattern = new Pattern(targetSynth!)
        {
            Name = pattern.Name,
            LoopLength = pattern.StepCount * pattern.StepLength
        };

        for (int padIndex = 0; padIndex < PadCount; padIndex++)
        {
            var pad = _pads[padIndex];
            for (int stepIndex = 0; stepIndex < pad.Steps.Length; stepIndex++)
            {
                var step = pad.Steps[stepIndex];
                if (!step.Active) continue;

                double beat = stepIndex * pattern.StepLength + step.MicroTiming;

                // Apply swing
                if (Swing > 0 && stepIndex % 2 == 1)
                {
                    beat += pattern.StepLength * Swing * 0.5;
                }

                double duration = pattern.StepLength * step.Gate;

                if (step.Ratchet > 1)
                {
                    double ratchetDuration = duration / step.Ratchet;
                    for (int r = 0; r < step.Ratchet; r++)
                    {
                        midiPattern.Events.Add(new NoteEvent
                        {
                            Note = pad.Note + step.NoteOffset,
                            Velocity = step.Velocity,
                            Beat = beat + r * ratchetDuration,
                            Duration = ratchetDuration * 0.9
                        });
                    }
                }
                else
                {
                    midiPattern.Events.Add(new NoteEvent
                    {
                        Note = pad.Note + step.NoteOffset,
                        Velocity = step.Velocity,
                        Beat = beat,
                        Duration = duration
                    });
                }
            }
        }

        return midiPattern;
    }

    /// <summary>
    /// Export pattern as audio (renders pattern to float array)
    /// </summary>
    public float[] ExportPatternAsAudio(int iterations = 1)
    {
        var pattern = _patterns[_currentPattern];
        double patternLengthBeats = pattern.StepCount * pattern.StepLength;
        double patternLengthSeconds = patternLengthBeats * 60.0 / Bpm;
        int totalSamples = (int)(patternLengthSeconds * _waveFormat.SampleRate * _waveFormat.Channels * iterations);

        float[] output = new float[totalSamples];

        // Save state
        bool wasPlaying = _isPlaying;
        int savedStep = _currentStep;
        double savedBeat = _currentBeat;

        // Reset and render
        Reset();
        Start();

        int samplesPerTick = 256;
        double tickDuration = (double)samplesPerTick / _waveFormat.SampleRate;
        double tickBeats = tickDuration * Bpm / 60.0;

        int position = 0;
        double beat = 0;

        while (position < totalSamples)
        {
            Process(beat);

            int samplesToRead = Math.Min(samplesPerTick * _waveFormat.Channels, totalSamples - position);
            Read(output, position, samplesToRead);

            position += samplesToRead;
            beat += tickBeats;
        }

        // Restore state
        _currentStep = savedStep;
        _currentBeat = savedBeat;
        _isPlaying = wasPlaying;

        return output;
    }

    #endregion

    #region Presets

    /// <summary>
    /// Create preset: Basic drum kit setup
    /// </summary>
    public static GrooveBox CreateBasicDrumKit(int? sampleRate = null)
    {
        var groovebox = new GrooveBox(sampleRate)
        {
            Name = "Basic Drum Kit",
            Bpm = 120
        };

        // Set up basic 4-on-the-floor pattern
        groovebox.SetStep(0, 0, true); // Kick on 1
        groovebox.SetStep(0, 4, true); // Kick on 2
        groovebox.SetStep(0, 8, true); // Kick on 3
        groovebox.SetStep(0, 12, true); // Kick on 4

        // Snare on 2 and 4
        groovebox.SetStep(1, 4, true);
        groovebox.SetStep(1, 12, true);

        // Hi-hats on all 8ths
        for (int i = 0; i < 16; i += 2)
        {
            groovebox.SetStep(2, i, true);
        }

        // Open hi-hat on off-beats occasionally
        groovebox.SetStep(3, 6, true);
        groovebox.SetStep(3, 14, true);
        groovebox.SetStepProperties(3, 6, probability: 0.5f);
        groovebox.SetStepProperties(3, 14, probability: 0.3f);

        return groovebox;
    }

    /// <summary>
    /// Create preset: Techno pattern
    /// </summary>
    public static GrooveBox CreateTechnoPattern(int? sampleRate = null)
    {
        var groovebox = new GrooveBox(sampleRate)
        {
            Name = "Techno",
            Bpm = 128,
            Swing = 0
        };

        // 4-on-the-floor kick
        for (int i = 0; i < 16; i += 4)
        {
            groovebox.SetStep(0, i, true);
            groovebox.SetStepProperties(0, i, velocity: 120);
        }

        // Clap/snare on 2 and 4
        groovebox.SetStep(4, 4, true);
        groovebox.SetStep(4, 12, true);

        // Hi-hats on all 16ths with velocity variation
        for (int i = 0; i < 16; i++)
        {
            groovebox.SetStep(2, i, true);
            int velocity = (i % 4 == 0) ? 100 : (i % 2 == 0) ? 80 : 60;
            groovebox.SetStepProperties(2, i, velocity: velocity);
        }

        // Open hi-hat with probability
        groovebox.SetStep(3, 2, true);
        groovebox.SetStep(3, 10, true);
        groovebox.SetStepProperties(3, 2, probability: 0.4f);
        groovebox.SetStepProperties(3, 10, probability: 0.4f);

        return groovebox;
    }

    /// <summary>
    /// Create preset: Hip-hop pattern
    /// </summary>
    public static GrooveBox CreateHipHopPattern(int? sampleRate = null)
    {
        var groovebox = new GrooveBox(sampleRate)
        {
            Name = "Hip Hop",
            Bpm = 90,
            Swing = 0.3f
        };

        // Kick pattern with swing
        groovebox.SetStep(0, 0, true);
        groovebox.SetStep(0, 3, true);
        groovebox.SetStep(0, 6, true);
        groovebox.SetStep(0, 10, true);

        // Snare on 2 and 4
        groovebox.SetStep(1, 4, true);
        groovebox.SetStep(1, 12, true);

        // Hi-hats with swing
        for (int i = 0; i < 16; i += 2)
        {
            groovebox.SetStep(2, i, true);
        }

        // Ghost snares
        groovebox.SetStep(1, 7, true);
        groovebox.SetStepProperties(1, 7, velocity: 50, probability: 0.5f);

        return groovebox;
    }

    /// <summary>
    /// Create preset: House pattern
    /// </summary>
    public static GrooveBox CreateHousePattern(int? sampleRate = null)
    {
        var groovebox = new GrooveBox(sampleRate)
        {
            Name = "House",
            Bpm = 124,
            Swing = 0.15f
        };

        // 4-on-the-floor kick
        for (int i = 0; i < 16; i += 4)
        {
            groovebox.SetStep(0, i, true);
        }

        // Clap on 2 and 4
        groovebox.SetStep(4, 4, true);
        groovebox.SetStep(4, 12, true);

        // Off-beat hi-hats (classic house)
        for (int i = 2; i < 16; i += 4)
        {
            groovebox.SetStep(3, i, true);
        }

        // Shaker/closed hi-hat
        for (int i = 0; i < 16; i++)
        {
            groovebox.SetStep(2, i, true);
            groovebox.SetStepProperties(2, i, velocity: (i % 2 == 0) ? 90 : 60);
        }

        return groovebox;
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Export groovebox state to JSON
    /// </summary>
    public string ExportToJson()
    {
        SavePatternSteps(); // Save current pad steps to pattern

        var state = new GrooveBoxState
        {
            Name = Name,
            Bpm = Bpm,
            Swing = Swing,
            Volume = Volume,
            CurrentPattern = _currentPattern,
            DelayTime = DelayTime,
            DelayFeedback = DelayFeedback,
            DelayMix = DelayMix,
            ReverbSize = ReverbSize,
            ReverbMix = ReverbMix
        };

        // Save pads (without sample data)
        foreach (var pad in _pads)
        {
            var padState = new GroovePadState
            {
                Index = pad.Index,
                Name = pad.Name,
                Note = pad.Note,
                Mode = pad.Mode,
                Pitch = pad.Pitch,
                Volume = pad.Volume,
                Pan = pad.Pan,
                FilterCutoff = pad.FilterCutoff,
                FilterResonance = pad.FilterResonance,
                Decay = pad.Decay,
                Attack = pad.Attack,
                ChokeGroup = pad.ChokeGroup,
                MuteGroup = pad.MuteGroup,
                DelaySend = pad.DelaySend,
                ReverbSend = pad.ReverbSend,
                Reverse = pad.Reverse,
                Steps = pad.Steps.ToList()
            };
            state.Pads.Add(padState);
        }

        // Save patterns
        foreach (var pattern in _patterns.Where(p => p.PadSteps.Count > 0 || p.Name != $"Pattern {p.Index + 1}"))
        {
            state.Patterns.Add(pattern);
        }

        // Save song chain
        state.SongChain.AddRange(_songChain);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        return JsonSerializer.Serialize(state, options);
    }

    /// <summary>
    /// Import groovebox state from JSON
    /// </summary>
    public void ImportFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        var state = JsonSerializer.Deserialize<GrooveBoxState>(json, options);
        if (state == null) return;

        Name = state.Name;
        Bpm = state.Bpm;
        Swing = state.Swing;
        Volume = state.Volume;
        DelayTime = state.DelayTime;
        DelayFeedback = state.DelayFeedback;
        DelayMix = state.DelayMix;
        ReverbSize = state.ReverbSize;
        ReverbMix = state.ReverbMix;

        // Load pads
        foreach (var padState in state.Pads)
        {
            if (padState.Index < 0 || padState.Index >= PadCount) continue;

            var pad = _pads[padState.Index];
            pad.Name = padState.Name;
            pad.Note = padState.Note;
            pad.Mode = padState.Mode;
            pad.Pitch = padState.Pitch;
            pad.Volume = padState.Volume;
            pad.Pan = padState.Pan;
            pad.FilterCutoff = padState.FilterCutoff;
            pad.FilterResonance = padState.FilterResonance;
            pad.Decay = padState.Decay;
            pad.Attack = padState.Attack;
            pad.ChokeGroup = padState.ChokeGroup;
            pad.MuteGroup = padState.MuteGroup;
            pad.DelaySend = padState.DelaySend;
            pad.ReverbSend = padState.ReverbSend;
            pad.Reverse = padState.Reverse;

            if (padState.Steps.Count > 0)
            {
                pad.Steps = padState.Steps.ToArray();
            }
        }

        // Load patterns
        foreach (var pattern in state.Patterns)
        {
            if (pattern.Index >= 0 && pattern.Index < MaxPatterns)
            {
                _patterns[pattern.Index] = pattern;
            }
        }

        // Load song chain
        _songChain.Clear();
        _songChain.AddRange(state.SongChain);

        // Select current pattern
        if (state.CurrentPattern >= 0 && state.CurrentPattern < MaxPatterns)
        {
            SelectPattern(state.CurrentPattern);
        }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        AllNotesOff();

        GC.SuppressFinalize(this);
    }

    #endregion
}

#region Serialization Classes

/// <summary>
/// Serialization state for GrooveBox
/// </summary>
internal class GrooveBoxState
{
    public string Name { get; set; } = "";
    public double Bpm { get; set; } = 120;
    public float Swing { get; set; }
    public float Volume { get; set; } = 0.8f;
    public int CurrentPattern { get; set; }
    public float DelayTime { get; set; }
    public float DelayFeedback { get; set; }
    public float DelayMix { get; set; }
    public float ReverbSize { get; set; }
    public float ReverbMix { get; set; }
    public List<GroovePadState> Pads { get; set; } = new();
    public List<GroovePattern> Patterns { get; set; } = new();
    public List<PatternChainEntry> SongChain { get; set; } = new();
}

/// <summary>
/// Serialization state for GroovePad (without sample data)
/// </summary>
internal class GroovePadState
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Note { get; set; }
    public PadMode Mode { get; set; }
    public float Pitch { get; set; }
    public float Volume { get; set; } = 1f;
    public float Pan { get; set; }
    public float FilterCutoff { get; set; } = 20000f;
    public float FilterResonance { get; set; }
    public float Decay { get; set; } = 0.5f;
    public float Attack { get; set; }
    public int ChokeGroup { get; set; }
    public int MuteGroup { get; set; }
    public float DelaySend { get; set; }
    public float ReverbSend { get; set; }
    public bool Reverse { get; set; }
    public List<GrooveStep> Steps { get; set; } = new();
}

#endregion
