using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core.MusicTheory;

public class ScaleTests
{
    #region GetNotes Basic Scale Tests

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
    public void GetNotes_HarmonicMinorScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.HarmonicMinor);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 63, 65, 67, 68, 71); // Raised 7th
    }

    [Fact]
    public void GetNotes_MelodicMinorScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.MelodicMinor);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 63, 65, 67, 69, 71); // Raised 6th and 7th
    }

    #endregion

    #region GetNotes Mode Tests

    [Fact]
    public void GetNotes_DorianMode_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Dorian);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 63, 65, 67, 69, 70);
    }

    [Fact]
    public void GetNotes_PhrygianMode_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Phrygian);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 61, 63, 65, 67, 68, 70);
    }

    [Fact]
    public void GetNotes_LydianMode_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Lydian);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 64, 66, 67, 69, 71);
    }

    [Fact]
    public void GetNotes_MixolydianMode_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Mixolydian);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 62, 64, 65, 67, 69, 70);
    }

    [Fact]
    public void GetNotes_LocrianMode_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Locrian);

        notes.Should().HaveCount(7);
        notes.Should().ContainInOrder(60, 61, 63, 65, 66, 68, 70);
    }

    #endregion

    #region GetNotes Special Scale Tests

    [Fact]
    public void GetNotes_PentatonicMajor_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.PentatonicMajor);

        notes.Should().HaveCount(5);
        notes.Should().ContainInOrder(60, 62, 64, 67, 69);
    }

    [Fact]
    public void GetNotes_PentatonicMinor_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.PentatonicMinor);

        notes.Should().HaveCount(5);
        notes.Should().ContainInOrder(60, 63, 65, 67, 70);
    }

    [Fact]
    public void GetNotes_BluesScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Blues);

        notes.Should().HaveCount(6);
        notes.Should().ContainInOrder(60, 63, 65, 66, 67, 70);
    }

    [Fact]
    public void GetNotes_WholeToneScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.WholeTone);

        notes.Should().HaveCount(6);
        notes.Should().ContainInOrder(60, 62, 64, 66, 68, 70);
    }

    [Fact]
    public void GetNotes_ChromaticScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Chromatic);

        notes.Should().HaveCount(12);
        // All 12 semitones
        for (int i = 0; i < 12; i++)
        {
            notes.Should().Contain(60 + i);
        }
    }

    [Fact]
    public void GetNotes_DiminishedScale_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(60, ScaleType.Diminished);

        notes.Should().HaveCount(8);
        notes.Should().ContainInOrder(60, 62, 63, 65, 66, 68, 69, 71);
    }

    #endregion

    #region GetNotes Multiple Octaves Tests

    [Fact]
    public void GetNotes_MultipleOctaves_ExtendsCorrectly()
    {
        var notes = Scale.GetNotes(60, ScaleType.Major, 2);

        notes.Should().HaveCount(14);
        notes.First().Should().Be(60);
        notes.Last().Should().Be(83);
    }

    [Fact]
    public void GetNotes_ThreeOctaves_ExtendsCorrectly()
    {
        var notes = Scale.GetNotes(60, ScaleType.Major, 3);

        notes.Should().HaveCount(21);
        notes.First().Should().Be(60);
    }

    [Fact]
    public void GetNotes_OctaveExtension_MaintainsIntervals()
    {
        var oneOctave = Scale.GetNotes(60, ScaleType.Major, 1);
        var twoOctaves = Scale.GetNotes(60, ScaleType.Major, 2);

        // Second octave should have same intervals shifted up 12
        for (int i = 0; i < oneOctave.Length; i++)
        {
            twoOctaves.Should().Contain(oneOctave[i] + 12);
        }
    }

    [Fact]
    public void GetNotes_ClampsToMidiRange()
    {
        // Start high and extend octaves - should not exceed 127
        var notes = Scale.GetNotes(120, ScaleType.Major, 3);

        notes.Should().OnlyContain(n => n <= 127);
    }

    #endregion

    #region GetNotes Overload Tests

    [Fact]
    public void GetNotes_FromString_ParsesCorrectly()
    {
        var notes = Scale.GetNotes("C4", ScaleType.Major);

        notes.Should().HaveCount(7);
        notes.First().Should().Be(60);
    }

    [Fact]
    public void GetNotes_FromNoteName_ReturnsCorrectNotes()
    {
        var notes = Scale.GetNotes(NoteName.C, 4, ScaleType.Major);

        notes.Should().HaveCount(7);
        notes.First().Should().Be(60);
    }

    #endregion

    #region GetDegree Tests

    [Theory]
    [InlineData(60, 60, ScaleType.Major, 1)] // C is root (1st degree)
    [InlineData(62, 60, ScaleType.Major, 2)] // D is 2nd degree
    [InlineData(64, 60, ScaleType.Major, 3)] // E is 3rd degree
    [InlineData(65, 60, ScaleType.Major, 4)] // F is 4th degree
    [InlineData(67, 60, ScaleType.Major, 5)] // G is 5th degree
    [InlineData(69, 60, ScaleType.Major, 6)] // A is 6th degree
    [InlineData(71, 60, ScaleType.Major, 7)] // B is 7th degree
    [InlineData(61, 60, ScaleType.Major, -1)] // C# is not in C major
    public void GetDegree_ReturnsCorrectDegree(int note, int root, ScaleType type, int expectedDegree)
    {
        Scale.GetDegree(note, root, type).Should().Be(expectedDegree);
    }

    [Fact]
    public void GetDegree_OctaveIndependent()
    {
        // C in different octaves should all be 1st degree
        Scale.GetDegree(48, 60, ScaleType.Major).Should().Be(1); // C3
        Scale.GetDegree(60, 60, ScaleType.Major).Should().Be(1); // C4
        Scale.GetDegree(72, 60, ScaleType.Major).Should().Be(1); // C5
    }

    [Fact]
    public void GetDegree_NegativeInterval_HandlesCorrectly()
    {
        // Note below root
        Scale.GetDegree(55, 60, ScaleType.Major).Should().Be(5); // G3 in C major
    }

    #endregion

    #region IsInScale Tests

    [Theory]
    [InlineData(60, 60, ScaleType.Major, true)]  // C in C major
    [InlineData(61, 60, ScaleType.Major, false)] // C# not in C major
    [InlineData(62, 60, ScaleType.Major, true)]  // D in C major
    [InlineData(63, 60, ScaleType.NaturalMinor, true)] // Eb in C minor
    [InlineData(63, 60, ScaleType.Major, false)] // Eb not in C major
    public void IsInScale_ReturnsCorrectResult(int note, int root, ScaleType type, bool expected)
    {
        Scale.IsInScale(note, root, type).Should().Be(expected);
    }

    [Fact]
    public void IsInScale_AllScaleNotes_ReturnsTrue()
    {
        var scaleNotes = Scale.GetNotes(60, ScaleType.Major);

        foreach (var note in scaleNotes)
        {
            Scale.IsInScale(note, 60, ScaleType.Major).Should().BeTrue();
        }
    }

    [Fact]
    public void IsInScale_ChromaticScale_AllNotesAreIn()
    {
        for (int note = 60; note < 72; note++)
        {
            Scale.IsInScale(note, 60, ScaleType.Chromatic).Should().BeTrue();
        }
    }

    #endregion

    #region Quantize Tests

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
    public void Quantize_AllScaleNotes_RemainUnchanged()
    {
        var scaleNotes = Scale.GetNotes(60, ScaleType.Major);

        foreach (var note in scaleNotes)
        {
            Scale.Quantize(note, 60, ScaleType.Major).Should().Be(note);
        }
    }

    [Fact]
    public void Quantize_MidwayBetweenNotes_QuantizesConsistently()
    {
        // Quantize many notes and verify they're all valid scale notes
        for (int note = 36; note <= 96; note++)
        {
            var quantized = Scale.Quantize(note, 60, ScaleType.Major);
            Scale.IsInScale(quantized, 60, ScaleType.Major).Should().BeTrue();
        }
    }

    #endregion

    #region GetDiatonicChords Tests

    [Fact]
    public void GetDiatonicChords_MajorScale_ReturnsCorrectTypes()
    {
        var chords = Scale.GetDiatonicChords(60, ScaleType.Major);

        chords.Should().HaveCount(7);
        chords[0].type.Should().Be(ChordType.Major);     // I
        chords[1].type.Should().Be(ChordType.Minor);     // ii
        chords[2].type.Should().Be(ChordType.Minor);     // iii
        chords[3].type.Should().Be(ChordType.Major);     // IV
        chords[4].type.Should().Be(ChordType.Major);     // V
        chords[5].type.Should().Be(ChordType.Minor);     // vi
        chords[6].type.Should().Be(ChordType.Diminished); // vii
    }

    [Fact]
    public void GetDiatonicChords_NaturalMinorScale_ReturnsCorrectTypes()
    {
        var chords = Scale.GetDiatonicChords(60, ScaleType.NaturalMinor);

        chords.Should().HaveCount(7);
        chords[0].type.Should().Be(ChordType.Minor);     // i
        chords[1].type.Should().Be(ChordType.Diminished); // ii
        chords[2].type.Should().Be(ChordType.Major);     // III
        chords[3].type.Should().Be(ChordType.Minor);     // iv
        chords[4].type.Should().Be(ChordType.Minor);     // v
        chords[5].type.Should().Be(ChordType.Major);     // VI
        chords[6].type.Should().Be(ChordType.Major);     // VII
    }

    [Fact]
    public void GetDiatonicChords_ReturnsCorrectRoots()
    {
        var chords = Scale.GetDiatonicChords(60, ScaleType.Major);
        var scaleNotes = Scale.GetNotes(60, ScaleType.Major);

        for (int i = 0; i < 7; i++)
        {
            chords[i].root.Should().Be(scaleNotes[i]);
        }
    }

    [Fact]
    public void GetDiatonicChords_DorianMode_ReturnsCorrectTypes()
    {
        var chords = Scale.GetDiatonicChords(60, ScaleType.Dorian);

        chords.Should().HaveCount(7);
        chords[0].type.Should().Be(ChordType.Minor);     // i
        chords[1].type.Should().Be(ChordType.Minor);     // ii
        chords[2].type.Should().Be(ChordType.Major);     // III
        chords[3].type.Should().Be(ChordType.Major);     // IV
        chords[4].type.Should().Be(ChordType.Minor);     // v
        chords[5].type.Should().Be(ChordType.Diminished); // vi
        chords[6].type.Should().Be(ChordType.Major);     // VII
    }

    #endregion

    #region GetRelative Tests

    [Fact]
    public void GetRelative_MajorToMinor_ReturnsCorrectRoot()
    {
        // Relative minor of C major is A minor (3 semitones down)
        Scale.GetRelative(60, ScaleType.Major).Should().Be(57);
    }

    [Fact]
    public void GetRelative_MinorToMajor_ReturnsCorrectRoot()
    {
        // Relative major of A minor is C major (3 semitones up)
        Scale.GetRelative(57, ScaleType.NaturalMinor).Should().Be(60);
    }

    [Fact]
    public void GetRelative_OtherScales_ReturnsSameRoot()
    {
        // For scales without defined relative, return same root
        Scale.GetRelative(60, ScaleType.Dorian).Should().Be(60);
    }

    [Fact]
    public void GetRelative_RoundTrips()
    {
        // Going to relative minor then back to relative major
        var relativeMinar = Scale.GetRelative(60, ScaleType.Major);
        var backToMajor = Scale.GetRelative(relativeMinar, ScaleType.NaturalMinor);

        backToMajor.Should().Be(60);
    }

    #endregion

    #region GetParallel Tests

    [Fact]
    public void GetParallel_MajorReturnsMinor()
    {
        Scale.GetParallel(ScaleType.Major).Should().Be(ScaleType.NaturalMinor);
    }

    [Fact]
    public void GetParallel_MinorReturnsMajor()
    {
        Scale.GetParallel(ScaleType.NaturalMinor).Should().Be(ScaleType.Major);
    }

    [Fact]
    public void GetParallel_OtherScales_ReturnsSame()
    {
        Scale.GetParallel(ScaleType.Dorian).Should().Be(ScaleType.Dorian);
        Scale.GetParallel(ScaleType.Blues).Should().Be(ScaleType.Blues);
    }

    #endregion

    #region RandomNote Tests

    [Fact]
    public void RandomNote_ReturnsNoteInScale()
    {
        for (int i = 0; i < 100; i++)
        {
            var note = Scale.RandomNote(60, ScaleType.Major);
            Scale.IsInScale(note, 60, ScaleType.Major).Should().BeTrue();
        }
    }

    [Fact]
    public void RandomNote_RespectsMinMax()
    {
        for (int i = 0; i < 100; i++)
        {
            var note = Scale.RandomNote(60, ScaleType.Major, 48, 72);
            note.Should().BeInRange(48, 72);
        }
    }

    #endregion

    #region All ScaleTypes Tests

    [Fact]
    public void GetNotes_AllScaleTypes_ReturnValidNotes()
    {
        foreach (ScaleType type in Enum.GetValues<ScaleType>())
        {
            var notes = Scale.GetNotes(60, type);

            notes.Should().NotBeEmpty($"ScaleType.{type} should produce notes");
            notes.Should().OnlyContain(n => n >= 0 && n <= 127,
                $"ScaleType.{type} should produce valid MIDI notes");
            notes.Should().Contain(60,
                $"ScaleType.{type} should contain the root note");
        }
    }

    [Fact]
    public void GetNotes_AllScaleTypes_StartWithRoot()
    {
        foreach (ScaleType type in Enum.GetValues<ScaleType>())
        {
            var notes = Scale.GetNotes(60, type);
            notes[0].Should().Be(60, $"ScaleType.{type} should start with root");
        }
    }

    [Fact]
    public void GetNotes_AllScaleTypes_AreAscending()
    {
        foreach (ScaleType type in Enum.GetValues<ScaleType>())
        {
            var notes = Scale.GetNotes(60, type);
            notes.Should().BeInAscendingOrder($"ScaleType.{type} should be ascending");
        }
    }

    #endregion
}
