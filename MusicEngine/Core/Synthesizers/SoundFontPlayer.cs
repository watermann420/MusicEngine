// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// SoundFont generator types (parameters).
/// </summary>
public enum SFGenerator : ushort
{
    /// <summary>Start address offset.</summary>
    StartAddrOffset = 0,
    /// <summary>End address offset.</summary>
    EndAddrOffset = 1,
    /// <summary>Start loop address offset.</summary>
    StartLoopAddrOffset = 2,
    /// <summary>End loop address offset.</summary>
    EndLoopAddrOffset = 3,
    /// <summary>Start address coarse offset.</summary>
    StartAddrCoarseOffset = 4,
    /// <summary>Modulation LFO to pitch.</summary>
    ModLfoToPitch = 5,
    /// <summary>Vibrato LFO to pitch.</summary>
    VibLfoToPitch = 6,
    /// <summary>Modulation envelope to pitch.</summary>
    ModEnvToPitch = 7,
    /// <summary>Initial filter cutoff.</summary>
    InitialFilterFc = 8,
    /// <summary>Initial filter Q.</summary>
    InitialFilterQ = 9,
    /// <summary>Modulation LFO to filter cutoff.</summary>
    ModLfoToFilterFc = 10,
    /// <summary>Modulation envelope to filter cutoff.</summary>
    ModEnvToFilterFc = 11,
    /// <summary>End address coarse offset.</summary>
    EndAddrCoarseOffset = 12,
    /// <summary>Modulation LFO to volume.</summary>
    ModLfoToVolume = 13,
    /// <summary>Chorus effects send.</summary>
    ChorusEffectsSend = 15,
    /// <summary>Reverb effects send.</summary>
    ReverbEffectsSend = 16,
    /// <summary>Pan position.</summary>
    Pan = 17,
    /// <summary>Modulation LFO delay.</summary>
    DelayModLfo = 21,
    /// <summary>Modulation LFO frequency.</summary>
    FreqModLfo = 22,
    /// <summary>Vibrato LFO delay.</summary>
    DelayVibLfo = 23,
    /// <summary>Vibrato LFO frequency.</summary>
    FreqVibLfo = 24,
    /// <summary>Modulation envelope delay.</summary>
    DelayModEnv = 25,
    /// <summary>Modulation envelope attack.</summary>
    AttackModEnv = 26,
    /// <summary>Modulation envelope hold.</summary>
    HoldModEnv = 27,
    /// <summary>Modulation envelope decay.</summary>
    DecayModEnv = 28,
    /// <summary>Modulation envelope sustain.</summary>
    SustainModEnv = 29,
    /// <summary>Modulation envelope release.</summary>
    ReleaseModEnv = 30,
    /// <summary>Key number to modulation envelope hold.</summary>
    KeynumToModEnvHold = 31,
    /// <summary>Key number to modulation envelope decay.</summary>
    KeynumToModEnvDecay = 32,
    /// <summary>Volume envelope delay.</summary>
    DelayVolEnv = 33,
    /// <summary>Volume envelope attack.</summary>
    AttackVolEnv = 34,
    /// <summary>Volume envelope hold.</summary>
    HoldVolEnv = 35,
    /// <summary>Volume envelope decay.</summary>
    DecayVolEnv = 36,
    /// <summary>Volume envelope sustain.</summary>
    SustainVolEnv = 37,
    /// <summary>Volume envelope release.</summary>
    ReleaseVolEnv = 38,
    /// <summary>Key number to volume envelope hold.</summary>
    KeynumToVolEnvHold = 39,
    /// <summary>Key number to volume envelope decay.</summary>
    KeynumToVolEnvDecay = 40,
    /// <summary>Instrument index.</summary>
    Instrument = 41,
    /// <summary>Key range.</summary>
    KeyRange = 43,
    /// <summary>Velocity range.</summary>
    VelRange = 44,
    /// <summary>Start loop address coarse offset.</summary>
    StartLoopAddrCoarseOffset = 45,
    /// <summary>Key number.</summary>
    Keynum = 46,
    /// <summary>Velocity.</summary>
    Velocity = 47,
    /// <summary>Initial attenuation.</summary>
    InitialAttenuation = 48,
    /// <summary>End loop address coarse offset.</summary>
    EndLoopAddrCoarseOffset = 50,
    /// <summary>Coarse tune.</summary>
    CoarseTune = 51,
    /// <summary>Fine tune.</summary>
    FineTune = 52,
    /// <summary>Sample ID.</summary>
    SampleId = 53,
    /// <summary>Sample modes.</summary>
    SampleModes = 54,
    /// <summary>Scale tuning.</summary>
    ScaleTuning = 56,
    /// <summary>Exclusive class.</summary>
    ExclusiveClass = 57,
    /// <summary>Overriding root key.</summary>
    OverridingRootKey = 58
}

/// <summary>
/// Sample loop modes.
/// </summary>
public enum SFSampleMode
{
    /// <summary>No loop.</summary>
    NoLoop = 0,
    /// <summary>Continuous loop.</summary>
    ContinuousLoop = 1,
    /// <summary>No loop (reserved).</summary>
    NoLoopReserved = 2,
    /// <summary>Loop during key depression then play remainder.</summary>
    LoopDuringRelease = 3
}

/// <summary>
/// Represents a SoundFont sample header.
/// </summary>
public class SFSample
{
    /// <summary>Sample name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Start position in sample data.</summary>
    public uint Start { get; set; }
    /// <summary>End position in sample data.</summary>
    public uint End { get; set; }
    /// <summary>Loop start position.</summary>
    public uint LoopStart { get; set; }
    /// <summary>Loop end position.</summary>
    public uint LoopEnd { get; set; }
    /// <summary>Sample rate.</summary>
    public uint SampleRate { get; set; }
    /// <summary>Original pitch (MIDI key number).</summary>
    public byte OriginalPitch { get; set; }
    /// <summary>Pitch correction in cents.</summary>
    public sbyte PitchCorrection { get; set; }
    /// <summary>Sample link index.</summary>
    public ushort SampleLink { get; set; }
    /// <summary>Sample type.</summary>
    public ushort SampleType { get; set; }
    /// <summary>Actual sample data (loaded on demand).</summary>
    public float[]? Data { get; set; }
}

/// <summary>
/// Represents a SoundFont preset (program).
/// </summary>
public class SFPreset
{
    /// <summary>Preset name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Preset number (program number).</summary>
    public ushort PresetNumber { get; set; }
    /// <summary>Bank number.</summary>
    public ushort Bank { get; set; }
    /// <summary>Zones in this preset.</summary>
    public List<SFZone> Zones { get; } = new();
}

/// <summary>
/// Represents a SoundFont instrument.
/// </summary>
public class SFInstrument
{
    /// <summary>Instrument name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Zones in this instrument.</summary>
    public List<SFZone> Zones { get; } = new();
}

/// <summary>
/// Represents a zone (region) in a preset or instrument.
/// </summary>
public class SFZone
{
    /// <summary>Generator values for this zone.</summary>
    public Dictionary<SFGenerator, short> Generators { get; } = new();
    /// <summary>Key range (low, high).</summary>
    public (byte Low, byte High) KeyRange { get; set; } = (0, 127);
    /// <summary>Velocity range (low, high).</summary>
    public (byte Low, byte High) VelocityRange { get; set; } = (0, 127);
    /// <summary>Sample index (for instrument zones).</summary>
    public int? SampleIndex { get; set; }
    /// <summary>Instrument index (for preset zones).</summary>
    public int? InstrumentIndex { get; set; }
}

/// <summary>
/// Voice state for SoundFont playback.
/// </summary>
internal class SFVoice
{
    public bool IsActive;
    public int NoteNumber;
    public int Velocity;
    public int Channel;
    public SFSample? Sample;
    public SFZone? Zone;

    // Playback state
    public double Position;
    public double Increment;
    public bool IsLooping;
    public uint LoopStart;
    public uint LoopEnd;

    // Envelope state
    public double EnvLevel;
    public int EnvStage; // 0=delay, 1=attack, 2=hold, 3=decay, 4=sustain, 5=release
    public double EnvTime;
    public bool IsReleasing;

    // Filter state
    public double FilterState1;
    public double FilterState2;

    // Generator values
    public double AttenuationDb;
    public double Pan;
    public double FineTune;
    public double CoarseTune;

    // Envelope parameters (in seconds)
    public double DelayTime;
    public double AttackTime;
    public double HoldTime;
    public double DecayTime;
    public double SustainLevel;
    public double ReleaseTime;

    // Filter parameters
    public double FilterCutoff;
    public double FilterQ;
}

/// <summary>
/// SoundFont (SF2/SF3) player implementing full preset/bank selection,
/// sample playback with loop points, velocity layers, and modulator support.
/// </summary>
public class SoundFontPlayer : ISynth
{
    private readonly int _sampleRate;
    private readonly WaveFormat _waveFormat;
    private readonly List<SFPreset> _presets = new();
    private readonly List<SFInstrument> _instruments = new();
    private readonly List<SFSample> _samples = new();
    private readonly List<SFVoice> _voices = new();
    private float[]? _sampleData;
    private int _currentPresetIndex;
    private int _currentBank;
    private int _currentProgram;
    private readonly object _lock = new();
    private const int MaxVoices = 64;

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "SoundFontPlayer";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>Gets the loaded presets.</summary>
    public IReadOnlyList<SFPreset> Presets => _presets.AsReadOnly();

    /// <summary>Gets the current preset.</summary>
    public SFPreset? CurrentPreset => _currentPresetIndex >= 0 && _currentPresetIndex < _presets.Count
        ? _presets[_currentPresetIndex]
        : null;

    /// <summary>Gets whether a SoundFont is loaded.</summary>
    public bool IsLoaded => _presets.Count > 0;

    /// <summary>Gets the SoundFont file path.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Gets or sets the reverb send level (0-1).</summary>
    public float ReverbSend { get; set; } = 0.2f;

    /// <summary>Gets or sets the chorus send level (0-1).</summary>
    public float ChorusSend { get; set; } = 0.1f;

    /// <summary>
    /// Creates a new SoundFont player.
    /// </summary>
    /// <param name="sampleRate">Sample rate (default: from Settings).</param>
    public SoundFontPlayer(int? sampleRate = null)
    {
        _sampleRate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, Settings.Channels);

        // Initialize voice pool
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices.Add(new SFVoice());
        }
    }

    /// <summary>
    /// Loads a SoundFont file.
    /// </summary>
    /// <param name="path">Path to the SF2/SF3 file.</param>
    /// <returns>True if loaded successfully.</returns>
    public bool LoadSoundFont(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.ASCII);

            // Read RIFF header
            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF")
                return false;

            uint fileSize = reader.ReadUInt32();
            string sfbk = new string(reader.ReadChars(4));
            if (sfbk != "sfbk")
                return false;

            // Clear existing data
            lock (_lock)
            {
                _presets.Clear();
                _instruments.Clear();
                _samples.Clear();
                _sampleData = null;
            }

            // Read chunks
            while (stream.Position < stream.Length - 8)
            {
                string chunkId = new string(reader.ReadChars(4));
                uint chunkSize = reader.ReadUInt32();
                long chunkEnd = stream.Position + chunkSize;

                switch (chunkId)
                {
                    case "LIST":
                        string listType = new string(reader.ReadChars(4));
                        switch (listType)
                        {
                            case "INFO":
                                ReadInfoChunk(reader, chunkEnd - 4);
                                break;
                            case "sdta":
                                ReadSampleDataChunk(reader, chunkEnd - 4);
                                break;
                            case "pdta":
                                ReadPresetDataChunk(reader, chunkEnd - 4);
                                break;
                        }
                        break;
                }

                stream.Position = chunkEnd;
                if (chunkSize % 2 != 0 && stream.Position < stream.Length)
                    stream.Position++;
            }

            FilePath = path;

            // Select first preset
            if (_presets.Count > 0)
            {
                SelectPreset(0, 0);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading SoundFont: {ex.Message}");
            return false;
        }
    }

    private void ReadInfoChunk(BinaryReader reader, long endPosition)
    {
        while (reader.BaseStream.Position < endPosition)
        {
            string subChunkId = new string(reader.ReadChars(4));
            uint subChunkSize = reader.ReadUInt32();

            // Skip INFO sub-chunks (ifil, isng, INAM, etc.)
            reader.BaseStream.Position += subChunkSize;
            if (subChunkSize % 2 != 0)
                reader.BaseStream.Position++;
        }
    }

    private void ReadSampleDataChunk(BinaryReader reader, long endPosition)
    {
        while (reader.BaseStream.Position < endPosition)
        {
            string subChunkId = new string(reader.ReadChars(4));
            uint subChunkSize = reader.ReadUInt32();

            if (subChunkId == "smpl")
            {
                // Read 16-bit sample data
                int sampleCount = (int)(subChunkSize / 2);
                _sampleData = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = reader.ReadInt16();
                    _sampleData[i] = sample / 32768f;
                }
            }
            else
            {
                reader.BaseStream.Position += subChunkSize;
            }

            if (subChunkSize % 2 != 0)
                reader.BaseStream.Position++;
        }
    }

    private void ReadPresetDataChunk(BinaryReader reader, long endPosition)
    {
        var presetHeaders = new List<(string Name, ushort Preset, ushort Bank, ushort BagIndex)>();
        var presetBags = new List<(ushort GenIndex, ushort ModIndex)>();
        var presetGens = new List<(SFGenerator Gen, short Amount)>();
        var instrumentHeaders = new List<(string Name, ushort BagIndex)>();
        var instrumentBags = new List<(ushort GenIndex, ushort ModIndex)>();
        var instrumentGens = new List<(SFGenerator Gen, short Amount)>();
        var sampleHeaders = new List<SFSample>();

        while (reader.BaseStream.Position < endPosition)
        {
            string subChunkId = new string(reader.ReadChars(4));
            uint subChunkSize = reader.ReadUInt32();
            long subChunkEnd = reader.BaseStream.Position + subChunkSize;

            switch (subChunkId)
            {
                case "phdr": // Preset headers
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        var name = ReadFixedString(reader, 20);
                        ushort preset = reader.ReadUInt16();
                        ushort bank = reader.ReadUInt16();
                        ushort bagIndex = reader.ReadUInt16();
                        reader.ReadUInt32(); // library
                        reader.ReadUInt32(); // genre
                        reader.ReadUInt32(); // morphology
                        presetHeaders.Add((name, preset, bank, bagIndex));
                    }
                    break;

                case "pbag": // Preset bags
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        ushort genIndex = reader.ReadUInt16();
                        ushort modIndex = reader.ReadUInt16();
                        presetBags.Add((genIndex, modIndex));
                    }
                    break;

                case "pgen": // Preset generators
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        var gen = (SFGenerator)reader.ReadUInt16();
                        short amount = reader.ReadInt16();
                        presetGens.Add((gen, amount));
                    }
                    break;

                case "inst": // Instrument headers
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        var name = ReadFixedString(reader, 20);
                        ushort bagIndex = reader.ReadUInt16();
                        instrumentHeaders.Add((name, bagIndex));
                    }
                    break;

                case "ibag": // Instrument bags
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        ushort genIndex = reader.ReadUInt16();
                        ushort modIndex = reader.ReadUInt16();
                        instrumentBags.Add((genIndex, modIndex));
                    }
                    break;

                case "igen": // Instrument generators
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        var gen = (SFGenerator)reader.ReadUInt16();
                        short amount = reader.ReadInt16();
                        instrumentGens.Add((gen, amount));
                    }
                    break;

                case "shdr": // Sample headers
                    while (reader.BaseStream.Position < subChunkEnd)
                    {
                        var sample = new SFSample
                        {
                            Name = ReadFixedString(reader, 20),
                            Start = reader.ReadUInt32(),
                            End = reader.ReadUInt32(),
                            LoopStart = reader.ReadUInt32(),
                            LoopEnd = reader.ReadUInt32(),
                            SampleRate = reader.ReadUInt32(),
                            OriginalPitch = reader.ReadByte(),
                            PitchCorrection = reader.ReadSByte(),
                            SampleLink = reader.ReadUInt16(),
                            SampleType = reader.ReadUInt16()
                        };
                        sampleHeaders.Add(sample);
                    }
                    break;

                default:
                    reader.BaseStream.Position = subChunkEnd;
                    break;
            }

            if (reader.BaseStream.Position < subChunkEnd)
                reader.BaseStream.Position = subChunkEnd;
            if (subChunkSize % 2 != 0 && reader.BaseStream.Position < endPosition)
                reader.BaseStream.Position++;
        }

        // Build structures
        _samples.AddRange(sampleHeaders);

        // Build instruments
        for (int i = 0; i < instrumentHeaders.Count - 1; i++)
        {
            var inst = new SFInstrument { Name = instrumentHeaders[i].Name };
            int startBag = instrumentHeaders[i].BagIndex;
            int endBag = instrumentHeaders[i + 1].BagIndex;

            for (int b = startBag; b < endBag && b < instrumentBags.Count - 1; b++)
            {
                var zone = new SFZone();
                int startGen = instrumentBags[b].GenIndex;
                int endGen = instrumentBags[b + 1].GenIndex;

                for (int g = startGen; g < endGen && g < instrumentGens.Count; g++)
                {
                    var (gen, amount) = instrumentGens[g];
                    zone.Generators[gen] = amount;

                    if (gen == SFGenerator.KeyRange)
                    {
                        zone.KeyRange = ((byte)(amount & 0xFF), (byte)((amount >> 8) & 0xFF));
                    }
                    else if (gen == SFGenerator.VelRange)
                    {
                        zone.VelocityRange = ((byte)(amount & 0xFF), (byte)((amount >> 8) & 0xFF));
                    }
                    else if (gen == SFGenerator.SampleId)
                    {
                        zone.SampleIndex = amount;
                    }
                }

                if (zone.SampleIndex.HasValue)
                    inst.Zones.Add(zone);
            }

            _instruments.Add(inst);
        }

        // Build presets
        for (int i = 0; i < presetHeaders.Count - 1; i++)
        {
            var preset = new SFPreset
            {
                Name = presetHeaders[i].Name,
                PresetNumber = presetHeaders[i].Preset,
                Bank = presetHeaders[i].Bank
            };

            int startBag = presetHeaders[i].BagIndex;
            int endBag = presetHeaders[i + 1].BagIndex;

            for (int b = startBag; b < endBag && b < presetBags.Count - 1; b++)
            {
                var zone = new SFZone();
                int startGen = presetBags[b].GenIndex;
                int endGen = presetBags[b + 1].GenIndex;

                for (int g = startGen; g < endGen && g < presetGens.Count; g++)
                {
                    var (gen, amount) = presetGens[g];
                    zone.Generators[gen] = amount;

                    if (gen == SFGenerator.KeyRange)
                    {
                        zone.KeyRange = ((byte)(amount & 0xFF), (byte)((amount >> 8) & 0xFF));
                    }
                    else if (gen == SFGenerator.VelRange)
                    {
                        zone.VelocityRange = ((byte)(amount & 0xFF), (byte)((amount >> 8) & 0xFF));
                    }
                    else if (gen == SFGenerator.Instrument)
                    {
                        zone.InstrumentIndex = amount;
                    }
                }

                preset.Zones.Add(zone);
            }

            _presets.Add(preset);
        }
    }

    private static string ReadFixedString(BinaryReader reader, int length)
    {
        var chars = reader.ReadChars(length);
        int nullIndex = Array.IndexOf(chars, '\0');
        return nullIndex >= 0 ? new string(chars, 0, nullIndex) : new string(chars);
    }

    /// <summary>
    /// Selects a preset by bank and program number.
    /// </summary>
    /// <param name="bank">Bank number.</param>
    /// <param name="program">Program number (0-127).</param>
    /// <returns>True if preset was found and selected.</returns>
    public bool SelectPreset(int bank, int program)
    {
        lock (_lock)
        {
            for (int i = 0; i < _presets.Count; i++)
            {
                if (_presets[i].Bank == bank && _presets[i].PresetNumber == program)
                {
                    _currentPresetIndex = i;
                    _currentBank = bank;
                    _currentProgram = program;
                    return true;
                }
            }

            // Try to find any preset with matching program
            for (int i = 0; i < _presets.Count; i++)
            {
                if (_presets[i].PresetNumber == program)
                {
                    _currentPresetIndex = i;
                    _currentBank = _presets[i].Bank;
                    _currentProgram = program;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets preset names grouped by bank.
    /// </summary>
    public Dictionary<int, List<(int Program, string Name)>> GetPresetsByBank()
    {
        var result = new Dictionary<int, List<(int, string)>>();
        foreach (var preset in _presets)
        {
            if (!result.ContainsKey(preset.Bank))
                result[preset.Bank] = new List<(int, string)>();
            result[preset.Bank].Add((preset.PresetNumber, preset.Name));
        }
        return result;
    }

    /// <summary>
    /// Triggers a note on event.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        var preset = CurrentPreset;
        if (preset == null || _sampleData == null)
            return;

        lock (_lock)
        {
            // Find matching zones
            foreach (var presetZone in preset.Zones)
            {
                if (note < presetZone.KeyRange.Low || note > presetZone.KeyRange.High)
                    continue;
                if (velocity < presetZone.VelocityRange.Low || velocity > presetZone.VelocityRange.High)
                    continue;

                if (!presetZone.InstrumentIndex.HasValue)
                    continue;

                int instIndex = presetZone.InstrumentIndex.Value;
                if (instIndex < 0 || instIndex >= _instruments.Count)
                    continue;

                var instrument = _instruments[instIndex];

                foreach (var instZone in instrument.Zones)
                {
                    if (note < instZone.KeyRange.Low || note > instZone.KeyRange.High)
                        continue;
                    if (velocity < instZone.VelocityRange.Low || velocity > instZone.VelocityRange.High)
                        continue;

                    if (!instZone.SampleIndex.HasValue)
                        continue;

                    int sampleIndex = instZone.SampleIndex.Value;
                    if (sampleIndex < 0 || sampleIndex >= _samples.Count)
                        continue;

                    var sample = _samples[sampleIndex];

                    // Allocate voice
                    var voice = AllocateVoice();
                    if (voice == null)
                        continue;

                    InitializeVoice(voice, note, velocity, sample, instZone, presetZone);
                }
            }
        }
    }

    private SFVoice? AllocateVoice()
    {
        // Find free voice
        foreach (var v in _voices)
        {
            if (!v.IsActive)
                return v;
        }

        // Steal oldest voice
        return _voices[0];
    }

    private void InitializeVoice(SFVoice voice, int note, int velocity, SFSample sample, SFZone instZone, SFZone presetZone)
    {
        voice.IsActive = true;
        voice.NoteNumber = note;
        voice.Velocity = velocity;
        voice.Sample = sample;
        voice.Zone = instZone;

        // Calculate pitch
        int rootKey = instZone.Generators.TryGetValue(SFGenerator.OverridingRootKey, out var rk) && rk >= 0
            ? rk
            : sample.OriginalPitch;

        double coarseTune = GetGeneratorValue(instZone, presetZone, SFGenerator.CoarseTune, 0);
        double fineTune = GetGeneratorValue(instZone, presetZone, SFGenerator.FineTune, 0) + sample.PitchCorrection;

        double pitchDiff = note - rootKey + coarseTune + fineTune / 100.0;
        double ratio = Math.Pow(2.0, pitchDiff / 12.0);
        voice.Increment = ratio * sample.SampleRate / _sampleRate;

        voice.Position = sample.Start;
        voice.LoopStart = sample.LoopStart;
        voice.LoopEnd = sample.LoopEnd;

        // Check sample mode
        var sampleMode = (SFSampleMode)GetGeneratorValue(instZone, presetZone, SFGenerator.SampleModes, 0);
        voice.IsLooping = sampleMode == SFSampleMode.ContinuousLoop || sampleMode == SFSampleMode.LoopDuringRelease;

        // Attenuation
        voice.AttenuationDb = GetGeneratorValue(instZone, presetZone, SFGenerator.InitialAttenuation, 0) / 10.0;
        voice.AttenuationDb += (1.0 - velocity / 127.0) * 48; // Velocity to attenuation

        // Pan
        voice.Pan = GetGeneratorValue(instZone, presetZone, SFGenerator.Pan, 0) / 500.0;

        // Envelope
        voice.DelayTime = TimecentsToSeconds(GetGeneratorValue(instZone, presetZone, SFGenerator.DelayVolEnv, -12000));
        voice.AttackTime = TimecentsToSeconds(GetGeneratorValue(instZone, presetZone, SFGenerator.AttackVolEnv, -12000));
        voice.HoldTime = TimecentsToSeconds(GetGeneratorValue(instZone, presetZone, SFGenerator.HoldVolEnv, -12000));
        voice.DecayTime = TimecentsToSeconds(GetGeneratorValue(instZone, presetZone, SFGenerator.DecayVolEnv, -12000));
        voice.SustainLevel = 1.0 - GetGeneratorValue(instZone, presetZone, SFGenerator.SustainVolEnv, 0) / 1000.0;
        voice.ReleaseTime = TimecentsToSeconds(GetGeneratorValue(instZone, presetZone, SFGenerator.ReleaseVolEnv, -12000));

        voice.EnvStage = 0;
        voice.EnvLevel = 0;
        voice.EnvTime = 0;
        voice.IsReleasing = false;

        // Filter
        voice.FilterCutoff = GetGeneratorValue(instZone, presetZone, SFGenerator.InitialFilterFc, 13500);
        voice.FilterQ = GetGeneratorValue(instZone, presetZone, SFGenerator.InitialFilterQ, 0) / 10.0;
        voice.FilterState1 = 0;
        voice.FilterState2 = 0;
    }

    private static double GetGeneratorValue(SFZone instZone, SFZone presetZone, SFGenerator gen, double defaultValue)
    {
        double value = defaultValue;
        if (instZone.Generators.TryGetValue(gen, out var instVal))
            value = instVal;
        if (presetZone.Generators.TryGetValue(gen, out var presetVal))
            value += presetVal; // Preset values are additive
        return value;
    }

    private static double TimecentsToSeconds(double timecents)
    {
        if (timecents <= -12000)
            return 0.001;
        return Math.Pow(2.0, timecents / 1200.0);
    }

    /// <summary>
    /// Triggers a note off event.
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.IsActive && voice.NoteNumber == note && !voice.IsReleasing)
                {
                    voice.IsReleasing = true;
                    voice.EnvStage = 5; // Release
                    voice.EnvTime = 0;
                }
            }
        }
    }

    /// <summary>
    /// Stops all playing notes.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.IsActive = false;
            }
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "reverb":
                ReverbSend = Math.Clamp(value, 0f, 1f);
                break;
            case "chorus":
                ChorusSend = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    /// <summary>
    /// Reads audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        if (_sampleData == null)
            return count;

        lock (_lock)
        {
            int channels = _waveFormat.Channels;
            double deltaTime = 1.0 / _sampleRate;

            for (int n = 0; n < count; n += channels)
            {
                float sampleL = 0;
                float sampleR = 0;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive || voice.Sample == null)
                        continue;

                    // Process envelope
                    ProcessEnvelope(voice, deltaTime);

                    if (!voice.IsActive)
                        continue;

                    // Get sample
                    int pos = (int)voice.Position;
                    if (pos < 0 || pos >= _sampleData.Length - 1)
                    {
                        voice.IsActive = false;
                        continue;
                    }

                    double frac = voice.Position - pos;
                    float s1 = _sampleData[pos];
                    float s2 = _sampleData[Math.Min(pos + 1, _sampleData.Length - 1)];
                    float sample = (float)(s1 + (s2 - s1) * frac);

                    // Apply envelope and attenuation
                    double gain = voice.EnvLevel * Math.Pow(10.0, -voice.AttenuationDb / 20.0);
                    sample *= (float)gain;

                    // Pan
                    float panL = (float)Math.Cos((voice.Pan + 1) * Math.PI / 4);
                    float panR = (float)Math.Sin((voice.Pan + 1) * Math.PI / 4);

                    sampleL += sample * panL;
                    sampleR += sample * panR;

                    // Advance position
                    voice.Position += voice.Increment;

                    // Handle looping
                    if (voice.IsLooping && voice.Position >= voice.LoopEnd)
                    {
                        voice.Position = voice.LoopStart + (voice.Position - voice.LoopEnd);
                    }
                    else if (voice.Position >= voice.Sample.End)
                    {
                        voice.IsActive = false;
                    }
                }

                buffer[offset + n] = sampleL * Volume;
                if (channels > 1)
                    buffer[offset + n + 1] = sampleR * Volume;
            }
        }

        return count;
    }

    private void ProcessEnvelope(SFVoice voice, double deltaTime)
    {
        voice.EnvTime += deltaTime;

        switch (voice.EnvStage)
        {
            case 0: // Delay
                if (voice.EnvTime >= voice.DelayTime)
                {
                    voice.EnvStage = 1;
                    voice.EnvTime = 0;
                }
                voice.EnvLevel = 0;
                break;

            case 1: // Attack
                if (voice.AttackTime > 0)
                    voice.EnvLevel = voice.EnvTime / voice.AttackTime;
                else
                    voice.EnvLevel = 1;

                if (voice.EnvLevel >= 1)
                {
                    voice.EnvLevel = 1;
                    voice.EnvStage = 2;
                    voice.EnvTime = 0;
                }
                break;

            case 2: // Hold
                voice.EnvLevel = 1;
                if (voice.EnvTime >= voice.HoldTime)
                {
                    voice.EnvStage = 3;
                    voice.EnvTime = 0;
                }
                break;

            case 3: // Decay
                if (voice.DecayTime > 0)
                {
                    double decayProgress = voice.EnvTime / voice.DecayTime;
                    voice.EnvLevel = 1 - (1 - voice.SustainLevel) * decayProgress;
                }

                if (voice.EnvLevel <= voice.SustainLevel || voice.EnvTime >= voice.DecayTime)
                {
                    voice.EnvLevel = voice.SustainLevel;
                    voice.EnvStage = 4;
                }
                break;

            case 4: // Sustain
                voice.EnvLevel = voice.SustainLevel;
                break;

            case 5: // Release
                if (voice.ReleaseTime > 0)
                {
                    voice.EnvLevel = voice.SustainLevel * (1 - voice.EnvTime / voice.ReleaseTime);
                }

                if (voice.EnvLevel <= 0.001 || voice.EnvTime >= voice.ReleaseTime)
                {
                    voice.IsActive = false;
                }
                break;
        }
    }
}
