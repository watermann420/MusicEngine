// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Processes MPE MIDI messages and tracks per-note expression state.
/// Routes incoming MIDI to per-note expression data for MPE-enabled synthesizers.
/// </summary>
/// <remarks>
/// The MpeProcessor handles:
/// - Note On/Off messages with channel-based voice allocation
/// - Per-note pitch bend (14-bit)
/// - Slide messages (CC74)
/// - Channel pressure (aftertouch)
/// - Master channel messages (global settings)
///
/// Usage:
/// 1. Create MpeProcessor with MpeConfiguration
/// 2. Subscribe to ExpressionChanged event
/// 3. Call ProcessMidiMessage() or individual Process*() methods
/// 4. Use GetActiveNotes() or GetExpression() to query state
/// </remarks>
public class MpeProcessor : IDisposable
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<int, PerNoteExpression> _activeNotes = new();
    private readonly ConcurrentDictionary<int, int> _channelToNoteId = new();
    private bool _disposed;

    /// <summary>
    /// Gets or sets the MPE configuration.
    /// </summary>
    public MpeConfiguration Configuration { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically detect MPE mode from RPN messages.
    /// </summary>
    public bool AutoDetectMpe { get; set; } = true;

    /// <summary>
    /// Master channel pitch bend (affects all notes).
    /// </summary>
    public float MasterPitchBend { get; private set; }

    /// <summary>
    /// Master channel pitch bend range in semitones.
    /// </summary>
    public int MasterPitchBendRange { get; set; } = 2;

    /// <summary>
    /// Master channel volume (CC7).
    /// </summary>
    public float MasterVolume { get; private set; } = 1.0f;

    /// <summary>
    /// Master channel expression (CC11).
    /// </summary>
    public float MasterExpression { get; private set; } = 1.0f;

    /// <summary>
    /// Fired when any per-note expression changes.
    /// </summary>
    public event EventHandler<PerNoteExpressionEventArgs>? ExpressionChanged;

    /// <summary>
    /// Fired when a note is triggered.
    /// </summary>
    public event EventHandler<PerNoteExpressionEventArgs>? NoteTriggered;

    /// <summary>
    /// Fired when a note is released.
    /// </summary>
    public event EventHandler<PerNoteExpressionEventArgs>? NoteReleased;

    /// <summary>
    /// Fired when MPE mode is auto-detected.
    /// </summary>
    public event EventHandler<MpeConfiguration>? MpeDetected;

    /// <summary>
    /// Creates a new MPE processor with the specified configuration.
    /// </summary>
    /// <param name="configuration">MPE zone configuration, or null for auto-detect.</param>
    public MpeProcessor(MpeConfiguration? configuration = null)
    {
        Configuration = configuration ?? new MpeConfiguration();
    }

    /// <summary>
    /// Processes a raw MIDI message (1-3 bytes).
    /// </summary>
    /// <param name="message">The raw MIDI message bytes.</param>
    /// <returns>True if the message was processed as an MPE message.</returns>
    public bool ProcessMidiMessage(byte[] message)
    {
        if (message == null || message.Length < 1) return false;

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
                    if (velocity == 0)
                        return ProcessNoteOff(channel, note, 64);
                    return ProcessNoteOn(channel, note, velocity);
                }
                break;

            case 0x80: // Note Off
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    int releaseVelocity = message[2] & 0x7F;
                    return ProcessNoteOff(channel, note, releaseVelocity);
                }
                break;

            case 0xE0: // Pitch Bend
                if (message.Length >= 3)
                {
                    int lsb = message[1] & 0x7F;
                    int msb = message[2] & 0x7F;
                    int pitchBend = (msb << 7) | lsb;
                    return ProcessPitchBend(channel, pitchBend);
                }
                break;

            case 0xB0: // Control Change
                if (message.Length >= 3)
                {
                    int cc = message[1] & 0x7F;
                    int value = message[2] & 0x7F;
                    return ProcessControlChange(channel, cc, value);
                }
                break;

            case 0xD0: // Channel Pressure (Aftertouch)
                if (message.Length >= 2)
                {
                    int pressure = message[1] & 0x7F;
                    return ProcessChannelPressure(channel, pressure);
                }
                break;

            case 0xA0: // Polyphonic Aftertouch (per-note pressure)
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    int pressure = message[2] & 0x7F;
                    return ProcessPolyphonicPressure(channel, note, pressure);
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Processes a Note On message.
    /// </summary>
    public bool ProcessNoteOn(int channel, int note, int velocity)
    {
        if (!Configuration.Enabled) return false;

        // Check if this is a member channel
        if (!Configuration.IsMemberChannel(channel) && !Configuration.IsMasterChannel(channel))
        {
            return false;
        }

        // Master channel notes apply globally (not typical in MPE but handle it)
        if (Configuration.IsMasterChannel(channel))
        {
            // Could implement global note behavior here
            return false;
        }

        lock (_lock)
        {
            // Create expression data for this note
            var expression = new PerNoteExpression(channel, note, velocity, Configuration.PitchBendRange)
            {
                Slide = Configuration.DefaultSlide / 127f,
                Pressure = Configuration.DefaultPressure / 127f
            };

            int noteId = expression.NoteId;

            // Store the note and channel mapping
            _activeNotes[noteId] = expression;
            _channelToNoteId[channel] = noteId;

            // Fire events
            var args = new PerNoteExpressionEventArgs(expression, ExpressionChangeType.NoteOn);
            NoteTriggered?.Invoke(this, args);
            ExpressionChanged?.Invoke(this, args);

            return true;
        }
    }

    /// <summary>
    /// Processes a Note Off message.
    /// </summary>
    public bool ProcessNoteOff(int channel, int note, int releaseVelocity = 64)
    {
        if (!Configuration.Enabled) return false;

        int noteId = PerNoteExpression.CreateNoteId(channel, note);

        lock (_lock)
        {
            if (_activeNotes.TryGetValue(noteId, out var expression))
            {
                expression.Release(releaseVelocity);

                // Fire events
                var args = new PerNoteExpressionEventArgs(expression, ExpressionChangeType.NoteOff);
                NoteReleased?.Invoke(this, args);
                ExpressionChanged?.Invoke(this, args);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Processes a Pitch Bend message.
    /// </summary>
    public bool ProcessPitchBend(int channel, int pitchBendValue)
    {
        if (!Configuration.Enabled) return false;

        // Master channel pitch bend affects all notes
        if (Configuration.IsMasterChannel(channel))
        {
            MasterPitchBend = (pitchBendValue - 8192) / 8192f * MasterPitchBendRange;
            return true;
        }

        // Member channel pitch bend is per-note
        if (!Configuration.IsMemberChannel(channel)) return false;

        lock (_lock)
        {
            if (_channelToNoteId.TryGetValue(channel, out int noteId) &&
                _activeNotes.TryGetValue(noteId, out var expression))
            {
                expression.SetPitchBend(pitchBendValue);

                var args = new PerNoteExpressionEventArgs(expression, ExpressionChangeType.PitchBend);
                ExpressionChanged?.Invoke(this, args);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Processes a Control Change message.
    /// </summary>
    public bool ProcessControlChange(int channel, int cc, int value)
    {
        if (!Configuration.Enabled && cc != 6 && cc != 100 && cc != 101)
        {
            // Always process RPN messages for auto-detection
            if (!AutoDetectMpe) return false;
        }

        // Handle RPN for MPE configuration (auto-detect)
        if (AutoDetectMpe && (cc == 100 || cc == 101 || cc == 6))
        {
            HandleRpn(channel, cc, value);
        }

        // Master channel CCs
        if (Configuration.IsMasterChannel(channel))
        {
            switch (cc)
            {
                case 7: // Volume
                    MasterVolume = value / 127f;
                    return true;
                case 11: // Expression
                    MasterExpression = value / 127f;
                    return true;
                case 74: // Slide (global)
                    // Apply to all active notes
                    foreach (var expression in _activeNotes.Values)
                    {
                        if (expression.IsActive)
                        {
                            expression.SetSlide(value);
                        }
                    }
                    return true;
            }
        }

        // Member channel CCs (per-note)
        if (!Configuration.IsMemberChannel(channel)) return false;

        lock (_lock)
        {
            if (_channelToNoteId.TryGetValue(channel, out int noteId) &&
                _activeNotes.TryGetValue(noteId, out var expression))
            {
                switch (cc)
                {
                    case 74: // Slide (Y-axis)
                        expression.SetSlide(value);
                        ExpressionChanged?.Invoke(this,
                            new PerNoteExpressionEventArgs(expression, ExpressionChangeType.Slide));
                        return true;

                    case 1: // Mod wheel (can also be used for per-note control)
                        // Some MPE controllers send mod wheel per-note
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Processes a Channel Pressure (aftertouch) message.
    /// </summary>
    public bool ProcessChannelPressure(int channel, int pressure)
    {
        if (!Configuration.Enabled) return false;

        // Master channel pressure affects all notes
        if (Configuration.IsMasterChannel(channel))
        {
            foreach (var expression in _activeNotes.Values)
            {
                if (expression.IsActive)
                {
                    expression.SetPressure(pressure);
                }
            }
            return true;
        }

        // Member channel pressure is per-note
        if (!Configuration.IsMemberChannel(channel)) return false;

        lock (_lock)
        {
            if (_channelToNoteId.TryGetValue(channel, out int noteId) &&
                _activeNotes.TryGetValue(noteId, out var expression))
            {
                expression.SetPressure(pressure);

                var args = new PerNoteExpressionEventArgs(expression, ExpressionChangeType.Pressure);
                ExpressionChanged?.Invoke(this, args);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Processes a Polyphonic Aftertouch message (per-note pressure by note number).
    /// </summary>
    public bool ProcessPolyphonicPressure(int channel, int note, int pressure)
    {
        if (!Configuration.Enabled) return false;

        int noteId = PerNoteExpression.CreateNoteId(channel, note);

        lock (_lock)
        {
            if (_activeNotes.TryGetValue(noteId, out var expression))
            {
                expression.SetPressure(pressure);

                var args = new PerNoteExpressionEventArgs(expression, ExpressionChangeType.Pressure);
                ExpressionChanged?.Invoke(this, args);

                return true;
            }
        }

        return false;
    }

    // RPN handling state for MPE detection
    private int _rpnMsb = 127;
    private int _rpnLsb = 127;
    private int _lastRpnChannel = -1;

    private void HandleRpn(int channel, int cc, int value)
    {
        switch (cc)
        {
            case 101: // RPN MSB
                _rpnMsb = value;
                _lastRpnChannel = channel;
                break;
            case 100: // RPN LSB
                _rpnLsb = value;
                _lastRpnChannel = channel;
                break;
            case 6: // Data Entry MSB
                if (_rpnMsb == 0 && _rpnLsb == 6 && _lastRpnChannel == channel)
                {
                    // MCM (MPE Configuration Message)
                    // Channel 1 or 16, value = number of member channels
                    if (channel == 0 && value > 0)
                    {
                        // Lower Zone MPE
                        Configuration = MpeConfiguration.CreateLowerZone(value);
                        MpeDetected?.Invoke(this, Configuration);
                    }
                    else if (channel == 15 && value > 0)
                    {
                        // Upper Zone MPE
                        Configuration = MpeConfiguration.CreateUpperZone(value);
                        MpeDetected?.Invoke(this, Configuration);
                    }
                    else if (value == 0)
                    {
                        // Disable MPE for this zone
                        Configuration.Enabled = false;
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Gets the expression data for a specific note.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <param name="note">MIDI note number (0-127).</param>
    /// <returns>Expression data if found, null otherwise.</returns>
    public PerNoteExpression? GetExpression(int channel, int note)
    {
        int noteId = PerNoteExpression.CreateNoteId(channel, note);
        _activeNotes.TryGetValue(noteId, out var expression);
        return expression;
    }

    /// <summary>
    /// Gets the expression data for a note by its ID.
    /// </summary>
    /// <param name="noteId">The unique note ID.</param>
    /// <returns>Expression data if found, null otherwise.</returns>
    public PerNoteExpression? GetExpression(int noteId)
    {
        _activeNotes.TryGetValue(noteId, out var expression);
        return expression;
    }

    /// <summary>
    /// Gets the note currently playing on a specific channel.
    /// </summary>
    /// <param name="channel">MIDI channel (0-15).</param>
    /// <returns>Expression data if a note is active on that channel, null otherwise.</returns>
    public PerNoteExpression? GetNoteOnChannel(int channel)
    {
        if (_channelToNoteId.TryGetValue(channel, out int noteId))
        {
            _activeNotes.TryGetValue(noteId, out var expression);
            return expression;
        }
        return null;
    }

    /// <summary>
    /// Gets all currently active notes.
    /// </summary>
    /// <returns>Collection of active note expressions.</returns>
    public IEnumerable<PerNoteExpression> GetActiveNotes()
    {
        foreach (var expression in _activeNotes.Values)
        {
            if (expression.IsActive)
            {
                yield return expression;
            }
        }
    }

    /// <summary>
    /// Gets all notes (active and releasing).
    /// </summary>
    /// <returns>Collection of all tracked note expressions.</returns>
    public IEnumerable<PerNoteExpression> GetAllNotes()
    {
        return _activeNotes.Values;
    }

    /// <summary>
    /// Gets the number of active notes.
    /// </summary>
    public int ActiveNoteCount
    {
        get
        {
            int count = 0;
            foreach (var expression in _activeNotes.Values)
            {
                if (expression.IsActive) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Removes a note from tracking (after it has finished releasing).
    /// </summary>
    /// <param name="noteId">The note ID to remove.</param>
    public void RemoveNote(int noteId)
    {
        lock (_lock)
        {
            if (_activeNotes.TryRemove(noteId, out var expression))
            {
                _channelToNoteId.TryRemove(expression.Channel, out _);
            }
        }
    }

    /// <summary>
    /// Clears all tracked notes.
    /// </summary>
    public void ClearAllNotes()
    {
        lock (_lock)
        {
            _activeNotes.Clear();
            _channelToNoteId.Clear();
        }
    }

    /// <summary>
    /// Sends All Notes Off to release all active notes.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var expression in _activeNotes.Values)
            {
                if (expression.IsActive && !expression.IsReleasing)
                {
                    expression.Release(64);
                    var args = new PerNoteExpressionEventArgs(expression, ExpressionChangeType.NoteOff);
                    NoteReleased?.Invoke(this, args);
                    ExpressionChanged?.Invoke(this, args);
                }
            }
        }
    }

    /// <summary>
    /// Cleans up notes that have been inactive for longer than the specified duration.
    /// </summary>
    /// <param name="maxAge">Maximum age for inactive notes.</param>
    public void CleanupInactiveNotes(TimeSpan maxAge)
    {
        var now = DateTime.Now;
        var toRemove = new List<int>();

        lock (_lock)
        {
            foreach (var kvp in _activeNotes)
            {
                if (!kvp.Value.IsActive && (now - kvp.Value.LastUpdated) > maxAge)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var noteId in toRemove)
            {
                RemoveNote(noteId);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ClearAllNotes();
        GC.SuppressFinalize(this);
    }
}
