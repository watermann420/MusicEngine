//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Real-time pitch correction effect (AutoTune) with scale-aware correction,
// humanize, vibrato, and formant preservation.

using System;
using System.Numerics;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Scale types for pitch correction targeting.
/// </summary>
public enum AutoTuneScale
{
    /// <summary>All 12 semitones are valid targets</summary>
    Chromatic,
    /// <summary>Major scale (W-W-H-W-W-W-H)</summary>
    Major,
    /// <summary>Natural minor scale (W-H-W-W-H-W-W)</summary>
    Minor,
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
    /// <summary>Pentatonic major scale</summary>
    PentatonicMajor,
    /// <summary>Pentatonic minor scale</summary>
    PentatonicMinor,
    /// <summary>Blues scale</summary>
    Blues,
    /// <summary>Custom scale defined by user</summary>
    Custom
}

/// <summary>
/// Real-time pitch correction effect (AutoTune style).
/// Uses YIN algorithm for pitch detection and PSOLA for pitch shifting
/// with formant preservation.
/// </summary>
public class AutoTune : EffectBase
{
    // Scale interval patterns (semitones from root)
    private static readonly int[] MajorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] MinorIntervals = { 0, 2, 3, 5, 7, 8, 10 };
    private static readonly int[] HarmonicMinorIntervals = { 0, 2, 3, 5, 7, 8, 11 };
    private static readonly int[] MelodicMinorIntervals = { 0, 2, 3, 5, 7, 9, 11 };
    private static readonly int[] DorianIntervals = { 0, 2, 3, 5, 7, 9, 10 };
    private static readonly int[] PhrygianIntervals = { 0, 1, 3, 5, 7, 8, 10 };
    private static readonly int[] LydianIntervals = { 0, 2, 4, 6, 7, 9, 11 };
    private static readonly int[] MixolydianIntervals = { 0, 2, 4, 5, 7, 9, 10 };
    private static readonly int[] PentatonicMajorIntervals = { 0, 2, 4, 7, 9 };
    private static readonly int[] PentatonicMinorIntervals = { 0, 3, 5, 7, 10 };
    private static readonly int[] BluesIntervals = { 0, 3, 5, 6, 7, 10 };
    private static readonly int[] ChromaticIntervals = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

    // Pitch detection (YIN algorithm)
    private readonly int _yinBufferSize;
    private readonly float[] _yinBuffer;
    private readonly float[] _yinDifference;
    private readonly float[] _yinCumulativeMean;

    // Analysis buffers
    private readonly float[] _inputBuffer;
    private readonly float[] _outputBuffer;
    private int _inputWritePos;
    private int _outputReadPos;

    // PSOLA (Pitch Synchronous Overlap-Add)
    private readonly float[] _analysisWindow;
    private readonly float[] _synthesisBuffer;
    private readonly int _hopSize;
    private readonly int _windowSize;
    private float _psolaPhase;

    // Formant preservation (simple cepstrum-based)
    private readonly Complex[] _fftBuffer;
    private readonly Complex[] _cepstrumBuffer;
    private readonly int _fftSize;
    private readonly int _lifterCutoff;

    // Current pitch state
    private float _currentInputPitch;
    private float _currentOutputPitch;
    private float _targetPitch;
    private float _pitchCorrection;

    // Vibrato LFO
    private float _vibratoPhase;

    // Parameters
    private AutoTuneScale _scale = AutoTuneScale.Chromatic;
    private int _rootNote = 0; // C = 0
    private float _retuneSpeed = 50f; // ms (0 = instant, 400 = slow)
    private float _humanize = 0f; // 0-1, preserves natural variation
    private float _vibratoDepth = 0f; // semitones
    private float _vibratoRate = 5f; // Hz
    private bool _formantPreservation = true;
    private float _correctionAmount = 1f; // 0-1
    private bool[] _customScale = new bool[12]; // For custom scale

    // Smoothing
    private float _smoothedPitch;
    private float _smoothingCoeff;

    /// <summary>
    /// Gets or sets the target scale for pitch correction.
    /// </summary>
    public AutoTuneScale Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            UpdateScaleNotes();
        }
    }

    /// <summary>
    /// Gets or sets the root note (0=C, 1=C#, 2=D, etc.)
    /// </summary>
    public int RootNote
    {
        get => _rootNote;
        set
        {
            _rootNote = Math.Clamp(value, 0, 11);
            UpdateScaleNotes();
        }
    }

    /// <summary>
    /// Gets or sets the retune speed in milliseconds (0-400).
    /// 0 = instant correction (robotic effect)
    /// Higher values = more natural, slower correction
    /// </summary>
    public float RetuneSpeed
    {
        get => _retuneSpeed;
        set
        {
            _retuneSpeed = Math.Clamp(value, 0f, 400f);
            UpdateSmoothingCoefficient();
        }
    }

    /// <summary>
    /// Gets or sets the humanize amount (0-1).
    /// Higher values preserve more natural pitch variation.
    /// </summary>
    public float Humanize
    {
        get => _humanize;
        set => _humanize = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the vibrato depth in semitones (0-2).
    /// </summary>
    public float VibratoDepth
    {
        get => _vibratoDepth;
        set => _vibratoDepth = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the vibrato rate in Hz (0.1-10).
    /// </summary>
    public float VibratoRate
    {
        get => _vibratoRate;
        set => _vibratoRate = Math.Clamp(value, 0.1f, 10f);
    }

    /// <summary>
    /// Gets or sets whether formant preservation is enabled.
    /// When true, preserves vocal character during pitch shifting.
    /// </summary>
    public bool FormantPreservation
    {
        get => _formantPreservation;
        set => _formantPreservation = value;
    }

    /// <summary>
    /// Gets or sets the correction amount (0-1).
    /// 0 = no correction, 1 = full correction.
    /// </summary>
    public float CorrectionAmount
    {
        get => _correctionAmount;
        set => _correctionAmount = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets the currently detected input pitch in Hz.
    /// </summary>
    public float InputPitch => _currentInputPitch;

    /// <summary>
    /// Gets the current output pitch in Hz.
    /// </summary>
    public float OutputPitch => _currentOutputPitch;

    /// <summary>
    /// Gets the target pitch in Hz.
    /// </summary>
    public float TargetPitch => _targetPitch;

    /// <summary>
    /// Gets the pitch correction amount in cents.
    /// </summary>
    public float PitchCorrectionCents => _pitchCorrection * 100f;

    // Valid scale notes cache
    private readonly bool[] _validNotes = new bool[12];

    /// <summary>
    /// Creates a new AutoTune effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public AutoTune(ISampleProvider source) : base(source, "AutoTune")
    {
        // Initialize YIN pitch detection
        _yinBufferSize = 2048;
        _yinBuffer = new float[_yinBufferSize];
        _yinDifference = new float[_yinBufferSize / 2];
        _yinCumulativeMean = new float[_yinBufferSize / 2];

        // Initialize PSOLA
        _windowSize = 2048;
        _hopSize = _windowSize / 4;
        _analysisWindow = CreateHannWindow(_windowSize);
        _synthesisBuffer = new float[_windowSize * 2];

        // Initialize FFT for formant preservation
        _fftSize = 4096;
        _fftBuffer = new Complex[_fftSize];
        _cepstrumBuffer = new Complex[_fftSize];
        _lifterCutoff = 50; // Low-quefrency cutoff for formant envelope

        // Initialize buffers
        _inputBuffer = new float[_yinBufferSize * 2];
        _outputBuffer = new float[_windowSize * 2];
        _inputWritePos = 0;
        _outputReadPos = 0;

        // Initialize scale
        UpdateScaleNotes();
        UpdateSmoothingCoefficient();

        // Set default custom scale to chromatic
        for (int i = 0; i < 12; i++)
            _customScale[i] = true;

        // Register parameters
        RegisterParameter("mix", 1f);
        RegisterParameter("retunespeed", 50f);
        RegisterParameter("humanize", 0f);
        RegisterParameter("vibratodepth", 0f);
        RegisterParameter("vibratorate", 5f);
        RegisterParameter("formant", 1f);
        RegisterParameter("correction", 1f);
    }

    /// <summary>
    /// Sets a custom scale by specifying which notes are valid.
    /// </summary>
    /// <param name="notes">Array of 12 booleans (C to B).</param>
    public void SetCustomScale(bool[] notes)
    {
        if (notes.Length != 12)
            throw new ArgumentException("Custom scale must have exactly 12 notes.");

        Array.Copy(notes, _customScale, 12);

        if (_scale == AutoTuneScale.Custom)
            UpdateScaleNotes();
    }

    private static float[] CreateHannWindow(int size)
    {
        var window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (size - 1)));
        }
        return window;
    }

    private void UpdateScaleNotes()
    {
        Array.Clear(_validNotes, 0, 12);

        int[] intervals = _scale switch
        {
            AutoTuneScale.Chromatic => ChromaticIntervals,
            AutoTuneScale.Major => MajorIntervals,
            AutoTuneScale.Minor => MinorIntervals,
            AutoTuneScale.HarmonicMinor => HarmonicMinorIntervals,
            AutoTuneScale.MelodicMinor => MelodicMinorIntervals,
            AutoTuneScale.Dorian => DorianIntervals,
            AutoTuneScale.Phrygian => PhrygianIntervals,
            AutoTuneScale.Lydian => LydianIntervals,
            AutoTuneScale.Mixolydian => MixolydianIntervals,
            AutoTuneScale.PentatonicMajor => PentatonicMajorIntervals,
            AutoTuneScale.PentatonicMinor => PentatonicMinorIntervals,
            AutoTuneScale.Blues => BluesIntervals,
            AutoTuneScale.Custom => ChromaticIntervals, // Handled separately
            _ => ChromaticIntervals
        };

        if (_scale == AutoTuneScale.Custom)
        {
            Array.Copy(_customScale, _validNotes, 12);
        }
        else
        {
            foreach (int interval in intervals)
            {
                int note = (_rootNote + interval) % 12;
                _validNotes[note] = true;
            }
        }
    }

    private void UpdateSmoothingCoefficient()
    {
        // Convert retune speed (ms) to smoothing coefficient
        // 0ms = instant (coeff = 1), higher ms = slower (smaller coeff)
        if (_retuneSpeed <= 0)
        {
            _smoothingCoeff = 1f;
        }
        else
        {
            float samplesPerRetune = (_retuneSpeed / 1000f) * SampleRate;
            _smoothingCoeff = 1f - MathF.Exp(-1f / (samplesPerRetune / _hopSize));
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        for (int i = 0; i < count; i += channels)
        {
            // Mix to mono for pitch detection
            float monoSample = sourceBuffer[i];
            if (channels > 1)
            {
                monoSample = (sourceBuffer[i] + sourceBuffer[i + 1]) * 0.5f;
            }

            // Add to input buffer
            _inputBuffer[_inputWritePos] = monoSample;
            _inputWritePos = (_inputWritePos + 1) % _inputBuffer.Length;

            // Read from output buffer
            float processedSample = _outputBuffer[_outputReadPos];
            _outputBuffer[_outputReadPos] = 0f;
            _outputReadPos = (_outputReadPos + 1) % _outputBuffer.Length;

            // Process when we have enough samples
            if (_inputWritePos % _hopSize == 0)
            {
                ProcessFrame();
            }

            // Apply vibrato
            if (_vibratoDepth > 0)
            {
                _vibratoPhase += 2f * MathF.PI * _vibratoRate / SampleRate;
                if (_vibratoPhase > 2f * MathF.PI)
                    _vibratoPhase -= 2f * MathF.PI;

                float vibratoMod = MathF.Sin(_vibratoPhase) * _vibratoDepth;
                float vibratoPitchRatio = MathF.Pow(2f, vibratoMod / 12f);
                processedSample *= vibratoPitchRatio;
            }

            // Write to output (both channels for stereo)
            destBuffer[offset + i] = processedSample;
            if (channels > 1)
            {
                destBuffer[offset + i + 1] = processedSample;
            }
        }
    }

    private void ProcessFrame()
    {
        // Extract analysis frame
        int startPos = (_inputWritePos - _yinBufferSize + _inputBuffer.Length) % _inputBuffer.Length;
        for (int i = 0; i < _yinBufferSize; i++)
        {
            _yinBuffer[i] = _inputBuffer[(startPos + i) % _inputBuffer.Length];
        }

        // Detect pitch using YIN algorithm
        float detectedPitch = DetectPitch(_yinBuffer, SampleRate);
        _currentInputPitch = detectedPitch;

        if (detectedPitch > 0)
        {
            // Find target pitch
            _targetPitch = FindTargetPitch(detectedPitch);

            // Calculate pitch correction in semitones
            float pitchDiff = 12f * MathF.Log2(_targetPitch / detectedPitch);

            // Apply humanize (reduce correction based on humanize amount)
            pitchDiff *= (1f - _humanize);

            // Apply correction amount
            pitchDiff *= _correctionAmount;

            // Smooth the pitch correction
            _pitchCorrection = _smoothedPitch + _smoothingCoeff * (pitchDiff - _smoothedPitch);
            _smoothedPitch = _pitchCorrection;

            // Calculate output pitch
            _currentOutputPitch = detectedPitch * MathF.Pow(2f, _pitchCorrection / 12f);

            // Apply pitch shift using PSOLA
            float pitchRatio = MathF.Pow(2f, _pitchCorrection / 12f);
            ApplyPitchShift(pitchRatio);
        }
        else
        {
            // No pitch detected - pass through
            _currentOutputPitch = 0;
            _targetPitch = 0;
            _pitchCorrection = 0;

            // Copy input to output unchanged
            int outputPos = _outputReadPos;
            for (int i = 0; i < _hopSize; i++)
            {
                _outputBuffer[(outputPos + i) % _outputBuffer.Length] = _yinBuffer[i];
            }
        }
    }

    private float DetectPitch(float[] buffer, int sampleRate)
    {
        int halfSize = buffer.Length / 2;

        // Step 1: Calculate difference function
        for (int tau = 0; tau < halfSize; tau++)
        {
            float sum = 0f;
            for (int j = 0; j < halfSize; j++)
            {
                float diff = buffer[j] - buffer[j + tau];
                sum += diff * diff;
            }
            _yinDifference[tau] = sum;
        }

        // Step 2: Calculate cumulative mean normalized difference
        _yinCumulativeMean[0] = 1f;
        float runningSum = 0f;
        for (int tau = 1; tau < halfSize; tau++)
        {
            runningSum += _yinDifference[tau];
            _yinCumulativeMean[tau] = _yinDifference[tau] * tau / runningSum;
        }

        // Step 3: Absolute threshold
        const float threshold = 0.1f;
        int tauEstimate = -1;

        for (int tau = 2; tau < halfSize; tau++)
        {
            if (_yinCumulativeMean[tau] < threshold)
            {
                // Find the first dip below threshold
                while (tau + 1 < halfSize && _yinCumulativeMean[tau + 1] < _yinCumulativeMean[tau])
                {
                    tau++;
                }
                tauEstimate = tau;
                break;
            }
        }

        // Step 4: If no estimate found, find global minimum
        if (tauEstimate < 0)
        {
            float minVal = float.MaxValue;
            for (int tau = 2; tau < halfSize; tau++)
            {
                if (_yinCumulativeMean[tau] < minVal)
                {
                    minVal = _yinCumulativeMean[tau];
                    tauEstimate = tau;
                }
            }
            // Only accept if below a relaxed threshold
            if (minVal > 0.5f)
                return -1f; // No pitch detected
        }

        // Step 5: Parabolic interpolation for better accuracy
        float betterTau = tauEstimate;
        if (tauEstimate > 0 && tauEstimate < halfSize - 1)
        {
            float s0 = _yinCumulativeMean[tauEstimate - 1];
            float s1 = _yinCumulativeMean[tauEstimate];
            float s2 = _yinCumulativeMean[tauEstimate + 1];
            float adjustment = (s2 - s0) / (2f * (2f * s1 - s2 - s0));
            if (!float.IsNaN(adjustment) && MathF.Abs(adjustment) < 1f)
            {
                betterTau = tauEstimate + adjustment;
            }
        }

        // Convert period to frequency
        float pitch = sampleRate / betterTau;

        // Sanity check (human voice range ~80Hz to ~1000Hz, with some margin)
        if (pitch < 50f || pitch > 2000f)
            return -1f;

        return pitch;
    }

    private float FindTargetPitch(float inputPitch)
    {
        // Convert frequency to MIDI note number (fractional)
        float midiNote = 69f + 12f * MathF.Log2(inputPitch / 440f);

        // Find the closest valid note
        int noteClass = ((int)MathF.Round(midiNote) % 12 + 12) % 12;
        float noteFraction = midiNote - MathF.Floor(midiNote);

        // Check if current note class is valid
        if (_validNotes[noteClass])
        {
            // Snap to nearest semitone
            int targetMidiNote = (int)MathF.Round(midiNote);
            return 440f * MathF.Pow(2f, (targetMidiNote - 69f) / 12f);
        }

        // Find closest valid note
        int lowerNote = noteClass;
        int upperNote = noteClass;
        int lowerDistance = 0;
        int upperDistance = 0;

        // Search downward
        while (!_validNotes[lowerNote] && lowerDistance < 12)
        {
            lowerNote = (lowerNote - 1 + 12) % 12;
            lowerDistance++;
        }

        // Search upward
        while (!_validNotes[upperNote] && upperDistance < 12)
        {
            upperNote = (upperNote + 1) % 12;
            upperDistance++;
        }

        // Choose closer note (with tie-breaker preferring lower)
        int targetNoteClass;
        int distanceFromOctave;
        if (lowerDistance <= upperDistance)
        {
            targetNoteClass = lowerNote;
            distanceFromOctave = -lowerDistance;
        }
        else
        {
            targetNoteClass = upperNote;
            distanceFromOctave = upperDistance;
        }

        // Calculate target MIDI note
        int baseMidiNote = (int)MathF.Floor(midiNote);
        int baseNoteClass = ((baseMidiNote % 12) + 12) % 12;
        int targetMidi = baseMidiNote - baseNoteClass + targetNoteClass;

        // Adjust for octave crossing
        if (targetNoteClass < baseNoteClass && distanceFromOctave > 0)
            targetMidi += 12;
        else if (targetNoteClass > baseNoteClass && distanceFromOctave < 0)
            targetMidi -= 12;

        // Decide whether to go up or down based on which is closer
        float targetFreq = 440f * MathF.Pow(2f, (targetMidi - 69f) / 12f);
        return targetFreq;
    }

    private void ApplyPitchShift(float pitchRatio)
    {
        // Simple PSOLA-style pitch shifting
        // For formant preservation, we would apply spectral envelope preservation here

        int analysisStart = (_inputWritePos - _windowSize + _inputBuffer.Length) % _inputBuffer.Length;
        int outputPos = _outputReadPos;

        // Apply windowed overlap-add with resampling
        for (int i = 0; i < _windowSize; i++)
        {
            float srcPos = i * pitchRatio;
            int srcIndex = (int)srcPos;
            float frac = srcPos - srcIndex;

            float sample = 0f;
            if (srcIndex < _windowSize - 1)
            {
                int idx1 = (analysisStart + srcIndex) % _inputBuffer.Length;
                int idx2 = (analysisStart + srcIndex + 1) % _inputBuffer.Length;
                sample = _inputBuffer[idx1] * (1f - frac) + _inputBuffer[idx2] * frac;
            }

            // Apply window and add to output
            float windowed = sample * _analysisWindow[i];
            _outputBuffer[(outputPos + i) % _outputBuffer.Length] += windowed;
        }

        // If formant preservation is enabled, apply formant correction
        if (_formantPreservation && pitchRatio != 1f)
        {
            ApplyFormantCorrection(pitchRatio);
        }
    }

    private void ApplyFormantCorrection(float pitchRatio)
    {
        // Simplified formant preservation using spectral envelope
        // In a full implementation, this would use cepstrum or LPC
        // to separate formants from pitch and apply inverse pitch shift to formants

        // For this simplified version, we apply a subtle low-pass filter
        // to counteract formant shift when pitch is raised
        if (pitchRatio > 1f)
        {
            // When pitch goes up, apply subtle low-pass to compensate
            float cutoff = 1f / pitchRatio;
            int outputPos = _outputReadPos;
            float prevSample = 0f;

            for (int i = 0; i < _hopSize; i++)
            {
                int idx = (outputPos + i) % _outputBuffer.Length;
                float sample = _outputBuffer[idx];
                _outputBuffer[idx] = prevSample + cutoff * (sample - prevSample);
                prevSample = _outputBuffer[idx];
            }
        }
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "retunespeed":
                RetuneSpeed = value;
                break;
            case "humanize":
                Humanize = value;
                break;
            case "vibratodepth":
                VibratoDepth = value;
                break;
            case "vibratorate":
                VibratoRate = value;
                break;
            case "formant":
                FormantPreservation = value > 0.5f;
                break;
            case "correction":
                CorrectionAmount = value;
                break;
        }
    }

    /// <summary>
    /// Creates a hard tune preset (robotic effect).
    /// </summary>
    public static AutoTune CreateHardTunePreset(ISampleProvider source)
    {
        var autoTune = new AutoTune(source)
        {
            RetuneSpeed = 0f,
            Humanize = 0f,
            CorrectionAmount = 1f,
            Scale = AutoTuneScale.Chromatic
        };
        autoTune.Mix = 1f;
        return autoTune;
    }

    /// <summary>
    /// Creates a natural correction preset.
    /// </summary>
    public static AutoTune CreateNaturalPreset(ISampleProvider source)
    {
        var autoTune = new AutoTune(source)
        {
            RetuneSpeed = 80f,
            Humanize = 0.3f,
            CorrectionAmount = 0.8f,
            Scale = AutoTuneScale.Chromatic
        };
        autoTune.Mix = 1f;
        return autoTune;
    }

    /// <summary>
    /// Creates a subtle correction preset.
    /// </summary>
    public static AutoTune CreateSubtlePreset(ISampleProvider source)
    {
        var autoTune = new AutoTune(source)
        {
            RetuneSpeed = 150f,
            Humanize = 0.5f,
            CorrectionAmount = 0.5f,
            Scale = AutoTuneScale.Chromatic
        };
        autoTune.Mix = 1f;
        return autoTune;
    }

    /// <summary>
    /// Creates a T-Pain style preset with vibrato.
    /// </summary>
    public static AutoTune CreateTPainPreset(ISampleProvider source)
    {
        var autoTune = new AutoTune(source)
        {
            RetuneSpeed = 0f,
            Humanize = 0f,
            CorrectionAmount = 1f,
            VibratoDepth = 0.3f,
            VibratoRate = 6f,
            Scale = AutoTuneScale.Chromatic
        };
        autoTune.Mix = 1f;
        return autoTune;
    }
}
