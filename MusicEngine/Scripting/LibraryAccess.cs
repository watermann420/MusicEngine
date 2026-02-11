// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Bridge between scripts and optional MusicEngine.Library instruments.

using System;
using System.Collections.Generic;
using MusicEngine.Instruments;

namespace MusicEngine.Scripting;

/// <summary>
/// Script-side access to optional MusicEngine.Library instruments.
/// </summary>
public sealed class LibraryAccess
{
    private readonly ScriptGlobals _globals;

    internal LibraryAccess(ScriptGlobals globals)
    {
        _globals = globals;
    }

    /// <summary>
    /// Create and route a library instrument by full type name.
    /// </summary>
    public dynamic Instrument(string typeName)
    {
        var synth = _globals.Host?.TryCreateLibraryInstrument(typeName);
        if (synth == null)
        {
            return new LibraryInstrumentProxy(typeName);
        }

        _globals.Engine.AddSampleProvider(synth);
        _globals.Host?.RegisterSynth(synth);
        _globals.SetLastInstrument(synth);
        return synth;
    }

    /// <summary>
    /// Check if a library instrument type exists.
    /// </summary>
    public bool Has(string typeName) => _globals.Host?.HasLibraryInstrument(typeName) ?? false;

    /// <summary>
    /// List all available library instrument types.
    /// </summary>
    public IReadOnlyList<string> List()
        => _globals.Host?.GetLibraryInstrumentTypeNames() ?? Array.Empty<string>();
}
