// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Fluent random helpers for scripts.

using System;

namespace MusicEngine.Scripting.FluentApi;

/// <summary>Entry point: ScriptGlobals.random</summary>
public class RandomControl
{
    private readonly Random _rnd = new Random();

    /// <summary>Next double in [0,1).</summary>
    public double next() => _rnd.NextDouble();

    /// <summary>Next int in [min, max).</summary>
    public int intRange(int min, int max) => _rnd.Next(min, max);

    /// <summary>Create a range helper.</summary>
    public RandomRange range(double min, double max) => new RandomRange(_rnd, min, max);

    /// <summary>Alias.</summary>
    public RandomRange Range(double min, double max) => range(min, max);

    /// <summary>Random bool with probability (default 0.5).</summary>
    public bool nextBool(double probability = 0.5)
    {
        if (probability <= 0) return false;
        if (probability >= 1) return true;
        return _rnd.NextDouble() < probability;
    }

    /// <summary>Alias for nextBool.</summary>
    public bool chance(double probability = 0.5) => nextBool(probability);

    /// <summary>Fair coin flip (50/50).</summary>
    public bool coin() => nextBool(0.5);
}

/// <summary>Random range with optional speed limiting (Hz).</summary>
public class RandomRange
{
    private readonly Random _rnd;
    private readonly double _min;
    private readonly double _max;
    private double _speedHz = double.PositiveInfinity; // unlimited
    private double _lastValue;
    private DateTime _lastUpdate = DateTime.MinValue;

    public RandomRange(Random rnd, double min, double max)
    {
        _rnd = rnd;
        _min = min;
        _max = max;
        _lastValue = NextImmediate();
        _lastUpdate = DateTime.UtcNow;
    }

    /// <summary>Limit how often a new value is generated (Hz). Example: .speed(2) updates every 0.5s max.</summary>
    public RandomRange speed(double hz)
    {
        _speedHz = hz <= 0 ? double.PositiveInfinity : hz;
        return this;
    }

    /// <summary>Get a value respecting speed limit.</summary>
    public double next()
    {
        var now = DateTime.UtcNow;
        var minDelta = double.IsPositiveInfinity(_speedHz) ? TimeSpan.Zero : TimeSpan.FromSeconds(1.0 / _speedHz);
        if (now - _lastUpdate >= minDelta)
        {
            _lastValue = NextImmediate();
            _lastUpdate = now;
        }
        return _lastValue;
    }

    /// <summary>Get a fresh value (ignores speed).</summary>
    public double NextImmediate()
    {
        return _min + _rnd.NextDouble() * (_max - _min);
    }
}
