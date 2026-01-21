using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Xunit;

namespace MusicEngine.Tests.Core;

public class PatternTests
{
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
}
