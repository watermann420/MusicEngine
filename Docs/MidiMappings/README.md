# MIDI Mappings (Presets)

This folder is for device-specific mapping presets and examples.

## Generic Jog Wheel

```csharp
var map = new MidiMap();
map.Set("JogWheel", 21);
map.SetJog("JogWheel", 21, JogMode.RelativeSigned, 1);
MidiMapLibrary.Register("GenericJog", map);
```

## Use in Script

```csharp
var preset = MidiMapLibrary.Get("GenericJog");
midi.device(0).jog(preset, "JogWheel")
    .to(delta => time.ScratchTicks(delta));
```
