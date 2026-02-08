// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modulation variable wrapper.

using System;
using System.Collections.Generic;
using MusicEngine.Effects.Modulation;

namespace MusicEngine.Core.Modulation;

public sealed class ModVar : IModNode
{
    private readonly Func<float> _get;
    private readonly Action<float> _set;
    private readonly List<IModulator> _modulators = new();
    private float _baseValue;
    private bool _enabled = true;

    internal ModVar(Func<float> get, Action<float> set, float? initial)
    {
        _get = get ?? throw new ArgumentNullException(nameof(get));
        _set = set ?? throw new ArgumentNullException(nameof(set));
        _baseValue = initial ?? _get();
        _set(_baseValue);
        ModEngine.Shared.Register(this);
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public ModVar Enable(bool enabled)
    {
        _enabled = enabled;
        return this;
    }

    public float Value
    {
        get => _baseValue;
        set
        {
            _baseValue = value;
            _set(value);
        }
    }

    public ModVar Set(float value)
    {
        Value = value;
        return this;
    }

    public ModVar Clear()
    {
        _modulators.Clear();
        return this;
    }

    public ModVar Random(float min, float max, double everyMs = 500)
    {
        _modulators.Add(new RandomModulator(min, max, everyMs));
        return this;
    }

    public ModVar Lfo(float min, float max, float rateHz = 1f)
    {
        _modulators.Add(new LfoValueModulator(min, max, rateHz));
        return this;
    }

    public ModVar Map(Func<float, float> transform)
    {
        _modulators.Add(new MapModulator(transform));
        return this;
    }

    public ModVar If(Func<bool> predicate, float whenTrue, float whenFalse)
    {
        _modulators.Add(new IfModulator(predicate, whenTrue, whenFalse));
        return this;
    }

    public void Update(double deltaSeconds)
    {
        if (!_enabled) return;
        if (_modulators.Count == 0) return;

        float value = _baseValue;
        foreach (var modulator in _modulators)
        {
            value = modulator.Apply(value, deltaSeconds);
        }
        _set(value);
    }

    private sealed class RandomModulator : IModulator
    {
        private readonly float _min;
        private readonly float _max;
        private readonly double _interval;
        private readonly Random _rng = new Random();
        private double _elapsed;
        private float _current;

        public RandomModulator(float min, float max, double intervalMs)
        {
            _min = min;
            _max = max;
            _interval = Math.Max(1.0, intervalMs) / 1000.0;
            _current = Next();
        }

        public float Apply(float value, double deltaSeconds)
        {
            _elapsed += deltaSeconds;
            if (_elapsed >= _interval)
            {
                _elapsed = 0;
                _current = Next();
            }
            return _current;
        }

        private float Next()
        {
            return (float)(_min + _rng.NextDouble() * (_max - _min));
        }
    }

    private sealed class LfoValueModulator : IModulator
    {
        private readonly LfoModulator _lfo;

        public LfoValueModulator(float min, float max, float rateHz)
        {
            _lfo = new LfoModulator
            {
                Min = min,
                Max = max,
                RateHz = rateHz
            };
        }

        public float Apply(float value, double deltaSeconds)
        {
            return _lfo.NextValue(deltaSeconds);
        }
    }

    private sealed class MapModulator : IModulator
    {
        private readonly Func<float, float> _map;

        public MapModulator(Func<float, float> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public float Apply(float value, double deltaSeconds)
        {
            return _map(value);
        }
    }

    private sealed class IfModulator : IModulator
    {
        private readonly Func<bool> _predicate;
        private readonly float _trueValue;
        private readonly float _falseValue;

        public IfModulator(Func<bool> predicate, float trueValue, float falseValue)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _trueValue = trueValue;
            _falseValue = falseValue;
        }

        public float Apply(float value, double deltaSeconds)
        {
            return _predicate() ? _trueValue : _falseValue;
        }
    }
}

internal interface IModNode
{
    void Update(double deltaSeconds);
}

internal interface IModulator
{
    float Apply(float value, double deltaSeconds);
}
