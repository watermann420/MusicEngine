# Speech Instrument

Text-to-speech instrument that generates audio on the fly (no wave files required).

## Basic Example

```csharp
var speech = CreateSpeech();

speech.Phrase("hello", note: 60);
speech.Phrase("world", note: 62);

midi.Device(0).to(speech);
```

Tuning example:
```csharp
var speech = CreateSpeech();
speech.Speed = 1.2f;
speech.VoicePitch = 1.1f;
speech.Phrase("Hallo MusicEngine", note: 60);
speech.NoteOn(60, 100);
```

## Pattern Example

```csharp
var speech = CreateSpeech();
speech.Phrase("alpha", note: 60);
speech.Phrase("beta", note: 62);

var pattern = CreatePattern(speech);
pattern.Note(60, 0.0, 0.5, 110);
pattern.Note(62, 0.5, 0.5, 110);
pattern.Play();
```

Notes:
- TTS uses the Windows speech engine when available (falls back to offline synthesis).
- The implementation lives in `MusicEngine.Library` (optional content project).
- Build the library so `MusicEngine.Library.dll` sits next to the engine executable.
- Use `Phrase(text, note)` to map speech to notes.

Parameters:
- `Rate` / `Speed`: playback speed multiplier (1 = normal).
- `Pitch` / `VoicePitch`: voice pitch multiplier (1 = normal).
- `FormantShift`: shifts vowel color (1 = normal).
- `VoiceLevel`: voiced component level.
- `NoiseLevel`: noise component level.
