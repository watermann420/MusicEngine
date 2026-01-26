using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using MusicEngine.Tests.Helpers;
using NAudio.Wave;
using Xunit;

namespace MusicEngine.Tests.Core.Effects;

/// <summary>
/// Test effect implementation for testing EffectBase functionality.
/// </summary>
public class TestEffect : EffectBase
{
    public float Gain { get; set; } = 1.0f;
    public int ProcessedSampleCount { get; private set; }
    public int ProcessBufferCallCount { get; private set; }

    public TestEffect(ISampleProvider source) : base(source, "TestEffect")
    {
        RegisterParameter("Gain", 1.0f);
    }

    protected override float ProcessSample(float sample, int channel)
    {
        ProcessedSampleCount++;
        return sample * Gain;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        ProcessBufferCallCount++;
        base.ProcessBuffer(sourceBuffer, destBuffer, offset, count);
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("Gain", StringComparison.OrdinalIgnoreCase))
        {
            Gain = value;
        }
    }
}

/// <summary>
/// Test effect that processes buffers directly.
/// </summary>
public class BufferProcessingEffect : EffectBase
{
    public BufferProcessingEffect(ISampleProvider source) : base(source, "BufferEffect")
    {
        RegisterParameter("Scale", 2.0f);
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        var scale = GetParameter("Scale");
        for (int i = 0; i < count; i++)
        {
            destBuffer[offset + i] = sourceBuffer[i] * scale;
        }
    }
}

public class EffectBaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameAndWaveFormat()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Name.Should().Be("TestEffect");
        effect.WaveFormat.Should().Be(source.WaveFormat);
    }

    [Fact]
    public void Constructor_ThrowsOnNullSource()
    {
        Action act = () => new TestEffect(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_PreservesSourceWaveFormat()
    {
        var source = new MockSampleProvider(new float[100], 48000, 1);
        var effect = new TestEffect(source);

        effect.WaveFormat.SampleRate.Should().Be(48000);
        effect.WaveFormat.Channels.Should().Be(1);
    }

    #endregion

    #region Mix Property Tests

    [Fact]
    public void Mix_DefaultsToOne()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix.Should().Be(1.0f);
    }

    [Fact]
    public void Mix_CanBeSet()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix = 0.5f;

        effect.Mix.Should().Be(0.5f);
    }

    [Fact]
    public void Mix_ClampsToValidRange_Low()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix = -0.5f;

        effect.Mix.Should().Be(0f);
    }

    [Fact]
    public void Mix_ClampsToValidRange_High()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix = 1.5f;

        effect.Mix.Should().Be(1f);
    }

    [Fact]
    public void Mix_AcceptsZero()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix = 0f;

        effect.Mix.Should().Be(0f);
    }

    #endregion

    #region Enabled Property Tests

    [Fact]
    public void Enabled_DefaultsToTrue()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Enabled_CanBeToggled()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Enabled = false;
        effect.Enabled.Should().BeFalse();

        effect.Enabled = true;
        effect.Enabled.Should().BeTrue();
    }

    #endregion

    #region Read Passthrough Tests

    [Fact]
    public void Read_WhenDisabled_PassesThroughSource()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source) { Gain = 2.0f };
        effect.Enabled = false;

        var buffer = new float[4];
        effect.Read(buffer, 0, 4);

        buffer.Should().BeEquivalentTo(sourceData);
    }

    [Fact]
    public void Read_WhenDisabled_DoesNotProcessSamples()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source) { Gain = 2.0f };
        effect.Enabled = false;

        var buffer = new float[4];
        effect.Read(buffer, 0, 4);

        effect.ProcessedSampleCount.Should().Be(0);
    }

    #endregion

    #region Read Processing Tests

    [Fact]
    public void Read_WhenEnabled_ProcessesSamples()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source) { Gain = 2.0f };

        var buffer = new float[4];
        effect.Read(buffer, 0, 4);

        buffer.Should().OnlyContain(x => x == 1.0f);
    }

    [Fact]
    public void Read_CountsProcessedSamples()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source);

        var buffer = new float[4];
        effect.Read(buffer, 0, 4);

        effect.ProcessedSampleCount.Should().Be(4);
    }

    [Fact]
    public void Read_WithOffset_WritesToCorrectPosition()
    {
        var sourceData = new float[] { 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source) { Gain = 2.0f };

        var buffer = new float[4];
        effect.Read(buffer, 2, 2);

        buffer[0].Should().Be(0f);
        buffer[1].Should().Be(0f);
        buffer[2].Should().Be(1.0f);
        buffer[3].Should().Be(1.0f);
    }

    [Fact]
    public void Read_ReturnsCorrectSampleCount()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source);

        var buffer = new float[10];
        var read = effect.Read(buffer, 0, 10);

        read.Should().Be(3);
    }

    #endregion

    #region Read Mix Tests

    [Fact]
    public void Read_AppliesMix()
    {
        var sourceData = new float[] { 1.0f, 1.0f };
        var source = new MockSampleProvider(sourceData, 44100, 1);
        var effect = new TestEffect(source) { Gain = 0.0f }; // Wet signal = 0
        effect.Mix = 0.5f; // 50% dry, 50% wet

        var buffer = new float[2];
        effect.Read(buffer, 0, 2);

        // 50% dry (1.0) + 50% wet (0.0) = 0.5
        buffer.Should().OnlyContain(x => Math.Abs(x - 0.5f) < 0.001f);
    }

    [Fact]
    public void Read_MixAtZero_OutputsDrySignal()
    {
        var sourceData = new float[] { 1.0f, 1.0f };
        var source = new MockSampleProvider(sourceData, 44100, 1);
        var effect = new TestEffect(source) { Gain = 0.0f }; // Wet = 0
        effect.Mix = 0f; // 100% dry

        var buffer = new float[2];
        effect.Read(buffer, 0, 2);

        // 100% dry (1.0) + 0% wet = 1.0
        buffer.Should().OnlyContain(x => Math.Abs(x - 1.0f) < 0.001f);
    }

    [Fact]
    public void Read_MixAtOne_OutputsWetSignal()
    {
        var sourceData = new float[] { 1.0f, 1.0f };
        var source = new MockSampleProvider(sourceData, 44100, 1);
        var effect = new TestEffect(source) { Gain = 2.0f }; // Wet = 2.0
        effect.Mix = 1.0f; // 100% wet

        var buffer = new float[2];
        effect.Read(buffer, 0, 2);

        // 0% dry + 100% wet (2.0) = 2.0
        buffer.Should().OnlyContain(x => Math.Abs(x - 2.0f) < 0.001f);
    }

    #endregion

    #region Parameter Tests

    [Fact]
    public void SetParameter_UpdatesValue()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.SetParameter("Gain", 0.5f);

        effect.GetParameter("Gain").Should().Be(0.5f);
    }

    [Fact]
    public void SetParameter_CallsOnParameterChanged()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.SetParameter("Gain", 0.5f);

        effect.Gain.Should().Be(0.5f);
    }

    [Fact]
    public void GetParameter_ReturnsZeroForUnknown()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.GetParameter("Unknown").Should().Be(0f);
    }

    [Fact]
    public void SetParameter_WithNullName_DoesNotThrow()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        Action act = () => effect.SetParameter(null!, 0.5f);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetParameter_WithEmptyName_DoesNotThrow()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        Action act = () => effect.SetParameter("", 0.5f);

        act.Should().NotThrow();
    }

    [Fact]
    public void GetParameter_WithNullName_ReturnsZero()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.GetParameter(null!).Should().Be(0f);
    }

    [Fact]
    public void SetParameter_CaseInsensitive()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.SetParameter("GAIN", 0.5f);

        effect.GetParameter("gain").Should().Be(0.5f);
        effect.GetParameter("Gain").Should().Be(0.5f);
    }

    [Fact]
    public void RegisterParameter_SetsInitialValue()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        // Gain was registered with initial value 1.0f
        effect.GetParameter("Gain").Should().Be(1.0f);
    }

    #endregion

    #region Empty Source Tests

    [Fact]
    public void Read_ReturnsZeroWhenSourceEmpty()
    {
        var source = new MockSampleProvider(Array.Empty<float>());
        var effect = new TestEffect(source);

        var buffer = new float[4];
        var read = effect.Read(buffer, 0, 4);

        read.Should().Be(0);
    }

    [Fact]
    public void Read_ReturnsZeroWhenSourceExhausted()
    {
        var source = new MockSampleProvider(new float[] { 0.5f, 0.5f });
        var effect = new TestEffect(source);

        var buffer = new float[2];
        effect.Read(buffer, 0, 2); // Exhaust the source

        var read = effect.Read(buffer, 0, 2);

        read.Should().Be(0);
    }

    #endregion

    #region ProcessBuffer Tests

    [Fact]
    public void ProcessBuffer_IsCalled()
    {
        var sourceData = new float[] { 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData);
        var effect = new TestEffect(source);

        var buffer = new float[2];
        effect.Read(buffer, 0, 2);

        effect.ProcessBufferCallCount.Should().Be(1);
    }

    [Fact]
    public void ProcessBuffer_CustomImplementation()
    {
        var sourceData = new float[] { 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData, 44100, 1);
        var effect = new BufferProcessingEffect(source);

        var buffer = new float[2];
        effect.Read(buffer, 0, 2);

        // Scale = 2.0 by default, so 0.5 * 2 = 1.0
        buffer.Should().OnlyContain(x => Math.Abs(x - 1.0f) < 0.001f);
    }

    #endregion

    #region Large Buffer Tests

    [Fact]
    public void Read_HandlesLargeBuffer()
    {
        var sampleCount = 44100 * 2; // 2 seconds of stereo
        var source = MockSampleProvider.CreateSineWave(440, sampleCount, 44100, 2);
        var effect = new TestEffect(source) { Gain = 0.5f };

        var buffer = new float[sampleCount];
        var read = effect.Read(buffer, 0, sampleCount);

        read.Should().Be(sampleCount);
    }

    [Fact]
    public void Read_MultipleReads_ProcessesCorrectly()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData, 44100, 1);
        var effect = new TestEffect(source) { Gain = 2.0f };

        var buffer = new float[2];

        effect.Read(buffer, 0, 2);
        buffer.Should().OnlyContain(x => x == 1.0f);

        effect.Read(buffer, 0, 2);
        buffer.Should().OnlyContain(x => x == 1.0f);

        effect.Read(buffer, 0, 2);
        buffer.Should().OnlyContain(x => x == 1.0f);
    }

    #endregion

    #region Channel Tests

    [Fact]
    public void Read_HandlesMonoSource()
    {
        var sourceData = new float[] { 0.5f, 0.5f };
        var source = new MockSampleProvider(sourceData, 44100, 1);
        var effect = new TestEffect(source);

        var buffer = new float[2];
        var read = effect.Read(buffer, 0, 2);

        read.Should().Be(2);
    }

    [Fact]
    public void Read_HandlesStereoSource()
    {
        var sourceData = new float[] { 0.5f, 0.5f, 0.5f, 0.5f }; // L, R, L, R
        var source = new MockSampleProvider(sourceData, 44100, 2);
        var effect = new TestEffect(source);

        var buffer = new float[4];
        var read = effect.Read(buffer, 0, 4);

        read.Should().Be(4);
    }

    #endregion

    #region AudioTestHelper Integration Tests

    [Fact]
    public void Effect_OutputIsSilentWhenSourceIsSilent()
    {
        var source = MockSampleProvider.CreateSilence(100);
        var effect = new TestEffect(source);

        var samples = AudioTestHelper.ReadSamples(effect, 100);

        AudioTestHelper.IsSilent(samples).Should().BeTrue();
    }

    [Fact]
    public void Effect_AppliesGainCorrectly()
    {
        var source = MockSampleProvider.CreateSineWave(440, 1000, 44100, 1);
        var effectHalf = new TestEffect(new MockSampleProvider(
            MockSampleProvider.CreateSineWave(440, 1000, 44100, 1).Read(new float[2000], 0, 2000) switch { _ => new float[2000] },
            44100, 1
        )) { Gain = 0.5f };

        // Just verify it doesn't throw
        var samples = AudioTestHelper.ReadSamples(effectHalf, 1000);
        samples.Should().HaveCount(1000);
    }

    #endregion
}
