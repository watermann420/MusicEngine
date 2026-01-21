using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Moq;
using NAudio.Wave;
using Xunit;

namespace MusicEngine.Tests.Core;

public class EffectChainTests
{
    #region Constructor Tests

    [Fact]
    public void EffectChain_Constructor_SetsWaveFormat()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = new EffectChain(source);

        chain.WaveFormat.Should().Be(source.WaveFormat);
    }

    [Fact]
    public void EffectChain_Constructor_StartsEmpty()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = new EffectChain(source);

        chain.Count.Should().Be(0);
    }

    [Fact]
    public void EffectChain_Constructor_ThrowsOnNullSource()
    {
        var action = () => new EffectChain(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EffectChain_Constructor_NotBypassedByDefault()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = new EffectChain(source);

        chain.Bypassed.Should().BeFalse();
    }

    #endregion

    #region AddEffect Tests

    [Fact]
    public void EffectChain_AddEffect_IncreasesCount()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");

        chain.AddEffect(effect);

        chain.Count.Should().Be(1);
    }

    [Fact]
    public void EffectChain_AddEffect_ThrowsOnNullEffect()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var action = () => chain.AddEffect((IEffect)null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EffectChain_AddEffect_PreservesOrder()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect1 = CreateMockEffect("Effect1");
        var effect2 = CreateMockEffect("Effect2");
        var effect3 = CreateMockEffect("Effect3");

        chain.AddEffect(effect1);
        chain.AddEffect(effect2);
        chain.AddEffect(effect3);

        chain[0].Should().BeSameAs(effect1);
        chain[1].Should().BeSameAs(effect2);
        chain[2].Should().BeSameAs(effect3);
    }

    [Fact]
    public void EffectChain_AddEffect_Generic_CreatesEffect()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var effect = chain.AddEffect<ReverbEffect>();

        effect.Should().NotBeNull();
        effect.Should().BeOfType<ReverbEffect>();
        chain.Count.Should().Be(1);
    }

    #endregion

    #region RemoveEffect Tests

    [Fact]
    public void EffectChain_RemoveEffect_ByIndex_DecreasesCount()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var result = chain.RemoveEffect(0);

        result.Should().BeTrue();
        chain.Count.Should().Be(0);
    }

    [Fact]
    public void EffectChain_RemoveEffect_ByIndex_ReturnsFalseForInvalidIndex()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var result = chain.RemoveEffect(0);

        result.Should().BeFalse();
    }

    [Fact]
    public void EffectChain_RemoveEffect_ByName_RemovesCorrectEffect()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect1 = CreateMockEffect("Effect1");
        var effect2 = CreateMockEffect("Effect2");
        chain.AddEffect(effect1);
        chain.AddEffect(effect2);

        var result = chain.RemoveEffect("Effect1");

        result.Should().BeTrue();
        chain.Count.Should().Be(1);
        chain[0]!.Name.Should().Be("Effect2");
    }

    [Fact]
    public void EffectChain_RemoveEffect_ByName_IsCaseInsensitive()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var result = chain.RemoveEffect("testeffect");

        result.Should().BeTrue();
        chain.Count.Should().Be(0);
    }

    [Fact]
    public void EffectChain_RemoveEffect_ByName_ReturnsFalseIfNotFound()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var result = chain.RemoveEffect("NonExistent");

        result.Should().BeFalse();
        chain.Count.Should().Be(1);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void EffectChain_Clear_RemovesAllEffects()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        chain.AddEffect(CreateMockEffect("Effect1"));
        chain.AddEffect(CreateMockEffect("Effect2"));
        chain.AddEffect(CreateMockEffect("Effect3"));

        chain.Clear();

        chain.Count.Should().Be(0);
    }

    #endregion

    #region GetEffect Tests

    [Fact]
    public void EffectChain_GetEffect_ByName_ReturnsCorrectEffect()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var retrieved = chain.GetEffect("TestEffect");

        retrieved.Should().BeSameAs(effect);
    }

    [Fact]
    public void EffectChain_GetEffect_ByName_IsCaseInsensitive()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var retrieved = chain.GetEffect("testeffect");

        retrieved.Should().BeSameAs(effect);
    }

    [Fact]
    public void EffectChain_GetEffect_ByName_ReturnsNullIfNotFound()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var retrieved = chain.GetEffect("NonExistent");

        retrieved.Should().BeNull();
    }

    [Fact]
    public void EffectChain_GetEffect_Generic_ReturnsCorrectType()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var reverb = new ReverbEffect(source);
        var delay = new DelayEffect(source);
        chain.AddEffect(reverb);
        chain.AddEffect(delay);

        var retrieved = chain.GetEffect<ReverbEffect>();

        retrieved.Should().BeSameAs(reverb);
    }

    [Fact]
    public void EffectChain_GetEffect_Generic_ReturnsNullIfNotFound()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var delay = new DelayEffect(source);
        chain.AddEffect(delay);

        var retrieved = chain.GetEffect<ReverbEffect>();

        retrieved.Should().BeNull();
    }

    [Fact]
    public void EffectChain_Indexer_ReturnsCorrectEffect()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var retrieved = chain[0];

        retrieved.Should().BeSameAs(effect);
    }

    [Fact]
    public void EffectChain_Indexer_ReturnsNullForInvalidIndex()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var retrieved = chain[5];

        retrieved.Should().BeNull();
    }

    [Fact]
    public void EffectChain_Indexer_ReturnsNullForNegativeIndex()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        chain.AddEffect(CreateMockEffect("Test"));

        var retrieved = chain[-1];

        retrieved.Should().BeNull();
    }

    #endregion

    #region GetEffectNames Tests

    [Fact]
    public void EffectChain_GetEffectNames_ReturnsAllNames()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        chain.AddEffect(CreateMockEffect("Effect1"));
        chain.AddEffect(CreateMockEffect("Effect2"));
        chain.AddEffect(CreateMockEffect("Effect3"));

        var names = chain.GetEffectNames();

        names.Should().HaveCount(3);
        names.Should().ContainInOrder("Effect1", "Effect2", "Effect3");
    }

    [Fact]
    public void EffectChain_GetEffectNames_ReturnsEmptyForEmptyChain()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var names = chain.GetEffectNames();

        names.Should().BeEmpty();
    }

    #endregion

    #region Effect Order Tests

    [Fact]
    public void EffectChain_InsertEffect_InsertsAtCorrectPosition()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect1 = new ReverbEffect(source);
        var effect3 = new ChorusEffect(source);
        chain.AddEffect(effect1);
        chain.AddEffect(effect3);

        var effect2 = chain.InsertEffect<DelayEffect>(1);

        chain.Count.Should().Be(3);
        chain[1].Should().BeSameAs(effect2);
    }

    [Fact]
    public void EffectChain_InsertEffect_AtZero_InsertsAtBeginning()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        chain.AddEffect(new ReverbEffect(source));

        var delay = chain.InsertEffect<DelayEffect>(0);

        chain[0].Should().BeSameAs(delay);
    }

    #endregion

    #region Bypass Tests

    [Fact]
    public void EffectChain_Bypassed_CanBeSetToTrue()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        chain.Bypassed = true;

        chain.Bypassed.Should().BeTrue();
    }

    [Fact]
    public void EffectChain_Bypassed_CanBeSetToFalse()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        chain.Bypassed = true;

        chain.Bypassed = false;

        chain.Bypassed.Should().BeFalse();
    }

    [Fact]
    public void EffectChain_Read_WhenBypassed_ReadsFromSource()
    {
        var samples = new float[] { 0.5f, 0.5f, 0.3f, 0.3f };
        var source = new MockSampleProvider(samples);
        var chain = new EffectChain(source);
        var effect = new ReverbEffect(source);
        chain.AddEffect(effect);
        chain.Bypassed = true;

        var buffer = new float[4];
        var read = chain.Read(buffer, 0, 4);

        read.Should().Be(4);
        buffer[0].Should().Be(0.5f);
        buffer[1].Should().Be(0.5f);
    }

    [Fact]
    public void EffectChain_Read_WhenNotBypassed_ProcessesThroughEffects()
    {
        var source = MockSampleProvider.CreateSineWave(440f, 1000);
        var chain = new EffectChain(source);
        var reverb = new ReverbEffect(source);
        reverb.Mix = 1.0f;
        reverb.Enabled = true;
        chain.AddEffect(reverb);

        var buffer = new float[100];
        var read = chain.Read(buffer, 0, 100);

        read.Should().Be(100);
    }

    [Fact]
    public void EffectChain_Read_EmptyChain_ReadsFromSource()
    {
        var samples = new float[] { 0.5f, 0.5f, 0.3f, 0.3f };
        var source = new MockSampleProvider(samples);
        var chain = new EffectChain(source);

        var buffer = new float[4];
        var read = chain.Read(buffer, 0, 4);

        read.Should().Be(4);
        buffer[0].Should().Be(0.5f);
    }

    #endregion

    #region SetEffectEnabled Tests

    [Fact]
    public void EffectChain_SetEffectEnabled_ByName_SetsEnabled()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var result = chain.SetEffectEnabled("TestEffect", false);

        result.Should().BeTrue();
        chain[0]!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void EffectChain_SetEffectEnabled_ByName_ReturnsFalseIfNotFound()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var result = chain.SetEffectEnabled("NonExistent", false);

        result.Should().BeFalse();
    }

    [Fact]
    public void EffectChain_SetEffectEnabled_ByIndex_SetsEnabled()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);
        var effect = CreateMockEffect("TestEffect");
        chain.AddEffect(effect);

        var result = chain.SetEffectEnabled(0, false);

        result.Should().BeTrue();
        chain[0]!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void EffectChain_SetEffectEnabled_ByIndex_ReturnsFalseForInvalidIndex()
    {
        var source = MockSampleProvider.CreateSilence(1000);
        var chain = new EffectChain(source);

        var result = chain.SetEffectEnabled(5, false);

        result.Should().BeFalse();
    }

    #endregion

    #region CreateStandardChain Tests

    [Fact]
    public void EffectChain_CreateStandardChain_CreatesChainWithEffects()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = EffectChain.CreateStandardChain(source);

        chain.Count.Should().Be(3);
    }

    [Fact]
    public void EffectChain_CreateStandardChain_WithNoReverb_ExcludesReverb()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = EffectChain.CreateStandardChain(source, includeReverb: false);

        chain.Count.Should().Be(2);
        chain.GetEffect<ReverbEffect>().Should().BeNull();
    }

    [Fact]
    public void EffectChain_CreateStandardChain_WithNoDelay_ExcludesDelay()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = EffectChain.CreateStandardChain(source, includeDelay: false);

        chain.Count.Should().Be(2);
        chain.GetEffect<DelayEffect>().Should().BeNull();
    }

    [Fact]
    public void EffectChain_CreateStandardChain_WithNoChorus_ExcludesChorus()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = EffectChain.CreateStandardChain(source, includeChorus: false);

        chain.Count.Should().Be(2);
        chain.GetEffect<ChorusEffect>().Should().BeNull();
    }

    [Fact]
    public void EffectChain_CreateStandardChain_EffectsAreDisabledByDefault()
    {
        var source = MockSampleProvider.CreateSilence(1000);

        var chain = EffectChain.CreateStandardChain(source);

        for (int i = 0; i < chain.Count; i++)
        {
            chain[i]!.Enabled.Should().BeFalse();
        }
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void EffectChain_Operations_AreThreadSafe()
    {
        var source = MockSampleProvider.CreateSilence(10000);
        var chain = new EffectChain(source);
        var exceptions = new List<Exception>();

        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        chain.AddEffect(CreateMockEffect($"Effect_{taskId}_{j}"));
                        _ = chain.Count;
                        _ = chain.GetEffectNames();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static IEffect CreateMockEffect(string name)
    {
        var mock = new Mock<IEffect>();
        mock.Setup(e => e.Name).Returns(name);
        mock.Setup(e => e.Enabled).Returns(true);
        mock.SetupProperty(e => e.Enabled);
        mock.Setup(e => e.Mix).Returns(1.0f);
        mock.Setup(e => e.WaveFormat).Returns(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
        mock.Setup(e => e.Read(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((float[] buffer, int offset, int count) => count);
        return mock.Object;
    }

    #endregion
}
