// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Script-friendly random value helper with configurable ranges.

using System;

namespace MusicEngine.Scripting;

/// <summary>
/// Script-friendly random value helper with configurable ranges and modes.
/// </summary>
public sealed class RandomSource
{
    private readonly Random _rng;
    private float _min = 0f;
    private float _max = 1f;
    private int _steps;
    private bool _useBool;
    private float _boolChance = 0.5f;
    private bool _useInt;
    private int _intMin;
    private int _intMax = 1;

    /// <summary>
    /// Create a new random source with a time-based seed.
    /// </summary>
    public RandomSource() : this(Environment.TickCount) { }

    /// <summary>
    /// Create a new random source with an explicit seed.
    /// </summary>
    public RandomSource(int seed)
    {
        _rng = new Random(seed);
    }

    /// <summary>
    /// Set the float range for <see cref="NextFloat"/>.
    /// </summary>
    public RandomSource Range(float min, float max)
    {
        _min = min;
        _max = max;
        _useBool = false;
        _useInt = false;
        return this;
    }

    /// <summary>
    /// Quantize the float range into a number of discrete steps.
    /// </summary>
    public RandomSource Steps(int steps)
    {
        _steps = Math.Max(0, steps);
        return this;
    }

    /// <summary>
    /// Switch to boolean mode with a given true probability.
    /// </summary>
    public RandomSource Bool(float chance = 0.5f)
    {
        _useBool = true;
        _useInt = false;
        _boolChance = Math.Clamp(chance, 0f, 1f);
        return this;
    }

    /// <summary>
    /// Switch to integer mode with a given inclusive range.
    /// </summary>
    public RandomSource Int(int min, int max)
    {
        _useInt = true;
        _useBool = false;
        _intMin = Math.Min(min, max);
        _intMax = Math.Max(min, max);
        return this;
    }

    /// <summary>
    /// Reset to default float range and modes.
    /// </summary>
    public RandomSource Reset()
    {
        _min = 0f;
        _max = 1f;
        _steps = 0;
        _useBool = false;
        _useInt = false;
        _boolChance = 0.5f;
        _intMin = 0;
        _intMax = 1;
        return this;
    }

    /// <summary>
    /// Get the next random float based on current settings.
    /// </summary>
    public float NextFloat()
    {
        if (_useBool)
        {
            return _rng.NextDouble() < _boolChance ? 1f : 0f;
        }

        if (_useInt)
        {
            if (_intMax <= _intMin) return _intMin;
            return _rng.Next(_intMin, _intMax + 1);
        }

        var min = _min;
        var max = _max;
        if (max < min)
        {
            (min, max) = (max, min);
        }

        var value = (float)(_rng.NextDouble() * (max - min) + min);
        if (_steps > 1)
        {
            var stepSize = (max - min) / (_steps - 1f);
            if (stepSize > 0f)
            {
                value = min + MathF.Round((value - min) / stepSize) * stepSize;
            }
        }

        return value;
    }

    /// <summary>
    /// Get the next random boolean based on current probability.
    /// </summary>
    public bool NextBool()
    {
        return _rng.NextDouble() < _boolChance;
    }

    /// <summary>
    /// Get the next random integer based on current range.
    /// </summary>
    public int NextInt()
    {
        if (_intMax <= _intMin) return _intMin;
        return _rng.Next(_intMin, _intMax + 1);
    }

    /// <summary>
    /// Implicit conversion to float using <see cref="NextFloat"/>.
    /// </summary>
    public static implicit operator float(RandomSource source) => source.NextFloat();

    /// <summary>
    /// Implicit conversion to bool using <see cref="NextBool"/>.
    /// </summary>
    public static implicit operator bool(RandomSource source) => source.NextBool();
}
