// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Low frequency oscillator for modulation.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Low Frequency Oscillator module.
/// Generates multiple waveform outputs at sub-audio frequencies for modulation purposes.
/// Includes Sample & Hold output for random stepped modulation.
/// </summary>
public class LFOModule : ModuleBase
{
    private double _phase;
    private float _sampleAndHoldValue;
    private bool _lastPhaseWrap;
    private readonly Random _random;

    // Inputs
    private readonly ModulePort _rateInput;
    private readonly ModulePort _resetInput;

    // Outputs
    private readonly ModulePort _sineOutput;
    private readonly ModulePort _sawOutput;
    private readonly ModulePort _squareOutput;
    private readonly ModulePort _triangleOutput;
    private readonly ModulePort _sAndHOutput;

    private const double TwoPi = 2.0 * Math.PI;

    public LFOModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("LFO", sampleRate, bufferSize)
    {
        _random = new Random();

        // Inputs
        _rateInput = AddInput("Rate CV", PortType.Control);
        _resetInput = AddInput("Reset", PortType.Trigger);

        // Outputs
        _sineOutput = AddOutput("Sine", PortType.Control);
        _sawOutput = AddOutput("Saw", PortType.Control);
        _squareOutput = AddOutput("Square", PortType.Control);
        _triangleOutput = AddOutput("Triangle", PortType.Control);
        _sAndHOutput = AddOutput("S&H", PortType.Control);

        // Parameters
        RegisterParameter("Rate", 1f, 0.01f, 100f);      // Hz
        RegisterParameter("Shape", 0f, 0f, 4f);           // 0=Sine, 1=Saw, 2=Square, 3=Tri, 4=S&H
        RegisterParameter("Unipolar", 0f, 0f, 1f);        // 0=Bipolar (-1 to +1), 1=Unipolar (0 to +1)
        RegisterParameter("Phase", 0f, 0f, 1f);           // Initial phase offset
        RegisterParameter("RateCVAmount", 1f, 0f, 10f);   // CV modulation depth
    }

    public override void Process(int sampleCount)
    {
        float rate = GetParameter("Rate");
        float unipolar = GetParameter("Unipolar");
        float rateCvAmount = GetParameter("RateCVAmount");

        for (int i = 0; i < sampleCount; i++)
        {
            float rateCv = _rateInput.GetValue(i);
            float reset = _resetInput.GetValue(i);

            // Handle reset trigger
            if (reset > 0.5f)
            {
                _phase = GetParameter("Phase");
            }

            // Calculate modulated rate
            float modulatedRate = rate * (float)Math.Pow(2.0, rateCv * rateCvAmount);
            modulatedRate = Math.Clamp(modulatedRate, 0.001f, 1000f);

            // Calculate phase increment
            double phaseInc = modulatedRate / SampleRate;

            // Track phase wrap for S&H
            bool phaseWrapped = false;
            double oldPhase = _phase;

            // Advance phase
            _phase += phaseInc;
            if (_phase >= 1.0)
            {
                _phase -= 1.0;
                phaseWrapped = true;
            }

            // Update S&H on phase wrap
            if (phaseWrapped && !_lastPhaseWrap)
            {
                _sampleAndHoldValue = (float)(_random.NextDouble() * 2.0 - 1.0);
            }
            _lastPhaseWrap = phaseWrapped;

            // Generate waveforms
            float sine = (float)Math.Sin(_phase * TwoPi);
            float saw = (float)(2.0 * _phase - 1.0);
            float square = _phase < 0.5 ? 1f : -1f;
            float triangle = (float)(4.0 * Math.Abs(_phase - 0.5) - 1.0);

            // Apply unipolar conversion if needed
            if (unipolar > 0.5f)
            {
                sine = (sine + 1f) * 0.5f;
                saw = (saw + 1f) * 0.5f;
                square = (square + 1f) * 0.5f;
                triangle = (triangle + 1f) * 0.5f;
                _sampleAndHoldValue = (_sampleAndHoldValue + 1f) * 0.5f;
            }

            // Output all waveforms
            _sineOutput.SetValue(i, sine);
            _sawOutput.SetValue(i, saw);
            _squareOutput.SetValue(i, square);
            _triangleOutput.SetValue(i, triangle);
            _sAndHOutput.SetValue(i, _sampleAndHoldValue);
        }
    }

    /// <summary>
    /// Resets the LFO to its initial phase.
    /// </summary>
    public void ResetPhase()
    {
        _phase = GetParameter("Phase");
    }

    public override void Reset()
    {
        base.Reset();
        _phase = GetParameter("Phase");
        _sampleAndHoldValue = 0;
        _lastPhaseWrap = false;
    }
}
