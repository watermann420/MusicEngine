//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Real-time pitch correction effect with scale-aware correction,
// formant preservation, humanization, and chromatic/scale-constrained modes.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Musical key options for pitch correction targeting.
/// </summary>
public enum PitchCorrectorKey
{
    C = 0,
    CSharp = 1,
    D = 2,
    DSharp = 3,
    E = 4,
    F = 5,
    FSharp = 6,
    G = 7,
    GSharp = 8,
    A = 9,
    ASharp = 10,
    B = 11
}

/// <summary>
/// Scale types for pitch correction targeting.
/// </summary>
public enum PitchCorrectorScale
{
    /// <summary>All 12 semitones are valid targets (chromatic mode)</summary>
    Chromatic,
    /// <summary>Major scale (W-W-H-W-W-W-H)</summary>
    Major,
    /// <summary>Natural minor scale (W-H-W-W-H-W-W)</summary>
    NaturalMinor,
    /// <summary>Harmonic minor scale</summary>
    HarmonicMinor,
    /// <summary>Melodic minor scale (ascending)</summary>
    MelodicMinor,
    /// <summary>Dorian mode</summary>
    Dorian,
    /// <summary>Phrygian mode</summary>
    Phrygian,
    /// <summary>Lydian mode</summary>
    Lydian,
    /// <summary>Mixolydian mode</summary>
    Mixolydian,
    /// <summary>Locrian mode</summary>
    Locrian,
    /// <summary>Pentatonic major scale</summary>
    PentatonicMajor,
    /// <summary>Pentatonic minor scale</summary>
    PentatonicMinor,
    /// <summary>Blues scale</summary>
    Blues,
    /// <summary>Whole tone scale</summary>
    WholeTone,
    /// <summary>Diminished (half-whole) scale</summary>
    Diminished
}

/// <summary>
/// Real-time pitch correction effect (auto-tune style).
/// Uses autocorrelation-based pitch detection and phase vocoder
/// for pitch shifting with optional formant preservation.
/// </summary>
public class PitchCorrector : EffectBase
{
    // Scale interval patterns (semitones from root)
    private static readonly int[] ChromaticIntervals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    private static readonly int[] MajorIntervals = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] NaturalMinorIntervals = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] HarmonicMinorIntervals = [0, 2, 3, 5, 7, 8, 11];
    private static readonly int[] MelodicMinorIntervals = [0, 2, 3, 5, 7, 9, 11];
    private static readonly int[] DorianIntervals = [0, 2, 3, 5, 7, 9, 10];
    private static readonly int[] PhrygianIntervals = [0, 1, 3, 5, 7, 8, 10];
    private static readonly int[] LydianIntervals = [0, 2, 4, 6, 7, 9, 11];
    private static readonly int[] MixolydianIntervals = [0, 2, 4, 5, 7, 9, 10];
    private static readonly int[] LocrianIntervals = [0, 1, 3, 5, 6, 8, 10];
    private static readonly int[] PentatonicMajorIntervals = [0, 2, 4, 7, 9];
    private static readonly int[] PentatonicMinorIntervals = [0, 3, 5, 7, 10];
    private static readonly int[] BluesIntervals = [0, 3, 5, 6, 7, 10];
    private static readonly int[] WholeToneIntervals = [0, 2, 4, 6, 8, 10];
    private static readonly int[] DiminishedIntervals = [0, 1, 3, 4, 6, 7, 9, 10];

    // Pitch detection buffers (autocorrelation method)
    private readonly int _analysisBufferSize;
    private readonly float[] _analysisBuffer;
    private readonly float[] _autocorrelation;
    private int _analysisWritePos;

    // Phase vocoder for pitch shifting
    private readonly int _fftSize;
    private readonly int _hopSize;
    private readonly int _overlapCount;
    private readonly float[] _inputBuffer;
    private readonly float[] _outputBuffer;
    private readonly float[] _windowFunction;
    private readonly float[] _lastPhase;
    private readonly float[] _sumPhase;
    private readonly float[] _analysisFreqs;
    private readonly float[] _analysisMags;
    private readonly float[] _synthFreqs;
    private readonly float[] _synthMags;
    private int _inputWritePos;
    private int _outputReadPos;
    private long _frameCounter;

    // Formant preservation
    private readonly float[] _formantEnvelope;
    private readonly float[] _shiftedEnvelope;
    private readonly int _formantOrder;

    // Current pitch state
    private float _currentInputPitch;
    private float _currentOutputPitch;
    private float _targetPitch;
    private float _pitchCorrectionCents;
    private float _smoothedCorrection;

    // Parameters
    private PitchCorrectorKey _key = PitchCorrectorKey.C;
    private PitchCorrectorScale _scale = PitchCorrectorScale.Chromatic;
    private float _correctionSpeed = 0.5f; // 0-1 (0 = instant, 1 = slow/natural)
    private float _humanizeAmount = 0f; // 0-1 (preserves natural variation)
    private bool _formantPreserve = true;
    private float _mix = 1f;

    // Valid scale notes cache
    private readonly bool[] _validNotes = new bool[12];
    private float _smoothingCoeff;

    /// <summary>
    /// Gets or sets the musical key for pitch correction.
    /// </summary>
    public PitchCorrectorKey Key
    {
        get => _key;
        set
        {
            _key = value;
            UpdateValidNotes();
        }
    }

    /// <summary>
    /// Gets or sets the scale type for pitch correction.
    /// </summary>
    public PitchCorrectorScale Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            UpdateValidNotes();
        }
    }

    /// <summary>
    /// Gets or sets the correction speed (0-1).
    /// 0 = instant correction (robotic/hard tune effect)
    /// 1 = slow correction (natural, subtle)
    /// </summary>
    public float CorrectionSpeed
    {
        get => _correctionSpeed;
        set
        {
            _correctionSpeed = Math.Clamp(value, 0f, 1f);
            UpdateSmoothingCoefficient();
        }
    }

    /// <summary>
    /// Gets or sets the humanize amount (0-1).
    /// Higher values preserve more natural pitch variation
    /// and reduce the intensity of correction.
    /// </summary>
    public float HumanizeAmount
    {
        get => _humanizeAmount;
        set => _humanizeAmount = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets whether formant preservation is enabled.
    /// When true, preserves the natural vocal character during pitch shifting.
    /// </summary>
    public bool FormantPreserve
    {
        get => _formantPreserve;
        set => _formantPreserve = value;
    }

    /// <summary>
    /// Gets the currently detected input pitch in Hz.
    /// Returns 0 if no pitch is detected.
    /// </summary>
    public float DetectedPitch => _currentInputPitch;

    /// <summary>
    /// Gets the current output pitch in Hz after correction.
    /// </summary>
    public float OutputPitch => _currentOutputPitch;

    /// <summary>
    /// Gets the target pitch in Hz that the corrector is aiming for.
    /// </summary>
    public float TargetPitch => _targetPitch;

    /// <summary>
    /// Gets the current pitch correction amount in cents.
    /// </summary>
    public float CorrectionCents => _pitchCorrectionCents;

    /// <summary>
    /// Creates a new PitchCorrector effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public PitchCorrector(ISampleProvider source) : base(source, "PitchCorrector")
    {
        // Initialize pitch detection (autocorrelation)
        _analysisBufferSize = 2048;
        _analysisBuffer = new float[_analysisBufferSize];
        _autocorrelation = new float[_analysisBufferSize];
        _analysisWritePos = 0;

        // Initialize phase vocoder
        _fftSize = 4096;
        _hopSize = _fftSize / 8; // 87.5% overlap for quality
        _overlapCount = _fftSize / _hopSize;

        _inputBuffer = new float[_fftSize * 2];
        _outputBuffer = new float[_fftSize * 2];
        _windowFunction = CreateHannWindow(_fftSize);
        _lastPhase = new float[_fftSize / 2 + 1];
        _sumPhase = new float[_fftSize / 2 + 1];
        _analysisFreqs = new float[_fftSize / 2 + 1];
        _analysisMags = new float[_fftSize / 2 + 1];
        _synthFreqs = new float[_fftSize / 2 + 1];
        _synthMags = new float[_fftSize / 2 + 1];

        _inputWritePos = 0;
        _outputReadPos = 0;
        _frameCounter = 0;

        // Initialize formant preservation
        _formantOrder = 30; // LPC order for formant estimation
        _formantEnvelope = new float[_fftSize / 2 + 1];
        _shiftedEnvelope = new float[_fftSize / 2 + 1];

        // Initialize scale
        UpdateValidNotes();
        UpdateSmoothingCoefficient();

        // Register parameters
        RegisterParameter("key", 0f);
        RegisterParameter("scale", 0f);
        RegisterParameter("correctionspeed", 0.5f);
        RegisterParameter("humanize", 0f);
        RegisterParameter("formantpreserve", 1f);
        RegisterParameter("mix", 1f);
    }

    /// <summary>
    /// Creates a Hann (Hanning) window for smooth windowing.
    /// </summary>
    private static float[] CreateHannWindow(int size)
    {
        var window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (size - 1)));
        }
        return window;
    }

    /// <summary>
    /// Updates the valid notes array based on current key and scale.
    /// </summary>
    private void UpdateValidNotes()
    {
        Array.Clear(_validNotes, 0, 12);

        int[] intervals = _scale switch
        {
            PitchCorrectorScale.Chromatic => ChromaticIntervals,
            PitchCorrectorScale.Major => MajorIntervals,
            PitchCorrectorScale.NaturalMinor => NaturalMinorIntervals,
            PitchCorrectorScale.HarmonicMinor => HarmonicMinorIntervals,
            PitchCorrectorScale.MelodicMinor => MelodicMinorIntervals,
            PitchCorrectorScale.Dorian => DorianIntervals,
            PitchCorrectorScale.Phrygian => PhrygianIntervals,
            PitchCorrectorScale.Lydian => LydianIntervals,
            PitchCorrectorScale.Mixolydian => MixolydianIntervals,
            PitchCorrectorScale.Locrian => LocrianIntervals,
            PitchCorrectorScale.PentatonicMajor => PentatonicMajorIntervals,
            PitchCorrectorScale.PentatonicMinor => PentatonicMinorIntervals,
            PitchCorrectorScale.Blues => BluesIntervals,
            PitchCorrectorScale.WholeTone => WholeToneIntervals,
            PitchCorrectorScale.Diminished => DiminishedIntervals,
            _ => ChromaticIntervals
        };

        int root = (int)_key;
        foreach (int interval in intervals)
        {
            int note = (root + interval) % 12;
            _validNotes[note] = true;
        }
    }

    /// <summary>
    /// Updates the smoothing coefficient based on correction speed.
    /// </summary>
    private void UpdateSmoothingCoefficient()
    {
        // Map correction speed (0-1) to time constant
        // 0 = instant (coeff = 1), 1 = very slow (coeff near 0)
        if (_correctionSpeed <= 0.001f)
        {
            _smoothingCoeff = 1f;
        }
        else
        {
            // Time constant from 1ms (instant) to 300ms (slow)
            float timeConstantMs = 1f + _correctionSpeed * 299f;
            float samplesPerTimeConstant = (timeConstantMs / 1000f) * SampleRate;
            _smoothingCoeff = 1f - MathF.Exp(-1f / (samplesPerTimeConstant / _hopSize));
        }
    }

    /// <inheritdoc />
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        for (int i = 0; i < count; i += channels)
        {
            // Convert to mono for analysis
            float monoSample = sourceBuffer[i];
            if (channels > 1)
            {
                monoSample = (sourceBuffer[i] + sourceBuffer[i + 1]) * 0.5f;
            }

            // Add to analysis buffer for pitch detection
            _analysisBuffer[_analysisWritePos] = monoSample;
            _analysisWritePos = (_analysisWritePos + 1) % _analysisBufferSize;

            // Add to input buffer for phase vocoder
            _inputBuffer[_inputWritePos] = monoSample;
            _inputWritePos = (_inputWritePos + 1) % _inputBuffer.Length;

            // Read from output buffer
            float processedSample = _outputBuffer[_outputReadPos];
            _outputBuffer[_outputReadPos] = 0f;
            _outputReadPos = (_outputReadPos + 1) % _outputBuffer.Length;

            // Process frame when we have accumulated enough samples
            if (_inputWritePos % _hopSize == 0)
            {
                ProcessFrame();
            }

            // Write output (both channels for stereo)
            destBuffer[offset + i] = processedSample;
            if (channels > 1)
            {
                destBuffer[offset + i + 1] = processedSample;
            }
        }
    }

    /// <summary>
    /// Processes one frame of audio for pitch correction.
    /// </summary>
    private void ProcessFrame()
    {
        _frameCounter++;

        // Detect pitch using autocorrelation
        float detectedPitch = DetectPitchAutocorrelation();
        _currentInputPitch = detectedPitch;

        if (detectedPitch > 0)
        {
            // Find target pitch based on scale
            _targetPitch = FindTargetPitch(detectedPitch);

            // Calculate correction in cents
            float pitchRatio = _targetPitch / detectedPitch;
            float correctionSemitones = 12f * MathF.Log2(pitchRatio);
            float correctionCents = correctionSemitones * 100f;

            // Apply humanize (reduce correction intensity)
            correctionCents *= (1f - _humanizeAmount);

            // Smooth the correction
            _pitchCorrectionCents = _smoothedCorrection + _smoothingCoeff * (correctionCents - _smoothedCorrection);
            _smoothedCorrection = _pitchCorrectionCents;

            // Calculate actual pitch ratio to apply
            float actualCorrectionSemitones = _pitchCorrectionCents / 100f;
            float actualPitchRatio = MathF.Pow(2f, actualCorrectionSemitones / 12f);

            // Update output pitch
            _currentOutputPitch = detectedPitch * actualPitchRatio;

            // Apply pitch shift via phase vocoder
            ApplyPitchShift(actualPitchRatio);
        }
        else
        {
            // No pitch detected - pass through unchanged
            _currentOutputPitch = 0;
            _targetPitch = 0;
            _pitchCorrectionCents = 0;

            PassThroughFrame();
        }
    }

    /// <summary>
    /// Detects pitch using normalized autocorrelation method.
    /// </summary>
    private float DetectPitchAutocorrelation()
    {
        int bufferSize = _analysisBufferSize;
        int halfSize = bufferSize / 2;

        // Calculate autocorrelation
        for (int lag = 0; lag < halfSize; lag++)
        {
            float sum = 0f;
            float energy1 = 0f;
            float energy2 = 0f;

            for (int j = 0; j < halfSize; j++)
            {
                int idx1 = (_analysisWritePos - bufferSize + j + _analysisBufferSize) % _analysisBufferSize;
                int idx2 = (_analysisWritePos - bufferSize + j + lag + _analysisBufferSize) % _analysisBufferSize;

                float s1 = _analysisBuffer[idx1];
                float s2 = _analysisBuffer[idx2];

                sum += s1 * s2;
                energy1 += s1 * s1;
                energy2 += s2 * s2;
            }

            // Normalized autocorrelation
            float denom = MathF.Sqrt(energy1 * energy2);
            _autocorrelation[lag] = denom > 0.0001f ? sum / denom : 0f;
        }

        // Find the first significant peak after the initial decline
        // Skip very short periods (high frequencies above ~2000 Hz)
        int minLag = SampleRate / 2000; // ~22 samples at 44100 Hz
        int maxLag = SampleRate / 50;   // ~882 samples at 44100 Hz (50 Hz minimum)

        // Find first dip (the autocorrelation typically dips before the fundamental period)
        int firstDip = minLag;
        for (int i = minLag; i < Math.Min(maxLag, halfSize - 1); i++)
        {
            if (_autocorrelation[i] < _autocorrelation[i - 1] && _autocorrelation[i] < _autocorrelation[i + 1])
            {
                firstDip = i;
                break;
            }
        }

        // Find the highest peak after the dip
        float maxCorr = -1f;
        int bestLag = -1;
        float threshold = 0.3f; // Minimum correlation threshold

        for (int i = firstDip; i < Math.Min(maxLag, halfSize - 1); i++)
        {
            if (_autocorrelation[i] > maxCorr && _autocorrelation[i] > threshold)
            {
                // Check if it's a local maximum
                if (_autocorrelation[i] > _autocorrelation[i - 1] && _autocorrelation[i] > _autocorrelation[i + 1])
                {
                    maxCorr = _autocorrelation[i];
                    bestLag = i;
                }
            }
        }

        if (bestLag < 0)
        {
            return -1f; // No pitch detected
        }

        // Parabolic interpolation for sub-sample accuracy
        float betterLag = bestLag;
        if (bestLag > 0 && bestLag < halfSize - 1)
        {
            float s0 = _autocorrelation[bestLag - 1];
            float s1 = _autocorrelation[bestLag];
            float s2 = _autocorrelation[bestLag + 1];

            float d = (s0 - s2) / (2f * (s0 - 2f * s1 + s2));
            if (!float.IsNaN(d) && MathF.Abs(d) < 1f)
            {
                betterLag = bestLag + d;
            }
        }

        // Convert lag to frequency
        float pitch = SampleRate / betterLag;

        // Sanity check for vocal range (approximately 80 Hz to 1000 Hz)
        if (pitch < 60f || pitch > 1500f)
        {
            return -1f;
        }

        return pitch;
    }

    /// <summary>
    /// Finds the target pitch by snapping to the nearest valid note in the scale.
    /// </summary>
    private float FindTargetPitch(float inputPitch)
    {
        // Convert frequency to MIDI note number (fractional)
        // MIDI note 69 = A4 = 440 Hz
        float midiNote = 69f + 12f * MathF.Log2(inputPitch / 440f);

        // Get the note class (0-11) and octave
        int roundedMidi = (int)MathF.Round(midiNote);
        int noteClass = ((roundedMidi % 12) + 12) % 12;

        // If chromatic mode, snap to nearest semitone
        if (_scale == PitchCorrectorScale.Chromatic)
        {
            return 440f * MathF.Pow(2f, (roundedMidi - 69f) / 12f);
        }

        // Check if current note is valid
        if (_validNotes[noteClass])
        {
            return 440f * MathF.Pow(2f, (roundedMidi - 69f) / 12f);
        }

        // Find nearest valid note
        int lowerNote = noteClass;
        int upperNote = noteClass;
        int lowerDist = 0;
        int upperDist = 0;

        // Search downward
        while (!_validNotes[lowerNote] && lowerDist < 12)
        {
            lowerNote = (lowerNote - 1 + 12) % 12;
            lowerDist++;
        }

        // Search upward
        while (!_validNotes[upperNote] && upperDist < 12)
        {
            upperNote = (upperNote + 1) % 12;
            upperDist++;
        }

        // Choose the closer note
        int targetNoteClass;
        int semitoneShift;
        if (lowerDist <= upperDist)
        {
            targetNoteClass = lowerNote;
            semitoneShift = -lowerDist;
        }
        else
        {
            targetNoteClass = upperNote;
            semitoneShift = upperDist;
        }

        // Calculate target MIDI note
        int targetMidi = roundedMidi + semitoneShift;

        return 440f * MathF.Pow(2f, (targetMidi - 69f) / 12f);
    }

    /// <summary>
    /// Applies pitch shift using a simplified phase vocoder approach.
    /// </summary>
    private void ApplyPitchShift(float pitchRatio)
    {
        // Extract analysis frame
        int frameStart = (_inputWritePos - _fftSize + _inputBuffer.Length) % _inputBuffer.Length;

        // Simple resampling-based pitch shift for efficiency
        // This is a simplified approach; a full phase vocoder would be more complex

        float outputGain = 1f / _overlapCount; // Normalize for overlap-add

        for (int i = 0; i < _hopSize; i++)
        {
            // Calculate source position with pitch ratio
            float srcPosFloat = i * pitchRatio;
            int srcPosInt = (int)srcPosFloat;
            float frac = srcPosFloat - srcPosInt;

            float sample = 0f;

            if (srcPosInt < _fftSize - 1)
            {
                int idx1 = (frameStart + srcPosInt) % _inputBuffer.Length;
                int idx2 = (frameStart + srcPosInt + 1) % _inputBuffer.Length;

                // Linear interpolation
                sample = _inputBuffer[idx1] * (1f - frac) + _inputBuffer[idx2] * frac;
            }
            else if (srcPosInt < _fftSize)
            {
                int idx = (frameStart + srcPosInt) % _inputBuffer.Length;
                sample = _inputBuffer[idx];
            }

            // Apply window
            float windowed = sample * _windowFunction[i] * outputGain;

            // If formant preservation is enabled, apply correction
            if (_formantPreserve && MathF.Abs(pitchRatio - 1f) > 0.001f)
            {
                windowed = ApplyFormantCorrection(windowed, pitchRatio, i);
            }

            // Add to output buffer (overlap-add)
            int outIdx = (_outputReadPos + i) % _outputBuffer.Length;
            _outputBuffer[outIdx] += windowed;
        }
    }

    /// <summary>
    /// Applies formant correction to counteract formant shift from pitch shifting.
    /// </summary>
    private float ApplyFormantCorrection(float sample, float pitchRatio, int sampleIndex)
    {
        // Simplified formant preservation using a resonant filter approach
        // When pitch goes up, formants shift up - we need to shift them back down
        // When pitch goes down, formants shift down - we need to shift them back up

        // This is a simplified single-pole filter approximation
        // A full implementation would use LPC or cepstral analysis

        if (pitchRatio > 1f)
        {
            // Pitch raised - apply subtle low-pass to counteract formant shift
            float alpha = 1f / pitchRatio;
            alpha = Math.Clamp(alpha, 0.5f, 1f);

            // Simple smoothing
            if (sampleIndex > 0)
            {
                sample = sample * alpha + _formantEnvelope[0] * (1f - alpha);
            }
            _formantEnvelope[0] = sample;
        }
        else if (pitchRatio < 1f)
        {
            // Pitch lowered - apply subtle high-pass to counteract formant shift
            float alpha = pitchRatio;
            alpha = Math.Clamp(alpha, 0.5f, 1f);

            // Simple differentiation for brightness
            float diff = sample - _formantEnvelope[0];
            _formantEnvelope[0] = sample;
            sample = sample * alpha + diff * (1f - alpha) * 0.5f;
        }

        return sample;
    }

    /// <summary>
    /// Passes through audio unchanged when no pitch is detected.
    /// </summary>
    private void PassThroughFrame()
    {
        int frameStart = (_inputWritePos - _hopSize + _inputBuffer.Length) % _inputBuffer.Length;
        float outputGain = 1f / _overlapCount;

        for (int i = 0; i < _hopSize; i++)
        {
            int inIdx = (frameStart + i) % _inputBuffer.Length;
            int outIdx = (_outputReadPos + i) % _outputBuffer.Length;

            float sample = _inputBuffer[inIdx] * _windowFunction[i] * outputGain;
            _outputBuffer[outIdx] += sample;
        }
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "key":
                Key = (PitchCorrectorKey)Math.Clamp((int)value, 0, 11);
                break;
            case "scale":
                Scale = (PitchCorrectorScale)Math.Clamp((int)value, 0, 14);
                break;
            case "correctionspeed":
                CorrectionSpeed = value;
                break;
            case "humanize":
                HumanizeAmount = value;
                break;
            case "formantpreserve":
                FormantPreserve = value > 0.5f;
                break;
            case "mix":
                Mix = value;
                break;
        }
    }

    /// <summary>
    /// Resets the pitch corrector state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_analysisBuffer, 0, _analysisBuffer.Length);
        Array.Clear(_autocorrelation, 0, _autocorrelation.Length);
        Array.Clear(_inputBuffer, 0, _inputBuffer.Length);
        Array.Clear(_outputBuffer, 0, _outputBuffer.Length);
        Array.Clear(_lastPhase, 0, _lastPhase.Length);
        Array.Clear(_sumPhase, 0, _sumPhase.Length);
        Array.Clear(_formantEnvelope, 0, _formantEnvelope.Length);

        _analysisWritePos = 0;
        _inputWritePos = 0;
        _outputReadPos = 0;
        _frameCounter = 0;

        _currentInputPitch = 0;
        _currentOutputPitch = 0;
        _targetPitch = 0;
        _pitchCorrectionCents = 0;
        _smoothedCorrection = 0;
    }

    /// <summary>
    /// Creates a preset for hard/robotic pitch correction (T-Pain style).
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured PitchCorrector instance.</returns>
    public static PitchCorrector CreateHardTunePreset(ISampleProvider source)
    {
        return new PitchCorrector(source)
        {
            Key = PitchCorrectorKey.C,
            Scale = PitchCorrectorScale.Chromatic,
            CorrectionSpeed = 0f, // Instant
            HumanizeAmount = 0f,
            FormantPreserve = false // Robotic effect often benefits from formant shift
        };
    }

    /// <summary>
    /// Creates a preset for natural, subtle pitch correction.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured PitchCorrector instance.</returns>
    public static PitchCorrector CreateNaturalPreset(ISampleProvider source)
    {
        return new PitchCorrector(source)
        {
            Key = PitchCorrectorKey.C,
            Scale = PitchCorrectorScale.Major,
            CorrectionSpeed = 0.6f, // Moderate
            HumanizeAmount = 0.4f,
            FormantPreserve = true
        };
    }

    /// <summary>
    /// Creates a preset for live performance with fast but musical correction.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured PitchCorrector instance.</returns>
    public static PitchCorrector CreateLivePreset(ISampleProvider source)
    {
        return new PitchCorrector(source)
        {
            Key = PitchCorrectorKey.C,
            Scale = PitchCorrectorScale.Chromatic,
            CorrectionSpeed = 0.3f, // Fairly fast
            HumanizeAmount = 0.2f,
            FormantPreserve = true
        };
    }

    /// <summary>
    /// Creates a preset for gentle correction suitable for acoustic/ballad vocals.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <returns>A configured PitchCorrector instance.</returns>
    public static PitchCorrector CreateGentlePreset(ISampleProvider source)
    {
        return new PitchCorrector(source)
        {
            Key = PitchCorrectorKey.C,
            Scale = PitchCorrectorScale.Major,
            CorrectionSpeed = 0.8f, // Slow
            HumanizeAmount = 0.6f,
            FormantPreserve = true
        };
    }
}
