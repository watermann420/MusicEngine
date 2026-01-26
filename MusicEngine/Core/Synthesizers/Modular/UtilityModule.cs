namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Utility module with various signal processing functions.
/// Provides attenuation, inversion, offset, rectification, and logic operations.
/// </summary>
public class UtilityModule : ModuleBase
{
    // Inputs
    private readonly ModulePort _input1;
    private readonly ModulePort _input2;

    // Outputs
    private readonly ModulePort _attenuatedOutput;
    private readonly ModulePort _invertedOutput;
    private readonly ModulePort _rectifiedOutput;
    private readonly ModulePort _sumOutput;
    private readonly ModulePort _diffOutput;
    private readonly ModulePort _maxOutput;
    private readonly ModulePort _minOutput;

    public UtilityModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Utility", sampleRate, bufferSize)
    {
        // Inputs
        _input1 = AddInput("In 1", PortType.Control);
        _input2 = AddInput("In 2", PortType.Control);

        // Outputs
        _attenuatedOutput = AddOutput("Attenuated", PortType.Control);
        _invertedOutput = AddOutput("Inverted", PortType.Control);
        _rectifiedOutput = AddOutput("Rectified", PortType.Control);
        _sumOutput = AddOutput("Sum", PortType.Control);
        _diffOutput = AddOutput("Diff", PortType.Control);
        _maxOutput = AddOutput("Max", PortType.Control);
        _minOutput = AddOutput("Min", PortType.Control);

        // Parameters
        RegisterParameter("Attenuate", 1f, 0f, 2f);
        RegisterParameter("Offset", 0f, -5f, 5f);
        RegisterParameter("RectifyMode", 0f, 0f, 2f);  // 0=Full, 1=Half+, 2=Half-
    }

    public override void Process(int sampleCount)
    {
        float attenuate = GetParameter("Attenuate");
        float offset = GetParameter("Offset");
        int rectifyMode = (int)GetParameter("RectifyMode");

        for (int i = 0; i < sampleCount; i++)
        {
            float in1 = _input1.GetValue(i);
            float in2 = _input2.GetValue(i);

            // Attenuated with offset
            float attenuated = in1 * attenuate + offset;
            _attenuatedOutput.SetValue(i, attenuated);

            // Inverted
            _invertedOutput.SetValue(i, -in1);

            // Rectified
            float rectified = rectifyMode switch
            {
                0 => Math.Abs(in1),           // Full wave
                1 => Math.Max(0, in1),        // Half wave positive
                2 => Math.Min(0, in1),        // Half wave negative
                _ => Math.Abs(in1)
            };
            _rectifiedOutput.SetValue(i, rectified);

            // Sum
            _sumOutput.SetValue(i, in1 + in2);

            // Difference
            _diffOutput.SetValue(i, in1 - in2);

            // Max
            _maxOutput.SetValue(i, Math.Max(in1, in2));

            // Min
            _minOutput.SetValue(i, Math.Min(in1, in2));
        }
    }
}
