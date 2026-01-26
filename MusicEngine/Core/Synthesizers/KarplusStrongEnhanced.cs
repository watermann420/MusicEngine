// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Karplus-Strong string synthesis.

using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// String material types affecting timbre and decay.
/// </summary>
public enum StringMaterial
{
    /// <summary>
    /// Nylon strings - warm, mellow tone with moderate sustain.
    /// </summary>
    Nylon,

    /// <summary>
    /// Steel strings - bright, metallic tone with long sustain.
    /// </summary>
    Steel,

    /// <summary>
    /// Bass strings - deep, thumpy tone with wound wire character.
    /// </summary>
    Bass,

    /// <summary>
    /// Piano strings - bright with complex harmonic content.
    /// </summary>
    Piano,

    /// <summary>
    /// Harp strings - ethereal, bell-like tone.
    /// </summary>
    Harp
}

/// <summary>
/// Body type for resonance modeling.
/// </summary>
public enum BodyType
{
    /// <summary>
    /// No body resonance.
    /// </summary>
    None,

    /// <summary>
    /// Acoustic guitar body.
    /// </summary>
    AcousticGuitar,

    /// <summary>
    /// Electric guitar body (minimal resonance).
    /// </summary>
    ElectricGuitar,

    /// <summary>
    /// Grand piano soundboard.
    /// </summary>
    Piano,

    /// <summary>
    /// Harp soundboard.
    /// </summary>
    Harp,

    /// <summary>
    /// Custom body with user-defined resonances.
    /// </summary>
    Custom
}

/// <summary>
/// Enhanced Karplus-Strong physical modeling synthesizer.
/// Models plucked and struck strings with body resonance, pickup simulation,
/// and sympathetic string resonance.
/// </summary>
/// <remarks>
/// Features:
/// - Multiple string materials (nylon, steel, bass, piano, harp)
/// - Body resonance modeling with customizable resonant frequencies
/// - Pickup position simulation for electric guitar tones
/// - String stiffness for inharmonicity modeling
/// - Sympathetic string resonance
/// - Pluck position control
/// - Advanced damping and decay control
/// </remarks>
public class KarplusStrongEnhanced : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly KarplusStrongVoice[] _voices;
    private readonly Dictionary<int, int> _noteToVoice = new();
    private readonly object _lock = new();
    private readonly Random _random = new();

    private const int MaxVoices = 12;
    private const int MaxSympatheticStrings = 6;

    // Sympathetic string resonators
    private readonly SympatheticString[] _sympatheticStrings;
    private bool _sympatheticEnabled;

    // Body resonance
    private readonly BodyResonator _bodyResonator;

    /// <summary>
    /// Gets or sets the synth name.
    /// </summary>
    public string Name { get; set; } = "KarplusStrongEnhanced";

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the master volume (0.0 - 1.0).
    /// </summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>
    /// Gets or sets the string material type.
    /// </summary>
    public StringMaterial StringMaterial { get; set; } = StringMaterial.Steel;

    /// <summary>
    /// Gets or sets the body type for resonance.
    /// </summary>
    public BodyType BodyType
    {
        get => _bodyResonator.BodyType;
        set => _bodyResonator.BodyType = value;
    }

    /// <summary>
    /// Gets or sets the pluck position (0.0 = bridge, 1.0 = middle of string).
    /// Affects harmonic content - closer to bridge produces brighter sound.
    /// </summary>
    public float PluckPosition { get; set; } = 0.12f;

    /// <summary>
    /// Gets or sets the pickup position (0.0 = bridge, 1.0 = neck).
    /// Simulates electric guitar pickup placement.
    /// </summary>
    public float PickupPosition { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the damping factor (0.0 = long sustain, 1.0 = short/muted).
    /// </summary>
    public float Damping { get; set; } = 0.1f;

    /// <summary>
    /// Gets or sets the string stiffness for inharmonicity (0.0 - 1.0).
    /// Higher values produce more piano-like detuned overtones.
    /// </summary>
    public float Stiffness { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the brightness (high-frequency content, 0.0 - 1.0).
    /// </summary>
    public float Brightness { get; set; } = 0.7f;

    /// <summary>
    /// Gets or sets the body resonance amount (0.0 - 1.0).
    /// </summary>
    public float BodyResonance
    {
        get => _bodyResonator.ResonanceAmount;
        set => _bodyResonator.ResonanceAmount = value;
    }

    /// <summary>
    /// Gets or sets whether sympathetic string resonance is enabled.
    /// </summary>
    public bool SympatheticEnabled
    {
        get => _sympatheticEnabled;
        set => _sympatheticEnabled = value;
    }

    /// <summary>
    /// Gets or sets the sympathetic resonance amount (0.0 - 1.0).
    /// </summary>
    public float SympatheticAmount { get; set; } = 0.3f;

    /// <summary>
    /// Creates a new enhanced Karplus-Strong synthesizer.
    /// </summary>
    /// <param name="sampleRate">Sample rate (optional, defaults to Settings.SampleRate).</param>
    public KarplusStrongEnhanced(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize voices
        _voices = new KarplusStrongVoice[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices[i] = new KarplusStrongVoice(rate);
        }

        // Initialize sympathetic strings (standard guitar tuning by default)
        _sympatheticStrings = new SympatheticString[MaxSympatheticStrings];
        float[] defaultTuning = { 82.41f, 110f, 146.83f, 196f, 246.94f, 329.63f };
        for (int i = 0; i < MaxSympatheticStrings; i++)
        {
            _sympatheticStrings[i] = new SympatheticString(rate, defaultTuning[i]);
        }

        // Initialize body resonator
        _bodyResonator = new BodyResonator(rate);
    }

    /// <summary>
    /// Sets the sympathetic string tuning.
    /// </summary>
    /// <param name="frequencies">Array of string frequencies in Hz.</param>
    public void SetSympatheticTuning(float[] frequencies)
    {
        if (frequencies == null)
            return;

        int count = Math.Min(frequencies.Length, MaxSympatheticStrings);
        for (int i = 0; i < count; i++)
        {
            _sympatheticStrings[i].SetFrequency(frequencies[i]);
        }
    }

    /// <summary>
    /// Sets custom body resonance frequencies.
    /// </summary>
    /// <param name="frequencies">Array of resonant frequencies.</param>
    /// <param name="qFactors">Array of Q factors for each resonance.</param>
    public void SetCustomBodyResonances(float[] frequencies, float[] qFactors)
    {
        _bodyResonator.SetCustomResonances(frequencies, qFactors);
    }

    /// <inheritdoc/>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            // Check if note is already playing
            if (_noteToVoice.TryGetValue(note, out int existingVoice))
            {
                _voices[existingVoice].Trigger(note, velocity, GetStringParams());
                return;
            }

            // Find free voice
            int voiceIndex = FindFreeVoice();
            if (voiceIndex < 0)
                return;

            // Remove old mapping
            foreach (var kvp in _noteToVoice.Where(x => x.Value == voiceIndex).ToList())
            {
                _noteToVoice.Remove(kvp.Key);
            }

            _voices[voiceIndex].Trigger(note, velocity, GetStringParams());
            _noteToVoice[note] = voiceIndex;
        }
    }

    /// <inheritdoc/>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out int voiceIndex))
            {
                _voices[voiceIndex].Release();
                _noteToVoice.Remove(note);
            }
        }
    }

    /// <inheritdoc/>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Release();
            }
            _noteToVoice.Clear();
        }
    }

    /// <inheritdoc/>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "pluckposition":
            case "pluck":
                PluckPosition = Math.Clamp(value, 0f, 1f);
                break;
            case "pickupposition":
            case "pickup":
                PickupPosition = Math.Clamp(value, 0f, 1f);
                break;
            case "damping":
            case "damp":
                Damping = Math.Clamp(value, 0f, 1f);
                break;
            case "stiffness":
                Stiffness = Math.Clamp(value, 0f, 1f);
                break;
            case "brightness":
            case "tone":
                Brightness = Math.Clamp(value, 0f, 1f);
                break;
            case "bodyresonance":
            case "body":
                BodyResonance = Math.Clamp(value, 0f, 1f);
                break;
            case "sympatheticamount":
            case "sympathetic":
                SympatheticAmount = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    /// <inheritdoc/>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        Array.Clear(buffer, offset, count);

        int channels = _waveFormat.Channels;
        float[] monoBuffer = new float[count / channels];

        lock (_lock)
        {
            // Process each voice
            foreach (var voice in _voices)
            {
                if (!voice.IsActive)
                    continue;

                for (int i = 0; i < monoBuffer.Length; i++)
                {
                    monoBuffer[i] += voice.Process();
                }
            }

            // Process sympathetic strings if enabled
            if (_sympatheticEnabled && SympatheticAmount > 0)
            {
                for (int i = 0; i < monoBuffer.Length; i++)
                {
                    float sympatheticOut = 0f;
                    foreach (var sympatheticString in _sympatheticStrings)
                    {
                        sympatheticOut += sympatheticString.Process(monoBuffer[i]);
                    }
                    monoBuffer[i] += sympatheticOut * SympatheticAmount;
                }
            }

            // Apply body resonance
            if (BodyResonance > 0)
            {
                for (int i = 0; i < monoBuffer.Length; i++)
                {
                    monoBuffer[i] = _bodyResonator.Process(monoBuffer[i]);
                }
            }

            // Apply volume and convert to stereo
            for (int i = 0; i < monoBuffer.Length; i++)
            {
                float sample = monoBuffer[i] * Volume;

                // Soft clipping
                sample = MathF.Tanh(sample);

                for (int c = 0; c < channels; c++)
                {
                    buffer[offset + i * channels + c] = sample;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Gets current string parameters for voice configuration.
    /// </summary>
    private StringParams GetStringParams()
    {
        return new StringParams
        {
            Material = StringMaterial,
            PluckPosition = PluckPosition,
            PickupPosition = PickupPosition,
            Damping = Damping,
            Stiffness = Stiffness,
            Brightness = Brightness
        };
    }

    /// <summary>
    /// Finds a free voice or steals the oldest.
    /// </summary>
    private int FindFreeVoice()
    {
        // Look for inactive voice
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].IsActive)
                return i;
        }

        // Steal oldest voice
        int oldest = 0;
        var oldestTime = _voices[0].TriggerTime;
        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].TriggerTime < oldestTime)
            {
                oldest = i;
                oldestTime = _voices[i].TriggerTime;
            }
        }

        return oldest;
    }

    /// <summary>
    /// Creates an acoustic guitar preset.
    /// </summary>
    public static KarplusStrongEnhanced CreateAcousticGuitar(int? sampleRate = null)
    {
        var synth = new KarplusStrongEnhanced(sampleRate)
        {
            Name = "Acoustic Guitar",
            StringMaterial = StringMaterial.Steel,
            BodyType = BodyType.AcousticGuitar,
            PluckPosition = 0.13f,
            Damping = 0.08f,
            Stiffness = 0.01f,
            Brightness = 0.65f,
            BodyResonance = 0.6f,
            SympatheticEnabled = true,
            SympatheticAmount = 0.15f
        };
        return synth;
    }

    /// <summary>
    /// Creates an electric guitar preset.
    /// </summary>
    public static KarplusStrongEnhanced CreateElectricGuitar(int? sampleRate = null)
    {
        var synth = new KarplusStrongEnhanced(sampleRate)
        {
            Name = "Electric Guitar",
            StringMaterial = StringMaterial.Steel,
            BodyType = BodyType.ElectricGuitar,
            PluckPosition = 0.1f,
            PickupPosition = 0.25f,
            Damping = 0.05f,
            Stiffness = 0.02f,
            Brightness = 0.8f,
            BodyResonance = 0.1f,
            SympatheticEnabled = false
        };
        return synth;
    }

    /// <summary>
    /// Creates a classical guitar (nylon) preset.
    /// </summary>
    public static KarplusStrongEnhanced CreateClassicalGuitar(int? sampleRate = null)
    {
        var synth = new KarplusStrongEnhanced(sampleRate)
        {
            Name = "Classical Guitar",
            StringMaterial = StringMaterial.Nylon,
            BodyType = BodyType.AcousticGuitar,
            PluckPosition = 0.15f,
            Damping = 0.12f,
            Stiffness = 0.005f,
            Brightness = 0.5f,
            BodyResonance = 0.7f,
            SympatheticEnabled = true,
            SympatheticAmount = 0.2f
        };
        return synth;
    }

    /// <summary>
    /// Creates an electric bass preset.
    /// </summary>
    public static KarplusStrongEnhanced CreateElectricBass(int? sampleRate = null)
    {
        var synth = new KarplusStrongEnhanced(sampleRate)
        {
            Name = "Electric Bass",
            StringMaterial = StringMaterial.Bass,
            BodyType = BodyType.ElectricGuitar,
            PluckPosition = 0.08f,
            PickupPosition = 0.2f,
            Damping = 0.15f,
            Stiffness = 0.03f,
            Brightness = 0.4f,
            BodyResonance = 0.05f,
            SympatheticEnabled = false
        };
        synth.SetSympatheticTuning(new[] { 41.2f, 55f, 73.42f, 98f }); // E1, A1, D2, G2
        return synth;
    }

    /// <summary>
    /// Creates a harp preset.
    /// </summary>
    public static KarplusStrongEnhanced CreateHarp(int? sampleRate = null)
    {
        var synth = new KarplusStrongEnhanced(sampleRate)
        {
            Name = "Harp",
            StringMaterial = StringMaterial.Harp,
            BodyType = BodyType.Harp,
            PluckPosition = 0.2f,
            Damping = 0.05f,
            Stiffness = 0.015f,
            Brightness = 0.75f,
            BodyResonance = 0.8f,
            SympatheticEnabled = true,
            SympatheticAmount = 0.4f
        };
        return synth;
    }

    /// <summary>
    /// Creates a piano-like preset (struck strings).
    /// </summary>
    public static KarplusStrongEnhanced CreatePiano(int? sampleRate = null)
    {
        var synth = new KarplusStrongEnhanced(sampleRate)
        {
            Name = "Plucked Piano",
            StringMaterial = StringMaterial.Piano,
            BodyType = BodyType.Piano,
            PluckPosition = 0.1f,
            Damping = 0.02f,
            Stiffness = 0.08f, // High stiffness for piano-like inharmonicity
            Brightness = 0.85f,
            BodyResonance = 0.5f,
            SympatheticEnabled = true,
            SympatheticAmount = 0.25f
        };
        return synth;
    }

    #region Internal Classes

    /// <summary>
    /// Parameters for string configuration.
    /// </summary>
    private struct StringParams
    {
        public StringMaterial Material;
        public float PluckPosition;
        public float PickupPosition;
        public float Damping;
        public float Stiffness;
        public float Brightness;
    }

    /// <summary>
    /// Single Karplus-Strong voice with enhancements.
    /// </summary>
    private class KarplusStrongVoice
    {
        private readonly int _sampleRate;
        private readonly Random _random = new();

        // Delay line
        private float[] _delayLine = null!;
        private int _delayLength;
        private int _writeIndex;

        // Filters
        private float _lpState;
        private float _apState;

        // State
        private float _gain;
        private float _dampingCoeff;
        private float _brightnessCoeff;
        private float _pickupCoeff;

        public bool IsActive { get; private set; }
        public DateTime TriggerTime { get; private set; }

        public KarplusStrongVoice(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        public void Trigger(int note, int velocity, StringParams parameters)
        {
            // Calculate frequency
            float frequency = 440f * MathF.Pow(2f, (note - 69f) / 12f);

            // Apply stiffness (inharmonicity) - raises the effective frequency slightly
            float stiffnessOffset = parameters.Stiffness * 0.02f * frequency / 440f;
            frequency *= 1f + stiffnessOffset;

            // Calculate delay length
            _delayLength = (int)(_sampleRate / frequency);
            _delayLength = Math.Max(_delayLength, 2);

            // Allocate delay line if needed
            if (_delayLine == null || _delayLine.Length < _delayLength)
            {
                _delayLine = new float[_delayLength];
            }

            // Initialize delay line with noise burst (pluck excitation)
            InitializeExcitation(parameters.PluckPosition, parameters.Material);

            // Set parameters
            _gain = velocity / 127f;
            _dampingCoeff = GetDampingCoeff(parameters.Material, parameters.Damping);
            _brightnessCoeff = 0.3f + parameters.Brightness * 0.65f;
            _pickupCoeff = parameters.PickupPosition;

            _writeIndex = 0;
            _lpState = 0f;
            _apState = 0f;

            IsActive = true;
            TriggerTime = DateTime.Now;
        }

        public void Release()
        {
            // Start rapid damping
            _dampingCoeff *= 0.95f;
        }

        public float Process()
        {
            if (!IsActive)
                return 0f;

            // Read from delay line (with pickup position interpolation)
            int pickupOffset = (int)(_pickupCoeff * _delayLength * 0.5f);
            int readIndex = (_writeIndex + pickupOffset) % _delayLength;
            float output = _delayLine[readIndex];

            // Read next sample for interpolation
            int nextIndex = (readIndex + 1) % _delayLength;
            float nextSample = _delayLine[nextIndex];

            // Pickup simulation: blend two positions for comb-filtering effect
            output = output * 0.7f + nextSample * 0.3f;

            // Apply lowpass filter (string damping)
            _lpState = _lpState + _brightnessCoeff * (output - _lpState);

            // Apply damping
            float filtered = _lpState * _dampingCoeff;

            // All-pass filter for fractional delay (improves tuning)
            float apOut = _apState + 0.5f * (filtered - _apState);
            _apState = filtered;

            // Write back to delay line
            _delayLine[_writeIndex] = apOut;
            _writeIndex = (_writeIndex + 1) % _delayLength;

            // Check if sound has decayed
            if (MathF.Abs(apOut) < 0.0001f && MathF.Abs(_lpState) < 0.0001f)
            {
                IsActive = false;
            }

            return output * _gain;
        }

        private void InitializeExcitation(float pluckPosition, StringMaterial material)
        {
            // Create initial displacement based on pluck position
            int pluckPoint = (int)(pluckPosition * _delayLength);
            pluckPoint = Math.Clamp(pluckPoint, 1, _delayLength - 2);

            // Fill with shaped noise
            for (int i = 0; i < _delayLength; i++)
            {
                float noise = (float)(_random.NextDouble() * 2 - 1);

                // Shape based on pluck position (triangular displacement)
                float position = (float)i / _delayLength;
                float envelope;
                if (i < pluckPoint)
                {
                    envelope = (float)i / pluckPoint;
                }
                else
                {
                    envelope = (float)(_delayLength - i) / (_delayLength - pluckPoint);
                }

                // Add material character
                float materialMod = GetMaterialExcitationMod(material);
                _delayLine[i] = noise * envelope * materialMod;
            }
        }

        private float GetDampingCoeff(StringMaterial material, float damping)
        {
            float baseDamping = material switch
            {
                StringMaterial.Nylon => 0.994f,
                StringMaterial.Steel => 0.998f,
                StringMaterial.Bass => 0.997f,
                StringMaterial.Piano => 0.9995f,
                StringMaterial.Harp => 0.999f,
                _ => 0.998f
            };

            return baseDamping - damping * 0.01f;
        }

        private float GetMaterialExcitationMod(StringMaterial material)
        {
            return material switch
            {
                StringMaterial.Nylon => 0.8f,
                StringMaterial.Steel => 1.0f,
                StringMaterial.Bass => 0.6f,
                StringMaterial.Piano => 1.2f,
                StringMaterial.Harp => 0.9f,
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Sympathetic string resonator using comb filter.
    /// </summary>
    private class SympatheticString
    {
        private readonly int _sampleRate;
        private float[] _delayLine = null!;
        private int _delayLength;
        private int _writeIndex;
        private float _feedback;
        private float _lpState;

        public SympatheticString(int sampleRate, float frequency)
        {
            _sampleRate = sampleRate;
            _feedback = 0.95f;
            SetFrequency(frequency);
        }

        public void SetFrequency(float frequency)
        {
            _delayLength = Math.Max(1, (int)(_sampleRate / frequency));
            _delayLine = new float[_delayLength];
            _writeIndex = 0;
        }

        public float Process(float input)
        {
            // Read from delay line
            int readIndex = (_writeIndex + 1) % _delayLength;
            float delayed = _delayLine[readIndex];

            // Lowpass for damping
            _lpState = _lpState * 0.7f + delayed * 0.3f;

            // Comb filter with input excitation
            float output = _lpState * _feedback + input * 0.05f;

            // Write to delay
            _delayLine[_writeIndex] = output;
            _writeIndex = (_writeIndex + 1) % _delayLength;

            return delayed;
        }
    }

    /// <summary>
    /// Body resonance modeling using parallel bandpass filters.
    /// </summary>
    private class BodyResonator
    {
        private readonly int _sampleRate;
        private BodyResonanceFilter[] _resonances = null!;
        private int _resonanceCount;
        private BodyType _bodyType;
        private float _resonanceAmount = 0.5f;

        public BodyType BodyType
        {
            get => _bodyType;
            set
            {
                if (_bodyType != value)
                {
                    _bodyType = value;
                    InitializeResonances();
                }
            }
        }

        public float ResonanceAmount
        {
            get => _resonanceAmount;
            set => _resonanceAmount = Math.Clamp(value, 0f, 1f);
        }

        public BodyResonator(int sampleRate)
        {
            _sampleRate = sampleRate;
            _bodyType = BodyType.None;
            InitializeResonances();
        }

        public void SetCustomResonances(float[] frequencies, float[] qFactors)
        {
            if (frequencies == null || qFactors == null)
                return;

            _bodyType = BodyType.Custom;
            _resonanceCount = Math.Min(frequencies.Length, qFactors.Length);
            _resonanceCount = Math.Min(_resonanceCount, 8);

            _resonances = new BodyResonanceFilter[_resonanceCount];
            for (int i = 0; i < _resonanceCount; i++)
            {
                _resonances[i] = new BodyResonanceFilter();
                _resonances[i].SetFrequency(frequencies[i], qFactors[i], _sampleRate);
            }
        }

        private void InitializeResonances()
        {
            switch (_bodyType)
            {
                case BodyType.AcousticGuitar:
                    SetupGuitarBody();
                    break;
                case BodyType.ElectricGuitar:
                    SetupElectricBody();
                    break;
                case BodyType.Piano:
                    SetupPianoBody();
                    break;
                case BodyType.Harp:
                    SetupHarpBody();
                    break;
                default:
                    _resonances = Array.Empty<BodyResonanceFilter>();
                    _resonanceCount = 0;
                    break;
            }
        }

        private void SetupGuitarBody()
        {
            float[] freqs = { 95f, 185f, 350f, 600f, 1200f };
            float[] qs = { 8f, 6f, 5f, 4f, 3f };
            SetCustomResonances(freqs, qs);
        }

        private void SetupElectricBody()
        {
            float[] freqs = { 250f, 1000f };
            float[] qs = { 2f, 2f };
            SetCustomResonances(freqs, qs);
        }

        private void SetupPianoBody()
        {
            float[] freqs = { 100f, 200f, 400f, 800f, 2000f, 4000f };
            float[] qs = { 10f, 8f, 6f, 5f, 4f, 3f };
            SetCustomResonances(freqs, qs);
        }

        private void SetupHarpBody()
        {
            float[] freqs = { 80f, 160f, 320f, 800f, 1600f };
            float[] qs = { 12f, 10f, 8f, 6f, 4f };
            SetCustomResonances(freqs, qs);
        }

        public float Process(float input)
        {
            if (_resonanceCount == 0 || _resonanceAmount <= 0)
                return input;

            float resonanceOut = 0f;
            for (int i = 0; i < _resonanceCount; i++)
            {
                resonanceOut += _resonances[i].Process(input);
            }

            resonanceOut /= _resonanceCount;
            return input * (1f - _resonanceAmount * 0.5f) + resonanceOut * _resonanceAmount;
        }
    }

    /// <summary>
    /// Single body resonance (bandpass filter).
    /// </summary>
    private class BodyResonanceFilter
    {
        private float _a1, _a2, _b0, _b2;
        private float _x1, _x2, _y1, _y2;

        public void SetFrequency(float freq, float q, int sampleRate)
        {
            float w0 = 2f * MathF.PI * freq / sampleRate;
            float alpha = MathF.Sin(w0) / (2f * q);

            float cosW0 = MathF.Cos(w0);
            float a0 = 1f + alpha;

            _b0 = alpha / a0;
            _b2 = -alpha / a0;
            _a1 = -2f * cosW0 / a0;
            _a2 = (1f - alpha) / a0;
        }

        public float Process(float input)
        {
            float output = _b0 * input + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

            _x2 = _x1;
            _x1 = input;
            _y2 = _y1;
            _y1 = output;

            return output;
        }
    }

    #endregion
}
