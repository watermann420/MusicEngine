using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Moq;
using NAudio.Wave;
using Xunit;

namespace MusicEngine.Tests.Core;

public class AudioRecorderTests
{
    #region Constructor Tests

    [Fact]
    public void AudioRecorder_Constructor_SetsDefaults()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        using var recorder = new AudioRecorder(source);

        recorder.IsRecording.Should().BeFalse();
        recorder.IsPaused.Should().BeFalse();
        recorder.SampleRate.Should().Be(44100);
        recorder.Channels.Should().Be(2);
        recorder.Format.Should().Be(RecordingFormat.Wav16Bit);
        recorder.OutputPath.Should().BeNull();
    }

    [Fact]
    public void AudioRecorder_Constructor_WithCustomSampleRate()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        using var recorder = new AudioRecorder(source, sampleRate: 48000);

        recorder.SampleRate.Should().Be(48000);
    }

    [Fact]
    public void AudioRecorder_Constructor_WithCustomChannels()
    {
        var source = MockSampleProvider.CreateSilence(1000, channels: 1);

        using var recorder = new AudioRecorder(source, channels: 1);

        recorder.Channels.Should().Be(1);
    }

    [Fact]
    public void AudioRecorder_Constructor_ThrowsOnNullSource()
    {
        var action = () => new AudioRecorder(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AudioRecorder_Constructor_ThrowsOnInvalidSampleRate()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var action = () => new AudioRecorder(source, sampleRate: 0);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AudioRecorder_Constructor_ThrowsOnInvalidChannels()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var action = () => new AudioRecorder(source, channels: 0);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Format Tests

    [Fact]
    public void AudioRecorder_Format_CanBeSet()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        recorder.Format = RecordingFormat.Wav24Bit;

        recorder.Format.Should().Be(RecordingFormat.Wav24Bit);
    }

    [Fact]
    public void AudioRecorder_Format_CannotChangeWhileRecording()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            var action = () => recorder.Format = RecordingFormat.Wav24Bit;

            action.Should().Throw<InvalidOperationException>();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Theory]
    [InlineData(RecordingFormat.Wav16Bit, 16)]
    [InlineData(RecordingFormat.Wav24Bit, 24)]
    [InlineData(RecordingFormat.Wav32BitFloat, 32)]
    public void RecordingFormat_GetBitDepth_ReturnsCorrectDepth(RecordingFormat format, int expectedDepth)
    {
        format.GetBitDepth().Should().Be(expectedDepth);
    }

    [Theory]
    [InlineData(RecordingFormat.Mp3_128kbps, 128)]
    [InlineData(RecordingFormat.Mp3_192kbps, 192)]
    [InlineData(RecordingFormat.Mp3_320kbps, 320)]
    public void RecordingFormat_GetMp3Bitrate_ReturnsCorrectBitrate(RecordingFormat format, int expectedBitrate)
    {
        format.GetMp3Bitrate().Should().Be(expectedBitrate);
    }

    [Theory]
    [InlineData(RecordingFormat.Wav16Bit, true)]
    [InlineData(RecordingFormat.Wav24Bit, true)]
    [InlineData(RecordingFormat.Wav32BitFloat, true)]
    [InlineData(RecordingFormat.Mp3_128kbps, false)]
    [InlineData(RecordingFormat.Mp3_192kbps, false)]
    [InlineData(RecordingFormat.Mp3_320kbps, false)]
    public void RecordingFormat_IsWavFormat_IdentifiesCorrectly(RecordingFormat format, bool expectedIsWav)
    {
        format.IsWavFormat().Should().Be(expectedIsWav);
    }

    [Theory]
    [InlineData(RecordingFormat.Wav16Bit, ".wav")]
    [InlineData(RecordingFormat.Wav24Bit, ".wav")]
    [InlineData(RecordingFormat.Mp3_128kbps, ".mp3")]
    public void RecordingFormat_GetFileExtension_ReturnsCorrectExtension(RecordingFormat format, string expectedExtension)
    {
        format.GetFileExtension().Should().Be(expectedExtension);
    }

    [Fact]
    public void RecordingFormat_GetDescription_ReturnsDescriptiveString()
    {
        RecordingFormat.Wav16Bit.GetDescription().Should().Contain("16");
        RecordingFormat.Wav24Bit.GetDescription().Should().Contain("24");
        RecordingFormat.Mp3_320kbps.GetDescription().Should().Contain("320");
    }

    [Fact]
    public void RecordingFormat_IsFloatFormat_IdentifiesCorrectly()
    {
        RecordingFormat.Wav32BitFloat.IsFloatFormat().Should().BeTrue();
        RecordingFormat.Wav16Bit.IsFloatFormat().Should().BeFalse();
        RecordingFormat.Mp3_320kbps.IsFloatFormat().Should().BeFalse();
    }

    #endregion

    #region StartRecording Tests

    [Fact]
    public void AudioRecorder_StartRecording_SetsIsRecordingTrue()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);

            recorder.StartRecording(tempFile);

            recorder.IsRecording.Should().BeTrue();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_StartRecording_SetsOutputPath()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.Combine(Path.GetTempPath(), "test_recording.wav");

        try
        {
            using var recorder = new AudioRecorder(source);

            recorder.StartRecording(tempFile);

            recorder.OutputPath.Should().EndWith(".wav");
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    [Fact]
    public void AudioRecorder_StartRecording_FiresRecordingStartedEvent()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();
        bool eventFired = false;

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.RecordingStarted += (s, e) => eventFired = true;

            recorder.StartRecording(tempFile);

            eventFired.Should().BeTrue();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_StartRecording_ThrowsOnEmptyPath()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        var action = () => recorder.StartRecording(string.Empty);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AudioRecorder_StartRecording_ThrowsOnWhitespacePath()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        var action = () => recorder.StartRecording("   ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AudioRecorder_StartRecording_ThrowsIfAlreadyRecording()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            var action = () => recorder.StartRecording("another.wav");

            action.Should().Throw<InvalidOperationException>();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_StartRecording_EnsuresCorrectExtension()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.Combine(Path.GetTempPath(), "test_file.mp3");

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.Format = RecordingFormat.Wav16Bit;

            recorder.StartRecording(tempFile);

            recorder.OutputPath.Should().EndWith(".wav");
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    #endregion

    #region StopRecording Tests

    [Fact]
    public void AudioRecorder_StopRecording_SetsIsRecordingFalse()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            recorder.StopRecording();

            recorder.IsRecording.Should().BeFalse();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_StopRecording_ClearsOutputPath()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            recorder.StopRecording();

            recorder.OutputPath.Should().BeNull();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_StopRecording_FiresRecordingCompletedEvent()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();
        RecordingCompletedEventArgs? eventArgs = null;

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.RecordingCompleted += (s, e) => eventArgs = e;
            recorder.StartRecording(tempFile);

            Thread.Sleep(100);
            recorder.StopRecording();

            eventArgs.Should().NotBeNull();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_StopRecording_ThrowsIfNotRecording()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        var action = () => recorder.StopRecording();

        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Pause/Resume Tests

    [Fact]
    public void AudioRecorder_PauseRecording_SetsIsPausedTrue()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            recorder.PauseRecording();

            recorder.IsPaused.Should().BeTrue();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_PauseRecording_ThrowsIfNotRecording()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        var action = () => recorder.PauseRecording();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AudioRecorder_PauseRecording_ThrowsIfAlreadyPaused()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);
            recorder.PauseRecording();

            var action = () => recorder.PauseRecording();

            action.Should().Throw<InvalidOperationException>();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_ResumeRecording_SetsIsPausedFalse()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);
            recorder.PauseRecording();

            recorder.ResumeRecording();

            recorder.IsPaused.Should().BeFalse();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_ResumeRecording_ThrowsIfNotRecording()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        var action = () => recorder.ResumeRecording();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AudioRecorder_ResumeRecording_ThrowsIfNotPaused()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            var action = () => recorder.ResumeRecording();

            action.Should().Throw<InvalidOperationException>();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_PauseResume_MaintainsRecordingState()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);
            recorder.PauseRecording();
            recorder.ResumeRecording();

            recorder.IsRecording.Should().BeTrue();
            recorder.IsPaused.Should().BeFalse();
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    #endregion

    #region RecordedDuration Tests

    [Fact]
    public void AudioRecorder_RecordedDuration_StartsAtZero()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        using var recorder = new AudioRecorder(source);

        recorder.RecordedDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AudioRecorder_RecordedDuration_IncreasesWhileRecording()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            Thread.Sleep(200);

            recorder.RecordedDuration.Should().BeGreaterThan(TimeSpan.Zero);
            recorder.StopRecording();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    #endregion

    #region Progress Event Tests

    [Fact]
    public void AudioRecorder_Progress_FiresWhileRecording()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();
        var progressEventCount = 0;

        try
        {
            using var recorder = new AudioRecorder(source);
            recorder.Progress += (s, e) => Interlocked.Increment(ref progressEventCount);
            recorder.StartRecording(tempFile);

            Thread.Sleep(300);
            recorder.StopRecording();

            progressEventCount.Should().BeGreaterThan(0);
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void AudioRecorder_Dispose_StopsRecordingIfInProgress()
    {
        var source = CreateContinuousSampleProvider();
        var tempFile = Path.GetTempFileName();

        try
        {
            var recorder = new AudioRecorder(source);
            recorder.StartRecording(tempFile);

            recorder.Dispose();

            recorder.IsRecording.Should().BeFalse();
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteFile(Path.ChangeExtension(tempFile, ".wav"));
        }
    }

    [Fact]
    public void AudioRecorder_Dispose_CanBeCalledMultipleTimes()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var recorder = new AudioRecorder(source);

        recorder.Dispose();
        var action = () => recorder.Dispose();

        action.Should().NotThrow();
    }

    [Fact]
    public void AudioRecorder_StartRecording_ThrowsAfterDispose()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var recorder = new AudioRecorder(source);
        recorder.Dispose();

        var action = () => recorder.StartRecording("test.wav");

        action.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Helper Methods

    private static ISampleProvider CreateContinuousSampleProvider()
    {
        return new ContinuousSampleProvider(44100, 2);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// A sample provider that continuously provides audio samples (for testing recording).
    /// </summary>
    private class ContinuousSampleProvider : ISampleProvider
    {
        private readonly WaveFormat _waveFormat;
        private readonly float _frequency;
        private double _phase;

        public WaveFormat WaveFormat => _waveFormat;

        public ContinuousSampleProvider(int sampleRate = 44100, int channels = 2, float frequency = 440f)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            _frequency = frequency;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            double phaseIncrement = 2 * Math.PI * _frequency / _waveFormat.SampleRate;

            for (int i = 0; i < count; i += _waveFormat.Channels)
            {
                float sample = (float)Math.Sin(_phase);
                for (int ch = 0; ch < _waveFormat.Channels; ch++)
                {
                    buffer[offset + i + ch] = sample * 0.5f;
                }
                _phase += phaseIncrement;
                if (_phase > 2 * Math.PI)
                {
                    _phase -= 2 * Math.PI;
                }
            }

            return count;
        }
    }

    #endregion
}
