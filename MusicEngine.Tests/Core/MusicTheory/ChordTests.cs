using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core.MusicTheory;

public class ChordTests
{
    [Fact]
    public void GetNotes_MajorChord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Major);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 64, 67 }); // C, E, G
    }

    [Fact]
    public void GetNotes_MinorChord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Minor);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 63, 67 }); // C, Eb, G
    }

    [Fact]
    public void GetNotes_Major7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Major7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 64, 67, 71 }); // C, E, G, B
    }

    [Fact]
    public void GetNotes_Dominant7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Dominant7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 64, 67, 70 }); // C, E, G, Bb
    }

    [Fact]
    public void GetNotes_FromString_ParsesCorrectly()
    {
        var notes = Chord.GetNotes("C4", ChordType.Major);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 64, 67 });
    }

    [Theory]
    [InlineData(ChordType.Power, 2)]
    [InlineData(ChordType.Major, 3)]
    [InlineData(ChordType.Minor7, 4)]
    [InlineData(ChordType.Major9, 5)]
    [InlineData(ChordType.Dominant11, 6)]
    public void GetNotes_ReturnsCorrectNoteCount(ChordType type, int expectedCount)
    {
        var notes = Chord.GetNotes(60, type);
        notes.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void GetInversion_FirstInversion_ReturnsCorrectNotes()
    {
        var original = new[] { 60, 64, 67 }; // C major
        var inverted = Chord.GetInversion(original, 1);

        inverted.Should().HaveCount(3);
        // First inversion moves root up an octave
        inverted.Should().Contain(64); // E
        inverted.Should().Contain(67); // G
        inverted.Should().Contain(72); // C (octave up)
    }

    [Fact]
    public void GetInversion_ZeroInversion_ReturnsOriginal()
    {
        var original = new[] { 60, 64, 67 };
        var inverted = Chord.GetInversion(original, 0);

        inverted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ChordShortcuts_ReturnCorrectNotes()
    {
        Chord.CMaj(4).Should().Contain(new[] { 60, 64, 67 });
        Chord.AMin(4).Should().Contain(new[] { 69, 72, 76 });
    }
}
