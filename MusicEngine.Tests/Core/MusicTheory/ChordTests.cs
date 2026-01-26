using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core.MusicTheory;

public class ChordTests
{
    #region GetNotes Basic Tests

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
    public void GetNotes_DiminishedChord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Diminished);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 63, 66 }); // C, Eb, Gb
    }

    [Fact]
    public void GetNotes_AugmentedChord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Augmented);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 64, 68 }); // C, E, G#
    }

    #endregion

    #region GetNotes Seventh Chord Tests

    [Fact]
    public void GetNotes_Major7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Major7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 64, 67, 71 }); // C, E, G, B
    }

    [Fact]
    public void GetNotes_Minor7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Minor7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 63, 67, 70 }); // C, Eb, G, Bb
    }

    [Fact]
    public void GetNotes_Dominant7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Dominant7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 64, 67, 70 }); // C, E, G, Bb
    }

    [Fact]
    public void GetNotes_Diminished7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Diminished7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 63, 66, 69 }); // C, Eb, Gb, Bbb(A)
    }

    [Fact]
    public void GetNotes_HalfDiminished7Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.HalfDiminished7);

        notes.Should().HaveCount(4);
        notes.Should().Contain(new[] { 60, 63, 66, 70 }); // C, Eb, Gb, Bb
    }

    #endregion

    #region GetNotes Extended Chord Tests

    [Fact]
    public void GetNotes_Major9Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Major9);

        notes.Should().HaveCount(5);
        notes.Should().Contain(new[] { 60, 64, 67, 71, 74 }); // C, E, G, B, D
    }

    [Fact]
    public void GetNotes_Sus2Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Sus2);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 62, 67 }); // C, D, G
    }

    [Fact]
    public void GetNotes_Sus4Chord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Sus4);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 65, 67 }); // C, F, G
    }

    [Fact]
    public void GetNotes_PowerChord_ReturnsCorrectIntervals()
    {
        var notes = Chord.GetNotes(60, ChordType.Power);

        notes.Should().HaveCount(2);
        notes.Should().Contain(new[] { 60, 67 }); // C, G
    }

    #endregion

    #region GetNotes Overload Tests

    [Fact]
    public void GetNotes_FromString_ParsesCorrectly()
    {
        var notes = Chord.GetNotes("C4", ChordType.Major);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 64, 67 });
    }

    [Fact]
    public void GetNotes_FromNoteName_ReturnsCorrectNotes()
    {
        var notes = Chord.GetNotes(NoteName.C, 4, ChordType.Major);

        notes.Should().HaveCount(3);
        notes.Should().Contain(new[] { 60, 64, 67 });
    }

    [Fact]
    public void GetNotes_DifferentRoots_ReturnsTransposedIntervals()
    {
        var cMajor = Chord.GetNotes(60, ChordType.Major);
        var dMajor = Chord.GetNotes(62, ChordType.Major);

        // D major should be C major transposed up 2 semitones
        dMajor.Should().BeEquivalentTo(cMajor.Select(n => n + 2));
    }

    #endregion

    #region GetNotes Count Tests

    [Theory]
    [InlineData(ChordType.Power, 2)]
    [InlineData(ChordType.Major, 3)]
    [InlineData(ChordType.Minor, 3)]
    [InlineData(ChordType.Diminished, 3)]
    [InlineData(ChordType.Augmented, 3)]
    [InlineData(ChordType.Sus2, 3)]
    [InlineData(ChordType.Sus4, 3)]
    [InlineData(ChordType.Major7, 4)]
    [InlineData(ChordType.Minor7, 4)]
    [InlineData(ChordType.Dominant7, 4)]
    [InlineData(ChordType.Add9, 4)]
    [InlineData(ChordType.Major9, 5)]
    [InlineData(ChordType.Dominant9, 5)]
    [InlineData(ChordType.Dominant11, 6)]
    public void GetNotes_ReturnsCorrectNoteCount(ChordType type, int expectedCount)
    {
        var notes = Chord.GetNotes(60, type);
        notes.Should().HaveCount(expectedCount);
    }

    #endregion

    #region GetNotes Clamping Tests

    [Fact]
    public void GetNotes_ClampsToValidMidiRange()
    {
        // High root note - chord should clamp at 127
        var notes = Chord.GetNotes(120, ChordType.Major);

        notes.Should().AllSatisfy(n => n.Should().BeLessOrEqualTo(127));
        notes.Should().AllSatisfy(n => n.Should().BeGreaterOrEqualTo(0));
    }

    [Fact]
    public void GetNotes_HandlesHighRoot()
    {
        var notes = Chord.GetNotes(125, ChordType.Major);

        notes.Should().Contain(125);
        notes.Should().OnlyContain(n => n <= 127);
    }

    #endregion

    #region GetInversion Tests

    [Fact]
    public void GetInversion_FirstInversion_ReturnsCorrectNotes()
    {
        var original = new[] { 60, 64, 67 }; // C major
        var inverted = Chord.GetInversion(original, 1);

        inverted.Should().HaveCount(3);
        inverted.Should().Contain(64); // E
        inverted.Should().Contain(67); // G
        inverted.Should().Contain(72); // C (octave up)
    }

    [Fact]
    public void GetInversion_SecondInversion_ReturnsCorrectNotes()
    {
        var original = new[] { 60, 64, 67 }; // C major
        var inverted = Chord.GetInversion(original, 2);

        inverted.Should().HaveCount(3);
        inverted.Should().Contain(67); // G
        inverted.Should().Contain(72); // C
        inverted.Should().Contain(76); // E
    }

    [Fact]
    public void GetInversion_ZeroInversion_ReturnsOriginal()
    {
        var original = new[] { 60, 64, 67 };
        var inverted = Chord.GetInversion(original, 0);

        inverted.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void GetInversion_EmptyArray_ReturnsEmpty()
    {
        var inverted = Chord.GetInversion(Array.Empty<int>(), 1);

        inverted.Should().BeEmpty();
    }

    [Fact]
    public void GetInversion_InversionWraps_ReturnsValidNotes()
    {
        var original = new[] { 60, 64, 67 };
        // Third inversion of a triad should be same as no inversion
        var inverted = Chord.GetInversion(original, 3);

        // Should wrap around
        inverted.Should().HaveCount(3);
    }

    [Fact]
    public void GetInversion_ReturnsSortedNotes()
    {
        var original = new[] { 60, 64, 67 };
        var inverted = Chord.GetInversion(original, 1);

        inverted.Should().BeInAscendingOrder();
    }

    #endregion

    #region Spread Tests

    [Fact]
    public void Spread_WithOctaveSpread_SpreadsChord()
    {
        var original = new[] { 60, 64, 67 };
        var spread = Chord.Spread(original, 1);

        spread.Should().HaveCount(3);
        // Notes should be spread across octaves
        spread[0].Should().Be(60);
    }

    [Fact]
    public void Spread_ClampsToMidiRange()
    {
        var original = new[] { 120, 124, 127 };
        var spread = Chord.Spread(original, 2);

        spread.Should().OnlyContain(n => n <= 127);
    }

    #endregion

    #region Drop Tests

    [Fact]
    public void Drop_Drop2_DropsSecondVoice()
    {
        var notes = new[] { 60, 64, 67, 72 }; // C E G C
        var dropped = Chord.Drop(notes, 2);

        dropped.Should().HaveCount(4);
        // Second highest note (G) should be dropped an octave
    }

    [Fact]
    public void Drop_InvalidDropVoice_ReturnsOriginal()
    {
        var notes = new[] { 60, 64, 67 };
        var dropped = Chord.Drop(notes, 0);

        dropped.Should().BeEquivalentTo(notes);
    }

    [Fact]
    public void Drop_DropVoiceTooBig_ReturnsOriginal()
    {
        var notes = new[] { 60, 64, 67 };
        var dropped = Chord.Drop(notes, 10);

        dropped.Should().BeEquivalentTo(notes);
    }

    #endregion

    #region Chord Shortcut Tests

    [Fact]
    public void ChordShortcuts_ReturnCorrectNotes()
    {
        Chord.CMaj(4).Should().Contain(new[] { 60, 64, 67 });
        Chord.CMin(4).Should().Contain(new[] { 60, 63, 67 });
        Chord.AMin(4).Should().Contain(new[] { 69, 72, 76 });
        Chord.AMaj(4).Should().Contain(new[] { 69, 73, 76 });
    }

    [Fact]
    public void ChordShortcuts_AllKeysWork()
    {
        Chord.DMaj(4).Should().HaveCount(3);
        Chord.DMin(4).Should().HaveCount(3);
        Chord.EMaj(4).Should().HaveCount(3);
        Chord.EMin(4).Should().HaveCount(3);
        Chord.FMaj(4).Should().HaveCount(3);
        Chord.FMin(4).Should().HaveCount(3);
        Chord.GMaj(4).Should().HaveCount(3);
        Chord.GMin(4).Should().HaveCount(3);
        Chord.BMaj(4).Should().HaveCount(3);
        Chord.BMin(4).Should().HaveCount(3);
    }

    [Fact]
    public void ChordShortcuts_DifferentOctaves()
    {
        var c3 = Chord.CMaj(3);
        var c4 = Chord.CMaj(4);
        var c5 = Chord.CMaj(5);

        // Each octave should be 12 semitones apart
        c4.Should().BeEquivalentTo(c3.Select(n => n + 12));
        c5.Should().BeEquivalentTo(c4.Select(n => n + 12));
    }

    #endregion

    #region All ChordTypes Tests

    [Fact]
    public void GetNotes_AllChordTypes_ReturnValidNotes()
    {
        foreach (ChordType type in Enum.GetValues<ChordType>())
        {
            var notes = Chord.GetNotes(60, type);

            notes.Should().NotBeEmpty($"ChordType.{type} should produce notes");
            notes.Should().OnlyContain(n => n >= 0 && n <= 127,
                $"ChordType.{type} should produce valid MIDI notes");
            notes.Should().Contain(60,
                $"ChordType.{type} should contain the root note");
        }
    }

    [Fact]
    public void GetNotes_AllChordTypes_ContainRoot()
    {
        foreach (ChordType type in Enum.GetValues<ChordType>())
        {
            var notes = Chord.GetNotes(60, type);
            notes[0].Should().Be(60, $"ChordType.{type} should have root as first note");
        }
    }

    #endregion
}
