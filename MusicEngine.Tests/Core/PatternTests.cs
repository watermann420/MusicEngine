using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Xunit;

namespace MusicEngine.Tests.Core;

public class PatternTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesWithSynth()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Synth.Should().BeSameAs(synth);
        pattern.Events.Should().BeEmpty();
        pattern.LoopLength.Should().Be(4.0);
        pattern.IsLooping.Should().BeTrue();
        pattern.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_InitializesWithDefaultName()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Name.Should().BeEmpty();
        pattern.InstrumentName.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesWithNullStartBeat()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.StartBeat.Should().BeNull();
    }

    #endregion

    #region Note Method Tests

    [Fact]
    public void Note_AddsNoteEvent()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Note(60, 0, 1.0, 100);

        pattern.Events.Should().HaveCount(1);
        pattern.Events[0].Note.Should().Be(60);
        pattern.Events[0].Beat.Should().Be(0);
        pattern.Events[0].Duration.Should().Be(1.0);
        pattern.Events[0].Velocity.Should().Be(100);
    }

    [Fact]
    public void Note_ReturnsSelfForChaining()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        var result = pattern.Note(60, 0, 1.0, 100);

        result.Should().BeSameAs(pattern);
    }

    [Fact]
    public void Note_SupportsChaining()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0, 1.0, 100)
            .Note(64, 1, 1.0, 100)
            .Note(67, 2, 1.0, 100);

        pattern.Events.Should().HaveCount(3);
    }

    [Fact]
    public void Note_AddsMultipleEventsAtSameBeat()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0, 1.0, 100)
            .Note(64, 0, 1.0, 100)
            .Note(67, 0, 1.0, 100);

        pattern.Events.Should().HaveCount(3);
        pattern.Events.Should().OnlyContain(e => e.Beat == 0);
    }

    [Fact]
    public void Note_WithDifferentVelocities()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0, 1.0, 127)
            .Note(64, 1, 1.0, 64)
            .Note(67, 2, 1.0, 32);

        pattern.Events[0].Velocity.Should().Be(127);
        pattern.Events[1].Velocity.Should().Be(64);
        pattern.Events[2].Velocity.Should().Be(32);
    }

    [Fact]
    public void Note_WithDifferentDurations()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0, 0.25, 100)
            .Note(64, 0.25, 0.5, 100)
            .Note(67, 0.75, 1.0, 100);

        pattern.Events[0].Duration.Should().Be(0.25);
        pattern.Events[1].Duration.Should().Be(0.5);
        pattern.Events[2].Duration.Should().Be(1.0);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Loop_IsAliasForIsLooping()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Loop = false;
        pattern.IsLooping.Should().BeFalse();

        pattern.IsLooping = true;
        pattern.Loop.Should().BeTrue();
    }

    [Fact]
    public void Id_IsUnique()
    {
        var synth = new MockSynth();
        var pattern1 = new Pattern(synth);
        var pattern2 = new Pattern(synth);

        pattern1.Id.Should().NotBe(pattern2.Id);
    }

    [Fact]
    public void Id_IsConsistent()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);
        var id = pattern.Id;

        pattern.Id.Should().Be(id);
    }

    [Fact]
    public void Name_CanBeSet()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Name = "Bass Line";

        pattern.Name.Should().Be("Bass Line");
    }

    [Fact]
    public void InstrumentName_CanBeSet()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.InstrumentName = "Synth Bass";

        pattern.InstrumentName.Should().Be("Synth Bass");
    }

    [Fact]
    public void LoopLength_CanBeSet()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.LoopLength = 8.0;

        pattern.LoopLength.Should().Be(8.0);
    }

    [Fact]
    public void Enabled_CanBeToggled()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Enabled = false;
        pattern.Enabled.Should().BeFalse();

        pattern.Enabled = true;
        pattern.Enabled.Should().BeTrue();
    }

    #endregion

    #region Process Disabled Tests

    [Fact]
    public void Process_WhenDisabled_CallsAllNotesOff()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0, 1.0, 100);
        pattern.Enabled = false;

        pattern.Process(0, 1, 120);

        synth.AllNotesOffCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Process_WhenDisabled_DoesNotTriggerNotes()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0.5, 1.0, 100);
        pattern.Enabled = false;

        pattern.Process(0, 1, 120);

        synth.NoteOnCount.Should().Be(0);
    }

    #endregion

    #region Process Trigger Tests

    [Fact]
    public void Process_TriggersNoteWithinRange()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0.5, 1.0, 100);

        pattern.Process(0, 1, 120);

        synth.NoteOnCount.Should().Be(1);
    }

    [Fact]
    public void Process_DoesNotTriggerNoteOutsideRange()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 2.5, 1.0, 100);

        pattern.Process(0, 1, 120);

        synth.NoteOnCount.Should().Be(0);
    }

    [Fact]
    public void Process_TriggersMultipleNotesInRange()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0.25, 0.5, 100)
            .Note(64, 0.5, 0.5, 100)
            .Note(67, 0.75, 0.5, 100);

        pattern.Process(0, 1, 120);

        synth.NoteOnCount.Should().Be(3);
    }

    [Fact]
    public void Process_TriggersNoteAtExactBeat()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0.5, 1.0, 100);

        pattern.Process(0.5, 1.0, 120);

        synth.NoteOnCount.Should().Be(1);
    }

    #endregion

    #region Process Looping Tests

    [Fact]
    public void Process_HandlesLoopWrapAround()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth) { LoopLength = 4.0 }
            .Note(60, 0.5, 1.0, 100);

        // Process from beat 3.5 to 4.5 (wraps around)
        pattern.Process(3.5, 4.5, 120);

        synth.NoteOnCount.Should().Be(1);
    }

    [Fact]
    public void Process_NonLooping_DoesNotRepeat()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
        {
            LoopLength = 4.0,
            IsLooping = false
        }.Note(60, 0.5, 1.0, 100);

        // First pass - should trigger
        pattern.Process(0, 1, 120);
        synth.NoteOnCount.Should().Be(1);

        // Reset and process after loop length - should not trigger again
        synth.Reset();
        pattern.Process(4, 5, 120);
        synth.NoteOnCount.Should().Be(0);
    }

    [Fact]
    public void Process_Looping_TriggersOnEachLoop()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
        {
            LoopLength = 4.0,
            IsLooping = true
        }.Note(60, 0.5, 1.0, 100);

        // First loop
        pattern.Process(0, 1, 120);
        synth.NoteOnCount.Should().Be(1);

        // Reset for second loop
        synth.Reset();
        // Second loop - trigger from beat 4.0 to 5.0
        pattern.Process(4, 5, 120);
        synth.NoteOnCount.Should().Be(1);
    }

    [Fact]
    public void Process_WithDifferentLoopLengths()
    {
        var synth = new MockSynth();

        var pattern1Beat = new Pattern(synth) { LoopLength = 1.0 }.Note(60, 0.5, 0.25, 100);
        var pattern4Beat = new Pattern(synth) { LoopLength = 4.0 }.Note(60, 0.5, 0.25, 100);

        // 1-beat pattern should trigger more often
        pattern1Beat.Process(0, 4, 120);
        var count1Beat = synth.NoteOnCount;

        synth.Reset();
        pattern4Beat.Process(0, 4, 120);
        var count4Beat = synth.NoteOnCount;

        count1Beat.Should().BeGreaterThan(count4Beat);
    }

    #endregion

    #region Process StartBeat Tests

    [Fact]
    public void Process_InitializesStartBeat()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.StartBeat.Should().BeNull();

        pattern.Process(2.0, 3.0, 120);

        pattern.StartBeat.Should().Be(2.0);
    }

    [Fact]
    public void Process_UsesExistingStartBeat()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.StartBeat = 1.0;
        pattern.Process(2.0, 3.0, 120);

        pattern.StartBeat.Should().Be(1.0);
    }

    #endregion

    #region Process BPM Tests

    [Fact]
    public void Process_RespectsBpm()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0.5, 1.0, 100);

        // Different BPM shouldn't affect note triggering (just timing)
        pattern.Process(0, 1, 60);
        synth.NoteOnCount.Should().Be(1);

        synth.Reset();
        pattern.StartBeat = null;

        pattern.Process(0, 1, 240);
        synth.NoteOnCount.Should().Be(1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Process_EmptyPattern_DoesNothing()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Process(0, 4, 120);

        synth.NoteOnCount.Should().Be(0);
        synth.NoteOffCount.Should().Be(0);
    }

    [Fact]
    public void Note_AtBeatZero_TriggersCorrectly()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth)
            .Note(60, 0, 1.0, 100);

        pattern.Process(0, 0.5, 120);

        synth.NoteOnCount.Should().Be(1);
    }

    [Fact]
    public void Note_AtEndOfLoop_TriggersCorrectly()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth) { LoopLength = 4.0 }
            .Note(60, 3.9, 0.1, 100);

        pattern.Process(3.5, 4.0, 120);

        synth.NoteOnCount.Should().Be(1);
    }

    #endregion

    #region Stop Method Tests

    [Fact]
    public void Stop_CallsAllNotesOffOnSynth()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth);

        pattern.Stop();

        synth.AllNotesOffCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Complex Pattern Tests

    [Fact]
    public void ComplexPattern_MultipleNotesAtVariousBeats()
    {
        var synth = new MockSynth();
        var pattern = new Pattern(synth) { LoopLength = 4.0 }
            // Beat 0 - chord
            .Note(60, 0, 0.5, 100)
            .Note(64, 0, 0.5, 100)
            .Note(67, 0, 0.5, 100)
            // Beat 1 - single note
            .Note(72, 1, 0.5, 100)
            // Beat 2 - chord
            .Note(60, 2, 0.5, 100)
            .Note(65, 2, 0.5, 100)
            .Note(69, 2, 0.5, 100)
            // Beat 3 - single note
            .Note(72, 3, 0.5, 100);

        pattern.Process(0, 4, 120);

        synth.NoteOnCount.Should().Be(8);
    }

    #endregion
}
