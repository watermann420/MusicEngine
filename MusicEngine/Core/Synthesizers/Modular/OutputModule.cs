// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Output module.
/// Final output stage with stereo mixing, metering, and limiting.
/// </summary>
public class OutputModule : ModuleBase
{
    private float _peakLeft;
    private float _peakRight;
    private float _rmsLeft;
    private float _rmsRight;
    private int _sampleCounter;

    // Inputs
    private readonly ModulePort _leftInput;
    private readonly ModulePort _rightInput;
    private readonly ModulePort _monoInput;

    // Internal buffers for stereo output
    private readonly float[] _leftBuffer;
    private readonly float[] _rightBuffer;

    public OutputModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Output", sampleRate, bufferSize)
    {
        _leftBuffer = new float[bufferSize];
        _rightBuffer = new float[bufferSize];

        // Inputs
        _leftInput = AddInput("Left", PortType.Audio);
        _rightInput = AddInput("Right", PortType.Audio);
        _monoInput = AddInput("Mono", PortType.Audio);

        // Parameters
        RegisterParameter("MasterLevel", 0.8f, 0f, 1f);
        RegisterParameter("Pan", 0.5f, 0f, 1f);          // For mono input
        RegisterParameter("Limiter", 1f, 0f, 1f);        // Enable/disable limiting
        RegisterParameter("LimiterThreshold", 0.95f, 0.1f, 1f);
    }

    public override void Process(int sampleCount)
    {
        float masterLevel = GetParameter("MasterLevel");
        float pan = GetParameter("Pan");
        bool limiterEnabled = GetParameter("Limiter") > 0.5f;
        float threshold = GetParameter("LimiterThreshold");

        float sumSquaredLeft = 0f;
        float sumSquaredRight = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float left = _leftInput.GetValue(i);
            float right = _rightInput.GetValue(i);
            float mono = _monoInput.GetValue(i);

            // Mix mono input into stereo with panning
            float monoLeft = mono * (float)Math.Sqrt(1f - pan);
            float monoRight = mono * (float)Math.Sqrt(pan);

            left += monoLeft;
            right += monoRight;

            // Apply master level
            left *= masterLevel;
            right *= masterLevel;

            // Apply limiting
            if (limiterEnabled)
            {
                left = SoftLimit(left, threshold);
                right = SoftLimit(right, threshold);
            }

            // Store in buffers
            _leftBuffer[i] = left;
            _rightBuffer[i] = right;

            // Update metering
            float absLeft = Math.Abs(left);
            float absRight = Math.Abs(right);

            if (absLeft > _peakLeft) _peakLeft = absLeft;
            if (absRight > _peakRight) _peakRight = absRight;

            sumSquaredLeft += left * left;
            sumSquaredRight += right * right;
        }

        // Update RMS values
        _sampleCounter += sampleCount;
        if (_sampleCounter >= SampleRate / 20)  // Update at ~20Hz
        {
            _rmsLeft = (float)Math.Sqrt(sumSquaredLeft / sampleCount);
            _rmsRight = (float)Math.Sqrt(sumSquaredRight / sampleCount);

            // Decay peaks
            _peakLeft *= 0.95f;
            _peakRight *= 0.95f;

            _sampleCounter = 0;
        }
    }

    private static float SoftLimit(float x, float threshold)
    {
        if (Math.Abs(x) <= threshold) return x;

        float sign = Math.Sign(x);
        float absX = Math.Abs(x);
        float knee = 1f - threshold;

        // Soft knee limiting
        float over = absX - threshold;
        float compressed = threshold + knee * (float)Math.Tanh(over / knee);

        return sign * Math.Min(compressed, 1f);
    }

    /// <summary>
    /// Gets the stereo output buffers.
    /// </summary>
    public (float[] Left, float[] Right) GetOutputBuffers()
    {
        return (_leftBuffer, _rightBuffer);
    }

    /// <summary>
    /// Copies the output to an interleaved stereo buffer.
    /// </summary>
    public void CopyToInterleavedBuffer(float[] buffer, int offset, int sampleCount)
    {
        for (int i = 0; i < sampleCount && i < _leftBuffer.Length; i++)
        {
            buffer[offset + i * 2] = _leftBuffer[i];
            buffer[offset + i * 2 + 1] = _rightBuffer[i];
        }
    }

    /// <summary>
    /// Gets the current peak levels.
    /// </summary>
    public (float Left, float Right) PeakLevels => (_peakLeft, _peakRight);

    /// <summary>
    /// Gets the current RMS levels.
    /// </summary>
    public (float Left, float Right) RmsLevels => (_rmsLeft, _rmsRight);

    /// <summary>
    /// Resets the peak meters.
    /// </summary>
    public void ResetPeaks()
    {
        _peakLeft = 0;
        _peakRight = 0;
    }

    public override void Reset()
    {
        base.Reset();
        Array.Clear(_leftBuffer, 0, _leftBuffer.Length);
        Array.Clear(_rightBuffer, 0, _rightBuffer.Length);
        _peakLeft = 0;
        _peakRight = 0;
        _rmsLeft = 0;
        _rmsRight = 0;
        _sampleCounter = 0;
    }
}
