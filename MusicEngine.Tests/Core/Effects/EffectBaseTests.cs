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

    public TestEffect(ISampleProvider source) : base(source, "TestEffect")
    {
        RegisterParameter("Gain", 1.0f);
    }

    protected override float ProcessSample(float sample, int channel)
    {
        ProcessedSampleCount++;
        return sample * Gain;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("Gain", StringComparison.OrdinalIgnoreCase))
        {
            Gain = value;
        }
    }
}

public class EffectBaseTests
{
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
    public void Mix_DefaultsToOne()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix.Should().Be(1.0f);
    }

    [Fact]
    public void Mix_ClampsToValidRange()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Mix = -0.5f;
        effect.Mix.Should().Be(0f);

        effect.Mix = 1.5f;
        effect.Mix.Should().Be(1f);
    }

    [Fact]
    public void Enabled_DefaultsToTrue()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var effect = new TestEffect(source);

        effect.Enabled.Should().BeTrue();
    }

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
    public void Read_ReturnsZeroWhenSourceEmpty()
    {
        var source = new MockSampleProvider(Array.Empty<float>());
        var effect = new TestEffect(source);

        var buffer = new float[4];
        var read = effect.Read(buffer, 0, 4);

        read.Should().Be(0);
    }
}
