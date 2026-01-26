// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// === Sample Fluent API ===

/// <summary>
/// Fluent API for creating and configuring sample instruments.
/// </summary>
public class SampleControl
{
    private readonly ScriptGlobals _globals;
    public SampleControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Creates a new sampler.
    /// </summary>
    public SamplerBuilder create(string? name = null)
    {
        var sampler = _globals.CreateSampler(name);
        return new SamplerBuilder(_globals, sampler);
    }

    /// <summary>Alias for create - PascalCase version</summary>
    public SamplerBuilder Create(string? name = null) => create(name);
    /// <summary>Alias for create - Creates a new sampler</summary>
    public SamplerBuilder @new(string? name = null) => create(name);
    /// <summary>Alias for create - Makes a new sampler</summary>
    public SamplerBuilder make(string? name = null) => create(name);

    /// <summary>
    /// Loads a single sample as an instrument.
    /// </summary>
    public SamplerBuilder load(string filePath, int rootNote = 60)
    {
        var sampler = _globals.CreateSamplerFromFile(filePath, rootNote);
        return new SamplerBuilder(_globals, sampler);
    }

    /// <summary>Alias for load - PascalCase version</summary>
    public SamplerBuilder Load(string filePath, int rootNote = 60) => load(filePath, rootNote);
    /// <summary>Alias for load - Adds a sample as an instrument</summary>
    public SamplerBuilder add(string filePath, int rootNote = 60) => load(filePath, rootNote);

    /// <summary>
    /// Creates a sampler from a directory of samples.
    /// </summary>
    public SamplerBuilder fromDirectory(string path)
    {
        var sampler = _globals.CreateSamplerFromDirectory(path);
        return new SamplerBuilder(_globals, sampler);
    }

    /// <summary>Alias for fromDirectory - PascalCase version</summary>
    public SamplerBuilder FromDirectory(string path) => fromDirectory(path);
    /// <summary>Alias for fromDirectory - Short form</summary>
    public SamplerBuilder fromDir(string path) => fromDirectory(path);
    /// <summary>Alias for fromDirectory - Directory-based sampler</summary>
    public SamplerBuilder dir(string path) => fromDirectory(path);
}

/// <summary>
/// Builder for configuring a sample instrument.
/// </summary>
public class SamplerBuilder
{
    private readonly ScriptGlobals _globals;
    private readonly SampleInstrument _sampler;

    public SamplerBuilder(ScriptGlobals globals, SampleInstrument sampler)
    {
        _globals = globals;
        _sampler = sampler;
    }

    /// <summary>
    /// Gets the underlying sampler.
    /// </summary>
    public SampleInstrument Sampler => _sampler;

    /// <summary>
    /// Loads a sample and maps it to a specific note.
    /// </summary>
    public SamplerBuilder map(string filePath, int note)
    {
        _globals.LoadSampleToNote(_sampler, filePath, note);
        return this;
    }

    /// <summary>
    /// Sets the sample directory for relative paths.
    /// </summary>
    public SamplerBuilder directory(string path)
    {
        _sampler.SetSampleDirectory(path);
        return this;
    }

    /// <summary>
    /// Sets the master volume.
    /// </summary>
    public SamplerBuilder volume(float vol)
    {
        _sampler.Volume = vol;
        return this;
    }

    /// <summary>
    /// Sets the name.
    /// </summary>
    public SamplerBuilder name(string n)
    {
        _sampler.Name = n;
        return this;
    }

    /// <summary>
    /// Creates a pattern with this sampler.
    /// </summary>
    public Pattern pattern()
    {
        return _globals.CreatePattern(_sampler);
    }

    /// <summary>
    /// Implicit conversion to SampleInstrument.
    /// </summary>
    public static implicit operator SampleInstrument(SamplerBuilder builder) => builder._sampler;
}
