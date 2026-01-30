//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Silence and gap detection tool with threshold-based detection, hysteresis,
// frequency weighting, noise floor learning, and integration with audio editing.

using System;
using System.Collections.Generic;
using System.Text.Json;
using NAudio.Dsp;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a detected silence region in audio.
/// </summary>
public class SilenceRegion
{
    /// <summary>Start time of the silence region in seconds.</summary>
    public double StartTimeSeconds { get; set; }

    /// <summary>End time of the silence region in seconds.</summary>
    public double EndTimeSeconds { get; set; }

    /// <summary>Duration of the silence region in seconds.</summary>
    public double DurationSeconds => EndTimeSeconds - StartTimeSeconds;

    /// <summary>Duration of the silence region in milliseconds.</summary>
    public double DurationMs => DurationSeconds * 1000.0;

    /// <summary>Start position in samples.</summary>
    public long StartSample { get; set; }

    /// <summary>End position in samples.</summary>
    public long EndSample { get; set; }

    /// <summary>Duration in samples.</summary>
    public long DurationSamples => EndSample - StartSample;

    /// <summary>Type of silence region.</summary>
    public SilenceRegionType RegionType { get; set; }

    /// <summary>Average level during silence in dBFS.</summary>
    public float AverageLevelDbfs { get; set; }

    /// <summary>Channel index (-1 for all channels combined).</summary>
    public int Channel { get; set; } = -1;

    /// <summary>Whether this region is marked for trimming.</summary>
    public bool MarkedForTrim { get; set; }

    /// <summary>Whether this region is a suggested split point.</summary>
    public bool SuggestedSplitPoint { get; set; }
}

/// <summary>
/// Type of silence region.
/// </summary>
public enum SilenceRegionType
{
    /// <summary>Silence at the beginning of the audio.</summary>
    Leading,

    /// <summary>Silence at the end of the audio.</summary>
    Trailing,

    /// <summary>Silence between audio content (gap).</summary>
    Gap,

    /// <summary>Unknown or general silence.</summary>
    Unknown
}

/// <summary>
/// Result of silence detection analysis.
/// </summary>
public class SilenceDetectionResult
{
    /// <summary>List of detected silence regions.</summary>
    public List<SilenceRegion> Regions { get; set; } = new();

    /// <summary>Total duration of silence in seconds.</summary>
    public double TotalSilenceDurationSeconds { get; set; }

    /// <summary>Total duration of audio (non-silence) in seconds.</summary>
    public double TotalAudioDurationSeconds { get; set; }

    /// <summary>Percentage of total duration that is silence.</summary>
    public double SilencePercentage { get; set; }

    /// <summary>Duration of leading silence in seconds (0 if none).</summary>
    public double LeadingSilenceDurationSeconds { get; set; }

    /// <summary>Duration of trailing silence in seconds (0 if none).</summary>
    public double TrailingSilenceDurationSeconds { get; set; }

    /// <summary>Number of gaps between audio content.</summary>
    public int GapCount { get; set; }

    /// <summary>Average gap duration in seconds.</summary>
    public double AverageGapDurationSeconds { get; set; }

    /// <summary>Learned noise floor level in dBFS (if noise floor learning was used).</summary>
    public float LearnedNoiseFloorDbfs { get; set; }

    /// <summary>Sample rate used for analysis.</summary>
    public int SampleRate { get; set; }

    /// <summary>Total samples analyzed.</summary>
    public long TotalSamples { get; set; }

    /// <summary>Total duration of the analyzed audio in seconds.</summary>
    public double TotalDurationSeconds { get; set; }

    /// <summary>Suggested trim points to remove leading/trailing silence.</summary>
    public (long startSample, long endSample) SuggestedTrimRange { get; set; }

    /// <summary>List of suggested split points (sample positions).</summary>
    public List<long> SuggestedSplitPoints { get; set; } = new();

    /// <summary>Gets regions marked for trimming.</summary>
    public List<SilenceRegion> GetRegionsMarkedForTrim()
    {
        return Regions.FindAll(r => r.MarkedForTrim);
    }

    /// <summary>Gets suggested split points as time positions.</summary>
    public List<double> GetSplitPointsAsSeconds()
    {
        var points = new List<double>();
        foreach (var sample in SuggestedSplitPoints)
        {
            points.Add((double)sample / SampleRate);
        }
        return points;
    }
}

/// <summary>
/// Event arguments for real-time silence detection state changes.
/// </summary>
public class SilenceStateChangedEventArgs : EventArgs
{
    /// <summary>Whether silence was entered (true) or exited (false).</summary>
    public bool EnteredSilence { get; }

    /// <summary>Time position in seconds when the state changed.</summary>
    public double TimeSeconds { get; }

    /// <summary>Current level in dBFS.</summary>
    public float CurrentLevelDbfs { get; }

    /// <summary>Channel index (-1 for combined).</summary>
    public int Channel { get; }

    public SilenceStateChangedEventArgs(bool enteredSilence, double timeSeconds, float currentLevelDbfs, int channel = -1)
    {
        EnteredSilence = enteredSilence;
        TimeSeconds = timeSeconds;
        CurrentLevelDbfs = currentLevelDbfs;
        Channel = channel;
    }
}

/// <summary>
/// Silence and gap detection analyzer with real-time and offline modes.
/// Features threshold-based detection, hysteresis, frequency weighting,
/// noise floor learning, and integration with audio editing workflows.
/// </summary>
public class SilenceDetector : IAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _frameSize;
    private readonly int _hopSize;
    private readonly float[] _frameBuffer;
    private int _frameBufferPosition;
    private double _currentTime;
    private readonly object _lock = new();

    // FFT for frequency weighting
    private readonly Complex[] _fftBuffer;
    private readonly int _fftLength;
    private readonly float _highPassCutoff;

    // Per-channel state tracking
    private readonly int _maxChannels;
    private readonly bool[] _inSilence;
    private readonly double[] _silenceStartTime;
    private readonly float[] _silenceLevelAccumulator;
    private readonly int[] _silenceSampleCount;
    private readonly float[][] _channelFrameBuffers;
    private readonly int[] _channelFramePositions;

    // Detection parameters
    private float _thresholdDbfs = -60f;
    private float _hysteresisEnterDbfs = -60f;
    private float _hysteresisExitDbfs = -54f;
    private double _minimumSilenceDurationMs = 100;
    private double _minimumAudioDurationMs = 50;
    private bool _useFrequencyWeighting = false;
    private bool _perChannelDetection = false;

    // Noise floor learning
    private float _learnedNoiseFloor = float.NegativeInfinity;
    private float _noiseFloorMarginDb = 6f;
    private readonly List<float> _noiseFloorSamples = new();
    private bool _isLearningNoiseFloor;
    private int _noiseFloorLearningSamples = 44100; // 1 second by default

    // Results
    private readonly List<SilenceRegion> _detectedRegions = new();
    private long _totalSamplesProcessed;
    private double _totalSilenceDuration;
    private bool _audioStarted;
    private long _firstAudioSample;
    private long _lastAudioSample;

    /// <summary>
    /// Gets or sets the silence threshold in dBFS (default: -60 dBFS).
    /// Audio below this level is considered silence.
    /// </summary>
    public float ThresholdDbfs
    {
        get => _thresholdDbfs;
        set
        {
            _thresholdDbfs = Math.Clamp(value, -120f, 0f);
            // Update hysteresis if not using custom values
            if (!_useCustomHysteresis)
            {
                _hysteresisEnterDbfs = _thresholdDbfs;
                _hysteresisExitDbfs = _thresholdDbfs + 6f;
            }
        }
    }

    /// <summary>
    /// Gets or sets the threshold for entering silence state (hysteresis low threshold).
    /// </summary>
    public float HysteresisEnterDbfs
    {
        get => _hysteresisEnterDbfs;
        set
        {
            _hysteresisEnterDbfs = Math.Clamp(value, -120f, 0f);
            _useCustomHysteresis = true;
        }
    }

    /// <summary>
    /// Gets or sets the threshold for exiting silence state (hysteresis high threshold).
    /// </summary>
    public float HysteresisExitDbfs
    {
        get => _hysteresisExitDbfs;
        set
        {
            _hysteresisExitDbfs = Math.Clamp(value, -120f, 0f);
            _useCustomHysteresis = true;
        }
    }

    private bool _useCustomHysteresis;

    /// <summary>
    /// Gets or sets the minimum silence duration in milliseconds.
    /// Silence shorter than this is ignored.
    /// </summary>
    public double MinimumSilenceDurationMs
    {
        get => _minimumSilenceDurationMs;
        set => _minimumSilenceDurationMs = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the minimum audio duration between silences in milliseconds.
    /// Audio segments shorter than this may be merged with adjacent silence.
    /// </summary>
    public double MinimumAudioDurationMs
    {
        get => _minimumAudioDurationMs;
        set => _minimumAudioDurationMs = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets whether to use frequency weighting (high-pass filter to ignore sub-bass rumble).
    /// </summary>
    public bool UseFrequencyWeighting
    {
        get => _useFrequencyWeighting;
        set => _useFrequencyWeighting = value;
    }

    /// <summary>
    /// Gets or sets whether to perform per-channel detection.
    /// </summary>
    public bool PerChannelDetection
    {
        get => _perChannelDetection;
        set => _perChannelDetection = value;
    }

    /// <summary>
    /// Gets or sets the margin above learned noise floor in dB for threshold setting.
    /// </summary>
    public float NoiseFloorMarginDb
    {
        get => _noiseFloorMarginDb;
        set => _noiseFloorMarginDb = Math.Max(0, value);
    }

    /// <summary>
    /// Gets or sets the number of samples to use for noise floor learning.
    /// </summary>
    public int NoiseFloorLearningSamples
    {
        get => _noiseFloorLearningSamples;
        set => _noiseFloorLearningSamples = Math.Max(_frameSize, value);
    }

    /// <summary>
    /// Gets the current silence state (true = in silence).
    /// </summary>
    public bool IsInSilence
    {
        get
        {
            lock (_lock)
            {
                return _inSilence[0];
            }
        }
    }

    /// <summary>
    /// Gets the learned noise floor in dBFS.
    /// </summary>
    public float LearnedNoiseFloorDbfs => _learnedNoiseFloor;

    /// <summary>
    /// Gets the list of detected silence regions.
    /// </summary>
    public IReadOnlyList<SilenceRegion> DetectedRegions
    {
        get
        {
            lock (_lock)
            {
                return new List<SilenceRegion>(_detectedRegions);
            }
        }
    }

    /// <summary>
    /// Event raised when entering or exiting silence (real-time mode).
    /// </summary>
    public event EventHandler<SilenceStateChangedEventArgs>? SilenceStateChanged;

    /// <summary>
    /// Event raised when a complete silence region is detected.
    /// </summary>
    public event EventHandler<SilenceRegion>? SilenceRegionDetected;

    /// <summary>
    /// Creates a new silence detector with the specified configuration.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate in Hz (default: 44100).</param>
    /// <param name="frameSize">Analysis frame size in samples (default: 1024).</param>
    /// <param name="hopSize">Hop size in samples (default: 512).</param>
    /// <param name="highPassCutoff">High-pass cutoff frequency for frequency weighting (default: 80 Hz).</param>
    /// <param name="maxChannels">Maximum number of channels to support for per-channel detection (default: 8).</param>
    public SilenceDetector(
        int sampleRate = 44100,
        int frameSize = 1024,
        int hopSize = 512,
        float highPassCutoff = 80f,
        int maxChannels = 8)
    {
        _sampleRate = sampleRate;
        _frameSize = frameSize;
        _hopSize = hopSize;
        _highPassCutoff = highPassCutoff;
        _maxChannels = maxChannels;

        _frameBuffer = new float[frameSize];

        // FFT setup for frequency weighting
        _fftLength = frameSize;
        _fftBuffer = new Complex[_fftLength];

        // Per-channel state
        _inSilence = new bool[maxChannels + 1]; // +1 for combined
        _silenceStartTime = new double[maxChannels + 1];
        _silenceLevelAccumulator = new float[maxChannels + 1];
        _silenceSampleCount = new int[maxChannels + 1];
        _channelFrameBuffers = new float[maxChannels][];
        _channelFramePositions = new int[maxChannels];

        for (int i = 0; i < maxChannels; i++)
        {
            _channelFrameBuffers[i] = new float[frameSize];
        }

        // Initialize all channels as in silence
        for (int i = 0; i <= maxChannels; i++)
        {
            _inSilence[i] = true;
        }
    }

    /// <summary>
    /// Processes audio samples for silence detection.
    /// </summary>
    /// <param name="samples">Audio sample buffer.</param>
    /// <param name="offset">Offset into the buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels.</param>
    public void ProcessSamples(float[] samples, int offset, int count, int channels)
    {
        int actualChannels = Math.Min(channels, _maxChannels);

        for (int i = offset; i < offset + count; i += channels)
        {
            // Mix to mono for combined detection
            float monoSample = 0;
            for (int ch = 0; ch < actualChannels; ch++)
            {
                float sample = i + ch < offset + count ? samples[i + ch] : 0;
                monoSample += sample;

                // Per-channel processing
                if (_perChannelDetection)
                {
                    _channelFrameBuffers[ch][_channelFramePositions[ch]] = sample;
                    _channelFramePositions[ch]++;

                    if (_channelFramePositions[ch] >= _frameSize)
                    {
                        ProcessChannelFrame(ch);
                        ShiftBuffer(_channelFrameBuffers[ch], _hopSize);
                        _channelFramePositions[ch] = _frameSize - _hopSize;
                    }
                }
            }
            monoSample /= actualChannels;

            // Combined (mono) processing
            _frameBuffer[_frameBufferPosition] = monoSample;
            _frameBufferPosition++;

            if (_frameBufferPosition >= _frameSize)
            {
                ProcessFrame(-1); // -1 = combined
                ShiftBuffer(_frameBuffer, _hopSize);
                _frameBufferPosition = _frameSize - _hopSize;
                _currentTime += (double)_hopSize / _sampleRate;
            }
        }

        _totalSamplesProcessed += count / channels;
    }

    /// <summary>
    /// Analyzes a complete audio buffer and returns silence detection results.
    /// </summary>
    /// <param name="samples">Complete audio buffer.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <returns>Silence detection result.</returns>
    public SilenceDetectionResult AnalyzeBuffer(float[] samples, int channels = 1)
    {
        Reset();
        ProcessSamples(samples, 0, samples.Length, channels);
        return GetResult();
    }

    /// <summary>
    /// Starts noise floor learning mode.
    /// Call ProcessSamples with noise-only audio, then call FinishNoiseFloorLearning.
    /// </summary>
    public void StartNoiseFloorLearning()
    {
        lock (_lock)
        {
            _noiseFloorSamples.Clear();
            _isLearningNoiseFloor = true;
        }
    }

    /// <summary>
    /// Finishes noise floor learning and sets the threshold.
    /// </summary>
    /// <returns>The learned noise floor in dBFS.</returns>
    public float FinishNoiseFloorLearning()
    {
        lock (_lock)
        {
            _isLearningNoiseFloor = false;

            if (_noiseFloorSamples.Count == 0)
            {
                return _learnedNoiseFloor;
            }

            // Calculate average noise floor
            float sum = 0;
            foreach (var level in _noiseFloorSamples)
            {
                sum += level;
            }
            _learnedNoiseFloor = sum / _noiseFloorSamples.Count;

            // Set threshold with margin
            ThresholdDbfs = _learnedNoiseFloor + _noiseFloorMarginDb;

            _noiseFloorSamples.Clear();
            return _learnedNoiseFloor;
        }
    }

    /// <summary>
    /// Learns noise floor from a buffer of noise-only audio.
    /// </summary>
    /// <param name="noiseBuffer">Buffer containing only noise/silence.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <returns>The learned noise floor in dBFS.</returns>
    public float LearnNoiseFloor(float[] noiseBuffer, int channels = 1)
    {
        StartNoiseFloorLearning();

        // Process in frames
        int samplesPerFrame = _frameSize * channels;
        for (int i = 0; i <= noiseBuffer.Length - samplesPerFrame; i += _hopSize * channels)
        {
            float rms = CalculateRms(noiseBuffer, i, _frameSize, channels);
            float dbfs = LinearToDbfs(rms);

            lock (_lock)
            {
                _noiseFloorSamples.Add(dbfs);
            }
        }

        return FinishNoiseFloorLearning();
    }

    /// <summary>
    /// Gets the current silence detection result.
    /// </summary>
    public SilenceDetectionResult GetResult()
    {
        lock (_lock)
        {
            // Finalize any ongoing silence region
            for (int ch = 0; ch <= (_perChannelDetection ? _maxChannels : 0); ch++)
            {
                if (_inSilence[ch] && _silenceStartTime[ch] >= 0)
                {
                    FinalizeCurrentSilenceRegion(ch, _currentTime);
                }
            }

            var result = new SilenceDetectionResult
            {
                Regions = new List<SilenceRegion>(_detectedRegions),
                SampleRate = _sampleRate,
                TotalSamples = _totalSamplesProcessed,
                TotalDurationSeconds = (double)_totalSamplesProcessed / _sampleRate,
                LearnedNoiseFloorDbfs = _learnedNoiseFloor
            };

            // Calculate statistics
            CalculateStatistics(result);

            // Determine region types and suggestions
            ClassifyRegions(result);
            CalculateTrimAndSplitSuggestions(result);

            return result;
        }
    }

    /// <summary>
    /// Marks silence regions for trimming based on criteria.
    /// </summary>
    /// <param name="trimLeading">Mark leading silence for trimming.</param>
    /// <param name="trimTrailing">Mark trailing silence for trimming.</param>
    /// <param name="trimGaps">Mark gaps for trimming.</param>
    /// <param name="minGapDurationMs">Minimum gap duration to mark for trimming.</param>
    public void MarkRegionsForTrimming(bool trimLeading = true, bool trimTrailing = true, bool trimGaps = false, double minGapDurationMs = 1000)
    {
        lock (_lock)
        {
            foreach (var region in _detectedRegions)
            {
                region.MarkedForTrim = false;

                if (trimLeading && region.RegionType == SilenceRegionType.Leading)
                {
                    region.MarkedForTrim = true;
                }
                else if (trimTrailing && region.RegionType == SilenceRegionType.Trailing)
                {
                    region.MarkedForTrim = true;
                }
                else if (trimGaps && region.RegionType == SilenceRegionType.Gap && region.DurationMs >= minGapDurationMs)
                {
                    region.MarkedForTrim = true;
                }
            }
        }
    }

    /// <summary>
    /// Marks silence regions as auto-split points.
    /// </summary>
    /// <param name="minSilenceDurationMs">Minimum silence duration to be considered a split point.</param>
    /// <param name="splitAtGapsOnly">Only mark gaps (not leading/trailing) as split points.</param>
    public void MarkAutoSplitPoints(double minSilenceDurationMs = 500, bool splitAtGapsOnly = true)
    {
        lock (_lock)
        {
            foreach (var region in _detectedRegions)
            {
                region.SuggestedSplitPoint = false;

                if (region.DurationMs >= minSilenceDurationMs)
                {
                    if (!splitAtGapsOnly || region.RegionType == SilenceRegionType.Gap)
                    {
                        region.SuggestedSplitPoint = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Exports silence map to JSON format.
    /// </summary>
    /// <returns>JSON string representing the silence map.</returns>
    public string ExportSilenceMapJson()
    {
        var result = GetResult();
        var exportData = new
        {
            sampleRate = result.SampleRate,
            totalDurationSeconds = result.TotalDurationSeconds,
            totalSilenceDurationSeconds = result.TotalSilenceDurationSeconds,
            silencePercentage = result.SilencePercentage,
            leadingSilenceSeconds = result.LeadingSilenceDurationSeconds,
            trailingSilenceSeconds = result.TrailingSilenceDurationSeconds,
            gapCount = result.GapCount,
            learnedNoiseFloorDbfs = result.LearnedNoiseFloorDbfs,
            regions = result.Regions.ConvertAll(r => new
            {
                startTimeSeconds = r.StartTimeSeconds,
                endTimeSeconds = r.EndTimeSeconds,
                durationSeconds = r.DurationSeconds,
                regionType = r.RegionType.ToString(),
                averageLevelDbfs = r.AverageLevelDbfs,
                channel = r.Channel,
                markedForTrim = r.MarkedForTrim,
                suggestedSplitPoint = r.SuggestedSplitPoint
            }),
            suggestedTrimRange = new
            {
                startSample = result.SuggestedTrimRange.startSample,
                endSample = result.SuggestedTrimRange.endSample
            },
            suggestedSplitPoints = result.SuggestedSplitPoints
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Gets cut suggestions for audio editing integration.
    /// Returns a list of (startSample, endSample) ranges to remove.
    /// </summary>
    public List<(long startSample, long endSample)> GetCutSuggestions()
    {
        var cuts = new List<(long, long)>();

        lock (_lock)
        {
            foreach (var region in _detectedRegions)
            {
                if (region.MarkedForTrim)
                {
                    cuts.Add((region.StartSample, region.EndSample));
                }
            }
        }

        return cuts;
    }

    /// <summary>
    /// Resets the detector state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _frameBufferPosition = 0;
            _currentTime = 0;
            _totalSamplesProcessed = 0;
            _totalSilenceDuration = 0;
            _audioStarted = false;
            _firstAudioSample = 0;
            _lastAudioSample = 0;
            _detectedRegions.Clear();

            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);

            for (int i = 0; i <= _maxChannels; i++)
            {
                _inSilence[i] = true;
                _silenceStartTime[i] = 0;
                _silenceLevelAccumulator[i] = 0;
                _silenceSampleCount[i] = 0;
            }

            for (int i = 0; i < _maxChannels; i++)
            {
                Array.Clear(_channelFrameBuffers[i], 0, _channelFrameBuffers[i].Length);
                _channelFramePositions[i] = 0;
            }
        }
    }

    private void ProcessFrame(int channel)
    {
        float[] buffer = channel < 0 ? _frameBuffer : _channelFrameBuffers[channel];
        int stateIndex = channel < 0 ? 0 : channel + 1;

        float level;
        if (_useFrequencyWeighting)
        {
            level = CalculateFrequencyWeightedLevel(buffer);
        }
        else
        {
            level = CalculateRms(buffer, 0, _frameSize, 1);
        }

        float levelDbfs = LinearToDbfs(level);

        // Noise floor learning
        if (_isLearningNoiseFloor)
        {
            lock (_lock)
            {
                if (_noiseFloorSamples.Count < _noiseFloorLearningSamples / _hopSize)
                {
                    _noiseFloorSamples.Add(levelDbfs);
                }
            }
            return;
        }

        // Apply hysteresis-based state detection
        bool wasInSilence = _inSilence[stateIndex];
        bool isNowInSilence;

        if (wasInSilence)
        {
            // Currently in silence, need to exceed exit threshold to leave
            isNowInSilence = levelDbfs < _hysteresisExitDbfs;
        }
        else
        {
            // Currently in audio, need to go below enter threshold to enter silence
            isNowInSilence = levelDbfs < _hysteresisEnterDbfs;
        }

        // State transition handling
        if (wasInSilence && !isNowInSilence)
        {
            // Exiting silence (audio starting)
            HandleSilenceExit(stateIndex, levelDbfs, channel);
        }
        else if (!wasInSilence && isNowInSilence)
        {
            // Entering silence (audio stopping)
            HandleSilenceEnter(stateIndex, levelDbfs, channel);
        }

        // Accumulate level during silence for average calculation
        if (isNowInSilence)
        {
            _silenceLevelAccumulator[stateIndex] += levelDbfs;
            _silenceSampleCount[stateIndex]++;
        }

        _inSilence[stateIndex] = isNowInSilence;
    }

    private void ProcessChannelFrame(int channel)
    {
        ProcessFrame(channel);
    }

    private void HandleSilenceEnter(int stateIndex, float levelDbfs, int channel)
    {
        lock (_lock)
        {
            _silenceStartTime[stateIndex] = _currentTime;
            _silenceLevelAccumulator[stateIndex] = levelDbfs;
            _silenceSampleCount[stateIndex] = 1;
        }

        // Raise event
        SilenceStateChanged?.Invoke(this, new SilenceStateChangedEventArgs(true, _currentTime, levelDbfs, channel));
    }

    private void HandleSilenceExit(int stateIndex, float levelDbfs, int channel)
    {
        // Finalize the silence region
        FinalizeCurrentSilenceRegion(stateIndex, _currentTime);

        // Track audio presence
        lock (_lock)
        {
            if (!_audioStarted)
            {
                _audioStarted = true;
                _firstAudioSample = (long)(_currentTime * _sampleRate);
            }
            _lastAudioSample = (long)(_currentTime * _sampleRate);
        }

        // Raise event
        SilenceStateChanged?.Invoke(this, new SilenceStateChangedEventArgs(false, _currentTime, levelDbfs, channel));
    }

    private void FinalizeCurrentSilenceRegion(int stateIndex, double endTime)
    {
        lock (_lock)
        {
            double startTime = _silenceStartTime[stateIndex];
            double duration = endTime - startTime;

            // Check minimum duration
            if (duration * 1000 < _minimumSilenceDurationMs)
            {
                return;
            }

            float avgLevel = _silenceSampleCount[stateIndex] > 0
                ? _silenceLevelAccumulator[stateIndex] / _silenceSampleCount[stateIndex]
                : float.NegativeInfinity;

            var region = new SilenceRegion
            {
                StartTimeSeconds = startTime,
                EndTimeSeconds = endTime,
                StartSample = (long)(startTime * _sampleRate),
                EndSample = (long)(endTime * _sampleRate),
                AverageLevelDbfs = avgLevel,
                Channel = stateIndex == 0 ? -1 : stateIndex - 1
            };

            _detectedRegions.Add(region);
            _totalSilenceDuration += duration;

            // Raise event
            SilenceRegionDetected?.Invoke(this, region);
        }
    }

    private float CalculateFrequencyWeightedLevel(float[] buffer)
    {
        // Apply Hann window and prepare FFT buffer
        for (int i = 0; i < _fftLength; i++)
        {
            float window = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_fftLength - 1))));
            _fftBuffer[i].X = buffer[i] * window;
            _fftBuffer[i].Y = 0;
        }

        // Perform FFT
        int m = (int)Math.Log(_fftLength, 2.0);
        FastFourierTransform.FFT(true, m, _fftBuffer);

        // Calculate energy, ignoring low frequencies (sub-bass rumble)
        float binResolution = (float)_sampleRate / _fftLength;
        int startBin = (int)(_highPassCutoff / binResolution);
        int maxBin = _fftLength / 2;

        float sumSquared = 0;
        for (int bin = startBin; bin < maxBin; bin++)
        {
            float magnitude = _fftBuffer[bin].X * _fftBuffer[bin].X + _fftBuffer[bin].Y * _fftBuffer[bin].Y;
            sumSquared += magnitude;
        }

        // Convert to RMS-like value
        return (float)Math.Sqrt(sumSquared / (maxBin - startBin));
    }

    private static float CalculateRms(float[] buffer, int offset, int length, int channels)
    {
        float sumSquared = 0;
        int sampleCount = 0;

        for (int i = offset; i < offset + length * channels; i += channels)
        {
            // Mix channels
            float sample = 0;
            for (int ch = 0; ch < channels && i + ch < buffer.Length; ch++)
            {
                sample += buffer[i + ch];
            }
            sample /= channels;

            sumSquared += sample * sample;
            sampleCount++;
        }

        return sampleCount > 0 ? (float)Math.Sqrt(sumSquared / sampleCount) : 0;
    }

    private static float LinearToDbfs(float linear)
    {
        if (linear <= 0)
            return -120f;
        return 20f * (float)Math.Log10(linear);
    }

    private static void ShiftBuffer(float[] buffer, int shiftAmount)
    {
        int remaining = buffer.Length - shiftAmount;
        Array.Copy(buffer, shiftAmount, buffer, 0, remaining);
        Array.Clear(buffer, remaining, shiftAmount);
    }

    private void CalculateStatistics(SilenceDetectionResult result)
    {
        double totalSilence = 0;
        foreach (var region in result.Regions)
        {
            totalSilence += region.DurationSeconds;
        }

        result.TotalSilenceDurationSeconds = totalSilence;
        result.TotalAudioDurationSeconds = result.TotalDurationSeconds - totalSilence;
        result.SilencePercentage = result.TotalDurationSeconds > 0
            ? (totalSilence / result.TotalDurationSeconds) * 100
            : 0;
    }

    private void ClassifyRegions(SilenceDetectionResult result)
    {
        if (result.Regions.Count == 0)
            return;

        double totalDuration = result.TotalDurationSeconds;
        double leadingDuration = 0;
        double trailingDuration = 0;
        int gapCount = 0;
        double totalGapDuration = 0;

        for (int i = 0; i < result.Regions.Count; i++)
        {
            var region = result.Regions[i];

            // Leading silence: starts at or very near 0
            if (region.StartTimeSeconds < 0.001)
            {
                region.RegionType = SilenceRegionType.Leading;
                leadingDuration = region.DurationSeconds;
            }
            // Trailing silence: ends at or very near total duration
            else if (Math.Abs(region.EndTimeSeconds - totalDuration) < 0.001)
            {
                region.RegionType = SilenceRegionType.Trailing;
                trailingDuration = region.DurationSeconds;
            }
            // Gap: silence between audio content
            else
            {
                region.RegionType = SilenceRegionType.Gap;
                gapCount++;
                totalGapDuration += region.DurationSeconds;
            }
        }

        result.LeadingSilenceDurationSeconds = leadingDuration;
        result.TrailingSilenceDurationSeconds = trailingDuration;
        result.GapCount = gapCount;
        result.AverageGapDurationSeconds = gapCount > 0 ? totalGapDuration / gapCount : 0;
    }

    private void CalculateTrimAndSplitSuggestions(SilenceDetectionResult result)
    {
        // Calculate suggested trim range (remove leading/trailing silence)
        long trimStart = 0;
        long trimEnd = result.TotalSamples;

        foreach (var region in result.Regions)
        {
            if (region.RegionType == SilenceRegionType.Leading)
            {
                trimStart = region.EndSample;
            }
            else if (region.RegionType == SilenceRegionType.Trailing)
            {
                trimEnd = region.StartSample;
            }
        }

        result.SuggestedTrimRange = (trimStart, trimEnd);

        // Calculate split points (middle of each gap)
        result.SuggestedSplitPoints.Clear();
        foreach (var region in result.Regions)
        {
            if (region.RegionType == SilenceRegionType.Gap)
            {
                long midPoint = (region.StartSample + region.EndSample) / 2;
                result.SuggestedSplitPoints.Add(midPoint);
            }
        }
    }

    /// <summary>
    /// Creates a preset for speech/dialogue detection.
    /// </summary>
    public static SilenceDetector CreateSpeechPreset(int sampleRate = 44100)
    {
        return new SilenceDetector(sampleRate)
        {
            ThresholdDbfs = -45f,
            MinimumSilenceDurationMs = 300,
            MinimumAudioDurationMs = 100,
            UseFrequencyWeighting = true
        };
    }

    /// <summary>
    /// Creates a preset for music track detection.
    /// </summary>
    public static SilenceDetector CreateMusicPreset(int sampleRate = 44100)
    {
        return new SilenceDetector(sampleRate)
        {
            ThresholdDbfs = -60f,
            MinimumSilenceDurationMs = 500,
            MinimumAudioDurationMs = 200,
            UseFrequencyWeighting = false
        };
    }

    /// <summary>
    /// Creates a preset for podcast/interview silence removal.
    /// </summary>
    public static SilenceDetector CreatePodcastPreset(int sampleRate = 44100)
    {
        return new SilenceDetector(sampleRate)
        {
            ThresholdDbfs = -40f,
            MinimumSilenceDurationMs = 500,
            MinimumAudioDurationMs = 150,
            UseFrequencyWeighting = true,
            HysteresisEnterDbfs = -42f,
            HysteresisExitDbfs = -36f
        };
    }

    /// <summary>
    /// Creates a preset for vinyl/tape recording with noise.
    /// </summary>
    public static SilenceDetector CreateVinylPreset(int sampleRate = 44100)
    {
        return new SilenceDetector(sampleRate)
        {
            ThresholdDbfs = -50f,
            MinimumSilenceDurationMs = 1000,
            MinimumAudioDurationMs = 500,
            UseFrequencyWeighting = true,
            NoiseFloorMarginDb = 10f
        };
    }

    /// <summary>
    /// Creates a preset for auto-splitting recordings at silence.
    /// </summary>
    public static SilenceDetector CreateAutoSplitPreset(int sampleRate = 44100)
    {
        return new SilenceDetector(sampleRate)
        {
            ThresholdDbfs = -55f,
            MinimumSilenceDurationMs = 2000,
            MinimumAudioDurationMs = 1000,
            UseFrequencyWeighting = false,
            HysteresisEnterDbfs = -57f,
            HysteresisExitDbfs = -50f
        };
    }
}
