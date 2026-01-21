using MusicEngine.Core;

namespace MusicEngine.Infrastructure.DependencyInjection.Interfaces;

/// <summary>
/// Interface for the sequencer.
/// </summary>
public interface ISequencer : IDisposable
{
    double Bpm { get; set; }
    double CurrentBeat { get; set; }
    bool IsRunning { get; }
    IReadOnlyList<Pattern> Patterns { get; }

    void AddPattern(Pattern pattern);
    void RemovePattern(Pattern pattern);
    void ClearPatterns();
    void Start();
    void Stop();
    void Skip(double beats);

    event EventHandler<MusicalEventArgs>? NoteTriggered;
    event EventHandler<MusicalEventArgs>? NoteEnded;
    event EventHandler<BeatChangedEventArgs>? BeatChanged;
    event EventHandler<PlaybackStateEventArgs>? PlaybackStarted;
    event EventHandler<PlaybackStateEventArgs>? PlaybackStopped;
}
