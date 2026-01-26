// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Modulation routing matrix.

namespace MusicEngine.Core.Modulation;

/// <summary>
/// A modulation matrix that manages modulation sources and their connections to parameters.
/// Allows flexible routing of LFOs, envelopes, and MIDI controllers to any parameter.
/// </summary>
public class ModulationMatrix
{
    private readonly List<ModulationSource> _sources = new();
    private readonly List<ModulationSlot> _slots = new();
    private readonly Dictionary<string, List<ModulationSlot>> _slotsByTarget = new();
    private readonly object _lock = new();

    /// <summary>
    /// Maximum number of modulation sources allowed.
    /// </summary>
    public int MaxSources { get; set; } = 32;

    /// <summary>
    /// Maximum number of modulation slots (routings) allowed.
    /// </summary>
    public int MaxSlots { get; set; } = 64;

    /// <summary>
    /// Gets all modulation sources.
    /// </summary>
    public IReadOnlyList<ModulationSource> Sources
    {
        get
        {
            lock (_lock)
            {
                return _sources.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Gets all modulation slots.
    /// </summary>
    public IReadOnlyList<ModulationSlot> Slots
    {
        get
        {
            lock (_lock)
            {
                return _slots.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Event fired when a modulation source is added.
    /// </summary>
    public event EventHandler<ModulationSource>? SourceAdded;

    /// <summary>
    /// Event fired when a modulation source is removed.
    /// </summary>
    public event EventHandler<ModulationSource>? SourceRemoved;

    /// <summary>
    /// Event fired when a modulation slot is added.
    /// </summary>
    public event EventHandler<ModulationSlot>? SlotAdded;

    /// <summary>
    /// Event fired when a modulation slot is removed.
    /// </summary>
    public event EventHandler<ModulationSlot>? SlotRemoved;

    /// <summary>
    /// Adds a new LFO modulation source.
    /// </summary>
    /// <param name="rate">LFO rate in Hz</param>
    /// <param name="waveform">LFO waveform shape</param>
    /// <returns>The created LFO source</returns>
    public ModulationSource AddLFO(float rate, LFOWaveform waveform)
    {
        var source = new ModulationSource(ModulationSourceType.LFO)
        {
            Name = $"LFO {_sources.Count(s => s.Type == ModulationSourceType.LFO) + 1}",
            Rate = rate,
            Waveform = waveform,
            IsBipolar = true
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a new envelope modulation source.
    /// </summary>
    /// <param name="attack">Attack time in seconds</param>
    /// <param name="decay">Decay time in seconds</param>
    /// <param name="sustain">Sustain level (0-1)</param>
    /// <param name="release">Release time in seconds</param>
    /// <returns>The created envelope source</returns>
    public ModulationSource AddEnvelope(float attack, float decay, float sustain, float release)
    {
        var source = new ModulationSource(ModulationSourceType.Envelope)
        {
            Name = $"Env {_sources.Count(s => s.Type == ModulationSourceType.Envelope) + 1}",
            Attack = attack,
            Decay = decay,
            Sustain = sustain,
            Release = release,
            IsBipolar = false
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a modulation source for MIDI velocity.
    /// </summary>
    /// <returns>The created velocity source</returns>
    public ModulationSource AddVelocitySource()
    {
        var source = new ModulationSource(ModulationSourceType.Velocity)
        {
            Name = "Velocity",
            IsBipolar = false
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a modulation source for aftertouch.
    /// </summary>
    /// <returns>The created aftertouch source</returns>
    public ModulationSource AddAftertouchSource()
    {
        var source = new ModulationSource(ModulationSourceType.Aftertouch)
        {
            Name = "Aftertouch",
            IsBipolar = false
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a modulation source for the mod wheel (CC1).
    /// </summary>
    /// <returns>The created mod wheel source</returns>
    public ModulationSource AddModWheelSource()
    {
        var source = new ModulationSource(ModulationSourceType.ModWheel)
        {
            Name = "Mod Wheel",
            IsBipolar = false
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a modulation source for the expression pedal (CC11).
    /// </summary>
    /// <returns>The created expression source</returns>
    public ModulationSource AddExpressionSource()
    {
        var source = new ModulationSource(ModulationSourceType.Expression)
        {
            Name = "Expression",
            IsBipolar = false
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a random modulation source.
    /// </summary>
    /// <param name="rate">Rate of random value changes in Hz</param>
    /// <returns>The created random source</returns>
    public ModulationSource AddRandomSource(float rate = 1f)
    {
        var source = new ModulationSource(ModulationSourceType.Random)
        {
            Name = $"Random {_sources.Count(s => s.Type == ModulationSourceType.Random) + 1}",
            Rate = rate,
            IsBipolar = true
        };

        AddSource(source);
        return source;
    }

    /// <summary>
    /// Adds a generic modulation source.
    /// </summary>
    /// <param name="source">The source to add</param>
    public void AddSource(ModulationSource source)
    {
        lock (_lock)
        {
            if (_sources.Count >= MaxSources)
            {
                throw new InvalidOperationException($"Maximum number of modulation sources ({MaxSources}) reached");
            }

            _sources.Add(source);
        }

        SourceAdded?.Invoke(this, source);
    }

    /// <summary>
    /// Removes a modulation source and all its connections.
    /// </summary>
    /// <param name="source">The source to remove</param>
    public void RemoveSource(ModulationSource source)
    {
        lock (_lock)
        {
            // Remove all slots using this source
            var slotsToRemove = _slots.Where(s => s.Source == source).ToList();
            foreach (var slot in slotsToRemove)
            {
                RemoveModulationInternal(slot);
            }

            _sources.Remove(source);
        }

        SourceRemoved?.Invoke(this, source);
    }

    /// <summary>
    /// Adds a modulation routing from a source to a target parameter.
    /// </summary>
    /// <param name="source">The modulation source</param>
    /// <param name="target">The target parameter name/path</param>
    /// <param name="amount">Modulation amount (-1 to 1)</param>
    /// <returns>The created modulation slot</returns>
    public ModulationSlot AddModulation(ModulationSource source, string target, float amount)
    {
        var slot = new ModulationSlot
        {
            Source = source,
            TargetParameter = target,
            Amount = amount,
            Bipolar = source.IsBipolar
        };

        lock (_lock)
        {
            if (_slots.Count >= MaxSlots)
            {
                throw new InvalidOperationException($"Maximum number of modulation slots ({MaxSlots}) reached");
            }

            _slots.Add(slot);

            // Index by target parameter for fast lookup
            if (!_slotsByTarget.TryGetValue(target, out var targetSlots))
            {
                targetSlots = new List<ModulationSlot>();
                _slotsByTarget[target] = targetSlots;
            }
            targetSlots.Add(slot);
        }

        SlotAdded?.Invoke(this, slot);
        return slot;
    }

    /// <summary>
    /// Removes a modulation routing.
    /// </summary>
    /// <param name="slot">The slot to remove</param>
    public void RemoveModulation(ModulationSlot slot)
    {
        lock (_lock)
        {
            RemoveModulationInternal(slot);
        }

        SlotRemoved?.Invoke(this, slot);
    }

    private void RemoveModulationInternal(ModulationSlot slot)
    {
        _slots.Remove(slot);

        if (_slotsByTarget.TryGetValue(slot.TargetParameter, out var targetSlots))
        {
            targetSlots.Remove(slot);
            if (targetSlots.Count == 0)
            {
                _slotsByTarget.Remove(slot.TargetParameter);
            }
        }
    }

    /// <summary>
    /// Updates all modulation sources.
    /// Should be called once per audio block or at a regular interval.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds</param>
    public void Process(float deltaTime)
    {
        lock (_lock)
        {
            foreach (var source in _sources)
            {
                source.Update(deltaTime);
            }
        }
    }

    /// <summary>
    /// Gets the modulated value for a parameter.
    /// Combines all modulation sources targeting this parameter.
    /// </summary>
    /// <param name="parameter">The parameter name/path</param>
    /// <param name="baseValue">The parameter's base value</param>
    /// <returns>The modulated value</returns>
    public float GetModulatedValue(string parameter, float baseValue)
    {
        lock (_lock)
        {
            if (!_slotsByTarget.TryGetValue(parameter, out var slots) || slots.Count == 0)
            {
                return baseValue;
            }

            // Sum all modulation offsets
            float totalModulation = 0f;
            foreach (var slot in slots)
            {
                if (slot.IsEnabled)
                {
                    totalModulation += slot.GetModulationOffset();
                }
            }

            return baseValue + totalModulation;
        }
    }

    /// <summary>
    /// Gets the total modulation offset for a parameter (without base value).
    /// </summary>
    /// <param name="parameter">The parameter name/path</param>
    /// <returns>The combined modulation offset</returns>
    public float GetModulationOffset(string parameter)
    {
        lock (_lock)
        {
            if (!_slotsByTarget.TryGetValue(parameter, out var slots) || slots.Count == 0)
            {
                return 0f;
            }

            float totalModulation = 0f;
            foreach (var slot in slots)
            {
                if (slot.IsEnabled)
                {
                    totalModulation += slot.GetModulationOffset();
                }
            }

            return totalModulation;
        }
    }

    /// <summary>
    /// Gets all modulation slots targeting a specific parameter.
    /// </summary>
    /// <param name="parameter">The parameter name/path</param>
    /// <returns>List of modulation slots</returns>
    public IReadOnlyList<ModulationSlot> GetSlotsForParameter(string parameter)
    {
        lock (_lock)
        {
            if (_slotsByTarget.TryGetValue(parameter, out var slots))
            {
                return slots.ToList().AsReadOnly();
            }
            return Array.Empty<ModulationSlot>();
        }
    }

    /// <summary>
    /// Triggers all envelopes.
    /// </summary>
    public void TriggerEnvelopes()
    {
        lock (_lock)
        {
            foreach (var source in _sources.Where(s => s.Type == ModulationSourceType.Envelope))
            {
                source.TriggerEnvelope();
            }
        }
    }

    /// <summary>
    /// Releases all envelopes.
    /// </summary>
    public void ReleaseEnvelopes()
    {
        lock (_lock)
        {
            foreach (var source in _sources.Where(s => s.Type == ModulationSourceType.Envelope))
            {
                source.ReleaseEnvelope();
            }
        }
    }

    /// <summary>
    /// Resets all LFO phases.
    /// </summary>
    public void ResetLFOs()
    {
        lock (_lock)
        {
            foreach (var source in _sources.Where(s => s.Type == ModulationSourceType.LFO))
            {
                source.ResetPhase();
            }
        }
    }

    /// <summary>
    /// Sets the tempo for all tempo-synced LFOs.
    /// </summary>
    /// <param name="bpm">Tempo in beats per minute</param>
    public void SetTempo(float bpm)
    {
        lock (_lock)
        {
            foreach (var source in _sources.Where(s => s.Type == ModulationSourceType.LFO))
            {
                source.SetTempo(bpm);
            }
        }
    }

    /// <summary>
    /// Sets the value of a MIDI-controlled modulation source.
    /// </summary>
    /// <param name="type">The source type (Velocity, Aftertouch, ModWheel, etc.)</param>
    /// <param name="value">The value (0-1)</param>
    public void SetMidiSourceValue(ModulationSourceType type, float value)
    {
        lock (_lock)
        {
            foreach (var source in _sources.Where(s => s.Type == type))
            {
                source.Value = value;
            }
        }
    }

    /// <summary>
    /// Finds a modulation source by its ID.
    /// </summary>
    /// <param name="id">The source ID</param>
    /// <returns>The source, or null if not found</returns>
    public ModulationSource? FindSourceById(Guid id)
    {
        lock (_lock)
        {
            return _sources.FirstOrDefault(s => s.Id == id);
        }
    }

    /// <summary>
    /// Finds a modulation slot by its ID.
    /// </summary>
    /// <param name="id">The slot ID</param>
    /// <returns>The slot, or null if not found</returns>
    public ModulationSlot? FindSlotById(Guid id)
    {
        lock (_lock)
        {
            return _slots.FirstOrDefault(s => s.Id == id);
        }
    }

    /// <summary>
    /// Clears all modulation sources and slots.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _sources.Clear();
            _slots.Clear();
            _slotsByTarget.Clear();
        }
    }
}
