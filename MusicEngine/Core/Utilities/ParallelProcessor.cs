// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Parallel processing utility for NY compression style parallel signal chains.

using NAudio.Wave;
using MusicEngine.Core.Dsp;
using MusicEngine.Core.PDC;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Blend curve types for mixing between parallel chains.
/// </summary>
public enum BlendCurveType
{
    /// <summary>Linear crossfade (constant voltage)</summary>
    Linear,
    /// <summary>Equal power crossfade (constant loudness)</summary>
    EqualPower,
    /// <summary>Logarithmic curve for more gradual transitions</summary>
    Logarithmic,
    /// <summary>S-curve for smooth transitions</summary>
    SCurve
}

/// <summary>
/// Crossover mode for splitting signal into frequency bands.
/// </summary>
public enum CrossoverMode
{
    /// <summary>No crossover - full bandwidth to all chains</summary>
    None,
    /// <summary>2-band split (low/high)</summary>
    TwoBand,
    /// <summary>3-band split (low/mid/high)</summary>
    ThreeBand,
    /// <summary>4-band split (low/low-mid/high-mid/high)</summary>
    FourBand
}

/// <summary>
/// Mid/Side processing mode.
/// </summary>
public enum MidSideMode
{
    /// <summary>Normal stereo processing</summary>
    Stereo,
    /// <summary>Process mid (center) signal only</summary>
    MidOnly,
    /// <summary>Process side (stereo difference) signal only</summary>
    SideOnly,
    /// <summary>Split mid and side to separate chains</summary>
    MidSideSplit
}

/// <summary>
/// Preset types for common parallel processing setups.
/// </summary>
public enum ParallelPreset
{
    /// <summary>Default balanced parallel mix</summary>
    Default,
    /// <summary>NY-style parallel compression (heavy compression blended with dry)</summary>
    NYCompression,
    /// <summary>Parallel distortion/saturation</summary>
    ParallelDistortion,
    /// <summary>Parallel exciter/harmonic enhancement</summary>
    ParallelExciter,
    /// <summary>Multi-band parallel processing</summary>
    MultiBand,
    /// <summary>Drum bus parallel processing</summary>
    DrumBus,
    /// <summary>Vocal parallel processing</summary>
    VocalChain,
    /// <summary>Bass enhancement with parallel sub</summary>
    BassEnhancer
}

/// <summary>
/// Represents a single parallel processing chain with effects, level, pan, and filtering.
/// </summary>
public class ParallelChain : IDisposable
{
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly List<IEffect> _effects = new();
    private readonly object _effectLock = new();

    // Filter state for HP/LP filters (2 stages for Linkwitz-Riley)
    private BiquadCoeffs _hpCoeffs;
    private BiquadCoeffs _lpCoeffs;
    private BiquadState[] _hpState1;
    private BiquadState[] _hpState2;
    private BiquadState[] _lpState1;
    private BiquadState[] _lpState2;

    // Envelope follower state
    private float[] _envelopeState;
    private float _envelopeAttack = 0.01f;
    private float _envelopeRelease = 0.1f;

    // Internal buffers
    private float[] _chainBuffer = Array.Empty<float>();
    private float[] _sidechainBuffer = Array.Empty<float>();

    /// <summary>
    /// Chain index (0-3).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Chain name for identification.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Output level (0.0 - 2.0).
    /// </summary>
    public float Level { get; set; } = 1.0f;

    /// <summary>
    /// Stereo pan (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public float Pan { get; set; } = 0.0f;

    /// <summary>
    /// Mute state.
    /// </summary>
    public bool Mute { get; set; }

    /// <summary>
    /// Solo state.
    /// </summary>
    public bool Solo { get; set; }

    /// <summary>
    /// Bypass all processing (passes input unchanged).
    /// </summary>
    public bool Bypass { get; set; }

    /// <summary>
    /// High-pass filter frequency in Hz (0 = disabled).
    /// </summary>
    public float HighPassFrequency
    {
        get => _highPassFrequency;
        set
        {
            _highPassFrequency = Math.Clamp(value, 0f, 20000f);
            UpdateFilters();
        }
    }
    private float _highPassFrequency;

    /// <summary>
    /// Low-pass filter frequency in Hz (0 or >= 20000 = disabled).
    /// </summary>
    public float LowPassFrequency
    {
        get => _lowPassFrequency;
        set
        {
            _lowPassFrequency = Math.Clamp(value, 0f, 22000f);
            UpdateFilters();
        }
    }
    private float _lowPassFrequency = 22000f;

    /// <summary>
    /// Filter resonance/Q (0.5 - 10.0).
    /// </summary>
    public float FilterQ { get; set; } = 0.707f;

    /// <summary>
    /// Enable envelope follower for dynamic mixing.
    /// </summary>
    public bool EnvelopeFollowerEnabled { get; set; }

    /// <summary>
    /// Envelope follower attack time in seconds.
    /// </summary>
    public float EnvelopeAttack
    {
        get => _envelopeAttack;
        set => _envelopeAttack = Math.Clamp(value, 0.0001f, 1f);
    }

    /// <summary>
    /// Envelope follower release time in seconds.
    /// </summary>
    public float EnvelopeRelease
    {
        get => _envelopeRelease;
        set => _envelopeRelease = Math.Clamp(value, 0.001f, 5f);
    }

    /// <summary>
    /// Current envelope follower value (0.0 - 1.0) for metering.
    /// </summary>
    public float EnvelopeValue { get; private set; }

    /// <summary>
    /// Latency in samples introduced by this chain's effects.
    /// </summary>
    public int LatencySamples { get; private set; }

    /// <summary>
    /// Phase invert for this chain.
    /// </summary>
    public bool PhaseInvert { get; set; }

    /// <summary>
    /// Sidechain source chain index (-1 = none, 0-3 = chain index).
    /// </summary>
    public int SidechainSourceChain { get; set; } = -1;

    /// <summary>
    /// Sidechain amount (0.0 - 1.0).
    /// </summary>
    public float SidechainAmount { get; set; }

    /// <summary>
    /// Gets the effects in this chain.
    /// </summary>
    public IReadOnlyList<IEffect> Effects
    {
        get
        {
            lock (_effectLock)
            {
                return _effects.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Creates a new parallel chain.
    /// </summary>
    /// <param name="index">Chain index (0-3).</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public ParallelChain(int index, int channels, int sampleRate)
    {
        Index = index;
        Name = $"Chain {index + 1}";
        _channels = channels;
        _sampleRate = sampleRate;

        _hpState1 = new BiquadState[channels];
        _hpState2 = new BiquadState[channels];
        _lpState1 = new BiquadState[channels];
        _lpState2 = new BiquadState[channels];
        _envelopeState = new float[channels];

        _hpCoeffs = BiquadCoeffs.Bypass;
        _lpCoeffs = BiquadCoeffs.Bypass;
    }

    /// <summary>
    /// Adds an effect to this chain.
    /// </summary>
    /// <param name="effect">The effect to add.</param>
    public void AddEffect(IEffect effect)
    {
        if (effect == null) return;

        lock (_effectLock)
        {
            _effects.Add(effect);
            UpdateLatency();
        }
    }

    /// <summary>
    /// Removes an effect from this chain.
    /// </summary>
    /// <param name="effect">The effect to remove.</param>
    /// <returns>True if removed.</returns>
    public bool RemoveEffect(IEffect effect)
    {
        if (effect == null) return false;

        lock (_effectLock)
        {
            bool removed = _effects.Remove(effect);
            if (removed) UpdateLatency();
            return removed;
        }
    }

    /// <summary>
    /// Clears all effects from this chain.
    /// </summary>
    public void ClearEffects()
    {
        lock (_effectLock)
        {
            _effects.Clear();
            LatencySamples = 0;
        }
    }

    private void UpdateFilters()
    {
        // High-pass filter (Linkwitz-Riley 4th order = 2 cascaded 2nd order)
        if (_highPassFrequency > 20f)
        {
            _hpCoeffs = BiquadCoeffs.Highpass(_sampleRate, _highPassFrequency, 0.707f);
        }
        else
        {
            _hpCoeffs = BiquadCoeffs.Bypass;
        }

        // Low-pass filter
        if (_lowPassFrequency > 0f && _lowPassFrequency < 20000f)
        {
            _lpCoeffs = BiquadCoeffs.Lowpass(_sampleRate, _lowPassFrequency, 0.707f);
        }
        else
        {
            _lpCoeffs = BiquadCoeffs.Bypass;
        }
    }

    private void UpdateLatency()
    {
        int totalLatency = 0;
        foreach (var effect in _effects)
        {
            if (effect is ILatencyReporter latencyReporter)
            {
                totalLatency += latencyReporter.LatencySamples;
            }
        }
        LatencySamples = totalLatency;
    }

    /// <summary>
    /// Processes audio through this chain.
    /// </summary>
    /// <param name="input">Input buffer.</param>
    /// <param name="output">Output buffer.</param>
    /// <param name="offset">Offset into output buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="sidechainInput">Optional sidechain input from another chain.</param>
    public void Process(float[] input, float[] output, int offset, int count, float[]? sidechainInput = null)
    {
        if (Bypass || Mute)
        {
            // Output silence for muted/bypassed chain
            Array.Clear(output, offset, count);
            return;
        }

        // Ensure chain buffer is large enough
        if (_chainBuffer.Length < count)
        {
            _chainBuffer = new float[count];
        }

        // Copy input to chain buffer
        Array.Copy(input, 0, _chainBuffer, 0, count);

        // Apply high-pass filter (2 stages for Linkwitz-Riley)
        if (_highPassFrequency > 20f)
        {
            ApplyBiquadFilter(_chainBuffer, count, _hpCoeffs, _hpState1);
            ApplyBiquadFilter(_chainBuffer, count, _hpCoeffs, _hpState2);
        }

        // Apply low-pass filter (2 stages for Linkwitz-Riley)
        if (_lowPassFrequency > 0f && _lowPassFrequency < 20000f)
        {
            ApplyBiquadFilter(_chainBuffer, count, _lpCoeffs, _lpState1);
            ApplyBiquadFilter(_chainBuffer, count, _lpCoeffs, _lpState2);
        }

        // Process through effects chain
        lock (_effectLock)
        {
            foreach (var effect in _effects)
            {
                if (effect.Enabled)
                {
                    // Create a wrapper to feed the chain buffer to the effect
                    ProcessEffectInPlace(effect, _chainBuffer, count);
                }
            }
        }

        // Apply envelope follower for dynamic mixing
        float envelopeMix = 1.0f;
        if (EnvelopeFollowerEnabled)
        {
            envelopeMix = ProcessEnvelopeFollower(_chainBuffer, count);
            EnvelopeValue = envelopeMix;
        }

        // Apply sidechain ducking if configured
        float sidechainMix = 1.0f;
        if (sidechainInput != null && SidechainSourceChain >= 0 && SidechainAmount > 0)
        {
            sidechainMix = ProcessSidechain(sidechainInput, count);
        }

        // Apply level, pan, phase, and copy to output
        float level = Level * envelopeMix * sidechainMix;
        float leftGain = level;
        float rightGain = level;

        if (_channels == 2 && Pan != 0f)
        {
            // Constant power panning
            float panAngle = (Pan + 1f) * MathF.PI * 0.25f;
            leftGain *= MathF.Cos(panAngle);
            rightGain *= MathF.Sin(panAngle);
        }

        float phaseMultiplier = PhaseInvert ? -1f : 1f;

        for (int i = 0; i < count; i += _channels)
        {
            if (_channels == 1)
            {
                output[offset + i] = _chainBuffer[i] * leftGain * phaseMultiplier;
            }
            else if (_channels == 2)
            {
                output[offset + i] = _chainBuffer[i] * leftGain * phaseMultiplier;
                output[offset + i + 1] = _chainBuffer[i + 1] * rightGain * phaseMultiplier;
            }
        }
    }

    private void ApplyBiquadFilter(float[] buffer, int count, BiquadCoeffs coeffs, BiquadState[] states)
    {
        // Direct Form II Transposed implementation
        for (int i = 0; i < count; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                float input = buffer[i + ch];
                ref var state = ref states[ch];

                // Direct Form II Transposed:
                // y[n] = b0*x[n] + z1
                // z1 = b1*x[n] - a1*y[n] + z2
                // z2 = b2*x[n] - a2*y[n]
                float output = coeffs.B0 * input + state.Z1;
                state.Z1 = coeffs.B1 * input - coeffs.A1 * output + state.Z2;
                state.Z2 = coeffs.B2 * input - coeffs.A2 * output;

                buffer[i + ch] = output;
            }
        }
    }

    private void ProcessEffectInPlace(IEffect effect, float[] buffer, int count)
    {
        // For effects that implement ISampleProvider, we process in-place
        // This is a simplified approach - in production, the effect chain
        // should properly wrap sources
        var tempBuffer = new float[count];
        Array.Copy(buffer, tempBuffer, count);

        // Most effects in MusicEngine use the EffectBase pattern
        // We create a simple buffer-based sample provider
        var provider = new BufferSampleProvider(tempBuffer, count,
            WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels));

        effect.Read(buffer, 0, count);
    }

    private float ProcessEnvelopeFollower(float[] buffer, int count)
    {
        float attackCoeff = MathF.Exp(-1f / (_envelopeAttack * _sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (_envelopeRelease * _sampleRate));

        float maxEnvelope = 0f;

        for (int i = 0; i < count; i += _channels)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                float inputAbs = MathF.Abs(buffer[i + ch]);
                float coeff = inputAbs > _envelopeState[ch] ? attackCoeff : releaseCoeff;
                _envelopeState[ch] = inputAbs + coeff * (_envelopeState[ch] - inputAbs);
                maxEnvelope = MathF.Max(maxEnvelope, _envelopeState[ch]);
            }
        }

        return Math.Clamp(maxEnvelope, 0f, 1f);
    }

    private float ProcessSidechain(float[] sidechainBuffer, int count)
    {
        // Simple sidechain envelope detection
        float maxLevel = 0f;
        for (int i = 0; i < count; i++)
        {
            maxLevel = MathF.Max(maxLevel, MathF.Abs(sidechainBuffer[i]));
        }

        // Apply ducking based on sidechain level
        float duckAmount = maxLevel * SidechainAmount;
        return Math.Clamp(1f - duckAmount, 0f, 1f);
    }

    /// <summary>
    /// Resets all filter and envelope states.
    /// </summary>
    public void Reset()
    {
        for (int ch = 0; ch < _channels; ch++)
        {
            _hpState1[ch] = default;
            _hpState2[ch] = default;
            _lpState1[ch] = default;
            _lpState2[ch] = default;
            _envelopeState[ch] = 0f;
        }
        EnvelopeValue = 0f;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        lock (_effectLock)
        {
            foreach (var effect in _effects)
            {
                if (effect is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _effects.Clear();
        }
    }

    /// <summary>
    /// Helper sample provider for buffer-based processing.
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
            int toRead = Math.Min(count, _count - _position);
            Array.Copy(_buffer, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }
    }
}

/// <summary>
/// Parallel processing utility for NY compression and multi-chain signal processing.
/// Provides up to 4 parallel processing chains with level, pan, filtering,
/// crossover splitting, mid/side processing, and dynamic mixing capabilities.
/// </summary>
public class ParallelProcessor : ISampleProvider, IDisposable
{
    private const int MaxChains = 4;

    private readonly ISampleProvider _source;
    private readonly ParallelChain[] _chains;
    private readonly object _lock = new();

    // Crossover filter state
    private readonly BiquadCoeffs[] _crossoverLpCoeffs;
    private readonly BiquadCoeffs[] _crossoverHpCoeffs;
    private readonly BiquadState[,] _crossoverLpState1;
    private readonly BiquadState[,] _crossoverLpState2;
    private readonly BiquadState[,] _crossoverHpState1;
    private readonly BiquadState[,] _crossoverHpState2;

    // Latency compensation delay lines
    private readonly float[][] _delayLines;
    private readonly int[] _delayWritePos;
    private int _maxLatency;

    // A/B comparison state
    private bool _isStateA = true;
    private ParallelProcessorState? _stateA;
    private ParallelProcessorState? _stateB;

    // Macro controls
    private readonly float[] _macroValues = new float[8];
    private readonly List<MacroMapping>[] _macroMappings = new List<MacroMapping>[8];

    // Internal buffers
    private float[] _sourceBuffer = Array.Empty<float>();
    private float[] _dryBuffer = Array.Empty<float>();
    private float[][] _chainInputBuffers;
    private float[][] _chainOutputBuffers;
    private float[] _midBuffer = Array.Empty<float>();
    private float[] _sideBuffer = Array.Empty<float>();

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate => WaveFormat.SampleRate;

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    public int Channels => WaveFormat.Channels;

    /// <summary>
    /// Gets or sets the dry signal level (0.0 - 2.0).
    /// </summary>
    public float DryLevel { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the overall output gain (0.0 - 4.0).
    /// </summary>
    public float OutputGain { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the blend curve type.
    /// </summary>
    public BlendCurveType BlendCurve { get; set; } = BlendCurveType.EqualPower;

    /// <summary>
    /// Gets or sets the crossover mode.
    /// </summary>
    public CrossoverMode CrossoverMode
    {
        get => _crossoverMode;
        set
        {
            _crossoverMode = value;
            UpdateCrossoverFilters();
        }
    }
    private CrossoverMode _crossoverMode = CrossoverMode.None;

    /// <summary>
    /// Gets or sets crossover frequency 1 (low/mid split) in Hz.
    /// </summary>
    public float CrossoverFreq1
    {
        get => _crossoverFreq1;
        set
        {
            _crossoverFreq1 = Math.Clamp(value, 20f, 20000f);
            UpdateCrossoverFilters();
        }
    }
    private float _crossoverFreq1 = 200f;

    /// <summary>
    /// Gets or sets crossover frequency 2 (mid/high split) in Hz.
    /// </summary>
    public float CrossoverFreq2
    {
        get => _crossoverFreq2;
        set
        {
            _crossoverFreq2 = Math.Clamp(value, 20f, 20000f);
            UpdateCrossoverFilters();
        }
    }
    private float _crossoverFreq2 = 2000f;

    /// <summary>
    /// Gets or sets crossover frequency 3 (high split) in Hz.
    /// </summary>
    public float CrossoverFreq3
    {
        get => _crossoverFreq3;
        set
        {
            _crossoverFreq3 = Math.Clamp(value, 20f, 20000f);
            UpdateCrossoverFilters();
        }
    }
    private float _crossoverFreq3 = 8000f;

    /// <summary>
    /// Gets or sets the mid/side processing mode.
    /// </summary>
    public MidSideMode MidSideMode { get; set; } = MidSideMode.Stereo;

    /// <summary>
    /// Gets or sets whether latency compensation is enabled.
    /// </summary>
    public bool LatencyCompensation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether phase alignment is enabled.
    /// </summary>
    public bool PhaseAlignment { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the processor is bypassed.
    /// </summary>
    public bool Bypassed { get; set; }

    /// <summary>
    /// Mix matrix values for blending between chains [fromChain, toOutput].
    /// Default is identity matrix (chain 0 -> output 0, etc.).
    /// </summary>
    public float[,] MixMatrix { get; } = new float[MaxChains, MaxChains];

    /// <summary>
    /// Gets the parallel chains.
    /// </summary>
    public IReadOnlyList<ParallelChain> Chains => _chains;

    /// <summary>
    /// Gets the total latency in samples.
    /// </summary>
    public int TotalLatencySamples => _maxLatency;

    /// <summary>
    /// Event raised when processing parameters change.
    /// </summary>
    public event EventHandler? ParametersChanged;

    /// <summary>
    /// Creates a new parallel processor.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public ParallelProcessor(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        WaveFormat = source.WaveFormat;

        // Initialize chains
        _chains = new ParallelChain[MaxChains];
        for (int i = 0; i < MaxChains; i++)
        {
            _chains[i] = new ParallelChain(i, Channels, SampleRate);
        }

        // Initialize chain buffers
        _chainInputBuffers = new float[MaxChains][];
        _chainOutputBuffers = new float[MaxChains][];
        for (int i = 0; i < MaxChains; i++)
        {
            _chainInputBuffers[i] = Array.Empty<float>();
            _chainOutputBuffers[i] = Array.Empty<float>();
        }

        // Initialize crossover filters (3 crossover points max)
        _crossoverLpCoeffs = new BiquadCoeffs[3];
        _crossoverHpCoeffs = new BiquadCoeffs[3];
        _crossoverLpState1 = new BiquadState[3, Channels];
        _crossoverLpState2 = new BiquadState[3, Channels];
        _crossoverHpState1 = new BiquadState[3, Channels];
        _crossoverHpState2 = new BiquadState[3, Channels];

        // Initialize delay lines for latency compensation
        int maxDelaySize = SampleRate; // 1 second max
        _delayLines = new float[MaxChains][];
        _delayWritePos = new int[MaxChains];
        for (int i = 0; i < MaxChains; i++)
        {
            _delayLines[i] = new float[maxDelaySize * Channels];
        }

        // Initialize mix matrix to identity
        for (int i = 0; i < MaxChains; i++)
        {
            MixMatrix[i, i] = 1.0f;
        }

        // Initialize macro mappings
        for (int i = 0; i < 8; i++)
        {
            _macroMappings[i] = new List<MacroMapping>();
        }

        UpdateCrossoverFilters();
    }

    /// <summary>
    /// Gets a chain by index.
    /// </summary>
    /// <param name="index">Chain index (0-3).</param>
    /// <returns>The parallel chain.</returns>
    public ParallelChain GetChain(int index)
    {
        return _chains[Math.Clamp(index, 0, MaxChains - 1)];
    }

    /// <summary>
    /// Sets the mix matrix value.
    /// </summary>
    /// <param name="fromChain">Source chain index.</param>
    /// <param name="toOutput">Output index.</param>
    /// <param name="amount">Mix amount (0.0 - 1.0).</param>
    public void SetMixMatrix(int fromChain, int toOutput, float amount)
    {
        if (fromChain >= 0 && fromChain < MaxChains && toOutput >= 0 && toOutput < MaxChains)
        {
            MixMatrix[fromChain, toOutput] = Math.Clamp(amount, 0f, 1f);
            ParametersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Applies a preset configuration.
    /// </summary>
    /// <param name="preset">The preset to apply.</param>
    public void ApplyPreset(ParallelPreset preset)
    {
        // Reset all chains first
        foreach (var chain in _chains)
        {
            chain.Level = 0f;
            chain.Pan = 0f;
            chain.Mute = true;
            chain.HighPassFrequency = 0f;
            chain.LowPassFrequency = 22000f;
            chain.ClearEffects();
        }

        DryLevel = 1.0f;
        CrossoverMode = CrossoverMode.None;
        MidSideMode = MidSideMode.Stereo;

        switch (preset)
        {
            case ParallelPreset.Default:
                // Simple parallel setup
                _chains[0].Name = "Dry";
                _chains[0].Level = 1.0f;
                _chains[0].Mute = false;
                _chains[1].Name = "Wet";
                _chains[1].Level = 0.5f;
                _chains[1].Mute = false;
                break;

            case ParallelPreset.NYCompression:
                // Classic NY compression setup
                DryLevel = 0.7f;
                _chains[0].Name = "Smash";
                _chains[0].Level = 0.5f;
                _chains[0].Mute = false;
                _chains[0].HighPassFrequency = 60f;  // Remove sub rumble
                // Note: Compressor effect should be added externally
                break;

            case ParallelPreset.ParallelDistortion:
                DryLevel = 0.8f;
                _chains[0].Name = "Drive";
                _chains[0].Level = 0.3f;
                _chains[0].Mute = false;
                _chains[0].HighPassFrequency = 200f;  // Remove low-end mud
                _chains[0].LowPassFrequency = 8000f;  // Tame harsh highs
                break;

            case ParallelPreset.ParallelExciter:
                DryLevel = 0.9f;
                _chains[0].Name = "Air";
                _chains[0].Level = 0.2f;
                _chains[0].Mute = false;
                _chains[0].HighPassFrequency = 4000f;  // Only highs
                break;

            case ParallelPreset.MultiBand:
                CrossoverMode = CrossoverMode.FourBand;
                CrossoverFreq1 = 150f;
                CrossoverFreq2 = 1000f;
                CrossoverFreq3 = 6000f;
                DryLevel = 0f;  // All through chains

                _chains[0].Name = "Sub";
                _chains[0].Level = 1.0f;
                _chains[0].Mute = false;

                _chains[1].Name = "Low-Mid";
                _chains[1].Level = 1.0f;
                _chains[1].Mute = false;

                _chains[2].Name = "High-Mid";
                _chains[2].Level = 1.0f;
                _chains[2].Mute = false;

                _chains[3].Name = "Air";
                _chains[3].Level = 1.0f;
                _chains[3].Mute = false;
                break;

            case ParallelPreset.DrumBus:
                DryLevel = 0.6f;
                _chains[0].Name = "Punch";
                _chains[0].Level = 0.5f;
                _chains[0].Mute = false;
                _chains[0].HighPassFrequency = 100f;
                _chains[0].EnvelopeFollowerEnabled = true;
                _chains[0].EnvelopeAttack = 0.001f;
                _chains[0].EnvelopeRelease = 0.05f;

                _chains[1].Name = "Sustain";
                _chains[1].Level = 0.3f;
                _chains[1].Mute = false;
                break;

            case ParallelPreset.VocalChain:
                DryLevel = 0.7f;
                _chains[0].Name = "Body";
                _chains[0].Level = 0.4f;
                _chains[0].Mute = false;
                _chains[0].HighPassFrequency = 80f;
                _chains[0].LowPassFrequency = 3000f;

                _chains[1].Name = "Presence";
                _chains[1].Level = 0.3f;
                _chains[1].Mute = false;
                _chains[1].HighPassFrequency = 2000f;
                _chains[1].LowPassFrequency = 10000f;
                break;

            case ParallelPreset.BassEnhancer:
                DryLevel = 0.8f;
                _chains[0].Name = "Sub";
                _chains[0].Level = 0.3f;
                _chains[0].Mute = false;
                _chains[0].LowPassFrequency = 100f;

                _chains[1].Name = "Harmonics";
                _chains[1].Level = 0.2f;
                _chains[1].Mute = false;
                _chains[1].HighPassFrequency = 100f;
                _chains[1].LowPassFrequency = 500f;
                break;
        }

        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets a macro control value.
    /// </summary>
    /// <param name="macroIndex">Macro index (0-7).</param>
    /// <returns>Macro value (0.0 - 1.0).</returns>
    public float GetMacro(int macroIndex)
    {
        if (macroIndex < 0 || macroIndex >= 8) return 0f;
        return _macroValues[macroIndex];
    }

    /// <summary>
    /// Sets a macro control value and updates mapped parameters.
    /// </summary>
    /// <param name="macroIndex">Macro index (0-7).</param>
    /// <param name="value">Value (0.0 - 1.0).</param>
    public void SetMacro(int macroIndex, float value)
    {
        if (macroIndex < 0 || macroIndex >= 8) return;

        _macroValues[macroIndex] = Math.Clamp(value, 0f, 1f);

        // Apply to mapped parameters
        foreach (var mapping in _macroMappings[macroIndex])
        {
            float mappedValue = mapping.MinValue + (mapping.MaxValue - mapping.MinValue) * value;
            mapping.SetValue?.Invoke(mappedValue);
        }

        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a macro mapping.
    /// </summary>
    /// <param name="macroIndex">Macro index (0-7).</param>
    /// <param name="name">Parameter name.</param>
    /// <param name="minValue">Minimum value.</param>
    /// <param name="maxValue">Maximum value.</param>
    /// <param name="setValue">Action to set the parameter value.</param>
    public void AddMacroMapping(int macroIndex, string name, float minValue, float maxValue, Action<float> setValue)
    {
        if (macroIndex < 0 || macroIndex >= 8) return;

        _macroMappings[macroIndex].Add(new MacroMapping
        {
            Name = name,
            MinValue = minValue,
            MaxValue = maxValue,
            SetValue = setValue
        });
    }

    /// <summary>
    /// Clears macro mappings for a specific macro.
    /// </summary>
    /// <param name="macroIndex">Macro index (0-7).</param>
    public void ClearMacroMappings(int macroIndex)
    {
        if (macroIndex >= 0 && macroIndex < 8)
        {
            _macroMappings[macroIndex].Clear();
        }
    }

    /// <summary>
    /// Saves current state for A/B comparison.
    /// </summary>
    /// <param name="isStateA">True to save as state A, false for state B.</param>
    public void SaveState(bool isStateA)
    {
        var state = new ParallelProcessorState
        {
            DryLevel = DryLevel,
            OutputGain = OutputGain,
            CrossoverMode = CrossoverMode,
            CrossoverFreq1 = CrossoverFreq1,
            CrossoverFreq2 = CrossoverFreq2,
            CrossoverFreq3 = CrossoverFreq3,
            MidSideMode = MidSideMode,
            BlendCurve = BlendCurve,
            ChainStates = new ChainState[MaxChains]
        };

        for (int i = 0; i < MaxChains; i++)
        {
            state.ChainStates[i] = new ChainState
            {
                Level = _chains[i].Level,
                Pan = _chains[i].Pan,
                Mute = _chains[i].Mute,
                HighPassFrequency = _chains[i].HighPassFrequency,
                LowPassFrequency = _chains[i].LowPassFrequency,
                PhaseInvert = _chains[i].PhaseInvert
            };
        }

        if (isStateA)
            _stateA = state;
        else
            _stateB = state;
    }

    /// <summary>
    /// Loads a saved state for A/B comparison.
    /// </summary>
    /// <param name="isStateA">True to load state A, false for state B.</param>
    public void LoadState(bool isStateA)
    {
        var state = isStateA ? _stateA : _stateB;
        if (state == null) return;

        DryLevel = state.DryLevel;
        OutputGain = state.OutputGain;
        CrossoverMode = state.CrossoverMode;
        CrossoverFreq1 = state.CrossoverFreq1;
        CrossoverFreq2 = state.CrossoverFreq2;
        CrossoverFreq3 = state.CrossoverFreq3;
        MidSideMode = state.MidSideMode;
        BlendCurve = state.BlendCurve;

        for (int i = 0; i < MaxChains && i < state.ChainStates.Length; i++)
        {
            var cs = state.ChainStates[i];
            _chains[i].Level = cs.Level;
            _chains[i].Pan = cs.Pan;
            _chains[i].Mute = cs.Mute;
            _chains[i].HighPassFrequency = cs.HighPassFrequency;
            _chains[i].LowPassFrequency = cs.LowPassFrequency;
            _chains[i].PhaseInvert = cs.PhaseInvert;
        }

        _isStateA = isStateA;
        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles between A/B states.
    /// </summary>
    public void ToggleAB()
    {
        SaveState(_isStateA);
        LoadState(!_isStateA);
    }

    /// <summary>
    /// Gets the current A/B state (true = A, false = B).
    /// </summary>
    public bool IsStateA => _isStateA;

    private void UpdateCrossoverFilters()
    {
        float[] freqs = { _crossoverFreq1, _crossoverFreq2, _crossoverFreq3 };

        for (int i = 0; i < 3; i++)
        {
            // Linkwitz-Riley crossover using cascaded Butterworth
            _crossoverLpCoeffs[i] = BiquadCoeffs.Lowpass(SampleRate, freqs[i], 0.707f);
            _crossoverHpCoeffs[i] = BiquadCoeffs.Highpass(SampleRate, freqs[i], 0.707f);
        }
    }

    private void UpdateLatencyCompensation()
    {
        if (!LatencyCompensation) return;

        // Find maximum latency across all enabled chains
        _maxLatency = 0;
        foreach (var chain in _chains)
        {
            if (!chain.Mute && !chain.Bypass)
            {
                _maxLatency = Math.Max(_maxLatency, chain.LatencySamples);
            }
        }
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Ensure buffers are large enough
        EnsureBufferSize(count);

        // Read from source
        int samplesRead = _source.Read(_sourceBuffer, 0, count);
        if (samplesRead == 0) return 0;

        // If bypassed, just copy source to output
        if (Bypassed)
        {
            Array.Copy(_sourceBuffer, 0, buffer, offset, samplesRead);
            return samplesRead;
        }

        // Check if any chain is soloed
        bool anySolo = false;
        foreach (var chain in _chains)
        {
            if (chain.Solo) { anySolo = true; break; }
        }

        // Store dry signal
        Array.Copy(_sourceBuffer, 0, _dryBuffer, 0, samplesRead);

        // Apply mid/side encoding if needed
        if (MidSideMode != MidSideMode.Stereo && Channels == 2)
        {
            EncodeMidSide(_sourceBuffer, samplesRead);
        }

        // Prepare chain inputs based on crossover mode
        PrepareChainInputs(_sourceBuffer, samplesRead);

        // Update latency compensation
        UpdateLatencyCompensation();

        // Process each chain
        for (int i = 0; i < MaxChains; i++)
        {
            var chain = _chains[i];

            // CPU efficiency: skip if chain won't contribute
            bool shouldProcess = !chain.Mute && !chain.Bypass;
            if (anySolo && !chain.Solo) shouldProcess = false;
            if (chain.Level < 0.0001f) shouldProcess = false;

            if (shouldProcess)
            {
                // Get sidechain input if configured
                float[]? sidechainInput = null;
                if (chain.SidechainSourceChain >= 0 && chain.SidechainSourceChain < MaxChains)
                {
                    sidechainInput = _chainOutputBuffers[chain.SidechainSourceChain];
                }

                chain.Process(_chainInputBuffers[i], _chainOutputBuffers[i], 0, samplesRead, sidechainInput);

                // Apply latency compensation delay if needed
                if (LatencyCompensation && chain.LatencySamples < _maxLatency)
                {
                    int delayNeeded = _maxLatency - chain.LatencySamples;
                    ApplyDelay(_chainOutputBuffers[i], i, samplesRead, delayNeeded);
                }
            }
            else
            {
                // Clear output for inactive chains
                Array.Clear(_chainOutputBuffers[i], 0, samplesRead);
            }
        }

        // Sum chains to output using mix matrix and blend curve
        Array.Clear(buffer, offset, samplesRead);

        // Add dry signal
        float dryGain = ApplyBlendCurve(DryLevel);
        for (int i = 0; i < samplesRead; i++)
        {
            buffer[offset + i] += _dryBuffer[i] * dryGain;
        }

        // Add chain outputs through mix matrix
        for (int fromChain = 0; fromChain < MaxChains; fromChain++)
        {
            var chain = _chains[fromChain];
            if (chain.Mute || chain.Bypass) continue;
            if (anySolo && !chain.Solo) continue;

            float chainSum = 0f;
            for (int toOutput = 0; toOutput < MaxChains; toOutput++)
            {
                chainSum += MixMatrix[fromChain, toOutput];
            }

            if (chainSum > 0.0001f)
            {
                float chainGain = ApplyBlendCurve(chain.Level);
                for (int i = 0; i < samplesRead; i++)
                {
                    buffer[offset + i] += _chainOutputBuffers[fromChain][i] * chainGain;
                }
            }
        }

        // Apply mid/side decoding if needed
        if (MidSideMode != MidSideMode.Stereo && Channels == 2)
        {
            DecodeMidSide(buffer, offset, samplesRead);
        }

        // Apply output gain
        if (Math.Abs(OutputGain - 1.0f) > 0.0001f)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] *= OutputGain;
            }
        }

        return samplesRead;
    }

    private void EnsureBufferSize(int count)
    {
        if (_sourceBuffer.Length < count)
        {
            _sourceBuffer = new float[count];
            _dryBuffer = new float[count];
            _midBuffer = new float[count];
            _sideBuffer = new float[count];

            for (int i = 0; i < MaxChains; i++)
            {
                _chainInputBuffers[i] = new float[count];
                _chainOutputBuffers[i] = new float[count];
            }
        }
    }

    private void PrepareChainInputs(float[] source, int count)
    {
        switch (CrossoverMode)
        {
            case CrossoverMode.None:
                // All chains get full bandwidth
                for (int i = 0; i < MaxChains; i++)
                {
                    Array.Copy(source, 0, _chainInputBuffers[i], 0, count);
                }
                break;

            case CrossoverMode.TwoBand:
                // Chain 0 = low, Chain 1 = high
                ApplyCrossover2Band(source, count);
                // Chains 2 and 3 get full signal as fallback
                Array.Copy(source, 0, _chainInputBuffers[2], 0, count);
                Array.Copy(source, 0, _chainInputBuffers[3], 0, count);
                break;

            case CrossoverMode.ThreeBand:
                // Chain 0 = low, Chain 1 = mid, Chain 2 = high
                ApplyCrossover3Band(source, count);
                // Chain 3 gets full signal as fallback
                Array.Copy(source, 0, _chainInputBuffers[3], 0, count);
                break;

            case CrossoverMode.FourBand:
                // Chain 0 = low, Chain 1 = low-mid, Chain 2 = high-mid, Chain 3 = high
                ApplyCrossover4Band(source, count);
                break;
        }
    }

    private void ApplyCrossover2Band(float[] source, int count)
    {
        // Copy source to both chain inputs
        Array.Copy(source, 0, _chainInputBuffers[0], 0, count);
        Array.Copy(source, 0, _chainInputBuffers[1], 0, count);

        // Apply LP to chain 0, HP to chain 1 (both 2-stage for Linkwitz-Riley)
        ApplyBiquadFilter(_chainInputBuffers[0], count, _crossoverLpCoeffs[0], _crossoverLpState1, 0);
        ApplyBiquadFilter(_chainInputBuffers[0], count, _crossoverLpCoeffs[0], _crossoverLpState2, 0);

        ApplyBiquadFilter(_chainInputBuffers[1], count, _crossoverHpCoeffs[0], _crossoverHpState1, 0);
        ApplyBiquadFilter(_chainInputBuffers[1], count, _crossoverHpCoeffs[0], _crossoverHpState2, 0);
    }

    private void ApplyCrossover3Band(float[] source, int count)
    {
        // First split at freq1
        Array.Copy(source, 0, _chainInputBuffers[0], 0, count);
        var highMidHigh = new float[count];
        Array.Copy(source, 0, highMidHigh, 0, count);

        // Chain 0 = low (LP at freq1)
        ApplyBiquadFilter(_chainInputBuffers[0], count, _crossoverLpCoeffs[0], _crossoverLpState1, 0);
        ApplyBiquadFilter(_chainInputBuffers[0], count, _crossoverLpCoeffs[0], _crossoverLpState2, 0);

        // High part (HP at freq1)
        ApplyBiquadFilter(highMidHigh, count, _crossoverHpCoeffs[0], _crossoverHpState1, 0);
        ApplyBiquadFilter(highMidHigh, count, _crossoverHpCoeffs[0], _crossoverHpState2, 0);

        // Second split at freq2
        Array.Copy(highMidHigh, 0, _chainInputBuffers[1], 0, count);
        Array.Copy(highMidHigh, 0, _chainInputBuffers[2], 0, count);

        // Chain 1 = mid (LP at freq2)
        ApplyBiquadFilter(_chainInputBuffers[1], count, _crossoverLpCoeffs[1], _crossoverLpState1, 1);
        ApplyBiquadFilter(_chainInputBuffers[1], count, _crossoverLpCoeffs[1], _crossoverLpState2, 1);

        // Chain 2 = high (HP at freq2)
        ApplyBiquadFilter(_chainInputBuffers[2], count, _crossoverHpCoeffs[1], _crossoverHpState1, 1);
        ApplyBiquadFilter(_chainInputBuffers[2], count, _crossoverHpCoeffs[1], _crossoverHpState2, 1);
    }

    private void ApplyCrossover4Band(float[] source, int count)
    {
        // First split at freq1
        Array.Copy(source, 0, _chainInputBuffers[0], 0, count);
        var midHighHigh = new float[count];
        Array.Copy(source, 0, midHighHigh, 0, count);

        // Chain 0 = low (LP at freq1)
        ApplyBiquadFilter(_chainInputBuffers[0], count, _crossoverLpCoeffs[0], _crossoverLpState1, 0);
        ApplyBiquadFilter(_chainInputBuffers[0], count, _crossoverLpCoeffs[0], _crossoverLpState2, 0);

        // HP at freq1
        ApplyBiquadFilter(midHighHigh, count, _crossoverHpCoeffs[0], _crossoverHpState1, 0);
        ApplyBiquadFilter(midHighHigh, count, _crossoverHpCoeffs[0], _crossoverHpState2, 0);

        // Second split at freq2
        Array.Copy(midHighHigh, 0, _chainInputBuffers[1], 0, count);
        var highMidHigh = new float[count];
        Array.Copy(midHighHigh, 0, highMidHigh, 0, count);

        // Chain 1 = low-mid (LP at freq2)
        ApplyBiquadFilter(_chainInputBuffers[1], count, _crossoverLpCoeffs[1], _crossoverLpState1, 1);
        ApplyBiquadFilter(_chainInputBuffers[1], count, _crossoverLpCoeffs[1], _crossoverLpState2, 1);

        // HP at freq2
        ApplyBiquadFilter(highMidHigh, count, _crossoverHpCoeffs[1], _crossoverHpState1, 1);
        ApplyBiquadFilter(highMidHigh, count, _crossoverHpCoeffs[1], _crossoverHpState2, 1);

        // Third split at freq3
        Array.Copy(highMidHigh, 0, _chainInputBuffers[2], 0, count);
        Array.Copy(highMidHigh, 0, _chainInputBuffers[3], 0, count);

        // Chain 2 = high-mid (LP at freq3)
        ApplyBiquadFilter(_chainInputBuffers[2], count, _crossoverLpCoeffs[2], _crossoverLpState1, 2);
        ApplyBiquadFilter(_chainInputBuffers[2], count, _crossoverLpCoeffs[2], _crossoverLpState2, 2);

        // Chain 3 = high (HP at freq3)
        ApplyBiquadFilter(_chainInputBuffers[3], count, _crossoverHpCoeffs[2], _crossoverHpState1, 2);
        ApplyBiquadFilter(_chainInputBuffers[3], count, _crossoverHpCoeffs[2], _crossoverHpState2, 2);
    }

    private void ApplyBiquadFilter(float[] buffer, int count, BiquadCoeffs coeffs, BiquadState[,] states, int filterIndex)
    {
        // Direct Form II Transposed implementation
        for (int i = 0; i < count; i += Channels)
        {
            for (int ch = 0; ch < Channels; ch++)
            {
                float input = buffer[i + ch];
                ref var state = ref states[filterIndex, ch];

                // Direct Form II Transposed:
                // y[n] = b0*x[n] + z1
                // z1 = b1*x[n] - a1*y[n] + z2
                // z2 = b2*x[n] - a2*y[n]
                float output = coeffs.B0 * input + state.Z1;
                state.Z1 = coeffs.B1 * input - coeffs.A1 * output + state.Z2;
                state.Z2 = coeffs.B2 * input - coeffs.A2 * output;

                buffer[i + ch] = output;
            }
        }
    }

    private void EncodeMidSide(float[] buffer, int count)
    {
        if (Channels != 2) return;

        for (int i = 0; i < count; i += 2)
        {
            float left = buffer[i];
            float right = buffer[i + 1];

            float mid = (left + right) * 0.5f;
            float side = (left - right) * 0.5f;

            switch (MidSideMode)
            {
                case MidSideMode.MidOnly:
                    buffer[i] = mid;
                    buffer[i + 1] = mid;
                    break;
                case MidSideMode.SideOnly:
                    buffer[i] = side;
                    buffer[i + 1] = -side;
                    break;
                case MidSideMode.MidSideSplit:
                    // Store mid in left, side in right for separate processing
                    buffer[i] = mid;
                    buffer[i + 1] = side;
                    break;
            }
        }
    }

    private void DecodeMidSide(float[] buffer, int offset, int count)
    {
        if (Channels != 2) return;
        if (MidSideMode == MidSideMode.MidOnly || MidSideMode == MidSideMode.SideOnly)
            return; // Already in stereo format

        if (MidSideMode == MidSideMode.MidSideSplit)
        {
            for (int i = 0; i < count; i += 2)
            {
                float mid = buffer[offset + i];
                float side = buffer[offset + i + 1];

                buffer[offset + i] = mid + side;      // Left
                buffer[offset + i + 1] = mid - side;  // Right
            }
        }
    }

    private void ApplyDelay(float[] buffer, int chainIndex, int count, int delaySamples)
    {
        if (delaySamples <= 0) return;

        var delayLine = _delayLines[chainIndex];
        ref int writePos = ref _delayWritePos[chainIndex];
        int delayLineSize = delayLine.Length;

        for (int i = 0; i < count; i++)
        {
            // Read from delay line
            int readPos = (writePos - delaySamples * Channels + delayLineSize) % delayLineSize;
            float delayed = delayLine[readPos];

            // Write current sample to delay line
            delayLine[writePos] = buffer[i];

            // Output delayed sample
            buffer[i] = delayed;

            // Advance write position
            writePos = (writePos + 1) % delayLineSize;
        }
    }

    private float ApplyBlendCurve(float value)
    {
        return BlendCurve switch
        {
            BlendCurveType.Linear => value,
            BlendCurveType.EqualPower => MathF.Sqrt(value),
            BlendCurveType.Logarithmic => value > 0 ? MathF.Log10(1f + 9f * value) : 0f,
            BlendCurveType.SCurve => value * value * (3f - 2f * value),
            _ => value
        };
    }

    /// <summary>
    /// Resets all processing state.
    /// </summary>
    public void Reset()
    {
        foreach (var chain in _chains)
        {
            chain.Reset();
        }

        // Reset crossover filter states
        for (int f = 0; f < 3; f++)
        {
            for (int ch = 0; ch < Channels; ch++)
            {
                _crossoverLpState1[f, ch] = default;
                _crossoverLpState2[f, ch] = default;
                _crossoverHpState1[f, ch] = default;
                _crossoverHpState2[f, ch] = default;
            }
        }

        // Reset delay lines
        for (int i = 0; i < MaxChains; i++)
        {
            Array.Clear(_delayLines[i]);
            _delayWritePos[i] = 0;
        }
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        foreach (var chain in _chains)
        {
            chain.Dispose();
        }
    }

    /// <summary>
    /// Creates a parallel processor with a specific preset.
    /// </summary>
    /// <param name="source">Audio source.</param>
    /// <param name="preset">Preset to apply.</param>
    /// <returns>Configured parallel processor.</returns>
    public static ParallelProcessor CreateWithPreset(ISampleProvider source, ParallelPreset preset)
    {
        var processor = new ParallelProcessor(source);
        processor.ApplyPreset(preset);
        return processor;
    }
}

/// <summary>
/// Mapping from a macro control to a parameter.
/// </summary>
internal class MacroMapping
{
    public string Name { get; set; } = "";
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public Action<float>? SetValue { get; set; }
}

/// <summary>
/// State snapshot for A/B comparison.
/// </summary>
internal class ParallelProcessorState
{
    public float DryLevel { get; set; }
    public float OutputGain { get; set; }
    public CrossoverMode CrossoverMode { get; set; }
    public float CrossoverFreq1 { get; set; }
    public float CrossoverFreq2 { get; set; }
    public float CrossoverFreq3 { get; set; }
    public MidSideMode MidSideMode { get; set; }
    public BlendCurveType BlendCurve { get; set; }
    public ChainState[] ChainStates { get; set; } = Array.Empty<ChainState>();
}

/// <summary>
/// State snapshot for a single chain.
/// </summary>
internal class ChainState
{
    public float Level { get; set; }
    public float Pan { get; set; }
    public bool Mute { get; set; }
    public float HighPassFrequency { get; set; }
    public float LowPassFrequency { get; set; }
    public bool PhaseInvert { get; set; }
}
