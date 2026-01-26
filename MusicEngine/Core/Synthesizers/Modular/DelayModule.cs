namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Delay module.
/// Provides voltage-controlled delay with feedback and multiple delay line taps.
/// </summary>
public class DelayModule : ModuleBase
{
    private readonly float[] _delayBuffer;
    private int _writeIndex;
    private int _maxDelaySamples;

    // Inputs
    private readonly ModulePort _audioInput;
    private readonly ModulePort _timeInput;
    private readonly ModulePort _feedbackInput;

    // Outputs
    private readonly ModulePort _wetOutput;
    private readonly ModulePort _mixOutput;
    private readonly ModulePort _tap1Output;
    private readonly ModulePort _tap2Output;

    public DelayModule(int sampleRate = 44100, int bufferSize = 1024, float maxDelaySeconds = 2f)
        : base("Delay", sampleRate, bufferSize)
    {
        _maxDelaySamples = (int)(maxDelaySeconds * sampleRate);
        _delayBuffer = new float[_maxDelaySamples];

        // Inputs
        _audioInput = AddInput("Audio In", PortType.Audio);
        _timeInput = AddInput("Time CV", PortType.Control);
        _feedbackInput = AddInput("Feedback CV", PortType.Control);

        // Outputs
        _wetOutput = AddOutput("Wet", PortType.Audio);
        _mixOutput = AddOutput("Mix", PortType.Audio);
        _tap1Output = AddOutput("Tap 1", PortType.Audio);
        _tap2Output = AddOutput("Tap 2", PortType.Audio);

        // Parameters
        RegisterParameter("Time", 0.25f, 0.001f, 2f);       // Delay time in seconds
        RegisterParameter("Feedback", 0.3f, 0f, 0.95f);     // Feedback amount
        RegisterParameter("Mix", 0.5f, 0f, 1f);             // Dry/wet mix
        RegisterParameter("Tap1Ratio", 0.333f, 0f, 1f);     // Tap 1 as ratio of delay time
        RegisterParameter("Tap2Ratio", 0.666f, 0f, 1f);     // Tap 2 as ratio of delay time
        RegisterParameter("TimeModDepth", 0f, 0f, 1f);      // CV modulation depth
        RegisterParameter("HighCut", 10000f, 500f, 20000f); // Feedback filter
    }

    public override void Process(int sampleCount)
    {
        float delayTime = GetParameter("Time");
        float feedback = GetParameter("Feedback");
        float mix = GetParameter("Mix");
        float tap1Ratio = GetParameter("Tap1Ratio");
        float tap2Ratio = GetParameter("Tap2Ratio");
        float timeModDepth = GetParameter("TimeModDepth");
        float highCut = GetParameter("HighCut");

        // Simple one-pole filter coefficient for feedback filtering
        float filterCoeff = (float)Math.Exp(-2.0 * Math.PI * highCut / SampleRate);
        float lastFiltered = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = _audioInput.GetValue(i);
            float timeCv = _timeInput.GetValue(i);
            float feedbackCv = _feedbackInput.GetValue(i);

            // Calculate modulated delay time
            float modulatedTime = delayTime * (1f + timeCv * timeModDepth);
            modulatedTime = Math.Clamp(modulatedTime, 0.001f, 2f);

            // Calculate delay in samples
            int delaySamples = Math.Clamp((int)(modulatedTime * SampleRate), 1, _maxDelaySamples - 1);
            int tap1Samples = Math.Max(1, (int)(delaySamples * tap1Ratio));
            int tap2Samples = Math.Max(1, (int)(delaySamples * tap2Ratio));

            // Read from delay line (with interpolation for smoother modulation)
            float delayed = ReadDelayInterpolated(delaySamples);
            float tap1 = ReadDelayInterpolated(tap1Samples);
            float tap2 = ReadDelayInterpolated(tap2Samples);

            // Apply feedback filter (one-pole lowpass)
            lastFiltered = delayed * (1f - filterCoeff) + lastFiltered * filterCoeff;

            // Calculate modulated feedback
            float modulatedFeedback = Math.Clamp(feedback + feedbackCv * 0.5f, 0f, 0.95f);

            // Write to delay line with feedback
            _delayBuffer[_writeIndex] = input + lastFiltered * modulatedFeedback;

            // Advance write position
            _writeIndex = (_writeIndex + 1) % _maxDelaySamples;

            // Outputs
            _wetOutput.SetValue(i, delayed);
            _mixOutput.SetValue(i, input * (1f - mix) + delayed * mix);
            _tap1Output.SetValue(i, tap1);
            _tap2Output.SetValue(i, tap2);
        }
    }

    private float ReadDelayInterpolated(int samples)
    {
        // Calculate read position
        int readPos = (_writeIndex - samples + _maxDelaySamples) % _maxDelaySamples;
        return _delayBuffer[readPos];
    }

    /// <summary>
    /// Clears the delay buffer.
    /// </summary>
    public void ClearBuffer()
    {
        Array.Clear(_delayBuffer, 0, _delayBuffer.Length);
    }

    public override void Reset()
    {
        base.Reset();
        ClearBuffer();
        _writeIndex = 0;
    }
}
