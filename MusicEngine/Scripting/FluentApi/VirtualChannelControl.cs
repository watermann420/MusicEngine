// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// === Virtual Channel Fluent API ===

/// <summary>
/// Fluent API for virtual audio channels.
/// </summary>
public class VirtualChannelControl
{
    private readonly ScriptGlobals _globals;
    public VirtualChannelControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Creates a new virtual channel.
    /// </summary>
    public VirtualChannelBuilder create(string name)
    {
        var channel = _globals.CreateVirtualChannel(name);
        return new VirtualChannelBuilder(channel);
    }

    /// <summary>Alias for create - PascalCase version</summary>
    public VirtualChannelBuilder Create(string name) => create(name);
    /// <summary>Alias for create - Creates a new virtual channel</summary>
    public VirtualChannelBuilder @new(string name) => create(name);
    /// <summary>Alias for create - Makes a new virtual channel</summary>
    public VirtualChannelBuilder make(string name) => create(name);

    /// <summary>
    /// Lists all virtual channels.
    /// </summary>
    public void list() => _globals.ListVirtualChannels();

    /// <summary>Alias for list - PascalCase version</summary>
    public void List() => list();
    /// <summary>Alias for list - Shows all virtual channels</summary>
    public void show() => list();
}

/// <summary>
/// Builder for configuring virtual channels.
/// </summary>
public class VirtualChannelBuilder
{
    private readonly VirtualAudioChannel _channel;

    public VirtualChannelBuilder(VirtualAudioChannel channel)
    {
        _channel = channel;
    }

    /// <summary>
    /// Gets the underlying channel.
    /// </summary>
    public VirtualAudioChannel Channel => _channel;

    /// <summary>
    /// Sets the volume.
    /// </summary>
    public VirtualChannelBuilder volume(float vol)
    {
        _channel.Volume = vol;
        return this;
    }

    /// <summary>
    /// Starts the channel.
    /// </summary>
    public VirtualChannelBuilder start()
    {
        _channel.Start();
        return this;
    }

    /// <summary>
    /// Stops the channel.
    /// </summary>
    public VirtualChannelBuilder stop()
    {
        _channel.Stop();
        return this;
    }

    /// <summary>
    /// Gets the pipe name for connecting from other applications.
    /// </summary>
    public string pipeName => _channel.PipeName;

    /// <summary>
    /// Implicit conversion to VirtualAudioChannel.
    /// </summary>
    public static implicit operator VirtualAudioChannel(VirtualChannelBuilder builder) => builder._channel;
}
