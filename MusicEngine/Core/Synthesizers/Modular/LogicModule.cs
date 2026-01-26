// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Logic module for gate and trigger processing.
/// Provides AND, OR, XOR, NOT, and flip-flop functions.
/// </summary>
public class LogicModule : ModuleBase
{
    private bool _flipFlopState;
    private bool _lastClock;
    private bool _lastA;
    private bool _lastB;

    // Inputs
    private readonly ModulePort _inputA;
    private readonly ModulePort _inputB;
    private readonly ModulePort _clockInput;
    private readonly ModulePort _resetInput;

    // Outputs
    private readonly ModulePort _andOutput;
    private readonly ModulePort _orOutput;
    private readonly ModulePort _xorOutput;
    private readonly ModulePort _notAOutput;
    private readonly ModulePort _notBOutput;
    private readonly ModulePort _flipFlopOutput;
    private readonly ModulePort _risingAOutput;
    private readonly ModulePort _fallingAOutput;

    public LogicModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Logic", sampleRate, bufferSize)
    {
        // Inputs
        _inputA = AddInput("A", PortType.Gate);
        _inputB = AddInput("B", PortType.Gate);
        _clockInput = AddInput("Clock", PortType.Trigger);
        _resetInput = AddInput("Reset", PortType.Trigger);

        // Outputs
        _andOutput = AddOutput("AND", PortType.Gate);
        _orOutput = AddOutput("OR", PortType.Gate);
        _xorOutput = AddOutput("XOR", PortType.Gate);
        _notAOutput = AddOutput("NOT A", PortType.Gate);
        _notBOutput = AddOutput("NOT B", PortType.Gate);
        _flipFlopOutput = AddOutput("Flip-Flop", PortType.Gate);
        _risingAOutput = AddOutput("Rising A", PortType.Trigger);
        _fallingAOutput = AddOutput("Falling A", PortType.Trigger);

        // Parameters
        RegisterParameter("Threshold", 0.5f, 0f, 1f);
    }

    public override void Process(int sampleCount)
    {
        float threshold = GetParameter("Threshold");

        for (int i = 0; i < sampleCount; i++)
        {
            float a = _inputA.GetValue(i);
            float b = _inputB.GetValue(i);
            float clock = _clockInput.GetValue(i);
            float reset = _resetInput.GetValue(i);

            bool boolA = a > threshold;
            bool boolB = b > threshold;
            bool clockHigh = clock > threshold;
            bool resetHigh = reset > threshold;

            // Basic logic gates
            _andOutput.SetValue(i, (boolA && boolB) ? 1f : 0f);
            _orOutput.SetValue(i, (boolA || boolB) ? 1f : 0f);
            _xorOutput.SetValue(i, (boolA ^ boolB) ? 1f : 0f);
            _notAOutput.SetValue(i, boolA ? 0f : 1f);
            _notBOutput.SetValue(i, boolB ? 0f : 1f);

            // Edge detection
            bool risingA = boolA && !_lastA;
            bool fallingA = !boolA && _lastA;
            _risingAOutput.SetValue(i, risingA ? 1f : 0f);
            _fallingAOutput.SetValue(i, fallingA ? 1f : 0f);

            // Flip-flop (toggle on rising edge of clock)
            if (resetHigh)
            {
                _flipFlopState = false;
            }
            else if (clockHigh && !_lastClock)
            {
                _flipFlopState = !_flipFlopState;
            }
            _flipFlopOutput.SetValue(i, _flipFlopState ? 1f : 0f);

            _lastA = boolA;
            _lastB = boolB;
            _lastClock = clockHigh;
        }
    }

    public override void Reset()
    {
        base.Reset();
        _flipFlopState = false;
        _lastClock = false;
        _lastA = false;
        _lastB = false;
    }
}
