# Activity + Sleep Performance Control

This document explains how the engine decides when to process, sleep, or bypass work. The goal is to keep CPU usage as low as possible while preserving audio quality.

## Core Principles

- Do not process what is silent.
- Do not update DSP if the source is idle.
- Always keep state, but allow processing to sleep.
- Only re-enable work when activity is detected.

## Global Settings (Settings.cs)

These defaults apply engine-wide. You can change them in code or via the Activity controller.

- AudioSilenceThreshold
  - Amplitude threshold used to decide if a buffer is silent.
  - Default: 1e-5.

- AudioEffectsEnabled
  - Enables/disables non-VST effect processing (engine effects).
  - When false, effects are bypassed (input -> output).

- VstInstrumentsEnabled
  - Enables/disables VST instrument processing.
  - When false, VST instruments return silence and ignore MIDI.

- VstEffectsEnabled
  - Enables/disables VST effect processing.
  - When false, VST effects are bypassed (input -> output).

- SequencerEnabled
  - Enables/disables sequencer processing.
  - When false, the sequencer thread stays alive but does no work.

- VstInstrumentSleepWhenIdle / VstEffectSleepWhenIdle
  - Default per-instance idle sleep behavior.

- VstIdleThreshold / VstIdleTimeoutSeconds
  - Silence threshold and time before VSTs sleep.

## Activity Controller (Script API)

The script API exposes a global Activity controller:

```csharp
Activity.AudioEnabled = true;           // starts/stops output device
Activity.MidiEnabled = true;            // enables/disables MIDI routing
Activity.SequencerEnabled = true;       // starts/stops sequencer
Activity.AudioEffectsEnabled = true;    // engine effects on/off
Activity.VstInstrumentsEnabled = true;  // VST instruments on/off
Activity.VstEffectsEnabled = true;      // VST effects on/off

Activity.VstIdleThreshold = 5e-4f;      // adjust idle sensitivity
Activity.VstIdleTimeoutSeconds = 0.08;  // time before sleep
Activity.AggressiveVstSleep();          // preset: more aggressive
```

## Where Sleep Happens

- AudioEngine
  - Virtual outputs do not push silence.
  - Channel sends skip silent buffers.

- SimpleSynth / SamplerInstrument
  - If no active voices, the read path exits early.

- VST Instrument
  - If output is silent for IdleTimeoutSeconds and no active notes, it sleeps.
  - Wakes on NoteOn, PitchBend, or parameter changes.

- VST Effect
  - If input + output remain silent for IdleTimeoutSeconds, it sleeps.
  - Wakes on parameter changes or non-silent input.
  - When VstEffectsEnabled = false, effect is bypassed.

- Sequencer
  - When no patterns are registered, loop sleeps longer.
  - When SequencerEnabled = false, no processing is done.

## Why This Improves Audio

- Less CPU usage leaves more headroom for the audio thread.
- Less contention reduces dropouts and crackles.
- Sleeping inactive DSP avoids unnecessary overhead.

## Tuning Tips

- If VSTs wake too slowly, lower VstIdleTimeoutSeconds.
- If CPU is still high, raise VstIdleThreshold or use AggressiveVstSleep().
- For very quiet material, use a lower AudioSilenceThreshold.

## When Not To Sleep

- Live performance: you may want faster wake-up (lower timeout).
- Heavy sidechains: some effects rely on always-on processing.

## Notes

- The engine favors "sleep" over "dispose"; state stays intact.
- Disabling output or sequencer does not destroy objects.
