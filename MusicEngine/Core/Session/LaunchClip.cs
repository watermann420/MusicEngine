// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Drawing;

namespace MusicEngine.Core.Session;

/// <summary>
/// State of a clip in the launcher grid.
/// </summary>
public enum ClipState
{
    /// <summary>No clip in slot.</summary>
    Empty,
    /// <summary>Clip is stopped.</summary>
    Stopped,
    /// <summary>Clip is queued to play on next quantize boundary.</summary>
    Queued,
    /// <summary>Clip is currently playing.</summary>
    Playing,
    /// <summary>Clip is recording.</summary>
    Recording
}

/// <summary>
/// How a clip responds to launch triggers.
/// </summary>
public enum LaunchMode
{
    /// <summary>Press to start, plays until end or stopped.</summary>
    Trigger,
    /// <summary>Plays while held, stops on release.</summary>
    Gate,
    /// <summary>Press to start/stop alternately.</summary>
    Toggle,
    /// <summary>Repeats from start on each trigger.</summary>
    Repeat
}

/// <summary>
/// Quantization for clip launch timing.
/// </summary>
public enum QuantizeMode
{
    /// <summary>Launch immediately.</summary>
    None,
    /// <summary>Launch on next bar.</summary>
    Bar,
    /// <summary>Launch on next beat.</summary>
    Beat,
    /// <summary>Launch on next eighth note.</summary>
    Eighth,
    /// <summary>Launch on next sixteenth note.</summary>
    Sixteenth
}

/// <summary>
/// What action to take after a clip finishes playing.
/// </summary>
public enum FollowActionType
{
    /// <summary>No follow action.</summary>
    None,
    /// <summary>Play the next clip in the track.</summary>
    Next,
    /// <summary>Play the previous clip in the track.</summary>
    Previous,
    /// <summary>Play the first clip in the track.</summary>
    First,
    /// <summary>Play the last clip in the track.</summary>
    Last,
    /// <summary>Play a random clip in the track.</summary>
    Random,
    /// <summary>Play a specific other clip (set via FollowActionOtherIndex).</summary>
    Other
}

/// <summary>
/// Represents a launchable clip containing either MIDI or audio data.
/// Used in session view / clip launcher grids similar to Ableton Live.
/// </summary>
public class LaunchClip
{
    /// <summary>Unique identifier for this clip.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name of the clip.</summary>
    public string Name { get; set; } = "";

    /// <summary>Current playback state.</summary>
    public ClipState State { get; internal set; } = ClipState.Stopped;

    /// <summary>How the clip responds to launch triggers.</summary>
    public LaunchMode LaunchMode { get; set; } = LaunchMode.Trigger;

    /// <summary>Quantization mode for launch timing.</summary>
    public QuantizeMode Quantize { get; set; } = QuantizeMode.Bar;

    /// <summary>Length of the clip in beats.</summary>
    public double LengthBeats { get; set; } = 4.0;

    /// <summary>Color for visual identification in the UI.</summary>
    public Color Color { get; set; } = Color.FromArgb(255, 100, 149, 237); // Cornflower blue

    /// <summary>MIDI pattern data (null if audio clip).</summary>
    public Pattern? MidiPattern { get; set; }

    /// <summary>Audio sample data (null if MIDI clip).</summary>
    public float[]? AudioData { get; set; }

    /// <summary>Sample rate of the audio data.</summary>
    public int AudioSampleRate { get; set; } = 44100;

    /// <summary>Number of audio channels (1=mono, 2=stereo).</summary>
    public int AudioChannels { get; set; } = 2;

    /// <summary>True if this clip contains audio data.</summary>
    public bool IsAudio => AudioData != null;

    /// <summary>True if this clip contains MIDI data.</summary>
    public bool IsMidi => MidiPattern != null;

    /// <summary>Current playback position in beats.</summary>
    public double PlayPosition { get; internal set; }

    /// <summary>Whether to execute a follow action after playback.</summary>
    public bool FollowAction { get; set; }

    /// <summary>Type of follow action to execute.</summary>
    public FollowActionType FollowActionType { get; set; } = FollowActionType.None;

    /// <summary>Time in beats after which follow action triggers.</summary>
    public double FollowTime { get; set; } = 4.0;

    /// <summary>Index of other clip for FollowActionType.Other.</summary>
    public int FollowActionOtherIndex { get; set; }

    /// <summary>Probability (0-1) that follow action will execute.</summary>
    public double FollowActionChance { get; set; } = 1.0;

    /// <summary>Whether the clip loops.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Start offset in beats for playback.</summary>
    public double StartOffset { get; set; }

    /// <summary>End offset in beats (0 = use full length).</summary>
    public double EndOffset { get; set; }

    /// <summary>Velocity/volume scaling (0-1).</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>Track index this clip belongs to.</summary>
    internal int TrackIndex { get; set; } = -1;

    /// <summary>Scene index this clip belongs to.</summary>
    internal int SceneIndex { get; set; } = -1;

    /// <summary>Reference to parent ClipLauncher.</summary>
    internal ClipLauncher? Launcher { get; set; }

    /// <summary>Time accumulated for follow action trigger.</summary>
    internal double FollowActionAccumulator { get; set; }

    /// <summary>
    /// Creates a new empty launch clip.
    /// </summary>
    public LaunchClip()
    {
    }

    /// <summary>
    /// Creates a new MIDI launch clip from a pattern.
    /// </summary>
    /// <param name="pattern">The MIDI pattern.</param>
    /// <param name="name">Optional name for the clip.</param>
    public LaunchClip(Pattern pattern, string? name = null)
    {
        MidiPattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        LengthBeats = pattern.LoopLength;
        Name = name ?? pattern.Name;
        if (string.IsNullOrEmpty(Name))
        {
            Name = $"MIDI Clip {Id.ToString()[..8]}";
        }
    }

    /// <summary>
    /// Creates a new audio launch clip from sample data.
    /// </summary>
    /// <param name="audioData">The audio sample data (interleaved if stereo).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="bpm">BPM to calculate length in beats.</param>
    /// <param name="name">Optional name for the clip.</param>
    public LaunchClip(float[] audioData, int sampleRate, int channels, double bpm, string? name = null)
    {
        AudioData = audioData ?? throw new ArgumentNullException(nameof(audioData));
        AudioSampleRate = sampleRate;
        AudioChannels = channels;

        // Calculate length in beats from audio duration
        double durationSeconds = (double)audioData.Length / (sampleRate * channels);
        LengthBeats = durationSeconds * (bpm / 60.0);

        Name = name ?? $"Audio Clip {Id.ToString()[..8]}";
    }

    /// <summary>
    /// Resets playback position to start.
    /// </summary>
    public void Reset()
    {
        PlayPosition = StartOffset;
        FollowActionAccumulator = 0;
    }

    /// <summary>
    /// Gets the effective length considering start/end offsets.
    /// </summary>
    public double EffectiveLength =>
        (EndOffset > 0 ? EndOffset : LengthBeats) - StartOffset;

    /// <summary>
    /// Creates a copy of this clip.
    /// </summary>
    public LaunchClip Clone()
    {
        var clone = new LaunchClip
        {
            Name = Name + " (Copy)",
            State = ClipState.Stopped,
            LaunchMode = LaunchMode,
            Quantize = Quantize,
            LengthBeats = LengthBeats,
            Color = Color,
            MidiPattern = MidiPattern, // Reference, not deep copy
            AudioData = AudioData != null ? (float[])AudioData.Clone() : null,
            AudioSampleRate = AudioSampleRate,
            AudioChannels = AudioChannels,
            FollowAction = FollowAction,
            FollowActionType = FollowActionType,
            FollowTime = FollowTime,
            FollowActionOtherIndex = FollowActionOtherIndex,
            FollowActionChance = FollowActionChance,
            Loop = Loop,
            StartOffset = StartOffset,
            EndOffset = EndOffset,
            Gain = Gain
        };
        return clone;
    }
}
