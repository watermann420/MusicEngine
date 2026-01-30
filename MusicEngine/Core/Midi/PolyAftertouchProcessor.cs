//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Polyphonic aftertouch processing and generation tool with curve shaping, LFO, and MPE support.


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Aftertouch curve types for response shaping.
/// </summary>
public enum AftertouchCurve
{
    /// <summary>Linear 1:1 response.</summary>
    Linear,
    /// <summary>Exponential curve (soft response, hard at end).</summary>
    Exponential,
    /// <summary>Logarithmic curve (quick response, soft at end).</summary>
    Logarithmic,
    /// <summary>S-curve (soft at both ends, steep in middle).</summary>
    SCurve,
    /// <summary>Reverse S-curve (steep at ends, soft in middle).</summary>
    ReverseSCurve,
    /// <summary>Square root curve (quick initial response).</summary>
    SquareRoot,
    /// <summary>Squared curve (slow initial response).</summary>
    Squared,
    /// <summary>Fixed value regardless of input.</summary>
    Fixed
}


/// <summary>
/// LFO waveform types for aftertouch modulation.
/// </summary>
public enum AftertouchLfoWaveform
{
    /// <summary>Sine wave.</summary>
    Sine,
    /// <summary>Triangle wave.</summary>
    Triangle,
    /// <summary>Sawtooth wave (ramp up).</summary>
    Sawtooth,
    /// <summary>Reverse sawtooth (ramp down).</summary>
    ReverseSawtooth,
    /// <summary>Square wave.</summary>
    Square,
    /// <summary>Sample and hold (random steps).</summary>
    SampleAndHold,
    /// <summary>Smooth random (interpolated).</summary>
    SmoothRandom
}


/// <summary>
/// Conversion mode for aftertouch processing.
/// </summary>
public enum AftertouchConversionMode
{
    /// <summary>Pass through without conversion.</summary>
    PassThrough,
    /// <summary>Convert channel pressure to poly aftertouch.</summary>
    ChannelToPoly,
    /// <summary>Convert poly aftertouch to channel pressure.</summary>
    PolyToChannel,
    /// <summary>Convert poly aftertouch to CC messages.</summary>
    PolyToCC,
    /// <summary>Convert poly aftertouch to pitch bend.</summary>
    PolyToPitchBend,
    /// <summary>Convert poly aftertouch to mod wheel (CC1).</summary>
    PolyToModWheel
}


/// <summary>
/// Keyboard zone definition for zone-based aftertouch response.
/// </summary>
public class AftertouchKeyboardZone
{
    /// <summary>
    /// Gets or sets the zone name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lowest note in the zone (0-127).
    /// </summary>
    public int LowNote { get; set; }

    /// <summary>
    /// Gets or sets the highest note in the zone (0-127).
    /// </summary>
    public int HighNote { get; set; } = 127;

    /// <summary>
    /// Gets or sets the response curve for this zone.
    /// </summary>
    public AftertouchCurve Curve { get; set; } = AftertouchCurve.Linear;

    /// <summary>
    /// Gets or sets the sensitivity multiplier (0.0-2.0).
    /// </summary>
    public float Sensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the minimum output value (0-127).
    /// </summary>
    public int MinOutput { get; set; }

    /// <summary>
    /// Gets or sets the maximum output value (0-127).
    /// </summary>
    public int MaxOutput { get; set; } = 127;

    /// <summary>
    /// Checks if a note falls within this zone.
    /// </summary>
    public bool ContainsNote(int note) => note >= LowNote && note <= HighNote;
}


/// <summary>
/// Per-note aftertouch state tracking.
/// </summary>
public class NoteAftertouchState
{
    /// <summary>MIDI channel (0-15).</summary>
    public int Channel { get; set; }

    /// <summary>MIDI note number (0-127).</summary>
    public int NoteNumber { get; set; }

    /// <summary>Current raw aftertouch value (0-127).</summary>
    public int RawValue { get; set; }

    /// <summary>Current processed aftertouch value after curve/range (0-127).</summary>
    public int ProcessedValue { get; set; }

    /// <summary>Smoothed aftertouch value (0.0-1.0).</summary>
    public float SmoothedValue { get; set; }

    /// <summary>Note velocity at trigger time.</summary>
    public int Velocity { get; set; }

    /// <summary>LFO phase for this note (0.0-1.0).</summary>
    public float LfoPhase { get; set; }

    /// <summary>Time when the note was triggered.</summary>
    public DateTime NoteOnTime { get; set; }

    /// <summary>Whether the note is in release phase.</summary>
    public bool IsReleasing { get; set; }

    /// <summary>Release aftertouch value captured at note off.</summary>
    public int ReleaseValue { get; set; }

    /// <summary>Time when note release started.</summary>
    public DateTime ReleaseTime { get; set; }

    /// <summary>Unique note ID.</summary>
    public int NoteId => (Channel * 128) + NoteNumber;
}


/// <summary>
/// Recorded aftertouch event for learn/playback mode.
/// </summary>
public class RecordedAftertouchEvent
{
    /// <summary>Time offset from recording start in milliseconds.</summary>
    public double TimeOffsetMs { get; set; }

    /// <summary>MIDI channel.</summary>
    public int Channel { get; set; }

    /// <summary>Note number.</summary>
    public int Note { get; set; }

    /// <summary>Aftertouch value.</summary>
    public int Value { get; set; }
}


/// <summary>
/// Aftertouch visualization data for UI display.
/// </summary>
public class AftertouchVisualizationData
{
    /// <summary>Per-note aftertouch values keyed by note number.</summary>
    public Dictionary<int, float> NoteValues { get; } = new();

    /// <summary>Average aftertouch across all active notes.</summary>
    public float AverageValue { get; set; }

    /// <summary>Maximum aftertouch across all active notes.</summary>
    public float MaxValue { get; set; }

    /// <summary>Number of active notes with aftertouch.</summary>
    public int ActiveNoteCount { get; set; }

    /// <summary>LFO current value (0.0-1.0).</summary>
    public float LfoValue { get; set; }

    /// <summary>Histogram of aftertouch distribution (16 bins).</summary>
    public int[] Histogram { get; } = new int[16];
}


/// <summary>
/// Event arguments for processed aftertouch output.
/// </summary>
public class AftertouchOutputEventArgs : EventArgs
{
    /// <summary>The output MIDI message bytes.</summary>
    public byte[] Message { get; }

    /// <summary>The note number (if applicable).</summary>
    public int Note { get; }

    /// <summary>The aftertouch value.</summary>
    public int Value { get; }

    /// <summary>The MIDI channel.</summary>
    public int Channel { get; }

    public AftertouchOutputEventArgs(byte[] message, int note, int value, int channel)
    {
        Message = message;
        Note = note;
        Value = value;
        Channel = channel;
    }
}


/// <summary>
/// Expression map trigger for aftertouch-based articulation switching.
/// </summary>
public class AftertouchExpressionTrigger
{
    /// <summary>Minimum aftertouch value to trigger (0-127).</summary>
    public int MinValue { get; set; }

    /// <summary>Maximum aftertouch value to trigger (0-127).</summary>
    public int MaxValue { get; set; } = 127;

    /// <summary>Articulation name to trigger.</summary>
    public string ArticulationName { get; set; } = string.Empty;

    /// <summary>Keyswitch note to send (-1 for none).</summary>
    public int KeyswitchNote { get; set; } = -1;

    /// <summary>CC number to send (-1 for none).</summary>
    public int ControlChange { get; set; } = -1;

    /// <summary>CC value to send.</summary>
    public int ControlValue { get; set; }

    /// <summary>Checks if a value falls within the trigger range.</summary>
    public bool IsTriggered(int value) => value >= MinValue && value <= MaxValue;
}


/// <summary>
/// Polyphonic aftertouch processor with conversion, curve shaping, LFO generation,
/// keyboard zones, MPE support, and learn/playback functionality.
/// </summary>
public class PolyAftertouchProcessor : IDisposable
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<int, NoteAftertouchState> _activeNotes = new();
    private readonly List<AftertouchKeyboardZone> _keyboardZones = new();
    private readonly List<RecordedAftertouchEvent> _recordedEvents = new();
    private readonly List<AftertouchExpressionTrigger> _expressionTriggers = new();
    private readonly Random _random = new();

    private bool _disposed;
    private DateTime _lastProcessTime = DateTime.UtcNow;
    private DateTime _recordingStartTime;
    private DateTime _playbackStartTime;
    private int _playbackIndex;
    private float _lfoPhase;
    private float _lastLfoValue;
    private float _smoothedLfoValue;


    #region Configuration Properties

    /// <summary>
    /// Gets or sets whether the processor is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the conversion mode.
    /// </summary>
    public AftertouchConversionMode ConversionMode { get; set; } = AftertouchConversionMode.PassThrough;

    /// <summary>
    /// Gets or sets the default aftertouch response curve.
    /// </summary>
    public AftertouchCurve DefaultCurve { get; set; } = AftertouchCurve.Linear;

    /// <summary>
    /// Gets or sets the minimum output value (0-127).
    /// </summary>
    public int MinOutput { get; set; }

    /// <summary>
    /// Gets or sets the maximum output value (0-127).
    /// </summary>
    public int MaxOutput { get; set; } = 127;

    /// <summary>
    /// Gets or sets the aftertouch threshold (values below this are ignored).
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Gets or sets the smoothing coefficient (0.0 = no smoothing, 0.99 = heavy smoothing).
    /// </summary>
    public float Smoothing { get; set; }

    /// <summary>
    /// Gets or sets the sensitivity multiplier (0.0-2.0).
    /// </summary>
    public float Sensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the fixed curve value (0-127) when using Fixed curve.
    /// </summary>
    public int FixedValue { get; set; } = 64;

    /// <summary>
    /// Gets or sets the target CC number for PolyToCC conversion mode.
    /// </summary>
    public int TargetCC { get; set; } = 11; // Expression

    /// <summary>
    /// Gets or sets the pitch bend range in semitones for PolyToPitchBend conversion.
    /// </summary>
    public float PitchBendRange { get; set; } = 2.0f;

    #endregion


    #region LFO Properties

    /// <summary>
    /// Gets or sets whether LFO generation is enabled.
    /// </summary>
    public bool LfoEnabled { get; set; }

    /// <summary>
    /// Gets or sets the LFO waveform.
    /// </summary>
    public AftertouchLfoWaveform LfoWaveform { get; set; } = AftertouchLfoWaveform.Sine;

    /// <summary>
    /// Gets or sets the LFO frequency in Hz.
    /// </summary>
    public float LfoFrequency { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the LFO depth (0.0-1.0).
    /// </summary>
    public float LfoDepth { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the LFO phase offset per note in degrees (0-360).
    /// </summary>
    public float LfoPhaseOffsetPerNote { get; set; }

    /// <summary>
    /// Gets or sets whether LFO should sync to tempo.
    /// </summary>
    public bool LfoSyncToTempo { get; set; }

    /// <summary>
    /// Gets or sets the LFO sync division (beats per cycle).
    /// </summary>
    public float LfoSyncDivision { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the current tempo in BPM for tempo sync.
    /// </summary>
    public double Tempo { get; set; } = 120.0;

    #endregion


    #region Velocity Envelope Properties

    /// <summary>
    /// Gets or sets whether to generate aftertouch from velocity envelope.
    /// </summary>
    public bool VelocityEnvelopeEnabled { get; set; }

    /// <summary>
    /// Gets or sets the attack time in milliseconds.
    /// </summary>
    public float VelocityAttackMs { get; set; } = 10.0f;

    /// <summary>
    /// Gets or sets the decay time in milliseconds.
    /// </summary>
    public float VelocityDecayMs { get; set; } = 100.0f;

    /// <summary>
    /// Gets or sets the sustain level (0.0-1.0).
    /// </summary>
    public float VelocitySustain { get; set; } = 0.7f;

    /// <summary>
    /// Gets or sets the release time in milliseconds.
    /// </summary>
    public float VelocityReleaseMs { get; set; } = 200.0f;

    #endregion


    #region Release Handling Properties

    /// <summary>
    /// Gets or sets whether to handle release aftertouch.
    /// </summary>
    public bool ReleaseAftertouchEnabled { get; set; }

    /// <summary>
    /// Gets or sets the release aftertouch fade time in milliseconds.
    /// </summary>
    public float ReleaseFadeTimeMs { get; set; } = 100.0f;

    #endregion


    #region MPE Properties

    /// <summary>
    /// Gets or sets whether MPE compatibility mode is enabled.
    /// </summary>
    public bool MpeEnabled { get; set; }

    /// <summary>
    /// Gets or sets the MPE zone (lower = 0, upper = 1).
    /// </summary>
    public int MpeZone { get; set; }

    /// <summary>
    /// Gets or sets the number of MPE member channels.
    /// </summary>
    public int MpeMemberChannels { get; set; } = 15;

    #endregion


    #region Learn Mode Properties

    /// <summary>
    /// Gets or sets whether learn mode is active (recording aftertouch).
    /// </summary>
    public bool LearnModeEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether playback mode is active.
    /// </summary>
    public bool PlaybackModeEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether playback should loop.
    /// </summary>
    public bool PlaybackLoop { get; set; }

    #endregion


    #region Random Variation Properties

    /// <summary>
    /// Gets or sets whether random variation (humanize) is enabled.
    /// </summary>
    public bool RandomVariationEnabled { get; set; }

    /// <summary>
    /// Gets or sets the random variation amount (0.0-1.0).
    /// </summary>
    public float RandomVariationAmount { get; set; } = 0.1f;

    #endregion


    #region Events

    /// <summary>
    /// Event raised when processed aftertouch output is ready.
    /// </summary>
    public event EventHandler<AftertouchOutputEventArgs>? OutputReady;

    /// <summary>
    /// Event raised when visualization data is updated.
    /// </summary>
    public event EventHandler<AftertouchVisualizationData>? VisualizationUpdated;

    /// <summary>
    /// Event raised when an expression map trigger is activated.
    /// </summary>
    public event EventHandler<AftertouchExpressionTrigger>? ExpressionTriggered;

    #endregion


    /// <summary>
    /// Creates a new polyphonic aftertouch processor.
    /// </summary>
    public PolyAftertouchProcessor()
    {
        // Add a default full-range zone
        _keyboardZones.Add(new AftertouchKeyboardZone
        {
            Name = "Default",
            LowNote = 0,
            HighNote = 127
        });
    }


    #region Zone Management

    /// <summary>
    /// Adds a keyboard zone with custom aftertouch response.
    /// </summary>
    public void AddKeyboardZone(AftertouchKeyboardZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        lock (_lock)
        {
            _keyboardZones.Add(zone);
        }
    }

    /// <summary>
    /// Removes a keyboard zone.
    /// </summary>
    public bool RemoveKeyboardZone(AftertouchKeyboardZone zone)
    {
        lock (_lock)
        {
            return _keyboardZones.Remove(zone);
        }
    }

    /// <summary>
    /// Clears all keyboard zones and resets to default.
    /// </summary>
    public void ClearKeyboardZones()
    {
        lock (_lock)
        {
            _keyboardZones.Clear();
            _keyboardZones.Add(new AftertouchKeyboardZone
            {
                Name = "Default",
                LowNote = 0,
                HighNote = 127
            });
        }
    }

    /// <summary>
    /// Gets the keyboard zones.
    /// </summary>
    public IReadOnlyList<AftertouchKeyboardZone> KeyboardZones => _keyboardZones.AsReadOnly();

    #endregion


    #region Expression Trigger Management

    /// <summary>
    /// Adds an expression map trigger.
    /// </summary>
    public void AddExpressionTrigger(AftertouchExpressionTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        lock (_lock)
        {
            _expressionTriggers.Add(trigger);
        }
    }

    /// <summary>
    /// Removes an expression trigger.
    /// </summary>
    public bool RemoveExpressionTrigger(AftertouchExpressionTrigger trigger)
    {
        lock (_lock)
        {
            return _expressionTriggers.Remove(trigger);
        }
    }

    /// <summary>
    /// Clears all expression triggers.
    /// </summary>
    public void ClearExpressionTriggers()
    {
        lock (_lock)
        {
            _expressionTriggers.Clear();
        }
    }

    #endregion


    #region MIDI Processing

    /// <summary>
    /// Processes a raw MIDI message.
    /// </summary>
    /// <param name="message">The raw MIDI message bytes.</param>
    /// <returns>True if the message was processed.</returns>
    public bool ProcessMidiMessage(byte[] message)
    {
        if (!Enabled || message == null || message.Length < 1)
            return false;

        int status = message[0];
        int channel = status & 0x0F;
        int messageType = status & 0xF0;

        switch (messageType)
        {
            case 0x90: // Note On
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    int velocity = message[2] & 0x7F;
                    if (velocity > 0)
                        return ProcessNoteOn(channel, note, velocity);
                    else
                        return ProcessNoteOff(channel, note);
                }
                break;

            case 0x80: // Note Off
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    return ProcessNoteOff(channel, note);
                }
                break;

            case 0xA0: // Poly Aftertouch
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    int pressure = message[2] & 0x7F;
                    return ProcessPolyAftertouch(channel, note, pressure);
                }
                break;

            case 0xD0: // Channel Pressure
                if (message.Length >= 2)
                {
                    int pressure = message[1] & 0x7F;
                    return ProcessChannelPressure(channel, pressure);
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Processes a Note On event.
    /// </summary>
    public bool ProcessNoteOn(int channel, int note, int velocity)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        var state = new NoteAftertouchState
        {
            Channel = channel,
            NoteNumber = note,
            Velocity = velocity,
            NoteOnTime = DateTime.UtcNow,
            LfoPhase = CalculateInitialLfoPhase(note)
        };

        int noteId = state.NoteId;
        _activeNotes[noteId] = state;

        return true;
    }

    /// <summary>
    /// Processes a Note Off event.
    /// </summary>
    public bool ProcessNoteOff(int channel, int note)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidateNote(note);

        int noteId = (channel * 128) + note;

        if (_activeNotes.TryGetValue(noteId, out var state))
        {
            if (ReleaseAftertouchEnabled)
            {
                state.IsReleasing = true;
                state.ReleaseValue = state.ProcessedValue;
                state.ReleaseTime = DateTime.UtcNow;
            }
            else
            {
                _activeNotes.TryRemove(noteId, out _);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Processes a polyphonic aftertouch message.
    /// </summary>
    public bool ProcessPolyAftertouch(int channel, int note, int pressure)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateControlValue(pressure);

        // Apply threshold
        if (pressure < Threshold)
            pressure = 0;

        int noteId = (channel * 128) + note;

        if (_activeNotes.TryGetValue(noteId, out var state))
        {
            state.RawValue = pressure;
            ProcessAftertouchValue(state);
        }
        else
        {
            // Create state for notes we may have missed
            state = new NoteAftertouchState
            {
                Channel = channel,
                NoteNumber = note,
                RawValue = pressure,
                NoteOnTime = DateTime.UtcNow,
                LfoPhase = CalculateInitialLfoPhase(note)
            };
            _activeNotes[noteId] = state;
            ProcessAftertouchValue(state);
        }

        // Record if learn mode is active
        if (LearnModeEnabled)
        {
            RecordAftertouchEvent(channel, note, state.ProcessedValue);
        }

        // Output based on conversion mode
        OutputAftertouch(state);

        return true;
    }

    /// <summary>
    /// Processes a channel pressure (aftertouch) message.
    /// </summary>
    public bool ProcessChannelPressure(int channel, int pressure)
    {
        MidiValidation.ValidateChannel(channel);
        MidiValidation.ValidateControlValue(pressure);

        if (ConversionMode == AftertouchConversionMode.ChannelToPoly)
        {
            // Convert to poly aftertouch for all active notes on this channel
            foreach (var state in _activeNotes.Values)
            {
                if (state.Channel == channel && !state.IsReleasing)
                {
                    ProcessPolyAftertouch(channel, state.NoteNumber, pressure);
                }
            }
            return true;
        }

        return false;
    }

    private void ProcessAftertouchValue(NoteAftertouchState state)
    {
        // Get zone for this note
        var zone = GetZoneForNote(state.NoteNumber);

        // Apply curve
        var curve = zone?.Curve ?? DefaultCurve;
        float normalized = state.RawValue / 127f;
        float curved = ApplyCurve(normalized, curve);

        // Apply sensitivity
        float sensitivity = zone?.Sensitivity ?? Sensitivity;
        curved *= sensitivity;
        curved = Math.Clamp(curved, 0f, 1f);

        // Apply range
        int minOut = zone?.MinOutput ?? MinOutput;
        int maxOut = zone?.MaxOutput ?? MaxOutput;
        int ranged = minOut + (int)((maxOut - minOut) * curved);

        // Apply smoothing
        if (Smoothing > 0)
        {
            state.SmoothedValue = state.SmoothedValue * Smoothing + curved * (1f - Smoothing);
            ranged = minOut + (int)((maxOut - minOut) * state.SmoothedValue);
        }
        else
        {
            state.SmoothedValue = curved;
        }

        // Apply random variation
        if (RandomVariationEnabled && RandomVariationAmount > 0)
        {
            int variation = (int)((_random.NextDouble() * 2 - 1) * RandomVariationAmount * 12);
            ranged = Math.Clamp(ranged + variation, 0, 127);
        }

        state.ProcessedValue = Math.Clamp(ranged, 0, 127);

        // Check expression triggers
        CheckExpressionTriggers(state);
    }

    private void OutputAftertouch(NoteAftertouchState state)
    {
        byte[] message;
        int channel = state.Channel;

        switch (ConversionMode)
        {
            case AftertouchConversionMode.PassThrough:
                // Output poly aftertouch
                message = new byte[] { (byte)(0xA0 | channel), (byte)state.NoteNumber, (byte)state.ProcessedValue };
                RaiseOutput(message, state.NoteNumber, state.ProcessedValue, channel);
                break;

            case AftertouchConversionMode.PolyToChannel:
                // Convert to channel pressure (use maximum of all active notes on channel)
                int maxPressure = GetMaxPressureForChannel(channel);
                message = new byte[] { (byte)(0xD0 | channel), (byte)maxPressure };
                RaiseOutput(message, -1, maxPressure, channel);
                break;

            case AftertouchConversionMode.PolyToCC:
                // Convert to CC message
                message = new byte[] { (byte)(0xB0 | channel), (byte)TargetCC, (byte)state.ProcessedValue };
                RaiseOutput(message, state.NoteNumber, state.ProcessedValue, channel);
                break;

            case AftertouchConversionMode.PolyToPitchBend:
                // Convert to pitch bend
                float bendAmount = (state.ProcessedValue / 127f) * PitchBendRange / 48f; // Normalize to -1..1 range
                int bendValue = 8192 + (int)(bendAmount * 8191);
                bendValue = Math.Clamp(bendValue, 0, 16383);
                int lsb = bendValue & 0x7F;
                int msb = (bendValue >> 7) & 0x7F;
                message = new byte[] { (byte)(0xE0 | channel), (byte)lsb, (byte)msb };
                RaiseOutput(message, state.NoteNumber, state.ProcessedValue, channel);
                break;

            case AftertouchConversionMode.PolyToModWheel:
                // Convert to mod wheel (CC1)
                message = new byte[] { (byte)(0xB0 | channel), 1, (byte)state.ProcessedValue };
                RaiseOutput(message, state.NoteNumber, state.ProcessedValue, channel);
                break;

            case AftertouchConversionMode.ChannelToPoly:
                // Already handled in ProcessChannelPressure
                message = new byte[] { (byte)(0xA0 | channel), (byte)state.NoteNumber, (byte)state.ProcessedValue };
                RaiseOutput(message, state.NoteNumber, state.ProcessedValue, channel);
                break;
        }
    }

    private void RaiseOutput(byte[] message, int note, int value, int channel)
    {
        OutputReady?.Invoke(this, new AftertouchOutputEventArgs(message, note, value, channel));
    }

    #endregion


    #region Curve Processing

    private float ApplyCurve(float input, AftertouchCurve curve)
    {
        return curve switch
        {
            AftertouchCurve.Linear => input,
            AftertouchCurve.Exponential => input * input,
            AftertouchCurve.Logarithmic => (float)Math.Sqrt(input),
            AftertouchCurve.SCurve => ApplySCurve(input),
            AftertouchCurve.ReverseSCurve => ApplyReverseSCurve(input),
            AftertouchCurve.SquareRoot => (float)Math.Sqrt(input),
            AftertouchCurve.Squared => input * input,
            AftertouchCurve.Fixed => FixedValue / 127f,
            _ => input
        };
    }

    private static float ApplySCurve(float x)
    {
        // Smooth S-curve using smoothstep
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float ApplyReverseSCurve(float x)
    {
        // Reverse S-curve
        x = Math.Clamp(x, 0f, 1f);
        if (x < 0.5f)
            return 2f * x * x;
        else
            return 1f - 2f * (1f - x) * (1f - x);
    }

    #endregion


    #region LFO Generation

    /// <summary>
    /// Processes the LFO and generates aftertouch for all active notes.
    /// Call this method periodically (e.g., from an audio callback or timer).
    /// </summary>
    /// <param name="deltaTimeMs">Time since last call in milliseconds.</param>
    public void ProcessLfo(double deltaTimeMs)
    {
        if (!LfoEnabled || deltaTimeMs <= 0)
            return;

        // Calculate LFO frequency
        float frequency = LfoFrequency;
        if (LfoSyncToTempo && Tempo > 0)
        {
            // Calculate frequency from tempo and division
            double beatsPerSecond = Tempo / 60.0;
            frequency = (float)(beatsPerSecond / LfoSyncDivision);
        }

        // Update global LFO phase
        float phaseIncrement = (float)(frequency * deltaTimeMs / 1000.0);
        _lfoPhase = (_lfoPhase + phaseIncrement) % 1.0f;

        // Calculate global LFO value
        float globalLfoValue = CalculateLfoValue(_lfoPhase);

        // Apply smoothing to LFO
        _smoothedLfoValue = _smoothedLfoValue * 0.9f + globalLfoValue * 0.1f;

        // Generate aftertouch for each active note
        foreach (var state in _activeNotes.Values)
        {
            if (state.IsReleasing)
                continue;

            // Calculate per-note LFO phase
            state.LfoPhase = (state.LfoPhase + phaseIncrement) % 1.0f;
            float notePhaseOffset = (LfoPhaseOffsetPerNote / 360f) * state.NoteNumber;
            float notePhase = (state.LfoPhase + notePhaseOffset) % 1.0f;

            float lfoValue = CalculateLfoValue(notePhase);

            // Combine with existing aftertouch
            float baseValue = state.SmoothedValue;
            float modulatedValue = baseValue + (lfoValue * LfoDepth * (1f - baseValue));
            modulatedValue = Math.Clamp(modulatedValue, 0f, 1f);

            int outputValue = MinOutput + (int)((MaxOutput - MinOutput) * modulatedValue);
            state.ProcessedValue = Math.Clamp(outputValue, 0, 127);

            // Output the modulated aftertouch
            OutputAftertouch(state);
        }

        UpdateVisualization();
    }

    private float CalculateLfoValue(float phase)
    {
        return LfoWaveform switch
        {
            AftertouchLfoWaveform.Sine => (float)(Math.Sin(phase * 2 * Math.PI) * 0.5 + 0.5),
            AftertouchLfoWaveform.Triangle => phase < 0.5f ? phase * 2f : 2f - phase * 2f,
            AftertouchLfoWaveform.Sawtooth => phase,
            AftertouchLfoWaveform.ReverseSawtooth => 1f - phase,
            AftertouchLfoWaveform.Square => phase < 0.5f ? 1f : 0f,
            AftertouchLfoWaveform.SampleAndHold => _lastLfoValue = (phase < 0.01f) ? (float)_random.NextDouble() : _lastLfoValue,
            AftertouchLfoWaveform.SmoothRandom => CalculateSmoothRandom(phase),
            _ => 0.5f
        };
    }

    private float CalculateSmoothRandom(float phase)
    {
        // Interpolate between random values
        if (phase < 0.01f)
        {
            _lastLfoValue = _smoothedLfoValue;
            _smoothedLfoValue = (float)_random.NextDouble();
        }
        return _lastLfoValue + ((_smoothedLfoValue - _lastLfoValue) * phase);
    }

    private float CalculateInitialLfoPhase(int note)
    {
        if (LfoPhaseOffsetPerNote > 0)
        {
            return (LfoPhaseOffsetPerNote / 360f * note) % 1.0f;
        }
        return _lfoPhase;
    }

    #endregion


    #region Velocity Envelope Generation

    /// <summary>
    /// Processes velocity envelope and generates aftertouch based on ADSR.
    /// Call this method periodically.
    /// </summary>
    /// <param name="deltaTimeMs">Time since last call in milliseconds.</param>
    public void ProcessVelocityEnvelope(double deltaTimeMs)
    {
        if (!VelocityEnvelopeEnabled)
            return;

        var now = DateTime.UtcNow;

        foreach (var state in _activeNotes.Values)
        {
            float envelopeValue;
            float elapsed = (float)(now - state.NoteOnTime).TotalMilliseconds;

            if (state.IsReleasing)
            {
                // Release phase
                float releaseElapsed = (float)(now - state.ReleaseTime).TotalMilliseconds;
                float releaseProgress = VelocityReleaseMs > 0 ? releaseElapsed / VelocityReleaseMs : 1f;
                releaseProgress = Math.Clamp(releaseProgress, 0f, 1f);
                envelopeValue = state.ReleaseValue / 127f * (1f - releaseProgress);

                if (releaseProgress >= 1f)
                {
                    _activeNotes.TryRemove(state.NoteId, out _);
                    continue;
                }
            }
            else if (elapsed < VelocityAttackMs)
            {
                // Attack phase
                float attackProgress = VelocityAttackMs > 0 ? elapsed / VelocityAttackMs : 1f;
                envelopeValue = attackProgress * (state.Velocity / 127f);
            }
            else if (elapsed < VelocityAttackMs + VelocityDecayMs)
            {
                // Decay phase
                float decayElapsed = elapsed - VelocityAttackMs;
                float decayProgress = VelocityDecayMs > 0 ? decayElapsed / VelocityDecayMs : 1f;
                float peakValue = state.Velocity / 127f;
                float sustainValue = peakValue * VelocitySustain;
                envelopeValue = peakValue - (peakValue - sustainValue) * decayProgress;
            }
            else
            {
                // Sustain phase
                envelopeValue = (state.Velocity / 127f) * VelocitySustain;
            }

            // Apply envelope to aftertouch
            int outputValue = MinOutput + (int)((MaxOutput - MinOutput) * envelopeValue);
            state.ProcessedValue = Math.Clamp(outputValue, 0, 127);

            OutputAftertouch(state);
        }

        UpdateVisualization();
    }

    #endregion


    #region Release Aftertouch Handling

    /// <summary>
    /// Processes release aftertouch fade for released notes.
    /// Call this method periodically.
    /// </summary>
    /// <param name="deltaTimeMs">Time since last call in milliseconds.</param>
    public void ProcessReleaseAftertouch(double deltaTimeMs)
    {
        if (!ReleaseAftertouchEnabled)
            return;

        var now = DateTime.UtcNow;
        var toRemove = new List<int>();

        foreach (var state in _activeNotes.Values)
        {
            if (!state.IsReleasing)
                continue;

            float releaseElapsed = (float)(now - state.ReleaseTime).TotalMilliseconds;
            float fadeProgress = ReleaseFadeTimeMs > 0 ? releaseElapsed / ReleaseFadeTimeMs : 1f;
            fadeProgress = Math.Clamp(fadeProgress, 0f, 1f);

            if (fadeProgress >= 1f)
            {
                toRemove.Add(state.NoteId);
                continue;
            }

            // Fade out the release value
            int fadedValue = (int)(state.ReleaseValue * (1f - fadeProgress));
            state.ProcessedValue = Math.Clamp(fadedValue, 0, 127);

            OutputAftertouch(state);
        }

        foreach (var noteId in toRemove)
        {
            _activeNotes.TryRemove(noteId, out _);
        }
    }

    #endregion


    #region Learn Mode

    /// <summary>
    /// Starts recording aftertouch events.
    /// </summary>
    public void StartRecording()
    {
        lock (_lock)
        {
            _recordedEvents.Clear();
            _recordingStartTime = DateTime.UtcNow;
            LearnModeEnabled = true;
        }
    }

    /// <summary>
    /// Stops recording aftertouch events.
    /// </summary>
    public void StopRecording()
    {
        LearnModeEnabled = false;
    }

    /// <summary>
    /// Starts playback of recorded aftertouch events.
    /// </summary>
    public void StartPlayback()
    {
        lock (_lock)
        {
            _playbackStartTime = DateTime.UtcNow;
            _playbackIndex = 0;
            PlaybackModeEnabled = true;
        }
    }

    /// <summary>
    /// Stops playback of recorded aftertouch events.
    /// </summary>
    public void StopPlayback()
    {
        PlaybackModeEnabled = false;
    }

    /// <summary>
    /// Gets the recorded aftertouch events.
    /// </summary>
    public IReadOnlyList<RecordedAftertouchEvent> RecordedEvents => _recordedEvents.AsReadOnly();

    /// <summary>
    /// Clears all recorded events.
    /// </summary>
    public void ClearRecordedEvents()
    {
        lock (_lock)
        {
            _recordedEvents.Clear();
        }
    }

    private void RecordAftertouchEvent(int channel, int note, int value)
    {
        double timeOffset = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;
        _recordedEvents.Add(new RecordedAftertouchEvent
        {
            TimeOffsetMs = timeOffset,
            Channel = channel,
            Note = note,
            Value = value
        });
    }

    /// <summary>
    /// Processes playback of recorded events.
    /// Call this method periodically during playback.
    /// </summary>
    public void ProcessPlayback()
    {
        if (!PlaybackModeEnabled || _recordedEvents.Count == 0)
            return;

        double elapsedMs = (DateTime.UtcNow - _playbackStartTime).TotalMilliseconds;

        while (_playbackIndex < _recordedEvents.Count)
        {
            var evt = _recordedEvents[_playbackIndex];
            if (evt.TimeOffsetMs <= elapsedMs)
            {
                // Play this event
                ProcessPolyAftertouch(evt.Channel, evt.Note, evt.Value);
                _playbackIndex++;
            }
            else
            {
                break;
            }
        }

        // Handle loop
        if (_playbackIndex >= _recordedEvents.Count)
        {
            if (PlaybackLoop && _recordedEvents.Count > 0)
            {
                _playbackStartTime = DateTime.UtcNow;
                _playbackIndex = 0;
            }
            else
            {
                PlaybackModeEnabled = false;
            }
        }
    }

    #endregion


    #region Expression Triggers

    private void CheckExpressionTriggers(NoteAftertouchState state)
    {
        lock (_lock)
        {
            foreach (var trigger in _expressionTriggers)
            {
                if (trigger.IsTriggered(state.ProcessedValue))
                {
                    // Output trigger messages
                    if (trigger.KeyswitchNote >= 0)
                    {
                        byte[] noteOn = { (byte)(0x90 | state.Channel), (byte)trigger.KeyswitchNote, 100 };
                        byte[] noteOff = { (byte)(0x80 | state.Channel), (byte)trigger.KeyswitchNote, 0 };
                        RaiseOutput(noteOn, trigger.KeyswitchNote, 100, state.Channel);
                        RaiseOutput(noteOff, trigger.KeyswitchNote, 0, state.Channel);
                    }

                    if (trigger.ControlChange >= 0)
                    {
                        byte[] cc = { (byte)(0xB0 | state.Channel), (byte)trigger.ControlChange, (byte)trigger.ControlValue };
                        RaiseOutput(cc, -1, trigger.ControlValue, state.Channel);
                    }

                    ExpressionTriggered?.Invoke(this, trigger);
                }
            }
        }
    }

    #endregion


    #region Visualization

    private void UpdateVisualization()
    {
        if (VisualizationUpdated == null)
            return;

        var data = GetVisualizationData();
        VisualizationUpdated?.Invoke(this, data);
    }

    /// <summary>
    /// Gets current visualization data.
    /// </summary>
    public AftertouchVisualizationData GetVisualizationData()
    {
        var data = new AftertouchVisualizationData();
        float sum = 0;
        float max = 0;
        int count = 0;

        // Clear histogram
        Array.Clear(data.Histogram, 0, data.Histogram.Length);

        foreach (var state in _activeNotes.Values)
        {
            if (state.IsReleasing)
                continue;

            float value = state.ProcessedValue / 127f;
            data.NoteValues[state.NoteNumber] = value;

            sum += value;
            if (value > max) max = value;
            count++;

            // Update histogram (16 bins)
            int bin = Math.Min(15, (int)(value * 16));
            data.Histogram[bin]++;
        }

        data.AverageValue = count > 0 ? sum / count : 0;
        data.MaxValue = max;
        data.ActiveNoteCount = count;
        data.LfoValue = _smoothedLfoValue;

        return data;
    }

    #endregion


    #region Helper Methods

    private AftertouchKeyboardZone? GetZoneForNote(int note)
    {
        lock (_lock)
        {
            // Return the first matching zone (priority order)
            foreach (var zone in _keyboardZones)
            {
                if (zone.ContainsNote(note))
                    return zone;
            }
            return null;
        }
    }

    private int GetMaxPressureForChannel(int channel)
    {
        int maxPressure = 0;
        foreach (var state in _activeNotes.Values)
        {
            if (state.Channel == channel && !state.IsReleasing)
            {
                if (state.ProcessedValue > maxPressure)
                    maxPressure = state.ProcessedValue;
            }
        }
        return maxPressure;
    }

    /// <summary>
    /// Gets all active note states.
    /// </summary>
    public IEnumerable<NoteAftertouchState> GetActiveNotes()
    {
        return _activeNotes.Values.Where(s => !s.IsReleasing);
    }

    /// <summary>
    /// Gets all notes including those in release phase.
    /// </summary>
    public IEnumerable<NoteAftertouchState> GetAllNotes()
    {
        return _activeNotes.Values;
    }

    /// <summary>
    /// Gets the aftertouch state for a specific note.
    /// </summary>
    public NoteAftertouchState? GetNoteState(int channel, int note)
    {
        int noteId = (channel * 128) + note;
        _activeNotes.TryGetValue(noteId, out var state);
        return state;
    }

    /// <summary>
    /// Clears all active notes.
    /// </summary>
    public void ClearAllNotes()
    {
        _activeNotes.Clear();
    }

    /// <summary>
    /// Resets the processor to initial state.
    /// </summary>
    public void Reset()
    {
        _activeNotes.Clear();
        _recordedEvents.Clear();
        _lfoPhase = 0;
        _lastLfoValue = 0;
        _smoothedLfoValue = 0;
        _playbackIndex = 0;
        LearnModeEnabled = false;
        PlaybackModeEnabled = false;
    }

    #endregion


    #region MPE Support

    /// <summary>
    /// Processes aftertouch in MPE mode, routing to per-note channels.
    /// </summary>
    /// <param name="masterChannel">The MPE master channel (0 or 15).</param>
    /// <param name="note">The note number.</param>
    /// <param name="pressure">The pressure value.</param>
    /// <param name="memberChannel">Output: the assigned member channel.</param>
    /// <returns>True if processed successfully.</returns>
    public bool ProcessMpeAftertouch(int masterChannel, int note, int pressure, out int memberChannel)
    {
        memberChannel = -1;

        if (!MpeEnabled)
            return false;

        // Find the member channel for this note
        foreach (var state in _activeNotes.Values)
        {
            if (state.NoteNumber == note && !state.IsReleasing)
            {
                // In MPE, the channel IS the member channel
                memberChannel = state.Channel;
                return ProcessPolyAftertouch(memberChannel, note, pressure);
            }
        }

        return false;
    }

    /// <summary>
    /// Allocates an MPE member channel for a new note.
    /// </summary>
    public int AllocateMpeMemberChannel()
    {
        if (!MpeEnabled)
            return 0;

        // Find first available member channel
        var usedChannels = new HashSet<int>();
        foreach (var state in _activeNotes.Values)
        {
            usedChannels.Add(state.Channel);
        }

        int startChannel = MpeZone == 0 ? 1 : 14; // Lower zone starts at 1, upper at 14
        int endChannel = MpeZone == 0 ? MpeMemberChannels : 15 - MpeMemberChannels;
        int direction = MpeZone == 0 ? 1 : -1;

        for (int i = 0; i < MpeMemberChannels; i++)
        {
            int channel = startChannel + (i * direction);
            if (!usedChannels.Contains(channel))
                return channel;
        }

        // All channels used, return first member channel (voice stealing)
        return startChannel;
    }

    #endregion


    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _activeNotes.Clear();
        _recordedEvents.Clear();
        _keyboardZones.Clear();
        _expressionTriggers.Clear();

        GC.SuppressFinalize(this);
    }

    #endregion
}
