using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Moq;
using Xunit;

namespace MusicEngine.Tests.Integration;

public class SequencerIntegrationTests
{
    #region Pattern Playback Tests

    [Fact]
    public void Sequencer_WithPattern_PlaysNotesAtCorrectBeats()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        pattern.AddNoteEvent(1.0, 64, 0.5, 100);
        pattern.AddNoteEvent(2.0, 67, 0.5, 100);
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(0.5);
        sequencer.Stop();

        synth.NoteOnCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sequencer_PatternLoop_RestartsCorrectly()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
        {
            LoopLength = 4.0,
            IsLooping = true
        };

        pattern.AddNoteEvent(0.0, 60, 0.25, 100);
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(8.0);

        sequencer.CurrentBeat.Should().BeGreaterThanOrEqualTo(8.0);
        sequencer.Stop();
    }

    [Fact]
    public void Sequencer_MultiplePatterns_PlaySimultaneously()
    {
        var sequencer = new Sequencer();
        var synth1 = new MockSynth();
        var synth2 = new MockSynth();

        var pattern1 = new Pattern(synth1);
        pattern1.AddNoteEvent(0.0, 60, 1.0, 100);

        var pattern2 = new Pattern(synth2);
        pattern2.AddNoteEvent(0.0, 72, 1.0, 100);

        sequencer.AddPattern(pattern1);
        sequencer.AddPattern(pattern2);

        sequencer.Start();
        sequencer.Skip(0.5);
        sequencer.Stop();

        synth1.NoteOnCount.Should().BeGreaterThan(0);
        synth2.NoteOnCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sequencer_DisabledPattern_DoesNotPlay()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth) { Enabled = false };

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(1.0);
        sequencer.Stop();

        synth.NoteOnCount.Should().Be(0);
    }

    [Fact]
    public void Sequencer_EnablePattern_StartsPlaying()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth) { Enabled = false };

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        pattern.AddNoteEvent(2.0, 64, 0.5, 100);
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(1.0);

        pattern.Enabled = true;
        sequencer.Skip(2.0);
        sequencer.Stop();

        synth.NoteOnCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sequencer_NoteOff_TriggeredAfterDuration()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(1.0);
        sequencer.Stop();

        synth.NoteOffCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sequencer_Stop_TriggersAllNotesOff()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.AddNoteEvent(0.0, 60, 2.0, 100);
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(0.5);
        synth.AllNotesOffCount = 0;

        sequencer.Stop();

        synth.AllNotesOffCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sequencer_CurrentBeat_TracksPosition()
    {
        var sequencer = new Sequencer();

        sequencer.Start();
        sequencer.Skip(4.0);

        sequencer.CurrentBeat.Should().BeGreaterThanOrEqualTo(4.0);
        sequencer.Stop();
    }

    [Fact]
    public void Sequencer_SetCurrentBeat_UpdatesPosition()
    {
        var sequencer = new Sequencer();

        sequencer.CurrentBeat = 8.0;

        sequencer.CurrentBeat.Should().Be(8.0);
    }

    #endregion

    #region Tempo Change Tests

    [Fact]
    public void Sequencer_BpmChange_AffectsPlayback()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        pattern.AddNoteEvent(1.0, 64, 0.5, 100);
        sequencer.AddPattern(pattern);

        sequencer.Bpm = 60.0;
        sequencer.Start();
        Thread.Sleep(100);
        sequencer.Bpm = 240.0;
        Thread.Sleep(100);
        sequencer.Stop();

        sequencer.Bpm.Should().Be(240.0);
    }

    [Fact]
    public void Sequencer_BpmChange_FiresEvent()
    {
        var sequencer = new Sequencer();
        double? oldBpm = null;
        double? newBpm = null;

        sequencer.BpmChanged += (s, e) =>
        {
            oldBpm = (double)e.OldValue;
            newBpm = (double)e.NewValue;
        };

        sequencer.Bpm = 140.0;

        oldBpm.Should().Be(120.0);
        newBpm.Should().Be(140.0);
    }

    [Fact]
    public void Sequencer_BpmClamping_EnforcesMinimum()
    {
        var sequencer = new Sequencer();

        sequencer.Bpm = 0;

        sequencer.Bpm.Should().Be(1.0);
    }

    [Fact]
    public void Sequencer_TempoChangeDuringPlayback_AppliesImmediately()
    {
        var sequencer = new Sequencer();
        sequencer.Bpm = 120.0;

        sequencer.Start();
        sequencer.Bpm = 180.0;

        sequencer.Bpm.Should().Be(180.0);
        sequencer.Stop();
    }

    [Fact]
    public void Sequencer_MultipleBpmChanges_TrackCorrectly()
    {
        var sequencer = new Sequencer();
        var bpmChanges = new List<double>();

        sequencer.BpmChanged += (s, e) => bpmChanges.Add((double)e.NewValue);

        sequencer.Bpm = 100.0;
        sequencer.Bpm = 120.0;
        sequencer.Bpm = 140.0;

        bpmChanges.Should().ContainInOrder(100.0, 120.0, 140.0);
    }

    #endregion

    #region Pattern Integration Tests

    [Fact]
    public void Pattern_AddedToSequencer_GetsSequencerReference()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        sequencer.AddPattern(pattern);

        pattern.Sequencer.Should().BeSameAs(sequencer);
    }

    [Fact]
    public void Pattern_RemovedFromSequencer_ClearsReference()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);
        sequencer.AddPattern(pattern);

        sequencer.RemovePattern(pattern);

        pattern.Sequencer.Should().BeNull();
    }

    [Fact]
    public void Pattern_GetPatternIndex_ReturnsCorrectIndex()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern1 = new Pattern(synth);
        var pattern2 = new Pattern(synth);
        var pattern3 = new Pattern(synth);

        sequencer.AddPattern(pattern1);
        sequencer.AddPattern(pattern2);
        sequencer.AddPattern(pattern3);

        pattern1.PatternIndex.Should().Be(0);
        pattern2.PatternIndex.Should().Be(1);
        pattern3.PatternIndex.Should().Be(2);
    }

    [Fact]
    public void Sequencer_ClearPatterns_RemovesAll()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();

        sequencer.AddPattern(new Pattern(synth));
        sequencer.AddPattern(new Pattern(synth));
        sequencer.AddPattern(new Pattern(synth));

        sequencer.ClearPatterns();

        sequencer.Patterns.Should().BeEmpty();
    }

    [Fact]
    public void Sequencer_ClearPatterns_FiresEvent()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        bool eventFired = false;

        sequencer.AddPattern(new Pattern(synth));
        sequencer.PatternsCleared += (s, e) => eventFired = true;

        sequencer.ClearPatterns();

        eventFired.Should().BeTrue();
    }

    #endregion

    #region Timing Precision Tests

    [Fact]
    public void Sequencer_DefaultTimingPrecision_IsStandard()
    {
        var sequencer = new Sequencer();

        sequencer.TimingPrecision.Should().Be(TimingPrecision.Standard);
    }

    [Fact]
    public void Sequencer_HighPrecision_CanBeSet()
    {
        var sequencer = new Sequencer(TimingPrecision.HighPrecision);

        sequencer.TimingPrecision.Should().Be(TimingPrecision.HighPrecision);
    }

    [Fact]
    public void Sequencer_TimingPrecision_CannotChangeWhileRunning()
    {
        var sequencer = new Sequencer();
        sequencer.Start();

        var action = () => sequencer.TimingPrecision = TimingPrecision.HighPrecision;

        action.Should().Throw<InvalidOperationException>();
        sequencer.Stop();
    }

    [Fact]
    public void Sequencer_TimingPrecision_CanChangeWhenStopped()
    {
        var sequencer = new Sequencer();

        sequencer.TimingPrecision = TimingPrecision.HighPrecision;

        sequencer.TimingPrecision.Should().Be(TimingPrecision.HighPrecision);
    }

    #endregion

    #region Start/Stop Integration Tests

    [Fact]
    public void Sequencer_Start_FiresPlaybackStartedEvent()
    {
        var sequencer = new Sequencer();
        bool eventFired = false;

        sequencer.PlaybackStarted += (s, e) => eventFired = true;
        sequencer.Start();

        eventFired.Should().BeTrue();
        sequencer.Stop();
    }

    [Fact]
    public void Sequencer_Stop_FiresPlaybackStoppedEvent()
    {
        var sequencer = new Sequencer();
        bool eventFired = false;

        sequencer.Start();
        sequencer.PlaybackStopped += (s, e) => eventFired = true;
        sequencer.Stop();

        eventFired.Should().BeTrue();
    }

    [Fact]
    public void Sequencer_StartStop_MultipleTimesWorks()
    {
        var sequencer = new Sequencer();

        for (int i = 0; i < 5; i++)
        {
            sequencer.Start();
            sequencer.IsRunning.Should().BeTrue();
            sequencer.Stop();
            sequencer.IsRunning.Should().BeFalse();
        }
    }

    [Fact]
    public void Sequencer_Dispose_StopsPlayback()
    {
        var sequencer = new Sequencer();
        sequencer.Start();

        sequencer.Dispose();

        sequencer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Sequencer_Dispose_CanBeCalledMultipleTimes()
    {
        var sequencer = new Sequencer();

        sequencer.Dispose();
        var action = () => sequencer.Dispose();

        action.Should().NotThrow();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void Sequencer_ComplexPattern_PlaysCorrectly()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
        {
            LoopLength = 4.0,
            IsLooping = true
        };

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        pattern.AddNoteEvent(0.5, 62, 0.5, 90);
        pattern.AddNoteEvent(1.0, 64, 0.5, 80);
        pattern.AddNoteEvent(1.5, 65, 0.5, 70);
        pattern.AddNoteEvent(2.0, 67, 0.5, 80);
        pattern.AddNoteEvent(2.5, 69, 0.5, 90);
        pattern.AddNoteEvent(3.0, 71, 0.5, 100);
        pattern.AddNoteEvent(3.5, 72, 0.5, 110);

        sequencer.AddPattern(pattern);
        sequencer.Start();
        sequencer.Skip(8.0);
        sequencer.Stop();

        synth.NoteOnCount.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void Sequencer_WithMultipleSynths_RoutesCorrectly()
    {
        var sequencer = new Sequencer();
        var bass = new MockSynth { Name = "Bass" };
        var lead = new MockSynth { Name = "Lead" };
        var drums = new MockSynth { Name = "Drums" };

        var bassPattern = new Pattern(bass);
        bassPattern.AddNoteEvent(0.0, 36, 1.0, 100);

        var leadPattern = new Pattern(lead);
        leadPattern.AddNoteEvent(0.0, 72, 0.5, 80);

        var drumPattern = new Pattern(drums);
        drumPattern.AddNoteEvent(0.0, 36, 0.25, 127);
        drumPattern.AddNoteEvent(1.0, 38, 0.25, 100);

        sequencer.AddPattern(bassPattern);
        sequencer.AddPattern(leadPattern);
        sequencer.AddPattern(drumPattern);

        sequencer.Start();
        sequencer.Skip(2.0);
        sequencer.Stop();

        bass.NoteOnCount.Should().BeGreaterThan(0);
        lead.NoteOnCount.Should().BeGreaterThan(0);
        drums.NoteOnCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sequencer_RealTimeTempoChange_WorksWithPatterns()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
        {
            LoopLength = 4.0,
            IsLooping = true
        };

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        pattern.AddNoteEvent(1.0, 64, 0.5, 100);
        pattern.AddNoteEvent(2.0, 67, 0.5, 100);
        pattern.AddNoteEvent(3.0, 72, 0.5, 100);

        sequencer.AddPattern(pattern);
        sequencer.Bpm = 120.0;
        sequencer.Start();
        sequencer.Skip(2.0);
        sequencer.Bpm = 240.0;
        sequencer.Skip(2.0);
        sequencer.Stop();

        synth.NoteOnCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Sequencer_DefaultLoopLength_AppliesNewPatterns()
    {
        var sequencer = new Sequencer();
        sequencer.DefaultLoopLength = 8.0;

        var synth = new MockSynth();
        var pattern = new Pattern(synth, sequencer);

        pattern.LoopLength.Should().Be(8.0);
    }

    [Fact]
    public void Sequencer_Skip_AdvancesCorrectAmount()
    {
        var sequencer = new Sequencer();

        sequencer.Skip(4.0);
        sequencer.Skip(4.0);

        sequencer.CurrentBeat.Should().Be(8.0);
    }

    #endregion

    #region Event Integration Tests

    [Fact]
    public void Pattern_NoteTriggered_FiresEvent()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);
        NoteEvent? triggeredNote = null;

        pattern.AddNoteEvent(0.0, 60, 0.5, 100);
        pattern.NoteTriggered += (s, note) => triggeredNote = note;
        sequencer.AddPattern(pattern);

        sequencer.Start();
        sequencer.Skip(0.5);
        sequencer.Stop();

        triggeredNote.Should().NotBeNull();
    }

    [Fact]
    public void Sequencer_PatternAdded_FiresEvent()
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
    public void Sequencer_PatternRemoved_FiresEvent()
    {
        var sequencer = new Sequencer();
        var synth = new MockSynth();
        var pattern = new Pattern(synth);
        Pattern? removedPattern = null;

        sequencer.AddPattern(pattern);
        sequencer.PatternRemoved += (s, p) => removedPattern = p;
        sequencer.RemovePattern(pattern);

        removedPattern.Should().BeSameAs(pattern);
    }

    #endregion
}
