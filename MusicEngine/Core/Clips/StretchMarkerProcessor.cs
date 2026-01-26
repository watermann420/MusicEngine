//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Processes multiple stretch markers within an audio clip for variable time-stretching.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Clips;

/// <summary>
/// Represents a stretch marker that defines a time anchor point for variable time-stretching.
/// </summary>
public class StretchMarker : IEquatable<StretchMarker>, IComparable<StretchMarker>
{
    /// <summary>Unique identifier for this stretch marker.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Position in the original source audio (in samples).</summary>
    public long OriginalPositionSamples { get; set; }

    /// <summary>Target position in the stretched output (in samples).</summary>
    public long TargetPositionSamples { get; set; }

    /// <summary>Optional label for this marker.</summary>
    public string? Label { get; set; }

    /// <summary>Whether this marker is locked from editing.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether this is an anchor point (start or end boundary).</summary>
    public bool IsAnchor { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new stretch marker with default values.
    /// </summary>
    public StretchMarker()
    {
    }

    /// <summary>
    /// Creates a new stretch marker at the specified positions.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <param name="targetPositionSamples">Target position in output (samples).</param>
    /// <param name="isAnchor">Whether this is a boundary anchor point.</param>
    public StretchMarker(long originalPositionSamples, long targetPositionSamples, bool isAnchor = false)
    {
        OriginalPositionSamples = originalPositionSamples;
        TargetPositionSamples = targetPositionSamples;
        IsAnchor = isAnchor;
    }

    /// <summary>
    /// Gets the original position in seconds.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <returns>Position in seconds.</returns>
    public double GetOriginalPositionSeconds(int sampleRate)
    {
        return (double)OriginalPositionSamples / sampleRate;
    }

    /// <summary>
    /// Gets the target position in seconds.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <returns>Position in seconds.</returns>
    public double GetTargetPositionSeconds(int sampleRate)
    {
        return (double)TargetPositionSamples / sampleRate;
    }

    /// <summary>
    /// Gets the original position in beats.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <returns>Position in beats.</returns>
    public double GetOriginalPositionBeats(int sampleRate, double bpm)
    {
        return GetOriginalPositionSeconds(sampleRate) * bpm / 60.0;
    }

    /// <summary>
    /// Gets the target position in beats.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <returns>Position in beats.</returns>
    public double GetTargetPositionBeats(int sampleRate, double bpm)
    {
        return GetTargetPositionSeconds(sampleRate) * bpm / 60.0;
    }

    /// <summary>
    /// Calculates the stretch ratio between this marker and a previous marker.
    /// </summary>
    /// <param name="previousMarker">The previous marker, or null if this is the first.</param>
    /// <returns>Stretch ratio (1.0 = no stretch, &gt;1 = sped up, &lt;1 = slowed down).</returns>
    public double CalculateStretchRatio(StretchMarker? previousMarker)
    {
        if (previousMarker == null)
            return 1.0;

        long originalDelta = OriginalPositionSamples - previousMarker.OriginalPositionSamples;
        long targetDelta = TargetPositionSamples - previousMarker.TargetPositionSamples;

        if (originalDelta <= 0 || targetDelta <= 0)
            return 1.0;

        return (double)originalDelta / targetDelta;
    }

    public bool Equals(StretchMarker? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is StretchMarker other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(StretchMarker? other)
    {
        if (other == null) return 1;
        return OriginalPositionSamples.CompareTo(other.OriginalPositionSamples);
    }

    public static bool operator ==(StretchMarker? left, StretchMarker? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(StretchMarker? left, StretchMarker? right) => !(left == right);

    public override string ToString() =>
        $"StretchMarker: {OriginalPositionSamples} -> {TargetPositionSamples} ({(IsAnchor ? "Anchor" : "Normal")})";
}

/// <summary>
/// Quality mode for the phase vocoder time-stretch algorithm.
/// </summary>
public enum StretchQuality
{
    /// <summary>Fast mode with smaller FFT (1024). Lower latency, lower quality.</summary>
    Fast,

    /// <summary>Normal mode with medium FFT (2048). Balanced quality and performance.</summary>
    Normal,

    /// <summary>High quality mode with larger FFT (4096). Best quality, higher latency.</summary>
    High
}

/// <summary>
/// Processes audio with multiple stretch markers for variable time-stretching within a clip.
/// Uses phase vocoder algorithm to preserve pitch while changing tempo.
/// </summary>
public class StretchMarkerProcessor
{
    private readonly List<StretchMarker> _markers = new();
    private readonly object _lock = new();

    // FFT configuration
    private int _fftSize;
    private int _hopSize;
    private int _overlapFactor;

    // Processing buffers
    private float[]? _inputBuffer;
    private float[]? _outputBuffer;
    private float[]? _analysisWindow;
    private float[]? _synthesisWindow;
    private Complex[]? _fftBuffer;
    private float[]? _lastPhase;
    private float[]? _accumulatedPhase;

    // State
    private bool _initialized;
    private StretchQuality _quality = StretchQuality.Normal;

    /// <summary>Audio sample rate.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of audio channels.</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Total length of the original audio in samples.</summary>
    public long TotalOriginalSamples { get; set; }

    /// <summary>Total length of the stretched output in samples.</summary>
    public long TotalTargetSamples => _markers.Count > 0
        ? _markers.Max(m => m.TargetPositionSamples)
        : TotalOriginalSamples;

    /// <summary>Whether to preserve pitch during stretching.</summary>
    public bool PreservePitch { get; set; } = true;

    /// <summary>Gets the quality mode for the stretch algorithm.</summary>
    public StretchQuality Quality
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

    /// <summary>Gets the number of stretch markers.</summary>
    public int MarkerCount => _markers.Count;

    /// <summary>Gets all stretch markers sorted by original position.</summary>
    public IReadOnlyList<StretchMarker> Markers
    {
        get
        {
            lock (_lock)
            {
                return _markers.OrderBy(m => m.OriginalPositionSamples).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Event raised when markers change.</summary>
    public event EventHandler? MarkersChanged;

    /// <summary>
    /// Creates a new stretch marker processor.
    /// </summary>
    public StretchMarkerProcessor()
    {
    }

    /// <summary>
    /// Creates a new stretch marker processor with specified parameters.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="totalSamples">Total samples in the original audio.</param>
    public StretchMarkerProcessor(int sampleRate, int channels, long totalSamples)
    {
        SampleRate = sampleRate;
        Channels = channels;
        TotalOriginalSamples = totalSamples;

        // Add start and end anchor markers
        AddMarker(0, 0, true);
        AddMarker(totalSamples, totalSamples, true);
    }

    /// <summary>
    /// Adds a stretch marker at the specified positions.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <param name="targetPositionSamples">Target position in output (samples).</param>
    /// <param name="isAnchor">Whether this is an anchor point.</param>
    /// <returns>The created stretch marker.</returns>
    public StretchMarker AddMarker(long originalPositionSamples, long targetPositionSamples, bool isAnchor = false)
    {
        var marker = new StretchMarker(originalPositionSamples, targetPositionSamples, isAnchor);

        lock (_lock)
        {
            // Check for duplicate position
            if (_markers.Any(m => m.OriginalPositionSamples == originalPositionSamples))
            {
                throw new InvalidOperationException("A marker already exists at this original position.");
            }

            _markers.Add(marker);
        }

        MarkersChanged?.Invoke(this, EventArgs.Empty);
        return marker;
    }

    /// <summary>
    /// Adds a stretch marker at positions specified in seconds.
    /// </summary>
    /// <param name="originalPositionSeconds">Position in original audio (seconds).</param>
    /// <param name="targetPositionSeconds">Target position in output (seconds).</param>
    /// <param name="isAnchor">Whether this is an anchor point.</param>
    /// <returns>The created stretch marker.</returns>
    public StretchMarker AddMarkerAtSeconds(double originalPositionSeconds, double targetPositionSeconds, bool isAnchor = false)
    {
        long originalSamples = (long)(originalPositionSeconds * SampleRate);
        long targetSamples = (long)(targetPositionSeconds * SampleRate);
        return AddMarker(originalSamples, targetSamples, isAnchor);
    }

    /// <summary>
    /// Adds a stretch marker at positions specified in beats.
    /// </summary>
    /// <param name="originalPositionBeats">Position in original audio (beats).</param>
    /// <param name="targetPositionBeats">Target position in output (beats).</param>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <param name="isAnchor">Whether this is an anchor point.</param>
    /// <returns>The created stretch marker.</returns>
    public StretchMarker AddMarkerAtBeats(double originalPositionBeats, double targetPositionBeats, double bpm, bool isAnchor = false)
    {
        double originalSeconds = originalPositionBeats * 60.0 / bpm;
        double targetSeconds = targetPositionBeats * 60.0 / bpm;
        return AddMarkerAtSeconds(originalSeconds, targetSeconds, isAnchor);
    }

    /// <summary>
    /// Removes a stretch marker.
    /// </summary>
    /// <param name="marker">The marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveMarker(StretchMarker marker)
    {
        if (marker == null || marker.IsAnchor)
            return false;

        bool removed;
        lock (_lock)
        {
            removed = _markers.Remove(marker);
        }

        if (removed)
        {
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>
    /// Removes a stretch marker by ID.
    /// </summary>
    /// <param name="markerId">ID of the marker to remove.</param>
    /// <returns>True if the marker was removed.</returns>
    public bool RemoveMarker(Guid markerId)
    {
        lock (_lock)
        {
            var marker = _markers.FirstOrDefault(m => m.Id == markerId);
            if (marker != null)
            {
                return RemoveMarker(marker);
            }
        }
        return false;
    }

    /// <summary>
    /// Moves a marker to a new target position.
    /// </summary>
    /// <param name="marker">The marker to move.</param>
    /// <param name="newTargetPositionSamples">New target position in samples.</param>
    /// <returns>True if the marker was moved.</returns>
    public bool MoveMarker(StretchMarker marker, long newTargetPositionSamples)
    {
        if (marker == null || marker.IsLocked)
            return false;

        lock (_lock)
        {
            if (!_markers.Contains(marker))
                return false;

            marker.TargetPositionSamples = Math.Max(0, newTargetPositionSamples);
        }

        MarkersChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Clears all non-anchor markers.
    /// </summary>
    public void ClearMarkers()
    {
        lock (_lock)
        {
            _markers.RemoveAll(m => !m.IsAnchor);
        }
        MarkersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the stretch ratio at a specific position in the original audio.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>Stretch ratio at that position.</returns>
    public double GetStretchRatioAt(long originalPositionSamples)
    {
        lock (_lock)
        {
            var sortedMarkers = _markers.OrderBy(m => m.OriginalPositionSamples).ToList();

            if (sortedMarkers.Count < 2)
                return 1.0;

            // Find the region containing this position
            for (int i = 1; i < sortedMarkers.Count; i++)
            {
                var prev = sortedMarkers[i - 1];
                var curr = sortedMarkers[i];

                if (originalPositionSamples >= prev.OriginalPositionSamples &&
                    originalPositionSamples < curr.OriginalPositionSamples)
                {
                    return curr.CalculateStretchRatio(prev);
                }
            }

            // Return ratio of last region
            if (sortedMarkers.Count >= 2)
            {
                return sortedMarkers[^1].CalculateStretchRatio(sortedMarkers[^2]);
            }

            return 1.0;
        }
    }

    /// <summary>
    /// Maps an original position to its corresponding target position.
    /// </summary>
    /// <param name="originalPositionSamples">Position in original audio (samples).</param>
    /// <returns>Corresponding position in stretched output (samples).</returns>
    public long OriginalToTarget(long originalPositionSamples)
    {
        lock (_lock)
        {
            var sortedMarkers = _markers.OrderBy(m => m.OriginalPositionSamples).ToList();

            if (sortedMarkers.Count < 2)
                return originalPositionSamples;

            // Find the region containing this position
            for (int i = 1; i < sortedMarkers.Count; i++)
            {
                var prev = sortedMarkers[i - 1];
                var curr = sortedMarkers[i];

                if (originalPositionSamples >= prev.OriginalPositionSamples &&
                    originalPositionSamples <= curr.OriginalPositionSamples)
                {
                    // Linear interpolation within the region
                    long originalDelta = curr.OriginalPositionSamples - prev.OriginalPositionSamples;
                    long targetDelta = curr.TargetPositionSamples - prev.TargetPositionSamples;

                    if (originalDelta == 0)
                        return prev.TargetPositionSamples;

                    double t = (double)(originalPositionSamples - prev.OriginalPositionSamples) / originalDelta;
                    return prev.TargetPositionSamples + (long)(t * targetDelta);
                }
            }

            // Position is beyond the last marker
            var lastMarker = sortedMarkers[^1];
            return lastMarker.TargetPositionSamples + (originalPositionSamples - lastMarker.OriginalPositionSamples);
        }
    }

    /// <summary>
    /// Maps a target position back to its corresponding original position.
    /// </summary>
    /// <param name="targetPositionSamples">Position in stretched output (samples).</param>
    /// <returns>Corresponding position in original audio (samples).</returns>
    public long TargetToOriginal(long targetPositionSamples)
    {
        lock (_lock)
        {
            var sortedMarkers = _markers.OrderBy(m => m.TargetPositionSamples).ToList();

            if (sortedMarkers.Count < 2)
                return targetPositionSamples;

            // Find the region containing this position
            for (int i = 1; i < sortedMarkers.Count; i++)
            {
                var prev = sortedMarkers[i - 1];
                var curr = sortedMarkers[i];

                if (targetPositionSamples >= prev.TargetPositionSamples &&
                    targetPositionSamples <= curr.TargetPositionSamples)
                {
                    // Linear interpolation within the region
                    long targetDelta = curr.TargetPositionSamples - prev.TargetPositionSamples;
                    long originalDelta = curr.OriginalPositionSamples - prev.OriginalPositionSamples;

                    if (targetDelta == 0)
                        return prev.OriginalPositionSamples;

                    double t = (double)(targetPositionSamples - prev.TargetPositionSamples) / targetDelta;
                    return prev.OriginalPositionSamples + (long)(t * originalDelta);
                }
            }

            // Position is beyond the last marker
            var lastMarker = sortedMarkers[^1];
            return lastMarker.OriginalPositionSamples + (targetPositionSamples - lastMarker.TargetPositionSamples);
        }
    }

    /// <summary>
    /// Initializes the phase vocoder buffers based on quality setting.
    /// </summary>
    private void Initialize()
    {
        _fftSize = _quality switch
        {
            StretchQuality.Fast => 1024,
            StretchQuality.Normal => 2048,
            StretchQuality.High => 4096,
            _ => 2048
        };

        _overlapFactor = 4; // 75% overlap
        _hopSize = _fftSize / _overlapFactor;

        _inputBuffer = new float[_fftSize];
        _outputBuffer = new float[_fftSize * 4];
        _fftBuffer = new Complex[_fftSize];
        _lastPhase = new float[_fftSize / 2 + 1];
        _accumulatedPhase = new float[_fftSize / 2 + 1];

        // Hann window
        _analysisWindow = new float[_fftSize];
        _synthesisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
            _synthesisWindow[i] = _analysisWindow[i];
        }

        _initialized = true;
    }

    /// <summary>
    /// Processes the input audio with variable time-stretching based on markers.
    /// </summary>
    /// <param name="inputSamples">Original audio samples (interleaved if stereo).</param>
    /// <param name="outputBuffer">Buffer to receive stretched audio.</param>
    /// <param name="startTargetSample">Starting position in the target output.</param>
    /// <returns>Number of samples written to the output buffer.</returns>
    public int Process(float[] inputSamples, float[] outputBuffer, long startTargetSample)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (!PreservePitch)
        {
            // Simple resampling without pitch preservation
            return ProcessWithoutPitchPreservation(inputSamples, outputBuffer, startTargetSample);
        }

        return ProcessWithPhaseVocoder(inputSamples, outputBuffer, startTargetSample);
    }

    /// <summary>
    /// Applies stretch markers to an AudioClip, processing the audio data.
    /// </summary>
    /// <param name="clip">The audio clip to process.</param>
    /// <returns>The stretched audio data.</returns>
    public float[] ApplyToClip(AudioClip clip)
    {
        if (clip.AudioData == null || clip.AudioData.Length == 0)
            return Array.Empty<float>();

        SampleRate = clip.SampleRate;
        Channels = clip.Channels;
        TotalOriginalSamples = clip.AudioData.Length / clip.Channels;

        // Calculate output size
        var outputSamples = TotalTargetSamples;
        var outputBuffer = new float[outputSamples * Channels];

        Process(clip.AudioData, outputBuffer, 0);

        return outputBuffer;
    }

    /// <summary>
    /// Process using simple resampling (changes pitch with tempo).
    /// </summary>
    private int ProcessWithoutPitchPreservation(float[] inputSamples, float[] outputBuffer, long startTargetSample)
    {
        int inputLength = inputSamples.Length / Channels;
        int outputLength = outputBuffer.Length / Channels;
        int samplesWritten = 0;

        for (int i = 0; i < outputLength; i++)
        {
            long targetPos = startTargetSample + i;
            long originalPos = TargetToOriginal(targetPos);

            if (originalPos < 0 || originalPos >= inputLength)
            {
                // Out of bounds, write silence
                for (int ch = 0; ch < Channels; ch++)
                {
                    outputBuffer[i * Channels + ch] = 0f;
                }
            }
            else
            {
                // Linear interpolation between samples
                int pos0 = (int)originalPos;
                int pos1 = Math.Min(pos0 + 1, inputLength - 1);
                float frac = (float)(originalPos - pos0);

                for (int ch = 0; ch < Channels; ch++)
                {
                    float sample0 = inputSamples[pos0 * Channels + ch];
                    float sample1 = inputSamples[pos1 * Channels + ch];
                    outputBuffer[i * Channels + ch] = sample0 * (1f - frac) + sample1 * frac;
                }
            }

            samplesWritten++;
        }

        return samplesWritten * Channels;
    }

    /// <summary>
    /// Process using phase vocoder for pitch-preserving time stretch.
    /// </summary>
    private int ProcessWithPhaseVocoder(float[] inputSamples, float[] outputBuffer, long startTargetSample)
    {
        int inputLengthPerChannel = inputSamples.Length / Channels;
        int outputLengthPerChannel = outputBuffer.Length / Channels;
        int halfSize = _fftSize / 2;
        float expectedPhaseDiff = 2f * MathF.PI * _hopSize / _fftSize;

        // Process each channel separately
        for (int ch = 0; ch < Channels; ch++)
        {
            Array.Clear(_lastPhase!, 0, _lastPhase!.Length);
            Array.Clear(_accumulatedPhase!, 0, _accumulatedPhase!.Length);

            int outputWritePos = 0;
            float[] channelOutput = new float[outputLengthPerChannel * 2];

            // Process in overlapping frames
            for (long targetFrameStart = 0; targetFrameStart < outputLengthPerChannel; targetFrameStart += _hopSize)
            {
                // Get the stretch ratio at this position
                long targetPos = startTargetSample + targetFrameStart;
                double stretchRatio = GetStretchRatioAt(TargetToOriginal(targetPos));

                // Calculate analysis hop based on stretch ratio
                int analysisHop = Math.Max(1, (int)(_hopSize * stretchRatio));

                // Get original position for this frame
                long originalPos = TargetToOriginal(targetPos);

                // Extract and window the input frame
                for (int i = 0; i < _fftSize; i++)
                {
                    long samplePos = originalPos - _fftSize / 2 + i;
                    float sample = 0f;

                    if (samplePos >= 0 && samplePos < inputLengthPerChannel)
                    {
                        sample = inputSamples[samplePos * Channels + ch];
                    }

                    _fftBuffer![i] = new Complex(sample * _analysisWindow![i], 0f);
                }

                // Forward FFT
                FFT(_fftBuffer!, false);

                // Phase vocoder processing
                for (int k = 0; k <= halfSize; k++)
                {
                    float real = _fftBuffer![k].Real;
                    float imag = _fftBuffer[k].Imag;
                    float magnitude = MathF.Sqrt(real * real + imag * imag);
                    float phase = MathF.Atan2(imag, real);

                    // Calculate phase difference
                    float phaseDiff = phase - _lastPhase![k];
                    _lastPhase[k] = phase;

                    // Remove expected phase increment
                    phaseDiff -= k * expectedPhaseDiff * (float)stretchRatio;

                    // Wrap to [-PI, PI]
                    phaseDiff = WrapPhase(phaseDiff);

                    // Calculate true frequency deviation
                    float deviation = phaseDiff * _overlapFactor / (2f * MathF.PI);
                    float trueFreq = k + deviation;

                    // Accumulate phase for synthesis
                    float synthExpectedPhase = 2f * MathF.PI * _hopSize / _fftSize;
                    _accumulatedPhase![k] += trueFreq * synthExpectedPhase;
                    _accumulatedPhase[k] = WrapPhase(_accumulatedPhase[k]);

                    float newPhase = _accumulatedPhase[k];
                    _fftBuffer[k] = new Complex(magnitude * MathF.Cos(newPhase), magnitude * MathF.Sin(newPhase));

                    // Mirror for negative frequencies
                    if (k > 0 && k < halfSize)
                    {
                        _fftBuffer[_fftSize - k] = new Complex(
                            magnitude * MathF.Cos(newPhase),
                            -magnitude * MathF.Sin(newPhase));
                    }
                }

                // Inverse FFT
                FFT(_fftBuffer!, true);

                // Overlap-add to output
                float normFactor = 1f / (_overlapFactor * 0.5f);
                for (int i = 0; i < _fftSize; i++)
                {
                    int outputPos = outputWritePos + i;
                    if (outputPos < channelOutput.Length)
                    {
                        channelOutput[outputPos] += _fftBuffer![i].Real * _synthesisWindow![i] * normFactor;
                    }
                }

                outputWritePos += _hopSize;
            }

            // Copy to output buffer (interleaved)
            for (int i = 0; i < outputLengthPerChannel && i < channelOutput.Length; i++)
            {
                outputBuffer[i * Channels + ch] = channelOutput[i];
            }
        }

        return outputBuffer.Length;
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

        public static Complex operator +(Complex a, Complex b) =>
            new Complex(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b) =>
            new Complex(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b) =>
            new Complex(
                a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real);
    }
}
