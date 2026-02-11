// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modulation groups for enabling/disabling multiple vars.

using System.Collections.Generic;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// Group of mod variables that can be enabled/disabled together.
/// </summary>
public sealed class ModGroup
{
    private readonly List<ModVar> _vars = new();

    /// <summary>
    /// Add mod variables to the group.
    /// </summary>
    public ModGroup Add(params ModVar[] vars)
    {
        foreach (var modVar in vars)
        {
            if (modVar == null) continue;
            if (_vars.Contains(modVar)) continue;
            _vars.Add(modVar);
        }
        return this;
    }

    /// <summary>
    /// Enable or disable all mod variables in the group.
    /// </summary>
    public ModGroup Enable(bool enabled)
    {
        foreach (var modVar in _vars)
        {
            modVar.Enable(enabled);
        }
        return this;
    }
}
