//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Test utilities for VST plugin testing.

using MusicEngine.Core;
using MusicEngine.Tests.Mocks;

namespace MusicEngine.Tests.Helpers;

/// <summary>
/// Helper methods for VST plugin testing.
/// </summary>
public static class VstTestHelper
{
    /// <summary>
    /// Creates a test VstPluginInfo with default values.
    /// </summary>
    public static VstPluginInfo CreateTestPluginInfo(
        string name = "TestPlugin",
        string path = @"C:\TestPlugins\TestPlugin.dll",
        bool isInstrument = false,
        string vendor = "TestVendor",
        string version = "1.0.0")
    {
        return new VstPluginInfo
        {
            Name = name,
            Path = path,
            Vendor = vendor,
            Version = version,
            UniqueId = name.GetHashCode(),
            IsInstrument = isInstrument,
            IsLoaded = false,
            NumInputs = 2,
            NumOutputs = 2,
            NumParameters = 10,
            NumPrograms = 5
        };
    }

    /// <summary>
    /// Creates a test Vst3PluginInfo with default values.
    /// </summary>
    public static Vst3PluginInfo CreateTestVst3PluginInfo(
        string name = "TestVst3Plugin",
        string path = @"C:\TestPlugins\TestPlugin.vst3",
        bool isInstrument = false,
        string vendor = "TestVst3Vendor",
        string version = "1.0.0",
        bool isBundle = false)
    {
        return new Vst3PluginInfo
        {
            Name = name,
            Path = path,
            ResolvedPath = isBundle ? Path.Combine(path, "Contents", "x86_64-win", $"{name}.vst3") : path,
            Vendor = vendor,
            Version = version,
            IsInstrument = isInstrument,
            IsBundle = isBundle,
            NumInputs = 2,
            NumOutputs = 2
        };
    }

    /// <summary>
    /// Creates a stereo audio buffer filled with zeros.
    /// </summary>
    public static float[] CreateStereoBuffer(int samplesPerChannel)
    {
        return new float[samplesPerChannel * 2];
    }

    /// <summary>
    /// Creates a stereo audio buffer filled with a sine wave.
    /// </summary>
    public static float[] CreateSineWaveBuffer(float frequency, int samplesPerChannel, int sampleRate = 44100)
    {
        var buffer = new float[samplesPerChannel * 2];
        for (int i = 0; i < samplesPerChannel; i++)
        {
            float value = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            buffer[i * 2] = value;        // Left channel
            buffer[i * 2 + 1] = value;    // Right channel
        }
        return buffer;
    }

    /// <summary>
    /// Creates a stereo audio buffer filled with white noise.
    /// </summary>
    public static float[] CreateNoiseBuffer(int samplesPerChannel, float amplitude = 0.5f, int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var buffer = new float[samplesPerChannel * 2];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (float)(random.NextDouble() * 2 - 1) * amplitude;
        }
        return buffer;
    }

    /// <summary>
    /// Creates test preset file content (FXP format header simulation).
    /// </summary>
    public static byte[] CreateTestPresetData(int uniqueId = 12345, int numParams = 10)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write FXP header
        writer.Write(SwapEndian(0x4B6E6343u)); // 'CcnK'
        writer.Write(SwapEndian(48u + (uint)numParams * 4)); // Size
        writer.Write(SwapEndian(0x6B437846u)); // 'FxCk' (preset)
        writer.Write(SwapEndian(1u)); // Version
        writer.Write(SwapEndian((uint)uniqueId)); // FX ID
        writer.Write(SwapEndian(1u)); // FX Version
        writer.Write(SwapEndian((uint)numParams)); // Num params

        // Write preset name (28 bytes)
        byte[] nameBytes = new byte[28];
        byte[] nameData = System.Text.Encoding.ASCII.GetBytes("TestPreset");
        Array.Copy(nameData, nameBytes, Math.Min(nameData.Length, 27));
        writer.Write(nameBytes);

        // Write parameters
        for (int i = 0; i < numParams; i++)
        {
            writer.Write(SwapEndian(BitConverter.ToUInt32(BitConverter.GetBytes(0.5f), 0)));
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Creates a temporary directory for VST plugin testing.
    /// </summary>
    public static string CreateTempVstDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"MusicEngineTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    /// <summary>
    /// Creates a temporary dummy VST DLL file for testing plugin discovery.
    /// </summary>
    public static string CreateDummyVstDll(string directory, string name = "TestPlugin.dll")
    {
        var path = Path.Combine(directory, name);
        // Create a file with at least 1KB to pass size validation
        File.WriteAllBytes(path, new byte[2048]);
        return path;
    }

    /// <summary>
    /// Creates a temporary dummy VST3 bundle structure for testing.
    /// </summary>
    public static string CreateDummyVst3Bundle(string directory, string name = "TestPlugin.vst3")
    {
        var bundlePath = Path.Combine(directory, name);
        var contentsPath = Path.Combine(bundlePath, "Contents", "x86_64-win");
        Directory.CreateDirectory(contentsPath);

        // Create the plugin binary
        var binaryPath = Path.Combine(contentsPath, name);
        File.WriteAllBytes(binaryPath, new byte[2048]);

        return bundlePath;
    }

    /// <summary>
    /// Cleans up a temporary test directory.
    /// </summary>
    public static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Verifies that audio buffer contains non-silent samples.
    /// </summary>
    public static bool HasAudioContent(float[] buffer, float threshold = 0.001f)
    {
        foreach (var sample in buffer)
        {
            if (Math.Abs(sample) > threshold)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Verifies that audio buffer is silent.
    /// </summary>
    public static bool IsSilent(float[] buffer, float threshold = 0.001f)
    {
        return !HasAudioContent(buffer, threshold);
    }

    /// <summary>
    /// Calculates the peak amplitude of an audio buffer.
    /// </summary>
    public static float GetPeakAmplitude(float[] buffer)
    {
        float peak = 0;
        foreach (var sample in buffer)
        {
            float abs = Math.Abs(sample);
            if (abs > peak)
                peak = abs;
        }
        return peak;
    }

    /// <summary>
    /// Calculates the RMS level of an audio buffer.
    /// </summary>
    public static float GetRmsLevel(float[] buffer)
    {
        if (buffer.Length == 0)
            return 0;

        double sum = 0;
        foreach (var sample in buffer)
        {
            sum += sample * sample;
        }
        return (float)Math.Sqrt(sum / buffer.Length);
    }

    /// <summary>
    /// Compares two audio buffers for approximate equality.
    /// </summary>
    public static bool BuffersAreEqual(float[] a, float[] b, float tolerance = 0.0001f)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > tolerance)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Creates a mock VstHost configuration for testing.
    /// </summary>
    public static void ConfigureTestVstPaths(string tempDirectory)
    {
        Settings.VstPluginSearchPaths = new List<string> { tempDirectory };
        Settings.VstPluginPath = tempDirectory;
    }

    /// <summary>
    /// Restores default VST paths after testing.
    /// </summary>
    public static void RestoreDefaultVstPaths()
    {
        Settings.ResetVstPathsToDefaults();
        Settings.VstPluginPath = "";
    }

    /// <summary>
    /// Creates a test preset file on disk.
    /// </summary>
    public static string CreateTestPresetFile(string directory, string name = "TestPreset.fxp")
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, CreateTestPresetData());
        return path;
    }

    /// <summary>
    /// Creates a test VST3 preset file on disk.
    /// </summary>
    public static string CreateTestVst3PresetFile(string directory, string name = "TestPreset.vstpreset")
    {
        var path = Path.Combine(directory, name);
        // VST3 preset format is different, create a minimal valid structure
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        // Write VST3 preset header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("VST3"));
        writer.Write(1); // Version
        writer.Write(Guid.NewGuid().ToByteArray()); // Class ID
        writer.Write(0L); // Component state offset
        writer.Write(0L); // Controller state offset

        return path;
    }

    /// <summary>
    /// Swaps endianness of a 32-bit unsigned integer.
    /// </summary>
    private static uint SwapEndian(uint value)
    {
        return ((value & 0xFF) << 24) |
               ((value & 0xFF00) << 8) |
               ((value & 0xFF0000) >> 8) |
               ((value & 0xFF000000) >> 24);
    }

    /// <summary>
    /// Generates MIDI note data for testing.
    /// </summary>
    public static (int Note, int Velocity)[] GenerateTestNotes(int count, int startNote = 60, int velocity = 100)
    {
        var notes = new (int Note, int Velocity)[count];
        for (int i = 0; i < count; i++)
        {
            notes[i] = (startNote + i, velocity);
        }
        return notes;
    }

    /// <summary>
    /// Generates a sequence of parameter values for automation testing.
    /// </summary>
    public static float[] GenerateAutomationCurve(int points, float startValue = 0f, float endValue = 1f)
    {
        var values = new float[points];
        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            values[i] = startValue + (endValue - startValue) * t;
        }
        return values;
    }
}
