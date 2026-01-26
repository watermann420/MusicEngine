// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Represents information about an available audio input device.
/// </summary>
public sealed class InputDeviceInfo
{
    /// <summary>
    /// Gets the device index used by NAudio WaveIn.
    /// </summary>
    public int DeviceIndex { get; }

    /// <summary>
    /// Gets the friendly name of the device.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the number of input channels supported by the device.
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets whether this is the default recording device.
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Creates a new InputDeviceInfo instance.
    /// </summary>
    /// <param name="deviceIndex">The NAudio device index.</param>
    /// <param name="name">The device name.</param>
    /// <param name="channels">Number of input channels.</param>
    /// <param name="isDefault">Whether this is the default device.</param>
    public InputDeviceInfo(int deviceIndex, string name, int channels, bool isDefault = false)
    {
        DeviceIndex = deviceIndex;
        Name = name;
        Channels = channels;
        IsDefault = isDefault;
    }

    /// <summary>
    /// Gets all available audio input devices.
    /// </summary>
    /// <returns>A collection of InputDeviceInfo for all available input devices.</returns>
    public static IReadOnlyList<InputDeviceInfo> GetAvailableDevices()
    {
        var devices = new List<InputDeviceInfo>();
        int deviceCount = WaveInEvent.DeviceCount;

        for (int i = 0; i < deviceCount; i++)
        {
            try
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                devices.Add(new InputDeviceInfo(
                    deviceIndex: i,
                    name: capabilities.ProductName,
                    channels: capabilities.Channels,
                    isDefault: i == 0
                ));
            }
            catch
            {
                // Skip devices that fail to enumerate
            }
        }

        return devices;
    }

    /// <summary>
    /// Gets the default audio input device.
    /// </summary>
    /// <returns>The default InputDeviceInfo, or null if no devices are available.</returns>
    public static InputDeviceInfo? GetDefaultDevice()
    {
        var devices = GetAvailableDevices();
        return devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
    }

    /// <summary>
    /// Gets an input device by index.
    /// </summary>
    /// <param name="deviceIndex">The device index.</param>
    /// <returns>The InputDeviceInfo for the specified device, or null if not found.</returns>
    public static InputDeviceInfo? GetDevice(int deviceIndex)
    {
        var devices = GetAvailableDevices();
        return devices.FirstOrDefault(d => d.DeviceIndex == deviceIndex);
    }

    /// <summary>
    /// Gets an input device by name.
    /// </summary>
    /// <param name="name">The device name (partial match supported).</param>
    /// <returns>The InputDeviceInfo for the matching device, or null if not found.</returns>
    public static InputDeviceInfo? GetDeviceByName(string name)
    {
        var devices = GetAvailableDevices();
        return devices.FirstOrDefault(d =>
            d.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns a string representation of the device.
    /// </summary>
    public override string ToString()
    {
        string defaultIndicator = IsDefault ? " [Default]" : "";
        return $"{Name} ({Channels}ch){defaultIndicator}";
    }

    /// <summary>
    /// Checks if this device supports the specified sample rate.
    /// Note: This is a best-effort check as WaveIn capabilities are limited.
    /// </summary>
    /// <param name="sampleRate">The sample rate to check.</param>
    /// <returns>True if likely supported, false otherwise.</returns>
    public bool SupportsSampleRate(int sampleRate)
    {
        // Common sample rates that most devices support
        int[] commonRates = [8000, 11025, 16000, 22050, 44100, 48000, 88200, 96000];
        return commonRates.Contains(sampleRate);
    }

    /// <summary>
    /// Gets recommended recording settings for this device.
    /// </summary>
    /// <returns>A WaveFormat with recommended settings.</returns>
    public WaveFormat GetRecommendedFormat()
    {
        // Prefer stereo 44.1kHz 16-bit for compatibility
        int channels = Math.Min(Channels, 2);
        return new WaveFormat(44100, 16, channels);
    }
}

/// <summary>
/// Extension methods for input device enumeration.
/// </summary>
public static class InputDeviceExtensions
{
    /// <summary>
    /// Prints all available input devices to the console.
    /// </summary>
    public static void ListInputDevices()
    {
        var devices = InputDeviceInfo.GetAvailableDevices();

        Console.WriteLine("\n=== Available Audio Input Devices ===");

        if (devices.Count == 0)
        {
            Console.WriteLine("No audio input devices found.");
            return;
        }

        foreach (var device in devices)
        {
            string defaultMark = device.IsDefault ? "*" : " ";
            Console.WriteLine($"  {defaultMark}[{device.DeviceIndex}] {device.Name} ({device.Channels} channels)");
        }

        Console.WriteLine("\n  * = Default device");
    }
}
