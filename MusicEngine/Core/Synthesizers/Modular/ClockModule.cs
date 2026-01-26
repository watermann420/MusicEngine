// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Clock Generator module.
/// Generates clock signals at various divisions and multiplications of the master tempo.
/// Supports swing/shuffle and external sync.
/// </summary>
public class ClockModule : ModuleBase
{
    private double _phase;
    private double _swingPhase;
    private bool _swingHalf;
    private int[] _divisionCounters;
    private bool[] _divisionStates;

    // Inputs
    private readonly ModulePort _externalClockInput;
    private readonly ModulePort _resetInput;
    private readonly ModulePort _tempoInput;

    // Outputs
    private readonly ModulePort _clockOutput;
    private readonly ModulePort _div2Output;
    private readonly ModulePort _div4Output;
    private readonly ModulePort _div8Output;
    private readonly ModulePort _mult2Output;
    private readonly ModulePort _resetOutput;

    private bool _lastExtClock;
    private int _extClockCounter;
    private float _measuredTempo;

    public ClockModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Clock", sampleRate, bufferSize)
    {
        _divisionCounters = new int[4];  // /2, /4, /8, /16
        _divisionStates = new bool[4];

        // Inputs
        _externalClockInput = AddInput("External", PortType.Trigger);
        _resetInput = AddInput("Reset", PortType.Trigger);
        _tempoInput = AddInput("Tempo CV", PortType.Control);

        // Outputs
        _clockOutput = AddOutput("Clock", PortType.Trigger);
        _div2Output = AddOutput("/2", PortType.Trigger);
        _div4Output = AddOutput("/4", PortType.Trigger);
        _div8Output = AddOutput("/8", PortType.Trigger);
        _mult2Output = AddOutput("x2", PortType.Trigger);
        _resetOutput = AddOutput("Reset Out", PortType.Trigger);

        // Parameters
        RegisterParameter("BPM", 120f, 20f, 300f);
        RegisterParameter("Swing", 0f, 0f, 1f);          // Swing amount (0-1)
        RegisterParameter("PulseWidth", 0.5f, 0.1f, 0.9f);  // Duty cycle
        RegisterParameter("Run", 1f, 0f, 1f);            // Start/Stop
        RegisterParameter("ExternalSync", 0f, 0f, 1f);   // Use external clock
    }

    public override void Process(int sampleCount)
    {
        float bpm = GetParameter("BPM");
        float swing = GetParameter("Swing");
        float pulseWidth = GetParameter("PulseWidth");
        bool run = GetParameter("Run") > 0.5f;
        bool externalSync = GetParameter("ExternalSync") > 0.5f;

        // Convert BPM to frequency (quarter notes per second)
        double clockFreq = bpm / 60.0;
        double phaseInc = clockFreq / SampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            float extClock = _externalClockInput.GetValue(i);
            float reset = _resetInput.GetValue(i);
            float tempoCv = _tempoInput.GetValue(i);

            // Apply tempo CV modulation (+/- 50% range)
            double modulatedPhaseInc = phaseInc * (1.0 + tempoCv * 0.5);

            float clockOut = 0f;
            float div2Out = 0f;
            float div4Out = 0f;
            float div8Out = 0f;
            float mult2Out = 0f;
            float resetOut = 0f;

            // Handle reset
            if (reset > 0.5f)
            {
                _phase = 0;
                _swingPhase = 0;
                _swingHalf = false;
                for (int d = 0; d < _divisionCounters.Length; d++)
                {
                    _divisionCounters[d] = 0;
                    _divisionStates[d] = false;
                }
                resetOut = 1f;
            }

            if (!run)
            {
                _clockOutput.SetValue(i, 0);
                _div2Output.SetValue(i, 0);
                _div4Output.SetValue(i, 0);
                _div8Output.SetValue(i, 0);
                _mult2Output.SetValue(i, 0);
                _resetOutput.SetValue(i, resetOut);
                continue;
            }

            if (externalSync)
            {
                // External sync mode
                bool extRising = extClock > 0.5f && !_lastExtClock;
                _lastExtClock = extClock > 0.5f;

                if (extRising)
                {
                    clockOut = 1f;
                    ProcessDivisions(ref div2Out, ref div4Out, ref div8Out);
                }
            }
            else
            {
                // Internal clock
                double oldPhase = _phase;
                _phase += modulatedPhaseInc;

                // Calculate swing offset for odd beats
                double swingOffset = _swingHalf ? swing * 0.3 : 0;
                double effectivePhase = (_phase + swingOffset) % 1.0;

                // Detect phase wrap (clock tick)
                if (_phase >= 1.0)
                {
                    _phase -= 1.0;
                    _swingHalf = !_swingHalf;
                    clockOut = 1f;
                    ProcessDivisions(ref div2Out, ref div4Out, ref div8Out);
                }

                // x2 multiplier (triggers at 0 and 0.5)
                double mult2Phase = (effectivePhase * 2.0) % 1.0;
                if ((oldPhase * 2.0) % 1.0 > mult2Phase)
                {
                    mult2Out = 1f;
                }
            }

            _clockOutput.SetValue(i, clockOut);
            _div2Output.SetValue(i, div2Out);
            _div4Output.SetValue(i, div4Out);
            _div8Output.SetValue(i, div8Out);
            _mult2Output.SetValue(i, mult2Out);
            _resetOutput.SetValue(i, resetOut);
        }
    }

    private void ProcessDivisions(ref float div2, ref float div4, ref float div8)
    {
        // /2
        _divisionCounters[0]++;
        if (_divisionCounters[0] >= 2)
        {
            _divisionCounters[0] = 0;
            div2 = 1f;

            // /4
            _divisionCounters[1]++;
            if (_divisionCounters[1] >= 2)
            {
                _divisionCounters[1] = 0;
                div4 = 1f;

                // /8
                _divisionCounters[2]++;
                if (_divisionCounters[2] >= 2)
                {
                    _divisionCounters[2] = 0;
                    div8 = 1f;
                }
            }
        }
    }

    /// <summary>
    /// Starts the clock.
    /// </summary>
    public void Start()
    {
        SetParameter("Run", 1f);
    }

    /// <summary>
    /// Stops the clock.
    /// </summary>
    public void Stop()
    {
        SetParameter("Run", 0f);
    }

    /// <summary>
    /// Gets the current phase (0-1).
    /// </summary>
    public double Phase => _phase;

    public override void Reset()
    {
        base.Reset();
        _phase = 0;
        _swingPhase = 0;
        _swingHalf = false;
        _lastExtClock = false;
        _extClockCounter = 0;
        _measuredTempo = 120f;
        for (int d = 0; d < _divisionCounters.Length; d++)
        {
            _divisionCounters[d] = 0;
            _divisionStates[d] = false;
        }
    }
}
