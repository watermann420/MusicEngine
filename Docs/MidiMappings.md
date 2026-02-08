# MIDI Mapping Helper

Use a named mapping to make scripts portable across different MIDI controllers.

## Basic Example

```csharp
var midimap = Midi.Map;
midimap.Set("JogWheel", 21);
midimap.Set("Filter", 74);
midimap.SetNote("Pad1", 36);
```

## Jog Wheel Mapping

```csharp
var midimap = Midi.Map;
midimap.SetJog("JogWheel", 21, JogMode.RelativeSigned, scale: 1);

midi.device(0).jog(midimap, "JogWheel")
    .to(delta => time.ScratchTicks(delta));
```

## Device Presets (Code)

```csharp
var map = new MidiMap();
map.Set("JogWheel", 21);
map.SetJog("JogWheel", 21, JogMode.RelativeBinaryOffset, 1);
MidiMapLibrary.Register("MyController", map);

var preset = MidiMapLibrary.Get("MyController");
midi.device(0).jog(preset, "JogWheel")
    .to(delta => time.ScratchTicks(delta));
```

## Notes

- Mapping names are case-insensitive.
- If a jog mapping is not found, the control ID mapping is used.
