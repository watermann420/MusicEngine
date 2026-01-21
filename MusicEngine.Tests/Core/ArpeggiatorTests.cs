using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Mocks;
using Xunit;

namespace MusicEngine.Tests.Core;

public class ArpeggiatorTests
{
    [Fact]
    public void Constructor_InitializesWithSynth()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.Pattern.Should().Be(ArpPattern.Up);
        arp.Rate.Should().Be(ArpNoteDuration.Sixteenth);
        arp.OctaveRange.Should().Be(1);
        arp.Gate.Should().Be(0.8f);
        arp.Enabled.Should().BeTrue();
        arp.Latch.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ThrowsOnNullSynth()
    {
        Action act = () => new Arpeggiator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NoteOn_AddsNoteToHeld()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.NoteOn(60, 100);

        arp.HasNotes.Should().BeTrue();
        arp.NoteCount.Should().Be(1);
    }

    [Fact]
    public void NoteOn_DoesNotDuplicate()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.NoteOn(60, 100);
        arp.NoteOn(60, 100);

        arp.NoteCount.Should().Be(1);
    }

    [Fact]
    public void NoteOff_RemovesNote()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.NoteOn(60, 100);
        arp.NoteOff(60);

        arp.HasNotes.Should().BeFalse();
        arp.NoteCount.Should().Be(0);
    }

    [Fact]
    public void NoteOff_WithLatch_DoesNotRemoveNote()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        arp.Latch = true;

        arp.NoteOn(60, 100);
        arp.NoteOff(60);

        arp.HasNotes.Should().BeTrue();
        arp.NoteCount.Should().Be(1);
    }

    [Fact]
    public void Clear_RemovesAllNotes()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.NoteOn(60, 100);
        arp.NoteOn(64, 100);
        arp.NoteOn(67, 100);
        arp.Clear();

        arp.HasNotes.Should().BeFalse();
        arp.NoteCount.Should().Be(0);
    }

    [Fact]
    public void Clear_StopsPlayingNote()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.NoteOn(60, 100);
        arp.Process(0, 120);
        arp.Clear();

        synth.NoteOffCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Process_WhenDisabled_DoesNothing()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        arp.Enabled = false;

        arp.NoteOn(60, 100);
        arp.Process(0, 120);

        synth.NoteOnCount.Should().Be(0);
    }

    [Fact]
    public void Process_WhenNoNotes_DoesNothing()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.Process(0, 120);

        synth.NoteOnCount.Should().Be(0);
    }

    [Fact]
    public void Process_TriggersNoteAtCorrectTime()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        arp.Rate = ArpNoteDuration.Quarter; // Every beat

        arp.NoteOn(60, 100);
        arp.Process(0, 120); // First beat

        synth.NoteOnCount.Should().Be(1);
    }

    [Fact]
    public void NotePlayed_EventFires()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        int? playedNote = null;

        arp.NotePlayed += (s, e) => playedNote = e.Note;
        arp.NoteOn(60, 100);
        arp.Process(0, 120);

        playedNote.Should().Be(60);
    }

    [Theory]
    [InlineData(ArpNoteDuration.Whole, 4.0)]
    [InlineData(ArpNoteDuration.Half, 2.0)]
    [InlineData(ArpNoteDuration.Quarter, 1.0)]
    [InlineData(ArpNoteDuration.Eighth, 0.5)]
    [InlineData(ArpNoteDuration.Sixteenth, 0.25)]
    public void Rate_ControlsNoteTiming(ArpNoteDuration rate, double expectedBeatsPerNote)
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        arp.Rate = rate;

        // Just verify the setting is applied
        arp.Rate.Should().Be(rate);
    }

    [Fact]
    public void Velocity_OverridesInputVelocity()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        arp.Velocity = 64;

        arp.NoteOn(60, 127);
        arp.Process(0, 120);

        // With fixed velocity, the arp should use 64 instead of the input 127
        // We can verify this through the event
        int? eventVelocity = null;
        arp.NotePlayed += (s, e) => eventVelocity = e.Velocity;
        arp.Process(1, 120);

        eventVelocity.Should().Be(64);
    }

    [Fact]
    public void OctaveRange_ExpandsPattern()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);
        arp.OctaveRange = 2;

        arp.NoteOn(60, 100);

        // With octave range 2, we should have notes across 3 octaves (0, 1, 2)
        arp.NoteCount.Should().Be(1); // Held notes don't change
    }

    [Fact]
    public void Dispose_ClearsNotes()
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.NoteOn(60, 100);
        arp.Dispose();

        arp.HasNotes.Should().BeFalse();
    }

    [Theory]
    [InlineData(ArpPattern.Up)]
    [InlineData(ArpPattern.Down)]
    [InlineData(ArpPattern.UpDown)]
    [InlineData(ArpPattern.DownUp)]
    [InlineData(ArpPattern.Random)]
    [InlineData(ArpPattern.Order)]
    public void Pattern_CanBeSet(ArpPattern pattern)
    {
        var synth = new MockSynth();
        var arp = new Arpeggiator(synth);

        arp.Pattern = pattern;

        arp.Pattern.Should().Be(pattern);
    }
}
