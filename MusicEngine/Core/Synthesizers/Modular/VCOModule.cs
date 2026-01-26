// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Voltage Controlled Oscillator module.
/// Generates multiple waveform outputs with voltage control over pitch, FM, PWM, and sync.
/// </summary>
public class VCOModule : ModuleBase
{
    private double _phase;
    private double _syncPhase;
    private bool _lastSyncState;

    // Inputs
    private readonly ModulePort _vOctInput;
    private readonly ModulePort _fmInput;
    private readonly ModulePort _pwmInput;
    private readonly ModulePort _syncInput;

    // Outputs
    private readonly ModulePort _sineOutput;
    private readonly ModulePort _sawOutput;
    private readonly ModulePort _squareOutput;
    private readonly ModulePort _triangleOutput;

    private const double TwoPi = 2.0 * Math.PI;

    public VCOModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("VCO", sampleRate, bufferSize)
    {
        // Inputs
        _vOctInput = AddInput("V/Oct", PortType.Control);
        _fmInput = AddInput("FM", PortType.Control);
        _pwmInput = AddInput("PWM", PortType.Control);
        _syncInput = AddInput("Sync", PortType.Audio);

        // Outputs
        _sineOutput = AddOutput("Sine", PortType.Audio);
        _sawOutput = AddOutput("Saw", PortType.Audio);
        _squareOutput = AddOutput("Square", PortType.Audio);
        _triangleOutput = AddOutput("Triangle", PortType.Audio);

        // Parameters
        RegisterParameter("Frequency", 440f, 20f, 20000f);
        RegisterParameter("Detune", 0f, -100f, 100f);  // Cents
        RegisterParameter("PulseWidth", 0.5f, 0.01f, 0.99f);
        RegisterParameter("FMAmount", 0f, 0f, 1f);
    }

    public override void Process(int sampleCount)
    {
        float baseFreq = GetParameter("Frequency");
        float detuneCents = GetParameter("Detune");
        float pulseWidth = GetParameter("PulseWidth");
        float fmAmount = GetParameter("FMAmount");

        // Apply detune in cents
        double detuneRatio = Math.Pow(2.0, detuneCents / 1200.0);
        double frequency = baseFreq * detuneRatio;

        for (int i = 0; i < sampleCount; i++)
        {
            // Get CV inputs
            float vOct = _vOctInput.GetValue(i);
            float fm = _fmInput.GetValue(i);
            float pwm = _pwmInput.GetValue(i);
            float sync = _syncInput.GetValue(i);

            // Apply V/Oct (1V per octave)
            double freqMod = frequency * Math.Pow(2.0, vOct);

            // Apply FM modulation
            freqMod += freqMod * fm * fmAmount;

            // Clamp frequency to valid range
            freqMod = Math.Clamp(freqMod, 20.0, SampleRate / 2.0);

            // Calculate phase increment
            double phaseInc = freqMod / SampleRate;

            // Hard sync detection (rising edge)
            bool syncState = sync > 0.5f;
            if (syncState && !_lastSyncState)
            {
                _phase = 0;
            }
            _lastSyncState = syncState;

            // Generate waveforms
            // Sine
            float sine = (float)Math.Sin(_phase * TwoPi);
            _sineOutput.SetValue(i, sine);

            // Saw (naive, with PolyBLEP anti-aliasing)
            float saw = (float)(2.0 * _phase - 1.0);
            saw -= PolyBlep(_phase, phaseInc);
            _sawOutput.SetValue(i, saw);

            // Square with PWM
            float effectivePW = Math.Clamp(pulseWidth + pwm * 0.5f, 0.01f, 0.99f);
            float square = _phase < effectivePW ? 1f : -1f;
            // Apply PolyBLEP at both transitions
            square += PolyBlep(_phase, phaseInc);
            square -= PolyBlep((_phase + 1.0 - effectivePW) % 1.0, phaseInc);
            _squareOutput.SetValue(i, square);

            // Triangle (integrated square wave)
            float triangle;
            if (_phase < 0.5)
            {
                triangle = (float)(4.0 * _phase - 1.0);
            }
            else
            {
                triangle = (float)(3.0 - 4.0 * _phase);
            }
            _triangleOutput.SetValue(i, triangle);

            // Advance phase
            _phase += phaseInc;
            if (_phase >= 1.0)
            {
                _phase -= 1.0;
            }
        }
    }

    /// <summary>
    /// PolyBLEP (Polynomial Band-Limited Step) for anti-aliasing.
    /// </summary>
    private static float PolyBlep(double phase, double phaseInc)
    {
        double t = phase;

        if (t < phaseInc)
        {
            t /= phaseInc;
            return (float)(t + t - t * t - 1.0);
        }
        else if (t > 1.0 - phaseInc)
        {
            t = (t - 1.0) / phaseInc;
            return (float)(t * t + t + t + 1.0);
        }

        return 0f;
    }

    public override void Reset()
    {
        base.Reset();
        _phase = 0;
        _syncPhase = 0;
        _lastSyncState = false;
    }
}
