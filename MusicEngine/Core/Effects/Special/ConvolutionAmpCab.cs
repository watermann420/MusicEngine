// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Convolution reverb processor.

using System;
using System.IO;
using System.Numerics;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Amp simulation type presets.
/// </summary>
public enum AmpType
{
    /// <summary>
    /// Clean amp with no distortion.
    /// </summary>
    Clean,

    /// <summary>
    /// Warm tube-style crunch.
    /// </summary>
    Crunch,

    /// <summary>
    /// British-style high gain.
    /// </summary>
    British,

    /// <summary>
    /// American-style high gain.
    /// </summary>
    American,

    /// <summary>
    /// Modern high-gain metal tone.
    /// </summary>
    Metal,

    /// <summary>
    /// Bass amp simulation.
    /// </summary>
    Bass
}

/// <summary>
/// Convolution-based amp/cabinet simulator with IR loading and amp simulation.
/// </summary>
/// <remarks>
/// Combines pre-amp drive/tone shaping with convolution-based cabinet impulse response.
/// Supports loading multiple IR files (WAV, AIFF) and blending between them.
/// Includes tube-style saturation, tone stack, and presence controls.
/// </remarks>
public class ConvolutionAmpCab : EffectBase
{
    // FFT settings
    private int _fftSize = 4096;
    private int _hopSize;

    // IR slots (up to 4)
    private readonly ImpulseResponseSlot[] _irSlots;
    private const int MaxIrSlots = 4;

    // Input/output buffers
    private float[][] _inputBuffer = Array.Empty<float[]>();
    private float[][] _outputBuffer = Array.Empty<float[]>();
    private int _inputWritePos;
    private int _outputReadPos;

    // FFT working arrays
    private Complex[] _fftInput = Array.Empty<Complex>();
    private Complex[] _fftOutput = Array.Empty<Complex>();

    // Amp simulation state
    private float[] _preFilterState;
    private float[] _toneStackState;
    private float[] _presenceState;
    private float _dcBlockState;

    // Parameters
    private float _drive = 0.5f;
    private float _bass = 0.5f;
    private float _mid = 0.5f;
    private float _treble = 0.5f;
    private float _presence = 0.5f;
    private float _outputLevel = 0.5f;
    private AmpType _ampType = AmpType.Crunch;
    private bool _ampBypass;

    /// <summary>
    /// Drive amount (0-1). Controls pre-amp saturation.
    /// </summary>
    public float Drive
    {
        get => _drive;
        set => _drive = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Bass control (0-1).
    /// </summary>
    public float Bass
    {
        get => _bass;
        set => _bass = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Mid control (0-1).
    /// </summary>
    public float Mid
    {
        get => _mid;
        set => _mid = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Treble control (0-1).
    /// </summary>
    public float Treble
    {
        get => _treble;
        set => _treble = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Presence control (0-1). High-frequency emphasis after power amp.
    /// </summary>
    public float Presence
    {
        get => _presence;
        set => _presence = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Output level (0-1).
    /// </summary>
    public float OutputLevel
    {
        get => _outputLevel;
        set => _outputLevel = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Amp type preset.
    /// </summary>
    public AmpType AmpType
    {
        get => _ampType;
        set => _ampType = value;
    }

    /// <summary>
    /// Bypasses the amp simulation (IR only).
    /// </summary>
    public bool AmpBypass
    {
        get => _ampBypass;
        set => _ampBypass = value;
    }

    /// <summary>
    /// Number of IR slots currently loaded.
    /// </summary>
    public int LoadedIrCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxIrSlots; i++)
            {
                if (_irSlots[i].IsLoaded) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Creates a new convolution amp/cab effect.
    /// </summary>
    public ConvolutionAmpCab(ISampleProvider source) : base(source, "Amp/Cab")
    {
        _hopSize = _fftSize / 2;

        // Initialize IR slots
        _irSlots = new ImpulseResponseSlot[MaxIrSlots];
        for (int i = 0; i < MaxIrSlots; i++)
        {
            _irSlots[i] = new ImpulseResponseSlot();
        }

        // Initialize filter states
        _preFilterState = new float[Channels * 2];
        _toneStackState = new float[Channels * 6];
        _presenceState = new float[Channels * 2];

        InitializeBuffers();

        // Generate default cabinet IR
        GenerateDefaultCabinet();

        // Register parameters
        RegisterParameter("drive", 0.5f);
        RegisterParameter("bass", 0.5f);
        RegisterParameter("mid", 0.5f);
        RegisterParameter("treble", 0.5f);
        RegisterParameter("presence", 0.5f);
        RegisterParameter("output", 0.5f);
        RegisterParameter("mix", 1.0f);
    }

    private void InitializeBuffers()
    {
        int channels = Channels;

        _inputBuffer = new float[channels][];
        _outputBuffer = new float[channels][];

        for (int c = 0; c < channels; c++)
        {
            _inputBuffer[c] = new float[_fftSize];
            _outputBuffer[c] = new float[_fftSize * 2];
        }

        _fftInput = new Complex[_fftSize];
        _fftOutput = new Complex[_fftSize];

        _inputWritePos = 0;
        _outputReadPos = 0;
    }

    /// <summary>
    /// Loads an IR file into a slot.
    /// </summary>
    /// <param name="slot">Slot index (0-3).</param>
    /// <param name="path">Path to WAV or AIFF file.</param>
    /// <param name="gain">Gain multiplier for this IR.</param>
    public void LoadIR(int slot, string path, float gain = 1.0f)
    {
        if (slot < 0 || slot >= MaxIrSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot must be 0-{MaxIrSlots - 1}");

        if (!File.Exists(path))
            throw new FileNotFoundException($"IR file not found: {path}");

        using var reader = new AudioFileReader(path);

        int irChannels = reader.WaveFormat.Channels;
        int totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
        int samplesPerChannel = totalSamples / irChannels;

        var buffer = new float[totalSamples];
        reader.Read(buffer, 0, totalSamples);

        // Deinterleave and get mono/stereo IR
        var ir = new float[Math.Max(irChannels, Channels)][];
        for (int c = 0; c < ir.Length; c++)
        {
            ir[c] = new float[samplesPerChannel];
        }

        for (int i = 0; i < samplesPerChannel; i++)
        {
            for (int c = 0; c < irChannels; c++)
            {
                ir[c][i] = buffer[i * irChannels + c];
            }
        }

        // Duplicate mono to stereo if needed
        if (irChannels == 1 && Channels == 2)
        {
            ir[1] = (float[])ir[0].Clone();
        }

        // Resample if needed
        if (reader.WaveFormat.SampleRate != SampleRate)
        {
            for (int c = 0; c < ir.Length; c++)
            {
                ir[c] = ResampleIR(ir[c], reader.WaveFormat.SampleRate, SampleRate);
            }
        }

        // Update FFT size if needed
        int requiredFftSize = 1;
        while (requiredFftSize < ir[0].Length + _hopSize)
        {
            requiredFftSize *= 2;
        }

        if (requiredFftSize > _fftSize)
        {
            _fftSize = requiredFftSize;
            _hopSize = _fftSize / 2;
            InitializeBuffers();
        }

        // Store in slot
        _irSlots[slot].Load(ir, _fftSize, Channels, gain, Path.GetFileName(path));
    }

    /// <summary>
    /// Loads IR from raw sample data.
    /// </summary>
    public void LoadIR(int slot, float[][] ir, float gain = 1.0f, string name = "Custom")
    {
        if (slot < 0 || slot >= MaxIrSlots)
            throw new ArgumentOutOfRangeException(nameof(slot));

        int requiredFftSize = 1;
        while (requiredFftSize < ir[0].Length + _hopSize)
        {
            requiredFftSize *= 2;
        }

        if (requiredFftSize > _fftSize)
        {
            _fftSize = requiredFftSize;
            _hopSize = _fftSize / 2;
            InitializeBuffers();
        }

        _irSlots[slot].Load(ir, _fftSize, Channels, gain, name);
    }

    /// <summary>
    /// Clears an IR slot.
    /// </summary>
    public void ClearIR(int slot)
    {
        if (slot < 0 || slot >= MaxIrSlots) return;
        _irSlots[slot].Clear();
    }

    /// <summary>
    /// Sets the gain for an IR slot.
    /// </summary>
    public void SetIRGain(int slot, float gain)
    {
        if (slot < 0 || slot >= MaxIrSlots) return;
        _irSlots[slot].Gain = Math.Clamp(gain, 0f, 2f);
    }

    /// <summary>
    /// Gets information about an IR slot.
    /// </summary>
    public (bool isLoaded, string name, float gain) GetIRInfo(int slot)
    {
        if (slot < 0 || slot >= MaxIrSlots)
            return (false, string.Empty, 0f);

        return (_irSlots[slot].IsLoaded, _irSlots[slot].Name, _irSlots[slot].Gain);
    }

    private void GenerateDefaultCabinet()
    {
        // Generate a simple 4x12 cabinet-style IR
        int irLength = (int)(SampleRate * 0.05); // 50ms
        var ir = new float[Channels][];

        for (int c = 0; c < Channels; c++)
        {
            ir[c] = new float[irLength];
        }

        var random = new Random(42);

        for (int i = 0; i < irLength; i++)
        {
            float t = (float)i / SampleRate;
            float decay = MathF.Exp(-40f * t);

            // Low-pass filter character (cabinet roll-off)
            float lpFactor = 1f / (1f + t * 50000f);

            for (int c = 0; c < Channels; c++)
            {
                float noise = (float)(random.NextDouble() * 2 - 1);
                ir[c][i] = noise * decay * lpFactor * 0.8f;
            }

            // Early reflections from cabinet
            if (i == (int)(0.0005f * SampleRate))
            {
                for (int c = 0; c < Channels; c++)
                    ir[c][i] += 0.5f;
            }
            if (i == (int)(0.001f * SampleRate))
            {
                for (int c = 0; c < Channels; c++)
                    ir[c][i] += 0.3f * (c == 0 ? 1 : -1);
            }
        }

        LoadIR(0, ir, 1.0f, "Default 4x12");
    }

    private float[] ResampleIR(float[] input, int inputRate, int outputRate)
    {
        double ratio = (double)outputRate / inputRate;
        int outputLength = (int)(input.Length * ratio);
        var output = new float[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            double srcPos = i / ratio;
            int srcIndex = (int)srcPos;
            double frac = srcPos - srcIndex;

            if (srcIndex + 1 < input.Length)
            {
                output[i] = (float)(input[srcIndex] * (1 - frac) + input[srcIndex + 1] * frac);
            }
            else if (srcIndex < input.Length)
            {
                output[i] = input[srcIndex];
            }
        }

        return output;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        for (int i = 0; i < count; i += channels)
        {
            for (int c = 0; c < channels; c++)
            {
                float sample = sourceBuffer[i + c];

                // Apply amp simulation (if not bypassed)
                if (!_ampBypass)
                {
                    sample = ProcessAmpStage(sample, c);
                }

                // Write to input buffer
                _inputBuffer[c][_inputWritePos] = sample;

                // Read from output buffer
                float output = _outputBuffer[c][_outputReadPos];
                _outputBuffer[c][_outputReadPos] = 0;

                // Apply output level
                output *= _outputLevel * 2f;

                destBuffer[offset + i + c] = output;
            }

            _inputWritePos++;
            _outputReadPos++;

            if (_inputWritePos >= _hopSize)
            {
                ProcessConvolutionBlock();
                _inputWritePos = 0;
            }

            if (_outputReadPos >= _hopSize)
            {
                _outputReadPos = 0;
            }
        }
    }

    private float ProcessAmpStage(float sample, int channel)
    {
        // Get amp characteristics based on type
        var (preGain, saturation, bassBoost, midScoop, trebleBoost) = GetAmpCharacteristics();

        // Pre-gain stage
        sample *= preGain * (_drive * 4f + 0.5f);

        // High-pass filter (DC blocking / tightness)
        float hpFreq = 80f + _bass * 60f; // 80-140 Hz
        float hpCoeff = 1f - MathF.Exp(-2f * MathF.PI * hpFreq / SampleRate);
        _dcBlockState = _dcBlockState * (1f - hpCoeff) + sample * hpCoeff;
        sample = sample - _dcBlockState;

        // Tube saturation
        sample = ApplySaturation(sample, saturation);

        // Tone stack
        sample = ApplyToneStack(sample, channel, bassBoost, midScoop, trebleBoost);

        // Presence (high shelf)
        sample = ApplyPresence(sample, channel);

        // Power amp saturation (subtle)
        sample = MathF.Tanh(sample * 0.5f) * 2f;

        return sample;
    }

    private (float preGain, float saturation, float bassBoost, float midScoop, float trebleBoost) GetAmpCharacteristics()
    {
        return _ampType switch
        {
            AmpType.Clean => (1.0f, 0.2f, 0.3f, 0.0f, 0.2f),
            AmpType.Crunch => (1.5f, 0.5f, 0.4f, 0.1f, 0.4f),
            AmpType.British => (2.0f, 0.7f, 0.5f, 0.2f, 0.6f),
            AmpType.American => (2.0f, 0.6f, 0.6f, 0.3f, 0.5f),
            AmpType.Metal => (3.0f, 0.9f, 0.7f, 0.4f, 0.7f),
            AmpType.Bass => (1.5f, 0.4f, 0.8f, -0.2f, -0.2f),
            _ => (1.5f, 0.5f, 0.4f, 0.1f, 0.4f)
        };
    }

    private float ApplySaturation(float sample, float amount)
    {
        // Asymmetric tube-style saturation
        float drive = 1f + amount * 10f;
        sample *= drive;

        // Soft clipping with asymmetry
        if (sample > 0)
        {
            sample = 1f - MathF.Exp(-sample);
        }
        else
        {
            sample = -1f + MathF.Exp(sample * 0.8f); // Slight asymmetry
        }

        return sample / drive * 2f;
    }

    private float ApplyToneStack(float sample, int channel, float bassBoost, float midScoop, float trebleBoost)
    {
        int baseIdx = channel * 6;

        // Bass control (low shelf around 100Hz)
        float bassFreq = 100f;
        float bassGain = (_bass * 2f - 1f + bassBoost) * 12f; // +/- 12dB
        float bassCoeff = MathF.Exp(-2f * MathF.PI * bassFreq / SampleRate);

        float bassLp = _toneStackState[baseIdx];
        bassLp = bassLp * bassCoeff + sample * (1f - bassCoeff);
        _toneStackState[baseIdx] = bassLp;

        float bassShelf = sample + (bassLp - sample) * MathF.Pow(10f, bassGain / 20f);

        // Mid control (bandpass around 800Hz)
        float midFreq = 800f;
        float midQ = 2f;
        float midGain = (_mid * 2f - 1f - midScoop) * 8f;

        float w0 = 2f * MathF.PI * midFreq / SampleRate;
        float alpha = MathF.Sin(w0) / (2f * midQ);

        float midIn = _toneStackState[baseIdx + 1];
        float midOut = _toneStackState[baseIdx + 2];
        float midFiltered = alpha * (bassShelf - midOut) + midIn;
        _toneStackState[baseIdx + 1] = midIn + 2f * alpha * (bassShelf - midIn);
        _toneStackState[baseIdx + 2] = midOut + 2f * alpha * midFiltered;

        float midBoost = bassShelf + midFiltered * MathF.Pow(10f, midGain / 20f);

        // Treble control (high shelf around 3kHz)
        float trebFreq = 3000f;
        float trebGain = (_treble * 2f - 1f + trebleBoost) * 10f;
        float trebCoeff = MathF.Exp(-2f * MathF.PI * trebFreq / SampleRate);

        float trebHp = midBoost - _toneStackState[baseIdx + 3];
        _toneStackState[baseIdx + 3] = _toneStackState[baseIdx + 3] * trebCoeff + midBoost * (1f - trebCoeff);

        return midBoost + trebHp * (MathF.Pow(10f, trebGain / 20f) - 1f);
    }

    private float ApplyPresence(float sample, int channel)
    {
        int baseIdx = channel * 2;

        // Presence: high shelf around 5kHz
        float presFreq = 5000f;
        float presGain = (_presence * 2f - 1f) * 6f;
        float presCoeff = MathF.Exp(-2f * MathF.PI * presFreq / SampleRate);

        float presHp = sample - _presenceState[baseIdx];
        _presenceState[baseIdx] = _presenceState[baseIdx] * presCoeff + sample * (1f - presCoeff);

        return sample + presHp * (MathF.Pow(10f, presGain / 20f) - 1f);
    }

    private void ProcessConvolutionBlock()
    {
        for (int c = 0; c < Channels; c++)
        {
            // Prepare FFT input
            for (int i = 0; i < _fftSize; i++)
            {
                _fftInput[i] = i < _hopSize ? new Complex(_inputBuffer[c][i], 0) : Complex.Zero;
            }

            // Forward FFT
            FFT(_fftInput, _fftOutput, false);

            // Sum contributions from all loaded IR slots
            var summedOutput = new Complex[_fftSize];

            for (int slot = 0; slot < MaxIrSlots; slot++)
            {
                if (!_irSlots[slot].IsLoaded) continue;

                var irFreq = _irSlots[slot].GetFrequencyDomain(c);
                float gain = _irSlots[slot].Gain;

                for (int i = 0; i < _fftSize; i++)
                {
                    summedOutput[i] += _fftOutput[i] * irFreq[i] * gain;
                }
            }

            // Inverse FFT
            FFT(summedOutput, _fftInput, true);

            // Overlap-add
            for (int i = 0; i < _fftSize; i++)
            {
                int outPos = i;
                if (outPos < _outputBuffer[c].Length)
                {
                    _outputBuffer[c][outPos] += (float)_fftInput[i].Real / _fftSize;
                }
            }

            // Shift output buffer
            Array.Copy(_outputBuffer[c], _hopSize, _outputBuffer[c], 0, _outputBuffer[c].Length - _hopSize);
            Array.Clear(_outputBuffer[c], _outputBuffer[c].Length - _hopSize, _hopSize);
        }
    }

    private void FFT(Complex[] input, Complex[] output, bool inverse)
    {
        int n = input.Length;
        Array.Copy(input, output, n);

        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
            {
                (output[i], output[j]) = (output[j], output[i]);
            }
        }

        for (int size = 2; size <= n; size *= 2)
        {
            double angle = (inverse ? 2 : -2) * Math.PI / size;
            Complex wn = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int start = 0; start < n; start += size)
            {
                Complex w = Complex.One;
                for (int k = 0; k < size / 2; k++)
                {
                    Complex t = w * output[start + k + size / 2];
                    Complex u = output[start + k];
                    output[start + k] = u + t;
                    output[start + k + size / 2] = u - t;
                    w *= wn;
                }
            }
        }
    }

    private static int BitReverse(int x, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (x & 1);
            x >>= 1;
        }
        return result;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "drive": Drive = value; break;
            case "bass": Bass = value; break;
            case "mid": Mid = value; break;
            case "treble": Treble = value; break;
            case "presence": Presence = value; break;
            case "output": OutputLevel = value; break;
        }
    }

    #region Presets

    /// <summary>
    /// Creates a clean amp preset.
    /// </summary>
    public static ConvolutionAmpCab CreateCleanPreset(ISampleProvider source)
    {
        var amp = new ConvolutionAmpCab(source);
        amp.AmpType = AmpType.Clean;
        amp.Drive = 0.2f;
        amp.Bass = 0.5f;
        amp.Mid = 0.6f;
        amp.Treble = 0.5f;
        amp.Presence = 0.4f;
        amp.OutputLevel = 0.6f;
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a crunch amp preset.
    /// </summary>
    public static ConvolutionAmpCab CreateCrunchPreset(ISampleProvider source)
    {
        var amp = new ConvolutionAmpCab(source);
        amp.AmpType = AmpType.Crunch;
        amp.Drive = 0.5f;
        amp.Bass = 0.5f;
        amp.Mid = 0.5f;
        amp.Treble = 0.6f;
        amp.Presence = 0.5f;
        amp.OutputLevel = 0.5f;
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a high-gain metal preset.
    /// </summary>
    public static ConvolutionAmpCab CreateMetalPreset(ISampleProvider source)
    {
        var amp = new ConvolutionAmpCab(source);
        amp.AmpType = AmpType.Metal;
        amp.Drive = 0.8f;
        amp.Bass = 0.6f;
        amp.Mid = 0.3f;
        amp.Treble = 0.7f;
        amp.Presence = 0.6f;
        amp.OutputLevel = 0.4f;
        amp.Mix = 1.0f;
        return amp;
    }

    /// <summary>
    /// Creates a bass amp preset.
    /// </summary>
    public static ConvolutionAmpCab CreateBassPreset(ISampleProvider source)
    {
        var amp = new ConvolutionAmpCab(source);
        amp.AmpType = AmpType.Bass;
        amp.Drive = 0.3f;
        amp.Bass = 0.7f;
        amp.Mid = 0.6f;
        amp.Treble = 0.4f;
        amp.Presence = 0.3f;
        amp.OutputLevel = 0.6f;
        amp.Mix = 1.0f;
        return amp;
    }

    #endregion

    /// <summary>
    /// Internal class for managing IR slots.
    /// </summary>
    private class ImpulseResponseSlot
    {
        private Complex[][] _freqDomain = Array.Empty<Complex[]>();

        public bool IsLoaded { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public float Gain { get; set; } = 1.0f;
        public int Length { get; private set; }

        public void Load(float[][] ir, int fftSize, int channels, float gain, string name)
        {
            Length = ir[0].Length;
            Gain = gain;
            Name = name;

            _freqDomain = new Complex[channels][];

            for (int c = 0; c < channels; c++)
            {
                var irPadded = new Complex[fftSize];
                int sourceChannel = Math.Min(c, ir.Length - 1);
                int copyLength = Math.Min(ir[sourceChannel].Length, fftSize);

                for (int i = 0; i < copyLength; i++)
                {
                    irPadded[i] = new Complex(ir[sourceChannel][i], 0);
                }

                _freqDomain[c] = new Complex[fftSize];
                FFTStatic(irPadded, _freqDomain[c], false);
            }

            IsLoaded = true;
        }

        public void Clear()
        {
            _freqDomain = Array.Empty<Complex[]>();
            IsLoaded = false;
            Name = string.Empty;
            Length = 0;
        }

        public Complex[] GetFrequencyDomain(int channel)
        {
            if (!IsLoaded || channel >= _freqDomain.Length)
                return Array.Empty<Complex>();
            return _freqDomain[channel];
        }

        private static void FFTStatic(Complex[] input, Complex[] output, bool inverse)
        {
            int n = input.Length;
            Array.Copy(input, output, n);

            int bits = (int)Math.Log2(n);
            for (int i = 0; i < n; i++)
            {
                int j = 0;
                int x = i;
                for (int b = 0; b < bits; b++)
                {
                    j = (j << 1) | (x & 1);
                    x >>= 1;
                }
                if (j > i)
                {
                    (output[i], output[j]) = (output[j], output[i]);
                }
            }

            for (int size = 2; size <= n; size *= 2)
            {
                double angle = (inverse ? 2 : -2) * Math.PI / size;
                Complex wn = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int start = 0; start < n; start += size)
                {
                    Complex w = Complex.One;
                    for (int k = 0; k < size / 2; k++)
                    {
                        Complex t = w * output[start + k + size / 2];
                        Complex u = output[start + k];
                        output[start + k] = u + t;
                        output[start + k + size / 2] = u - t;
                        w *= wn;
                    }
                }
            }
        }
    }
}
