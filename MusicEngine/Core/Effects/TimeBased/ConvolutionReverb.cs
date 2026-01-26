// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Reverb effect processor.

using System;
using System.IO;
using System.Numerics;
using NAudio.Wave;


namespace MusicEngine.Core.Effects.TimeBased;


/// <summary>
/// Convolution reverb that uses impulse response files for realistic reverb.
/// Uses FFT-based overlap-add convolution for efficient processing.
/// </summary>
public class ConvolutionReverb : EffectBase
{
    // FFT settings
    private int _fftSize = 4096;
    private int _hopSize;

    // Impulse response in frequency domain (for each channel)
    private Complex[][] _irFreqDomain = Array.Empty<Complex[]>();
    private int _irLength;

    // Input/output buffers
    private float[][] _inputBuffer = Array.Empty<float[]>();      // Circular input buffer per channel
    private float[][] _outputBuffer = Array.Empty<float[]>();     // Overlap-add output buffer per channel
    private int _inputWritePos;
    private int _outputReadPos;

    // FFT working arrays
    private Complex[] _fftInput = Array.Empty<Complex>();
    private Complex[] _fftOutput = Array.Empty<Complex>();

    // Parameters
    private float _preDelay = 0f;        // Pre-delay in ms
    private float _decay = 1f;           // Decay trim (0-1)
    private float _lowCut = 20f;         // Low frequency cut (Hz)
    private float _highCut = 20000f;     // High frequency cut (Hz)

    // Pre-delay buffer
    private float[][] _preDelayBuffer = Array.Empty<float[]>();
    private int _preDelayLength;
    private int _preDelayPos;

    /// <summary>
    /// Pre-delay time in milliseconds
    /// </summary>
    public float PreDelay
    {
        get => _preDelay;
        set
        {
            _preDelay = Math.Clamp(value, 0f, 500f);
            UpdatePreDelay();
        }
    }

    /// <summary>
    /// Decay trim - shortens the reverb tail (0-1)
    /// </summary>
    public float Decay
    {
        get => _decay;
        set => _decay = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Low frequency cutoff for the reverb
    /// </summary>
    public float LowCut
    {
        get => _lowCut;
        set => _lowCut = Math.Clamp(value, 20f, 2000f);
    }

    /// <summary>
    /// High frequency cutoff for the reverb
    /// </summary>
    public float HighCut
    {
        get => _highCut;
        set => _highCut = Math.Clamp(value, 1000f, 20000f);
    }

    /// <summary>
    /// Creates a convolution reverb effect
    /// </summary>
    public ConvolutionReverb(ISampleProvider source) : base(source, "Convolution Reverb")
    {
        _hopSize = _fftSize / 2;

        InitializeBuffers();

        // Generate a simple default IR (small room)
        GenerateDefaultIR();

        RegisterParameter("mix", 0.3f);
        RegisterParameter("predelay", 0f);
        RegisterParameter("decay", 1f);
        RegisterParameter("lowcut", 20f);
        RegisterParameter("highcut", 20000f);
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

        UpdatePreDelay();
    }

    private void UpdatePreDelay()
    {
        _preDelayLength = (int)(PreDelay * SampleRate / 1000f);
        _preDelayLength = Math.Max(1, _preDelayLength);

        _preDelayBuffer = new float[Channels][];
        for (int c = 0; c < Channels; c++)
        {
            _preDelayBuffer[c] = new float[_preDelayLength];
        }
        _preDelayPos = 0;
    }

    /// <summary>
    /// Generate a simple algorithmic IR for default use
    /// </summary>
    private void GenerateDefaultIR()
    {
        // Generate a simple exponential decay IR
        _irLength = SampleRate; // 1 second IR
        int channels = Channels;

        var ir = new float[channels][];
        for (int c = 0; c < channels; c++)
        {
            ir[c] = new float[_irLength];
        }

        var random = new Random(42);

        for (int i = 0; i < _irLength; i++)
        {
            float t = (float)i / SampleRate;
            float decay = MathF.Exp(-3f * t); // Decay rate

            for (int c = 0; c < channels; c++)
            {
                // Random noise with decay
                float noise = (float)(random.NextDouble() * 2 - 1);
                ir[c][i] = noise * decay * 0.5f;
            }

            // Add some early reflections
            if (i == (int)(0.01f * SampleRate)) // 10ms
            {
                for (int c = 0; c < channels; c++)
                    ir[c][i] += 0.3f * (c == 0 ? 1 : -1);
            }
            if (i == (int)(0.02f * SampleRate)) // 20ms
            {
                for (int c = 0; c < channels; c++)
                    ir[c][i] += 0.2f;
            }
            if (i == (int)(0.035f * SampleRate)) // 35ms
            {
                for (int c = 0; c < channels; c++)
                    ir[c][i] += 0.15f * (c == 0 ? -1 : 1);
            }
        }

        PrepareIR(ir);
    }

    /// <summary>
    /// Load an impulse response from a WAV file
    /// </summary>
    public void LoadIR(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"IR file not found: {path}");

        using var reader = new AudioFileReader(path);

        int irChannels = reader.WaveFormat.Channels;
        int totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
        int samplesPerChannel = totalSamples / irChannels;

        // Read the entire file
        var buffer = new float[totalSamples];
        int read = reader.Read(buffer, 0, totalSamples);

        // Deinterleave
        var ir = new float[Math.Max(irChannels, Channels)][];
        for (int c = 0; c < ir.Length; c++)
        {
            ir[c] = new float[samplesPerChannel];
        }

        for (int i = 0; i < samplesPerChannel; i++)
        {
            for (int c = 0; c < irChannels; c++)
            {
                if (i * irChannels + c < read)
                {
                    ir[c][i] = buffer[i * irChannels + c];
                }
            }
        }

        // If IR is mono and we need stereo, duplicate
        if (irChannels == 1 && Channels == 2)
        {
            ir[1] = new float[samplesPerChannel];
            Array.Copy(ir[0], ir[1], samplesPerChannel);
        }

        // Resample if necessary
        if (reader.WaveFormat.SampleRate != SampleRate)
        {
            for (int c = 0; c < ir.Length; c++)
            {
                ir[c] = ResampleIR(ir[c], reader.WaveFormat.SampleRate, SampleRate);
            }
        }

        PrepareIR(ir);
    }

    /// <summary>
    /// Load an IR from raw sample data
    /// </summary>
    public void LoadIR(float[][] ir)
    {
        PrepareIR(ir);
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

    private void PrepareIR(float[][] ir)
    {
        _irLength = ir[0].Length;

        // Determine FFT size (next power of 2 that fits IR + hopSize)
        _fftSize = 1;
        while (_fftSize < _irLength + _hopSize)
        {
            _fftSize *= 2;
        }
        _fftSize = Math.Max(_fftSize, 4096);
        _hopSize = _fftSize / 2;

        // Reinitialize buffers with new size
        InitializeBuffers();

        // Transform IR to frequency domain
        _irFreqDomain = new Complex[Channels][];

        for (int c = 0; c < Channels; c++)
        {
            var irPadded = new Complex[_fftSize];

            int sourceChannel = Math.Min(c, ir.Length - 1);
            int copyLength = Math.Min(ir[sourceChannel].Length, _fftSize);

            for (int i = 0; i < copyLength; i++)
            {
                // Apply decay trim
                float decayEnv = 1f;
                if (_decay < 1f)
                {
                    float t = (float)i / copyLength;
                    decayEnv = MathF.Pow(1f - t, (1f - _decay) * 3f);
                }

                irPadded[i] = new Complex(ir[sourceChannel][i] * decayEnv, 0);
            }

            // FFT the IR
            _irFreqDomain[c] = new Complex[_fftSize];
            FFT(irPadded, _irFreqDomain[c], false);
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        for (int i = 0; i < count; i += channels)
        {
            for (int c = 0; c < channels; c++)
            {
                float input = sourceBuffer[i + c];

                // Apply pre-delay
                float delayed = _preDelayBuffer[c][_preDelayPos];
                _preDelayBuffer[c][_preDelayPos] = input;

                // Write to input buffer
                _inputBuffer[c][_inputWritePos] = delayed;

                // Read from output buffer
                float output = _outputBuffer[c][_outputReadPos];
                _outputBuffer[c][_outputReadPos] = 0; // Clear for next overlap-add

                destBuffer[offset + i + c] = output;
            }

            _inputWritePos++;
            _outputReadPos++;
            _preDelayPos = (_preDelayPos + 1) % _preDelayLength;

            // When we've accumulated enough samples, do convolution
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

    private void ProcessConvolutionBlock()
    {
        for (int c = 0; c < Channels; c++)
        {
            // Prepare FFT input (zero-pad)
            for (int i = 0; i < _fftSize; i++)
            {
                _fftInput[i] = i < _hopSize ? new Complex(_inputBuffer[c][i], 0) : Complex.Zero;
            }

            // Forward FFT
            FFT(_fftInput, _fftOutput, false);

            // Multiply with IR spectrum
            for (int i = 0; i < _fftSize; i++)
            {
                _fftOutput[i] *= _irFreqDomain[c][i];
            }

            // Inverse FFT
            FFT(_fftOutput, _fftInput, true);

            // Overlap-add to output buffer
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

    /// <summary>
    /// Simple in-place Cooley-Tukey FFT
    /// </summary>
    private void FFT(Complex[] input, Complex[] output, bool inverse)
    {
        int n = input.Length;
        Array.Copy(input, output, n);

        // Bit-reversal permutation
        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
            {
                (output[i], output[j]) = (output[j], output[i]);
            }
        }

        // Cooley-Tukey iterative FFT
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

    private int BitReverse(int x, int bits)
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
            case "predelay":
                PreDelay = value;
                break;
            case "decay":
                Decay = value;
                break;
            case "lowcut":
                LowCut = value;
                break;
            case "highcut":
                HighCut = value;
                break;
        }
    }

    /// <summary>
    /// Create a hall reverb preset
    /// </summary>
    public static ConvolutionReverb CreateHallPreset(ISampleProvider source)
    {
        var reverb = new ConvolutionReverb(source);
        reverb.Mix = 0.35f;
        reverb.PreDelay = 25f;
        reverb.Decay = 0.8f;
        return reverb;
    }

    /// <summary>
    /// Create a room reverb preset
    /// </summary>
    public static ConvolutionReverb CreateRoomPreset(ISampleProvider source)
    {
        var reverb = new ConvolutionReverb(source);
        reverb.Mix = 0.25f;
        reverb.PreDelay = 10f;
        reverb.Decay = 0.6f;
        return reverb;
    }

    /// <summary>
    /// Create a plate reverb preset
    /// </summary>
    public static ConvolutionReverb CreatePlatePreset(ISampleProvider source)
    {
        var reverb = new ConvolutionReverb(source);
        reverb.Mix = 0.4f;
        reverb.PreDelay = 5f;
        reverb.Decay = 0.9f;
        reverb.HighCut = 8000f;
        return reverb;
    }
}
