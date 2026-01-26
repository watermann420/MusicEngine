// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI clock sync.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NAudio.Midi;


namespace MusicEngine.Core;


/// <summary>
/// MIDI clock message types as per the MIDI specification.
/// </summary>
public enum MidiClockMessageType
{
    /// <summary>Timing Clock (0xF8) - 24 pulses per quarter note.</summary>
    TimingClock = 0xF8,

    /// <summary>Start (0xFA) - Start playback from the beginning.</summary>
    Start = 0xFA,

    /// <summary>Continue (0xFB) - Continue playback from current position.</summary>
    Continue = 0xFB,

    /// <summary>Stop (0xFC) - Stop playback.</summary>
    Stop = 0xFC,

    /// <summary>Song Position Pointer (0xF2) - Set playback position.</summary>
    SongPositionPointer = 0xF2,

    /// <summary>Song Select (0xF3) - Select a song/pattern.</summary>
    SongSelect = 0xF3
}

/// <summary>
/// MIDI clock sync mode - whether this instance sends or receives clock.
/// </summary>
public enum MidiClockMode
{
    /// <summary>Internal clock - generate MIDI clock from internal tempo.</summary>
    Internal,

    /// <summary>External clock - synchronize to incoming MIDI clock.</summary>
    External,

    /// <summary>Both - generate clock internally while also receiving external clock.</summary>
    Both
}

/// <summary>
/// Event arguments for MIDI clock events.
/// </summary>
public class MidiClockEventArgs : EventArgs
{
    /// <summary>The type of MIDI clock message.</summary>
    public MidiClockMessageType MessageType { get; }

    /// <summary>The current beat position (for Song Position Pointer).</summary>
    public double BeatPosition { get; }

    /// <summary>The estimated BPM from external clock.</summary>
    public double EstimatedBpm { get; }

    /// <summary>The MIDI device index that sent the message.</summary>
    public int DeviceIndex { get; }

    /// <summary>Timestamp of the event.</summary>
    public double Timestamp { get; }

    public MidiClockEventArgs(MidiClockMessageType messageType, double beatPosition, double estimatedBpm, int deviceIndex, double timestamp)
    {
        MessageType = messageType;
        BeatPosition = beatPosition;
        EstimatedBpm = estimatedBpm;
        DeviceIndex = deviceIndex;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Provides MIDI clock synchronization for professional-grade timing.
/// Supports 24 PPQN (pulses per quarter note) as per MIDI standard.
/// </summary>
public class MidiClockSync : IDisposable
{
    /// <summary>Standard MIDI clock pulses per quarter note.</summary>
    public const int PulsesPerQuarterNote = 24;

    /// <summary>MIDI beats per Song Position Pointer unit (16th notes, so 4 per beat).</summary>
    public const int MidiBeatsPerBeat = 4;

    /// <summary>Song Position Pointer units per quarter note.</summary>
    public const int SppUnitsPerBeat = 4;

    private readonly object _lock = new();
    private readonly Stopwatch _stopwatch;
    private readonly HighResolutionTimer _clockTimer;

    // Clock state
    private MidiClockMode _mode = MidiClockMode.Internal;
    private double _bpm = 120.0;
    private bool _isRunning;
    private bool _disposed;

    // Internal clock generation
    private long _pulseCount;
    private readonly List<int> _outputDeviceIndices = new();

    // External clock tracking
    private readonly CircularBuffer<double> _externalPulseIntervals;
    private double _lastExternalPulseTime;
    private double _estimatedExternalBpm;
    private int _externalPulseCount;
    private bool _externalClockRunning;

    // Song Position
    private int _songPositionPointer; // In MIDI beat units (6 MIDI clocks per unit)
    private double _currentBeatPosition;

    // MIDI output references (managed externally by AudioEngine)
    private readonly Dictionary<int, MidiOut> _midiOutputs = new();

    /// <summary>
    /// Fired when a MIDI clock pulse is generated or received.
    /// </summary>
    public event EventHandler<MidiClockEventArgs>? ClockPulse;

    /// <summary>
    /// Fired when a Start message is received or sent.
    /// </summary>
    public event EventHandler<MidiClockEventArgs>? Started;

    /// <summary>
    /// Fired when a Stop message is received or sent.
    /// </summary>
    public event EventHandler<MidiClockEventArgs>? Stopped;

    /// <summary>
    /// Fired when a Continue message is received or sent.
    /// </summary>
    public event EventHandler<MidiClockEventArgs>? Continued;

    /// <summary>
    /// Fired when the Song Position Pointer changes.
    /// </summary>
    public event EventHandler<MidiClockEventArgs>? PositionChanged;

    /// <summary>
    /// Fired when external clock BPM is detected/updated.
    /// </summary>
    public event EventHandler<MidiClockEventArgs>? ExternalBpmDetected;

    /// <summary>
    /// Gets or sets the clock mode.
    /// </summary>
    public MidiClockMode Mode
    {
        get => _mode;
        set
        {
            lock (_lock)
            {
                _mode = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the internal BPM. Only used when Mode is Internal or Both.
    /// </summary>
    public double Bpm
    {
        get => _bpm;
        set
        {
            lock (_lock)
            {
                _bpm = Math.Max(20.0, Math.Min(300.0, value));
                UpdateClockInterval();
            }
        }
    }

    /// <summary>
    /// Gets the estimated BPM from external clock source.
    /// </summary>
    public double ExternalBpm => _estimatedExternalBpm;

    /// <summary>
    /// Gets the effective BPM (external if in External mode, internal otherwise).
    /// </summary>
    public double EffectiveBpm => _mode == MidiClockMode.External ? _estimatedExternalBpm : _bpm;

    /// <summary>
    /// Gets whether the clock is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the current beat position.
    /// </summary>
    public double CurrentBeatPosition => _currentBeatPosition;

    /// <summary>
    /// Gets the current Song Position Pointer value.
    /// </summary>
    public int SongPositionPointer => _songPositionPointer;

    /// <summary>
    /// Gets the total pulse count since start.
    /// </summary>
    public long PulseCount => _pulseCount;

    /// <summary>
    /// Creates a new MIDI clock sync instance.
    /// </summary>
    public MidiClockSync()
    {
        _stopwatch = new Stopwatch();
        _externalPulseIntervals = new CircularBuffer<double>(48); // Track 2 beats worth of pulses

        // Create high-resolution timer for clock generation
        _clockTimer = new HighResolutionTimer();
        _clockTimer.Tick += OnClockTimerTick;

        UpdateClockInterval();
    }

    /// <summary>
    /// Adds a MIDI output device for sending clock messages.
    /// </summary>
    /// <param name="deviceIndex">The MIDI output device index.</param>
    /// <param name="midiOut">The MidiOut instance.</param>
    public void AddMidiOutput(int deviceIndex, MidiOut midiOut)
    {
        lock (_lock)
        {
            _midiOutputs[deviceIndex] = midiOut;
            if (!_outputDeviceIndices.Contains(deviceIndex))
            {
                _outputDeviceIndices.Add(deviceIndex);
            }
        }
    }

    /// <summary>
    /// Removes a MIDI output device from clock sending.
    /// </summary>
    public void RemoveMidiOutput(int deviceIndex)
    {
        lock (_lock)
        {
            _midiOutputs.Remove(deviceIndex);
            _outputDeviceIndices.Remove(deviceIndex);
        }
    }

    /// <summary>
    /// Enables or disables clock sending to a specific output.
    /// </summary>
    public void SetOutputEnabled(int deviceIndex, bool enabled)
    {
        lock (_lock)
        {
            if (enabled && !_outputDeviceIndices.Contains(deviceIndex))
            {
                _outputDeviceIndices.Add(deviceIndex);
            }
            else if (!enabled)
            {
                _outputDeviceIndices.Remove(deviceIndex);
            }
        }
    }

    /// <summary>
    /// Starts the MIDI clock.
    /// </summary>
    /// <param name="fromBeginning">If true, sends Start message. If false, sends Continue.</param>
    public void Start(bool fromBeginning = true)
    {
        lock (_lock)
        {
            if (_isRunning) return;

            _isRunning = true;

            if (fromBeginning)
            {
                _pulseCount = 0;
                _currentBeatPosition = 0;
                _songPositionPointer = 0;
            }

            _stopwatch.Restart();

            // Send Start or Continue message
            if (_mode != MidiClockMode.External)
            {
                if (fromBeginning)
                {
                    SendMidiMessage(MidiClockMessageType.Start);
                    Started?.Invoke(this, CreateEventArgs(MidiClockMessageType.Start));
                }
                else
                {
                    SendMidiMessage(MidiClockMessageType.Continue);
                    Continued?.Invoke(this, CreateEventArgs(MidiClockMessageType.Continue));
                }

                _clockTimer.Start();
            }
        }
    }

    /// <summary>
    /// Stops the MIDI clock.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            _isRunning = false;

            if (_mode != MidiClockMode.External)
            {
                _clockTimer.Stop();
                SendMidiMessage(MidiClockMessageType.Stop);
            }

            _stopwatch.Stop();
            Stopped?.Invoke(this, CreateEventArgs(MidiClockMessageType.Stop));
        }
    }

    /// <summary>
    /// Sets the song position in beats.
    /// </summary>
    /// <param name="beatPosition">The beat position to set.</param>
    public void SetPosition(double beatPosition)
    {
        lock (_lock)
        {
            _currentBeatPosition = Math.Max(0, beatPosition);

            // Convert to Song Position Pointer (in 16th note units)
            // SPP = beat * 4 (since SPP counts 16th notes)
            _songPositionPointer = (int)(_currentBeatPosition * SppUnitsPerBeat);

            // Update pulse count to match
            _pulseCount = (long)(_currentBeatPosition * PulsesPerQuarterNote);

            // Send Song Position Pointer message
            if (_mode != MidiClockMode.External)
            {
                SendSongPositionPointer(_songPositionPointer);
            }

            PositionChanged?.Invoke(this, CreateEventArgs(MidiClockMessageType.SongPositionPointer));
        }
    }

    /// <summary>
    /// Processes an incoming MIDI message for clock synchronization.
    /// Call this from your MIDI input handler.
    /// </summary>
    /// <param name="deviceIndex">The device index that received the message.</param>
    /// <param name="midiEvent">The MIDI event received.</param>
    public void ProcessIncomingMidi(int deviceIndex, MidiEvent midiEvent)
    {
        if (_mode == MidiClockMode.Internal) return;

        // Handle system real-time messages
        int status = (int)midiEvent.CommandCode;

        switch (status)
        {
            case 0xF8: // Timing Clock
                HandleExternalClockPulse(deviceIndex);
                break;

            case 0xFA: // Start
                HandleExternalStart(deviceIndex);
                break;

            case 0xFB: // Continue
                HandleExternalContinue(deviceIndex);
                break;

            case 0xFC: // Stop
                HandleExternalStop(deviceIndex);
                break;

            case 0xF2: // Song Position Pointer
                // Song Position Pointer is a system common message with 2 data bytes
                // NAudio doesn't expose SPP directly through MidiEvent, use ProcessRawMidiMessage instead
                break;
        }
    }

    /// <summary>
    /// Processes a raw MIDI message (status byte + data).
    /// </summary>
    public void ProcessRawMidiMessage(int deviceIndex, int status, int data1 = 0, int data2 = 0)
    {
        if (_mode == MidiClockMode.Internal) return;

        switch (status)
        {
            case 0xF8: // Timing Clock
                HandleExternalClockPulse(deviceIndex);
                break;

            case 0xFA: // Start
                HandleExternalStart(deviceIndex);
                break;

            case 0xFB: // Continue
                HandleExternalContinue(deviceIndex);
                break;

            case 0xFC: // Stop
                HandleExternalStop(deviceIndex);
                break;

            case 0xF2: // Song Position Pointer
                int spp = data1 | (data2 << 7);
                HandleSongPositionPointer(deviceIndex, spp);
                break;
        }
    }

    private void HandleExternalClockPulse(int deviceIndex)
    {
        double currentTime = _stopwatch.Elapsed.TotalSeconds;

        if (_externalClockRunning)
        {
            // Calculate interval since last pulse
            double interval = currentTime - _lastExternalPulseTime;

            if (interval > 0 && interval < 1.0) // Sanity check
            {
                _externalPulseIntervals.Push(interval);

                // Estimate BPM from average interval
                double avgInterval = _externalPulseIntervals.Average();
                if (avgInterval > 0)
                {
                    double newBpm = 60.0 / (avgInterval * PulsesPerQuarterNote);

                    // Smooth BPM estimation
                    if (_estimatedExternalBpm > 0)
                    {
                        _estimatedExternalBpm = _estimatedExternalBpm * 0.9 + newBpm * 0.1;
                    }
                    else
                    {
                        _estimatedExternalBpm = newBpm;
                    }

                    ExternalBpmDetected?.Invoke(this, new MidiClockEventArgs(
                        MidiClockMessageType.TimingClock,
                        _currentBeatPosition,
                        _estimatedExternalBpm,
                        deviceIndex,
                        currentTime
                    ));
                }
            }
        }

        _lastExternalPulseTime = currentTime;
        _externalPulseCount++;

        // Update beat position
        _currentBeatPosition = (double)_externalPulseCount / PulsesPerQuarterNote;

        ClockPulse?.Invoke(this, new MidiClockEventArgs(
            MidiClockMessageType.TimingClock,
            _currentBeatPosition,
            _estimatedExternalBpm,
            deviceIndex,
            currentTime
        ));
    }

    private void HandleExternalStart(int deviceIndex)
    {
        lock (_lock)
        {
            _externalClockRunning = true;
            _externalPulseCount = 0;
            _currentBeatPosition = 0;
            _externalPulseIntervals.Clear();
            _stopwatch.Restart();
            _isRunning = true;
        }

        Started?.Invoke(this, new MidiClockEventArgs(
            MidiClockMessageType.Start,
            0,
            _estimatedExternalBpm,
            deviceIndex,
            _stopwatch.Elapsed.TotalSeconds
        ));
    }

    private void HandleExternalContinue(int deviceIndex)
    {
        lock (_lock)
        {
            _externalClockRunning = true;
            _isRunning = true;
        }

        Continued?.Invoke(this, new MidiClockEventArgs(
            MidiClockMessageType.Continue,
            _currentBeatPosition,
            _estimatedExternalBpm,
            deviceIndex,
            _stopwatch.Elapsed.TotalSeconds
        ));
    }

    private void HandleExternalStop(int deviceIndex)
    {
        lock (_lock)
        {
            _externalClockRunning = false;
            _isRunning = false;
        }

        Stopped?.Invoke(this, new MidiClockEventArgs(
            MidiClockMessageType.Stop,
            _currentBeatPosition,
            _estimatedExternalBpm,
            deviceIndex,
            _stopwatch.Elapsed.TotalSeconds
        ));
    }

    private void HandleSongPositionPointer(int deviceIndex, int spp)
    {
        lock (_lock)
        {
            _songPositionPointer = spp;
            // Convert SPP (16th notes) to beats (quarter notes)
            _currentBeatPosition = (double)spp / SppUnitsPerBeat;
            _externalPulseCount = (int)(_currentBeatPosition * PulsesPerQuarterNote);
        }

        PositionChanged?.Invoke(this, new MidiClockEventArgs(
            MidiClockMessageType.SongPositionPointer,
            _currentBeatPosition,
            _estimatedExternalBpm,
            deviceIndex,
            _stopwatch.Elapsed.TotalSeconds
        ));
    }

    private void OnClockTimerTick(object? sender, TimerTickEventArgs e)
    {
        if (!_isRunning || _mode == MidiClockMode.External) return;

        // Calculate how many pulses should have occurred by now
        double expectedPulses = e.CurrentTime * (_bpm / 60.0) * PulsesPerQuarterNote;

        while (_pulseCount < expectedPulses)
        {
            _pulseCount++;
            _currentBeatPosition = (double)_pulseCount / PulsesPerQuarterNote;

            // Send timing clock to all outputs
            SendMidiMessage(MidiClockMessageType.TimingClock);

            ClockPulse?.Invoke(this, new MidiClockEventArgs(
                MidiClockMessageType.TimingClock,
                _currentBeatPosition,
                _bpm,
                -1,
                e.CurrentTime
            ));
        }
    }

    private void UpdateClockInterval()
    {
        // Calculate tick interval for precise clock generation
        // We need 24 pulses per beat, so interval = (60 / BPM / 24) seconds
        // But we tick more frequently and check if it's time to send a pulse
        double pulsesPerSecond = (_bpm / 60.0) * PulsesPerQuarterNote;

        // Tick at 4x the pulse rate for smooth timing
        _clockTimer.TargetTicksPerSecond = pulsesPerSecond * 4;
    }

    private void SendMidiMessage(MidiClockMessageType messageType)
    {
        int status = (int)messageType;

        lock (_lock)
        {
            foreach (var deviceIndex in _outputDeviceIndices)
            {
                if (_midiOutputs.TryGetValue(deviceIndex, out var midiOut))
                {
                    try
                    {
                        midiOut.Send(status);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending MIDI clock to device {deviceIndex}: {ex.Message}");
                    }
                }
            }
        }
    }

    private void SendSongPositionPointer(int spp)
    {
        // Song Position Pointer: F2 LL HH
        // LL = LSB (bits 0-6), HH = MSB (bits 7-13)
        int lsb = spp & 0x7F;
        int msb = (spp >> 7) & 0x7F;
        int message = 0xF2 | (lsb << 8) | (msb << 16);

        lock (_lock)
        {
            foreach (var deviceIndex in _outputDeviceIndices)
            {
                if (_midiOutputs.TryGetValue(deviceIndex, out var midiOut))
                {
                    try
                    {
                        midiOut.Send(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending SPP to device {deviceIndex}: {ex.Message}");
                    }
                }
            }
        }
    }

    private MidiClockEventArgs CreateEventArgs(MidiClockMessageType messageType)
    {
        return new MidiClockEventArgs(
            messageType,
            _currentBeatPosition,
            _mode == MidiClockMode.External ? _estimatedExternalBpm : _bpm,
            -1,
            _stopwatch.Elapsed.TotalSeconds
        );
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _clockTimer.Dispose();

        GC.SuppressFinalize(this);
    }

    ~MidiClockSync()
    {
        Dispose();
    }
}
