using NAudio.Wave;

namespace MusicEngine.Tests.Mocks;

/// <summary>
/// Mock sample provider for testing audio effects.
/// </summary>
public class MockSampleProvider : ISampleProvider
{
    private readonly WaveFormat _waveFormat;
    private readonly float[] _samples;
    private int _position;

    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Creates a mock sample provider with specified samples.
    /// </summary>
    public MockSampleProvider(float[] samples, int sampleRate = 44100, int channels = 2)
    {
        _samples = samples;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    /// <summary>
    /// Creates a mock sample provider that generates a sine wave.
    /// </summary>
    public static MockSampleProvider CreateSineWave(float frequency, int sampleCount, int sampleRate = 44100, int channels = 2)
    {
        var samples = new float[sampleCount * channels];
        for (int i = 0; i < sampleCount; i++)
        {
            float value = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            for (int ch = 0; ch < channels; ch++)
            {
                samples[i * channels + ch] = value;
            }
        }
        return new MockSampleProvider(samples, sampleRate, channels);
    }

    /// <summary>
    /// Creates a mock sample provider that generates silence.
    /// </summary>
    public static MockSampleProvider CreateSilence(int sampleCount, int sampleRate = 44100, int channels = 2)
    {
        return new MockSampleProvider(new float[sampleCount * channels], sampleRate, channels);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesAvailable = _samples.Length - _position;
        int samplesToRead = Math.Min(count, samplesAvailable);

        if (samplesToRead > 0)
        {
            Array.Copy(_samples, _position, buffer, offset, samplesToRead);
            _position += samplesToRead;
        }

        return samplesToRead;
    }

    public void Reset()
    {
        _position = 0;
    }
}
