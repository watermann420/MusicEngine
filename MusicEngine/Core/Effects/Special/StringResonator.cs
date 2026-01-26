//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Sympathetic string resonance modeling effect using comb filter bank.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// String tuning presets for the resonator.
/// </summary>
public enum StringTuning
{
    /// <summary>
    /// Standard guitar tuning (E2, A2, D3, G3, B3, E4).
    /// </summary>
    Guitar,

    /// <summary>
    /// Piano strings - covers a wide range of frequencies.
    /// </summary>
    Piano,

    /// <summary>
    /// Sitar sympathetic strings (13 strings).
    /// </summary>
    Sitar,

    /// <summary>
    /// Open D tuning (D2, A2, D3, F#3, A3, D4).
    /// </summary>
    OpenD,

    /// <summary>
    /// Drop D tuning (D2, A2, D3, G3, B3, E4).
    /// </summary>
    DropD,

    /// <summary>
    /// Custom user-defined tuning.
    /// </summary>
    Custom
}

/// <summary>
/// Sympathetic string resonance effect using comb filter bank implementation.
/// Models the resonance of strings vibrating in response to input audio.
/// </summary>
/// <remarks>
/// The effect works by:
/// 1. Using comb filters tuned to specific string frequencies
/// 2. Each comb filter has adjustable feedback (damping) and delay (pitch)
/// 3. The input signal excites the comb filters creating resonance
/// 4. Multiple resonant frequencies combine for rich harmonic content
/// </remarks>
public class StringResonator : EffectBase
{
    private const int MaxStrings = 24;
    private const float MaxDelaySeconds = 0.1f; // 10 Hz minimum

    // Comb filter state
    private readonly float[][] _delayLines;
    private readonly int[] _delayLengths;
    private readonly int[] _writePositions;
    private readonly float[] _stringFrequencies;
    private readonly float[] _stringGains;
    private int _activeStringCount;

    // Parameters
    private float _resonance = 0.5f;
    private float _damping = 0.3f;
    private float _brightness = 0.7f;
    private float _spread = 1.0f;
    private StringTuning _tuning = StringTuning.Guitar;

    // Internal state
    private bool _initialized;
    private int _maxDelaySamples;

    /// <summary>
    /// Creates a new string resonator effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public StringResonator(ISampleProvider source) : this(source, "String Resonator")
    {
    }

    /// <summary>
    /// Creates a new string resonator effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public StringResonator(ISampleProvider source, string name) : base(source, name)
    {
        _delayLines = new float[MaxStrings][];
        _delayLengths = new int[MaxStrings];
        _writePositions = new int[MaxStrings];
        _stringFrequencies = new float[MaxStrings];
        _stringGains = new float[MaxStrings];

        RegisterParameter("Resonance", 0.5f);
        RegisterParameter("Damping", 0.3f);
        RegisterParameter("Brightness", 0.7f);
        RegisterParameter("Spread", 1.0f);
        RegisterParameter("Mix", 0.5f);

        _initialized = false;
    }

    /// <summary>
    /// Gets or sets the resonance amount (0.0 - 1.0).
    /// Higher values produce stronger sympathetic vibration.
    /// </summary>
    public float Resonance
    {
        get => _resonance;
        set
        {
            _resonance = Math.Clamp(value, 0f, 1f);
            SetParameter("Resonance", _resonance);
        }
    }

    /// <summary>
    /// Gets or sets the damping amount (0.0 - 1.0).
    /// Higher values cause faster decay of resonance.
    /// </summary>
    public float Damping
    {
        get => _damping;
        set
        {
            _damping = Math.Clamp(value, 0f, 1f);
            SetParameter("Damping", _damping);
        }
    }

    /// <summary>
    /// Gets or sets the brightness (0.0 - 1.0).
    /// Controls high-frequency content in resonance.
    /// </summary>
    public float Brightness
    {
        get => _brightness;
        set
        {
            _brightness = Math.Clamp(value, 0f, 1f);
            SetParameter("Brightness", _brightness);
        }
    }

    /// <summary>
    /// Gets or sets the stereo spread (0.0 - 1.0).
    /// Distributes strings across the stereo field.
    /// </summary>
    public float Spread
    {
        get => _spread;
        set
        {
            _spread = Math.Clamp(value, 0f, 1f);
            SetParameter("Spread", _spread);
        }
    }

    /// <summary>
    /// Gets or sets the string tuning preset.
    /// </summary>
    public StringTuning Tuning
    {
        get => _tuning;
        set
        {
            if (_tuning != value)
            {
                _tuning = value;
                _initialized = false;
            }
        }
    }

    /// <summary>
    /// Gets or sets custom string frequencies (for Custom tuning).
    /// </summary>
    /// <param name="frequencies">Array of frequencies in Hz.</param>
    public void SetCustomTuning(float[] frequencies)
    {
        if (frequencies == null || frequencies.Length == 0)
            return;

        _tuning = StringTuning.Custom;
        _activeStringCount = Math.Min(frequencies.Length, MaxStrings);

        for (int i = 0; i < _activeStringCount; i++)
        {
            _stringFrequencies[i] = Math.Clamp(frequencies[i], 20f, 20000f);
        }

        _initialized = false;
    }

    /// <summary>
    /// Initializes the comb filter bank based on current tuning.
    /// </summary>
    private void Initialize()
    {
        int sampleRate = SampleRate;
        _maxDelaySamples = (int)(MaxDelaySeconds * sampleRate);

        // Set up string frequencies based on tuning
        switch (_tuning)
        {
            case StringTuning.Guitar:
                SetGuitarTuning();
                break;
            case StringTuning.Piano:
                SetPianoTuning();
                break;
            case StringTuning.Sitar:
                SetSitarTuning();
                break;
            case StringTuning.OpenD:
                SetOpenDTuning();
                break;
            case StringTuning.DropD:
                SetDropDTuning();
                break;
            case StringTuning.Custom:
                // Already set via SetCustomTuning
                break;
        }

        // Initialize delay lines for each string
        for (int i = 0; i < _activeStringCount; i++)
        {
            // Calculate delay length from frequency
            int delaySamples = (int)(sampleRate / _stringFrequencies[i]);
            delaySamples = Math.Clamp(delaySamples, 1, _maxDelaySamples);

            _delayLengths[i] = delaySamples;
            _delayLines[i] = new float[delaySamples];
            _writePositions[i] = 0;

            // Calculate gain based on string position (higher strings quieter)
            _stringGains[i] = 1.0f / MathF.Sqrt(i + 1);
        }

        _initialized = true;
    }

    /// <summary>
    /// Sets standard guitar tuning (E2, A2, D3, G3, B3, E4).
    /// </summary>
    private void SetGuitarTuning()
    {
        _stringFrequencies[0] = 82.41f;   // E2
        _stringFrequencies[1] = 110.00f;  // A2
        _stringFrequencies[2] = 146.83f;  // D3
        _stringFrequencies[3] = 196.00f;  // G3
        _stringFrequencies[4] = 246.94f;  // B3
        _stringFrequencies[5] = 329.63f;  // E4
        _activeStringCount = 6;
    }

    /// <summary>
    /// Sets piano tuning with representative strings across the range.
    /// </summary>
    private void SetPianoTuning()
    {
        // Representative piano strings from low to high
        float[] pianoFreqs = {
            27.50f,   // A0
            55.00f,   // A1
            110.00f,  // A2
            220.00f,  // A3
            261.63f,  // C4 (middle C)
            329.63f,  // E4
            440.00f,  // A4
            523.25f,  // C5
            659.25f,  // E5
            880.00f,  // A5
            1046.50f, // C6
            1318.51f, // E6
            1760.00f, // A6
            2093.00f, // C7
            2637.02f, // E7
            3520.00f  // A7
        };

        _activeStringCount = Math.Min(pianoFreqs.Length, MaxStrings);
        for (int i = 0; i < _activeStringCount; i++)
        {
            _stringFrequencies[i] = pianoFreqs[i];
        }
    }

    /// <summary>
    /// Sets sitar sympathetic string tuning.
    /// </summary>
    private void SetSitarTuning()
    {
        // Sitar sympathetic strings (taraf) - typical tuning
        float[] sitarFreqs = {
            130.81f,  // C3
            146.83f,  // D3
            164.81f,  // E3
            174.61f,  // F3
            196.00f,  // G3
            220.00f,  // A3
            246.94f,  // B3
            261.63f,  // C4
            293.66f,  // D4
            329.63f,  // E4
            349.23f,  // F4
            392.00f,  // G4
            440.00f   // A4
        };

        _activeStringCount = Math.Min(sitarFreqs.Length, MaxStrings);
        for (int i = 0; i < _activeStringCount; i++)
        {
            _stringFrequencies[i] = sitarFreqs[i];
        }
    }

    /// <summary>
    /// Sets open D tuning (D2, A2, D3, F#3, A3, D4).
    /// </summary>
    private void SetOpenDTuning()
    {
        _stringFrequencies[0] = 73.42f;   // D2
        _stringFrequencies[1] = 110.00f;  // A2
        _stringFrequencies[2] = 146.83f;  // D3
        _stringFrequencies[3] = 185.00f;  // F#3
        _stringFrequencies[4] = 220.00f;  // A3
        _stringFrequencies[5] = 293.66f;  // D4
        _activeStringCount = 6;
    }

    /// <summary>
    /// Sets drop D tuning (D2, A2, D3, G3, B3, E4).
    /// </summary>
    private void SetDropDTuning()
    {
        _stringFrequencies[0] = 73.42f;   // D2
        _stringFrequencies[1] = 110.00f;  // A2
        _stringFrequencies[2] = 146.83f;  // D3
        _stringFrequencies[3] = 196.00f;  // G3
        _stringFrequencies[4] = 246.94f;  // B3
        _stringFrequencies[5] = 329.63f;  // E4
        _activeStringCount = 6;
    }

    /// <inheritdoc/>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float feedback = 1.0f - _damping * 0.5f;
        float resonanceGain = _resonance * 0.8f;
        float lowpassCoeff = 0.3f + _brightness * 0.6f;

        for (int n = 0; n < count; n += channels)
        {
            // Get mono input for exciting the strings
            float inputL = sourceBuffer[n];
            float inputR = channels > 1 ? sourceBuffer[n + 1] : inputL;
            float monoInput = (inputL + inputR) * 0.5f;

            float resonanceL = 0f;
            float resonanceR = 0f;

            // Process each string (comb filter)
            for (int s = 0; s < _activeStringCount; s++)
            {
                var delayLine = _delayLines[s];
                int delayLen = _delayLengths[s];
                int writePos = _writePositions[s];

                // Read from delay line
                int readPos = (writePos + 1) % delayLen;
                float delayedSample = delayLine[readPos];

                // Simple lowpass filter for damping high frequencies
                float filtered = delayedSample * lowpassCoeff + delayLine[(readPos + 1) % delayLen] * (1f - lowpassCoeff);

                // Comb filter: input + delayed * feedback
                float output = monoInput * 0.1f + filtered * feedback;

                // Write to delay line
                delayLine[writePos] = output;
                _writePositions[s] = (writePos + 1) % delayLen;

                // Apply string gain and resonance
                float stringOutput = output * _stringGains[s] * resonanceGain;

                // Stereo spread: distribute strings across stereo field
                float pan = _spread * ((float)s / (_activeStringCount - 1) * 2f - 1f);
                float panL = MathF.Cos((pan + 1f) * MathF.PI * 0.25f);
                float panR = MathF.Sin((pan + 1f) * MathF.PI * 0.25f);

                resonanceL += stringOutput * panL;
                resonanceR += stringOutput * panR;
            }

            // Mix resonance with input
            destBuffer[offset + n] = inputL + resonanceL;
            if (channels > 1)
            {
                destBuffer[offset + n + 1] = inputR + resonanceR;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "resonance":
                _resonance = value;
                break;
            case "damping":
                _damping = value;
                break;
            case "brightness":
                _brightness = value;
                break;
            case "spread":
                _spread = value;
                break;
        }
    }

    /// <summary>
    /// Creates a guitar body resonance preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured StringResonator effect.</returns>
    public static StringResonator CreateGuitarBody(ISampleProvider source)
    {
        var effect = new StringResonator(source, "Guitar Body");
        effect.Tuning = StringTuning.Guitar;
        effect.Resonance = 0.6f;
        effect.Damping = 0.4f;
        effect.Brightness = 0.6f;
        effect.Spread = 0.7f;
        effect.Mix = 0.4f;
        return effect;
    }

    /// <summary>
    /// Creates a sitar drone preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured StringResonator effect.</returns>
    public static StringResonator CreateSitarDrone(ISampleProvider source)
    {
        var effect = new StringResonator(source, "Sitar Drone");
        effect.Tuning = StringTuning.Sitar;
        effect.Resonance = 0.8f;
        effect.Damping = 0.2f;
        effect.Brightness = 0.8f;
        effect.Spread = 0.9f;
        effect.Mix = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a piano strings resonance preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured StringResonator effect.</returns>
    public static StringResonator CreatePianoStrings(ISampleProvider source)
    {
        var effect = new StringResonator(source, "Piano Strings");
        effect.Tuning = StringTuning.Piano;
        effect.Resonance = 0.5f;
        effect.Damping = 0.35f;
        effect.Brightness = 0.65f;
        effect.Spread = 1.0f;
        effect.Mix = 0.35f;
        return effect;
    }

    /// <summary>
    /// Creates a subtle ambient resonance preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured StringResonator effect.</returns>
    public static StringResonator CreateAmbientResonance(ISampleProvider source)
    {
        var effect = new StringResonator(source, "Ambient Resonance");
        effect.Tuning = StringTuning.Piano;
        effect.Resonance = 0.3f;
        effect.Damping = 0.15f;
        effect.Brightness = 0.5f;
        effect.Spread = 1.0f;
        effect.Mix = 0.25f;
        return effect;
    }
}
