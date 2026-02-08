// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Named MIDI mapping helper for scripts and device presets.

using System;
using System.Collections.Generic;
using MusicEngine.Scripting.FluentApi;

namespace MusicEngine.Scripting;

/// <summary>
/// Named MIDI mapping helper for scripts and device presets.
/// </summary>
public sealed class MidiMap
{
    private readonly Dictionary<string, int> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _notes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JogMapping> _jogs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Map a name to a control change ID.
    /// </summary>
    public void Set(string name, int controlId)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _controls[name] = controlId;
    }

    /// <summary>
    /// Map a name to a MIDI note number.
    /// </summary>
    public void SetNote(string name, int note)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _notes[name] = note;
    }

    /// <summary>
    /// Map a name to a jog wheel definition.
    /// </summary>
    public void SetJog(string name, int controlId, JogMode mode = JogMode.RelativeSigned, int scale = 1)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _jogs[name] = new JogMapping(controlId, mode, Math.Max(1, scale));
    }

    /// <summary>
    /// Get a control change ID by name.
    /// </summary>
    public int Get(string name, int fallback = -1)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        return _controls.TryGetValue(name, out var id) ? id : fallback;
    }

    /// <summary>
    /// Get a MIDI note number by name.
    /// </summary>
    public int GetNote(string name, int fallback = -1)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        return _notes.TryGetValue(name, out var note) ? note : fallback;
    }

    /// <summary>
    /// Try get a jog mapping by name.
    /// </summary>
    public bool TryGetJog(string name, out JogMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            mapping = default;
            return false;
        }
        return _jogs.TryGetValue(name, out mapping);
    }
}

/// <summary>
/// Jog wheel mapping details.
/// </summary>
public readonly record struct JogMapping(int ControlId, JogMode Mode, int Scale);

/// <summary>
/// Library of named MIDI mapping presets.
/// </summary>
public static class MidiMapLibrary
{
    private static readonly Dictionary<string, MidiMap> Maps = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string name, MidiMap map)
    {
        if (string.IsNullOrWhiteSpace(name) || map == null) return;
        Maps[name] = map;
    }

    public static MidiMap? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Maps.TryGetValue(name, out var map) ? map : null;
    }

    public static IReadOnlyList<string> List()
    {
        var keys = new List<string>(Maps.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return keys;
    }
}
