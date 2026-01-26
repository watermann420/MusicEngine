// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Noise type enumeration for different noise color spectrums.
/// </summary>
public enum NoiseType
{
    /// <summary>White noise - equal energy at all frequencies (flat spectrum).</summary>
    White,
    /// <summary>Pink noise - energy decreases 3dB per octave (1/f spectrum).</summary>
    Pink,
    /// <summary>Brown/Brownian/Red noise - energy decreases 6dB per octave (1/f^2 spectrum).</summary>
    Brown,
    /// <summary>Blue noise - energy increases 3dB per octave.</summary>
    Blue,
    /// <summary>Violet/Purple noise - energy increases 6dB per octave.</summary>
    Violet
}

/// <summary>
/// Filter type enumeration for noise shaping.
/// </summary>
public enum NoiseFilterType
{
    /// <summary>No filter applied.</summary>
    None,
    /// <summary>Low-pass filter - allows frequencies below cutoff.</summary>
    LowPass,
    /// <summary>High-pass filter - allows frequencies above cutoff.</summary>
    HighPass,
    /// <summary>Band-pass filter - allows frequencies around cutoff.</summary>
    BandPass
}

/// <summary>
/// A noise generator synthesizer implementation with multiple noise types,
/// optional filtering, and stereo width control.
/// </summary>
public class NoiseGenerator : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();
    private readonly Random _random = new();

    // Pink noise state (Voss-McCartney algorithm with 16 octaves)
    private readonly float[] _pinkRows = new float[16];
    private int _pinkIndex;
    private float _pinkRunningSum;

    // Brown noise state (integrated white noise)
    private float _brownState;
    private const float BrownLeakFactor = 0.998f; // Prevents DC drift
    private const float BrownIntegrationFactor = 0.02f;

    // Violet/Blue noise state (differentiated white noise)
    private float _lastWhiteSample;

    // Filter state (biquad coefficients and delay elements)
    private float _filterZ1Left, _filterZ2Left;
    private float _filterZ1Right, _filterZ2Right;
    private float _filterB0, _filterB1, _filterB2;
    private float _filterA1, _filterA2;
    private bool _filterCoefficientsValid;
    private float _lastFilterFrequency = -1;
    private float _lastFilterResonance = -1;
    private NoiseFilterType _lastFilterType = NoiseFilterType.None;

    // Playback state
    private bool _isPlaying;
    private float _currentGain;
    private float _targetGain;
    private bool _isContinuous;
    private int _activeNoteCount;

    // Stereo width state
    private float _leftChannel;
    private float _rightChannel;

    /// <summary>Gets or sets the noise type.</summary>
    public NoiseType NoiseType { get; set; } = NoiseType.White;

    /// <summary>Gets or sets the master volume (0.0 to 1.0).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the filter type.</summary>
    public NoiseFilterType FilterType { get; set; } = NoiseFilterType.None;

    /// <summary>
    /// Gets or sets the filter cutoff frequency in Hz (20 to 20000).
    /// </summary>
    public float FilterFrequency { get; set; } = 1000f;

    /// <summary>
    /// Gets or sets the filter resonance/Q (0.0 to 1.0).
    /// 0.0 = no resonance, 1.0 = maximum resonance.
    /// </summary>
    public float FilterResonance { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the stereo width (0.0 to 1.0).
    /// 0.0 = mono, 1.0 = full stereo (independent L/R noise).
    /// </summary>
    public float StereoWidth { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets whether the noise runs continuously without requiring note triggers.
    /// When true, noise plays automatically. When false, requires NoteOn/NoteOff.
    /// </summary>
    public bool Continuous
    {
        get => _isContinuous;
        set
        {
            lock (_lock)
            {
                _isContinuous = value;
                if (value && !_isPlaying)
                {
                    _isPlaying = true;
                    _targetGain = 1.0f;
                }
            }
        }
    }

    /// <summary>Gets or sets the synth name for identification.</summary>
    public string Name { get; set; } = "NoiseGenerator";

    /// <summary>Gets the wave format for audio output.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Creates a new noise generator with the specified sample rate.
    /// </summary>
    /// <param name="sampleRate">Optional sample rate (defaults to Settings.SampleRate).</param>
    public NoiseGenerator(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize pink noise rows with random values
        for (int i = 0; i < _pinkRows.Length; i++)
        {
            _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            _pinkRunningSum += _pinkRows[i];
        }
    }

    /// <summary>
    /// Triggers the noise generator with the specified velocity.
    /// The note parameter is ignored as noise is not pitched.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        lock (_lock)
        {
            _activeNoteCount++;
            _isPlaying = true;
            _targetGain = velocity / 127f;
        }
    }

    /// <summary>
    /// Releases the noise generator.
    /// The note parameter is ignored as noise is not pitched.
    /// </summary>
    public void NoteOff(int note)
    {
        MidiValidation.ValidateNote(note);

        lock (_lock)
        {
            _activeNoteCount = Math.Max(0, _activeNoteCount - 1);

            if (_activeNoteCount == 0 && !_isContinuous)
            {
                _targetGain = 0.0f;
            }
        }
    }

    /// <summary>
    /// Stops all noise output immediately (unless in continuous mode).
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            _activeNoteCount = 0;

            if (!_isContinuous)
            {
                _targetGain = 0.0f;
            }
        }
    }

    /// <summary>
    /// Sets a synthesizer parameter by name.
    /// </summary>
    /// <param name="name">Parameter name (case-insensitive).</param>
    /// <param name="value">Parameter value.</param>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "noisetype":
            case "type":
                NoiseType = (NoiseType)(int)Math.Clamp(value, 0, 4);
                break;
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "filtertype":
            case "filter":
                FilterType = (NoiseFilterType)(int)Math.Clamp(value, 0, 3);
                InvalidateFilterCoefficients();
                break;
            case "filterfrequency":
            case "cutoff":
            case "frequency":
                FilterFrequency = Math.Clamp(value, 20f, 20000f);
                InvalidateFilterCoefficients();
                break;
            case "filterresonance":
            case "resonance":
            case "q":
                FilterResonance = Math.Clamp(value, 0f, 1f);
                InvalidateFilterCoefficients();
                break;
            case "stereowidth":
            case "width":
            case "stereo":
                StereoWidth = Math.Clamp(value, 0f, 1f);
                break;
            case "continuous":
                Continuous = value >= 0.5f;
                break;
        }
    }

    /// <summary>
    /// Reads audio samples into the buffer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int channels = _waveFormat.Channels;
        int sampleRate = _waveFormat.SampleRate;

        // Clear buffer
        for (int n = 0; n < count; n++)
        {
            buffer[offset + n] = 0;
        }

        lock (_lock)
        {
            // Update filter coefficients if needed
            UpdateFilterCoefficientsIfNeeded(sampleRate);

            for (int n = 0; n < count; n += channels)
            {
                // Smooth gain transitions
                float gainDelta = _targetGain - _currentGain;
                _currentGain += gainDelta * 0.001f; // Smooth transition

                // Check if we should stop
                if (!_isPlaying && _currentGain < 0.0001f)
                {
                    continue;
                }

                // Generate noise sample(s)
                float monoSample = GenerateNoiseSample();

                // Apply stereo width
                float stereoNoise = 0;
                if (StereoWidth > 0 && channels >= 2)
                {
                    stereoNoise = GenerateNoiseSample();
                }

                // Calculate left and right channels with stereo width
                _leftChannel = monoSample;
                _rightChannel = channels >= 2
                    ? monoSample * (1 - StereoWidth) + stereoNoise * StereoWidth
                    : monoSample;

                // Apply filter
                if (FilterType != NoiseFilterType.None)
                {
                    _leftChannel = ApplyFilter(_leftChannel, ref _filterZ1Left, ref _filterZ2Left);
                    if (channels >= 2)
                    {
                        _rightChannel = ApplyFilter(_rightChannel, ref _filterZ1Right, ref _filterZ2Right);
                    }
                }

                // Apply volume and gain
                float finalGain = Volume * _currentGain;

                // Write to buffer
                if (offset + n < buffer.Length)
                {
                    buffer[offset + n] = _leftChannel * finalGain;
                }

                if (channels >= 2 && offset + n + 1 < buffer.Length)
                {
                    buffer[offset + n + 1] = _rightChannel * finalGain;
                }
            }

            // Update playing state
            if (_currentGain < 0.0001f && _targetGain < 0.0001f && !_isContinuous)
            {
                _isPlaying = false;
            }
        }

        return count;
    }

    /// <summary>
    /// Generates a single noise sample based on the current noise type.
    /// </summary>
    private float GenerateNoiseSample()
    {
        return NoiseType switch
        {
            NoiseType.White => GenerateWhiteNoise(),
            NoiseType.Pink => GeneratePinkNoise(),
            NoiseType.Brown => GenerateBrownNoise(),
            NoiseType.Blue => GenerateBlueNoise(),
            NoiseType.Violet => GenerateVioletNoise(),
            _ => GenerateWhiteNoise()
        };
    }

    /// <summary>
    /// Generates white noise with uniform spectral density.
    /// </summary>
    private float GenerateWhiteNoise()
    {
        return (float)(_random.NextDouble() * 2.0 - 1.0);
    }

    /// <summary>
    /// Generates pink noise using the Voss-McCartney algorithm.
    /// Pink noise has equal energy per octave (1/f spectrum).
    /// </summary>
    private float GeneratePinkNoise()
    {
        // Voss-McCartney algorithm with 16 octave bands
        int lastIndex = _pinkIndex;
        _pinkIndex++;

        // XOR to find which octave bands to update
        int diff = lastIndex ^ _pinkIndex;

        for (int i = 0; i < _pinkRows.Length; i++)
        {
            if ((diff & (1 << i)) != 0)
            {
                // Remove old value from running sum
                _pinkRunningSum -= _pinkRows[i];
                // Generate new random value
                _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
                // Add new value to running sum
                _pinkRunningSum += _pinkRows[i];
                break; // Only update one row per sample
            }
        }

        // Add white noise for high frequencies and normalize
        float white = (float)(_random.NextDouble() * 2.0 - 1.0);
        float pink = (_pinkRunningSum + white) / (_pinkRows.Length + 1);

        return pink * 0.5f; // Scale to reasonable level
    }

    /// <summary>
    /// Generates brown (Brownian/red) noise by integrating white noise.
    /// Brown noise has energy that decreases 6dB per octave (1/f^2 spectrum).
    /// </summary>
    private float GenerateBrownNoise()
    {
        float white = GenerateWhiteNoise();

        // Integrate white noise with leak factor to prevent DC drift
        _brownState = _brownState * BrownLeakFactor + white * BrownIntegrationFactor;

        // Clamp to prevent runaway
        _brownState = Math.Clamp(_brownState, -1f, 1f);

        return _brownState;
    }

    /// <summary>
    /// Generates blue noise by differentiating white noise.
    /// Blue noise has energy that increases 3dB per octave.
    /// </summary>
    private float GenerateBlueNoise()
    {
        float white = GenerateWhiteNoise();

        // Simple first-order difference (differentiation)
        float blue = (white - _lastWhiteSample) * 0.5f;
        _lastWhiteSample = white;

        return blue;
    }

    /// <summary>
    /// Generates violet (purple) noise by double-differentiating white noise.
    /// Violet noise has energy that increases 6dB per octave.
    /// </summary>
    private float GenerateVioletNoise()
    {
        // Generate blue noise first, then differentiate again
        float blue = GenerateBlueNoise();

        // Apply another high-pass differentiation
        // We use a simple approximation here
        float violet = blue * 2.0f; // Amplify high frequencies

        return Math.Clamp(violet, -1f, 1f);
    }

    /// <summary>
    /// Invalidates filter coefficients to force recalculation.
    /// </summary>
    private void InvalidateFilterCoefficients()
    {
        _filterCoefficientsValid = false;
    }

    /// <summary>
    /// Updates filter coefficients if parameters have changed.
    /// </summary>
    private void UpdateFilterCoefficientsIfNeeded(int sampleRate)
    {
        bool needsUpdate = !_filterCoefficientsValid ||
                          Math.Abs(FilterFrequency - _lastFilterFrequency) > 0.01f ||
                          Math.Abs(FilterResonance - _lastFilterResonance) > 0.001f ||
                          FilterType != _lastFilterType;

        if (!needsUpdate) return;

        CalculateFilterCoefficients(sampleRate);

        _lastFilterFrequency = FilterFrequency;
        _lastFilterResonance = FilterResonance;
        _lastFilterType = FilterType;
        _filterCoefficientsValid = true;
    }

    /// <summary>
    /// Calculates biquad filter coefficients based on current settings.
    /// </summary>
    private void CalculateFilterCoefficients(int sampleRate)
    {
        if (FilterType == NoiseFilterType.None)
        {
            _filterB0 = 1; _filterB1 = 0; _filterB2 = 0;
            _filterA1 = 0; _filterA2 = 0;
            return;
        }

        // Clamp frequency to valid range
        float freq = Math.Clamp(FilterFrequency, 20f, sampleRate * 0.45f);

        // Calculate Q from resonance (0.5 to 10)
        float q = 0.5f + FilterResonance * 9.5f;

        // Angular frequency
        float omega = 2.0f * MathF.PI * freq / sampleRate;
        float sinOmega = MathF.Sin(omega);
        float cosOmega = MathF.Cos(omega);
        float alpha = sinOmega / (2.0f * q);

        float a0;

        switch (FilterType)
        {
            case NoiseFilterType.LowPass:
                _filterB0 = (1 - cosOmega) / 2;
                _filterB1 = 1 - cosOmega;
                _filterB2 = (1 - cosOmega) / 2;
                a0 = 1 + alpha;
                _filterA1 = -2 * cosOmega;
                _filterA2 = 1 - alpha;
                break;

            case NoiseFilterType.HighPass:
                _filterB0 = (1 + cosOmega) / 2;
                _filterB1 = -(1 + cosOmega);
                _filterB2 = (1 + cosOmega) / 2;
                a0 = 1 + alpha;
                _filterA1 = -2 * cosOmega;
                _filterA2 = 1 - alpha;
                break;

            case NoiseFilterType.BandPass:
                _filterB0 = alpha;
                _filterB1 = 0;
                _filterB2 = -alpha;
                a0 = 1 + alpha;
                _filterA1 = -2 * cosOmega;
                _filterA2 = 1 - alpha;
                break;

            default:
                _filterB0 = 1; _filterB1 = 0; _filterB2 = 0;
                _filterA1 = 0; _filterA2 = 0;
                return;
        }

        // Normalize coefficients
        _filterB0 /= a0;
        _filterB1 /= a0;
        _filterB2 /= a0;
        _filterA1 /= a0;
        _filterA2 /= a0;
    }

    /// <summary>
    /// Applies the biquad filter to a sample using Direct Form II Transposed.
    /// </summary>
    private float ApplyFilter(float input, ref float z1, ref float z2)
    {
        float output = _filterB0 * input + z1;
        z1 = _filterB1 * input - _filterA1 * output + z2;
        z2 = _filterB2 * input - _filterA2 * output;
        return output;
    }

    /// <summary>
    /// Resets the noise generator state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            // Reset pink noise state
            _pinkIndex = 0;
            _pinkRunningSum = 0;
            for (int i = 0; i < _pinkRows.Length; i++)
            {
                _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
                _pinkRunningSum += _pinkRows[i];
            }

            // Reset brown noise state
            _brownState = 0;

            // Reset violet/blue noise state
            _lastWhiteSample = 0;

            // Reset filter state
            _filterZ1Left = 0;
            _filterZ2Left = 0;
            _filterZ1Right = 0;
            _filterZ2Right = 0;

            // Reset playback state
            _currentGain = 0;
            _activeNoteCount = 0;

            if (!_isContinuous)
            {
                _isPlaying = false;
                _targetGain = 0;
            }
        }
    }
}
