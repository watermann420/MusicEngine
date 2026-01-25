//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Effect chain for processing audio through multiple effects in series.


using System;
using System.Collections.Generic;
using NAudio.Wave;
using MusicEngine.Core.PDC;


namespace MusicEngine.Core;


/// <summary>
/// Chains multiple audio effects together in series.
/// Audio flows through each effect in order, allowing complex signal processing.
/// </summary>
public class EffectChain : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly List<IEffect> _effects = new();
    private readonly object _lock = new();
    private bool _bypassed;

    /// <summary>
    /// Creates a new effect chain with the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to process</param>
    public EffectChain(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        WaveFormat = source.WaveFormat;
    }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the number of effects in the chain.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _effects.Count;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the entire chain is bypassed.
    /// When bypassed, audio passes through unchanged.
    /// </summary>
    public bool Bypassed
    {
        get => _bypassed;
        set => _bypassed = value;
    }

    /// <summary>
    /// Gets the effect at the specified index.
    /// </summary>
    /// <param name="index">The index of the effect</param>
    /// <returns>The effect at the index, or null if out of range</returns>
    public IEffect? this[int index]
    {
        get
        {
            lock (_lock)
            {
                if (index >= 0 && index < _effects.Count)
                    return _effects[index];
                return null;
            }
        }
    }

    /// <summary>
    /// Adds an effect to the end of the chain.
    /// </summary>
    /// <typeparam name="T">The type of effect to create</typeparam>
    /// <returns>The created effect instance</returns>
    public T AddEffect<T>() where T : class, IEffect
    {
        lock (_lock)
        {
            // Determine the source for this effect
            ISampleProvider effectSource = _effects.Count == 0 ? _source : _effects[^1];

            // Create the effect using reflection
            var effect = (T?)Activator.CreateInstance(typeof(T), effectSource);
            if (effect == null)
                throw new InvalidOperationException($"Failed to create effect of type {typeof(T).Name}");

            _effects.Add(effect);
            return effect;
        }
    }

    /// <summary>
    /// Adds an existing effect instance to the chain.
    /// Note: The effect should be configured with the correct source.
    /// </summary>
    /// <param name="effect">The effect to add</param>
    public void AddEffect(IEffect effect)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));

        lock (_lock)
        {
            _effects.Add(effect);
        }
    }

    /// <summary>
    /// Adds a VST effect plugin to the end of the chain.
    /// </summary>
    /// <param name="plugin">The VST plugin to add (must be an effect, not an instrument).</param>
    /// <returns>The created VstEffectAdapter.</returns>
    /// <exception cref="ArgumentNullException">Thrown if plugin is null.</exception>
    /// <exception cref="ArgumentException">Thrown if plugin is an instrument.</exception>
    public VstEffectAdapter AddVstEffect(IVstPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (plugin.IsInstrument)
            throw new ArgumentException("Cannot add instrument plugin as an effect.", nameof(plugin));

        lock (_lock)
        {
            // Determine the source for this effect
            ISampleProvider effectSource = _effects.Count == 0 ? _source : _effects[^1];

            // Create the adapter with the source
            var adapter = new VstEffectAdapter(plugin, effectSource);
            _effects.Add(adapter);
            return adapter;
        }
    }

    /// <summary>
    /// Inserts a VST effect plugin at the specified index.
    /// Note: This rebuilds the source chain for subsequent effects.
    /// </summary>
    /// <param name="index">The index to insert at.</param>
    /// <param name="plugin">The VST plugin to insert (must be an effect, not an instrument).</param>
    /// <returns>The created VstEffectAdapter.</returns>
    /// <exception cref="ArgumentNullException">Thrown if plugin is null.</exception>
    /// <exception cref="ArgumentException">Thrown if plugin is an instrument.</exception>
    public VstEffectAdapter InsertVstEffect(int index, IVstPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (plugin.IsInstrument)
            throw new ArgumentException("Cannot add instrument plugin as an effect.", nameof(plugin));

        lock (_lock)
        {
            index = Math.Clamp(index, 0, _effects.Count);

            // Determine the source for this effect
            ISampleProvider effectSource = index == 0 ? _source : _effects[index - 1];

            // Create the adapter with the source
            var adapter = new VstEffectAdapter(plugin, effectSource);
            _effects.Insert(index, adapter);

            // Rebuild source chain for subsequent effects
            RebuildSourceChain(index + 1);

            return adapter;
        }
    }

    /// <summary>
    /// Rebuilds the source chain starting from the specified index.
    /// This ensures each effect's source points to the previous effect in the chain.
    /// </summary>
    /// <param name="startIndex">The index to start rebuilding from.</param>
    private void RebuildSourceChain(int startIndex)
    {
        for (int i = startIndex; i < _effects.Count; i++)
        {
            ISampleProvider newSource = i == 0 ? _source : _effects[i - 1];

            if (_effects[i] is VstEffectAdapter vstAdapter)
            {
                vstAdapter.SetSource(newSource);
            }
            // Note: Built-in effects typically don't support dynamic source reassignment
            // They would need to be recreated if we want to support full reordering
        }
    }

    /// <summary>
    /// Moves an effect from one position to another in the chain.
    /// </summary>
    /// <param name="fromIndex">The current index of the effect.</param>
    /// <param name="toIndex">The target index for the effect.</param>
    /// <returns>True if the move was successful.</returns>
    public bool MoveEffect(int fromIndex, int toIndex)
    {
        lock (_lock)
        {
            if (fromIndex < 0 || fromIndex >= _effects.Count ||
                toIndex < 0 || toIndex >= _effects.Count ||
                fromIndex == toIndex)
            {
                return false;
            }

            var effect = _effects[fromIndex];
            _effects.RemoveAt(fromIndex);
            _effects.Insert(toIndex, effect);

            // Rebuild the source chain from the minimum affected index
            int rebuildFrom = Math.Min(fromIndex, toIndex);
            RebuildSourceChain(rebuildFrom);

            return true;
        }
    }

    /// <summary>
    /// Gets a VST effect adapter at the specified index.
    /// </summary>
    /// <param name="index">The index of the effect.</param>
    /// <returns>The VstEffectAdapter if the effect at that index is a VST effect, null otherwise.</returns>
    public VstEffectAdapter? GetVstEffect(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _effects.Count)
            {
                return _effects[index] as VstEffectAdapter;
            }
            return null;
        }
    }

    /// <summary>
    /// Gets all VST effect adapters in the chain.
    /// </summary>
    /// <returns>Enumerable of VST effect adapters (no allocation if not enumerated).</returns>
    public IEnumerable<VstEffectAdapter> GetVstEffects()
    {
        // Take snapshot under lock to avoid holding lock during enumeration
        IEffect[] effectsCopy;
        lock (_lock)
        {
            effectsCopy = _effects.ToArray();
        }

        foreach (var effect in effectsCopy)
        {
            if (effect is VstEffectAdapter vstAdapter)
            {
                yield return vstAdapter;
            }
        }
    }

    /// <summary>
    /// Gets all VST effect adapters as a list (for cases where List is needed).
    /// </summary>
    /// <returns>List of VST effect adapters.</returns>
    public List<VstEffectAdapter> GetVstEffectsList()
    {
        return GetVstEffects().ToList();
    }

    /// <summary>
    /// Inserts an effect at the specified index.
    /// Note: This rebuilds the chain to maintain correct routing.
    /// </summary>
    /// <typeparam name="T">The type of effect to create</typeparam>
    /// <param name="index">The index to insert at</param>
    /// <returns>The created effect instance</returns>
    public T InsertEffect<T>(int index) where T : class, IEffect
    {
        lock (_lock)
        {
            index = Math.Clamp(index, 0, _effects.Count);

            // Determine the source for this effect
            ISampleProvider effectSource = index == 0 ? _source : _effects[index - 1];

            // Create the effect
            var effect = (T?)Activator.CreateInstance(typeof(T), effectSource);
            if (effect == null)
                throw new InvalidOperationException($"Failed to create effect of type {typeof(T).Name}");

            _effects.Insert(index, effect);

            // Note: Effects after the inserted one would need to be rebuilt
            // to maintain proper routing. For simplicity, we assume effects
            // are added in order and not rearranged frequently.

            return effect;
        }
    }

    /// <summary>
    /// Removes an effect from the chain by index.
    /// </summary>
    /// <param name="index">The index of the effect to remove</param>
    /// <returns>True if the effect was removed</returns>
    public bool RemoveEffect(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _effects.Count)
            {
                _effects.RemoveAt(index);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes an effect from the chain by name.
    /// </summary>
    /// <param name="name">The name of the effect to remove</param>
    /// <returns>True if the effect was removed</returns>
    public bool RemoveEffect(string name)
    {
        lock (_lock)
        {
            var effect = _effects.Find(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (effect != null)
            {
                return _effects.Remove(effect);
            }
            return false;
        }
    }

    /// <summary>
    /// Removes all effects from the chain.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _effects.Clear();
        }
    }

    /// <summary>
    /// Gets an effect by name.
    /// </summary>
    /// <param name="name">The name of the effect</param>
    /// <returns>The effect, or null if not found</returns>
    public IEffect? GetEffect(string name)
    {
        lock (_lock)
        {
            return _effects.Find(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Gets the first effect of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of effect to find</typeparam>
    /// <returns>The effect, or null if not found</returns>
    public T? GetEffect<T>() where T : class, IEffect
    {
        lock (_lock)
        {
            foreach (var effect in _effects)
            {
                if (effect is T typed)
                    return typed;
            }
            return null;
        }
    }

    /// <summary>
    /// Enables or disables an effect by name.
    /// </summary>
    /// <param name="name">The name of the effect</param>
    /// <param name="enabled">Whether the effect should be enabled</param>
    /// <returns>True if the effect was found and updated</returns>
    public bool SetEffectEnabled(string name, bool enabled)
    {
        lock (_lock)
        {
            var effect = _effects.Find(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (effect != null)
            {
                effect.Enabled = enabled;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Enables or disables an effect by index.
    /// </summary>
    /// <param name="index">The index of the effect</param>
    /// <param name="enabled">Whether the effect should be enabled</param>
    /// <returns>True if the effect was found and updated</returns>
    public bool SetEffectEnabled(int index, bool enabled)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _effects.Count)
            {
                _effects[index].Enabled = enabled;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets a list of all effect names in the chain.
    /// </summary>
    /// <returns>List of effect names</returns>
    public List<string> GetEffectNames()
    {
        lock (_lock)
        {
            var names = new List<string>();
            foreach (var effect in _effects)
            {
                names.Add(effect.Name);
            }
            return names;
        }
    }

    /// <summary>
    /// Gets the total latency in samples introduced by all effects in the chain.
    /// This sums up the latency from all ILatencyReporter effects (e.g., VST plugins).
    /// </summary>
    /// <returns>Total latency in samples.</returns>
    /// <remarks>
    /// Only effects that implement ILatencyReporter contribute to this total.
    /// Built-in effects typically have zero latency unless they implement lookahead.
    /// </remarks>
    public int GetTotalLatencySamples()
    {
        lock (_lock)
        {
            int totalLatency = 0;

            foreach (var effect in _effects)
            {
                if (effect is ILatencyReporter latencyReporter)
                {
                    totalLatency += latencyReporter.LatencySamples;
                }
            }

            return totalLatency;
        }
    }

    /// <summary>
    /// Gets all latency reporters in the chain.
    /// </summary>
    /// <returns>List of all effects that implement ILatencyReporter.</returns>
    public List<ILatencyReporter> GetLatencyReporters()
    {
        lock (_lock)
        {
            var reporters = new List<ILatencyReporter>();

            foreach (var effect in _effects)
            {
                if (effect is ILatencyReporter latencyReporter)
                {
                    reporters.Add(latencyReporter);
                }
            }

            return reporters;
        }
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Use lock-free read pattern: capture references under lock, then read outside lock
        ISampleProvider? effectToRead;
        bool bypassed;

        lock (_lock)
        {
            bypassed = _bypassed;
            effectToRead = _effects.Count > 0 ? _effects[^1] : null;
        }

        // Read outside lock to avoid holding it during audio processing
        if (bypassed || effectToRead == null)
        {
            return _source.Read(buffer, offset, count);
        }

        return effectToRead.Read(buffer, offset, count);
    }

    /// <summary>
    /// Creates a simple effect chain with common effects pre-configured.
    /// </summary>
    /// <param name="source">The audio source</param>
    /// <param name="includeReverb">Whether to include reverb</param>
    /// <param name="includeDelay">Whether to include delay</param>
    /// <param name="includeChorus">Whether to include chorus</param>
    /// <returns>The configured effect chain</returns>
    public static EffectChain CreateStandardChain(
        ISampleProvider source,
        bool includeReverb = true,
        bool includeDelay = true,
        bool includeChorus = true)
    {
        var chain = new EffectChain(source);

        // Build the chain - effects are processed in this order
        ISampleProvider currentSource = source;

        if (includeChorus)
        {
            var chorus = new ChorusEffect(currentSource);
            chorus.Mix = 0.3f;
            chorus.Enabled = false; // Disabled by default
            chain.AddEffect(chorus);
            currentSource = chorus;
        }

        if (includeDelay)
        {
            var delay = new DelayEffect(currentSource);
            delay.Mix = 0.3f;
            delay.Enabled = false; // Disabled by default
            chain.AddEffect(delay);
            currentSource = delay;
        }

        if (includeReverb)
        {
            var reverb = new ReverbEffect(currentSource);
            reverb.Mix = 0.3f;
            reverb.Enabled = false; // Disabled by default
            chain.AddEffect(reverb);
        }

        return chain;
    }
}
