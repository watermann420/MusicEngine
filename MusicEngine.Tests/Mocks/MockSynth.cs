using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Tests.Mocks;

/// <summary>
/// Mock synthesizer for testing purposes.
/// </summary>
public class MockSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<(int Note, int Velocity)> _activeNotes = new();
    private readonly List<(string Name, float Value)> _parameterChanges = new();

    public string Name { get; set; } = "MockSynth";
    public WaveFormat WaveFormat => _waveFormat;

    public IReadOnlyList<(int Note, int Velocity)> ActiveNotes => _activeNotes;
    public IReadOnlyList<(string Name, float Value)> ParameterChanges => _parameterChanges;

    public int NoteOnCount { get; private set; }
    public int NoteOffCount { get; private set; }
    public int AllNotesOffCount { get; private set; }

    public MockSynth(int sampleRate = 44100, int channels = 2)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    public void NoteOn(int note, int velocity)
    {
        _activeNotes.Add((note, velocity));
        NoteOnCount++;
    }

    public void NoteOff(int note)
    {
        _activeNotes.RemoveAll(n => n.Note == note);
        NoteOffCount++;
    }

    public void AllNotesOff()
    {
        _activeNotes.Clear();
        AllNotesOffCount++;
    }

    public void SetParameter(string name, float value)
    {
        _parameterChanges.Add((name, value));
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Generate silence
        Array.Clear(buffer, offset, count);
        return count;
    }

    public void Reset()
    {
        _activeNotes.Clear();
        _parameterChanges.Clear();
        NoteOnCount = 0;
        NoteOffCount = 0;
        AllNotesOffCount = 0;
    }
}
