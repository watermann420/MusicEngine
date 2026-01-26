//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Real-time pitch-based harmonizer effect that adds parallel voices with scale-aware harmonization.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Musical scale types for scale-aware harmonization.
/// </summary>
public enum HarmonizerScale
{
    /// <summary>All 12 semitones are valid (no scale constraint)</summary>
    Chromatic,
    /// <summary>Major scale (Ionian mode)</summary>
    Major,
    /// <summary>Natural minor scale (Aeolian mode)</summary>
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
    Blues
}

/// <summary>
/// Quality mode for the harmonizer pitch shifting algorithm.
/// </summary>
public enum HarmonizerQuality
{
    /// <summary>Fast mode with smaller FFT (1024). Lower latency, lower quality.</summary>
    Fast,
    /// <summary>Normal mode with medium FFT (2048). Balanced latency and quality.</summary>
    Normal,
    /// <summary>High quality mode with larger FFT (4096). Higher latency, best quality.</summary>
    HighQuality
}

/// <summary>
/// Real-time pitch-based harmonizer effect that adds parallel harmony voices.
/// Uses phase vocoder pitch shifting with scale-aware quantization to keep
/// harmonies in key. Supports up to 4 independent harmony voices.
/// </summary>
/// <remarks>
/// The harmonizer can operate in two modes:
/// 1. Fixed interval mode: Each voice is shifted by a fixed number of semitones
/// 2. Scale-aware mode: Intervals are adjusted to stay within the selected scale
///
/// Each harmony voice has independent level control and can be panned in stereo.
/// Formant preservation is available to maintain vocal character during pitch shift.
/// </remarks>
public class Harmonizer : EffectBase
{
    // Scale interval patterns (semitones from root)
    private static readonly int[] MajorIntervals = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorIntervals = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] HarmonicMinorIntervals = [0, 2, 3, 5, 7, 8, 11];
    private static readonly int[] MelodicMinorIntervals = [0, 2, 3, 5, 7, 9, 11];
    private static readonly int[] DorianIntervals = [0, 2, 3, 5, 7, 9, 10];
    private static readonly int[] PhrygianIntervals = [0, 1, 3, 5, 7, 8, 10];
    private static readonly int[] LydianIntervals = [0, 2, 4, 6, 7, 9, 11];
    private static readonly int[] MixolydianIntervals = [0, 2, 4, 5, 7, 9, 10];
    private static readonly int[] PentatonicMajorIntervals = [0, 2, 4, 7, 9];
    private static readonly int[] PentatonicMinorIntervals = [0, 3, 5, 7, 10];
    private static readonly int[] BluesIntervals = [0, 3, 5, 6, 7, 10];
    private static readonly int[] ChromaticIntervals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    // Number of harmony voices supported
    private const int MaxVoices = 4;

    // FFT parameters
    private int _fftSize;
    private int _hopSize;
    private int _overlapFactor;

    // Per-voice pitch shift state
    private float[][] _voiceInputBuffer = null!;
    private float[][] _voiceOutputBuffer = null!;
    private int[] _voiceInputWritePos = null!;
    private int[] _voiceOutputReadPos = null!;
    private Complex[][] _voiceFftBuffer = null!;
    private float[][] _voiceLastInputPhase = null!;
    private float[][] _voiceAccumulatedPhase = null!;
    private float[][] _voiceSpectralEnvelope = null!;

    // Shared analysis window
    private float[] _analysisWindow = null!;

    // Frame counter for processing
    private int _samplesUntilNextFrame;

    // Scale notes cache
    private readonly bool[] _validNotes = new bool[12];

    // Voice parameters
    private readonly float[] _voiceIntervals = new float[MaxVoices];
    private readonly float[] _voiceLevels = new float[MaxVoices];
    private readonly float[] _voicePans = new float[MaxVoices];
    private readonly bool[] _voiceEnabled = new bool[MaxVoices];

    // State
    private bool _initialized;
    private HarmonizerQuality _quality = HarmonizerQuality.Normal;
    private HarmonizerScale _scale = HarmonizerScale.Chromatic;
    private int _key; // 0=C, 1=C#, 2=D, etc.
    private bool _scaleAware = true;
    private float _formantPreserve;
    private int _envelopeOrder;

    /// <summary>
    /// Creates a new Harmonizer effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public Harmonizer(ISampleProvider source) : this(source, "Harmonizer")
    {
    }

    /// <summary>
    /// Creates a new Harmonizer effect with a custom name.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="name">The effect name.</param>
    public Harmonizer(ISampleProvider source, string name) : base(source, name)
    {
        // Initialize default voice settings
        // Voice 1: Third above
        _voiceIntervals[0] = 4f;
        _voiceLevels[0] = 0.7f;
        _voicePans[0] = -0.3f;
        _voiceEnabled[0] = true;

        // Voice 2: Fifth above
        _voiceIntervals[1] = 7f;
        _voiceLevels[1] = 0.5f;
        _voicePans[1] = 0.3f;
        _voiceEnabled[1] = false;

        // Voice 3: Octave above
        _voiceIntervals[2] = 12f;
        _voiceLevels[2] = 0.4f;
        _voicePans[2] = 0f;
        _voiceEnabled[2] = false;

        // Voice 4: Third below
        _voiceIntervals[3] = -3f;
        _voiceLevels[3] = 0.6f;
        _voicePans[3] = 0f;
        _voiceEnabled[3] = false;

        // Register parameters
        RegisterParameter("Voice1Interval", _voiceIntervals[0]);
        RegisterParameter("Voice2Interval", _voiceIntervals[1]);
        RegisterParameter("Voice3Interval", _voiceIntervals[2]);
        RegisterParameter("Voice4Interval", _voiceIntervals[3]);
        RegisterParameter("Voice1Level", _voiceLevels[0]);
        RegisterParameter("Voice2Level", _voiceLevels[1]);
        RegisterParameter("Voice3Level", _voiceLevels[2]);
        RegisterParameter("Voice4Level", _voiceLevels[3]);
        RegisterParameter("Voice1Pan", _voicePans[0]);
        RegisterParameter("Voice2Pan", _voicePans[1]);
        RegisterParameter("Voice3Pan", _voicePans[2]);
        RegisterParameter("Voice4Pan", _voicePans[3]);
        RegisterParameter("Voice1Enabled", 1f);
        RegisterParameter("Voice2Enabled", 0f);
        RegisterParameter("Voice3Enabled", 0f);
        RegisterParameter("Voice4Enabled", 0f);
        RegisterParameter("Key", 0f);
        RegisterParameter("Scale", 0f);
        RegisterParameter("ScaleAware", 1f);
        RegisterParameter("FormantPreserve", 0.5f);
        RegisterParameter("Mix", 0.5f);

        _formantPreserve = 0.5f;

        // Initialize scale
        UpdateScaleNotes();
    }

    #region Properties

    /// <summary>
    /// Gets or sets the interval in semitones for voice 1 (-24 to +24).
    /// </summary>
    public float Voice1Interval
    {
        get => _voiceIntervals[0];
        set
        {
            _voiceIntervals[0] = Math.Clamp(value, -24f, 24f);
            SetParameter("Voice1Interval", _voiceIntervals[0]);
        }
    }

    /// <summary>
    /// Gets or sets the interval in semitones for voice 2 (-24 to +24).
    /// </summary>
    public float Voice2Interval
    {
        get => _voiceIntervals[1];
        set
        {
            _voiceIntervals[1] = Math.Clamp(value, -24f, 24f);
            SetParameter("Voice2Interval", _voiceIntervals[1]);
        }
    }

    /// <summary>
    /// Gets or sets the interval in semitones for voice 3 (-24 to +24).
    /// </summary>
    public float Voice3Interval
    {
        get => _voiceIntervals[2];
        set
        {
            _voiceIntervals[2] = Math.Clamp(value, -24f, 24f);
            SetParameter("Voice3Interval", _voiceIntervals[2]);
        }
    }

    /// <summary>
    /// Gets or sets the interval in semitones for voice 4 (-24 to +24).
    /// </summary>
    public float Voice4Interval
    {
        get => _voiceIntervals[3];
        set
        {
            _voiceIntervals[3] = Math.Clamp(value, -24f, 24f);
            SetParameter("Voice4Interval", _voiceIntervals[3]);
        }
    }

    /// <summary>
    /// Gets or sets the level for voice 1 (0.0 to 1.0).
    /// </summary>
    public float Voice1Level
    {
        get => _voiceLevels[0];
        set
        {
            _voiceLevels[0] = Math.Clamp(value, 0f, 1f);
            SetParameter("Voice1Level", _voiceLevels[0]);
        }
    }

    /// <summary>
    /// Gets or sets the level for voice 2 (0.0 to 1.0).
    /// </summary>
    public float Voice2Level
    {
        get => _voiceLevels[1];
        set
        {
            _voiceLevels[1] = Math.Clamp(value, 0f, 1f);
            SetParameter("Voice2Level", _voiceLevels[1]);
        }
    }

    /// <summary>
    /// Gets or sets the level for voice 3 (0.0 to 1.0).
    /// </summary>
    public float Voice3Level
    {
        get => _voiceLevels[2];
        set
        {
            _voiceLevels[2] = Math.Clamp(value, 0f, 1f);
            SetParameter("Voice3Level", _voiceLevels[2]);
        }
    }

    /// <summary>
    /// Gets or sets the level for voice 4 (0.0 to 1.0).
    /// </summary>
    public float Voice4Level
    {
        get => _voiceLevels[3];
        set
        {
            _voiceLevels[3] = Math.Clamp(value, 0f, 1f);
            SetParameter("Voice4Level", _voiceLevels[3]);
        }
    }

    /// <summary>
    /// Gets or sets the pan position for voice 1 (-1.0 left to +1.0 right).
    /// </summary>
    public float Voice1Pan
    {
        get => _voicePans[0];
        set
        {
            _voicePans[0] = Math.Clamp(value, -1f, 1f);
            SetParameter("Voice1Pan", _voicePans[0]);
        }
    }

    /// <summary>
    /// Gets or sets the pan position for voice 2 (-1.0 left to +1.0 right).
    /// </summary>
    public float Voice2Pan
    {
        get => _voicePans[1];
        set
        {
            _voicePans[1] = Math.Clamp(value, -1f, 1f);
            SetParameter("Voice2Pan", _voicePans[1]);
        }
    }

    /// <summary>
    /// Gets or sets the pan position for voice 3 (-1.0 left to +1.0 right).
    /// </summary>
    public float Voice3Pan
    {
        get => _voicePans[2];
        set
        {
            _voicePans[2] = Math.Clamp(value, -1f, 1f);
            SetParameter("Voice3Pan", _voicePans[2]);
        }
    }

    /// <summary>
    /// Gets or sets the pan position for voice 4 (-1.0 left to +1.0 right).
    /// </summary>
    public float Voice4Pan
    {
        get => _voicePans[3];
        set
        {
            _voicePans[3] = Math.Clamp(value, -1f, 1f);
            SetParameter("Voice4Pan", _voicePans[3]);
        }
    }

    /// <summary>
    /// Gets or sets whether voice 1 is enabled.
    /// </summary>
    public bool Voice1Enabled
    {
        get => _voiceEnabled[0];
        set
        {
            _voiceEnabled[0] = value;
            SetParameter("Voice1Enabled", value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets or sets whether voice 2 is enabled.
    /// </summary>
    public bool Voice2Enabled
    {
        get => _voiceEnabled[1];
        set
        {
            _voiceEnabled[1] = value;
            SetParameter("Voice2Enabled", value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets or sets whether voice 3 is enabled.
    /// </summary>
    public bool Voice3Enabled
    {
        get => _voiceEnabled[2];
        set
        {
            _voiceEnabled[2] = value;
            SetParameter("Voice3Enabled", value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets or sets whether voice 4 is enabled.
    /// </summary>
    public bool Voice4Enabled
    {
        get => _voiceEnabled[3];
        set
        {
            _voiceEnabled[3] = value;
            SetParameter("Voice4Enabled", value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets or sets the musical key (0=C, 1=C#, 2=D, etc.).
    /// </summary>
    public int Key
    {
        get => _key;
        set
        {
            _key = Math.Clamp(value, 0, 11);
            SetParameter("Key", _key);
            UpdateScaleNotes();
        }
    }

    /// <summary>
    /// Gets or sets the musical scale for scale-aware harmonization.
    /// </summary>
    public HarmonizerScale Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            SetParameter("Scale", (float)_scale);
            UpdateScaleNotes();
        }
    }

    /// <summary>
    /// Gets or sets whether scale-aware harmonization is enabled.
    /// When enabled, harmony intervals are adjusted to stay within the selected scale.
    /// </summary>
    public bool ScaleAware
    {
        get => _scaleAware;
        set
        {
            _scaleAware = value;
            SetParameter("ScaleAware", value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets or sets the formant preservation amount (0.0 to 1.0).
    /// Higher values preserve more of the original vocal character.
    /// </summary>
    public float FormantPreserve
    {
        get => _formantPreserve;
        set
        {
            _formantPreserve = Math.Clamp(value, 0f, 1f);
            SetParameter("FormantPreserve", _formantPreserve);
        }
    }

    /// <summary>
    /// Gets or sets the processing quality.
    /// </summary>
    public HarmonizerQuality Quality
    {
        get => _quality;
        set
        {
            if (_quality != value)
            {
                _quality = value;
                _initialized = false;
            }
        }
    }

    #endregion

    /// <summary>
    /// Updates the valid notes array based on current key and scale.
    /// </summary>
    private void UpdateScaleNotes()
    {
        Array.Clear(_validNotes, 0, 12);

        int[] intervals = _scale switch
        {
            HarmonizerScale.Chromatic => ChromaticIntervals,
            HarmonizerScale.Major => MajorIntervals,
            HarmonizerScale.Minor => MinorIntervals,
            HarmonizerScale.HarmonicMinor => HarmonicMinorIntervals,
            HarmonizerScale.MelodicMinor => MelodicMinorIntervals,
            HarmonizerScale.Dorian => DorianIntervals,
            HarmonizerScale.Phrygian => PhrygianIntervals,
            HarmonizerScale.Lydian => LydianIntervals,
            HarmonizerScale.Mixolydian => MixolydianIntervals,
            HarmonizerScale.PentatonicMajor => PentatonicMajorIntervals,
            HarmonizerScale.PentatonicMinor => PentatonicMinorIntervals,
            HarmonizerScale.Blues => BluesIntervals,
            _ => ChromaticIntervals
        };

        foreach (int interval in intervals)
        {
            int note = (_key + interval) % 12;
            _validNotes[note] = true;
        }
    }

    /// <summary>
    /// Quantizes a semitone interval to the nearest valid scale degree.
    /// </summary>
    /// <param name="baseMidiNote">The MIDI note of the input pitch (can be fractional).</param>
    /// <param name="interval">The desired interval in semitones.</param>
    /// <returns>The adjusted interval that lands on a scale note.</returns>
    private float QuantizeIntervalToScale(float baseMidiNote, float interval)
    {
        if (!_scaleAware || _scale == HarmonizerScale.Chromatic)
        {
            return interval;
        }

        // Calculate target note
        float targetNote = baseMidiNote + interval;
        int targetNoteClass = ((int)MathF.Round(targetNote) % 12 + 12) % 12;

        // If target note is in scale, use it as-is
        if (_validNotes[targetNoteClass])
        {
            return MathF.Round(interval);
        }

        // Find nearest valid note
        int lowerNote = targetNoteClass;
        int upperNote = targetNoteClass;
        int lowerDistance = 0;
        int upperDistance = 0;

        while (!_validNotes[lowerNote] && lowerDistance < 6)
        {
            lowerNote = (lowerNote - 1 + 12) % 12;
            lowerDistance++;
        }

        while (!_validNotes[upperNote] && upperDistance < 6)
        {
            upperNote = (upperNote + 1) % 12;
            upperDistance++;
        }

        // Choose the closer one
        float adjustedInterval;
        if (lowerDistance <= upperDistance)
        {
            adjustedInterval = MathF.Round(interval) - lowerDistance;
        }
        else
        {
            adjustedInterval = MathF.Round(interval) + upperDistance;
        }

        return adjustedInterval;
    }

    /// <summary>
    /// Initializes internal buffers based on quality setting.
    /// </summary>
    private void Initialize()
    {
        // Set FFT size based on quality
        _fftSize = _quality switch
        {
            HarmonizerQuality.Fast => 1024,
            HarmonizerQuality.Normal => 2048,
            HarmonizerQuality.HighQuality => 4096,
            _ => 2048
        };

        _overlapFactor = 4;
        _hopSize = _fftSize / _overlapFactor;
        _envelopeOrder = Math.Min(SampleRate / 1000 + 4, _fftSize / 8);

        int halfSize = _fftSize / 2 + 1;

        // Allocate per-voice buffers
        _voiceInputBuffer = new float[MaxVoices][];
        _voiceOutputBuffer = new float[MaxVoices][];
        _voiceInputWritePos = new int[MaxVoices];
        _voiceOutputReadPos = new int[MaxVoices];
        _voiceFftBuffer = new Complex[MaxVoices][];
        _voiceLastInputPhase = new float[MaxVoices][];
        _voiceAccumulatedPhase = new float[MaxVoices][];
        _voiceSpectralEnvelope = new float[MaxVoices][];

        for (int v = 0; v < MaxVoices; v++)
        {
            _voiceInputBuffer[v] = new float[_fftSize * 2];
            _voiceOutputBuffer[v] = new float[_fftSize * 4];
            _voiceInputWritePos[v] = 0;
            _voiceOutputReadPos[v] = 0;
            _voiceFftBuffer[v] = new Complex[_fftSize];
            _voiceLastInputPhase[v] = new float[halfSize];
            _voiceAccumulatedPhase[v] = new float[halfSize];
            _voiceSpectralEnvelope[v] = new float[halfSize];
        }

        // Generate Hann window
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        _samplesUntilNextFrame = 0;
        _initialized = true;
    }

    /// <summary>
    /// Processes a buffer of audio samples.
    /// </summary>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        bool isStereo = channels >= 2;

        // Process samples
        for (int i = 0; i < count; i += channels)
        {
            // Get mono input (average of channels)
            float monoInput;
            if (isStereo)
            {
                monoInput = (sourceBuffer[i] + sourceBuffer[i + 1]) * 0.5f;
            }
            else
            {
                monoInput = sourceBuffer[i];
            }

            // Write to all voice input buffers
            for (int v = 0; v < MaxVoices; v++)
            {
                _voiceInputBuffer[v][_voiceInputWritePos[v]] = monoInput;
                _voiceInputWritePos[v] = (_voiceInputWritePos[v] + 1) % _voiceInputBuffer[v].Length;
            }

            _samplesUntilNextFrame--;

            // Process frame when hop size is reached
            if (_samplesUntilNextFrame <= 0)
            {
                _samplesUntilNextFrame = _hopSize;

                // Process each enabled voice
                for (int v = 0; v < MaxVoices; v++)
                {
                    if (_voiceEnabled[v] && MathF.Abs(_voiceIntervals[v]) > 0.001f)
                    {
                        // Get the effective interval (may be quantized to scale)
                        float effectiveInterval = _scaleAware
                            ? QuantizeIntervalToScale(60f, _voiceIntervals[v])
                            : _voiceIntervals[v];

                        float pitchRatio = MathF.Pow(2f, effectiveInterval / 12f);
                        ProcessVoiceFrame(v, pitchRatio);
                    }
                }
            }

            // Read from voice output buffers and mix
            float leftOut = 0f;
            float rightOut = 0f;

            for (int v = 0; v < MaxVoices; v++)
            {
                if (_voiceEnabled[v] && MathF.Abs(_voiceIntervals[v]) > 0.001f)
                {
                    float voiceSample = _voiceOutputBuffer[v][_voiceOutputReadPos[v]];
                    _voiceOutputBuffer[v][_voiceOutputReadPos[v]] = 0f;
                    _voiceOutputReadPos[v] = (_voiceOutputReadPos[v] + 1) % _voiceOutputBuffer[v].Length;

                    // Apply level
                    voiceSample *= _voiceLevels[v];

                    // Apply pan (constant power panning)
                    float pan = _voicePans[v];
                    float panAngle = (pan + 1f) * MathF.PI * 0.25f;
                    float leftGain = MathF.Cos(panAngle);
                    float rightGain = MathF.Sin(panAngle);

                    leftOut += voiceSample * leftGain;
                    rightOut += voiceSample * rightGain;
                }
                else
                {
                    // Still advance read position even if disabled
                    _voiceOutputReadPos[v] = (_voiceOutputReadPos[v] + 1) % _voiceOutputBuffer[v].Length;
                }
            }

            // Write to output buffer
            if (isStereo)
            {
                destBuffer[offset + i] = leftOut;
                destBuffer[offset + i + 1] = rightOut;
            }
            else
            {
                destBuffer[offset + i] = (leftOut + rightOut) * 0.5f;
            }
        }
    }

    /// <summary>
    /// Processes one phase vocoder frame for a single voice.
    /// </summary>
    private void ProcessVoiceFrame(int voice, float pitchRatio)
    {
        int halfSize = _fftSize / 2;
        float freqPerBin = (float)SampleRate / _fftSize;
        float expectedPhaseDiff = 2f * MathF.PI * _hopSize / _fftSize;

        // Copy windowed input to FFT buffer
        int readStart = (_voiceInputWritePos[voice] - _fftSize + _voiceInputBuffer[voice].Length) % _voiceInputBuffer[voice].Length;
        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _voiceInputBuffer[voice].Length;
            float windowedSample = _voiceInputBuffer[voice][readPos] * _analysisWindow[i];
            _voiceFftBuffer[voice][i] = new Complex(windowedSample, 0f);
        }

        // Forward FFT
        FFT(_voiceFftBuffer[voice], false);

        // Extract spectral envelope for formant preservation
        if (_formantPreserve > 0f)
        {
            ExtractSpectralEnvelope(voice);
        }

        // Analysis: Calculate magnitude and true frequency for each bin
        float[] magnitude = new float[halfSize + 1];
        float[] trueFreq = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            float real = _voiceFftBuffer[voice][k].Real;
            float imag = _voiceFftBuffer[voice][k].Imag;

            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            float phase = MathF.Atan2(imag, real);

            float phaseDiff = phase - _voiceLastInputPhase[voice][k];
            _voiceLastInputPhase[voice][k] = phase;

            phaseDiff -= k * expectedPhaseDiff;
            phaseDiff = WrapPhase(phaseDiff);

            float deviation = phaseDiff * _overlapFactor / (2f * MathF.PI);
            trueFreq[k] = k + deviation;
        }

        // Synthesis: Shift frequencies and accumulate phase
        float[] newMagnitude = new float[halfSize + 1];
        float[] newPhase = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            int targetBin = (int)MathF.Round(k * pitchRatio);

            if (targetBin >= 0 && targetBin <= halfSize)
            {
                newMagnitude[targetBin] += magnitude[k];

                float scaledFreq = trueFreq[k] * pitchRatio;
                float phaseDelta = scaledFreq * expectedPhaseDiff;
                _voiceAccumulatedPhase[voice][targetBin] += phaseDelta;
                _voiceAccumulatedPhase[voice][targetBin] = WrapPhase(_voiceAccumulatedPhase[voice][targetBin]);
                newPhase[targetBin] = _voiceAccumulatedPhase[voice][targetBin];
            }
        }

        // Apply formant correction if enabled
        if (_formantPreserve > 0f)
        {
            ApplyFormantCorrection(voice, newMagnitude, pitchRatio);
        }

        // Convert back to complex for inverse FFT
        for (int k = 0; k <= halfSize; k++)
        {
            float mag = newMagnitude[k];
            float ph = newPhase[k];
            _voiceFftBuffer[voice][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            if (k > 0 && k < halfSize)
            {
                _voiceFftBuffer[voice][_fftSize - k] = new Complex(mag * MathF.Cos(ph), -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_voiceFftBuffer[voice], true);

        // Overlap-add to output buffer
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_voiceOutputReadPos[voice] + i) % _voiceOutputBuffer[voice].Length;
            _voiceOutputBuffer[voice][outputPos] += _voiceFftBuffer[voice][i].Real * _analysisWindow[i] * normFactor;
        }
    }

    /// <summary>
    /// Extracts the spectral envelope using cepstral smoothing for formant preservation.
    /// </summary>
    private void ExtractSpectralEnvelope(int voice)
    {
        int halfSize = _fftSize / 2;

        float[] logMag = new float[_fftSize];
        for (int k = 0; k < _fftSize; k++)
        {
            float mag = MathF.Sqrt(_voiceFftBuffer[voice][k].Real * _voiceFftBuffer[voice][k].Real +
                                   _voiceFftBuffer[voice][k].Imag * _voiceFftBuffer[voice][k].Imag);
            logMag[k] = MathF.Log(mag + 1e-10f);
        }

        Complex[] cepstrum = new Complex[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            cepstrum[i] = new Complex(logMag[i], 0f);
        }

        FFT(cepstrum, false);

        int lifterCutoff = _envelopeOrder;
        for (int i = lifterCutoff; i < _fftSize - lifterCutoff; i++)
        {
            cepstrum[i] = new Complex(0f, 0f);
        }

        FFT(cepstrum, true);

        for (int k = 0; k <= halfSize; k++)
        {
            _voiceSpectralEnvelope[voice][k] = MathF.Exp(cepstrum[k].Real / _fftSize);
        }
    }

    /// <summary>
    /// Applies formant correction to preserve vocal character during pitch shift.
    /// </summary>
    private void ApplyFormantCorrection(int voice, float[] magnitude, float pitchRatio)
    {
        int halfSize = _fftSize / 2;
        float[] correctedMag = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            float sourcePos = k / pitchRatio;
            int sourceIdx = (int)sourcePos;
            float frac = sourcePos - sourceIdx;

            float sourceEnvelope = 1f;
            if (sourceIdx >= 0 && sourceIdx < halfSize)
            {
                sourceEnvelope = _voiceSpectralEnvelope[voice][sourceIdx];
                if (sourceIdx + 1 <= halfSize)
                {
                    sourceEnvelope = sourceEnvelope * (1f - frac) + _voiceSpectralEnvelope[voice][sourceIdx + 1] * frac;
                }
            }

            float targetEnvelope = _voiceSpectralEnvelope[voice][k];

            float correction = 1f;
            if (sourceEnvelope > 1e-10f)
            {
                correction = targetEnvelope / sourceEnvelope;
            }

            correction = 1f + (correction - 1f) * _formantPreserve;
            correction = Math.Clamp(correction, 0.1f, 10f);

            correctedMag[k] = magnitude[k] * correction;
        }

        Array.Copy(correctedMag, magnitude, halfSize + 1);
    }

    /// <summary>
    /// Wraps a phase value to the range [-PI, PI].
    /// </summary>
    private static float WrapPhase(float phase)
    {
        while (phase > MathF.PI) phase -= 2f * MathF.PI;
        while (phase < -MathF.PI) phase += 2f * MathF.PI;
        return phase;
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
    /// </summary>
    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int m = n >> 1;
            while (j >= m && m >= 1)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative FFT
        float direction = inverse ? 1f : -1f;
        for (int len = 2; len <= n; len <<= 1)
        {
            float theta = direction * 2f * MathF.PI / len;
            Complex wn = new Complex(MathF.Cos(theta), MathF.Sin(theta));

            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1f, 0f);
                int halfLen = len / 2;
                for (int k = 0; k < halfLen; k++)
                {
                    Complex t = w * data[i + k + halfLen];
                    Complex u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + halfLen] = u - t;
                    w = w * wn;
                }
            }
        }

        // Scale for inverse FFT
        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    /// <summary>
    /// Called when a parameter value changes.
    /// </summary>
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "voice1interval":
                _voiceIntervals[0] = Math.Clamp(value, -24f, 24f);
                break;
            case "voice2interval":
                _voiceIntervals[1] = Math.Clamp(value, -24f, 24f);
                break;
            case "voice3interval":
                _voiceIntervals[2] = Math.Clamp(value, -24f, 24f);
                break;
            case "voice4interval":
                _voiceIntervals[3] = Math.Clamp(value, -24f, 24f);
                break;
            case "voice1level":
                _voiceLevels[0] = Math.Clamp(value, 0f, 1f);
                break;
            case "voice2level":
                _voiceLevels[1] = Math.Clamp(value, 0f, 1f);
                break;
            case "voice3level":
                _voiceLevels[2] = Math.Clamp(value, 0f, 1f);
                break;
            case "voice4level":
                _voiceLevels[3] = Math.Clamp(value, 0f, 1f);
                break;
            case "voice1pan":
                _voicePans[0] = Math.Clamp(value, -1f, 1f);
                break;
            case "voice2pan":
                _voicePans[1] = Math.Clamp(value, -1f, 1f);
                break;
            case "voice3pan":
                _voicePans[2] = Math.Clamp(value, -1f, 1f);
                break;
            case "voice4pan":
                _voicePans[3] = Math.Clamp(value, -1f, 1f);
                break;
            case "voice1enabled":
                _voiceEnabled[0] = value > 0.5f;
                break;
            case "voice2enabled":
                _voiceEnabled[1] = value > 0.5f;
                break;
            case "voice3enabled":
                _voiceEnabled[2] = value > 0.5f;
                break;
            case "voice4enabled":
                _voiceEnabled[3] = value > 0.5f;
                break;
            case "key":
                _key = Math.Clamp((int)value, 0, 11);
                UpdateScaleNotes();
                break;
            case "scale":
                _scale = (HarmonizerScale)Math.Clamp((int)value, 0, 11);
                UpdateScaleNotes();
                break;
            case "scaleaware":
                _scaleAware = value > 0.5f;
                break;
            case "formantpreserve":
                _formantPreserve = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    #region Preset Factory Methods

    /// <summary>
    /// Creates a simple two-voice harmony preset (third and fifth above).
    /// </summary>
    public static Harmonizer CreateSimpleHarmony(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Simple Harmony")
        {
            Voice1Interval = 4f,  // Major third
            Voice1Level = 0.6f,
            Voice1Pan = -0.3f,
            Voice1Enabled = true,
            Voice2Interval = 7f,  // Perfect fifth
            Voice2Level = 0.5f,
            Voice2Pan = 0.3f,
            Voice2Enabled = true,
            Voice3Enabled = false,
            Voice4Enabled = false,
            ScaleAware = true,
            Scale = HarmonizerScale.Major,
            FormantPreserve = 0.7f
        };
        harmonizer.Mix = 0.5f;
        return harmonizer;
    }

    /// <summary>
    /// Creates a three-voice chord harmony preset.
    /// </summary>
    public static Harmonizer CreateTriadHarmony(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Triad Harmony")
        {
            Voice1Interval = 4f,   // Third
            Voice1Level = 0.5f,
            Voice1Pan = -0.4f,
            Voice1Enabled = true,
            Voice2Interval = 7f,   // Fifth
            Voice2Level = 0.5f,
            Voice2Pan = 0.4f,
            Voice2Enabled = true,
            Voice3Interval = 12f,  // Octave
            Voice3Level = 0.3f,
            Voice3Pan = 0f,
            Voice3Enabled = true,
            Voice4Enabled = false,
            ScaleAware = true,
            Scale = HarmonizerScale.Major,
            FormantPreserve = 0.6f
        };
        harmonizer.Mix = 0.5f;
        return harmonizer;
    }

    /// <summary>
    /// Creates a lower harmony preset (third and fifth below).
    /// </summary>
    public static Harmonizer CreateLowerHarmony(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Lower Harmony")
        {
            Voice1Interval = -3f,  // Minor third below
            Voice1Level = 0.6f,
            Voice1Pan = -0.2f,
            Voice1Enabled = true,
            Voice2Interval = -5f,  // Perfect fifth below (perfect fourth up inverted)
            Voice2Level = 0.5f,
            Voice2Pan = 0.2f,
            Voice2Enabled = true,
            Voice3Enabled = false,
            Voice4Enabled = false,
            ScaleAware = true,
            Scale = HarmonizerScale.Minor,
            FormantPreserve = 0.5f
        };
        harmonizer.Mix = 0.5f;
        return harmonizer;
    }

    /// <summary>
    /// Creates an octave doubler preset.
    /// </summary>
    public static Harmonizer CreateOctaveDoubler(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Octave Doubler")
        {
            Voice1Interval = 12f,  // Octave up
            Voice1Level = 0.4f,
            Voice1Pan = 0f,
            Voice1Enabled = true,
            Voice2Interval = -12f, // Octave down
            Voice2Level = 0.4f,
            Voice2Pan = 0f,
            Voice2Enabled = true,
            Voice3Enabled = false,
            Voice4Enabled = false,
            ScaleAware = false,
            FormantPreserve = 0.3f
        };
        harmonizer.Mix = 0.4f;
        return harmonizer;
    }

    /// <summary>
    /// Creates a wide stereo spread harmony preset.
    /// </summary>
    public static Harmonizer CreateStereoSpread(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Stereo Spread")
        {
            Voice1Interval = 7f,   // Fifth up
            Voice1Level = 0.5f,
            Voice1Pan = -0.8f,     // Hard left
            Voice1Enabled = true,
            Voice2Interval = 4f,   // Third up
            Voice2Level = 0.5f,
            Voice2Pan = 0.8f,      // Hard right
            Voice2Enabled = true,
            Voice3Interval = -5f,  // Fourth down
            Voice3Level = 0.4f,
            Voice3Pan = -0.5f,
            Voice3Enabled = true,
            Voice4Interval = -3f,  // Third down
            Voice4Level = 0.4f,
            Voice4Pan = 0.5f,
            Voice4Enabled = true,
            ScaleAware = true,
            Scale = HarmonizerScale.Major,
            FormantPreserve = 0.6f
        };
        harmonizer.Mix = 0.4f;
        return harmonizer;
    }

    /// <summary>
    /// Creates a barbershop quartet-style preset.
    /// </summary>
    public static Harmonizer CreateBarbershop(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Barbershop")
        {
            Voice1Interval = -3f,  // Third below
            Voice1Level = 0.6f,
            Voice1Pan = -0.4f,
            Voice1Enabled = true,
            Voice2Interval = -7f,  // Fifth below
            Voice2Level = 0.5f,
            Voice2Pan = 0.4f,
            Voice2Enabled = true,
            Voice3Interval = -12f, // Octave below (bass)
            Voice3Level = 0.4f,
            Voice3Pan = 0f,
            Voice3Enabled = true,
            Voice4Enabled = false,
            ScaleAware = true,
            Scale = HarmonizerScale.Major,
            FormantPreserve = 0.8f
        };
        harmonizer.Mix = 0.5f;
        return harmonizer;
    }

    /// <summary>
    /// Creates a chromatic detune effect (no scale quantization).
    /// </summary>
    public static Harmonizer CreateDetuneEffect(ISampleProvider source)
    {
        var harmonizer = new Harmonizer(source, "Detune")
        {
            Voice1Interval = 0.1f,  // Slight detune up
            Voice1Level = 0.7f,
            Voice1Pan = -0.5f,
            Voice1Enabled = true,
            Voice2Interval = -0.1f, // Slight detune down
            Voice2Level = 0.7f,
            Voice2Pan = 0.5f,
            Voice2Enabled = true,
            Voice3Enabled = false,
            Voice4Enabled = false,
            ScaleAware = false,
            FormantPreserve = 0f,
            Quality = HarmonizerQuality.Fast
        };
        harmonizer.Mix = 0.5f;
        return harmonizer;
    }

    #endregion

    #region Complex Number Struct

    /// <summary>
    /// Simple complex number struct for FFT operations.
    /// </summary>
    private readonly struct Complex
    {
        public readonly float Real;
        public readonly float Imag;

        public Complex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        public static Complex operator +(Complex a, Complex b)
        {
            return new Complex(a.Real + b.Real, a.Imag + b.Imag);
        }

        public static Complex operator -(Complex a, Complex b)
        {
            return new Complex(a.Real - b.Real, a.Imag - b.Imag);
        }

        public static Complex operator *(Complex a, Complex b)
        {
            return new Complex(
                a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real
            );
        }
    }

    #endregion
}
