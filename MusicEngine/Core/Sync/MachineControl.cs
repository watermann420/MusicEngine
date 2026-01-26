// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MMC/MTC machine control.

using System.Diagnostics;
using NAudio.Midi;

namespace MusicEngine.Core.Sync;

/// <summary>
/// SMPTE/MTC frame rate options.
/// </summary>
public enum MtcFrameRate
{
    /// <summary>
    /// 24 frames per second (film).
    /// </summary>
    Fps24 = 0,

    /// <summary>
    /// 25 frames per second (PAL video).
    /// </summary>
    Fps25 = 1,

    /// <summary>
    /// 29.97 frames per second drop-frame (NTSC video).
    /// </summary>
    Fps2997DropFrame = 2,

    /// <summary>
    /// 30 frames per second (audio/NTSC non-drop).
    /// </summary>
    Fps30 = 3
}

/// <summary>
/// MMC command types as per MIDI specification.
/// </summary>
public enum MmcCommand : byte
{
    /// <summary>Stop playback.</summary>
    Stop = 0x01,

    /// <summary>Start playback.</summary>
    Play = 0x02,

    /// <summary>Fast forward.</summary>
    DeferredPlay = 0x03,

    /// <summary>Fast forward.</summary>
    FastForward = 0x04,

    /// <summary>Rewind.</summary>
    Rewind = 0x05,

    /// <summary>Start recording.</summary>
    RecordStrobe = 0x06,

    /// <summary>Exit recording.</summary>
    RecordExit = 0x07,

    /// <summary>Pause recording.</summary>
    RecordPause = 0x08,

    /// <summary>Pause playback.</summary>
    Pause = 0x09,

    /// <summary>Eject media.</summary>
    Eject = 0x0A,

    /// <summary>Chase/locate mode.</summary>
    Chase = 0x0B,

    /// <summary>Reset device.</summary>
    Reset = 0x0D,

    /// <summary>Write data.</summary>
    Write = 0x40,

    /// <summary>Locate to position.</summary>
    Locate = 0x44,

    /// <summary>Shuttle transport.</summary>
    Shuttle = 0x47
}

/// <summary>
/// Event arguments for MMC commands.
/// </summary>
public class MmcCommandEventArgs : EventArgs
{
    /// <summary>The MMC command received.</summary>
    public MmcCommand Command { get; }

    /// <summary>Device ID (0x7F = all devices).</summary>
    public byte DeviceId { get; }

    /// <summary>Optional locate position in frames.</summary>
    public long? LocatePosition { get; }

    /// <summary>Timestamp of the event.</summary>
    public double Timestamp { get; }

    /// <summary>
    /// Creates new MMC command event args.
    /// </summary>
    public MmcCommandEventArgs(MmcCommand command, byte deviceId, long? locatePosition, double timestamp)
    {
        Command = command;
        DeviceId = deviceId;
        LocatePosition = locatePosition;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Event arguments for MTC time code updates.
/// </summary>
public class MtcTimeEventArgs : EventArgs
{
    /// <summary>Hours (0-23).</summary>
    public int Hours { get; }

    /// <summary>Minutes (0-59).</summary>
    public int Minutes { get; }

    /// <summary>Seconds (0-59).</summary>
    public int Seconds { get; }

    /// <summary>Frames (0-29 depending on frame rate).</summary>
    public int Frames { get; }

    /// <summary>Frame rate.</summary>
    public MtcFrameRate FrameRate { get; }

    /// <summary>Total position in frames.</summary>
    public long TotalFrames { get; }

    /// <summary>Position in seconds.</summary>
    public double PositionSeconds { get; }

    /// <summary>
    /// Creates new MTC time event args.
    /// </summary>
    public MtcTimeEventArgs(int hours, int minutes, int seconds, int frames,
                            MtcFrameRate frameRate, long totalFrames, double positionSeconds)
    {
        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
        FrameRate = frameRate;
        TotalFrames = totalFrames;
        PositionSeconds = positionSeconds;
    }
}

/// <summary>
/// MIDI Machine Control (MMC) and MIDI Time Code (MTC) implementation.
/// Provides synchronization with external hardware and software via standard MIDI protocols.
/// </summary>
/// <remarks>
/// Features:
/// - MMC transport commands (play, stop, record, locate)
/// - MTC generation and reception
/// - Multiple frame rates (24, 25, 29.97, 30 fps)
/// - Offset adjustment for sync alignment
/// - Events for external control integration
/// </remarks>
public class MachineControl : IDisposable
{
    private const byte SysexStart = 0xF0;
    private const byte SysexEnd = 0xF7;
    private const byte MmcManufacturer = 0x7F; // Universal Real-Time
    private const byte MmcSubId1 = 0x06; // MMC Command
    private const byte MmcSubId2 = 0x01; // Information Field

    // Timing
    private readonly Stopwatch _stopwatch;
    private readonly HighResolutionTimer _mtcTimer;
    private readonly object _lock = new();

    // State
    private bool _isRunning;
    private bool _isGeneratingMtc;
    private bool _disposed;
    private MtcFrameRate _frameRate = MtcFrameRate.Fps30;

    // MTC position tracking
    private int _hours;
    private int _minutes;
    private int _seconds;
    private int _frames;
    private int _quarterFrameIndex;
    private long _totalFrames;

    // Offset for sync alignment
    private double _offsetSeconds;

    // MIDI I/O
    private readonly Dictionary<int, MidiOut> _midiOutputs = new();
    private readonly List<int> _outputDeviceIndices = new();
    private byte _deviceId = 0x7F; // All devices

    /// <summary>
    /// Fired when an MMC command is received.
    /// </summary>
    public event EventHandler<MmcCommandEventArgs>? CommandReceived;

    /// <summary>
    /// Fired when MTC time updates.
    /// </summary>
    public event EventHandler<MtcTimeEventArgs>? TimeCodeReceived;

    /// <summary>
    /// Fired when transport state changes.
    /// </summary>
    public event EventHandler<bool>? TransportStateChanged;

    /// <summary>
    /// Gets or sets the SMPTE frame rate.
    /// </summary>
    public MtcFrameRate FrameRate
    {
        get => _frameRate;
        set => _frameRate = value;
    }

    /// <summary>
    /// Gets the actual frames per second for the current frame rate.
    /// </summary>
    public float FramesPerSecond => _frameRate switch
    {
        MtcFrameRate.Fps24 => 24f,
        MtcFrameRate.Fps25 => 25f,
        MtcFrameRate.Fps2997DropFrame => 29.97f,
        MtcFrameRate.Fps30 => 30f,
        _ => 30f
    };

    /// <summary>
    /// Gets or sets the offset in seconds for sync alignment.
    /// </summary>
    public double OffsetSeconds
    {
        get => _offsetSeconds;
        set => _offsetSeconds = value;
    }

    /// <summary>
    /// Gets or sets the device ID for MMC (0x7F = all devices).
    /// </summary>
    public byte DeviceId
    {
        get => _deviceId;
        set => _deviceId = value;
    }

    /// <summary>
    /// Gets whether the transport is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets whether MTC generation is active.
    /// </summary>
    public bool IsGeneratingMtc => _isGeneratingMtc;

    /// <summary>
    /// Gets the current position in total frames.
    /// </summary>
    public long CurrentPositionFrames => _totalFrames;

    /// <summary>
    /// Gets the current position in seconds.
    /// </summary>
    public double CurrentPositionSeconds => _totalFrames / (double)FramesPerSecond + _offsetSeconds;

    /// <summary>
    /// Gets the current time code as a formatted string.
    /// </summary>
    public string TimeCodeString => $"{_hours:D2}:{_minutes:D2}:{_seconds:D2}:{_frames:D2}";

    /// <summary>
    /// Creates a new MachineControl instance.
    /// </summary>
    public MachineControl()
    {
        _stopwatch = new Stopwatch();
        _mtcTimer = new HighResolutionTimer();
        _mtcTimer.Tick += OnMtcTimerTick;
    }

    /// <summary>
    /// Adds a MIDI output device for sending MMC/MTC.
    /// </summary>
    /// <param name="deviceIndex">MIDI output device index.</param>
    /// <param name="midiOut">MidiOut instance.</param>
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
    /// Removes a MIDI output device.
    /// </summary>
    /// <param name="deviceIndex">MIDI output device index.</param>
    public void RemoveMidiOutput(int deviceIndex)
    {
        lock (_lock)
        {
            _midiOutputs.Remove(deviceIndex);
            _outputDeviceIndices.Remove(deviceIndex);
        }
    }

    /// <summary>
    /// Sends an MMC Play command.
    /// </summary>
    public void Play()
    {
        SendMmcCommand(MmcCommand.Play);
        _isRunning = true;
        _stopwatch.Start();
        TransportStateChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Sends an MMC Stop command.
    /// </summary>
    public void Stop()
    {
        SendMmcCommand(MmcCommand.Stop);
        _isRunning = false;
        _stopwatch.Stop();
        TransportStateChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Sends an MMC Record Strobe command (start recording).
    /// </summary>
    public void Record()
    {
        SendMmcCommand(MmcCommand.RecordStrobe);
    }

    /// <summary>
    /// Sends an MMC Record Exit command (stop recording).
    /// </summary>
    public void RecordExit()
    {
        SendMmcCommand(MmcCommand.RecordExit);
    }

    /// <summary>
    /// Sends an MMC Pause command.
    /// </summary>
    public void Pause()
    {
        SendMmcCommand(MmcCommand.Pause);
        _isRunning = false;
        _stopwatch.Stop();
        TransportStateChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Sends an MMC Fast Forward command.
    /// </summary>
    public void FastForward()
    {
        SendMmcCommand(MmcCommand.FastForward);
    }

    /// <summary>
    /// Sends an MMC Rewind command.
    /// </summary>
    public void Rewind()
    {
        SendMmcCommand(MmcCommand.Rewind);
    }

    /// <summary>
    /// Sends an MMC Locate command to a specific time code position.
    /// </summary>
    /// <param name="hours">Hours (0-23).</param>
    /// <param name="minutes">Minutes (0-59).</param>
    /// <param name="seconds">Seconds (0-59).</param>
    /// <param name="frames">Frames (0-29).</param>
    public void Locate(int hours, int minutes, int seconds, int frames)
    {
        SetPosition(hours, minutes, seconds, frames);
        SendMmcLocate(hours, minutes, seconds, frames);
    }

    /// <summary>
    /// Sends an MMC Locate command to a specific position in seconds.
    /// </summary>
    /// <param name="positionSeconds">Position in seconds.</param>
    public void Locate(double positionSeconds)
    {
        PositionToTimeCode(positionSeconds, out int h, out int m, out int s, out int f);
        Locate(h, m, s, f);
    }

    /// <summary>
    /// Sets the current position without sending locate command.
    /// </summary>
    /// <param name="hours">Hours.</param>
    /// <param name="minutes">Minutes.</param>
    /// <param name="seconds">Seconds.</param>
    /// <param name="frames">Frames.</param>
    public void SetPosition(int hours, int minutes, int seconds, int frames)
    {
        lock (_lock)
        {
            _hours = Math.Clamp(hours, 0, 23);
            _minutes = Math.Clamp(minutes, 0, 59);
            _seconds = Math.Clamp(seconds, 0, 59);
            _frames = Math.Clamp(frames, 0, (int)FramesPerSecond - 1);
            _quarterFrameIndex = 0;

            _totalFrames = TimeCodeToFrames(_hours, _minutes, _seconds, _frames);
        }
    }

    /// <summary>
    /// Sets the current position in seconds.
    /// </summary>
    /// <param name="positionSeconds">Position in seconds.</param>
    public void SetPosition(double positionSeconds)
    {
        PositionToTimeCode(positionSeconds, out int h, out int m, out int s, out int f);
        SetPosition(h, m, s, f);
    }

    /// <summary>
    /// Starts generating MTC quarter-frame messages.
    /// </summary>
    public void StartMtcGeneration()
    {
        if (_isGeneratingMtc)
            return;

        lock (_lock)
        {
            _isGeneratingMtc = true;
            _quarterFrameIndex = 0;

            // MTC runs at 4x frame rate (quarter frames)
            float quarterFrameRate = FramesPerSecond * 4f;
            _mtcTimer.TargetTicksPerSecond = quarterFrameRate;
            _mtcTimer.Start();
        }
    }

    /// <summary>
    /// Stops generating MTC.
    /// </summary>
    public void StopMtcGeneration()
    {
        if (!_isGeneratingMtc)
            return;

        lock (_lock)
        {
            _isGeneratingMtc = false;
            _mtcTimer.Stop();
        }
    }

    /// <summary>
    /// Sends a full MTC frame message (for initial sync).
    /// </summary>
    public void SendFullFrame()
    {
        lock (_lock)
        {
            // Full frame message format:
            // F0 7F dd 01 01 hr mn sc fr F7
            byte frameRateBits = (byte)((int)_frameRate << 5);
            byte hourByte = (byte)(_hours | frameRateBits);

            byte[] sysex = {
                SysexStart,
                0x7F,           // Universal Real-Time
                _deviceId,      // Device ID
                0x01,           // MTC
                0x01,           // Full Frame
                hourByte,
                (byte)_minutes,
                (byte)_seconds,
                (byte)_frames,
                SysexEnd
            };

            SendSysex(sysex);
        }
    }

    /// <summary>
    /// Processes incoming MIDI data for MMC commands.
    /// </summary>
    /// <param name="deviceIndex">Source device index.</param>
    /// <param name="midiEvent">MIDI event.</param>
    public void ProcessIncomingMidi(int deviceIndex, MidiEvent midiEvent)
    {
        // Handle MTC quarter-frame messages (status 0xF1)
        if ((int)midiEvent.CommandCode == 0xF1)
        {
            // Quarter-frame: F1 nd (n = nibble index, d = data)
            // This would require raw data access, handled in ProcessRawMidi
        }
    }

    /// <summary>
    /// Processes raw MIDI bytes for MMC/MTC.
    /// </summary>
    /// <param name="data">Raw MIDI data bytes.</param>
    public void ProcessRawMidi(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        // Check for MTC quarter-frame (F1 xx)
        if (data[0] == 0xF1 && data.Length >= 2)
        {
            ProcessMtcQuarterFrame(data[1]);
            return;
        }

        // Check for SysEx MMC message
        if (data[0] == SysexStart && data.Length >= 6)
        {
            // Check for MMC: F0 7F dd 06 cc ... F7
            if (data[1] == MmcManufacturer && data[3] == MmcSubId1)
            {
                byte receivedDeviceId = data[2];
                if (receivedDeviceId == _deviceId || receivedDeviceId == 0x7F)
                {
                    ProcessMmcCommand(data);
                }
            }
            // Check for MTC Full Frame: F0 7F dd 01 01 hr mn sc fr F7
            else if (data[1] == 0x7F && data[3] == 0x01 && data[4] == 0x01 && data.Length >= 10)
            {
                ProcessMtcFullFrame(data);
            }
        }
    }

    /// <summary>
    /// Processes an MMC command from sysex data.
    /// </summary>
    private void ProcessMmcCommand(byte[] data)
    {
        if (data.Length < 6)
            return;

        byte deviceId = data[2];
        MmcCommand command = (MmcCommand)data[4];
        long? locatePosition = null;

        // Check for Locate command with position data
        if (command == MmcCommand.Locate && data.Length >= 13)
        {
            // Locate format: F0 7F dd 06 44 06 01 hr mn sc fr 00 F7
            int hours = data[7] & 0x1F;
            int minutes = data[8];
            int seconds = data[9];
            int frames = data[10];
            locatePosition = TimeCodeToFrames(hours, minutes, seconds, frames);
        }

        var args = new MmcCommandEventArgs(command, deviceId, locatePosition,
            _stopwatch.Elapsed.TotalSeconds);
        CommandReceived?.Invoke(this, args);

        // Handle transport commands
        switch (command)
        {
            case MmcCommand.Play:
                _isRunning = true;
                TransportStateChanged?.Invoke(this, true);
                break;
            case MmcCommand.Stop:
            case MmcCommand.Pause:
                _isRunning = false;
                TransportStateChanged?.Invoke(this, false);
                break;
            case MmcCommand.Locate:
                if (locatePosition.HasValue)
                {
                    _totalFrames = locatePosition.Value;
                    FramesToTimeCode(_totalFrames, out _hours, out _minutes, out _seconds, out _frames);
                }
                break;
        }
    }

    /// <summary>
    /// Processes an MTC quarter-frame message.
    /// </summary>
    private void ProcessMtcQuarterFrame(byte data)
    {
        int nibbleIndex = (data >> 4) & 0x07;
        int nibbleValue = data & 0x0F;

        // Update appropriate time code component
        switch (nibbleIndex)
        {
            case 0: _frames = (_frames & 0xF0) | nibbleValue; break;
            case 1: _frames = (_frames & 0x0F) | (nibbleValue << 4); break;
            case 2: _seconds = (_seconds & 0xF0) | nibbleValue; break;
            case 3: _seconds = (_seconds & 0x0F) | (nibbleValue << 4); break;
            case 4: _minutes = (_minutes & 0xF0) | nibbleValue; break;
            case 5: _minutes = (_minutes & 0x0F) | (nibbleValue << 4); break;
            case 6: _hours = (_hours & 0x10) | nibbleValue; break;
            case 7:
                _hours = (_hours & 0x0F) | ((nibbleValue & 0x01) << 4);
                _frameRate = (MtcFrameRate)((nibbleValue >> 1) & 0x03);
                break;
        }

        // After all 8 quarter-frames, fire time code event
        if (nibbleIndex == 7)
        {
            _totalFrames = TimeCodeToFrames(_hours, _minutes, _seconds, _frames);
            double positionSeconds = _totalFrames / (double)FramesPerSecond + _offsetSeconds;

            var args = new MtcTimeEventArgs(_hours, _minutes, _seconds, _frames,
                _frameRate, _totalFrames, positionSeconds);
            TimeCodeReceived?.Invoke(this, args);
        }
    }

    /// <summary>
    /// Processes an MTC full frame message.
    /// </summary>
    private void ProcessMtcFullFrame(byte[] data)
    {
        byte hourByte = data[5];
        _frameRate = (MtcFrameRate)((hourByte >> 5) & 0x03);
        _hours = hourByte & 0x1F;
        _minutes = data[6];
        _seconds = data[7];
        _frames = data[8];

        _totalFrames = TimeCodeToFrames(_hours, _minutes, _seconds, _frames);
        double positionSeconds = _totalFrames / (double)FramesPerSecond + _offsetSeconds;

        var args = new MtcTimeEventArgs(_hours, _minutes, _seconds, _frames,
            _frameRate, _totalFrames, positionSeconds);
        TimeCodeReceived?.Invoke(this, args);
    }

    /// <summary>
    /// Timer callback for MTC generation.
    /// </summary>
    private void OnMtcTimerTick(object? sender, TimerTickEventArgs e)
    {
        if (!_isGeneratingMtc || !_isRunning)
            return;

        SendMtcQuarterFrame();

        _quarterFrameIndex++;
        if (_quarterFrameIndex >= 8)
        {
            _quarterFrameIndex = 0;

            // Advance time code by two frames (MTC quarter-frames are sent at 2 frames behind)
            _totalFrames++;
            FramesToTimeCode(_totalFrames, out _hours, out _minutes, out _seconds, out _frames);
        }
    }

    /// <summary>
    /// Sends an MTC quarter-frame message.
    /// </summary>
    private void SendMtcQuarterFrame()
    {
        int nibbleValue;
        int nibbleIndex = _quarterFrameIndex;

        switch (nibbleIndex)
        {
            case 0: nibbleValue = _frames & 0x0F; break;
            case 1: nibbleValue = (_frames >> 4) & 0x0F; break;
            case 2: nibbleValue = _seconds & 0x0F; break;
            case 3: nibbleValue = (_seconds >> 4) & 0x0F; break;
            case 4: nibbleValue = _minutes & 0x0F; break;
            case 5: nibbleValue = (_minutes >> 4) & 0x0F; break;
            case 6: nibbleValue = _hours & 0x0F; break;
            case 7: nibbleValue = ((_hours >> 4) & 0x01) | (((int)_frameRate) << 1); break;
            default: return;
        }

        byte quarterFrame = (byte)((nibbleIndex << 4) | nibbleValue);
        int message = 0xF1 | (quarterFrame << 8);

        SendMidiMessage(message);
    }

    /// <summary>
    /// Sends an MMC command.
    /// </summary>
    private void SendMmcCommand(MmcCommand command)
    {
        byte[] sysex = {
            SysexStart,
            MmcManufacturer,
            _deviceId,
            MmcSubId1,
            (byte)command,
            SysexEnd
        };

        SendSysex(sysex);
    }

    /// <summary>
    /// Sends an MMC Locate command with position.
    /// </summary>
    private void SendMmcLocate(int hours, int minutes, int seconds, int frames)
    {
        byte frameRateBits = (byte)((int)_frameRate << 5);
        byte hourByte = (byte)((hours & 0x1F) | frameRateBits);

        byte[] sysex = {
            SysexStart,
            MmcManufacturer,
            _deviceId,
            MmcSubId1,
            (byte)MmcCommand.Locate,
            0x06,               // Sub-command length
            MmcSubId2,          // Information field
            hourByte,
            (byte)minutes,
            (byte)seconds,
            (byte)frames,
            0x00,               // Sub-frames
            SysexEnd
        };

        SendSysex(sysex);
    }

    /// <summary>
    /// Sends a SysEx message to all outputs.
    /// </summary>
    private void SendSysex(byte[] data)
    {
        lock (_lock)
        {
            foreach (var deviceIndex in _outputDeviceIndices)
            {
                if (_midiOutputs.TryGetValue(deviceIndex, out var midiOut))
                {
                    try
                    {
                        midiOut.SendBuffer(data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending sysex to device {deviceIndex}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sends a simple MIDI message to all outputs.
    /// </summary>
    private void SendMidiMessage(int message)
    {
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
                        Console.WriteLine($"Error sending MIDI to device {deviceIndex}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts time code to total frames.
    /// </summary>
    private long TimeCodeToFrames(int hours, int minutes, int seconds, int frames)
    {
        float fps = FramesPerSecond;

        if (_frameRate == MtcFrameRate.Fps2997DropFrame)
        {
            // Drop-frame calculation: drop 2 frames at start of each minute except 0, 10, 20...
            int totalMinutes = hours * 60 + minutes;
            int droppedFrames = 2 * (totalMinutes - totalMinutes / 10);
            return (long)(hours * 3600 * fps + minutes * 60 * fps + seconds * fps + frames - droppedFrames);
        }

        return (long)(hours * 3600 * fps + minutes * 60 * fps + seconds * fps + frames);
    }

    /// <summary>
    /// Converts total frames to time code components.
    /// </summary>
    private void FramesToTimeCode(long totalFrames, out int hours, out int minutes, out int seconds, out int frames)
    {
        float fps = FramesPerSecond;
        int intFps = (int)MathF.Ceiling(fps);

        if (_frameRate == MtcFrameRate.Fps2997DropFrame)
        {
            // Drop-frame reverse calculation
            int framesPerMinute = 30 * 60 - 2;
            int framesPer10Minutes = 10 * framesPerMinute + 2;

            int tenMinuteBlocks = (int)(totalFrames / framesPer10Minutes);
            long remainder = totalFrames % framesPer10Minutes;

            long adjustedFrames = totalFrames + 2 * tenMinuteBlocks * 9;
            if (remainder > 2)
            {
                adjustedFrames += 2 * ((int)(remainder - 2) / framesPerMinute);
            }

            hours = (int)(adjustedFrames / (30 * 3600));
            adjustedFrames %= 30 * 3600;
            minutes = (int)(adjustedFrames / (30 * 60));
            adjustedFrames %= 30 * 60;
            seconds = (int)(adjustedFrames / 30);
            frames = (int)(adjustedFrames % 30);
        }
        else
        {
            hours = (int)(totalFrames / (intFps * 3600));
            totalFrames %= intFps * 3600;
            minutes = (int)(totalFrames / (intFps * 60));
            totalFrames %= intFps * 60;
            seconds = (int)(totalFrames / intFps);
            frames = (int)(totalFrames % intFps);
        }
    }

    /// <summary>
    /// Converts position in seconds to time code components.
    /// </summary>
    private void PositionToTimeCode(double positionSeconds, out int hours, out int minutes, out int seconds, out int frames)
    {
        positionSeconds -= _offsetSeconds;
        if (positionSeconds < 0) positionSeconds = 0;

        long totalFrames = (long)(positionSeconds * FramesPerSecond);
        FramesToTimeCode(totalFrames, out hours, out minutes, out seconds, out frames);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopMtcGeneration();
        _mtcTimer.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~MachineControl()
    {
        Dispose();
    }
}
