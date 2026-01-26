namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Sample and Hold module.
/// Samples the input signal on trigger and holds the value until the next trigger.
/// Includes slew limiting for smooth transitions.
/// </summary>
public class SampleAndHoldModule : ModuleBase
{
    private float _heldValue;
    private float _currentOutput;
    private bool _lastTrigger;
    private bool _lastGate;

    // Inputs
    private readonly ModulePort _signalInput;
    private readonly ModulePort _triggerInput;
    private readonly ModulePort _gateInput;

    // Outputs
    private readonly ModulePort _output;
    private readonly ModulePort _triggerOutput;
    private readonly ModulePort _invertedOutput;

    public SampleAndHoldModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Sample & Hold", sampleRate, bufferSize)
    {
        // Inputs
        _signalInput = AddInput("Signal", PortType.Control);
        _triggerInput = AddInput("Trigger", PortType.Trigger);
        _gateInput = AddInput("Gate", PortType.Gate);  // Alternative: track & hold mode

        // Outputs
        _output = AddOutput("Out", PortType.Control);
        _triggerOutput = AddOutput("Trigger", PortType.Trigger);
        _invertedOutput = AddOutput("Inverted", PortType.Control);

        // Parameters
        RegisterParameter("Slew", 0f, 0f, 1f);           // Slew rate limiting
        RegisterParameter("Mode", 0f, 0f, 1f);           // 0=S&H, 1=Track & Hold
        RegisterParameter("Noise", 0f, 0f, 0.1f);        // Add noise to held value
        RegisterParameter("Quantize", 0f, 0f, 12f);      // Quantize steps (0=off)
    }

    public override void Process(int sampleCount)
    {
        float slew = GetParameter("Slew");
        float mode = GetParameter("Mode");
        float noise = GetParameter("Noise");
        float quantize = GetParameter("Quantize");

        float slewCoeff = slew > 0 ? 1f - (float)Math.Exp(-1.0 / (slew * SampleRate * 0.1)) : 1f;

        for (int i = 0; i < sampleCount; i++)
        {
            float signal = _signalInput.GetValue(i);
            float trigger = _triggerInput.GetValue(i);
            float gate = _gateInput.GetValue(i);

            bool triggerRising = trigger > 0.5f && !_lastTrigger;
            bool gateHigh = gate > 0.5f;
            _lastTrigger = trigger > 0.5f;

            float triggerOut = 0f;

            if (mode < 0.5f)
            {
                // Sample & Hold mode
                if (triggerRising)
                {
                    _heldValue = signal;
                    triggerOut = 1f;

                    // Add noise
                    if (noise > 0)
                    {
                        _heldValue += (float)(Random.Shared.NextDouble() * 2 - 1) * noise;
                    }

                    // Quantize
                    if (quantize > 0)
                    {
                        float steps = quantize;
                        _heldValue = (float)Math.Round(_heldValue * steps) / steps;
                    }
                }
            }
            else
            {
                // Track & Hold mode (tracks while gate is high)
                if (gateHigh)
                {
                    _heldValue = signal;
                }
                else if (_lastGate && !gateHigh)
                {
                    // Gate just went low
                    if (noise > 0)
                    {
                        _heldValue += (float)(Random.Shared.NextDouble() * 2 - 1) * noise;
                    }
                    if (quantize > 0)
                    {
                        float steps = quantize;
                        _heldValue = (float)Math.Round(_heldValue * steps) / steps;
                    }
                    triggerOut = 1f;
                }
            }

            _lastGate = gateHigh;

            // Apply slew limiting
            if (slew > 0)
            {
                _currentOutput += (_heldValue - _currentOutput) * slewCoeff;
            }
            else
            {
                _currentOutput = _heldValue;
            }

            _output.SetValue(i, _currentOutput);
            _invertedOutput.SetValue(i, -_currentOutput);
            _triggerOutput.SetValue(i, triggerOut);
        }
    }

    /// <summary>
    /// Gets the currently held value.
    /// </summary>
    public float HeldValue => _heldValue;

    public override void Reset()
    {
        base.Reset();
        _heldValue = 0;
        _currentOutput = 0;
        _lastTrigger = false;
        _lastGate = false;
    }
}
