// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Global time master for patterns, decks, and samplers.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MusicEngine.Instruments;
using MusicEngine.Timing;

namespace MusicEngine.Core;

/// <summary>
/// Global time master controlling playback time for patterns, decks, and samplers.
/// </summary>
public sealed class TimeMasterController : IDisposable
{
    private readonly object _lock = new();
    private readonly List<Pattern> _patterns = new();
    private readonly List<AudioDeck> _decks = new();
    private readonly List<SamplerInstrument> _samplers = new();
    private Thread? _thread;
    private volatile bool _running;
    private double _currentTimeSeconds;
    private int _randomSeed;
    private Random _random = new();

    /// <summary>
    /// Timing master for BPM and groove settings.
    /// </summary>
    public TimingMaster Timing { get; } = new TimingMaster();

    /// <summary>
    /// Playback speed multiplier.
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// When false, time does not advance.
    /// </summary>
    public bool IsPlaying { get; set; } = true;

    /// <summary>
    /// Enable looping between LoopStartSeconds and LoopEndSeconds.
    /// </summary>
    public bool LoopEnabled { get; set; }

    /// <summary>
    /// Loop start time in seconds.
    /// </summary>
    public double LoopStartSeconds { get; set; }

    /// <summary>
    /// Loop end time in seconds.
    /// </summary>
    public double LoopEndSeconds { get; set; }

    /// <summary>
    /// Jog ticks per full wheel revolution.
    /// </summary>
    public int JogTicksPerRevolution { get; set; } = 1024;

    /// <summary>
    /// Seconds represented by one full wheel revolution.
    /// </summary>
    public double JogSecondsPerRevolution { get; set; } = 1.0;

    /// <summary>
    /// Scale factor for scratch deltas.
    /// </summary>
    public double ScratchScale { get; set; } = 1.0;

    /// <summary>
    /// Clamp scratch deltas to this maximum (seconds).
    /// </summary>
    public double MaxScratchSeconds { get; set; } = 2.0;

    /// <summary>
    /// Current time in seconds.
    /// </summary>
    public double CurrentTimeSeconds => _currentTimeSeconds;

    /// <summary>
    /// Start the time master thread.
    /// </summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "TimeMaster"
        };
        _thread.Start();
    }

    /// <summary>
    /// Stop the time master thread.
    /// </summary>
    public void Stop()
    {
        _running = false;
        _thread?.Join(200);
    }

    /// <summary>
    /// Bind a pattern to the time master.
    /// </summary>
    public void BindPattern(Pattern pattern)
    {
        if (pattern == null) return;
        lock (_lock)
        {
            if (_patterns.Contains(pattern)) return;
            _patterns.Add(pattern);
        }
    }

    /// <summary>
    /// Bind an audio deck to the time master.
    /// </summary>
    public void BindDeck(AudioDeck deck)
    {
        if (deck == null) return;
        lock (_lock)
        {
            if (_decks.Contains(deck)) return;
            _decks.Add(deck);
        }
    }

    /// <summary>
    /// Bind a sampler to the time master.
    /// </summary>
    public void BindSampler(SamplerInstrument sampler)
    {
        if (sampler == null) return;
        lock (_lock)
        {
            if (_samplers.Contains(sampler)) return;
            _samplers.Add(sampler);
        }
    }

    /// <summary>
    /// Remove all bindings.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _patterns.Clear();
            _decks.Clear();
            _samplers.Clear();
        }
    }

    /// <summary>
    /// Seek to an absolute time in seconds.
    /// </summary>
    public void SeekSeconds(double seconds)
    {
        lock (_lock)
        {
            _currentTimeSeconds = Math.Max(0, seconds);
            var beat = _currentTimeSeconds * Timing.Bpm / 60.0;
            foreach (var pattern in _patterns)
            {
                pattern.SeekBeat(beat);
            }
            foreach (var deck in _decks)
            {
                deck.SeekSeconds(_currentTimeSeconds);
            }
        }
    }

    /// <summary>
    /// Scratch (scrub) by delta seconds.
    /// </summary>
    public void ScratchSeconds(double deltaSeconds)
    {
        if (deltaSeconds == 0) return;
        if (MaxScratchSeconds > 0)
        {
            deltaSeconds = Math.Clamp(deltaSeconds, -MaxScratchSeconds, MaxScratchSeconds);
        }

        lock (_lock)
        {
            _currentTimeSeconds = Math.Max(0, _currentTimeSeconds + deltaSeconds);
            var beat = _currentTimeSeconds * Timing.Bpm / 60.0;
            foreach (var pattern in _patterns)
            {
                pattern.SeekBeat(beat);
            }
            foreach (var deck in _decks)
            {
                deck.ScratchSeconds(deltaSeconds);
            }
            foreach (var sampler in _samplers)
            {
                sampler.ScratchSeconds(deltaSeconds);
            }
        }
    }

    /// <summary>
    /// Scratch (scrub) by jog ticks.
    /// </summary>
    public void ScratchTicks(int deltaTicks)
    {
        if (JogTicksPerRevolution <= 0) return;
        double secondsPerTick = JogSecondsPerRevolution / JogTicksPerRevolution;
        var delta = deltaTicks * secondsPerTick * ScratchScale;
        ScratchSeconds(delta);
    }

    /// <summary>
    /// Randomize playback time by a max delta in seconds.
    /// </summary>
    public void Randomize(double maxDeltaSeconds)
    {
        if (maxDeltaSeconds <= 0) return;
        double delta = (_random.NextDouble() * 2.0 - 1.0) * maxDeltaSeconds;
        ScratchSeconds(delta);
    }

    /// <summary>
    /// Set a deterministic random seed for time randomization.
    /// </summary>
    public void SetRandomSeed(int seed)
    {
        _randomSeed = seed;
        _random = new Random(seed);
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

            if (!IsPlaying)
            {
                Thread.Sleep(1);
                continue;
            }

            delta *= Speed;

            Pattern[] patterns;
            AudioDeck[] decks;
            SamplerInstrument[] samplers;
            lock (_lock)
            {
                _currentTimeSeconds += delta;
                if (LoopEnabled && LoopEndSeconds > LoopStartSeconds && _currentTimeSeconds >= LoopEndSeconds)
                {
                    _currentTimeSeconds = LoopStartSeconds;
                }

                patterns = _patterns.ToArray();
                decks = _decks.ToArray();
                samplers = _samplers.ToArray();
            }

            foreach (var pattern in patterns)
            {
                pattern.Process(delta, Timing);
            }

            foreach (var deck in decks)
            {
                deck.PlaySpeed = (float)Math.Max(0.01, Speed);
                deck.IsPlaying = true;
            }

            foreach (var sampler in samplers)
            {
                sampler.PlaySpeed = (float)Math.Max(0.01, Speed);
            }

            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        Stop();
        Clear();
    }
}
