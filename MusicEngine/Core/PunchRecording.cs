//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Manages punch in/out recording settings for recording only within specific regions.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Manages punch in/out recording settings.
/// Punch recording allows audio to be recorded only within a specific region,
/// enabling precise overdubbing and corrections.
/// </summary>
public class PunchRecording
{
    private double _punchInBeat;
    private double _punchOutBeat;
    private bool _wasInPunchRegion;

    /// <summary>
    /// Gets or sets whether punch-in is enabled.
    /// When enabled, recording will start at the punch-in point.
    /// </summary>
    public bool PunchInEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether punch-out is enabled.
    /// When enabled, recording will stop at the punch-out point.
    /// </summary>
    public bool PunchOutEnabled { get; set; }

    /// <summary>
    /// Gets or sets the punch-in point in beats.
    /// Recording will begin when playback reaches this position.
    /// </summary>
    public double PunchInBeat
    {
        get => _punchInBeat;
        set => _punchInBeat = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the punch-out point in beats.
    /// Recording will stop when playback reaches this position.
    /// </summary>
    public double PunchOutBeat
    {
        get => _punchOutBeat;
        set => _punchOutBeat = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the number of bars to play before the punch-in point (count-in).
    /// This allows the performer to hear context before recording starts.
    /// Default is 1 bar.
    /// </summary>
    public int PreRollBars { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of bars to play after the punch-out point.
    /// This allows hearing the transition after recording stops.
    /// Default is 1 bar.
    /// </summary>
    public int PostRollBars { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether auto-punch mode is enabled.
    /// When enabled, recording automatically starts at punch-in and stops at punch-out.
    /// When disabled, manual arming is required even if punch points are set.
    /// </summary>
    public bool AutoPunchEnabled { get; set; }

    /// <summary>
    /// Gets whether punch recording is active (either punch in or out is enabled).
    /// </summary>
    public bool IsActive => PunchInEnabled || PunchOutEnabled;

    /// <summary>
    /// Gets whether both punch in and out points are set and enabled.
    /// </summary>
    public bool IsPunchRangeComplete => PunchInEnabled && PunchOutEnabled && PunchOutBeat > PunchInBeat;

    /// <summary>
    /// Gets the length of the punch region in beats.
    /// Returns 0 if punch range is not complete.
    /// </summary>
    public double PunchRegionLength =>
        IsPunchRangeComplete ? PunchOutBeat - PunchInBeat : 0;

    /// <summary>
    /// Event raised when the punch-in point is triggered during playback.
    /// This indicates that recording should start.
    /// </summary>
    public event EventHandler<PunchEventArgs>? PunchInTriggered;

    /// <summary>
    /// Event raised when the punch-out point is triggered during playback.
    /// This indicates that recording should stop.
    /// </summary>
    public event EventHandler<PunchOutEventArgs>? PunchOutTriggered;

    /// <summary>
    /// Event raised when punch settings change.
    /// </summary>
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Creates a new PunchRecording instance with default settings.
    /// </summary>
    public PunchRecording()
    {
    }

    /// <summary>
    /// Creates a new PunchRecording instance with specified punch points.
    /// </summary>
    /// <param name="punchInBeat">The punch-in position in beats.</param>
    /// <param name="punchOutBeat">The punch-out position in beats.</param>
    public PunchRecording(double punchInBeat, double punchOutBeat)
    {
        PunchInBeat = punchInBeat;
        PunchOutBeat = punchOutBeat;
        PunchInEnabled = true;
        PunchOutEnabled = true;
    }

    /// <summary>
    /// Checks if the current beat position is within the punch region.
    /// </summary>
    /// <param name="currentBeat">The current playback position in beats.</param>
    /// <returns>True if currently in the punch region (recording should be active).</returns>
    public bool IsInPunchRegion(double currentBeat)
    {
        bool afterPunchIn = !PunchInEnabled || currentBeat >= PunchInBeat;
        bool beforePunchOut = !PunchOutEnabled || currentBeat <= PunchOutBeat;
        return afterPunchIn && beforePunchOut;
    }

    /// <summary>
    /// Gets the pre-roll start position (where playback should begin for punch recording).
    /// </summary>
    /// <param name="beatsPerBar">Number of beats per bar (typically 4 for 4/4 time).</param>
    /// <returns>The beat position where playback should start for pre-roll.</returns>
    public double GetPreRollStartBeat(double beatsPerBar = 4.0)
    {
        if (!PunchInEnabled)
            return 0;

        var preRollStart = PunchInBeat - (PreRollBars * beatsPerBar);
        return Math.Max(0, preRollStart);
    }

    /// <summary>
    /// Gets the post-roll end position (where playback should stop after punch recording).
    /// </summary>
    /// <param name="beatsPerBar">Number of beats per bar (typically 4 for 4/4 time).</param>
    /// <returns>The beat position where playback should stop after post-roll.</returns>
    public double GetPostRollEndBeat(double beatsPerBar = 4.0)
    {
        if (!PunchOutEnabled)
            return double.MaxValue;

        return PunchOutBeat + (PostRollBars * beatsPerBar);
    }

    /// <summary>
    /// Processes the current beat position to check for punch events.
    /// Call this during playback to trigger punch in/out events.
    /// </summary>
    /// <param name="currentBeat">The current playback position in beats.</param>
    public void Process(double currentBeat)
    {
        bool isCurrentlyInRegion = IsInPunchRegion(currentBeat);

        // Check for punch-in transition
        if (PunchInEnabled && !_wasInPunchRegion && isCurrentlyInRegion && currentBeat >= PunchInBeat)
        {
            OnPunchInTriggered(new PunchEventArgs(PunchInBeat, currentBeat));
        }

        // Check for punch-out transition
        if (PunchOutEnabled && _wasInPunchRegion && !isCurrentlyInRegion && currentBeat > PunchOutBeat)
        {
            OnPunchOutTriggered(new PunchOutEventArgs(PunchOutBeat, currentBeat, PunchRegionLength));
        }

        _wasInPunchRegion = isCurrentlyInRegion;
    }

    /// <summary>
    /// Resets the punch state tracking.
    /// Call this when starting playback or when position changes discontinuously.
    /// </summary>
    public void Reset()
    {
        _wasInPunchRegion = false;
    }

    /// <summary>
    /// Sets the punch region from a start and end beat position.
    /// </summary>
    /// <param name="startBeat">The punch-in position in beats.</param>
    /// <param name="endBeat">The punch-out position in beats.</param>
    /// <param name="enable">Whether to enable both punch in and out.</param>
    public void SetPunchRegion(double startBeat, double endBeat, bool enable = true)
    {
        PunchInBeat = Math.Min(startBeat, endBeat);
        PunchOutBeat = Math.Max(startBeat, endBeat);
        PunchInEnabled = enable;
        PunchOutEnabled = enable;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the punch region and disables punch recording.
    /// </summary>
    public void ClearPunchRegion()
    {
        PunchInEnabled = false;
        PunchOutEnabled = false;
        PunchInBeat = 0;
        PunchOutBeat = 0;
        Reset();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the PunchInTriggered event.
    /// </summary>
    protected virtual void OnPunchInTriggered(PunchEventArgs e)
    {
        PunchInTriggered?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the PunchOutTriggered event.
    /// </summary>
    protected virtual void OnPunchOutTriggered(PunchOutEventArgs e)
    {
        PunchOutTriggered?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for punch-in events.
/// </summary>
public class PunchEventArgs : EventArgs
{
    /// <summary>
    /// Gets the beat position of the punch point.
    /// </summary>
    public double Beat { get; }

    /// <summary>
    /// Gets the actual beat position when the event was triggered.
    /// May be slightly after Beat due to timing resolution.
    /// </summary>
    public double ActualBeat { get; }

    /// <summary>
    /// Gets the timestamp when the punch event occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates a new PunchEventArgs.
    /// </summary>
    /// <param name="beat">The configured punch point in beats.</param>
    /// <param name="actualBeat">The actual beat position when triggered.</param>
    public PunchEventArgs(double beat, double actualBeat)
    {
        Beat = beat;
        ActualBeat = actualBeat;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new PunchEventArgs with only the beat position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    public PunchEventArgs(double beat) : this(beat, beat)
    {
    }
}

/// <summary>
/// Event arguments for punch-out events with additional recording information.
/// </summary>
public class PunchOutEventArgs : PunchEventArgs
{
    /// <summary>
    /// Gets the duration of the recording in beats (from punch-in to punch-out).
    /// </summary>
    public double RecordedDurationBeats { get; }

    /// <summary>
    /// Creates a new PunchOutEventArgs.
    /// </summary>
    /// <param name="beat">The configured punch-out point in beats.</param>
    /// <param name="actualBeat">The actual beat position when triggered.</param>
    /// <param name="recordedDurationBeats">The duration recorded in beats.</param>
    public PunchOutEventArgs(double beat, double actualBeat, double recordedDurationBeats)
        : base(beat, actualBeat)
    {
        RecordedDurationBeats = recordedDurationBeats;
    }
}
