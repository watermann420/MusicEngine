// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Multiply/Ring Modulator module.
/// Multiplies two signals together for ring modulation and VCA functionality.
/// </summary>
public class MultiplyModule : ModuleBase
{
    // Inputs
    private readonly ModulePort _input1;
    private readonly ModulePort _input2;
    private readonly ModulePort _input3;
    private readonly ModulePort _input4;

    // Outputs
    private readonly ModulePort _output12;
    private readonly ModulePort _output34;
    private readonly ModulePort _outputAll;

    public MultiplyModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Multiply", sampleRate, bufferSize)
    {
        // Inputs
        _input1 = AddInput("In 1", PortType.Audio);
        _input2 = AddInput("In 2", PortType.Audio);
        _input3 = AddInput("In 3", PortType.Audio);
        _input4 = AddInput("In 4", PortType.Audio);

        // Outputs
        _output12 = AddOutput("1 * 2", PortType.Audio);
        _output34 = AddOutput("3 * 4", PortType.Audio);
        _outputAll = AddOutput("All", PortType.Audio);

        // Parameters
        RegisterParameter("Carrier", 1f, 0f, 1f);  // 0=In1, 1=Internal carrier
        RegisterParameter("CarrierFreq", 440f, 20f, 2000f);
    }

    private double _carrierPhase;

    public override void Process(int sampleCount)
    {
        float useCarrier = GetParameter("Carrier");
        float carrierFreq = GetParameter("CarrierFreq");
        double phaseInc = carrierFreq / SampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            float in1 = _input1.GetValue(i);
            float in2 = _input2.GetValue(i);
            float in3 = _input3.GetValue(i);
            float in4 = _input4.GetValue(i);

            // If using internal carrier, replace in2
            if (useCarrier > 0.5f && !_input2.IsConnected)
            {
                in2 = (float)Math.Sin(_carrierPhase * 2.0 * Math.PI);
                _carrierPhase += phaseInc;
                if (_carrierPhase >= 1.0) _carrierPhase -= 1.0;
            }

            // Multiply pairs
            float out12 = in1 * in2;
            float out34 = in3 * in4;
            float outAll = out12 * out34;

            // If inputs 3,4 aren't connected, just use 1*2
            if (!_input3.IsConnected && !_input4.IsConnected)
            {
                outAll = out12;
            }

            _output12.SetValue(i, out12);
            _output34.SetValue(i, out34);
            _outputAll.SetValue(i, outAll);
        }
    }

    public override void Reset()
    {
        base.Reset();
        _carrierPhase = 0;
    }
}
