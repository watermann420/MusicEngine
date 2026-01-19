# MusicEngine & MusicEngineEditor - Task List

## P0 - Critical (Immediate)

### MusicEngine

- [ ] **Refactor ScriptHost.cs (961 LOC)**
  - Extract ScriptGlobals to separate file
  - Create `FluentApi/` folder with:
    - MidiControl.cs
    - DeviceControl.cs
    - AudioControl.cs
    - VstControl.cs
    - PatternControl.cs
    - SampleControl.cs
    - VirtualChannelControl.cs

- [ ] **Fix Event Subscription Leaks in AudioEngine.cs**
  - Line 142: FftCalculated handler
  - Line 163: DataAvailable handler
  - Line 226: MessageReceived handler
  - Add explicit unsubscription in Dispose()

- [ ] **Add ConfigureAwait(false) in VirtualAudioChannel.cs**
  - Line 150: WaitForConnectionAsync
  - Line 154: SendWaveHeader
  - Line 173: WriteAsync
  - Line 177: Task.Delay
  - Line 213: WriteAsync

### MusicEngineEditor

- [ ] **Refactor MainWindow.xaml.cs (2,372 LOC)**
  - Extract menu handling to MenuViewModel
  - Extract toolbar logic to ToolbarViewModel
  - Move status bar to StatusBarViewModel
  - Keep only UI event handlers in code-behind

---

## P1 - Important (This Sprint)

### MusicEngine

- [ ] **Replace Thread.Sleep with Task.Delay**
  - `AudioEngine.cs:601` - Dispose method
  - `Sequencer.cs:280` - Run loop (use Timer instead)
  - `Sequencer.cs:503` - TriggerNote (use Task.Delay)

- [ ] **Split God Classes**
  - AudioEngine.cs (611 LOC) → AudioEngine + MidiRouter + AudioMixer
  - Sequencer.cs (551 LOC) → Sequencer + Pattern + NoteEvent
  - VstHost.cs (687 LOC) → VstHost + VstPlugin

- [ ] **Make VST Paths Configurable**
  - Settings.cs:33-37 - Move to user config file

### MusicEngineEditor

- [ ] **Implement Missing Dialogs**
  - [ ] NewProjectDialog (wire up existing)
  - [ ] ProjectSettingsDialog
  - [ ] AddScriptDialog
  - [ ] ImportAudioDialog
  - [ ] AddReferenceDialog
  - [ ] AboutDialog
  - [ ] Find/Replace integration

- [ ] **Complete Project Explorer Operations**
  - [ ] AddNewScript command (Line 138)
  - [ ] AddNewFolder command (Line 144)
  - [ ] DeleteNode command (Line 150)
  - [ ] RenameNode command (Line 156)

- [ ] **Update NuGet Packages**
  ```bash
  dotnet add package AvalonEdit --version 6.3.1.120
  dotnet add package Microsoft.Extensions.DependencyInjection --version 10.0.2
  dotnet add package Serilog --version 4.3.0
  # Review breaking changes before:
  dotnet add package Serilog.Sinks.File --version 7.0.0
  ```

- [ ] **Complete TODO Items in MainViewModel.cs**
  - Line 80: Show NewProjectDialog
  - Line 87: Show OpenFileDialog
  - Line 125: Show NewFileDialog
  - Line 152: Show Find dialog
  - Line 158: Show Replace dialog
  - Line 217: Check for unsaved changes
  - Line 243: Ask to save prompt

---

## P2 - Nice to Have (Backlog)

### MusicEngine

- [ ] **Complete VST DSP Processing**
  - Implement actual audio processing in VstPlugin
  - Add preset management
  - Add parameter automation

- [ ] **Add Effects System**
  - IEffect interface
  - ReverbEffect
  - DelayEffect
  - ChorusEffect
  - Effect chain routing

- [ ] **Add Recording Feature**
  - WaveFileWriter integration
  - Real-time audio capture
  - Export to WAV/MP3

- [ ] **Improve Timing Precision**
  - High-resolution timer for sequencer
  - MIDI clock sync
  - Audio-rate scheduling

### MusicEngineEditor

- [ ] **Add Debugger Support**
  - Breakpoint system
  - Step-through execution
  - Variable inspection
  - Call stack view

- [ ] **Implement Settings/Preferences**
  - Audio device selection
  - MIDI device configuration
  - Theme customization
  - Keyboard shortcut editor

- [ ] **Add Git Integration**
  - Repository status
  - Commit/push from IDE
  - Branch management

- [ ] **Visual Enhancements**
  - Waveform display
  - Piano roll editor
  - Audio preview player

---

## Testing Tasks

### MusicEngine.Tests (NEW PROJECT)

- [ ] Create xUnit test project
- [ ] AudioEngineTests
  - Initialize/Dispose lifecycle
  - MIDI routing
  - Sample provider management
- [ ] SequencerTests
  - Pattern playback
  - Beat accuracy
  - Event emission
- [ ] SimpleSynthTests
  - Waveform generation
  - Note on/off
  - Parameter changes

### MusicEngineEditor.Tests (NEW PROJECT)

- [ ] Create xUnit test project
- [ ] MainViewModelTests
  - Command execution
  - State management
- [ ] ProjectServiceTests
  - Create/Open/Save operations
  - File serialization
- [ ] ScriptExecutionServiceTests
  - Compilation
  - Error handling

---

## Documentation Tasks

- [ ] Update README.md with current features
- [ ] Create API.md for MusicEngine public API
- [ ] Create GettingStarted.md tutorial
- [ ] Add XML documentation to public methods
- [ ] Create example scripts folder

---

## Progress Tracking

| Phase | Status | Completion |
|-------|--------|------------|
| P0 - Critical | NOT STARTED | 0% |
| P1 - Important | NOT STARTED | 0% |
| P2 - Backlog | NOT STARTED | 0% |
| Testing | NOT STARTED | 0% |
| Documentation | PARTIAL | 20% |

**Last Updated:** 2026-01-19
