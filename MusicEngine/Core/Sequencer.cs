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

public sealed class Sequencer : IDisposable
{
    private readonly object _lock = new();
    private readonly List<Pattern> _patterns = new();
    private Thread? _thread;
    private volatile bool _running;
    private double _currentTimeSeconds;
    public TimingMaster Timing { get; } = new TimingMaster();

    public double Bpm
    {
        get => Timing.Bpm;
        set => Timing.Bpm = value;
    }

    public bool IsRunning => _running;

    public double CurrentBeat
    {
        get => _currentTimeSeconds * Timing.Bpm / 60.0;
        set => _currentTimeSeconds = Timing.Bpm <= 0 ? 0 : value * 60.0 / Timing.Bpm;
    }

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

    public void Stop()
    {
        _running = false;
        _thread?.Join(200);
    }

    public void AddPattern(Pattern pattern)
    {
        lock (_lock)
        {
            if (_patterns.Contains(pattern)) return;
            pattern.Sequencer = this;
            _patterns.Add(pattern);
        }
    }

    public void RemovePattern(Pattern pattern)
    {
        lock (_lock)
        {
            _patterns.Remove(pattern);
        }
    }

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

    public void Dispose()
    {
        Stop();
        ClearPatterns();
    }
}
