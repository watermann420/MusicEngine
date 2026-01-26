// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using NAudio.Wave;


namespace MusicEngine.Core.Routing;


/// <summary>
/// Specifies where the sidechain signal is tapped from.
/// </summary>
public enum SidechainTapPoint
{
    /// <summary>
    /// Signal is tapped before the channel fader.
    /// </summary>
    PreFader,

    /// <summary>
    /// Signal is tapped after the channel fader.
    /// </summary>
    PostFader,

    /// <summary>
    /// Signal is tapped before insert effects.
    /// </summary>
    PreInsert,

    /// <summary>
    /// Signal is tapped after insert effects.
    /// </summary>
    PostInsert
}


/// <summary>
/// Metering data for the sidechain bus.
/// </summary>
public class SidechainMeterData
{
    /// <summary>
    /// Peak level in decibels.
    /// </summary>
    public float PeakDb { get; set; }

    /// <summary>
    /// RMS level in decibels.
    /// </summary>
    public float RmsDb { get; set; }

    /// <summary>
    /// Peak level as linear value (0.0 - 1.0+).
    /// </summary>
    public float PeakLinear { get; set; }

    /// <summary>
    /// RMS level as linear value (0.0 - 1.0+).
    /// </summary>
    public float RmsLinear { get; set; }

    /// <summary>
    /// Whether the sidechain signal is clipping.
    /// </summary>
    public bool IsClipping { get; set; }

    /// <summary>
    /// Envelope follower value (smoothed).
    /// </summary>
    public float EnvelopeLevel { get; set; }
}


/// <summary>
/// A dedicated sidechain signal path for routing audio to effect sidechain inputs.
/// Supports input from any track/bus with pre/post fader selection and metering.
/// </summary>
public class SidechainBus : ISampleProvider
{
    private readonly object _lock = new();
    private ISampleProvider? _source;
    private readonly float[] _buffer;
    private readonly float[] _envelope;
    private readonly int _channels;
    private float _inputGain;
    private float _peakLevel;
    private float _rmsSum;
    private int _rmsSampleCount;

    // High-pass filter state
    private float[] _filterState;
    private float _filterFrequency;
    private float _filterCoefficient;

    /// <summary>
    /// Creates a new sidechain bus.
    /// </summary>
    /// <param name="waveFormat">The audio format for the bus.</param>
    /// <param name="name">The name of this sidechain bus.</param>
    /// <exception cref="ArgumentNullException">Thrown if waveFormat is null.</exception>
    public SidechainBus(WaveFormat waveFormat, string name = "Sidechain Bus")
    {
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        Name = name ?? "Sidechain Bus";
        Id = Guid.NewGuid();

        _channels = waveFormat.Channels;
        _buffer = new float[waveFormat.SampleRate * _channels]; // 1 second buffer
        _envelope = new float[_channels];
        _filterState = new float[_channels];
        _inputGain = 1.0f;
        _filterFrequency = 0f;
        _filterCoefficient = 0f;

        TapPoint = SidechainTapPoint.PostFader;
        IsActive = true;
    }

    /// <summary>
    /// Gets the unique identifier for this sidechain bus.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the name of this sidechain bus.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the wave format for this sidechain bus.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets or sets the signal source for this sidechain bus.
    /// </summary>
    public ISampleProvider? Source
    {
        get
        {
            lock (_lock)
            {
                return _source;
            }
        }
        set
        {
            lock (_lock)
            {
                if (value != null && value.WaveFormat.SampleRate != WaveFormat.SampleRate)
                {
                    throw new ArgumentException(
                        $"Source sample rate ({value.WaveFormat.SampleRate}) must match bus sample rate ({WaveFormat.SampleRate})");
                }

                _source = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the source track identifier.
    /// </summary>
    public string? SourceTrackId { get; set; }

    /// <summary>
    /// Gets or sets the target effect name that receives this sidechain signal.
    /// </summary>
    public string? TargetEffectName { get; set; }

    /// <summary>
    /// Gets or sets where the sidechain signal is tapped from.
    /// </summary>
    public SidechainTapPoint TapPoint { get; set; }

    /// <summary>
    /// Gets or sets the input gain for the sidechain signal (0.1 - 10.0).
    /// </summary>
    public float InputGain
    {
        get
        {
            lock (_lock)
            {
                return _inputGain;
            }
        }
        set
        {
            lock (_lock)
            {
                _inputGain = Math.Clamp(value, 0.1f, 10f);
            }
        }
    }

    /// <summary>
    /// Gets or sets the input gain in decibels (-20 to +20 dB).
    /// </summary>
    public float InputGainDb
    {
        get
        {
            float gain = InputGain;
            if (gain <= 0f) return -100f;
            return 20f * MathF.Log10(gain);
        }
        set
        {
            float db = Math.Clamp(value, -20f, 20f);
            InputGain = MathF.Pow(10f, db / 20f);
        }
    }

    /// <summary>
    /// Gets or sets the high-pass filter cutoff frequency in Hz.
    /// Set to 0 to disable filtering.
    /// </summary>
    public float FilterFrequency
    {
        get
        {
            lock (_lock)
            {
                return _filterFrequency;
            }
        }
        set
        {
            lock (_lock)
            {
                _filterFrequency = Math.Max(0f, Math.Min(value, WaveFormat.SampleRate / 2f));
                UpdateFilterCoefficient();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this sidechain bus is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether metering is enabled.
    /// </summary>
    public bool MeteringEnabled { get; set; } = true;

    /// <summary>
    /// Gets the current peak level in decibels.
    /// </summary>
    public float PeakDb
    {
        get
        {
            float peak = _peakLevel;
            if (peak <= 0f) return -100f;
            return 20f * MathF.Log10(peak);
        }
    }

    /// <summary>
    /// Gets the current RMS level in decibels.
    /// </summary>
    public float RmsDb
    {
        get
        {
            if (_rmsSampleCount <= 0) return -100f;
            float rms = MathF.Sqrt(_rmsSum / _rmsSampleCount);
            if (rms <= 0f) return -100f;
            return 20f * MathF.Log10(rms);
        }
    }

    /// <summary>
    /// Gets the current envelope level for the specified channel.
    /// </summary>
    /// <param name="channel">The channel index.</param>
    /// <returns>The envelope level (0.0 - 1.0+).</returns>
    public float GetEnvelopeLevel(int channel = 0)
    {
        if (channel < 0 || channel >= _envelope.Length)
            return 0f;

        lock (_lock)
        {
            return _envelope[channel];
        }
    }

    /// <summary>
    /// Gets the current metering data.
    /// </summary>
    public SidechainMeterData GetMeterData()
    {
        lock (_lock)
        {
            float rms = _rmsSampleCount > 0 ? MathF.Sqrt(_rmsSum / _rmsSampleCount) : 0f;

            return new SidechainMeterData
            {
                PeakLinear = _peakLevel,
                PeakDb = _peakLevel > 0f ? 20f * MathF.Log10(_peakLevel) : -100f,
                RmsLinear = rms,
                RmsDb = rms > 0f ? 20f * MathF.Log10(rms) : -100f,
                IsClipping = _peakLevel > 1f,
                EnvelopeLevel = _envelope.Length > 0 ? _envelope[0] : 0f
            };
        }
    }

    /// <summary>
    /// Reads audio samples from the sidechain bus.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (!IsActive)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        ISampleProvider? source;
        float gain;

        lock (_lock)
        {
            source = _source;
            gain = _inputGain;
        }

        if (source == null)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        // Read from source
        int samplesRead = source.Read(buffer, offset, count);

        if (samplesRead == 0)
        {
            return 0;
        }

        // Process samples
        ProcessSamples(buffer, offset, samplesRead, gain);

        return samplesRead;
    }

    /// <summary>
    /// Processes audio samples through the sidechain bus.
    /// </summary>
    private void ProcessSamples(float[] buffer, int offset, int count, float gain)
    {
        int channels = _channels;
        int sourceChannels = _source?.WaveFormat.Channels ?? channels;

        // Envelope follower coefficients
        float attackCoeff = MathF.Exp(-1f / (0.001f * WaveFormat.SampleRate)); // 1ms attack
        float releaseCoeff = MathF.Exp(-1f / (0.05f * WaveFormat.SampleRate)); // 50ms release

        float peakMax = 0f;
        float sumSquared = 0f;

        lock (_lock)
        {
            for (int i = 0; i < count; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = offset + i + ch;
                    float sample = buffer[idx];

                    // Handle mono-to-stereo expansion if needed
                    if (sourceChannels == 1 && channels == 2 && ch == 1)
                    {
                        sample = buffer[idx - 1]; // Copy from left channel
                    }

                    // Apply gain
                    sample *= gain;

                    // Apply high-pass filter if enabled
                    if (_filterFrequency > 0f && ch < _filterState.Length)
                    {
                        float filtered = sample - _filterState[ch];
                        _filterState[ch] += filtered * _filterCoefficient;
                        sample = filtered;
                    }

                    buffer[idx] = sample;

                    // Update envelope follower
                    if (ch < _envelope.Length)
                    {
                        float absSample = MathF.Abs(sample);
                        float coeff = absSample > _envelope[ch] ? attackCoeff : releaseCoeff;
                        _envelope[ch] = absSample + coeff * (_envelope[ch] - absSample);
                    }

                    // Metering
                    if (MeteringEnabled)
                    {
                        float absSample = MathF.Abs(sample);
                        peakMax = MathF.Max(peakMax, absSample);
                        sumSquared += sample * sample;
                    }
                }
            }

            // Update metering values
            if (MeteringEnabled)
            {
                // Peak hold with decay
                if (peakMax > _peakLevel)
                {
                    _peakLevel = peakMax;
                }
                else
                {
                    _peakLevel *= 0.9995f; // Slow decay
                }

                _rmsSum += sumSquared;
                _rmsSampleCount += count;

                // Reset RMS periodically (every ~100ms)
                if (_rmsSampleCount > WaveFormat.SampleRate / 10)
                {
                    _rmsSum = sumSquared;
                    _rmsSampleCount = count;
                }
            }
        }
    }

    /// <summary>
    /// Resets the metering values.
    /// </summary>
    public void ResetMeters()
    {
        lock (_lock)
        {
            _peakLevel = 0f;
            _rmsSum = 0f;
            _rmsSampleCount = 0;
            Array.Clear(_envelope);
        }
    }

    /// <summary>
    /// Resets the filter state.
    /// </summary>
    public void ResetFilter()
    {
        lock (_lock)
        {
            Array.Clear(_filterState);
        }
    }

    /// <summary>
    /// Updates the high-pass filter coefficient.
    /// </summary>
    private void UpdateFilterCoefficient()
    {
        if (_filterFrequency <= 0f || WaveFormat.SampleRate <= 0)
        {
            _filterCoefficient = 0f;
            return;
        }

        // Simple one-pole high-pass filter coefficient
        float rc = 1f / (2f * MathF.PI * _filterFrequency);
        float dt = 1f / WaveFormat.SampleRate;
        _filterCoefficient = dt / (rc + dt);
    }

    /// <summary>
    /// Creates a string representation of this sidechain bus.
    /// </summary>
    public override string ToString()
    {
        string sourceInfo = SourceTrackId ?? "No Source";
        string targetInfo = TargetEffectName ?? "No Target";
        string activeStr = IsActive ? "" : " [INACTIVE]";
        return $"{Name}: {sourceInfo} -> {targetInfo} ({TapPoint}){activeStr}";
    }
}


/// <summary>
/// Manager for multiple sidechain buses in the system.
/// </summary>
public class SidechainBusManager
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, SidechainBus> _buses;
    private readonly WaveFormat _waveFormat;

    /// <summary>
    /// Event raised when a sidechain bus is created.
    /// </summary>
    public event EventHandler<SidechainBus>? BusCreated;

    /// <summary>
    /// Event raised when a sidechain bus is removed.
    /// </summary>
    public event EventHandler<SidechainBus>? BusRemoved;

    /// <summary>
    /// Creates a new sidechain bus manager.
    /// </summary>
    /// <param name="waveFormat">The default wave format for new buses.</param>
    public SidechainBusManager(WaveFormat waveFormat)
    {
        _waveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        _buses = new Dictionary<Guid, SidechainBus>();
    }

    /// <summary>
    /// Gets all sidechain buses.
    /// </summary>
    public IReadOnlyList<SidechainBus> Buses
    {
        get
        {
            lock (_lock)
            {
                return _buses.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the count of sidechain buses.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _buses.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new sidechain bus.
    /// </summary>
    /// <param name="name">The name of the bus.</param>
    /// <param name="source">Optional source sample provider.</param>
    /// <param name="sourceTrackId">Optional source track ID.</param>
    /// <returns>The created sidechain bus.</returns>
    public SidechainBus CreateBus(string name, ISampleProvider? source = null, string? sourceTrackId = null)
    {
        var bus = new SidechainBus(_waveFormat, name)
        {
            Source = source,
            SourceTrackId = sourceTrackId
        };

        lock (_lock)
        {
            _buses[bus.Id] = bus;
        }

        BusCreated?.Invoke(this, bus);
        return bus;
    }

    /// <summary>
    /// Creates a sidechain bus for a specific effect.
    /// </summary>
    /// <param name="effectName">The name of the target effect.</param>
    /// <param name="source">Optional source sample provider.</param>
    /// <param name="sourceTrackId">Optional source track ID.</param>
    /// <returns>The created sidechain bus.</returns>
    public SidechainBus CreateBusForEffect(string effectName, ISampleProvider? source = null, string? sourceTrackId = null)
    {
        var bus = CreateBus($"{effectName} Sidechain", source, sourceTrackId);
        bus.TargetEffectName = effectName;
        return bus;
    }

    /// <summary>
    /// Removes a sidechain bus.
    /// </summary>
    /// <param name="busId">The bus ID to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveBus(Guid busId)
    {
        SidechainBus? bus;

        lock (_lock)
        {
            if (!_buses.TryGetValue(busId, out bus))
            {
                return false;
            }

            _buses.Remove(busId);
        }

        BusRemoved?.Invoke(this, bus);
        return true;
    }

    /// <summary>
    /// Gets a sidechain bus by ID.
    /// </summary>
    /// <param name="busId">The bus ID.</param>
    /// <returns>The sidechain bus or null if not found.</returns>
    public SidechainBus? GetBus(Guid busId)
    {
        lock (_lock)
        {
            return _buses.TryGetValue(busId, out var bus) ? bus : null;
        }
    }

    /// <summary>
    /// Gets a sidechain bus by name.
    /// </summary>
    /// <param name="name">The bus name.</param>
    /// <returns>The sidechain bus or null if not found.</returns>
    public SidechainBus? GetBusByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _buses.Values.FirstOrDefault(
                b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets the sidechain bus for a specific effect.
    /// </summary>
    /// <param name="effectName">The effect name.</param>
    /// <returns>The sidechain bus or null if not found.</returns>
    public SidechainBus? GetBusForEffect(string effectName)
    {
        if (string.IsNullOrWhiteSpace(effectName))
            return null;

        lock (_lock)
        {
            return _buses.Values.FirstOrDefault(
                b => b.TargetEffectName?.Equals(effectName, StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    /// <summary>
    /// Gets all sidechain buses that source from a specific track.
    /// </summary>
    /// <param name="trackId">The source track ID.</param>
    /// <returns>List of matching sidechain buses.</returns>
    public List<SidechainBus> GetBusesForSource(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return new List<SidechainBus>();

        lock (_lock)
        {
            return _buses.Values
                .Where(b => b.SourceTrackId?.Equals(trackId, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all sidechain buses.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buses.Clear();
        }
    }
}
