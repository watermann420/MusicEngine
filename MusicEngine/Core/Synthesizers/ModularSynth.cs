namespace MusicEngine.Core.Synthesizers;

using NAudio.Wave;
using MusicEngine.Core.Synthesizers.Modular;
using System.Drawing;

/// <summary>
/// Modular synthesizer that allows connecting various modules together.
/// Provides a flexible patching system similar to hardware modular synthesizers.
/// </summary>
public class ModularSynth : ISynth, ISampleProvider, IDisposable
{
    private readonly List<ModuleBase> _modules = new();
    private readonly List<Cable> _cables = new();
    private readonly object _lock = new();
    private readonly int _sampleRate;
    private readonly int _bufferSize;
    private bool _disposed;

    // Default modules for basic playback
    private VCOModule? _mainVco;
    private ADSRModule? _mainAdsr;
    private VCAModule? _mainVca;
    private OutputModule? _outputModule;

    // Processing order (topologically sorted)
    private List<ModuleBase>? _processingOrder;
    private bool _processingOrderDirty = true;

    // Note tracking
    private readonly Dictionary<int, float> _activeNotes = new();
    private int _currentNote = -1;
    private float _currentVelocity;

    public WaveFormat WaveFormat { get; }
    public string Name { get; set; } = "ModularSynth";

    /// <summary>
    /// Gets the list of modules in the synth.
    /// </summary>
    public IReadOnlyList<ModuleBase> Modules
    {
        get
        {
            lock (_lock)
            {
                return _modules.ToList();
            }
        }
    }

    /// <summary>
    /// Gets the list of cables connecting modules.
    /// </summary>
    public IReadOnlyList<Cable> Cables
    {
        get
        {
            lock (_lock)
            {
                return _cables.ToList();
            }
        }
    }

    public ModularSynth(int sampleRate = 44100, int bufferSize = 1024)
    {
        _sampleRate = sampleRate;
        _bufferSize = bufferSize;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);

        // Create default patch for basic playability
        CreateDefaultPatch();
    }

    private void CreateDefaultPatch()
    {
        // Create basic modules
        _mainVco = AddModule<VCOModule>();
        _mainAdsr = AddModule<ADSRModule>();
        _mainVca = AddModule<VCAModule>();
        _outputModule = AddModule<OutputModule>();

        // Create basic patch: VCO -> VCA -> Output, with ADSR controlling VCA
        var vcoSaw = _mainVco.GetOutput("Saw");
        var vcaIn = _mainVca.GetInput("Audio In");
        var vcaCv = _mainVca.GetInput("CV");
        var adsrOut = _mainAdsr.GetOutput("Envelope Out");
        var vcaOut = _mainVca.GetOutput("Audio Out");
        var outputMono = _outputModule.GetInput("Mono");

        if (vcoSaw != null && vcaIn != null)
            Connect(vcoSaw, vcaIn);

        if (adsrOut != null && vcaCv != null)
            Connect(adsrOut, vcaCv);

        if (vcaOut != null && outputMono != null)
            Connect(vcaOut, outputMono);
    }

    /// <summary>
    /// Adds a new module to the synth.
    /// </summary>
    public T AddModule<T>() where T : ModuleBase
    {
        lock (_lock)
        {
            T module = (T)Activator.CreateInstance(typeof(T), _sampleRate, _bufferSize)!;
            _modules.Add(module);
            _processingOrderDirty = true;
            return module;
        }
    }

    /// <summary>
    /// Adds an existing module instance to the synth.
    /// </summary>
    public void AddModule(ModuleBase module)
    {
        lock (_lock)
        {
            if (!_modules.Contains(module))
            {
                _modules.Add(module);
                _processingOrderDirty = true;
            }
        }
    }

    /// <summary>
    /// Removes a module from the synth.
    /// </summary>
    public void RemoveModule(ModuleBase module)
    {
        lock (_lock)
        {
            // Remove all cables connected to this module
            var cablesToRemove = _cables.Where(c =>
                c.Source.Owner == module || c.Destination.Owner == module).ToList();

            foreach (var cable in cablesToRemove)
            {
                DisconnectInternal(cable);
            }

            _modules.Remove(module);
            _processingOrderDirty = true;
            module.Dispose();
        }
    }

    /// <summary>
    /// Connects an output port to an input port.
    /// </summary>
    public Cable Connect(ModulePort output, ModulePort input)
    {
        if (output.Direction != PortDirection.Output)
            throw new ArgumentException("Source must be an output port", nameof(output));

        if (input.Direction != PortDirection.Input)
            throw new ArgumentException("Destination must be an input port", nameof(input));

        lock (_lock)
        {
            // Disconnect any existing connection to this input
            var existingCable = _cables.FirstOrDefault(c => c.Destination == input);
            if (existingCable != null)
            {
                DisconnectInternal(existingCable);
            }

            // Create new cable
            var cable = new Cable(output, input);
            _cables.Add(cable);

            // Update port connections
            input.ConnectedTo = output;

            _processingOrderDirty = true;
            return cable;
        }
    }

    /// <summary>
    /// Disconnects a cable.
    /// </summary>
    public void Disconnect(Cable cable)
    {
        lock (_lock)
        {
            DisconnectInternal(cable);
        }
    }

    private void DisconnectInternal(Cable cable)
    {
        cable.Destination.ConnectedTo = null;
        _cables.Remove(cable);
        _processingOrderDirty = true;
    }

    /// <summary>
    /// Triggers a note on.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            _activeNotes[note] = velocity / 127f;
            _currentNote = note;
            _currentVelocity = velocity / 127f;

            // Calculate V/Oct CV (0V = C4/MIDI note 60)
            float vOct = (note - 60) / 12f;

            // Update VCO frequency based on note
            if (_mainVco != null)
            {
                // Set base frequency to A4 (440 Hz) and use V/Oct for pitch
                _mainVco.SetParameter("Frequency", 440f);

                // Calculate actual frequency from note
                float freq = 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);
                _mainVco.SetParameter("Frequency", freq);
            }

            // Trigger ADSR
            _mainAdsr?.Trigger();
        }
    }

    /// <summary>
    /// Triggers a note off.
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            _activeNotes.Remove(note);

            if (note == _currentNote)
            {
                if (_activeNotes.Count > 0)
                {
                    // Play the most recent held note
                    var lastNote = _activeNotes.Last();
                    _currentNote = lastNote.Key;
                    _currentVelocity = lastNote.Value;

                    float freq = 440f * (float)Math.Pow(2.0, (_currentNote - 69) / 12.0);
                    _mainVco?.SetParameter("Frequency", freq);
                }
                else
                {
                    _currentNote = -1;
                    _mainAdsr?.ReleaseEnvelope();
                }
            }
        }
    }

    /// <summary>
    /// Stops all notes.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            _activeNotes.Clear();
            _currentNote = -1;
            _mainAdsr?.ReleaseEnvelope();

            foreach (var module in _modules)
            {
                if (module is ADSRModule adsr)
                {
                    adsr.ReleaseEnvelope();
                }
            }
        }
    }

    /// <summary>
    /// Sets a parameter on the synth or a specific module.
    /// Format: "ModuleName.ParameterName" or just "ParameterName" for global params.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        lock (_lock)
        {
            if (name.Contains('.'))
            {
                var parts = name.Split('.', 2);
                var moduleName = parts[0];
                var paramName = parts[1];

                var module = _modules.FirstOrDefault(m => m.Name == moduleName);
                module?.SetParameter(paramName, value);
            }
            else
            {
                // Set on all modules that have this parameter
                foreach (var module in _modules)
                {
                    if (module.Parameters.ContainsKey(name))
                    {
                        module.SetParameter(name, value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets a module by name.
    /// </summary>
    public ModuleBase? GetModule(string name)
    {
        lock (_lock)
        {
            return _modules.FirstOrDefault(m => m.Name == name);
        }
    }

    /// <summary>
    /// Gets all modules of a specific type.
    /// </summary>
    public IEnumerable<T> GetModules<T>() where T : ModuleBase
    {
        lock (_lock)
        {
            return _modules.OfType<T>().ToList();
        }
    }

    /// <summary>
    /// Reads audio samples from the synth.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samples = count / 2;  // Stereo samples

        lock (_lock)
        {
            // Ensure processing order is up to date
            if (_processingOrderDirty)
            {
                UpdateProcessingOrder();
            }

            // Process all modules in order
            if (_processingOrder != null)
            {
                foreach (var module in _processingOrder)
                {
                    module.Process(samples);
                }
            }

            // Get output from output module
            if (_outputModule != null)
            {
                _outputModule.CopyToInterleavedBuffer(buffer, offset, samples);
            }
            else
            {
                // No output module - silence
                Array.Clear(buffer, offset, count);
            }
        }

        return count;
    }

    /// <summary>
    /// Updates the processing order based on module connections.
    /// Uses topological sort to ensure modules are processed in the correct order.
    /// </summary>
    private void UpdateProcessingOrder()
    {
        _processingOrder = new List<ModuleBase>();
        var visited = new HashSet<ModuleBase>();
        var visiting = new HashSet<ModuleBase>();

        void Visit(ModuleBase module)
        {
            if (visited.Contains(module)) return;
            if (visiting.Contains(module))
            {
                // Cycle detected - skip to prevent infinite loop
                return;
            }

            visiting.Add(module);

            // Visit all modules that provide input to this module
            foreach (var input in module.Inputs)
            {
                if (input.ConnectedTo != null)
                {
                    Visit(input.ConnectedTo.Owner);
                }
            }

            visiting.Remove(module);
            visited.Add(module);
            _processingOrder.Add(module);
        }

        foreach (var module in _modules)
        {
            Visit(module);
        }

        _processingOrderDirty = false;
    }

    /// <summary>
    /// Clears all modules and cables.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var module in _modules)
            {
                module.Dispose();
            }

            _modules.Clear();
            _cables.Clear();
            _processingOrder = null;
            _processingOrderDirty = true;

            _mainVco = null;
            _mainAdsr = null;
            _mainVca = null;
            _outputModule = null;
        }
    }

    /// <summary>
    /// Resets all modules to their initial state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            foreach (var module in _modules)
            {
                module.Reset();
            }

            _activeNotes.Clear();
            _currentNote = -1;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a cable connection between two module ports.
/// </summary>
public class Cable
{
    private static readonly Color[] DefaultColors = new[]
    {
        Color.Red,
        Color.Blue,
        Color.Green,
        Color.Yellow,
        Color.Orange,
        Color.Purple,
        Color.Cyan,
        Color.Magenta
    };

    private static int _colorIndex;

    public Guid Id { get; } = Guid.NewGuid();
    public ModulePort Source { get; }
    public ModulePort Destination { get; }
    public Color Color { get; set; }

    public Cable(ModulePort source, ModulePort destination)
    {
        Source = source;
        Destination = destination;
        Color = DefaultColors[_colorIndex++ % DefaultColors.Length];
    }

    public Cable(ModulePort source, ModulePort destination, Color color)
    {
        Source = source;
        Destination = destination;
        Color = color;
    }
}
