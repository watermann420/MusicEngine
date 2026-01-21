using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Xunit;

namespace MusicEngine.Tests.Core;

public class SequencerTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var sequencer = new Sequencer();

        sequencer.Bpm.Should().Be(120.0);
        sequencer.CurrentBeat.Should().Be(0);
        sequencer.IsRunning.Should().BeFalse();
        sequencer.Patterns.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithTimingPrecision_SetsCorrectly()
    {
        var sequencer = new Sequencer(TimingPrecision.HighPrecision);

        sequencer.TimingPrecision.Should().Be(TimingPrecision.HighPrecision);
    }

    [Fact]
    public void Bpm_CanBeSet()
    {
        var sequencer = new Sequencer();

        sequencer.Bpm = 140.0;

        sequencer.Bpm.Should().Be(140.0);
    }

    [Fact]
    public void Bpm_ClampsToMinimum()
    {
        var sequencer = new Sequencer();

        sequencer.Bpm = 0;

        sequencer.Bpm.Should().Be(1.0);
    }

    [Fact]
    public void Bpm_FiresBpmChangedEvent()
    {
        var sequencer = new Sequencer();
        double? oldValue = null;
        double? newValue = null;

        sequencer.BpmChanged += (s, e) =>
        {
            oldValue = (double)e.OldValue;
            newValue = (double)e.NewValue;
        };

        sequencer.Bpm = 140.0;

        oldValue.Should().Be(120.0);
        newValue.Should().Be(140.0);
    }

    [Fact]
    public void AddPattern_AddsPatternToList()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        sequencer.AddPattern(pattern);

        sequencer.Patterns.Should().Contain(pattern);
    }

    [Fact]
    public void AddPattern_SetsPatternIndex()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern1 = new Pattern(synth);
        var pattern2 = new Pattern(synth);

        sequencer.AddPattern(pattern1);
        sequencer.AddPattern(pattern2);

        pattern1.PatternIndex.Should().Be(0);
        pattern2.PatternIndex.Should().Be(1);
    }

    [Fact]
    public void AddPattern_SetsSequencerReference()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        sequencer.AddPattern(pattern);

        pattern.Sequencer.Should().BeSameAs(sequencer);
    }

    [Fact]
    public void AddPattern_FiresPatternAddedEvent()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);
        Pattern? addedPattern = null;

        sequencer.PatternAdded += (s, p) => addedPattern = p;
        sequencer.AddPattern(pattern);

        addedPattern.Should().BeSameAs(pattern);
    }

    [Fact]
    public void RemovePattern_RemovesFromList()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        sequencer.AddPattern(pattern);
        sequencer.RemovePattern(pattern);

        sequencer.Patterns.Should().NotContain(pattern);
    }

    [Fact]
    public void RemovePattern_CallsAllNotesOff()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        sequencer.AddPattern(pattern);
        sequencer.RemovePattern(pattern);

        synth.AllNotesOffCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ClearPatterns_RemovesAllPatterns()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();

        sequencer.AddPattern(new Pattern(synth));
        sequencer.AddPattern(new Pattern(synth));
        sequencer.ClearPatterns();

        sequencer.Patterns.Should().BeEmpty();
    }

    [Fact]
    public void ClearPatterns_FiresPatternsCleared()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        bool fired = false;

        sequencer.PatternsCleared += (s, e) => fired = true;
        sequencer.AddPattern(new Pattern(synth));
        sequencer.ClearPatterns();

        fired.Should().BeTrue();
    }

    [Fact]
    public void Start_SetsIsRunningTrue()
    {
        var sequencer = new Sequencer();

        sequencer.Start();

        sequencer.IsRunning.Should().BeTrue();

        sequencer.Stop();
    }

    [Fact]
    public void Start_FiresPlaybackStartedEvent()
    {
        var sequencer = new Sequencer();
        bool started = false;

        sequencer.PlaybackStarted += (s, e) => started = true;
        sequencer.Start();

        started.Should().BeTrue();

        sequencer.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunningFalse()
    {
        var sequencer = new Sequencer();

        sequencer.Start();
        sequencer.Stop();

        sequencer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Stop_FiresPlaybackStoppedEvent()
    {
        var sequencer = new Sequencer();
        bool stopped = false;

        sequencer.PlaybackStopped += (s, e) => stopped = true;
        sequencer.Start();
        sequencer.Stop();

        stopped.Should().BeTrue();
    }

    [Fact]
    public void Skip_AdvancesBeat()
    {
        var sequencer = new Sequencer();

        sequencer.Skip(4.0);

        sequencer.CurrentBeat.Should().Be(4.0);
    }

    [Fact]
    public void CurrentBeat_CanBeSet()
    {
        var sequencer = new Sequencer();

        sequencer.CurrentBeat = 8.0;

        sequencer.CurrentBeat.Should().Be(8.0);
    }

    [Fact]
    public void DefaultLoopLength_HasDefault()
    {
        var sequencer = new Sequencer();

        sequencer.DefaultLoopLength.Should().Be(4.0);
    }

    [Fact]
    public void DefaultLoopLength_ClampsMinimum()
    {
        var sequencer = new Sequencer();

        sequencer.DefaultLoopLength = 0.1;

        sequencer.DefaultLoopLength.Should().Be(0.25);
    }

    [Fact]
    public void TimingPrecision_CannotChangeWhileRunning()
    {
        var sequencer = new Sequencer();
        sequencer.Start();

        Action act = () => sequencer.TimingPrecision = TimingPrecision.HighPrecision;

        act.Should().Throw<InvalidOperationException>();

        sequencer.Stop();
    }

    [Fact]
    public void Dispose_StopsSequencer()
    {
        var sequencer = new Sequencer();
        sequencer.Start();

        sequencer.Dispose();

        sequencer.IsRunning.Should().BeFalse();
    }
}
