// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Combined analyzer pipeline that wraps an audio source and feeds multiple analyzers.
/// Provides a unified interface for real-time audio analysis including spectrum, correlation,
/// peak detection, and goniometer data.
/// </summary>
public class AnalysisChain : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly List<IAnalyzer> _analyzers = new();
    private readonly object _lock = new();

    // Built-in analyzers (optional, created on demand)
    private SpectrumAnalyzer? _spectrumAnalyzer;
    private CorrelationMeter? _correlationMeter;
    private EnhancedPeakDetector? _peakDetector;
    private GoniometerDataProvider? _goniometer;

    /// <summary>
    /// Gets the wave format of the audio stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets the spectrum analyzer (created on first access).
    /// </summary>
    public SpectrumAnalyzer SpectrumAnalyzer
    {
        get
        {
            if (_spectrumAnalyzer == null)
            {
                _spectrumAnalyzer = new SpectrumAnalyzer(
                    sampleRate: _source.WaveFormat.SampleRate);
            }
            return _spectrumAnalyzer;
        }
    }

    /// <summary>
    /// Gets the correlation meter (created on first access, requires stereo source).
    /// Returns null if source is not stereo.
    /// </summary>
    public CorrelationMeter? CorrelationMeter
    {
        get
        {
            if (_correlationMeter == null && _source.WaveFormat.Channels == 2)
            {
                _correlationMeter = new CorrelationMeter(_source);
            }
            return _correlationMeter;
        }
    }

    /// <summary>
    /// Gets the enhanced peak detector (created on first access).
    /// </summary>
    public EnhancedPeakDetector PeakDetector
    {
        get
        {
            if (_peakDetector == null)
            {
                _peakDetector = new EnhancedPeakDetector(_source);
            }
            return _peakDetector;
        }
    }

    /// <summary>
    /// Gets the goniometer data provider (created on first access, requires stereo source).
    /// Returns null if source is not stereo.
    /// </summary>
    public GoniometerDataProvider? Goniometer
    {
        get
        {
            if (_goniometer == null && _source.WaveFormat.Channels == 2)
            {
                _goniometer = new GoniometerDataProvider(_source);
            }
            return _goniometer;
        }
    }

    /// <summary>
    /// Gets whether spectrum analysis is enabled.
    /// </summary>
    public bool SpectrumEnabled { get; set; } = true;

    /// <summary>
    /// Gets whether correlation metering is enabled.
    /// </summary>
    public bool CorrelationEnabled { get; set; } = true;

    /// <summary>
    /// Gets whether peak detection is enabled.
    /// </summary>
    public bool PeakDetectionEnabled { get; set; } = true;

    /// <summary>
    /// Gets whether goniometer visualization is enabled.
    /// </summary>
    public bool GoniometerEnabled { get; set; } = true;

    /// <summary>
    /// Creates a new analysis chain wrapping the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to analyze.</param>
    public AnalysisChain(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Adds a custom analyzer to the chain.
    /// </summary>
    /// <param name="analyzer">The analyzer to add.</param>
    public void AddAnalyzer(IAnalyzer analyzer)
    {
        if (analyzer == null)
            throw new ArgumentNullException(nameof(analyzer));

        lock (_lock)
        {
            _analyzers.Add(analyzer);
        }
    }

    /// <summary>
    /// Removes a custom analyzer from the chain.
    /// </summary>
    /// <param name="analyzer">The analyzer to remove.</param>
    /// <returns>True if the analyzer was removed, false if not found.</returns>
    public bool RemoveAnalyzer(IAnalyzer analyzer)
    {
        lock (_lock)
        {
            return _analyzers.Remove(analyzer);
        }
    }

    /// <summary>
    /// Reads audio samples, feeds all enabled analyzers, and passes through unchanged.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        // Feed all analyzers
        ProcessAnalyzers(buffer, offset, samplesRead);

        return samplesRead;
    }

    /// <summary>
    /// Resets all analyzers.
    /// </summary>
    public void Reset()
    {
        _spectrumAnalyzer?.Reset();
        _correlationMeter?.Reset();
        _peakDetector?.Reset();
        _goniometer?.Clear();

        lock (_lock)
        {
            foreach (var analyzer in _analyzers)
            {
                analyzer.Reset();
            }
        }
    }

    private void ProcessAnalyzers(float[] buffer, int offset, int count)
    {
        int channels = _source.WaveFormat.Channels;

        // Process built-in analyzers if enabled
        if (SpectrumEnabled && _spectrumAnalyzer != null)
        {
            _spectrumAnalyzer.ProcessSamples(buffer, count, channels);
        }

        if (CorrelationEnabled && _correlationMeter != null && channels == 2)
        {
            _correlationMeter.AnalyzeSamples(buffer, count);
        }

        if (PeakDetectionEnabled && _peakDetector != null)
        {
            _peakDetector.AnalyzeSamples(buffer, count);
        }

        if (GoniometerEnabled && _goniometer != null && channels == 2)
        {
            _goniometer.AnalyzeSamples(buffer, count);
        }

        // Process custom analyzers
        lock (_lock)
        {
            foreach (var analyzer in _analyzers)
            {
                analyzer.ProcessSamples(buffer, offset, count, channels);
            }
        }
    }
}

/// <summary>
/// Interface for custom analyzers that can be added to the analysis chain.
/// </summary>
public interface IAnalyzer
{
    /// <summary>
    /// Processes audio samples for analysis.
    /// </summary>
    /// <param name="samples">Audio sample buffer.</param>
    /// <param name="offset">Offset into the buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <param name="channels">Number of audio channels.</param>
    void ProcessSamples(float[] samples, int offset, int count, int channels);

    /// <summary>
    /// Resets the analyzer state.
    /// </summary>
    void Reset();
}
