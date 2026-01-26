// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace MusicEngine.Core.Warp;

/// <summary>
/// Quality settings for elastic audio processing.
/// Affects FFT size, latency, and time-stretch quality.
/// </summary>
public enum ElasticAudioQuality
{
    /// <summary>
    /// Fast mode with smaller FFT size (1024). Lower latency, lower quality.
    /// Good for real-time preview or monitoring.
    /// </summary>
    Fast,

    /// <summary>
    /// Medium quality with balanced FFT size (2048). Balanced latency and quality.
    /// Suitable for most applications.
    /// </summary>
    Medium,

    /// <summary>
    /// High quality mode with larger FFT size (4096). Higher latency, best quality.
    /// Recommended for final rendering.
    /// </summary>
    High
}

/// <summary>
/// Performs real-time audio time-stretching based on warp markers.
/// Uses a phase vocoder (STFT-based) for high-quality time manipulation.
/// Implements ISampleProvider for direct integration with NAudio pipelines.
/// </summary>
/// <remarks>
/// The phase vocoder algorithm:
/// 1. STFT Analysis: Window -> FFT -> Extract magnitude and phase
/// 2. Phase Tracking: Calculate instantaneous frequency from phase differences
/// 3. Variable Stretch: Adjust synthesis hop size based on local warp region stretch ratio
/// 4. Phase Accumulation: Maintain phase coherence across frames
/// 5. STFT Synthesis: Reconstruct time domain signal via IFFT and overlap-add
/// </remarks>
public class ElasticAudio : ISampleProvider, IDisposable
{
    private readonly float[] _sourceAudio;
    private readonly AudioWarpProcessor _warpProcessor;
    private readonly int _channels;
    private readonly int _sampleRate;

    // Phase vocoder parameters
    private int _fftSize;
    private int _analysisHopSize;
    private int _overlapFactor;

    // Phase vocoder working buffers (per channel)
    private float[][] _inputBuffer = null!;
    private float[][] _outputBuffer = null!;
    private int[] _inputWritePos = null!;
    private int[] _outputWritePos = null!;
    private int[] _outputReadPos = null!;

    // FFT data (per channel)
    private Complex[][] _fftBuffer = null!;
    private float[][] _lastInputPhase = null!;
    private float[][] _accumulatedPhase = null!;

    // Windows
    private float[] _analysisWindow = null!;
    private float[] _synthesisWindow = null!;

    // Transient detection (per channel)
    private float[][] _previousEnergy = null!;
    private const int TransientHistorySize = 4;
    private int _transientHistoryIndex;

    // Playback state
    private long _currentWarpedSample;
    private double _currentWarpedBeat;
    private double _playbackSpeed = 1.0;
    private int _samplesUntilNextAnalysis;
    private bool _initialized;
    private bool _disposed;

    // Resampling buffer for variable stretch
    private float[][] _resampleBuffer = null!;
    private int[] _resampleWritePos = null!;
    private float[] _resampleReadPos = null!;

    /// <summary>
    /// Gets the wave format of the audio output.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the warp processor managing warp markers and regions.
    /// </summary>
    public AudioWarpProcessor WarpProcessor => _warpProcessor;

    /// <summary>
    /// Gets or sets whether warping is enabled.
    /// When disabled, audio plays back without time-stretching.
    /// </summary>
    public bool WarpEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether transient preservation is enabled.
    /// When enabled, resets phase at detected transients for sharper attacks.
    /// </summary>
    public bool PreserveTransients { get; set; } = true;

    /// <summary>
    /// Gets or sets the current playback position in warped beats.
    /// </summary>
    public double CurrentBeatPosition
    {
        get => _currentWarpedBeat;
        set => Seek(value);
    }

    /// <summary>
    /// Gets or sets the current playback position in warped samples.
    /// </summary>
    public long CurrentSamplePosition
    {
        get => _currentWarpedSample;
        set => SeekToSample(value);
    }

    /// <summary>
    /// Gets or sets the playback speed multiplier (1.0 = normal, affects tempo not pitch).
    /// Range: 0.1 to 4.0
    /// </summary>
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = Math.Clamp(value, 0.1, 4.0);
    }

    /// <summary>
    /// Gets or sets the quality setting for the phase vocoder.
    /// Changes to quality require reinitialization.
    /// </summary>
    public ElasticAudioQuality Quality { get; set; } = ElasticAudioQuality.Medium;

    /// <summary>
    /// Gets the total length of the warped audio in samples.
    /// </summary>
    public long TotalWarpedSamples => _warpProcessor.TotalWarpedSamples;

    /// <summary>
    /// Gets the total length of the original audio in samples.
    /// </summary>
    public long TotalOriginalSamples => _warpProcessor.TotalOriginalSamples;

    /// <summary>
    /// Gets the BPM used for beat calculations.
    /// </summary>
    public double Bpm => _warpProcessor.Bpm;

    /// <summary>
    /// Creates a new ElasticAudio processor.
    /// </summary>
    /// <param name="sourceAudio">Source audio samples (interleaved if stereo).</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="warpProcessor">Warp processor with markers and regions.</param>
    public ElasticAudio(float[] sourceAudio, int sampleRate, int channels, AudioWarpProcessor warpProcessor)
    {
        _sourceAudio = sourceAudio ?? throw new ArgumentNullException(nameof(sourceAudio));
        _warpProcessor = warpProcessor ?? throw new ArgumentNullException(nameof(warpProcessor));
        _channels = channels;
        _sampleRate = sampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        // Ensure warp processor has correct settings
        if (_warpProcessor.SampleRate != sampleRate)
            _warpProcessor.SampleRate = sampleRate;

        long sourceSampleCount = sourceAudio.Length / channels;
        if (_warpProcessor.TotalOriginalSamples != sourceSampleCount)
            _warpProcessor.TotalOriginalSamples = sourceSampleCount;
    }

    /// <summary>
    /// Creates a new ElasticAudio processor with a new WarpProcessor.
    /// </summary>
    /// <param name="sourceAudio">Source audio samples (interleaved if stereo).</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="bpm">Project tempo in BPM.</param>
    public ElasticAudio(float[] sourceAudio, int sampleRate, int channels, double bpm)
        : this(sourceAudio, sampleRate, channels,
               new AudioWarpProcessor(sampleRate, bpm, sourceAudio.Length / channels))
    {
    }

    /// <summary>
    /// Initializes the phase vocoder buffers based on quality setting.
    /// </summary>
    private void Initialize()
    {
        // Set FFT size based on quality
        (_fftSize, _overlapFactor) = Quality switch
        {
            ElasticAudioQuality.Fast => (1024, 4),
            ElasticAudioQuality.Medium => (2048, 4),
            ElasticAudioQuality.High => (4096, 4),
            _ => (2048, 4)
        };

        _analysisHopSize = _fftSize / _overlapFactor;

        // Calculate max output buffer size (for slowest stretch 0.25x = 4x longer)
        int maxOutputSize = _fftSize * 8;

        // Allocate per-channel buffers
        _inputBuffer = new float[_channels][];
        _outputBuffer = new float[_channels][];
        _inputWritePos = new int[_channels];
        _outputWritePos = new int[_channels];
        _outputReadPos = new int[_channels];
        _fftBuffer = new Complex[_channels][];
        _lastInputPhase = new float[_channels][];
        _accumulatedPhase = new float[_channels][];
        _previousEnergy = new float[_channels][];
        _resampleBuffer = new float[_channels][];
        _resampleWritePos = new int[_channels];
        _resampleReadPos = new float[_channels];

        for (int ch = 0; ch < _channels; ch++)
        {
            _inputBuffer[ch] = new float[_fftSize * 2];
            _outputBuffer[ch] = new float[maxOutputSize];
            _inputWritePos[ch] = 0;
            _outputWritePos[ch] = 0;
            _outputReadPos[ch] = 0;

            _fftBuffer[ch] = new Complex[_fftSize];
            _lastInputPhase[ch] = new float[_fftSize / 2 + 1];
            _accumulatedPhase[ch] = new float[_fftSize / 2 + 1];
            _previousEnergy[ch] = new float[TransientHistorySize];

            _resampleBuffer[ch] = new float[maxOutputSize];
            _resampleWritePos[ch] = 0;
            _resampleReadPos[ch] = 0f;
        }

        // Generate Hann windows
        _analysisWindow = CreateHannWindow(_fftSize);
        _synthesisWindow = CreateHannWindow(_fftSize);

        _samplesUntilNextAnalysis = 0;
        _transientHistoryIndex = 0;
        _initialized = true;
    }

    /// <summary>
    /// Seeks to a specific beat position.
    /// </summary>
    /// <param name="beatPosition">Target position in warped beats.</param>
    public void Seek(double beatPosition)
    {
        double seconds = beatPosition * 60.0 / _warpProcessor.Bpm;
        long samples = (long)(seconds * _sampleRate);
        SeekToSample(samples);
    }

    /// <summary>
    /// Seeks to a specific sample position.
    /// </summary>
    /// <param name="samplePosition">Target position in warped samples.</param>
    public void SeekToSample(long samplePosition)
    {
        _currentWarpedSample = Math.Clamp(samplePosition, 0, TotalWarpedSamples);
        _currentWarpedBeat = (double)_currentWarpedSample / _sampleRate * _warpProcessor.Bpm / 60.0;

        // Reset phase vocoder state on seek
        if (_initialized)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                Array.Clear(_inputBuffer[ch], 0, _inputBuffer[ch].Length);
                Array.Clear(_outputBuffer[ch], 0, _outputBuffer[ch].Length);
                Array.Clear(_lastInputPhase[ch], 0, _lastInputPhase[ch].Length);
                Array.Clear(_accumulatedPhase[ch], 0, _accumulatedPhase[ch].Length);
                Array.Clear(_resampleBuffer[ch], 0, _resampleBuffer[ch].Length);
                _inputWritePos[ch] = 0;
                _outputWritePos[ch] = 0;
                _outputReadPos[ch] = 0;
                _resampleWritePos[ch] = 0;
                _resampleReadPos[ch] = 0f;
            }
            _samplesUntilNextAnalysis = 0;
        }
    }

    /// <summary>
    /// Reads samples with time-stretching applied based on warp markers.
    /// </summary>
    /// <param name="buffer">Output buffer for samples.</param>
    /// <param name="offset">Offset in buffer to start writing.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>Number of samples actually read.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed)
            return 0;

        if (!_initialized)
            Initialize();

        // If warping is disabled, read directly from source
        if (!WarpEnabled)
            return ReadDirect(buffer, offset, count);

        int samplesGenerated = 0;
        int framesRequested = count / _channels;

        while (samplesGenerated < count && _currentWarpedSample < TotalWarpedSamples)
        {
            // Get the current region and its stretch ratio
            var region = _warpProcessor.GetRegionAtWarpedPosition(_currentWarpedSample);
            double stretchRatio = region?.StretchRatio ?? 1.0;

            // Calculate synthesis hop size based on stretch ratio
            // stretchRatio > 1.0 means audio sped up (original plays faster)
            // For time stretch: synthesis_hop = analysis_hop * (1/stretchRatio)
            int synthesisHopSize = Math.Max(1, (int)(_analysisHopSize / stretchRatio));

            // Process one frame worth of samples
            int framesToProcess = Math.Min(
                (count - samplesGenerated) / _channels,
                _analysisHopSize);

            int processed = ProcessWithStretch(
                buffer,
                offset + samplesGenerated,
                framesToProcess * _channels,
                stretchRatio,
                synthesisHopSize);

            if (processed == 0)
                break;

            samplesGenerated += processed;

            // Advance warped position
            int framesProcessed = processed / _channels;
            _currentWarpedSample += (long)(framesProcessed * _playbackSpeed);
            _currentWarpedBeat = (double)_currentWarpedSample / _sampleRate * _warpProcessor.Bpm / 60.0;
        }

        return samplesGenerated;
    }

    /// <summary>
    /// Reads audio directly without time-stretching.
    /// </summary>
    private int ReadDirect(float[] buffer, int offset, int count)
    {
        int framesRequested = count / _channels;
        int samplesRead = 0;

        for (int i = 0; i < framesRequested && _currentWarpedSample < TotalWarpedSamples; i++)
        {
            // Map warped position to original
            long originalSample = _warpProcessor.WarpedToOriginal(_currentWarpedSample);

            for (int ch = 0; ch < _channels; ch++)
            {
                int sourceIndex = (int)(originalSample * _channels + ch);
                float sample = 0f;

                if (sourceIndex >= 0 && sourceIndex < _sourceAudio.Length)
                    sample = _sourceAudio[sourceIndex];

                buffer[offset + i * _channels + ch] = sample;
            }

            samplesRead += _channels;
            _currentWarpedSample += (long)_playbackSpeed;
        }

        _currentWarpedBeat = (double)_currentWarpedSample / _sampleRate * _warpProcessor.Bpm / 60.0;
        return samplesRead;
    }

    /// <summary>
    /// Processes audio with the phase vocoder for time-stretching.
    /// </summary>
    private int ProcessWithStretch(float[] buffer, int offset, int count, double stretchRatio, int synthesisHopSize)
    {
        int samplesWritten = 0;

        for (int i = 0; i < count; i += _channels)
        {
            // Get the original position for current warped position
            double originalPosPrecise = _warpProcessor.WarpedToOriginalPrecise(_currentWarpedSample + i / _channels);
            long originalPos = (long)originalPosPrecise;
            double frac = originalPosPrecise - originalPos;

            // Feed input samples to phase vocoder
            for (int ch = 0; ch < _channels; ch++)
            {
                // Get interpolated input sample from source
                int srcIdx0 = (int)(originalPos * _channels + ch);
                int srcIdx1 = srcIdx0 + _channels;

                float sample0 = (srcIdx0 >= 0 && srcIdx0 < _sourceAudio.Length)
                    ? _sourceAudio[srcIdx0] : 0f;
                float sample1 = (srcIdx1 >= 0 && srcIdx1 < _sourceAudio.Length)
                    ? _sourceAudio[srcIdx1] : 0f;

                float inputSample = (float)(sample0 * (1.0 - frac) + sample1 * frac);

                // Write to circular input buffer
                _inputBuffer[ch][_inputWritePos[ch]] = inputSample;
                _inputWritePos[ch] = (_inputWritePos[ch] + 1) % _inputBuffer[ch].Length;
            }

            _samplesUntilNextAnalysis--;

            // Time to process a new analysis frame?
            if (_samplesUntilNextAnalysis <= 0)
            {
                _samplesUntilNextAnalysis = _analysisHopSize;

                // Detect transients before processing
                bool isTransient = false;
                if (PreserveTransients)
                    isTransient = DetectTransient(0);

                // Process phase vocoder for each channel
                for (int ch = 0; ch < _channels; ch++)
                {
                    ProcessPhaseVocoderFrame(ch, synthesisHopSize, isTransient);
                }
            }

            // Read from resample buffer and write to output
            for (int ch = 0; ch < _channels; ch++)
            {
                float outputSample = 0f;

                int resampleAvailable = (_resampleWritePos[ch] - (int)_resampleReadPos[ch] +
                                        _resampleBuffer[ch].Length) % _resampleBuffer[ch].Length;

                if (resampleAvailable > 1)
                {
                    // Linear interpolation for smooth resampling
                    int readPosInt = (int)_resampleReadPos[ch];
                    float readFrac = _resampleReadPos[ch] - readPosInt;

                    int pos0 = readPosInt % _resampleBuffer[ch].Length;
                    int pos1 = (readPosInt + 1) % _resampleBuffer[ch].Length;

                    outputSample = _resampleBuffer[ch][pos0] * (1f - readFrac) +
                                   _resampleBuffer[ch][pos1] * readFrac;

                    // Advance read position based on stretch ratio
                    _resampleReadPos[ch] += (float)(1.0 / stretchRatio);

                    while (_resampleReadPos[ch] >= _resampleBuffer[ch].Length)
                        _resampleReadPos[ch] -= _resampleBuffer[ch].Length;
                }

                buffer[offset + i + ch] = outputSample;
            }

            samplesWritten += _channels;
        }

        return samplesWritten;
    }

    /// <summary>
    /// Processes one phase vocoder frame for a single channel.
    /// </summary>
    private void ProcessPhaseVocoderFrame(int channel, int synthesisHopSize, bool isTransient)
    {
        int halfSize = _fftSize / 2;
        float expectedPhaseDiff = 2f * MathF.PI * _analysisHopSize / _fftSize;

        // Copy windowed input to FFT buffer
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) %
                       _inputBuffer[channel].Length;

        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float windowedSample = _inputBuffer[channel][readPos] * _analysisWindow[i];
            _fftBuffer[channel][i] = new Complex(windowedSample, 0f);
        }

        // Forward FFT
        FFT(_fftBuffer[channel], false);

        // Analysis: Calculate magnitude and true frequency for each bin
        float[] magnitude = new float[halfSize + 1];
        float[] trueFreq = new float[halfSize + 1];

        for (int k = 0; k <= halfSize; k++)
        {
            float real = _fftBuffer[channel][k].Real;
            float imag = _fftBuffer[channel][k].Imag;

            magnitude[k] = MathF.Sqrt(real * real + imag * imag);
            float phase = MathF.Atan2(imag, real);

            // Calculate phase difference from last analysis frame
            float phaseDiff = phase - _lastInputPhase[channel][k];
            _lastInputPhase[channel][k] = phase;

            // Remove expected phase increment based on analysis hop
            phaseDiff -= k * expectedPhaseDiff;

            // Wrap to [-PI, PI]
            phaseDiff = WrapPhase(phaseDiff);

            // Calculate true frequency as deviation from bin center frequency
            float deviation = phaseDiff * _overlapFactor / (2f * MathF.PI);
            trueFreq[k] = k + deviation;
        }

        // Synthesis: Accumulate phase based on synthesis hop size
        float synthesisExpectedPhase = 2f * MathF.PI * synthesisHopSize / _fftSize;

        // Handle transients by resetting phase
        if (isTransient)
        {
            for (int k = 0; k <= halfSize; k++)
            {
                _accumulatedPhase[channel][k] = _lastInputPhase[channel][k];
            }
        }

        // Build output spectrum
        for (int k = 0; k <= halfSize; k++)
        {
            // Accumulate phase based on true frequency and synthesis hop
            float phaseDelta = trueFreq[k] * synthesisExpectedPhase;
            _accumulatedPhase[channel][k] += phaseDelta;
            _accumulatedPhase[channel][k] = WrapPhase(_accumulatedPhase[channel][k]);

            float mag = magnitude[k];
            float ph = _accumulatedPhase[channel][k];

            _fftBuffer[channel][k] = new Complex(mag * MathF.Cos(ph), mag * MathF.Sin(ph));

            // Mirror for negative frequencies (conjugate symmetric)
            if (k > 0 && k < halfSize)
            {
                _fftBuffer[channel][_fftSize - k] = new Complex(
                    mag * MathF.Cos(ph),
                    -mag * MathF.Sin(ph));
            }
        }

        // Inverse FFT
        FFT(_fftBuffer[channel], true);

        // Overlap-add to resample buffer at synthesis hop positions
        float normFactor = 1f / (_overlapFactor * 0.5f);
        for (int i = 0; i < _fftSize; i++)
        {
            int outputPos = (_resampleWritePos[channel] + i) % _resampleBuffer[channel].Length;
            _resampleBuffer[channel][outputPos] +=
                _fftBuffer[channel][i].Real * _synthesisWindow[i] * normFactor;
        }

        // Clear the region we just wrote over from previous overlap
        int clearStart = (_resampleWritePos[channel] + _fftSize) % _resampleBuffer[channel].Length;
        for (int i = 0; i < synthesisHopSize && i < _fftSize; i++)
        {
            int clearPos = (clearStart + i) % _resampleBuffer[channel].Length;
            _resampleBuffer[channel][clearPos] = 0f;
        }

        // Advance write position by synthesis hop
        _resampleWritePos[channel] =
            (_resampleWritePos[channel] + synthesisHopSize) % _resampleBuffer[channel].Length;
    }

    /// <summary>
    /// Detects if the current frame contains a transient (attack).
    /// </summary>
    private bool DetectTransient(int channel)
    {
        int readStart = (_inputWritePos[channel] - _fftSize + _inputBuffer[channel].Length) %
                       _inputBuffer[channel].Length;
        float energy = 0f;

        for (int i = 0; i < _fftSize; i++)
        {
            int readPos = (readStart + i) % _inputBuffer[channel].Length;
            float sample = _inputBuffer[channel][readPos];
            energy += sample * sample;
        }

        energy = MathF.Sqrt(energy / _fftSize);

        // Calculate average of previous energies
        float avgEnergy = 0f;
        for (int i = 0; i < TransientHistorySize; i++)
        {
            avgEnergy += _previousEnergy[channel][i];
        }
        avgEnergy /= TransientHistorySize;

        // Store current energy in history
        _previousEnergy[channel][_transientHistoryIndex] = energy;
        _transientHistoryIndex = (_transientHistoryIndex + 1) % TransientHistorySize;

        // Transient detection: significant energy increase
        const float threshold = 2.0f;
        const float minEnergy = 0.001f;

        return energy > minEnergy && avgEnergy > 0.0001f && (energy / avgEnergy) > threshold;
    }

    /// <summary>
    /// Creates a Hann window of the specified size.
    /// </summary>
    private static float[] CreateHannWindow(int size)
    {
        float[] window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (size - 1)));
        }
        return window;
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
    /// Gets all warp markers.
    /// </summary>
    public IReadOnlyList<WarpMarker> GetMarkers()
    {
        return _warpProcessor.Markers;
    }

    /// <summary>
    /// Gets all warp regions.
    /// </summary>
    public IReadOnlyList<WarpRegion> GetRegions()
    {
        return _warpProcessor.Regions;
    }

    /// <summary>
    /// Adds a warp marker at the specified positions.
    /// </summary>
    public WarpMarker AddMarker(long originalPositionSamples, long warpedPositionSamples,
                                 WarpMarkerType markerType = WarpMarkerType.User)
    {
        return _warpProcessor.AddMarker(originalPositionSamples, warpedPositionSamples, markerType);
    }

    /// <summary>
    /// Adds a warp marker at the specified beat positions.
    /// </summary>
    public WarpMarker AddMarkerAtBeats(double originalBeats, double warpedBeats,
                                        WarpMarkerType markerType = WarpMarkerType.User)
    {
        return _warpProcessor.AddMarkerAtBeats(originalBeats, warpedBeats, markerType);
    }

    /// <summary>
    /// Removes a warp marker.
    /// </summary>
    public bool RemoveMarker(WarpMarker marker)
    {
        return _warpProcessor.RemoveMarker(marker);
    }

    /// <summary>
    /// Moves a warp marker to a new warped position.
    /// </summary>
    public bool MoveMarker(WarpMarker marker, long newWarpedPositionSamples)
    {
        return _warpProcessor.MoveMarker(marker, newWarpedPositionSamples);
    }

    /// <summary>
    /// Resets all warp markers to their original positions.
    /// </summary>
    public void ResetWarp()
    {
        _warpProcessor.ResetWarp();
    }

    /// <summary>
    /// Gets the stretch ratio at a specific warped position.
    /// </summary>
    public double GetStretchRatioAt(long warpedPositionSamples)
    {
        return _warpProcessor.GetStretchRatioAt(warpedPositionSamples);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Clear all buffers
        if (_inputBuffer != null)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                _inputBuffer[ch] = null!;
                _outputBuffer[ch] = null!;
                _fftBuffer[ch] = null!;
                _lastInputPhase[ch] = null!;
                _accumulatedPhase[ch] = null!;
                _previousEnergy[ch] = null!;
                _resampleBuffer[ch] = null!;
            }
        }

        _analysisWindow = null!;
        _synthesisWindow = null!;
    }

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
