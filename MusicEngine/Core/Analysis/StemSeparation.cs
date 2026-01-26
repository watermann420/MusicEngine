// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: AI-based stem separation.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Stem types that can be separated.
/// </summary>
public enum StemType
{
    /// <summary>Vocal track</summary>
    Vocals,
    /// <summary>Drum/percussion track</summary>
    Drums,
    /// <summary>Bass track</summary>
    Bass,
    /// <summary>Other instruments (piano, guitars, synths, etc.)</summary>
    Other,
    /// <summary>Full mix (all stems combined)</summary>
    Mix
}

/// <summary>
/// Quality/speed tradeoff settings for stem separation.
/// </summary>
public enum SeparationQuality
{
    /// <summary>Fast processing, lower quality</summary>
    Fast,
    /// <summary>Balanced speed and quality</summary>
    Medium,
    /// <summary>Slower processing, higher quality</summary>
    High,
    /// <summary>Highest quality, slowest processing</summary>
    Ultra
}

/// <summary>
/// Progress information for stem separation.
/// </summary>
public record StemSeparationProgress(
    float OverallProgress,
    string CurrentPhase,
    StemType? CurrentStem
);

/// <summary>
/// Result of stem separation containing separated audio streams.
/// </summary>
public class StemSeparationResult : IDisposable
{
    private readonly Dictionary<StemType, float[]> _stems = new();
    private readonly int _sampleRate;
    private readonly int _channels;
    private bool _disposed;

    /// <summary>
    /// Sample rate of the separated stems.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Number of channels (1 = mono, 2 = stereo).
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets the available stem types.
    /// </summary>
    public IEnumerable<StemType> AvailableStems => _stems.Keys;

    internal StemSeparationResult(int sampleRate, int channels)
    {
        _sampleRate = sampleRate;
        _channels = channels;
    }

    internal void AddStem(StemType type, float[] samples)
    {
        _stems[type] = samples;
    }

    /// <summary>
    /// Gets the raw sample data for a stem.
    /// </summary>
    public float[]? GetStemSamples(StemType type)
    {
        return _stems.TryGetValue(type, out var samples) ? samples : null;
    }

    /// <summary>
    /// Gets a stem as an ISampleProvider for playback or further processing.
    /// </summary>
    public ISampleProvider? GetStemProvider(StemType type)
    {
        if (!_stems.TryGetValue(type, out var samples))
            return null;

        return new StemSampleProvider(samples, _sampleRate, _channels);
    }

    /// <summary>
    /// Exports a stem to a WAV file.
    /// </summary>
    public void ExportStem(StemType type, string path)
    {
        if (!_stems.TryGetValue(type, out var samples))
            throw new ArgumentException($"Stem type {type} not found");

        var format = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels);
        using var writer = new WaveFileWriter(path, format);
        writer.WriteSamples(samples, 0, samples.Length);
    }

    /// <summary>
    /// Exports all stems to WAV files in the specified directory.
    /// </summary>
    public void ExportAllStems(string directory, string baseName = "stem")
    {
        if (!System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);

        foreach (var (type, _) in _stems)
        {
            string path = System.IO.Path.Combine(directory, $"{baseName}_{type.ToString().ToLower()}.wav");
            ExportStem(type, path);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stems.Clear();
    }
}

/// <summary>
/// Sample provider wrapper for separated stem audio.
/// </summary>
internal class StemSampleProvider : ISampleProvider
{
    private readonly float[] _samples;
    private int _position;

    public WaveFormat WaveFormat { get; }

    public StemSampleProvider(float[] samples, int sampleRate, int channels)
    {
        _samples = samples;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesToRead = Math.Min(count, _samples.Length - _position);
        if (samplesToRead <= 0)
            return 0;

        Array.Copy(_samples, _position, buffer, offset, samplesToRead);
        _position += samplesToRead;
        return samplesToRead;
    }
}

/// <summary>
/// Audio source separation using spectral masking and Non-negative Matrix Factorization (NMF).
/// Separates audio into vocals, drums, bass, and other stems.
/// </summary>
public class StemSeparation
{
    private readonly int _fftSize;
    private readonly int _hopSize;
    private readonly float[] _window;
    private readonly Complex[] _fftBuffer;
    private readonly Complex[] _fftResult;

    // NMF parameters
    private readonly int _nmfComponents;
    private readonly int _nmfIterations;

    // Frequency band definitions (Hz)
    private const float BassLowCutoff = 20f;
    private const float BassHighCutoff = 250f;
    private const float VocalLowCutoff = 100f;
    private const float VocalHighCutoff = 8000f;
    private const float DrumTransientThreshold = 0.5f;

    /// <summary>
    /// Creates a new stem separation processor.
    /// </summary>
    /// <param name="quality">Quality/speed tradeoff</param>
    public StemSeparation(SeparationQuality quality = SeparationQuality.Medium)
    {
        // Set parameters based on quality
        switch (quality)
        {
            case SeparationQuality.Fast:
                _fftSize = 2048;
                _nmfComponents = 8;
                _nmfIterations = 50;
                break;
            case SeparationQuality.Medium:
                _fftSize = 4096;
                _nmfComponents = 16;
                _nmfIterations = 100;
                break;
            case SeparationQuality.High:
                _fftSize = 8192;
                _nmfComponents = 32;
                _nmfIterations = 200;
                break;
            case SeparationQuality.Ultra:
                _fftSize = 16384;
                _nmfComponents = 64;
                _nmfIterations = 500;
                break;
            default:
                _fftSize = 4096;
                _nmfComponents = 16;
                _nmfIterations = 100;
                break;
        }

        _hopSize = _fftSize / 4;
        _window = CreateHannWindow(_fftSize);
        _fftBuffer = new Complex[_fftSize];
        _fftResult = new Complex[_fftSize];
    }

    /// <summary>
    /// Separates audio into stems asynchronously.
    /// </summary>
    /// <param name="audioPath">Path to the audio file</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stem separation result</returns>
    public async Task<StemSeparationResult> SeparateAsync(
        string audioPath,
        IProgress<StemSeparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var reader = new AudioFileReader(audioPath);
        return await SeparateAsync(reader, progress, cancellationToken);
    }

    /// <summary>
    /// Separates audio into stems asynchronously.
    /// </summary>
    /// <param name="source">Audio source</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stem separation result</returns>
    public async Task<StemSeparationResult> SeparateAsync(
        ISampleProvider source,
        IProgress<StemSeparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Separate(source, progress, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Separates audio into stems synchronously.
    /// </summary>
    public StemSeparationResult Separate(
        ISampleProvider source,
        IProgress<StemSeparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int sampleRate = source.WaveFormat.SampleRate;
        int channels = source.WaveFormat.Channels;

        // Read all audio into memory
        progress?.Report(new StemSeparationProgress(0f, "Loading audio", null));

        var audioData = ReadAllSamples(source);
        if (audioData.Length == 0)
            throw new InvalidOperationException("No audio data to process");

        // Convert to mono for analysis
        var monoData = ConvertToMono(audioData, channels);

        // Phase 1: STFT analysis
        progress?.Report(new StemSeparationProgress(0.1f, "Analyzing spectrum", null));
        var (magnitude, phase) = ComputeSTFT(monoData, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        // Phase 2: Create spectral masks using NMF and heuristics
        progress?.Report(new StemSeparationProgress(0.3f, "Computing separation masks", null));
        var masks = ComputeSeparationMasks(magnitude, sampleRate, progress, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        // Phase 3: Apply masks and reconstruct stems
        var result = new StemSeparationResult(sampleRate, channels);

        foreach (var (stemType, mask) in masks)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            progress?.Report(new StemSeparationProgress(
                0.5f + 0.1f * (int)stemType,
                $"Reconstructing {stemType}",
                stemType));

            var stemMagnitude = ApplyMask(magnitude, mask);
            var stemMono = ComputeISTFT(stemMagnitude, phase, monoData.Length);

            // Convert back to original channel format
            var stemSamples = channels == 1 ? stemMono : ConvertToStereo(stemMono, channels);
            result.AddStem(stemType, stemSamples);
        }

        progress?.Report(new StemSeparationProgress(1f, "Complete", null));
        return result;
    }

    private float[] ReadAllSamples(ISampleProvider source)
    {
        var samples = new List<float>();
        var buffer = new float[4096];
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(buffer[i]);
        }

        return samples.ToArray();
    }

    private float[] ConvertToMono(float[] samples, int channels)
    {
        if (channels == 1)
            return samples;

        var mono = new float[samples.Length / channels];
        for (int i = 0; i < mono.Length; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++)
                sum += samples[i * channels + c];
            mono[i] = sum / channels;
        }
        return mono;
    }

    private float[] ConvertToStereo(float[] mono, int channels)
    {
        var stereo = new float[mono.Length * channels];
        for (int i = 0; i < mono.Length; i++)
        {
            for (int c = 0; c < channels; c++)
                stereo[i * channels + c] = mono[i];
        }
        return stereo;
    }

    private (float[,] magnitude, float[,] phase) ComputeSTFT(float[] samples, CancellationToken cancellationToken)
    {
        int numFrames = (samples.Length - _fftSize) / _hopSize + 1;
        int numBins = _fftSize / 2 + 1;

        var magnitude = new float[numFrames, numBins];
        var phase = new float[numFrames, numBins];

        for (int frame = 0; frame < numFrames; frame++)
        {
            if (cancellationToken.IsCancellationRequested)
                return (magnitude, phase);

            int startSample = frame * _hopSize;

            // Apply window and load into FFT buffer
            for (int i = 0; i < _fftSize; i++)
            {
                int sampleIdx = startSample + i;
                float sample = sampleIdx < samples.Length ? samples[sampleIdx] : 0f;
                _fftBuffer[i] = new Complex(sample * _window[i], 0);
            }

            // Perform FFT
            Array.Copy(_fftBuffer, _fftResult, _fftSize);
            FFT(_fftResult, false);

            // Extract magnitude and phase
            for (int bin = 0; bin < numBins; bin++)
            {
                magnitude[frame, bin] = (float)_fftResult[bin].Magnitude;
                phase[frame, bin] = (float)Math.Atan2(_fftResult[bin].Imaginary, _fftResult[bin].Real);
            }
        }

        return (magnitude, phase);
    }

    private float[] ComputeISTFT(float[,] magnitude, float[,] phase, int originalLength)
    {
        int numFrames = magnitude.GetLength(0);
        int numBins = magnitude.GetLength(1);

        var output = new float[originalLength + _fftSize];
        var windowSum = new float[output.Length];

        for (int frame = 0; frame < numFrames; frame++)
        {
            int startSample = frame * _hopSize;

            // Reconstruct complex spectrum
            for (int bin = 0; bin < numBins; bin++)
            {
                float mag = magnitude[frame, bin];
                float ph = phase[frame, bin];
                _fftResult[bin] = new Complex(mag * Math.Cos(ph), mag * Math.Sin(ph));

                // Mirror for negative frequencies
                if (bin > 0 && bin < numBins - 1)
                {
                    _fftResult[_fftSize - bin] = Complex.Conjugate(_fftResult[bin]);
                }
            }

            // Inverse FFT
            FFT(_fftResult, true);

            // Overlap-add with window
            for (int i = 0; i < _fftSize; i++)
            {
                int outIdx = startSample + i;
                if (outIdx < output.Length)
                {
                    output[outIdx] += (float)_fftResult[i].Real * _window[i] / _fftSize;
                    windowSum[outIdx] += _window[i] * _window[i];
                }
            }
        }

        // Normalize by window sum
        for (int i = 0; i < originalLength; i++)
        {
            if (windowSum[i] > 1e-8f)
                output[i] /= windowSum[i];
        }

        // Trim to original length
        var result = new float[originalLength];
        Array.Copy(output, result, originalLength);
        return result;
    }

    private Dictionary<StemType, float[,]> ComputeSeparationMasks(
        float[,] magnitude,
        int sampleRate,
        IProgress<StemSeparationProgress>? progress,
        CancellationToken cancellationToken)
    {
        int numFrames = magnitude.GetLength(0);
        int numBins = magnitude.GetLength(1);
        float freqPerBin = (float)sampleRate / _fftSize;

        var masks = new Dictionary<StemType, float[,]>
        {
            [StemType.Vocals] = new float[numFrames, numBins],
            [StemType.Drums] = new float[numFrames, numBins],
            [StemType.Bass] = new float[numFrames, numBins],
            [StemType.Other] = new float[numFrames, numBins]
        };

        // Compute onset/transient detection for drums
        var transientEnergy = ComputeTransientEnergy(magnitude);

        // Compute harmonic-percussive separation
        var (harmonicMask, percussiveMask) = ComputeHPSeparation(magnitude);

        // Apply NMF for more refined separation
        progress?.Report(new StemSeparationProgress(0.35f, "Running NMF decomposition", null));
        var (W, H) = NMFDecompose(magnitude, _nmfComponents, _nmfIterations, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            return masks;

        // Classify NMF components and create masks
        ClassifyComponents(W, H, magnitude, sampleRate, masks);

        // Combine with frequency-based heuristics
        for (int frame = 0; frame < numFrames; frame++)
        {
            for (int bin = 0; bin < numBins; bin++)
            {
                float freq = bin * freqPerBin;

                // Bass: low frequency + harmonic
                if (freq >= BassLowCutoff && freq <= BassHighCutoff)
                {
                    masks[StemType.Bass][frame, bin] = Math.Max(
                        masks[StemType.Bass][frame, bin],
                        harmonicMask[frame, bin] * 0.8f
                    );
                }

                // Drums: percussive content
                if (transientEnergy[frame] > DrumTransientThreshold)
                {
                    masks[StemType.Drums][frame, bin] = Math.Max(
                        masks[StemType.Drums][frame, bin],
                        percussiveMask[frame, bin] * 0.7f
                    );
                }

                // Vocals: mid-frequency harmonic content
                if (freq >= VocalLowCutoff && freq <= VocalHighCutoff)
                {
                    float vocalWeight = harmonicMask[frame, bin] * (1f - percussiveMask[frame, bin]);
                    masks[StemType.Vocals][frame, bin] = Math.Max(
                        masks[StemType.Vocals][frame, bin],
                        vocalWeight * 0.6f
                    );
                }

                // Normalize masks so they sum to ~1
                float sum = masks[StemType.Vocals][frame, bin] +
                           masks[StemType.Drums][frame, bin] +
                           masks[StemType.Bass][frame, bin];

                if (sum > 0)
                {
                    // "Other" gets the remainder
                    masks[StemType.Other][frame, bin] = Math.Max(0, 1f - sum);
                }
                else
                {
                    masks[StemType.Other][frame, bin] = 1f;
                }

                // Ensure all masks are in [0, 1]
                foreach (var stemType in masks.Keys)
                {
                    masks[stemType][frame, bin] = Math.Clamp(masks[stemType][frame, bin], 0f, 1f);
                }
            }
        }

        return masks;
    }

    private float[] ComputeTransientEnergy(float[,] magnitude)
    {
        int numFrames = magnitude.GetLength(0);
        int numBins = magnitude.GetLength(1);
        var energy = new float[numFrames];

        for (int frame = 1; frame < numFrames; frame++)
        {
            float diff = 0;
            for (int bin = 0; bin < numBins; bin++)
            {
                float d = magnitude[frame, bin] - magnitude[frame - 1, bin];
                if (d > 0)
                    diff += d * d;
            }
            energy[frame] = MathF.Sqrt(diff);
        }

        // Normalize
        float maxEnergy = energy.Max();
        if (maxEnergy > 0)
        {
            for (int i = 0; i < numFrames; i++)
                energy[i] /= maxEnergy;
        }

        return energy;
    }

    private (float[,] harmonic, float[,] percussive) ComputeHPSeparation(float[,] magnitude)
    {
        int numFrames = magnitude.GetLength(0);
        int numBins = magnitude.GetLength(1);

        var harmonic = new float[numFrames, numBins];
        var percussive = new float[numFrames, numBins];

        // Median filtering for harmonic/percussive separation
        int harmonicWidth = 17; // Time direction
        int percussiveWidth = 17; // Frequency direction

        for (int frame = 0; frame < numFrames; frame++)
        {
            for (int bin = 0; bin < numBins; bin++)
            {
                // Harmonic: median filter along time
                var timeValues = new List<float>();
                for (int t = Math.Max(0, frame - harmonicWidth / 2);
                     t < Math.Min(numFrames, frame + harmonicWidth / 2 + 1); t++)
                {
                    timeValues.Add(magnitude[t, bin]);
                }
                timeValues.Sort();
                float harmonicMedian = timeValues[timeValues.Count / 2];

                // Percussive: median filter along frequency
                var freqValues = new List<float>();
                for (int f = Math.Max(0, bin - percussiveWidth / 2);
                     f < Math.Min(numBins, bin + percussiveWidth / 2 + 1); f++)
                {
                    freqValues.Add(magnitude[frame, f]);
                }
                freqValues.Sort();
                float percussiveMedian = freqValues[freqValues.Count / 2];

                // Wiener-like masking
                float hEnergy = harmonicMedian * harmonicMedian;
                float pEnergy = percussiveMedian * percussiveMedian;
                float total = hEnergy + pEnergy + 1e-10f;

                harmonic[frame, bin] = hEnergy / total;
                percussive[frame, bin] = pEnergy / total;
            }
        }

        return (harmonic, percussive);
    }

    private (float[,] W, float[,] H) NMFDecompose(
        float[,] V,
        int components,
        int iterations,
        CancellationToken cancellationToken)
    {
        int numFrames = V.GetLength(0);
        int numBins = V.GetLength(1);

        // Initialize W and H with random values
        var random = new Random(42);
        var W = new float[numBins, components];
        var H = new float[components, numFrames];

        for (int i = 0; i < numBins; i++)
            for (int j = 0; j < components; j++)
                W[i, j] = (float)random.NextDouble() + 0.1f;

        for (int i = 0; i < components; i++)
            for (int j = 0; j < numFrames; j++)
                H[i, j] = (float)random.NextDouble() + 0.1f;

        // Multiplicative update rules
        for (int iter = 0; iter < iterations; iter++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Update H
            var WtV = new float[components, numFrames];
            var WtWH = new float[components, numFrames];

            for (int k = 0; k < components; k++)
            {
                for (int t = 0; t < numFrames; t++)
                {
                    float sumWtV = 0, sumWtWH = 0;
                    for (int f = 0; f < numBins; f++)
                    {
                        sumWtV += W[f, k] * V[t, f];

                        float wh = 0;
                        for (int kk = 0; kk < components; kk++)
                            wh += W[f, kk] * H[kk, t];
                        sumWtWH += W[f, k] * wh;
                    }
                    WtV[k, t] = sumWtV;
                    WtWH[k, t] = sumWtWH + 1e-10f;
                }
            }

            for (int k = 0; k < components; k++)
                for (int t = 0; t < numFrames; t++)
                    H[k, t] *= WtV[k, t] / WtWH[k, t];

            // Update W
            var VHt = new float[numBins, components];
            var WHHt = new float[numBins, components];

            for (int f = 0; f < numBins; f++)
            {
                for (int k = 0; k < components; k++)
                {
                    float sumVHt = 0, sumWHHt = 0;
                    for (int t = 0; t < numFrames; t++)
                    {
                        sumVHt += V[t, f] * H[k, t];

                        float wh = 0;
                        for (int kk = 0; kk < components; kk++)
                            wh += W[f, kk] * H[kk, t];
                        sumWHHt += wh * H[k, t];
                    }
                    VHt[f, k] = sumVHt;
                    WHHt[f, k] = sumWHHt + 1e-10f;
                }
            }

            for (int f = 0; f < numBins; f++)
                for (int k = 0; k < components; k++)
                    W[f, k] *= VHt[f, k] / WHHt[f, k];
        }

        return (W, H);
    }

    private void ClassifyComponents(
        float[,] W,
        float[,] H,
        float[,] magnitude,
        int sampleRate,
        Dictionary<StemType, float[,]> masks)
    {
        int numBins = W.GetLength(0);
        int components = W.GetLength(1);
        int numFrames = H.GetLength(1);
        float freqPerBin = (float)sampleRate / _fftSize;

        // Classify each NMF component based on spectral characteristics
        for (int k = 0; k < components; k++)
        {
            // Calculate centroid and bandwidth for this component
            float weightedSum = 0, totalWeight = 0;
            for (int f = 0; f < numBins; f++)
            {
                float freq = f * freqPerBin;
                weightedSum += W[f, k] * freq;
                totalWeight += W[f, k];
            }
            float centroid = totalWeight > 0 ? weightedSum / totalWeight : 0;

            // Calculate temporal variance (higher = more percussive)
            float meanActivation = 0;
            for (int t = 0; t < numFrames; t++)
                meanActivation += H[k, t];
            meanActivation /= numFrames;

            float variance = 0;
            for (int t = 0; t < numFrames; t++)
            {
                float diff = H[k, t] - meanActivation;
                variance += diff * diff;
            }
            variance /= numFrames;

            // Classify and add to appropriate mask
            StemType classification;
            float confidence;

            if (centroid < BassHighCutoff)
            {
                classification = StemType.Bass;
                confidence = 0.7f;
            }
            else if (variance > 0.5f * meanActivation * meanActivation)
            {
                classification = StemType.Drums;
                confidence = 0.6f;
            }
            else if (centroid > VocalLowCutoff && centroid < VocalHighCutoff)
            {
                classification = StemType.Vocals;
                confidence = 0.5f;
            }
            else
            {
                classification = StemType.Other;
                confidence = 0.4f;
            }

            // Add this component's contribution to the mask
            for (int t = 0; t < numFrames; t++)
            {
                for (int f = 0; f < numBins; f++)
                {
                    float contribution = W[f, k] * H[k, t] * confidence;
                    float currentMag = magnitude[t, f];
                    if (currentMag > 1e-10f)
                    {
                        masks[classification][t, f] += contribution / currentMag;
                    }
                }
            }
        }
    }

    private float[,] ApplyMask(float[,] magnitude, float[,] mask)
    {
        int numFrames = magnitude.GetLength(0);
        int numBins = magnitude.GetLength(1);
        var result = new float[numFrames, numBins];

        for (int t = 0; t < numFrames; t++)
        {
            for (int f = 0; f < numBins; f++)
            {
                result[t, f] = magnitude[t, f] * mask[t, f];
            }
        }

        return result;
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

    private void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        int bits = (int)Math.Log2(n);

        // Bit-reversal permutation
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
                (data[i], data[j]) = (data[j], data[i]);
        }

        // Cooley-Tukey iterative FFT
        for (int size = 2; size <= n; size *= 2)
        {
            double angle = (inverse ? 2 : -2) * Math.PI / size;
            var wn = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int start = 0; start < n; start += size)
            {
                var w = Complex.One;
                for (int k = 0; k < size / 2; k++)
                {
                    var t = w * data[start + k + size / 2];
                    var u = data[start + k];
                    data[start + k] = u + t;
                    data[start + k + size / 2] = u - t;
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
}
