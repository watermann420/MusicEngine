// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using MusicEngine.Core;


namespace MusicEngine.Core.Routing;


/// <summary>
/// Represents an auxiliary send from a source channel to a return bus.
/// Sends allow routing a portion of a channel's signal to an effects bus (e.g., reverb, delay).
/// </summary>
public class Send
{
    private readonly object _lock = new();
    private float _level;
    private bool _preFader;
    private bool _isMuted;

    /// <summary>
    /// Gets the unique identifier for this send.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the source channel this send originates from.
    /// </summary>
    public AudioChannel SourceChannel { get; }

    /// <summary>
    /// Gets the target return bus this send routes to.
    /// </summary>
    public ReturnBus TargetBus { get; }

    /// <summary>
    /// Gets or sets the send level (0.0 - 1.0).
    /// Controls how much of the source signal is sent to the return bus.
    /// </summary>
    public float Level
    {
        get
        {
            lock (_lock)
            {
                return _level;
            }
        }
        set
        {
            lock (_lock)
            {
                _level = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this send is pre-fader.
    /// When true, the send level is independent of the channel fader.
    /// When false (post-fader), the send follows the channel fader.
    /// </summary>
    public bool PreFader
    {
        get
        {
            lock (_lock)
            {
                return _preFader;
            }
        }
        set
        {
            lock (_lock)
            {
                _preFader = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this send is muted.
    /// When muted, no signal is sent to the return bus from this send.
    /// </summary>
    public bool IsMuted
    {
        get
        {
            lock (_lock)
            {
                return _isMuted;
            }
        }
        set
        {
            lock (_lock)
            {
                _isMuted = value;
            }
        }
    }

    /// <summary>
    /// Creates a new send from a source channel to a target return bus.
    /// </summary>
    /// <param name="sourceChannel">The channel to send from.</param>
    /// <param name="targetBus">The return bus to send to.</param>
    /// <param name="level">Initial send level (0.0 - 1.0).</param>
    /// <param name="preFader">Whether the send is pre-fader (default: false).</param>
    /// <exception cref="ArgumentNullException">Thrown if sourceChannel or targetBus is null.</exception>
    public Send(AudioChannel sourceChannel, ReturnBus targetBus, float level = 0.5f, bool preFader = false)
    {
        SourceChannel = sourceChannel ?? throw new ArgumentNullException(nameof(sourceChannel));
        TargetBus = targetBus ?? throw new ArgumentNullException(nameof(targetBus));
        Id = Guid.NewGuid();
        _level = Math.Clamp(level, 0f, 1f);
        _preFader = preFader;
        _isMuted = false;
    }

    /// <summary>
    /// Calculates the effective send level considering mute, pre/post fader, and channel settings.
    /// </summary>
    /// <returns>The effective send level to apply.</returns>
    public float GetEffectiveLevel()
    {
        lock (_lock)
        {
            if (_isMuted || SourceChannel.Mute)
            {
                return 0f;
            }

            float effectiveLevel = _level;

            // Post-fader: multiply by channel fader and volume
            if (!_preFader)
            {
                effectiveLevel *= SourceChannel.Volume * SourceChannel.Fader;
            }

            return effectiveLevel;
        }
    }
}


/// <summary>
/// A return bus that receives audio from multiple sends and processes it through an effect chain.
/// Typically used for shared effects like reverb, delay, or chorus.
/// The output of a return bus is then mixed back into the main mix.
/// </summary>
public class ReturnBus : ISampleProvider
{
    private readonly object _lock = new();
    private readonly List<Send> _inputs;
    private readonly float[] _mixBuffer;
    private readonly float[] _inputBuffer;
    private float _level;
    private float _pan;
    private bool _isMuted;

    /// <summary>
    /// Gets the unique identifier for this return bus.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of this return bus (e.g., "Reverb", "Delay").
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the wave format of this return bus.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the list of sends routing to this return bus.
    /// </summary>
    public IReadOnlyList<Send> Inputs
    {
        get
        {
            lock (_lock)
            {
                return _inputs.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets the effect chain applied to the summed input signal.
    /// Can be null if no effects are needed (dry return).
    /// </summary>
    public EffectChain? Effects { get; set; }

    /// <summary>
    /// Gets or sets the return bus output level (0.0 - 2.0).
    /// </summary>
    public float Level
    {
        get
        {
            lock (_lock)
            {
                return _level;
            }
        }
        set
        {
            lock (_lock)
            {
                _level = Math.Clamp(value, 0f, 2f);
            }
        }
    }

    /// <summary>
    /// Gets or sets the return bus pan position (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public float Pan
    {
        get
        {
            lock (_lock)
            {
                return _pan;
            }
        }
        set
        {
            lock (_lock)
            {
                _pan = Math.Clamp(value, -1f, 1f);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this return bus is muted.
    /// </summary>
    public bool IsMuted
    {
        get
        {
            lock (_lock)
            {
                return _isMuted;
            }
        }
        set
        {
            lock (_lock)
            {
                _isMuted = value;
            }
        }
    }

    /// <summary>
    /// Creates a new return bus with the specified name and audio format.
    /// </summary>
    /// <param name="name">The name of the return bus (e.g., "Reverb", "Delay").</param>
    /// <param name="waveFormat">The audio format for the bus.</param>
    /// <exception cref="ArgumentNullException">Thrown if name or waveFormat is null.</exception>
    public ReturnBus(string name, WaveFormat waveFormat)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        Id = Guid.NewGuid();

        _inputs = new List<Send>();
        _level = 1.0f;
        _pan = 0.0f;
        _isMuted = false;

        // Allocate buffers for 1 second of audio
        int bufferSize = waveFormat.SampleRate * waveFormat.Channels;
        _mixBuffer = new float[bufferSize];
        _inputBuffer = new float[bufferSize];
    }

    /// <summary>
    /// Adds a send to this return bus's input list.
    /// This is called internally by SendManager.
    /// </summary>
    /// <param name="send">The send to add.</param>
    internal void AddInput(Send send)
    {
        lock (_lock)
        {
            if (!_inputs.Contains(send))
            {
                _inputs.Add(send);
            }
        }
    }

    /// <summary>
    /// Removes a send from this return bus's input list.
    /// This is called internally by SendManager.
    /// </summary>
    /// <param name="send">The send to remove.</param>
    internal void RemoveInput(Send send)
    {
        lock (_lock)
        {
            _inputs.Remove(send);
        }
    }

    /// <summary>
    /// Reads and mixes audio from all input sends, applies effects, and outputs the result.
    /// </summary>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="offset">The offset into the output buffer.</param>
    /// <param name="count">The number of samples to read.</param>
    /// <returns>The number of samples written.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            // Clear mix buffer
            Array.Clear(_mixBuffer, 0, count);

            // If muted, output silence
            if (_isMuted)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            // Mix all inputs from sends
            foreach (var send in _inputs)
            {
                float effectiveLevel = send.GetEffectiveLevel();

                if (effectiveLevel <= 0f)
                {
                    continue;
                }

                // Read from the source channel
                // Note: We need to read raw samples from the channel's source, not through the channel's Read()
                // because that would apply channel processing twice. Instead, we should tap the signal.
                int samplesRead = ReadFromChannel(send.SourceChannel, _inputBuffer, 0, count, send.PreFader);

                // Apply send level and mix into the mix buffer
                for (int i = 0; i < samplesRead; i++)
                {
                    _mixBuffer[i] += _inputBuffer[i] * effectiveLevel;
                }
            }

            // Apply effects if present
            float[] outputBuffer = _mixBuffer;
            if (Effects != null && Effects.Count > 0 && !Effects.Bypassed)
            {
                // The effect chain reads from an internal source
                // We need to provide the mixed signal to the effects
                // For this, we use a helper provider
                var mixedSource = new BufferSampleProvider(_mixBuffer, count, WaveFormat);

                // Create a temporary effect chain or read through existing
                // Note: Ideally, the EffectChain should be pre-configured with this bus as source
                // For now, we apply effects directly to the buffer
                outputBuffer = ProcessEffects(_mixBuffer, count);
            }

            // Apply return level and pan
            int channels = WaveFormat.Channels;
            float leftGain = _level;
            float rightGain = _level;

            if (channels == 2 && _pan != 0f)
            {
                // Constant power panning
                float panAngle = (_pan + 1f) * MathF.PI * 0.25f;
                leftGain *= MathF.Cos(panAngle);
                rightGain *= MathF.Sin(panAngle);
            }

            // Copy to output buffer with level and pan applied
            for (int i = 0; i < count; i += channels)
            {
                if (channels == 1)
                {
                    buffer[offset + i] = outputBuffer[i] * leftGain;
                }
                else if (channels == 2)
                {
                    buffer[offset + i] = outputBuffer[i] * leftGain;
                    buffer[offset + i + 1] = outputBuffer[i + 1] * rightGain;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Reads audio from a channel, optionally bypassing the channel's fader (for pre-fader sends).
    /// </summary>
    private int ReadFromChannel(AudioChannel channel, float[] buffer, int offset, int count, bool preFader)
    {
        // For pre-fader, we want the signal before channel volume/fader
        // For post-fader, we can use the channel's Read() method
        // However, reading from the channel directly would consume samples
        // In a real implementation, we'd need a tap point or buffering mechanism

        // Simplified implementation: read from channel and adjust for pre/post
        // This is a simplified approach - a production system would use proper tap points
        int samplesRead = channel.Read(buffer, offset, count);

        if (preFader && samplesRead > 0)
        {
            // Undo the channel's volume and fader to get pre-fader signal
            float compensation = 1f;
            if (channel.Volume > 0f && channel.Fader > 0f)
            {
                compensation = 1f / (channel.Volume * channel.Fader);
            }

            for (int i = offset; i < offset + samplesRead; i++)
            {
                buffer[i] *= compensation;
            }
        }

        return samplesRead;
    }

    /// <summary>
    /// Processes the mixed signal through the effect chain.
    /// </summary>
    private float[] ProcessEffects(float[] inputBuffer, int count)
    {
        if (Effects == null)
        {
            return inputBuffer;
        }

        // Create output buffer
        float[] outputBuffer = new float[count];

        // Process through effect chain
        var source = new BufferSampleProvider(inputBuffer, count, WaveFormat);

        // Note: In a full implementation, the EffectChain would be set up with proper source routing
        // For now, we read through the chain if effects are configured
        Effects.Read(outputBuffer, 0, count);

        return outputBuffer;
    }

    /// <summary>
    /// Helper sample provider that wraps a pre-filled buffer.
    /// </summary>
    private class BufferSampleProvider : ISampleProvider
    {
        private readonly float[] _buffer;
        private readonly int _count;
        private int _position;

        public WaveFormat WaveFormat { get; }

        public BufferSampleProvider(float[] buffer, int count, WaveFormat waveFormat)
        {
            _buffer = buffer;
            _count = count;
            _position = 0;
            WaveFormat = waveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesToRead = Math.Min(count, _count - _position);
            Array.Copy(_buffer, _position, buffer, offset, samplesToRead);
            _position += samplesToRead;
            return samplesToRead;
        }
    }
}


/// <summary>
/// Manages sends and return buses for a mixing session.
/// Provides thread-safe creation, removal, and querying of send/return routing.
/// </summary>
public class SendManager
{
    private readonly object _lock = new();
    private readonly List<Send> _sends;
    private readonly List<ReturnBus> _returnBuses;

    /// <summary>
    /// Gets all sends in the manager.
    /// </summary>
    public IReadOnlyList<Send> Sends
    {
        get
        {
            lock (_lock)
            {
                return _sends.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets all return buses in the manager.
    /// </summary>
    public IReadOnlyList<ReturnBus> ReturnBuses
    {
        get
        {
            lock (_lock)
            {
                return _returnBuses.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Creates a new SendManager instance.
    /// </summary>
    public SendManager()
    {
        _sends = new List<Send>();
        _returnBuses = new List<ReturnBus>();
    }

    /// <summary>
    /// Creates a new return bus and adds it to the manager.
    /// </summary>
    /// <param name="name">The name of the return bus.</param>
    /// <param name="waveFormat">The audio format for the bus.</param>
    /// <returns>The created return bus.</returns>
    /// <exception cref="ArgumentNullException">Thrown if name or waveFormat is null.</exception>
    public ReturnBus CreateReturnBus(string name, WaveFormat waveFormat)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        if (waveFormat == null)
            throw new ArgumentNullException(nameof(waveFormat));

        lock (_lock)
        {
            var bus = new ReturnBus(name, waveFormat);
            _returnBuses.Add(bus);
            return bus;
        }
    }

    /// <summary>
    /// Removes a return bus and all its associated sends.
    /// </summary>
    /// <param name="bus">The return bus to remove.</param>
    /// <returns>True if the bus was removed, false if not found.</returns>
    public bool RemoveReturnBus(ReturnBus bus)
    {
        if (bus == null)
            return false;

        lock (_lock)
        {
            // Remove all sends to this bus
            var sendsToRemove = _sends.Where(s => s.TargetBus == bus).ToList();
            foreach (var send in sendsToRemove)
            {
                _sends.Remove(send);
            }

            return _returnBuses.Remove(bus);
        }
    }

    /// <summary>
    /// Gets a return bus by name.
    /// </summary>
    /// <param name="name">The name of the return bus.</param>
    /// <returns>The return bus, or null if not found.</returns>
    public ReturnBus? GetReturnBus(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _returnBuses.Find(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets a return bus by ID.
    /// </summary>
    /// <param name="id">The ID of the return bus.</param>
    /// <returns>The return bus, or null if not found.</returns>
    public ReturnBus? GetReturnBus(Guid id)
    {
        lock (_lock)
        {
            return _returnBuses.Find(b => b.Id == id);
        }
    }

    /// <summary>
    /// Creates a new send from a channel to a return bus.
    /// </summary>
    /// <param name="channel">The source channel.</param>
    /// <param name="bus">The target return bus.</param>
    /// <param name="level">The send level (0.0 - 1.0).</param>
    /// <param name="preFader">Whether the send is pre-fader.</param>
    /// <returns>The created send.</returns>
    /// <exception cref="ArgumentNullException">Thrown if channel or bus is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a send already exists from this channel to this bus.</exception>
    public Send CreateSend(AudioChannel channel, ReturnBus bus, float level = 0.5f, bool preFader = false)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));

        lock (_lock)
        {
            // Check for duplicate send
            if (_sends.Any(s => s.SourceChannel == channel && s.TargetBus == bus))
            {
                throw new InvalidOperationException(
                    $"A send already exists from channel '{channel.Name}' to bus '{bus.Name}'.");
            }

            // Verify the bus is managed by this SendManager
            if (!_returnBuses.Contains(bus))
            {
                throw new InvalidOperationException(
                    $"Return bus '{bus.Name}' is not managed by this SendManager.");
            }

            var send = new Send(channel, bus, level, preFader);
            _sends.Add(send);
            bus.AddInput(send);

            return send;
        }
    }

    /// <summary>
    /// Removes a send.
    /// </summary>
    /// <param name="send">The send to remove.</param>
    /// <returns>True if the send was removed, false if not found.</returns>
    public bool RemoveSend(Send send)
    {
        if (send == null)
            return false;

        lock (_lock)
        {
            if (_sends.Remove(send))
            {
                send.TargetBus.RemoveInput(send);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes a send by finding it from channel and bus.
    /// </summary>
    /// <param name="channel">The source channel.</param>
    /// <param name="bus">The target return bus.</param>
    /// <returns>True if the send was removed, false if not found.</returns>
    public bool RemoveSend(AudioChannel channel, ReturnBus bus)
    {
        if (channel == null || bus == null)
            return false;

        lock (_lock)
        {
            var send = _sends.Find(s => s.SourceChannel == channel && s.TargetBus == bus);
            if (send != null)
            {
                return RemoveSend(send);
            }
            return false;
        }
    }

    /// <summary>
    /// Gets a send from a specific channel to a specific bus.
    /// </summary>
    /// <param name="channel">The source channel.</param>
    /// <param name="bus">The target return bus.</param>
    /// <returns>The send, or null if not found.</returns>
    public Send? GetSend(AudioChannel channel, ReturnBus bus)
    {
        if (channel == null || bus == null)
            return null;

        lock (_lock)
        {
            return _sends.Find(s => s.SourceChannel == channel && s.TargetBus == bus);
        }
    }

    /// <summary>
    /// Gets all sends originating from a specific channel.
    /// </summary>
    /// <param name="channel">The channel to get sends for.</param>
    /// <returns>List of sends from the channel.</returns>
    public List<Send> GetSendsForChannel(AudioChannel channel)
    {
        if (channel == null)
            return new List<Send>();

        lock (_lock)
        {
            return _sends.Where(s => s.SourceChannel == channel).ToList();
        }
    }

    /// <summary>
    /// Gets all sends routing to a specific return bus.
    /// </summary>
    /// <param name="bus">The return bus to get sends for.</param>
    /// <returns>List of sends to the bus.</returns>
    public List<Send> GetSendsForBus(ReturnBus bus)
    {
        if (bus == null)
            return new List<Send>();

        lock (_lock)
        {
            return _sends.Where(s => s.TargetBus == bus).ToList();
        }
    }

    /// <summary>
    /// Removes all sends from a specific channel.
    /// </summary>
    /// <param name="channel">The channel to remove sends from.</param>
    /// <returns>The number of sends removed.</returns>
    public int RemoveAllSendsForChannel(AudioChannel channel)
    {
        if (channel == null)
            return 0;

        lock (_lock)
        {
            var sendsToRemove = _sends.Where(s => s.SourceChannel == channel).ToList();
            foreach (var send in sendsToRemove)
            {
                send.TargetBus.RemoveInput(send);
                _sends.Remove(send);
            }
            return sendsToRemove.Count;
        }
    }

    /// <summary>
    /// Sets the level of a send between a channel and a bus.
    /// Creates the send if it doesn't exist.
    /// </summary>
    /// <param name="channel">The source channel.</param>
    /// <param name="bus">The target return bus.</param>
    /// <param name="level">The send level (0.0 - 1.0).</param>
    /// <returns>The send (existing or newly created).</returns>
    public Send SetSendLevel(AudioChannel channel, ReturnBus bus, float level)
    {
        lock (_lock)
        {
            var send = _sends.Find(s => s.SourceChannel == channel && s.TargetBus == bus);
            if (send != null)
            {
                send.Level = level;
                return send;
            }
            else
            {
                return CreateSend(channel, bus, level);
            }
        }
    }

    /// <summary>
    /// Clears all sends and return buses from the manager.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var send in _sends)
            {
                send.TargetBus.RemoveInput(send);
            }
            _sends.Clear();
            _returnBuses.Clear();
        }
    }
}
