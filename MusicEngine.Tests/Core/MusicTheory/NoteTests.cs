using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core.MusicTheory;

public class NoteTests
{
    [Theory]
    [InlineData(NoteName.C, 4, 60)]
    [InlineData(NoteName.A, 4, 69)]
    [InlineData(NoteName.C, 0, 12)]
    [InlineData(NoteName.C, -1, 0)]
    [InlineData(NoteName.G, 9, 127)]
    public void FromName_ReturnsCorrectMidiNote(NoteName note, int octave, int expectedMidi)
    {
        Note.FromName(note, octave).Should().Be(expectedMidi);
    }

    [Theory]
    [InlineData("C4", 60)]
    [InlineData("A4", 69)]
    [InlineData("C#4", 61)]
    [InlineData("Db4", 61)]
    [InlineData("F#3", 54)]
    [InlineData("Bb5", 82)]
    [InlineData("C0", 12)]
    public void FromString_ParsesCorrectly(string noteString, int expectedMidi)
    {
        Note.FromString(noteString).Should().Be(expectedMidi);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X4")]
    [InlineData("Z#5")]
    public void FromString_ThrowsOnInvalidInput(string invalidNote)
    {
        Action act = () => Note.FromString(invalidNote);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(60, "C4")]
    [InlineData(69, "A4")]
    [InlineData(61, "C#4")]
    [InlineData(127, "G9")]
    public void ToName_ReturnsCorrectString(int midiNote, string expectedName)
    {
        Note.ToName(midiNote).Should().Be(expectedName);
    }

    [Theory]
    [InlineData(60, NoteName.C)]
    [InlineData(61, NoteName.CSharp)]
    [InlineData(69, NoteName.A)]
    public void GetNoteName_ReturnsCorrectNoteName(int midiNote, NoteName expectedName)
    {
        Note.GetNoteName(midiNote).Should().Be(expectedName);
    }

    [Theory]
    [InlineData(60, 4)]
    [InlineData(72, 5)]
    [InlineData(48, 3)]
    [InlineData(0, -1)]
    public void GetOctave_ReturnsCorrectOctave(int midiNote, int expectedOctave)
    {
        Note.GetOctave(midiNote).Should().Be(expectedOctave);
    }

    [Theory]
    [InlineData(60, 12, 72)]
    [InlineData(60, -12, 48)]
    [InlineData(120, 20, 127)] // Should clamp to max
    [InlineData(10, -20, 0)] // Should clamp to min
    public void Transpose_TransposesAndClamps(int note, int semitones, int expected)
    {
        Note.Transpose(note, semitones).Should().Be(expected);
    }

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
    public void FromFrequency_ReturnsCorrectMidiNote()
    {
        Note.FromFrequency(440.0).Should().Be(69);
        Note.FromFrequency(261.626).Should().Be(60);
    }

    [Fact]
    public void NoteShortcuts_ReturnCorrectValues()
    {
        Note.C(4).Should().Be(60);
        Note.D(4).Should().Be(62);
        Note.E(4).Should().Be(64);
        Note.A(4).Should().Be(69);
    }
}
