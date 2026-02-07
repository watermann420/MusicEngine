// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal sequencer for pattern playback.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MusicEngine.Timing;

namespace MusicEngine.Core;

/// <summary>
/// Background sequencer that advances patterns over time.
/// </summary>
public sealed class Sequencer : IDisposable
{
    private readonly object _lock = new();
    private readonly List<Pattern> _patterns = new();
    private Thread? _thread;
    private volatile bool _running;
    private double _currentTimeSeconds;
    /// <summary>
    /// Timing master controlling BPM and groove.
    /// </summary>
    public TimingMaster Timing { get; } = new TimingMaster();

    /// <summary>
    /// Current tempo in beats per minute.
    /// </summary>
    public double Bpm
    {
        get => Timing.Bpm;
        set => Timing.Bpm = value;
    }

    /// <summary>
    /// True while the sequencer thread is running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Current beat position of the sequencer.
    /// </summary>
    public double CurrentBeat
    {
        get => _currentTimeSeconds * Timing.Bpm / 60.0;
        set => _currentTimeSeconds = Timing.Bpm <= 0 ? 0 : value * 60.0 / Timing.Bpm;
    }

    /// <summary>
    /// Current time in seconds.
    /// </summary>
    public double CurrentTimeSeconds => _currentTimeSeconds;

    /// <summary>
    /// Snapshot of registered patterns.
    /// </summary>
    public IReadOnlyList<Pattern> Patterns
    {
        get
        {
            lock (_lock)
            {
                return _patterns.ToArray();
            }
        }
    }

    /// <summary>
    /// Start the sequencer thread.
    /// </summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Sequencer"
        };
        _thread.Start();
    }

    /// <summary>
    /// Stop the sequencer thread.
    /// </summary>
    public void Stop()
    {
        _running = false;
        _thread?.Join(200);
    }

    /// <summary>
    /// Add a pattern to the sequencer.
    /// </summary>
    public void AddPattern(Pattern pattern)
    {
        lock (_lock)
        {
            if (_patterns.Contains(pattern)) return;
            pattern.Sequencer = this;
            _patterns.Add(pattern);
        }
    }

    /// <summary>
    /// Remove a pattern from the sequencer.
    /// </summary>
    public void RemovePattern(Pattern pattern)
    {
        lock (_lock)
        {
            _patterns.Remove(pattern);
        }
    }

    /// <summary>
    /// Remove all patterns and stop their playback.
    /// </summary>
    public void ClearPatterns()
    {
        lock (_lock)
        {
            var snapshot = _patterns.ToArray();
            _patterns.Clear();
            foreach (var pattern in snapshot)
            {
                pattern.Stop();
            }
        }
    }

    private void Run()
    {
        var sw = Stopwatch.StartNew();
        double lastTime = sw.Elapsed.TotalSeconds;

        while (_running)
        {
            double now = sw.Elapsed.TotalSeconds;
            double delta = now - lastTime;
            lastTime = now;

            Pattern[] snapshot;
            lock (_lock)
            {
                _currentTimeSeconds += delta;
                snapshot = _patterns.ToArray();
            }

            foreach (var pattern in snapshot)
            {
                pattern.Process(delta, Timing);
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Stop the sequencer and clear patterns.
    /// </summary>
    public void Dispose()
    {
        Stop();
        ClearPatterns();
    }
}
