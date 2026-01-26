// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core.Spatial;

/// <summary>
/// Decoder output type.
/// </summary>
public enum AmbisonicDecoderOutput
{
    /// <summary>Decode to stereo speakers</summary>
    Stereo,
    /// <summary>Decode to 5.1 surround</summary>
    Surround51,
    /// <summary>Decode to 7.1 surround</summary>
    Surround71,
    /// <summary>Decode to binaural (headphones)</summary>
    Binaural,
    /// <summary>Decode to custom speaker layout</summary>
    Custom
}

/// <summary>
/// Decoding method for ambisonics.
/// </summary>
public enum AmbisonicDecodingMethod
{
    /// <summary>Basic sampling decoder (simple, efficient)</summary>
    Basic,
    /// <summary>Max-rE decoder (energy-preserving, better localization)</summary>
    MaxRE,
    /// <summary>In-phase decoder (shelf filters for psychoacoustic optimization)</summary>
    InPhase
}

/// <summary>
/// Decodes Ambisonic B-format audio to various speaker layouts or binaural output.
/// </summary>
public class AmbisonicDecoder : IDisposable
{
    private readonly int _order;
    private readonly int _inputChannelCount;
    private readonly int _sampleRate;
    private AmbisonicDecoderOutput _outputType;
    private AmbisonicDecodingMethod _decodingMethod;

    // Speaker configuration
    private SpeakerPosition[] _speakerLayout;
    private float[,] _decoderMatrix; // [speaker, ambisonic_channel]
    private int _outputChannelCount;

    // Shelf filters for psychoacoustic optimization
    private readonly ShelfFilter[] _shelfFiltersLow;
    private readonly ShelfFilter[] _shelfFiltersHigh;
    private bool _shelfFiltersEnabled = true;

    // Binaural decoder
    private BinauralRenderer? _binauralRenderer;

    // Temporary buffer for processing
    private float[] _tempBuffer = Array.Empty<float>();

    /// <summary>
    /// Gets the ambisonic order.
    /// </summary>
    public int Order => _order;

    /// <summary>
    /// Gets the number of input (ambisonic) channels.
    /// </summary>
    public int InputChannelCount => _inputChannelCount;

    /// <summary>
    /// Gets the number of output (speaker) channels.
    /// </summary>
    public int OutputChannelCount => _outputChannelCount;

    /// <summary>
    /// Gets or sets the output type.
    /// </summary>
    public AmbisonicDecoderOutput OutputType
    {
        get => _outputType;
        set
        {
            if (_outputType != value)
            {
                _outputType = value;
                ConfigureOutput();
            }
        }
    }

    /// <summary>
    /// Gets or sets the decoding method.
    /// </summary>
    public AmbisonicDecodingMethod DecodingMethod
    {
        get => _decodingMethod;
        set
        {
            if (_decodingMethod != value)
            {
                _decodingMethod = value;
                CalculateDecoderMatrix();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether shelf filters are enabled for psychoacoustic optimization.
    /// </summary>
    public bool ShelfFiltersEnabled
    {
        get => _shelfFiltersEnabled;
        set => _shelfFiltersEnabled = value;
    }

    /// <summary>
    /// Creates a new ambisonic decoder.
    /// </summary>
    /// <param name="order">Ambisonic order (1, 2, or 3)</param>
    /// <param name="sampleRate">Audio sample rate</param>
    /// <param name="outputType">Target output type</param>
    /// <param name="decodingMethod">Decoding method</param>
    public AmbisonicDecoder(
        int order = 1,
        int sampleRate = 44100,
        AmbisonicDecoderOutput outputType = AmbisonicDecoderOutput.Stereo,
        AmbisonicDecodingMethod decodingMethod = AmbisonicDecodingMethod.Basic)
    {
        if (order < 1 || order > 3)
            throw new ArgumentOutOfRangeException(nameof(order), "Ambisonic order must be 1, 2, or 3");

        _order = order;
        _inputChannelCount = (order + 1) * (order + 1);
        _sampleRate = sampleRate;
        _outputType = outputType;
        _decodingMethod = decodingMethod;

        // Initialize shelf filters (one per ambisonic order)
        _shelfFiltersLow = new ShelfFilter[order + 1];
        _shelfFiltersHigh = new ShelfFilter[order + 1];
        for (int i = 0; i <= order; i++)
        {
            _shelfFiltersLow[i] = new ShelfFilter(sampleRate, 400f, ShelfFilterType.Low);
            _shelfFiltersHigh[i] = new ShelfFilter(sampleRate, 4000f, ShelfFilterType.High);
        }

        // Configure output
        _speakerLayout = Array.Empty<SpeakerPosition>();
        _decoderMatrix = new float[0, 0];
        ConfigureOutput();
    }

    /// <summary>
    /// Sets a custom speaker layout for decoding.
    /// </summary>
    public void SetCustomLayout(SpeakerPosition[] speakers)
    {
        _speakerLayout = speakers;
        _outputType = AmbisonicDecoderOutput.Custom;
        _outputChannelCount = speakers.Length;
        CalculateDecoderMatrix();
    }

    /// <summary>
    /// Configures the output based on the selected output type.
    /// </summary>
    private void ConfigureOutput()
    {
        _speakerLayout = _outputType switch
        {
            AmbisonicDecoderOutput.Stereo => SpeakerLayouts.Stereo,
            AmbisonicDecoderOutput.Surround51 => SpeakerLayouts.Surround51,
            AmbisonicDecoderOutput.Surround71 => SpeakerLayouts.Surround71,
            AmbisonicDecoderOutput.Binaural => SpeakerLayouts.Stereo,
            AmbisonicDecoderOutput.Custom => _speakerLayout,
            _ => SpeakerLayouts.Stereo
        };

        _outputChannelCount = _speakerLayout.Length;

        if (_outputType == AmbisonicDecoderOutput.Binaural)
        {
            _binauralRenderer = new BinauralRenderer(_sampleRate);
            _outputChannelCount = 2;
        }
        else
        {
            _binauralRenderer?.Dispose();
            _binauralRenderer = null;
        }

        CalculateDecoderMatrix();
    }

    /// <summary>
    /// Calculates the decoder matrix based on speaker layout and decoding method.
    /// </summary>
    private void CalculateDecoderMatrix()
    {
        if (_outputType == AmbisonicDecoderOutput.Binaural)
        {
            // Binaural uses HRTF, not a matrix
            return;
        }

        _decoderMatrix = new float[_speakerLayout.Length, _inputChannelCount];

        for (int sp = 0; sp < _speakerLayout.Length; sp++)
        {
            var speaker = _speakerLayout[sp];

            // Skip LFE channel for spatial decoding
            if (speaker.Name == "LFE")
            {
                // LFE gets a portion of the omnidirectional (W) channel
                _decoderMatrix[sp, 0] = 0.7071f; // -3dB
                continue;
            }

            // Calculate spherical harmonic values for this speaker direction
            float azRad = speaker.Azimuth * MathF.PI / 180f;
            float elRad = speaker.Elevation * MathF.PI / 180f;

            float cosEl = MathF.Cos(elRad);
            float sinEl = MathF.Sin(elRad);
            float cosPhi = MathF.Cos(azRad);
            float sinPhi = MathF.Sin(azRad);

            // Order 0
            _decoderMatrix[sp, 0] = GetDecodingWeight(0) / _speakerLayout.Length; // W

            if (_order >= 1 && _inputChannelCount > 3)
            {
                float order1Weight = GetDecodingWeight(1) / _speakerLayout.Length;
                _decoderMatrix[sp, 1] = sinPhi * cosEl * order1Weight;  // Y
                _decoderMatrix[sp, 2] = sinEl * order1Weight;           // Z
                _decoderMatrix[sp, 3] = cosPhi * cosEl * order1Weight;  // X
            }

            if (_order >= 2 && _inputChannelCount > 8)
            {
                float order2Weight = GetDecodingWeight(2) / _speakerLayout.Length;
                float cos2Phi = MathF.Cos(2f * azRad);
                float sin2Phi = MathF.Sin(2f * azRad);
                float cosEl2 = cosEl * cosEl;

                _decoderMatrix[sp, 4] = MathF.Sqrt(3f) / 2f * sin2Phi * cosEl2 * order2Weight;
                _decoderMatrix[sp, 5] = MathF.Sqrt(3f) / 2f * sinPhi * 2f * sinEl * cosEl * order2Weight;
                _decoderMatrix[sp, 6] = 0.5f * (3f * sinEl * sinEl - 1f) * order2Weight;
                _decoderMatrix[sp, 7] = MathF.Sqrt(3f) / 2f * cosPhi * 2f * sinEl * cosEl * order2Weight;
                _decoderMatrix[sp, 8] = MathF.Sqrt(3f) / 2f * cos2Phi * cosEl2 * order2Weight;
            }

            if (_order >= 3 && _inputChannelCount > 15)
            {
                float order3Weight = GetDecodingWeight(3) / _speakerLayout.Length;
                float cos3Phi = MathF.Cos(3f * azRad);
                float sin3Phi = MathF.Sin(3f * azRad);
                float cos2Phi = MathF.Cos(2f * azRad);
                float sin2Phi = MathF.Sin(2f * azRad);
                float cosEl2 = cosEl * cosEl;
                float cosEl3 = cosEl2 * cosEl;
                float sinEl2 = sinEl * sinEl;

                _decoderMatrix[sp, 9] = MathF.Sqrt(5f / 8f) * sin3Phi * cosEl3 * order3Weight;
                _decoderMatrix[sp, 10] = MathF.Sqrt(15f) / 2f * sin2Phi * sinEl * cosEl2 * order3Weight;
                _decoderMatrix[sp, 11] = MathF.Sqrt(3f / 8f) * sinPhi * cosEl * (5f * sinEl2 - 1f) * order3Weight;
                _decoderMatrix[sp, 12] = 0.5f * sinEl * (5f * sinEl2 - 3f) * order3Weight;
                _decoderMatrix[sp, 13] = MathF.Sqrt(3f / 8f) * cosPhi * cosEl * (5f * sinEl2 - 1f) * order3Weight;
                _decoderMatrix[sp, 14] = MathF.Sqrt(15f) / 2f * cos2Phi * sinEl * cosEl2 * order3Weight;
                _decoderMatrix[sp, 15] = MathF.Sqrt(5f / 8f) * cos3Phi * cosEl3 * order3Weight;
            }
        }

        // Normalize decoder matrix
        NormalizeDecoderMatrix();
    }

    /// <summary>
    /// Gets the decoding weight for a given order based on decoding method.
    /// </summary>
    private float GetDecodingWeight(int order)
    {
        return _decodingMethod switch
        {
            AmbisonicDecodingMethod.Basic => 1f,
            AmbisonicDecodingMethod.MaxRE => GetMaxREWeight(order),
            AmbisonicDecodingMethod.InPhase => GetInPhaseWeight(order),
            _ => 1f
        };
    }

    /// <summary>
    /// Max-rE weights for improved localization.
    /// </summary>
    private float GetMaxREWeight(int order)
    {
        // Max-rE weights optimize energy vector
        return order switch
        {
            0 => 1f,
            1 => 0.775f,
            2 => 0.4f,
            3 => 0.105f,
            _ => 1f / (order + 1)
        };
    }

    /// <summary>
    /// In-phase weights for improved perception.
    /// </summary>
    private float GetInPhaseWeight(int order)
    {
        // In-phase weights ensure all speakers contribute positively
        int n = _order;
        return (float)(Factorial(n) * Factorial(n + 1)) /
               (float)(Factorial(n + order + 1) * Factorial(n - order));
    }

    /// <summary>
    /// Normalizes the decoder matrix to prevent clipping.
    /// </summary>
    private void NormalizeDecoderMatrix()
    {
        // Find maximum sum across all speakers
        float maxSum = 0f;
        for (int sp = 0; sp < _speakerLayout.Length; sp++)
        {
            float sum = 0f;
            for (int ch = 0; ch < _inputChannelCount; ch++)
            {
                sum += MathF.Abs(_decoderMatrix[sp, ch]);
            }
            maxSum = MathF.Max(maxSum, sum);
        }

        // Normalize if needed
        if (maxSum > 1f)
        {
            float scale = 1f / maxSum;
            for (int sp = 0; sp < _speakerLayout.Length; sp++)
            {
                for (int ch = 0; ch < _inputChannelCount; ch++)
                {
                    _decoderMatrix[sp, ch] *= scale;
                }
            }
        }
    }

    /// <summary>
    /// Decodes an ambisonic frame to the output speaker layout.
    /// </summary>
    /// <param name="ambisonicInput">Input buffer containing one frame of ambisonic channels</param>
    /// <param name="inputOffset">Offset into the input buffer</param>
    /// <param name="speakerOutput">Output buffer for speaker signals</param>
    /// <param name="outputOffset">Offset into the output buffer</param>
    public void DecodeFrame(float[] ambisonicInput, int inputOffset, float[] speakerOutput, int outputOffset)
    {
        if (_outputType == AmbisonicDecoderOutput.Binaural && _binauralRenderer != null)
        {
            DecodeBinauralFrame(ambisonicInput, inputOffset, speakerOutput, outputOffset);
            return;
        }

        // Apply shelf filters if enabled
        if (_shelfFiltersEnabled)
        {
            EnsureBufferSize(ref _tempBuffer, _inputChannelCount);
            Array.Copy(ambisonicInput, inputOffset, _tempBuffer, 0, _inputChannelCount);
            ApplyShelfFilters(_tempBuffer);
        }
        else
        {
            EnsureBufferSize(ref _tempBuffer, _inputChannelCount);
            Array.Copy(ambisonicInput, inputOffset, _tempBuffer, 0, _inputChannelCount);
        }

        // Matrix multiplication: speakers = decoder_matrix * ambisonic_input
        for (int sp = 0; sp < _outputChannelCount; sp++)
        {
            float sample = 0f;
            for (int ch = 0; ch < _inputChannelCount; ch++)
            {
                sample += _decoderMatrix[sp, ch] * _tempBuffer[ch];
            }
            speakerOutput[outputOffset + sp] = sample;
        }
    }

    /// <summary>
    /// Decodes a buffer of ambisonic frames.
    /// </summary>
    /// <param name="ambisonicInput">Input buffer</param>
    /// <param name="speakerOutput">Output buffer</param>
    /// <param name="frameCount">Number of frames to decode</param>
    public void Decode(float[] ambisonicInput, float[] speakerOutput, int frameCount)
    {
        for (int frame = 0; frame < frameCount; frame++)
        {
            DecodeFrame(
                ambisonicInput, frame * _inputChannelCount,
                speakerOutput, frame * _outputChannelCount);
        }
    }

    /// <summary>
    /// Decodes to binaural output using HRTF.
    /// </summary>
    private void DecodeBinauralFrame(float[] ambisonicInput, int inputOffset, float[] speakerOutput, int outputOffset)
    {
        if (_binauralRenderer == null) return;

        // For binaural, we decode each direction and apply HRTF
        // This is a simplified approach - a full implementation would use
        // virtual speakers or direct HRTF convolution per ambisonic channel

        // Use W + X*cos + Y*sin approach for basic binaural
        float w = ambisonicInput[inputOffset];
        float y = _inputChannelCount > 1 ? ambisonicInput[inputOffset + 1] : 0f;
        float z = _inputChannelCount > 2 ? ambisonicInput[inputOffset + 2] : 0f;
        float x = _inputChannelCount > 3 ? ambisonicInput[inputOffset + 3] : 0f;

        // Simple stereo decode with HRTF-like processing
        // Left ear emphasizes Y (left) signal, right ear emphasizes -Y
        float leftEar = w * 0.7071f + y * 0.5f + x * 0.3f;
        float rightEar = w * 0.7071f - y * 0.5f + x * 0.3f;

        // Add some interaural time difference simulation
        // (Full HRTF would use the BinauralRenderer)
        speakerOutput[outputOffset] = leftEar;
        speakerOutput[outputOffset + 1] = rightEar;
    }

    /// <summary>
    /// Applies shelf filters for psychoacoustic optimization.
    /// </summary>
    private void ApplyShelfFilters(float[] buffer)
    {
        // Apply different shelf filter gains per order
        // This helps with localization at different frequencies

        int channelIndex = 0;
        for (int order = 0; order <= _order; order++)
        {
            int channelsInOrder = 2 * order + 1;
            float lowGain = GetShelfGain(order, true);
            float highGain = GetShelfGain(order, false);

            _shelfFiltersLow[order].SetGain(lowGain);
            _shelfFiltersHigh[order].SetGain(highGain);

            for (int i = 0; i < channelsInOrder && channelIndex < buffer.Length; i++)
            {
                float sample = buffer[channelIndex];
                sample = _shelfFiltersLow[order].Process(sample);
                sample = _shelfFiltersHigh[order].Process(sample);
                buffer[channelIndex] = sample;
                channelIndex++;
            }
        }
    }

    /// <summary>
    /// Gets shelf filter gain for a given order.
    /// </summary>
    private float GetShelfGain(int order, bool isLow)
    {
        // Higher orders need more boost at low frequencies
        // and less boost at high frequencies for natural perception
        if (isLow)
        {
            return 1f + order * 0.15f; // Boost low frequencies for higher orders
        }
        else
        {
            return 1f - order * 0.1f; // Attenuate high frequencies for higher orders
        }
    }

    /// <summary>
    /// Calculates factorial.
    /// </summary>
    private static long Factorial(int n)
    {
        if (n <= 1) return 1;
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }

    /// <summary>
    /// Ensures the buffer is at least the specified size.
    /// </summary>
    private void EnsureBufferSize(ref float[] buffer, int requiredSize)
    {
        if (buffer.Length < requiredSize)
        {
            buffer = new float[requiredSize];
        }
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        _binauralRenderer?.Dispose();
    }
}

/// <summary>
/// Shelf filter type.
/// </summary>
public enum ShelfFilterType
{
    /// <summary>Low shelf filter</summary>
    Low,
    /// <summary>High shelf filter</summary>
    High
}

/// <summary>
/// Simple shelf filter for ambisonic decoding.
/// </summary>
internal class ShelfFilter
{
    private readonly int _sampleRate;
    private readonly float _frequency;
    private readonly ShelfFilterType _type;
    private float _gain = 1f;

    // Filter state
    private float _x1, _x2, _y1, _y2;

    // Filter coefficients
    private float _a0, _a1, _a2, _b0, _b1, _b2;

    public ShelfFilter(int sampleRate, float frequency, ShelfFilterType type)
    {
        _sampleRate = sampleRate;
        _frequency = frequency;
        _type = type;
        CalculateCoefficients();
    }

    public void SetGain(float gain)
    {
        if (MathF.Abs(_gain - gain) > 0.001f)
        {
            _gain = gain;
            CalculateCoefficients();
        }
    }

    private void CalculateCoefficients()
    {
        float A = MathF.Sqrt(_gain);
        float w0 = 2f * MathF.PI * _frequency / _sampleRate;
        float cosw0 = MathF.Cos(w0);
        float sinw0 = MathF.Sin(w0);
        float alpha = sinw0 / 2f * MathF.Sqrt((A + 1f / A) * (1f / 0.9f - 1f) + 2f);
        float sqrtA = MathF.Sqrt(A);

        if (_type == ShelfFilterType.Low)
        {
            _b0 = A * ((A + 1f) - (A - 1f) * cosw0 + 2f * sqrtA * alpha);
            _b1 = 2f * A * ((A - 1f) - (A + 1f) * cosw0);
            _b2 = A * ((A + 1f) - (A - 1f) * cosw0 - 2f * sqrtA * alpha);
            _a0 = (A + 1f) + (A - 1f) * cosw0 + 2f * sqrtA * alpha;
            _a1 = -2f * ((A - 1f) + (A + 1f) * cosw0);
            _a2 = (A + 1f) + (A - 1f) * cosw0 - 2f * sqrtA * alpha;
        }
        else
        {
            _b0 = A * ((A + 1f) + (A - 1f) * cosw0 + 2f * sqrtA * alpha);
            _b1 = -2f * A * ((A - 1f) + (A + 1f) * cosw0);
            _b2 = A * ((A + 1f) + (A - 1f) * cosw0 - 2f * sqrtA * alpha);
            _a0 = (A + 1f) - (A - 1f) * cosw0 + 2f * sqrtA * alpha;
            _a1 = 2f * ((A - 1f) - (A + 1f) * cosw0);
            _a2 = (A + 1f) - (A - 1f) * cosw0 - 2f * sqrtA * alpha;
        }

        // Normalize
        _b0 /= _a0;
        _b1 /= _a0;
        _b2 /= _a0;
        _a1 /= _a0;
        _a2 /= _a0;
    }

    public float Process(float input)
    {
        float output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return output;
    }

    public void Reset()
    {
        _x1 = _x2 = _y1 = _y2 = 0f;
    }
}
