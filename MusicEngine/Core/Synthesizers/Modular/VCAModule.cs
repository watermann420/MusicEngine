// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Voltage Controlled Amplifier module.
/// Controls the amplitude of an audio signal based on a control voltage.
/// Supports both linear and exponential response curves.
/// </summary>
public class VCAModule : ModuleBase
{
    // Inputs
    private readonly ModulePort _audioInput;
    private readonly ModulePort _cvInput;

    // Outputs
    private readonly ModulePort _audioOutput;

    public VCAModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("VCA", sampleRate, bufferSize)
    {
        // Inputs
        _audioInput = AddInput("Audio In", PortType.Audio);
        _cvInput = AddInput("CV", PortType.Control);

        // Outputs
        _audioOutput = AddOutput("Audio Out", PortType.Audio);

        // Parameters
        RegisterParameter("Gain", 1f, 0f, 2f);
        RegisterParameter("Response", 0f, 0f, 1f);  // 0 = Linear, 1 = Exponential
        RegisterParameter("Bias", 0f, -1f, 1f);     // DC offset for CV
    }

    public override void Process(int sampleCount)
    {
        float gain = GetParameter("Gain");
        float response = GetParameter("Response");
        float bias = GetParameter("Bias");

        for (int i = 0; i < sampleCount; i++)
        {
            float audioIn = _audioInput.GetValue(i);
            float cv = _cvInput.GetValue(i) + bias;

            // Clamp CV to 0-1 range (unipolar for amplitude control)
            cv = Math.Clamp(cv, 0f, 1f);

            // Apply response curve
            float amplitude;
            if (response <= 0f)
            {
                // Linear response
                amplitude = cv;
            }
            else if (response >= 1f)
            {
                // Exponential response (approximation of voltage-controlled exponential)
                // Using a curve that gives ~60dB range
                amplitude = cv > 0.001f ? (float)Math.Pow(cv, 3.0) : 0f;
            }
            else
            {
                // Blend between linear and exponential
                float linear = cv;
                float exponential = cv > 0.001f ? (float)Math.Pow(cv, 3.0) : 0f;
                amplitude = linear * (1f - response) + exponential * response;
            }

            // Apply gain and output
            float output = audioIn * amplitude * gain;
            _audioOutput.SetValue(i, output);
        }
    }

    public override void Reset()
    {
        base.Reset();
    }
}
