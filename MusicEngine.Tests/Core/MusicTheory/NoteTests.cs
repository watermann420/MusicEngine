using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core.MusicTheory;

public class NoteTests
{
    #region FromName Tests

    [Theory]
    [InlineData(NoteName.C, 4, 60)]
    [InlineData(NoteName.A, 4, 69)]
    [InlineData(NoteName.C, 0, 12)]
    [InlineData(NoteName.C, -1, 0)]
    [InlineData(NoteName.G, 9, 127)]
    [InlineData(NoteName.CSharp, 4, 61)]
    [InlineData(NoteName.D, 4, 62)]
    [InlineData(NoteName.DSharp, 4, 63)]
    [InlineData(NoteName.E, 4, 64)]
    [InlineData(NoteName.F, 4, 65)]
    [InlineData(NoteName.FSharp, 4, 66)]
    [InlineData(NoteName.G, 4, 67)]
    [InlineData(NoteName.GSharp, 4, 68)]
    [InlineData(NoteName.ASharp, 4, 70)]
    [InlineData(NoteName.B, 4, 71)]
    public void FromName_ReturnsCorrectMidiNote(NoteName note, int octave, int expectedMidi)
    {
        Note.FromName(note, octave).Should().Be(expectedMidi);
    }

    [Fact]
    public void FromName_WithDefaultOctave_ReturnsOctave4()
    {
        Note.FromName(NoteName.C).Should().Be(60);
    }

    [Fact]
    public void FromName_AllNoteNames_ReturnsUniqueValues()
    {
        var midiNotes = Enum.GetValues<NoteName>()
            .Select(n => Note.FromName(n, 4))
            .ToList();

        midiNotes.Should().HaveCount(12);
        midiNotes.Distinct().Should().HaveCount(12);
    }

    #endregion

    #region FromString Tests

    [Theory]
    [InlineData("C4", 60)]
    [InlineData("A4", 69)]
    [InlineData("C#4", 61)]
    [InlineData("Db4", 61)]
    [InlineData("F#3", 54)]
    [InlineData("Bb5", 82)]
    [InlineData("C0", 12)]
    [InlineData("c4", 60)] // lowercase
    [InlineData("C", 60)] // no octave defaults to 4
    [InlineData("C#", 61)] // sharp without octave
    [InlineData("Eb", 63)] // flat without octave
    public void FromString_ParsesCorrectly(string noteString, int expectedMidi)
    {
        Note.FromString(noteString).Should().Be(expectedMidi);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("X4")]
    [InlineData("Z#5")]
    public void FromString_ThrowsOnInvalidInput(string invalidNote)
    {
        Action act = () => Note.FromString(invalidNote);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromString_WithWhitespace_TrimsAndParses()
    {
        Note.FromString("  C4  ").Should().Be(60);
    }

    [Theory]
    [InlineData("C-1", 0)]
    [InlineData("G9", 127)]
    public void FromString_HandlesExtremeOctaves(string noteString, int expectedMidi)
    {
        Note.FromString(noteString).Should().Be(expectedMidi);
    }

    #endregion

    #region ToName Tests

    [Theory]
    [InlineData(60, "C4")]
    [InlineData(69, "A4")]
    [InlineData(61, "C#4")]
    [InlineData(127, "G9")]
    [InlineData(0, "C-1")]
    [InlineData(12, "C0")]
    [InlineData(24, "C1")]
    public void ToName_ReturnsCorrectString(int midiNote, string expectedName)
    {
        Note.ToName(midiNote).Should().Be(expectedName);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(61)]
    [InlineData(69)]
    [InlineData(127)]
    public void ToName_RoundTrips_WithFromString(int midiNote)
    {
        var name = Note.ToName(midiNote);
        Note.FromString(name).Should().Be(midiNote);
    }

    #endregion

    #region GetNoteName Tests

    [Theory]
    [InlineData(60, NoteName.C)]
    [InlineData(61, NoteName.CSharp)]
    [InlineData(69, NoteName.A)]
    [InlineData(72, NoteName.C)] // C5
    [InlineData(84, NoteName.C)] // C6
    [InlineData(0, NoteName.C)]  // C-1
    public void GetNoteName_ReturnsCorrectNoteName(int midiNote, NoteName expectedName)
    {
        Note.GetNoteName(midiNote).Should().Be(expectedName);
    }

    [Fact]
    public void GetNoteName_IsOctaveIndependent()
    {
        // Same note name across different octaves
        Note.GetNoteName(48).Should().Be(NoteName.C); // C3
        Note.GetNoteName(60).Should().Be(NoteName.C); // C4
        Note.GetNoteName(72).Should().Be(NoteName.C); // C5
    }

    #endregion

    #region GetOctave Tests

    [Theory]
    [InlineData(60, 4)]
    [InlineData(72, 5)]
    [InlineData(48, 3)]
    [InlineData(0, -1)]
    [InlineData(12, 0)]
    [InlineData(127, 9)]
    public void GetOctave_ReturnsCorrectOctave(int midiNote, int expectedOctave)
    {
        Note.GetOctave(midiNote).Should().Be(expectedOctave);
    }

    [Fact]
    public void GetOctave_ContiguousWithinOctave()
    {
        // All notes from 60-71 should be octave 4
        for (int i = 60; i <= 71; i++)
        {
            Note.GetOctave(i).Should().Be(4);
        }
    }

    #endregion

    #region Transpose Tests

    [Theory]
    [InlineData(60, 12, 72)]  // Up one octave
    [InlineData(60, -12, 48)] // Down one octave
    [InlineData(60, 7, 67)]   // Up a fifth
    [InlineData(120, 20, 127)] // Should clamp to max
    [InlineData(10, -20, 0)]   // Should clamp to min
    [InlineData(60, 0, 60)]    // No transpose
    public void Transpose_TransposesAndClamps(int note, int semitones, int expected)
    {
        Note.Transpose(note, semitones).Should().Be(expected);
    }

    [Fact]
    public void Transpose_ClampsToMinimum()
    {
        Note.Transpose(0, -100).Should().Be(0);
    }

    [Fact]
    public void Transpose_ClampsToMaximum()
    {
        Note.Transpose(127, 100).Should().Be(127);
    }

    #endregion

    #region GetFrequency Tests

    [Fact]
    public void GetFrequency_ReturnsCorrectFrequencyForA4()
    {
        Note.GetFrequency(69).Should().BeApproximately(440.0, 0.001);
    }

    [Fact]
    public void GetFrequency_ReturnsCorrectFrequencyForMiddleC()
    {
        Note.GetFrequency(60).Should().BeApproximately(261.626, 0.01);
    }

    [Fact]
    public void GetFrequency_DoublesEveryOctave()
    {
        var freqA4 = Note.GetFrequency(69);
        var freqA5 = Note.GetFrequency(81);

        freqA5.Should().BeApproximately(freqA4 * 2, 0.001);
    }

    [Fact]
    public void GetFrequency_HalvesEveryOctave()
    {
        var freqA4 = Note.GetFrequency(69);
        var freqA3 = Note.GetFrequency(57);

        freqA3.Should().BeApproximately(freqA4 / 2, 0.001);
    }

    [Theory]
    [InlineData(21, 27.5)]   // A0
    [InlineData(108, 4186.01)] // C8
    public void GetFrequency_ReturnsExpectedValues(int midiNote, double expectedFrequency)
    {
        Note.GetFrequency(midiNote).Should().BeApproximately(expectedFrequency, 0.1);
    }

    #endregion

    #region FromFrequency Tests

    [Fact]
    public void FromFrequency_ReturnsCorrectMidiNote()
    {
        Note.FromFrequency(440.0).Should().Be(69);
        Note.FromFrequency(261.626).Should().Be(60);
    }

    [Fact]
    public void FromFrequency_RoundTrips_WithGetFrequency()
    {
        for (int midiNote = 21; midiNote <= 108; midiNote++)
        {
            var frequency = Note.GetFrequency(midiNote);
            Note.FromFrequency(frequency).Should().Be(midiNote);
        }
    }

    [Fact]
    public void FromFrequency_RoundsToNearestNote()
    {
        // 443 Hz should round to A4 (440 Hz)
        Note.FromFrequency(443.0).Should().Be(69);
    }

    #endregion

    #region Note Shortcut Tests

    [Fact]
    public void NoteShortcuts_ReturnCorrectValues()
    {
        Note.C(4).Should().Be(60);
        Note.D(4).Should().Be(62);
        Note.E(4).Should().Be(64);
        Note.F(4).Should().Be(65);
        Note.G(4).Should().Be(67);
        Note.A(4).Should().Be(69);
        Note.B(4).Should().Be(71);
    }

    [Fact]
    public void NoteShortcuts_WithDefaultOctave_ReturnsOctave4()
    {
        Note.C().Should().Be(60);
        Note.D().Should().Be(62);
        Note.E().Should().Be(64);
        Note.F().Should().Be(65);
        Note.G().Should().Be(67);
        Note.A().Should().Be(69);
        Note.B().Should().Be(71);
    }

    [Fact]
    public void NoteShortcuts_DifferentOctaves()
    {
        Note.C(3).Should().Be(48);
        Note.C(5).Should().Be(72);
        Note.A(3).Should().Be(57);
        Note.A(5).Should().Be(81);
    }

    #endregion
}
