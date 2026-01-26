using NAudio.Wave;

namespace MusicEngine.Tests.Helpers;

/// <summary>
/// Helper methods for audio testing.
/// </summary>
public static class AudioTestHelper
{
    /// <summary>
    /// Calculates the RMS (Root Mean Square) of audio samples.
    /// </summary>
    public static float CalculateRms(float[] samples)
    {
        if (samples.Length == 0) return 0;

        float sum = 0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }
        return (float)Math.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// Calculates the peak amplitude of audio samples.
    /// </summary>
    public static float CalculatePeak(float[] samples)
    {
        float peak = 0;
        foreach (var sample in samples)
        {
            float abs = Math.Abs(sample);
            if (abs > peak) peak = abs;
        }
        return peak;
    }

    /// <summary>
    /// Checks if samples are approximately silent (below threshold).
    /// </summary>
    public static bool IsSilent(float[] samples, float threshold = 0.001f)
    {
        return CalculatePeak(samples) < threshold;
    }

    /// <summary>
    /// Compares two sample arrays for approximate equality.
    /// </summary>
    public static bool SamplesAreEqual(float[] a, float[] b, float tolerance = 0.0001f)
    {
        if (a.Length != b.Length) return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > tolerance) return false;
        }
        return true;
    }

    /// <summary>
    /// Generates a buffer of specified size from a sample provider.
    /// </summary>
    public static float[] ReadSamples(ISampleProvider provider, int count)
    {
        var buffer = new float[count];
        int read = provider.Read(buffer, 0, count);
        if (read < count)
        {
            Array.Resize(ref buffer, read);
        }
        return buffer;
    }
}
