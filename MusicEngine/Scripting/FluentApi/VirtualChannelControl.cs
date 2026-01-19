//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Fluent API for virtual audio channel control operations.


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

    /// <summary>
    /// Lists all virtual channels.
    /// </summary>
    public void list() => _globals.ListVirtualChannels();
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
