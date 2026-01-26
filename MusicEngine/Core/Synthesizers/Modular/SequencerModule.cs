namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Step Sequencer module.
/// Generates CV and gate sequences triggered by clock input.
/// Supports 1-16 steps with individual pitch values, gate lengths, and step probability.
/// </summary>
public class SequencerModule : ModuleBase
{
    private const int MaxSteps = 16;
    private int _currentStep;
    private bool _lastClock;
    private bool _lastReset;
    private int _gateCounter;
    private bool _gateActive;
    private readonly float[] _stepValues;
    private readonly float[] _stepGateLengths;
    private readonly bool[] _stepEnabled;
    private readonly Random _random;

    // Inputs
    private readonly ModulePort _clockInput;
    private readonly ModulePort _resetInput;
    private readonly ModulePort _directionInput;

    // Outputs
    private readonly ModulePort _cvOutput;
    private readonly ModulePort _gateOutput;
    private readonly ModulePort _triggerOutput;
    private readonly ModulePort _stepOutput;  // Current step as CV (0-1)

    private int _direction = 1;  // 1 = forward, -1 = backward
    private bool _pingPongForward = true;

    public SequencerModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Sequencer", sampleRate, bufferSize)
    {
        _stepValues = new float[MaxSteps];
        _stepGateLengths = new float[MaxSteps];
        _stepEnabled = new bool[MaxSteps];
        _random = new Random();

        // Initialize default step values (C major scale over 2 octaves)
        float[] defaultScale = { 0f, 0.167f, 0.333f, 0.417f, 0.5f, 0.583f, 0.75f, 0.833f,
                                 1f, 1.167f, 1.333f, 1.417f, 1.5f, 1.583f, 1.75f, 1.833f };
        for (int i = 0; i < MaxSteps; i++)
        {
            _stepValues[i] = defaultScale[i] / 2f;  // Normalize to 0-1 range
            _stepGateLengths[i] = 0.5f;
            _stepEnabled[i] = true;
        }

        // Inputs
        _clockInput = AddInput("Clock", PortType.Trigger);
        _resetInput = AddInput("Reset", PortType.Trigger);
        _directionInput = AddInput("Direction", PortType.Control);

        // Outputs
        _cvOutput = AddOutput("CV Out", PortType.Control);
        _gateOutput = AddOutput("Gate Out", PortType.Gate);
        _triggerOutput = AddOutput("Trigger", PortType.Trigger);
        _stepOutput = AddOutput("Step CV", PortType.Control);

        // Parameters
        RegisterParameter("Steps", 8f, 1f, 16f);
        RegisterParameter("GateLength", 0.5f, 0.01f, 1f);  // Relative to clock period
        RegisterParameter("Mode", 0f, 0f, 3f);  // 0=Forward, 1=Backward, 2=PingPong, 3=Random
        RegisterParameter("Transpose", 0f, -2f, 2f);  // Octave transpose
        RegisterParameter("Glide", 0f, 0f, 1f);  // Portamento amount

        // Register step parameters
        for (int i = 0; i < MaxSteps; i++)
        {
            RegisterParameter($"Step{i + 1}Value", _stepValues[i], 0f, 1f);
            RegisterParameter($"Step{i + 1}Gate", 0.5f, 0f, 1f);
            RegisterParameter($"Step{i + 1}Enabled", 1f, 0f, 1f);
        }
    }

    public override void Process(int sampleCount)
    {
        int steps = (int)GetParameter("Steps");
        float gateLength = GetParameter("GateLength");
        int mode = (int)GetParameter("Mode");
        float transpose = GetParameter("Transpose");
        float glide = GetParameter("Glide");

        // Read step values from parameters
        for (int i = 0; i < MaxSteps; i++)
        {
            _stepValues[i] = GetParameter($"Step{i + 1}Value");
            _stepGateLengths[i] = GetParameter($"Step{i + 1}Gate");
            _stepEnabled[i] = GetParameter($"Step{i + 1}Enabled") > 0.5f;
        }

        // Calculate samples per gate based on expected clock period
        int gateSamples = (int)(SampleRate * 0.1 * gateLength);  // Assume 100ms per step

        for (int i = 0; i < sampleCount; i++)
        {
            float clock = _clockInput.GetValue(i);
            float reset = _resetInput.GetValue(i);
            float directionCv = _directionInput.GetValue(i);

            bool clockRising = clock > 0.5f && !_lastClock;
            bool resetRising = reset > 0.5f && !_lastReset;
            float trigger = 0f;

            _lastClock = clock > 0.5f;
            _lastReset = reset > 0.5f;

            // Handle reset
            if (resetRising)
            {
                _currentStep = 0;
                _direction = 1;
                _pingPongForward = true;
                _gateActive = false;
                _gateCounter = 0;
            }

            // Handle clock
            if (clockRising)
            {
                // Advance step based on mode
                AdvanceStep(steps, mode);

                // Skip disabled steps
                int attempts = 0;
                while (!_stepEnabled[_currentStep] && attempts < steps)
                {
                    AdvanceStep(steps, mode);
                    attempts++;
                }

                // Start gate
                _gateActive = true;
                _gateCounter = (int)(gateSamples * _stepGateLengths[_currentStep]);
                trigger = 1f;
            }

            // Handle gate timing
            if (_gateActive)
            {
                _gateCounter--;
                if (_gateCounter <= 0)
                {
                    _gateActive = false;
                }
            }

            // Get current step value with transpose
            float cvValue = _stepValues[_currentStep] + transpose;

            // Output
            _cvOutput.SetValue(i, cvValue);
            _gateOutput.SetValue(i, _gateActive ? 1f : 0f);
            _triggerOutput.SetValue(i, trigger);
            _stepOutput.SetValue(i, (float)_currentStep / steps);
        }
    }

    private void AdvanceStep(int steps, int mode)
    {
        switch (mode)
        {
            case 0: // Forward
                _currentStep = (_currentStep + 1) % steps;
                break;

            case 1: // Backward
                _currentStep = (_currentStep - 1 + steps) % steps;
                break;

            case 2: // PingPong
                if (_pingPongForward)
                {
                    _currentStep++;
                    if (_currentStep >= steps - 1)
                    {
                        _pingPongForward = false;
                    }
                }
                else
                {
                    _currentStep--;
                    if (_currentStep <= 0)
                    {
                        _pingPongForward = true;
                    }
                }
                _currentStep = Math.Clamp(_currentStep, 0, steps - 1);
                break;

            case 3: // Random
                _currentStep = _random.Next(steps);
                break;
        }
    }

    /// <summary>
    /// Sets the CV value for a specific step (0-15).
    /// </summary>
    public void SetStepValue(int step, float value)
    {
        if (step >= 0 && step < MaxSteps)
        {
            SetParameter($"Step{step + 1}Value", value);
        }
    }

    /// <summary>
    /// Gets the CV value for a specific step (0-15).
    /// </summary>
    public float GetStepValue(int step)
    {
        if (step >= 0 && step < MaxSteps)
        {
            return GetParameter($"Step{step + 1}Value");
        }
        return 0f;
    }

    /// <summary>
    /// Sets whether a step is enabled.
    /// </summary>
    public void SetStepEnabled(int step, bool enabled)
    {
        if (step >= 0 && step < MaxSteps)
        {
            SetParameter($"Step{step + 1}Enabled", enabled ? 1f : 0f);
        }
    }

    /// <summary>
    /// Gets the current step index.
    /// </summary>
    public int CurrentStep => _currentStep;

    /// <summary>
    /// Gets whether the gate is currently active.
    /// </summary>
    public bool GateActive => _gateActive;

    public override void Reset()
    {
        base.Reset();
        _currentStep = 0;
        _lastClock = false;
        _lastReset = false;
        _gateCounter = 0;
        _gateActive = false;
        _direction = 1;
        _pingPongForward = true;
    }
}
