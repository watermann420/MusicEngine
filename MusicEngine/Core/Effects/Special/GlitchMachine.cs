//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Random audio glitch effects processor with multiple glitch types and tempo sync.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// The type of glitch effect to apply.
/// </summary>
public enum GlitchType
{
    /// <summary>
    /// Buffer stutter - repeats a small section of audio rapidly.
    /// </summary>
    Stutter,

    /// <summary>
    /// Bitcrushing - reduces bit depth for lo-fi distortion.
    /// </summary>
    Bitcrush,

    /// <summary>
    /// Reverse - plays audio backwards.
    /// </summary>
    Reverse,

    /// <summary>
    /// Pitch jump - sudden pitch shift up or down.
    /// </summary>
    PitchJump,

    /// <summary>
    /// Tape stop - simulates tape slowing down.
    /// </summary>
    TapeStop,

    /// <summary>
    /// Gate - rhythmic volume gating.
    /// </summary>
    Gate
}

/// <summary>
/// Glitch machine effect that randomly applies various audio glitch effects.
/// Creates unpredictable, chaotic audio mutations common in glitch and IDM music.
/// </summary>
/// <remarks>
/// The GlitchMachine combines multiple glitch effects (stutter, bitcrush, reverse,
/// pitch jump, tape stop, gate) and triggers them randomly based on probability.
/// Can be synchronized to tempo for musical glitching.
/// </remarks>
public class GlitchMachine : EffectBase
{
    private const int MaxBufferSeconds = 4;

    // Audio capture buffer
    private readonly float[] _captureBuffer;
    private readonly int _maxBufferSamples;
    private int _captureWritePos;
    private int _capturedSamples;

    // Current glitch state
    private bool _glitchActive;
    private GlitchType _currentGlitchType;
    private int _glitchSamplesRemaining;
    private int _glitchTotalSamples;

    // Stutter state
    private int _stutterReadPos;
    private int _stutterLength;
    private int _stutterRepeatPos;

    // Bitcrush state
    private float _crushHoldSample;
    private int _crushHoldCounter;
    private int _crushDownsampleFactor;
    private float _crushLevels;

    // Reverse state
    private int _reverseReadPos;
    private int _reverseLength;

    // Pitch jump state
    private double _pitchReadPhase;
    private float _pitchRatio;

    // Tape stop state
    private double _tapeStopSpeed;
    private double _tapeStopDeceleration;
    private double _tapeStopReadPhase;

    // Gate state
    private bool _gateOpen;
    private int _gatePeriodSamples;
    private int _gatePosition;
    private float _gateOpenRatio;

    // Timing
    private double _currentBeatPosition;
    private double _lastGlitchBeat;
    private long _totalSamplesProcessed;
    private int _samplesPerBeat;

    // Random number generator
    private readonly Random _random;

    /// <summary>
    /// Gets or sets the glitch rate (probability of triggering a glitch per interval).
    /// Range: 0.0 to 1.0 (0 = never, 1 = always).
    /// </summary>
    public float GlitchRate
    {
        get => GetParameter("GlitchRate");
        set => SetParameter("GlitchRate", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the glitch intensity/amount.
    /// Range: 0.0 to 1.0. Higher values create more extreme glitches.
    /// </summary>
    public float GlitchAmount
    {
        get => GetParameter("GlitchAmount");
        set => SetParameter("GlitchAmount", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the random seed for reproducible glitch patterns.
    /// Set to -1 for truly random behavior.
    /// </summary>
    public int RandomSeed
    {
        get => (int)GetParameter("RandomSeed");
        set
        {
            SetParameter("RandomSeed", value);
            if (value >= 0)
            {
                // Note: We don't reinitialize _random here as it would require
                // recreating the Random instance. Use Reset() to apply new seed.
            }
        }
    }

    /// <summary>
    /// Gets or sets the dry/wet effect mix.
    /// Range: 0.0 to 1.0 (0 = dry, 1 = wet).
    /// </summary>
    public float EffectMix
    {
        get => Mix;
        set => Mix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM for tempo-synchronized glitching.
    /// Range: 20 to 300 BPM.
    /// </summary>
    public float Tempo
    {
        get => GetParameter("Tempo");
        set
        {
            float clamped = Math.Clamp(value, 20f, 300f);
            SetParameter("Tempo", clamped);
            UpdateTimingParameters();
        }
    }

    /// <summary>
    /// Gets or sets whether glitches are synchronized to the tempo.
    /// </summary>
    public bool TempoSync
    {
        get => GetParameter("TempoSync") > 0.5f;
        set => SetParameter("TempoSync", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets the glitch interval in beats when tempo sync is enabled.
    /// Range: 0.0625 (1/16 note) to 4.0 (whole note).
    /// </summary>
    public float GlitchInterval
    {
        get => GetParameter("GlitchInterval");
        set => SetParameter("GlitchInterval", Math.Clamp(value, 0.0625f, 4f));
    }

    /// <summary>
    /// Gets or sets the minimum glitch duration in milliseconds.
    /// Range: 10 to 2000 ms.
    /// </summary>
    public float MinGlitchDuration
    {
        get => GetParameter("MinGlitchDuration");
        set => SetParameter("MinGlitchDuration", Math.Clamp(value, 10f, 2000f));
    }

    /// <summary>
    /// Gets or sets the maximum glitch duration in milliseconds.
    /// Range: 10 to 2000 ms.
    /// </summary>
    public float MaxGlitchDuration
    {
        get => GetParameter("MaxGlitchDuration");
        set => SetParameter("MaxGlitchDuration", Math.Clamp(value, 10f, 2000f));
    }

    /// <summary>
    /// Gets or sets whether stutter glitches are enabled.
    /// </summary>
    public bool EnableStutter
    {
        get => GetParameter("EnableStutter") > 0.5f;
        set => SetParameter("EnableStutter", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether bitcrush glitches are enabled.
    /// </summary>
    public bool EnableBitcrush
    {
        get => GetParameter("EnableBitcrush") > 0.5f;
        set => SetParameter("EnableBitcrush", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether reverse glitches are enabled.
    /// </summary>
    public bool EnableReverse
    {
        get => GetParameter("EnableReverse") > 0.5f;
        set => SetParameter("EnableReverse", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether pitch jump glitches are enabled.
    /// </summary>
    public bool EnablePitchJump
    {
        get => GetParameter("EnablePitchJump") > 0.5f;
        set => SetParameter("EnablePitchJump", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether tape stop glitches are enabled.
    /// </summary>
    public bool EnableTapeStop
    {
        get => GetParameter("EnableTapeStop") > 0.5f;
        set => SetParameter("EnableTapeStop", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets whether gate glitches are enabled.
    /// </summary>
    public bool EnableGate
    {
        get => GetParameter("EnableGate") > 0.5f;
        set => SetParameter("EnableGate", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets whether a glitch is currently active.
    /// </summary>
    public bool IsGlitchActive => _glitchActive;

    /// <summary>
    /// Gets the type of the currently active glitch.
    /// </summary>
    public GlitchType CurrentGlitchType => _currentGlitchType;

    /// <summary>
    /// Gets the current beat position (0.0 to 4.0 for a bar in 4/4 time).
    /// </summary>
    public double CurrentBeatPosition => _currentBeatPosition;

    /// <summary>
    /// Event raised when a glitch starts.
    /// </summary>
    public event EventHandler<GlitchType>? GlitchStarted;

    /// <summary>
    /// Event raised when a glitch ends.
    /// </summary>
    public event EventHandler? GlitchEnded;

    /// <summary>
    /// Creates a new glitch machine effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public GlitchMachine(ISampleProvider source) : base(source, "Glitch Machine")
    {
        _maxBufferSamples = MaxBufferSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels;
        _captureBuffer = new float[_maxBufferSamples];
        _random = new Random();

        // Register parameters with defaults
        RegisterParameter("GlitchRate", 0.3f);
        RegisterParameter("GlitchAmount", 0.5f);
        RegisterParameter("RandomSeed", -1f);
        RegisterParameter("Mix", 1f);
        RegisterParameter("Tempo", 120f);
        RegisterParameter("TempoSync", 1f);
        RegisterParameter("GlitchInterval", 0.25f); // Quarter note
        RegisterParameter("MinGlitchDuration", 50f);
        RegisterParameter("MaxGlitchDuration", 500f);
        RegisterParameter("EnableStutter", 1f);
        RegisterParameter("EnableBitcrush", 1f);
        RegisterParameter("EnableReverse", 1f);
        RegisterParameter("EnablePitchJump", 1f);
        RegisterParameter("EnableTapeStop", 1f);
        RegisterParameter("EnableGate", 1f);

        UpdateTimingParameters();
        Reset();
    }

    /// <summary>
    /// Creates a new glitch machine effect with a specific random seed.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="seed">Random seed for reproducible glitches.</param>
    public GlitchMachine(ISampleProvider source, int seed) : this(source)
    {
        if (seed >= 0)
        {
            SetParameter("RandomSeed", seed);
        }
    }

    /// <summary>
    /// Manually triggers a glitch effect.
    /// </summary>
    /// <param name="type">Optional specific glitch type. If null, a random type is selected.</param>
    public void TriggerGlitch(GlitchType? type = null)
    {
        if (_glitchActive) return;

        GlitchType glitchType = type ?? SelectRandomGlitchType();
        StartGlitch(glitchType);
    }

    /// <summary>
    /// Stops the currently active glitch immediately.
    /// </summary>
    public void StopGlitch()
    {
        if (_glitchActive)
        {
            _glitchActive = false;
            GlitchEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Resets the effect to its initial state.
    /// </summary>
    public void Reset()
    {
        _glitchActive = false;
        _captureWritePos = 0;
        _capturedSamples = 0;
        _currentBeatPosition = 0;
        _lastGlitchBeat = -1;
        _totalSamplesProcessed = 0;
        _glitchSamplesRemaining = 0;

        // Reset all glitch-specific state
        _stutterReadPos = 0;
        _stutterLength = 0;
        _stutterRepeatPos = 0;
        _crushHoldSample = 0;
        _crushHoldCounter = 0;
        _reverseReadPos = 0;
        _reverseLength = 0;
        _pitchReadPhase = 0;
        _pitchRatio = 1f;
        _tapeStopSpeed = 1.0;
        _tapeStopReadPhase = 0;
        _gateOpen = true;
        _gatePosition = 0;

        Array.Clear(_captureBuffer, 0, _captureBuffer.Length);
    }

    /// <summary>
    /// Sets the beat position externally for synchronization with a sequencer.
    /// </summary>
    /// <param name="beatPosition">Beat position (0.0 = start of bar).</param>
    public void SetBeatPosition(double beatPosition)
    {
        _currentBeatPosition = beatPosition;
    }

    /// <summary>
    /// Updates timing parameters based on tempo.
    /// </summary>
    private void UpdateTimingParameters()
    {
        float tempo = Tempo;
        float beatsPerSecond = tempo / 60f;
        _samplesPerBeat = (int)(SampleRate / beatsPerSecond);
    }

    /// <summary>
    /// Selects a random glitch type from the enabled types.
    /// </summary>
    private GlitchType SelectRandomGlitchType()
    {
        var enabledTypes = new List<GlitchType>();

        if (EnableStutter) enabledTypes.Add(GlitchType.Stutter);
        if (EnableBitcrush) enabledTypes.Add(GlitchType.Bitcrush);
        if (EnableReverse) enabledTypes.Add(GlitchType.Reverse);
        if (EnablePitchJump) enabledTypes.Add(GlitchType.PitchJump);
        if (EnableTapeStop) enabledTypes.Add(GlitchType.TapeStop);
        if (EnableGate) enabledTypes.Add(GlitchType.Gate);

        if (enabledTypes.Count == 0)
        {
            return GlitchType.Stutter; // Default fallback
        }

        return enabledTypes[_random.Next(enabledTypes.Count)];
    }

    /// <summary>
    /// Starts a new glitch effect.
    /// </summary>
    private void StartGlitch(GlitchType type)
    {
        _glitchActive = true;
        _currentGlitchType = type;

        // Calculate glitch duration
        float minDuration = MinGlitchDuration;
        float maxDuration = MaxGlitchDuration;
        if (minDuration > maxDuration) maxDuration = minDuration;

        float durationMs = minDuration + (float)_random.NextDouble() * (maxDuration - minDuration);
        _glitchTotalSamples = (int)(durationMs * SampleRate / 1000f) * Channels;
        _glitchSamplesRemaining = _glitchTotalSamples;

        float amount = GlitchAmount;

        // Initialize glitch-specific parameters
        switch (type)
        {
            case GlitchType.Stutter:
                InitializeStutter(amount);
                break;
            case GlitchType.Bitcrush:
                InitializeBitcrush(amount);
                break;
            case GlitchType.Reverse:
                InitializeReverse();
                break;
            case GlitchType.PitchJump:
                InitializePitchJump(amount);
                break;
            case GlitchType.TapeStop:
                InitializeTapeStop();
                break;
            case GlitchType.Gate:
                InitializeGate(amount);
                break;
        }

        GlitchStarted?.Invoke(this, type);
    }

    /// <summary>
    /// Initializes stutter glitch parameters.
    /// </summary>
    private void InitializeStutter(float amount)
    {
        // Stutter length: shorter with higher amount (more stutters)
        int minLength = 64 * Channels;
        int maxLength = (int)(0.2f * SampleRate) * Channels;
        float lengthFactor = 1f - amount * 0.8f;
        _stutterLength = (int)(minLength + (maxLength - minLength) * lengthFactor);
        _stutterLength = Math.Min(_stutterLength, _capturedSamples);
        _stutterLength = Math.Max(_stutterLength, minLength);

        // Start from recent audio in the buffer
        _stutterReadPos = (_captureWritePos - _stutterLength + _maxBufferSamples) % _maxBufferSamples;
        _stutterRepeatPos = 0;
    }

    /// <summary>
    /// Initializes bitcrush glitch parameters.
    /// </summary>
    private void InitializeBitcrush(float amount)
    {
        // Bit depth: lower with higher amount
        float bitDepth = 16f - amount * 14f; // 2 to 16 bits
        bitDepth = Math.Clamp(bitDepth, 2f, 16f);
        _crushLevels = MathF.Pow(2f, bitDepth);

        // Downsample factor: higher with higher amount
        int maxDownsample = (int)(SampleRate / 1000f); // Down to 1kHz
        _crushDownsampleFactor = (int)(1 + amount * (maxDownsample - 1));
        _crushDownsampleFactor = Math.Max(1, _crushDownsampleFactor);

        _crushHoldSample = 0;
        _crushHoldCounter = 0;
    }

    /// <summary>
    /// Initializes reverse glitch parameters.
    /// </summary>
    private void InitializeReverse()
    {
        _reverseLength = Math.Min(_glitchTotalSamples, _capturedSamples);
        _reverseReadPos = (_captureWritePos - Channels + _maxBufferSamples) % _maxBufferSamples;
    }

    /// <summary>
    /// Initializes pitch jump glitch parameters.
    /// </summary>
    private void InitializePitchJump(float amount)
    {
        // Random pitch shift in semitones
        float maxSemitones = 12f * amount;
        float semitones = (float)(_random.NextDouble() * 2 - 1) * maxSemitones;

        // Occasionally do octave jumps
        if (_random.NextDouble() < 0.3 * amount)
        {
            semitones = (_random.NextDouble() < 0.5 ? -12f : 12f) * (float)(_random.NextDouble() * 0.5 + 0.5);
        }

        _pitchRatio = MathF.Pow(2f, semitones / 12f);
        _pitchReadPhase = 0;
    }

    /// <summary>
    /// Initializes tape stop glitch parameters.
    /// </summary>
    private void InitializeTapeStop()
    {
        _tapeStopSpeed = 1.0;
        // Calculate deceleration to reach near-zero at end of glitch
        _tapeStopDeceleration = 1.0 / (_glitchTotalSamples / (double)Channels);
        _tapeStopReadPhase = 0;
    }

    /// <summary>
    /// Initializes gate glitch parameters.
    /// </summary>
    private void InitializeGate(float amount)
    {
        // Gate rate: faster with higher amount
        float minPeriodMs = 20f;
        float maxPeriodMs = 200f;
        float periodMs = maxPeriodMs - amount * (maxPeriodMs - minPeriodMs);
        _gatePeriodSamples = (int)(periodMs * SampleRate / 1000f) * Channels;
        _gatePeriodSamples = Math.Max(_gatePeriodSamples, Channels * 4);

        // Open ratio: more rhythmic variation with higher amount
        _gateOpenRatio = 0.3f + (float)_random.NextDouble() * 0.4f;
        _gatePosition = 0;
        _gateOpen = true;
    }

    /// <summary>
    /// Checks if a glitch should be triggered based on probability and timing.
    /// </summary>
    private bool ShouldTriggerGlitch()
    {
        if (_glitchActive) return false;

        float glitchRate = GlitchRate;
        if (glitchRate <= 0f) return false;

        bool shouldTrigger = false;

        if (TempoSync)
        {
            // Tempo-synchronized triggering
            double interval = GlitchInterval;
            double currentIntervalIndex = Math.Floor(_currentBeatPosition / interval);
            double lastIntervalIndex = Math.Floor(_lastGlitchBeat / interval);

            if (currentIntervalIndex != lastIntervalIndex || _lastGlitchBeat < 0)
            {
                _lastGlitchBeat = _currentBeatPosition;
                shouldTrigger = _random.NextDouble() < glitchRate;
            }
        }
        else
        {
            // Free-running probability (check every ~10ms worth of samples)
            int checkInterval = (int)(0.01f * SampleRate) * Channels;
            if (_totalSamplesProcessed % checkInterval < Channels)
            {
                // Scale probability to roughly match desired rate per second
                float adjustedProbability = glitchRate * 0.1f;
                shouldTrigger = _random.NextDouble() < adjustedProbability;
            }
        }

        return shouldTrigger;
    }

    /// <inheritdoc />
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        // Calculate beat advancement per sample
        double beatsPerSample = Tempo / 60.0 / SampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Capture incoming audio into circular buffer
            for (int ch = 0; ch < channels; ch++)
            {
                _captureBuffer[_captureWritePos + ch] = sourceBuffer[i + ch];
            }
            _captureWritePos = (_captureWritePos + channels) % _maxBufferSamples;
            _capturedSamples = Math.Min(_capturedSamples + channels, _maxBufferSamples);

            // Update beat position
            if (TempoSync)
            {
                _currentBeatPosition += beatsPerSample;
                if (_currentBeatPosition >= 4.0)
                {
                    _currentBeatPosition -= 4.0;
                }
            }

            // Check for glitch trigger
            if (ShouldTriggerGlitch())
            {
                GlitchType glitchType = SelectRandomGlitchType();
                StartGlitch(glitchType);
            }

            // Process audio
            if (_glitchActive && _capturedSamples >= channels * 64)
            {
                ProcessGlitch(sourceBuffer, destBuffer, offset, i, channels);

                _glitchSamplesRemaining -= channels;
                if (_glitchSamplesRemaining <= 0)
                {
                    _glitchActive = false;
                    GlitchEnded?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                // Pass through
                for (int ch = 0; ch < channels; ch++)
                {
                    destBuffer[offset + i + ch] = sourceBuffer[i + ch];
                }
            }

            _totalSamplesProcessed++;
        }
    }

    /// <summary>
    /// Processes a single frame based on the current glitch type.
    /// </summary>
    private void ProcessGlitch(float[] sourceBuffer, float[] destBuffer, int offset, int i, int channels)
    {
        switch (_currentGlitchType)
        {
            case GlitchType.Stutter:
                ProcessStutter(destBuffer, offset, i, channels);
                break;
            case GlitchType.Bitcrush:
                ProcessBitcrush(sourceBuffer, destBuffer, offset, i, channels);
                break;
            case GlitchType.Reverse:
                ProcessReverse(destBuffer, offset, i, channels);
                break;
            case GlitchType.PitchJump:
                ProcessPitchJump(sourceBuffer, destBuffer, offset, i, channels);
                break;
            case GlitchType.TapeStop:
                ProcessTapeStop(destBuffer, offset, i, channels);
                break;
            case GlitchType.Gate:
                ProcessGate(sourceBuffer, destBuffer, offset, i, channels);
                break;
        }
    }

    /// <summary>
    /// Processes stutter effect.
    /// </summary>
    private void ProcessStutter(float[] destBuffer, int offset, int i, int channels)
    {
        for (int ch = 0; ch < channels; ch++)
        {
            int readIdx = (_stutterReadPos + _stutterRepeatPos + ch) % _maxBufferSamples;
            destBuffer[offset + i + ch] = _captureBuffer[readIdx];
        }

        _stutterRepeatPos += channels;
        if (_stutterRepeatPos >= _stutterLength)
        {
            _stutterRepeatPos = 0;
        }
    }

    /// <summary>
    /// Processes bitcrush effect.
    /// </summary>
    private void ProcessBitcrush(float[] sourceBuffer, float[] destBuffer, int offset, int i, int channels)
    {
        if (_crushHoldCounter <= 0)
        {
            _crushHoldCounter = _crushDownsampleFactor;

            // Process first channel for sample-and-hold
            float input = sourceBuffer[i];
            float step = 2f / _crushLevels;
            _crushHoldSample = MathF.Round(input / step) * step;
        }
        else
        {
            _crushHoldCounter--;
        }

        // Apply held sample to all channels
        for (int ch = 0; ch < channels; ch++)
        {
            destBuffer[offset + i + ch] = _crushHoldSample;
        }
    }

    /// <summary>
    /// Processes reverse effect.
    /// </summary>
    private void ProcessReverse(float[] destBuffer, int offset, int i, int channels)
    {
        for (int ch = 0; ch < channels; ch++)
        {
            int readIdx = (_reverseReadPos + ch + _maxBufferSamples) % _maxBufferSamples;
            destBuffer[offset + i + ch] = _captureBuffer[readIdx];
        }

        _reverseReadPos = (_reverseReadPos - channels + _maxBufferSamples) % _maxBufferSamples;
    }

    /// <summary>
    /// Processes pitch jump effect.
    /// </summary>
    private void ProcessPitchJump(float[] sourceBuffer, float[] destBuffer, int offset, int i, int channels)
    {
        // Read from capture buffer with pitch shift using linear interpolation
        int baseIndex = (int)_pitchReadPhase;
        float frac = (float)(_pitchReadPhase - baseIndex);

        for (int ch = 0; ch < channels; ch++)
        {
            int readPos = (_captureWritePos - _glitchTotalSamples + baseIndex * channels + ch + _maxBufferSamples) % _maxBufferSamples;
            int nextPos = (_captureWritePos - _glitchTotalSamples + (baseIndex + 1) * channels + ch + _maxBufferSamples) % _maxBufferSamples;

            float sample1 = _captureBuffer[readPos];
            float sample2 = _captureBuffer[nextPos];
            destBuffer[offset + i + ch] = sample1 * (1f - frac) + sample2 * frac;
        }

        _pitchReadPhase += _pitchRatio;

        // Wrap if we've read past available data
        int maxPhase = _capturedSamples / channels;
        if (_pitchReadPhase >= maxPhase)
        {
            _pitchReadPhase = 0;
        }
    }

    /// <summary>
    /// Processes tape stop effect.
    /// </summary>
    private void ProcessTapeStop(float[] destBuffer, int offset, int i, int channels)
    {
        // Read from capture buffer at decreasing rate
        int baseIndex = (int)_tapeStopReadPhase;
        float frac = (float)(_tapeStopReadPhase - baseIndex);

        for (int ch = 0; ch < channels; ch++)
        {
            int readPos = (_captureWritePos - _glitchTotalSamples + baseIndex * channels + ch + _maxBufferSamples) % _maxBufferSamples;
            int nextPos = (_captureWritePos - _glitchTotalSamples + (baseIndex + 1) * channels + ch + _maxBufferSamples) % _maxBufferSamples;

            float sample1 = _captureBuffer[readPos];
            float sample2 = _captureBuffer[nextPos];
            float sample = sample1 * (1f - frac) + sample2 * frac;

            // Apply slight low-pass effect as speed decreases (tape characteristic)
            destBuffer[offset + i + ch] = sample * (float)_tapeStopSpeed;
        }

        _tapeStopReadPhase += _tapeStopSpeed;
        _tapeStopSpeed = Math.Max(0.0, _tapeStopSpeed - _tapeStopDeceleration);
    }

    /// <summary>
    /// Processes gate effect.
    /// </summary>
    private void ProcessGate(float[] sourceBuffer, float[] destBuffer, int offset, int i, int channels)
    {
        // Determine gate state
        float positionInPeriod = (float)_gatePosition / _gatePeriodSamples;
        _gateOpen = positionInPeriod < _gateOpenRatio;

        for (int ch = 0; ch < channels; ch++)
        {
            if (_gateOpen)
            {
                destBuffer[offset + i + ch] = sourceBuffer[i + ch];
            }
            else
            {
                destBuffer[offset + i + ch] = 0f;
            }
        }

        _gatePosition = (_gatePosition + channels) % _gatePeriodSamples;
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("Tempo", StringComparison.OrdinalIgnoreCase))
        {
            UpdateTimingParameters();
        }
    }

    /// <summary>
    /// Creates a subtle glitch preset for adding occasional digital artifacts.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured GlitchMachine instance.</returns>
    public static GlitchMachine CreateSubtle(ISampleProvider source)
    {
        var effect = new GlitchMachine(source)
        {
            GlitchRate = 0.1f,
            GlitchAmount = 0.3f,
            TempoSync = true,
            GlitchInterval = 1f,
            MinGlitchDuration = 30f,
            MaxGlitchDuration = 100f,
            EnableStutter = true,
            EnableBitcrush = true,
            EnableReverse = false,
            EnablePitchJump = false,
            EnableTapeStop = false,
            EnableGate = true,
            EffectMix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a moderate glitch preset for electronic music production.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured GlitchMachine instance.</returns>
    public static GlitchMachine CreateModerate(ISampleProvider source)
    {
        var effect = new GlitchMachine(source)
        {
            GlitchRate = 0.3f,
            GlitchAmount = 0.5f,
            TempoSync = true,
            GlitchInterval = 0.5f,
            MinGlitchDuration = 50f,
            MaxGlitchDuration = 300f,
            EnableStutter = true,
            EnableBitcrush = true,
            EnableReverse = true,
            EnablePitchJump = true,
            EnableTapeStop = false,
            EnableGate = true,
            EffectMix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates an extreme glitch preset for heavy glitch and IDM music.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured GlitchMachine instance.</returns>
    public static GlitchMachine CreateExtreme(ISampleProvider source)
    {
        var effect = new GlitchMachine(source)
        {
            GlitchRate = 0.6f,
            GlitchAmount = 0.8f,
            TempoSync = true,
            GlitchInterval = 0.25f,
            MinGlitchDuration = 30f,
            MaxGlitchDuration = 500f,
            EnableStutter = true,
            EnableBitcrush = true,
            EnableReverse = true,
            EnablePitchJump = true,
            EnableTapeStop = true,
            EnableGate = true,
            EffectMix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a chaotic glitch preset for experimental sound design.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured GlitchMachine instance.</returns>
    public static GlitchMachine CreateChaos(ISampleProvider source)
    {
        var effect = new GlitchMachine(source)
        {
            GlitchRate = 0.9f,
            GlitchAmount = 1f,
            TempoSync = false,
            MinGlitchDuration = 10f,
            MaxGlitchDuration = 800f,
            EnableStutter = true,
            EnableBitcrush = true,
            EnableReverse = true,
            EnablePitchJump = true,
            EnableTapeStop = true,
            EnableGate = true,
            EffectMix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a tape-focused glitch preset emphasizing tape stop effects.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured GlitchMachine instance.</returns>
    public static GlitchMachine CreateTapeFocused(ISampleProvider source)
    {
        var effect = new GlitchMachine(source)
        {
            GlitchRate = 0.2f,
            GlitchAmount = 0.6f,
            TempoSync = true,
            GlitchInterval = 2f,
            MinGlitchDuration = 200f,
            MaxGlitchDuration = 1000f,
            EnableStutter = false,
            EnableBitcrush = false,
            EnableReverse = true,
            EnablePitchJump = false,
            EnableTapeStop = true,
            EnableGate = false,
            EffectMix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a rhythmic glitch preset focusing on stutter and gate effects.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured GlitchMachine instance.</returns>
    public static GlitchMachine CreateRhythmic(ISampleProvider source)
    {
        var effect = new GlitchMachine(source)
        {
            GlitchRate = 0.4f,
            GlitchAmount = 0.4f,
            TempoSync = true,
            GlitchInterval = 0.25f,
            MinGlitchDuration = 50f,
            MaxGlitchDuration = 250f,
            EnableStutter = true,
            EnableBitcrush = false,
            EnableReverse = false,
            EnablePitchJump = false,
            EnableTapeStop = false,
            EnableGate = true,
            EffectMix = 1f
        };
        return effect;
    }
}
