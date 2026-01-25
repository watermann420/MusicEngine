MusicEngine Factory Presets
============================

This folder contains factory preset banks for MusicEngine synthesizers.

File Formats:
- .mepb - MusicEngine Preset Bank (compressed ZIP containing bank.json)
- .mepreset - Individual preset file (JSON)

To generate the factory preset bank programmatically, use:

    var bank = FactoryPresets.CreateFactoryBank();
    bank.Save("path/to/MusicEngine Factory.mepb");

Or save individual presets:

    foreach (var preset in FactoryPresets.CreatePolySynthPresets())
    {
        preset.SaveToFile($"path/to/{preset.Name}.mepreset");
    }

Built-in Categories:
- Bass: Bass sounds (sub bass, synth bass, etc.)
- Lead: Lead sounds for melodies and solos
- Pad: Pad sounds for harmonic backgrounds
- Keys: Keyboard and piano sounds
- Pluck: Short, plucked sounds and stabs
- Strings: String ensemble and synth strings
- Brass: Brass and horn sounds
- FX: Sound effects, risers, impacts
- Drums: Drum and percussion sounds
- Atmosphere: Atmospheric textures and ambient sounds

Synth Types:
- PolySynth: Polyphonic synthesizer with voice management
- FMSynth: FM synthesis with 6 operators
- SimpleSynth: Basic monophonic oscillator
