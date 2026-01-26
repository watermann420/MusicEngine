// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Pattern container for musical events.

using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// Control for pattern operations
public class PatternControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    public PatternControl(ScriptGlobals globals) => _globals = globals; // Constructor

    public void start(Pattern p) => p.Enabled = true; // Start a pattern

    /// <summary>Alias for start - PascalCase version</summary>
    public void Start(Pattern p) => start(p);
    /// <summary>Alias for start - Plays a pattern</summary>
    public void play(Pattern p) => start(p);
    /// <summary>Alias for start - Enables a pattern</summary>
    public void enable(Pattern p) => start(p);

    public void stop(Pattern p) => p.Enabled = false; // Stop a pattern

    /// <summary>Alias for stop - PascalCase version</summary>
    public void Stop(Pattern p) => stop(p);
    /// <summary>Alias for stop - Pauses a pattern</summary>
    public void pause(Pattern p) => stop(p);
    /// <summary>Alias for stop - Disables a pattern</summary>
    public void disable(Pattern p) => stop(p);

    public void toggle(Pattern p) => p.Enabled = !p.Enabled; // Toggle a pattern's enabled state

    /// <summary>Alias for toggle - PascalCase version</summary>
    public void Toggle(Pattern p) => toggle(p);
    /// <summary>Alias for toggle - Switches pattern state</summary>
    public void @switch(Pattern p) => toggle(p);
}
