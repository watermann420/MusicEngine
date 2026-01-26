//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Unit tests for the Sequencer class.

using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Xunit;

namespace MusicEngine.Tests.Core;

public class SequencerTests : IDisposable
{
    private readonly Sequencer _sequencer;
    private readonly MockSynth _mockSynth;

    public SequencerTests()
    {
        _sequencer = new Sequencer();
        _mockSynth = new MockSynth();
    }

    public void Dispose()
    {
        _sequencer.Dispose();
    }

    #region Start/Stop Tests

    [Fact]
    public void Start_ShouldSetIsRunningToTrue()
    {
        // Arrange
        _sequencer.IsRunning.Should().BeFalse();

        // Act
        _sequencer.Start();

        // Assert
        _sequencer.IsRunning.Should().BeTrue();

        // Cleanup
        _sequencer.Stop();
    }

    [Fact]
    public void Stop_ShouldSetIsRunningToFalse()
    {
        // Arrange
        _sequencer.Start();
        _sequencer.IsRunning.Should().BeTrue();

        // Act
        _sequencer.Stop();

        // Assert
        _sequencer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ShouldNotThrow()
    {
        // Arrange
        _sequencer.Start();

        // Act
        var act = () => _sequencer.Start();

        // Assert
        act.Should().NotThrow();
        _sequencer.IsRunning.Should().BeTrue();

        // Cleanup
        _sequencer.Stop();
    }

    [Fact]
    public void Stop_WhenNotRunning_ShouldNotThrow()
    {
        // Arrange
        _sequencer.IsRunning.Should().BeFalse();

        // Act
        var act = () => _sequencer.Stop();

        // Assert
        act.Should().NotThrow();
        _sequencer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_ShouldFirePlaybackStartedEvent()
    {
        // Arrange
        var eventFired = false;
        _sequencer.PlaybackStarted += (_, _) => eventFired = true;

        // Act
        _sequencer.Start();

        // Assert
        eventFired.Should().BeTrue();

        // Cleanup
        _sequencer.Stop();
    }

    [Fact]
    public void Stop_ShouldFirePlaybackStoppedEvent()
    {
        // Arrange
        _sequencer.Start();
        var eventFired = false;
        _sequencer.PlaybackStopped += (_, _) => eventFired = true;

        // Act
        _sequencer.Stop();

        // Assert
        eventFired.Should().BeTrue();
    }

    #endregion

    #region BPM Tests

    [Fact]
    public void Bpm_ShouldInitializeToDefaultValue()
    {
        // Assert
        _sequencer.Bpm.Should().Be(120.0);
    }

    [Fact]
    public void Bpm_ShouldSetNewValue()
    {
        // Act
        _sequencer.Bpm = 140.0;

        // Assert
        _sequencer.Bpm.Should().Be(140.0);
    }

    [Fact]
    public void Bpm_ShouldNotGoBelowMinimum()
    {
        // Act
        _sequencer.Bpm = 0.5;

        // Assert
        _sequencer.Bpm.Should().BeGreaterOrEqualTo(1.0);
    }

    [Fact]
    public void Bpm_ShouldFireBpmChangedEvent()
    {
        // Arrange
        var eventFired = false;
        double? oldValue = null;
        double? newValue = null;
        _sequencer.BpmChanged += (_, args) =>
        {
            eventFired = true;
            oldValue = (double)args.OldValue;
            newValue = (double)args.NewValue;
        };

        // Act
        _sequencer.Bpm = 150.0;

        // Assert
        eventFired.Should().BeTrue();
        oldValue.Should().Be(120.0);
        newValue.Should().Be(150.0);
    }

    [Fact]
    public void Bpm_WhenSetToSameValue_ShouldNotFireEvent()
    {
        // Arrange
        var eventCount = 0;
        _sequencer.BpmChanged += (_, _) => eventCount++;

        // Act
        _sequencer.Bpm = 120.0;

        // Assert
        eventCount.Should().Be(0);
    }

    #endregion

    #region Pattern Management Tests

    [Fact]
    public void AddPattern_ShouldAddPatternToList()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth) { Name = "Test Pattern" };

        // Act
        _sequencer.AddPattern(pattern);

        // Assert
        _sequencer.Patterns.Should().HaveCount(1);
        _sequencer.Patterns[0].Should().BeSameAs(pattern);
    }

    [Fact]
    public void AddPattern_ShouldSetPatternIndex()
    {
        // Arrange
        var pattern1 = new Pattern(_mockSynth);
        var pattern2 = new Pattern(_mockSynth);

        // Act
        _sequencer.AddPattern(pattern1);
        _sequencer.AddPattern(pattern2);

        // Assert
        pattern1.PatternIndex.Should().Be(0);
        pattern2.PatternIndex.Should().Be(1);
    }

    [Fact]
    public void AddPattern_ShouldLinkPatternToSequencer()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth);

        // Act
        _sequencer.AddPattern(pattern);

        // Assert
        pattern.Sequencer.Should().BeSameAs(_sequencer);
    }

    [Fact]
    public void AddPattern_ShouldFirePatternAddedEvent()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth);
        Pattern? addedPattern = null;
        _sequencer.PatternAdded += (_, p) => addedPattern = p;

        // Act
        _sequencer.AddPattern(pattern);

        // Assert
        addedPattern.Should().BeSameAs(pattern);
    }

    [Fact]
    public void RemovePattern_ShouldRemovePatternFromList()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth);
        _sequencer.AddPattern(pattern);

        // Act
        _sequencer.RemovePattern(pattern);

        // Assert
        _sequencer.Patterns.Should().BeEmpty();
    }

    [Fact]
    public void RemovePattern_ShouldReindexRemainingPatterns()
    {
        // Arrange
        var pattern1 = new Pattern(_mockSynth);
        var pattern2 = new Pattern(_mockSynth);
        var pattern3 = new Pattern(_mockSynth);
        _sequencer.AddPattern(pattern1);
        _sequencer.AddPattern(pattern2);
        _sequencer.AddPattern(pattern3);

        // Act
        _sequencer.RemovePattern(pattern2);

        // Assert
        pattern1.PatternIndex.Should().Be(0);
        pattern3.PatternIndex.Should().Be(1);
    }

    [Fact]
    public void RemovePattern_ShouldFirePatternRemovedEvent()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth);
        _sequencer.AddPattern(pattern);
        Pattern? removedPattern = null;
        _sequencer.PatternRemoved += (_, p) => removedPattern = p;

        // Act
        _sequencer.RemovePattern(pattern);

        // Assert
        removedPattern.Should().BeSameAs(pattern);
    }

    [Fact]
    public void ClearPatterns_ShouldRemoveAllPatterns()
    {
        // Arrange
        _sequencer.AddPattern(new Pattern(_mockSynth));
        _sequencer.AddPattern(new Pattern(_mockSynth));
        _sequencer.AddPattern(new Pattern(_mockSynth));

        // Act
        _sequencer.ClearPatterns();

        // Assert
        _sequencer.Patterns.Should().BeEmpty();
    }

    [Fact]
    public void ClearPatterns_ShouldFirePatternsClearedEvent()
    {
        // Arrange
        _sequencer.AddPattern(new Pattern(_mockSynth));
        var eventFired = false;
        _sequencer.PatternsCleared += (_, _) => eventFired = true;

        // Act
        _sequencer.ClearPatterns();

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void ClearPatterns_ShouldCallAllNotesOffOnSynths()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth);
        _sequencer.AddPattern(pattern);

        // Act
        _sequencer.ClearPatterns();

        // Assert
        _mockSynth.AllNotesOffCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region BeatChanged Event Tests

    [Fact]
    public void BeatChanged_ShouldContainCurrentBeatPosition()
    {
        // Arrange
        double? reportedBeat = null;
        _sequencer.BeatChanged += (_, args) => reportedBeat = args.CurrentBeat;

        // Act
        _sequencer.Start();
        Thread.Sleep(100); // Wait for at least one beat event

        // Assert
        reportedBeat.Should().NotBeNull();

        // Cleanup
        _sequencer.Stop();
    }

    [Fact]
    public void BeatChanged_ShouldContainBpmInfo()
    {
        // Arrange
        _sequencer.Bpm = 140.0;
        double? reportedBpm = null;
        _sequencer.BeatChanged += (_, args) => reportedBpm = args.Bpm;

        // Act
        _sequencer.Start();
        Thread.Sleep(100);

        // Assert
        reportedBpm.Should().Be(140.0);

        // Cleanup
        _sequencer.Stop();
    }

    [Fact]
    public void BeatChanged_ShouldContainLoopLength()
    {
        // Arrange
        var pattern = new Pattern(_mockSynth) { LoopLength = 8.0 };
        _sequencer.AddPattern(pattern);
        double? reportedLoopLength = null;
        _sequencer.BeatChanged += (_, args) => reportedLoopLength = args.LoopLength;

        // Act
        _sequencer.Start();
        Thread.Sleep(100);

        // Assert
        reportedLoopLength.Should().Be(8.0);

        // Cleanup
        _sequencer.Stop();
    }

    [Fact]
    public void DefaultLoopLength_ShouldBeUsedWhenNoPatternsExist()
    {
        // Arrange
        _sequencer.DefaultLoopLength = 16.0;
        double? reportedLoopLength = null;
        _sequencer.BeatChanged += (_, args) => reportedLoopLength = args.LoopLength;

        // Act
        _sequencer.Start();
        Thread.Sleep(100);

        // Assert
        reportedLoopLength.Should().Be(16.0);

        // Cleanup
        _sequencer.Stop();
    }

    #endregion

    #region Loop Functionality Tests

    [Fact]
    public void CurrentBeat_ShouldAdvanceWhilePlaying()
    {
        // Arrange
        _sequencer.Bpm = 300.0; // Fast BPM for quick test
        var initialBeat = _sequencer.CurrentBeat;

        // Act
        _sequencer.Start();
        Thread.Sleep(200);
        var finalBeat = _sequencer.CurrentBeat;
        _sequencer.Stop();

        // Assert
        finalBeat.Should().BeGreaterThan(initialBeat);
    }

    [Fact]
    public void CurrentBeat_CanBeSetManually()
    {
        // Act
        _sequencer.CurrentBeat = 8.0;

        // Assert
        _sequencer.CurrentBeat.Should().Be(8.0);
    }

    [Fact]
    public void Skip_ShouldAdvanceBeatPosition()
    {
        // Arrange
        _sequencer.CurrentBeat = 4.0;

        // Act
        _sequencer.Skip(2.0);

        // Assert
        _sequencer.CurrentBeat.Should().Be(6.0);
    }

    [Fact]
    public void Skip_WithNegativeValue_ShouldMoveBeatBackward()
    {
        // Arrange
        _sequencer.CurrentBeat = 8.0;

        // Act
        _sequencer.Skip(-3.0);

        // Assert
        _sequencer.CurrentBeat.Should().Be(5.0);
    }

    #endregion

    #region Timing Precision Tests

    [Fact]
    public void TimingPrecision_DefaultShouldBeStandard()
    {
        // Assert
        _sequencer.TimingPrecision.Should().Be(TimingPrecision.Standard);
    }

    [Fact]
    public void TimingPrecision_CanBeSetWhenNotRunning()
    {
        // Act
        _sequencer.TimingPrecision = TimingPrecision.HighPrecision;

        // Assert
        _sequencer.TimingPrecision.Should().Be(TimingPrecision.HighPrecision);
    }

    [Fact]
    public void TimingPrecision_ShouldThrowWhenChangedWhileRunning()
    {
        // Arrange
        _sequencer.Start();

        // Act
        var act = () => _sequencer.TimingPrecision = TimingPrecision.HighPrecision;

        // Assert
        act.Should().Throw<InvalidOperationException>();

        // Cleanup
        _sequencer.Stop();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithBpmAndTimeSignature_ShouldSetValues()
    {
        // Arrange & Act
        using var sequencer = new Sequencer(140.0, new TimeSignature(3, 4));

        // Assert
        sequencer.Bpm.Should().Be(140.0);
        sequencer.CurrentTimeSignature.Numerator.Should().Be(3);
        sequencer.CurrentTimeSignature.Denominator.Should().Be(4);
    }

    [Fact]
    public void Constructor_WithTimingPrecision_ShouldSetValue()
    {
        // Arrange & Act
        using var sequencer = new Sequencer(TimingPrecision.HighPrecision);

        // Assert
        sequencer.TimingPrecision.Should().Be(TimingPrecision.HighPrecision);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldStopSequencer()
    {
        // Arrange
        var sequencer = new Sequencer();
        sequencer.Start();

        // Act
        sequencer.Dispose();

        // Assert
        sequencer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var sequencer = new Sequencer();

        // Act
        var act = () =>
        {
            sequencer.Dispose();
            sequencer.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
