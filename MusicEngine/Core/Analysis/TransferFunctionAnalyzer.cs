//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Transfer function analyzer for measuring EQ/compressor frequency and phase response curves.

using System;
using System.Numerics;
using NAudio.Dsp;
using NAudio.Wave;
using Complex = NAudio.Dsp.Complex;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Analysis mode for transfer function measurement.
/// </summary>
public enum TransferFunctionMode
{
    /// <summary>Uses a sine sweep from low to high frequency.</summary>
    SineSweep,

    /// <summary>Uses white noise for broadband excitation.</summary>
    WhiteNoise,

    /// <summary>Uses pink noise (equal energy per octave).</summary>
    PinkNoise,

    /// <summary>Uses an impulse response measurement.</summary>
    Impulse
}

/// <summary>
/// Represents a point on a frequency response curve.
/// </summary>
public class FrequencyResponsePoint
{
    /// <summary>Gets the frequency in Hz.</summary>
    public float Frequency { get; init; }

    /// <summary>Gets the gain in dB.</summary>
    public float GainDb { get; init; }

    /// <summary>Gets the phase in degrees (-180 to +180).</summary>
    public float PhaseDegrees { get; init; }

    /// <summary>Gets the phase in radians.</summary>
    public float PhaseRadians => PhaseDegrees * (float)(Math.PI / 180.0);

    /// <summary>Gets the linear gain (ratio).</summary>
    public float GainLinear => (float)Math.Pow(10, GainDb / 20.0);
}

/// <summary>
/// Result of a transfer function analysis.
/// </summary>
public class TransferFunctionResult
{
    /// <summary>
    /// Gets the frequency response points.
    /// </summary>
    public FrequencyResponsePoint[] FrequencyResponse { get; init; } = Array.Empty<FrequencyResponsePoint>();

    /// <summary>
    /// Gets the analysis mode used.
    /// </summary>
    public TransferFunctionMode Mode { get; init; }

    /// <summary>
    /// Gets the minimum frequency analyzed in Hz.
    /// </summary>
    public float MinFrequency { get; init; }

    /// <summary>
    /// Gets the maximum frequency analyzed in Hz.
    /// </summary>
    public float MaxFrequency { get; init; }

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize { get; init; }

    /// <summary>
    /// Gets the peak gain in dB.
    /// </summary>
    public float PeakGainDb { get; init; }

    /// <summary>
    /// Gets the frequency at peak gain in Hz.
    /// </summary>
    public float PeakFrequency { get; init; }

    /// <summary>
    /// Gets the minimum gain in dB.
    /// </summary>
    public float MinGainDb { get; init; }

    /// <summary>
    /// Gets the average gain in dB.
    /// </summary>
    public float AverageGainDb { get; init; }

    /// <summary>
    /// Gets the group delay in samples at each frequency point.
    /// </summary>
    public float[]? GroupDelay { get; init; }

    /// <summary>
    /// Gets whether the analysis detected significant phase issues.
    /// </summary>
    public bool HasPhaseIssues { get; init; }

    /// <summary>
    /// Gets a description of detected characteristics.
    /// </summary>
    public string? Characteristics { get; init; }
}

/// <summary>
/// Event arguments for real-time transfer function analysis updates.
/// </summary>
public class TransferFunctionEventArgs : EventArgs
{
    /// <summary>Gets the current partial result.</summary>
    public TransferFunctionResult Result { get; }

    /// <summary>Gets the analysis progress (0.0 to 1.0).</summary>
    public float Progress { get; }

    /// <summary>
    /// Creates new transfer function event arguments.
    /// </summary>
    public TransferFunctionEventArgs(TransferFunctionResult result, float progress)
    {
        Result = result;
        Progress = progress;
    }
}

/// <summary>
/// Transfer function analyzer for measuring the frequency and phase response
/// of audio processors like EQs, compressors, and effects.
/// </summary>
/// <remarks>
/// This analyzer works by:
/// 1. Generating a test signal (sine sweep, white noise, etc.)
/// 2. Capturing the signal before and after processing
/// 3. Computing the transfer function H(f) = Output(f) / Input(f)
/// 4. Extracting magnitude (gain) and phase response curves
/// </remarks>
public class TransferFunctionAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _fftSize;
    private readonly float _minFrequency;
    private readonly float _maxFrequency;
    private readonly int _numPoints;

    private readonly Complex[] _inputFftBuffer;
    private readonly Complex[] _outputFftBuffer;
    private readonly float[] _window;
    private readonly object _lock = new();

    // Real-time analysis buffers
    private float[] _inputBuffer;
    private float[] _outputBuffer;
    private int _bufferPosition;
    private bool _isCapturing;

    /// <summary>
    /// Gets the sample rate used for analysis.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the FFT size used for analysis.
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Gets the minimum frequency for analysis in Hz.
    /// </summary>
    public float MinFrequency => _minFrequency;

    /// <summary>
    /// Gets the maximum frequency for analysis in Hz.
    /// </summary>
    public float MaxFrequency => _maxFrequency;

    /// <summary>
    /// Gets the number of frequency points in the response.
    /// </summary>
    public int NumPoints => _numPoints;

    /// <summary>
    /// Event raised during analysis progress updates.
    /// </summary>
    public event EventHandler<TransferFunctionEventArgs>? AnalysisProgress;

    /// <summary>
    /// Creates a new transfer function analyzer with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="fftSize">FFT window size, must be power of 2 (default: 8192).</param>
    /// <param name="minFrequency">Minimum frequency to analyze in Hz (default: 20).</param>
    /// <param name="maxFrequency">Maximum frequency to analyze in Hz (default: 20000).</param>
    /// <param name="numPoints">Number of frequency points in the response (default: 256).</param>
    public TransferFunctionAnalyzer(
        int sampleRate = 44100,
        int fftSize = 8192,
        float minFrequency = 20f,
        float maxFrequency = 20000f,
        int numPoints = 256)
    {
        if (!IsPowerOfTwo(fftSize))
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));
        if (minFrequency >= maxFrequency)
            throw new ArgumentException("Minimum frequency must be less than maximum frequency.");
        if (maxFrequency > sampleRate / 2)
            maxFrequency = sampleRate / 2f;
        if (numPoints < 16 || numPoints > 4096)
            throw new ArgumentOutOfRangeException(nameof(numPoints), "Number of points must be between 16 and 4096.");

        _sampleRate = sampleRate;
        _fftSize = fftSize;
        _minFrequency = minFrequency;
        _maxFrequency = maxFrequency;
        _numPoints = numPoints;

        _inputFftBuffer = new Complex[fftSize];
        _outputFftBuffer = new Complex[fftSize];
        _window = GenerateHannWindow(fftSize);

        _inputBuffer = new float[fftSize * 4]; // Allow for multiple frames
        _outputBuffer = new float[fftSize * 4];
    }

    /// <summary>
    /// Generates a test signal for transfer function measurement.
    /// </summary>
    /// <param name="mode">Type of test signal to generate.</param>
    /// <param name="durationSeconds">Duration of the test signal in seconds.</param>
    /// <param name="amplitude">Amplitude of the test signal (0.0 to 1.0).</param>
    /// <returns>Mono audio samples containing the test signal.</returns>
    public float[] GenerateTestSignal(
        TransferFunctionMode mode,
        float durationSeconds = 2.0f,
        float amplitude = 0.5f)
    {
        int numSamples = (int)(durationSeconds * _sampleRate);
        float[] signal = new float[numSamples];

        switch (mode)
        {
            case TransferFunctionMode.SineSweep:
                GenerateSineSweep(signal, amplitude);
                break;
            case TransferFunctionMode.WhiteNoise:
                GenerateWhiteNoise(signal, amplitude);
                break;
            case TransferFunctionMode.PinkNoise:
                GeneratePinkNoise(signal, amplitude);
                break;
            case TransferFunctionMode.Impulse:
                GenerateImpulse(signal, amplitude);
                break;
        }

        return signal;
    }

    /// <summary>
    /// Analyzes the transfer function by comparing input and output signals.
    /// </summary>
    /// <param name="inputSignal">Original (dry) signal.</param>
    /// <param name="outputSignal">Processed (wet) signal.</param>
    /// <param name="mode">Analysis mode used for generating the test signal.</param>
    /// <returns>Transfer function analysis result.</returns>
    public TransferFunctionResult Analyze(
        float[] inputSignal,
        float[] outputSignal,
        TransferFunctionMode mode = TransferFunctionMode.WhiteNoise)
    {
        if (inputSignal == null || inputSignal.Length == 0)
            throw new ArgumentException("Input signal cannot be null or empty.", nameof(inputSignal));
        if (outputSignal == null || outputSignal.Length == 0)
            throw new ArgumentException("Output signal cannot be null or empty.", nameof(outputSignal));

        int minLength = Math.Min(inputSignal.Length, outputSignal.Length);
        int numFrames = minLength / _fftSize;

        if (numFrames < 1)
            throw new ArgumentException($"Signals must be at least {_fftSize} samples long.");

        // Accumulate cross-spectral density
        var inputPsd = new double[_fftSize / 2 + 1];
        var outputPsd = new double[_fftSize / 2 + 1];
        var crossPsd = new System.Numerics.Complex[_fftSize / 2 + 1];

        int hopSize = _fftSize / 2;
        int position = 0;
        int frameCount = 0;

        while (position + _fftSize <= minLength)
        {
            // Copy and window input
            for (int i = 0; i < _fftSize; i++)
            {
                _inputFftBuffer[i].X = inputSignal[position + i] * _window[i];
                _inputFftBuffer[i].Y = 0;
                _outputFftBuffer[i].X = outputSignal[position + i] * _window[i];
                _outputFftBuffer[i].Y = 0;
            }

            // Perform FFTs
            int m = (int)Math.Log(_fftSize, 2.0);
            FastFourierTransform.FFT(true, m, _inputFftBuffer);
            FastFourierTransform.FFT(true, m, _outputFftBuffer);

            // Accumulate PSDs
            for (int bin = 0; bin <= _fftSize / 2; bin++)
            {
                var inputComplex = new System.Numerics.Complex(_inputFftBuffer[bin].X, _inputFftBuffer[bin].Y);
                var outputComplex = new System.Numerics.Complex(_outputFftBuffer[bin].X, _outputFftBuffer[bin].Y);

                inputPsd[bin] += inputComplex.Magnitude * inputComplex.Magnitude;
                outputPsd[bin] += outputComplex.Magnitude * outputComplex.Magnitude;
                crossPsd[bin] += outputComplex * System.Numerics.Complex.Conjugate(inputComplex);
            }

            frameCount++;
            position += hopSize;

            // Report progress
            float progress = (float)position / minLength;
            AnalysisProgress?.Invoke(this, new TransferFunctionEventArgs(
                CreatePartialResult(inputPsd, crossPsd, frameCount), progress));
        }

        return CreateResult(inputPsd, outputPsd, crossPsd, frameCount, mode);
    }

    /// <summary>
    /// Analyzes the transfer function using ISampleProvider streams (real-time capable).
    /// </summary>
    /// <param name="inputSource">Audio source before processing.</param>
    /// <param name="outputSource">Audio source after processing.</param>
    /// <param name="mode">Analysis mode.</param>
    /// <param name="durationSeconds">Duration to analyze in seconds.</param>
    /// <returns>Transfer function analysis result.</returns>
    public TransferFunctionResult AnalyzeStreams(
        ISampleProvider inputSource,
        ISampleProvider outputSource,
        TransferFunctionMode mode = TransferFunctionMode.WhiteNoise,
        float durationSeconds = 2.0f)
    {
        int totalSamples = (int)(durationSeconds * _sampleRate);
        float[] inputSignal = new float[totalSamples];
        float[] outputSignal = new float[totalSamples];

        // Read from both streams
        int inputRead = inputSource.Read(inputSignal, 0, totalSamples);
        int outputRead = outputSource.Read(outputSignal, 0, totalSamples);

        // Truncate to minimum
        int actualSamples = Math.Min(inputRead, outputRead);
        if (actualSamples < totalSamples)
        {
            Array.Resize(ref inputSignal, actualSamples);
            Array.Resize(ref outputSignal, actualSamples);
        }

        return Analyze(inputSignal, outputSignal, mode);
    }

    /// <summary>
    /// Starts real-time capture for transfer function analysis.
    /// Call CaptureInput/CaptureOutput with synchronized samples, then call FinishCapture.
    /// </summary>
    public void StartCapture()
    {
        lock (_lock)
        {
            _inputBuffer = new float[_fftSize * 4];
            _outputBuffer = new float[_fftSize * 4];
            _bufferPosition = 0;
            _isCapturing = true;
        }
    }

    /// <summary>
    /// Captures input samples for real-time analysis.
    /// Must be called in sync with CaptureOutput.
    /// </summary>
    public void CaptureInput(float[] samples, int count)
    {
        lock (_lock)
        {
            if (!_isCapturing) return;

            int copyCount = Math.Min(count, _inputBuffer.Length - _bufferPosition);
            Array.Copy(samples, 0, _inputBuffer, _bufferPosition, copyCount);
        }
    }

    /// <summary>
    /// Captures output samples for real-time analysis.
    /// Must be called in sync with CaptureInput.
    /// </summary>
    public void CaptureOutput(float[] samples, int count)
    {
        lock (_lock)
        {
            if (!_isCapturing) return;

            int copyCount = Math.Min(count, _outputBuffer.Length - _bufferPosition);
            Array.Copy(samples, 0, _outputBuffer, _bufferPosition, copyCount);
            _bufferPosition += copyCount;
        }
    }

    /// <summary>
    /// Finishes capture and returns the analysis result.
    /// </summary>
    public TransferFunctionResult FinishCapture(TransferFunctionMode mode = TransferFunctionMode.WhiteNoise)
    {
        lock (_lock)
        {
            _isCapturing = false;

            float[] input = new float[_bufferPosition];
            float[] output = new float[_bufferPosition];
            Array.Copy(_inputBuffer, input, _bufferPosition);
            Array.Copy(_outputBuffer, output, _bufferPosition);

            return Analyze(input, output, mode);
        }
    }

    /// <summary>
    /// Calculates the gain at a specific frequency from a transfer function result.
    /// </summary>
    /// <param name="result">Transfer function result.</param>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <returns>Interpolated gain in dB at the specified frequency.</returns>
    public static float GetGainAtFrequency(TransferFunctionResult result, float frequency)
    {
        if (result.FrequencyResponse.Length == 0)
            return 0;

        // Find nearest points and interpolate
        for (int i = 0; i < result.FrequencyResponse.Length - 1; i++)
        {
            var p1 = result.FrequencyResponse[i];
            var p2 = result.FrequencyResponse[i + 1];

            if (frequency >= p1.Frequency && frequency <= p2.Frequency)
            {
                float t = (frequency - p1.Frequency) / (p2.Frequency - p1.Frequency);
                return p1.GainDb + t * (p2.GainDb - p1.GainDb);
            }
        }

        // Return endpoint if out of range
        if (frequency < result.FrequencyResponse[0].Frequency)
            return result.FrequencyResponse[0].GainDb;

        return result.FrequencyResponse[^1].GainDb;
    }

    /// <summary>
    /// Calculates the phase at a specific frequency from a transfer function result.
    /// </summary>
    /// <param name="result">Transfer function result.</param>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <returns>Interpolated phase in degrees at the specified frequency.</returns>
    public static float GetPhaseAtFrequency(TransferFunctionResult result, float frequency)
    {
        if (result.FrequencyResponse.Length == 0)
            return 0;

        // Find nearest points and interpolate
        for (int i = 0; i < result.FrequencyResponse.Length - 1; i++)
        {
            var p1 = result.FrequencyResponse[i];
            var p2 = result.FrequencyResponse[i + 1];

            if (frequency >= p1.Frequency && frequency <= p2.Frequency)
            {
                float t = (frequency - p1.Frequency) / (p2.Frequency - p1.Frequency);
                return p1.PhaseDegrees + t * (p2.PhaseDegrees - p1.PhaseDegrees);
            }
        }

        // Return endpoint if out of range
        if (frequency < result.FrequencyResponse[0].Frequency)
            return result.FrequencyResponse[0].PhaseDegrees;

        return result.FrequencyResponse[^1].PhaseDegrees;
    }

    private TransferFunctionResult CreateResult(
        double[] inputPsd,
        double[] outputPsd,
        System.Numerics.Complex[] crossPsd,
        int frameCount,
        TransferFunctionMode mode)
    {
        float binResolution = (float)_sampleRate / _fftSize;
        var points = new FrequencyResponsePoint[_numPoints];

        // Calculate logarithmically spaced frequency points
        float logMin = (float)Math.Log10(_minFrequency);
        float logMax = (float)Math.Log10(_maxFrequency);
        float logStep = (logMax - logMin) / (_numPoints - 1);

        float peakGain = float.MinValue;
        float peakFreq = 0;
        float minGain = float.MaxValue;
        float sumGain = 0;
        float[] groupDelay = new float[_numPoints];
        bool hasPhaseIssues = false;
        float previousPhase = 0;

        for (int i = 0; i < _numPoints; i++)
        {
            float frequency = (float)Math.Pow(10, logMin + i * logStep);
            int bin = (int)(frequency / binResolution);
            bin = Math.Clamp(bin, 1, _fftSize / 2 - 1);

            // Calculate transfer function from cross-spectral density
            var cross = crossPsd[bin] / frameCount;
            double inputPower = inputPsd[bin] / frameCount;

            double magnitude;
            double phase;

            if (inputPower > 1e-10)
            {
                var h = cross / inputPower;
                magnitude = h.Magnitude;
                phase = Math.Atan2(h.Imaginary, h.Real);
            }
            else
            {
                magnitude = 1.0;
                phase = 0;
            }

            float gainDb = (float)(20 * Math.Log10(Math.Max(magnitude, 1e-10)));
            float phaseDeg = (float)(phase * 180 / Math.PI);

            // Unwrap phase
            while (phaseDeg - previousPhase > 180) phaseDeg -= 360;
            while (phaseDeg - previousPhase < -180) phaseDeg += 360;
            previousPhase = phaseDeg;

            // Check for phase issues (rapid changes)
            if (i > 0 && Math.Abs(phaseDeg - points[i - 1].PhaseDegrees) > 90)
            {
                hasPhaseIssues = true;
            }

            points[i] = new FrequencyResponsePoint
            {
                Frequency = frequency,
                GainDb = gainDb,
                PhaseDegrees = phaseDeg
            };

            // Calculate group delay (derivative of phase)
            if (i > 0)
            {
                float phaseDiff = (points[i].PhaseRadians - points[i - 1].PhaseRadians);
                float freqDiff = points[i].Frequency - points[i - 1].Frequency;
                groupDelay[i] = -phaseDiff / (2 * (float)Math.PI * freqDiff);
            }

            // Track statistics
            if (gainDb > peakGain)
            {
                peakGain = gainDb;
                peakFreq = frequency;
            }
            if (gainDb < minGain)
            {
                minGain = gainDb;
            }
            sumGain += gainDb;
        }

        // Determine characteristics
        string characteristics = DetermineCharacteristics(points, peakGain, minGain);

        return new TransferFunctionResult
        {
            FrequencyResponse = points,
            Mode = mode,
            MinFrequency = _minFrequency,
            MaxFrequency = _maxFrequency,
            SampleRate = _sampleRate,
            FftSize = _fftSize,
            PeakGainDb = peakGain,
            PeakFrequency = peakFreq,
            MinGainDb = minGain,
            AverageGainDb = sumGain / _numPoints,
            GroupDelay = groupDelay,
            HasPhaseIssues = hasPhaseIssues,
            Characteristics = characteristics
        };
    }

    private TransferFunctionResult CreatePartialResult(
        double[] inputPsd,
        System.Numerics.Complex[] crossPsd,
        int frameCount)
    {
        // Simplified result for progress reporting
        float binResolution = (float)_sampleRate / _fftSize;
        var points = new FrequencyResponsePoint[Math.Min(32, _numPoints)];

        float logMin = (float)Math.Log10(_minFrequency);
        float logMax = (float)Math.Log10(_maxFrequency);
        float logStep = (logMax - logMin) / (points.Length - 1);

        for (int i = 0; i < points.Length; i++)
        {
            float frequency = (float)Math.Pow(10, logMin + i * logStep);
            int bin = (int)(frequency / binResolution);
            bin = Math.Clamp(bin, 1, _fftSize / 2 - 1);

            var cross = crossPsd[bin] / Math.Max(frameCount, 1);
            double inputPower = inputPsd[bin] / Math.Max(frameCount, 1);

            double magnitude = inputPower > 1e-10 ? (cross / inputPower).Magnitude : 1.0;
            float gainDb = (float)(20 * Math.Log10(Math.Max(magnitude, 1e-10)));

            points[i] = new FrequencyResponsePoint
            {
                Frequency = frequency,
                GainDb = gainDb,
                PhaseDegrees = 0
            };
        }

        return new TransferFunctionResult
        {
            FrequencyResponse = points,
            MinFrequency = _minFrequency,
            MaxFrequency = _maxFrequency
        };
    }

    private static string DetermineCharacteristics(FrequencyResponsePoint[] points, float peakGain, float minGain)
    {
        var characteristics = new System.Collections.Generic.List<string>();

        float range = peakGain - minGain;

        // Check if it's a flat response
        if (range < 1.0f)
        {
            characteristics.Add("Flat response (unity gain)");
        }
        else if (range < 3.0f)
        {
            characteristics.Add("Nearly flat response");
        }

        // Check for low-pass characteristic
        if (points.Length > 10)
        {
            float lowAvg = 0, highAvg = 0;
            int third = points.Length / 3;

            for (int i = 0; i < third; i++)
                lowAvg += points[i].GainDb;
            for (int i = points.Length - third; i < points.Length; i++)
                highAvg += points[i].GainDb;

            lowAvg /= third;
            highAvg /= third;

            if (lowAvg - highAvg > 6)
                characteristics.Add("Low-pass characteristic");
            else if (highAvg - lowAvg > 6)
                characteristics.Add("High-pass characteristic");

            // Check for mid-boost/cut
            float midAvg = 0;
            for (int i = third; i < points.Length - third; i++)
                midAvg += points[i].GainDb;
            midAvg /= (points.Length - 2 * third);

            if (midAvg > lowAvg + 3 && midAvg > highAvg + 3)
                characteristics.Add("Mid-frequency boost");
            else if (midAvg < lowAvg - 3 && midAvg < highAvg - 3)
                characteristics.Add("Mid-frequency cut");
        }

        // Check for compression (if gain varies with frequency in a specific pattern)
        if (peakGain < 0 && minGain < 0)
        {
            characteristics.Add("Gain reduction present");
        }

        return characteristics.Count > 0
            ? string.Join("; ", characteristics)
            : "Complex frequency response";
    }

    private void GenerateSineSweep(float[] signal, float amplitude)
    {
        // Exponential sine sweep from minFrequency to maxFrequency
        double w1 = 2 * Math.PI * _minFrequency;
        double w2 = 2 * Math.PI * _maxFrequency;
        double T = (double)signal.Length / _sampleRate;
        double k = T / Math.Log(w2 / w1);

        for (int i = 0; i < signal.Length; i++)
        {
            double t = (double)i / _sampleRate;
            double phase = k * w1 * (Math.Exp(t / k) - 1);
            signal[i] = (float)(amplitude * Math.Sin(phase));
        }

        // Apply fade in/out to reduce transients
        ApplyFades(signal, (int)(_sampleRate * 0.01));
    }

    private void GenerateWhiteNoise(float[] signal, float amplitude)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] = (float)((random.NextDouble() * 2 - 1) * amplitude);
        }
        ApplyFades(signal, (int)(_sampleRate * 0.01));
    }

    private void GeneratePinkNoise(float[] signal, float amplitude)
    {
        // Pink noise using Voss-McCartney algorithm
        var random = new Random(42);
        const int numRows = 16;
        double[] rows = new double[numRows];
        int counter = 0;

        for (int i = 0; i < signal.Length; i++)
        {
            double sum = 0;
            int changed = counter ^ (counter - 1);
            counter++;

            for (int row = 0; row < numRows; row++)
            {
                if ((changed & (1 << row)) != 0)
                {
                    rows[row] = random.NextDouble() * 2 - 1;
                }
                sum += rows[row];
            }

            signal[i] = (float)(sum / numRows * amplitude);
        }

        ApplyFades(signal, (int)(_sampleRate * 0.01));
    }

    private void GenerateImpulse(float[] signal, float amplitude)
    {
        // Multiple impulses spaced throughout the signal
        int numImpulses = 8;
        int spacing = signal.Length / numImpulses;

        for (int i = 0; i < numImpulses; i++)
        {
            int position = i * spacing + spacing / 2;
            if (position < signal.Length)
            {
                signal[position] = amplitude;
            }
        }
    }

    private static void ApplyFades(float[] signal, int fadeLength)
    {
        fadeLength = Math.Min(fadeLength, signal.Length / 4);

        for (int i = 0; i < fadeLength; i++)
        {
            float fade = (float)i / fadeLength;
            signal[i] *= fade;
            signal[signal.Length - 1 - i] *= fade;
        }
    }

    private static float[] GenerateHannWindow(int length)
    {
        float[] window = new float[length];
        for (int i = 0; i < length; i++)
        {
            window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1))));
        }
        return window;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
}
