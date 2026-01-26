// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Brickwall limiter.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Slew Limiter / Portamento module.
/// Smooths transitions between voltage levels with separate rise and fall times.
/// </summary>
public class SlewLimiterModule : ModuleBase
{
    private float _currentValue;

    // Inputs
    private readonly ModulePort _input;
    private readonly ModulePort _riseInput;
    private readonly ModulePort _fallInput;

    // Outputs
    private readonly ModulePort _output;

    public SlewLimiterModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Slew Limiter", sampleRate, bufferSize)
    {
        // Inputs
        _input = AddInput("In", PortType.Control);
        _riseInput = AddInput("Rise CV", PortType.Control);
        _fallInput = AddInput("Fall CV", PortType.Control);

        // Outputs
        _output = AddOutput("Out", PortType.Control);

        // Parameters
        RegisterParameter("Rise", 0.1f, 0.001f, 10f);   // Rise time in seconds
        RegisterParameter("Fall", 0.1f, 0.001f, 10f);   // Fall time in seconds
        RegisterParameter("Shape", 0.5f, 0f, 1f);       // 0=Linear, 1=Exponential
    }

    public override void Process(int sampleCount)
    {
        float riseTime = GetParameter("Rise");
        float fallTime = GetParameter("Fall");
        float shape = GetParameter("Shape");

        for (int i = 0; i < sampleCount; i++)
        {
            float input = _input.GetValue(i);
            float riseCv = _riseInput.GetValue(i);
            float fallCv = _fallInput.GetValue(i);

            // Modulate times with CV
            float modulatedRise = riseTime * (1f + riseCv);
            float modulatedFall = fallTime * (1f + fallCv);

            modulatedRise = Math.Max(0.0001f, modulatedRise);
            modulatedFall = Math.Max(0.0001f, modulatedFall);

            // Calculate slew rate
            float delta = input - _currentValue;

            if (Math.Abs(delta) < 0.0001f)
            {
                _currentValue = input;
            }
            else if (delta > 0)
            {
                // Rising
                float rate = 1f / (modulatedRise * SampleRate);
                if (shape > 0.5f)
                {
                    // Exponential
                    rate *= Math.Abs(delta);
                }
                _currentValue += Math.Min(rate, delta);
            }
            else
            {
                // Falling
                float rate = 1f / (modulatedFall * SampleRate);
                if (shape > 0.5f)
                {
                    // Exponential
                    rate *= Math.Abs(delta);
                }
                _currentValue -= Math.Min(rate, -delta);
            }

            _output.SetValue(i, _currentValue);
        }
    }

    public override void Reset()
    {
        base.Reset();
        _currentValue = 0;
    }
}
