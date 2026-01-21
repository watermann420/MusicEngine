using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core.MusicTheory;

public class ScaleTests
{
    [Fact]
    public void GetNotes_MajorScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Major);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 64, 65, 67, 69, 71);
    }

    [Fact]
    public void GetNotes_NaturalMinorScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.NaturalMinor);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 63, 65, 67, 68, 70);
    }

    [Fact]
    public void GetNotes_PentatonicMajor_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.PentatonicMajor);

        notes.Should().HaveCount(5);
        notes.Should().ContainInOrder(60, 62, 64, 67, 69);
    }

    [Fact]
    public void GetNotes_MultipleOctaves_ExtendsCorrectly()
    {
        var notes = Scale.GetNotes(60, ScaleType.Major, 2);

        notes.Should().HaveCount(14);
        notes.First().Should().Be(60);
        notes.Last().Should().Be(83);
    }

    [Theory]
    [InlineData(60, 60, ScaleType.Major, 1)] // C is root (1st degree)
    [InlineData(64, 60, ScaleType.Major, 3)] // E is 3rd degree
    [InlineData(67, 60, ScaleType.Major, 5)] // G is 5th degree
    [InlineData(61, 60, ScaleType.Major, -1)] // C# is not in C major
    public void GetDegree_ReturnsCorrectDegree(int note, int root, ScaleType type, int expectedDegree)
    {
        Scale.GetDegree(note, root, type).Should().Be(expectedDegree);
    }

    [Theory]
    [InlineData(60, 60, ScaleType.Major, true)]
    [InlineData(61, 60, ScaleType.Major, false)]
    [InlineData(63, 60, ScaleType.NaturalMinor, true)]
    [InlineData(63, 60, ScaleType.Major, false)]
    public void IsInScale_ReturnsCorrectResult(int note, int root, ScaleType type, bool expected)
    {
        Scale.IsInScale(note, root, type).Should().Be(expected);
    }

    [Fact]
    public void Quantize_SnapsToNearestScaleNote()
    {
        // C# (61) should quantize to either C (60) or D (62) in C major
        var quantized = Scale.Quantize(61, 60, ScaleType.Major);

        (quantized == 60 || quantized == 62).Should().BeTrue();
    }

    [Fact]
    public void Quantize_ScaleNoteUnchanged()
    {
        var quantized = Scale.Quantize(60, 60, ScaleType.Major);
        quantized.Should().Be(60);
    }

    [Fact]
    public void GetDiatonicChords_MajorScale_ReturnsCorrectTypes()
    {
        var chords = Scale.GetDiatonicChords(60, ScaleType.Major);

        chords.Should().HaveCount(7);
        chords[0].type.Should().Be(ChordType.Major); // I
        chords[1].type.Should().Be(ChordType.Minor); // ii
        chords[2].type.Should().Be(ChordType.Minor); // iii
        chords[3].type.Should().Be(ChordType.Major); // IV
        chords[4].type.Should().Be(ChordType.Major); // V
        chords[5].type.Should().Be(ChordType.Minor); // vi
        chords[6].type.Should().Be(ChordType.Diminished); // vii°
    }

    [Fact]
    public void GetRelative_ReturnsCorrectRelative()
    {
        // Relative minor of C major is A minor (3 semitones down)
        Scale.GetRelative(60, ScaleType.Major).Should().Be(57);

        // Relative major of A minor is C major (3 semitones up)
        Scale.GetRelative(57, ScaleType.NaturalMinor).Should().Be(60);
    }

    [Fact]
    public void GetParallel_ReturnsCorrectParallel()
    {
        Scale.GetParallel(ScaleType.Major).Should().Be(ScaleType.NaturalMinor);
        Scale.GetParallel(ScaleType.NaturalMinor).Should().Be(ScaleType.Major);
    }
}
