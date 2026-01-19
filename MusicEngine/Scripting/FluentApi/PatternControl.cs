//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Fluent API for pattern control operations.


using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// Control for pattern operations
public class PatternControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    public PatternControl(ScriptGlobals globals) => _globals = globals; // Constructor

    public void start(Pattern p) => p.Enabled = true; // Start a pattern
    public void stop(Pattern p) => p.Enabled = false; // Stop a pattern
    public void toggle(Pattern p) => p.Enabled = !p.Enabled; // Toggle a pattern's enabled state
}
