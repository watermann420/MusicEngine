namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Voltage Controlled Filter module.
/// Implements a state-variable filter with voltage control over cutoff and resonance.
/// Provides simultaneous lowpass, highpass, bandpass, and notch outputs.
/// </summary>
public class VCFModule : ModuleBase
{
    // State variables for the filter
    private double _lowpass;
    private double _highpass;
    private double _bandpass;
    private double _notch;

    // Inputs
    private readonly ModulePort _audioInput;
    private readonly ModulePort _cutoffCvInput;
    private readonly ModulePort _resonanceCvInput;

    // Outputs
    private readonly ModulePort _lpOutput;
    private readonly ModulePort _hpOutput;
    private readonly ModulePort _bpOutput;
    private readonly ModulePort _notchOutput;

    private const double TwoPi = 2.0 * Math.PI;

    public VCFModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("VCF", sampleRate, bufferSize)
    {
        // Inputs
        _audioInput = AddInput("Audio In", PortType.Audio);
        _cutoffCvInput = AddInput("Cutoff CV", PortType.Control);
        _resonanceCvInput = AddInput("Resonance CV", PortType.Control);

        // Outputs
        _lpOutput = AddOutput("LP", PortType.Audio);
        _hpOutput = AddOutput("HP", PortType.Audio);
        _bpOutput = AddOutput("BP", PortType.Audio);
        _notchOutput = AddOutput("Notch", PortType.Audio);

        // Parameters
        RegisterParameter("Cutoff", 1000f, 20f, 20000f);
        RegisterParameter("Resonance", 0f, 0f, 1f);
        RegisterParameter("CutoffCVAmount", 1f, 0f, 5f);  // CV modulation depth in octaves
        RegisterParameter("Drive", 0f, 0f, 1f);           // Input saturation
    }

    public override void Process(int sampleCount)
    {
        float baseCutoff = GetParameter("Cutoff");
        float resonance = GetParameter("Resonance");
        float cvAmount = GetParameter("CutoffCVAmount");
        float drive = GetParameter("Drive");

        for (int i = 0; i < sampleCount; i++)
        {
            float audioIn = _audioInput.GetValue(i);
            float cutoffCv = _cutoffCvInput.GetValue(i);
            float resonanceCv = _resonanceCvInput.GetValue(i);

            // Apply drive/saturation
            if (drive > 0)
            {
                float driveAmount = 1f + drive * 10f;
                audioIn = (float)Math.Tanh(audioIn * driveAmount) / (float)Math.Tanh(driveAmount);
            }

            // Calculate modulated cutoff frequency
            // CV is in octaves (1V/oct style)
            double cutoff = baseCutoff * Math.Pow(2.0, cutoffCv * cvAmount);
            cutoff = Math.Clamp(cutoff, 20.0, SampleRate * 0.45);

            // Calculate modulated resonance (Q)
            float totalResonance = Math.Clamp(resonance + resonanceCv, 0f, 0.99f);

            // State Variable Filter coefficients
            // Using the Chamberlin implementation
            double f = 2.0 * Math.Sin(Math.PI * cutoff / SampleRate);
            double q = 1.0 - totalResonance;

            // Ensure stability
            f = Math.Min(f, 0.99);
            q = Math.Max(q, 0.01);

            // Calculate filter
            _highpass = audioIn - _lowpass - q * _bandpass;
            _bandpass += f * _highpass;
            _lowpass += f * _bandpass;
            _notch = _highpass + _lowpass;

            // Soft clip outputs to prevent blowup at high resonance
            _lpOutput.SetValue(i, SoftClip((float)_lowpass));
            _hpOutput.SetValue(i, SoftClip((float)_highpass));
            _bpOutput.SetValue(i, SoftClip((float)_bandpass));
            _notchOutput.SetValue(i, SoftClip((float)_notch));
        }
    }

    private static float SoftClip(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }

    public override void Reset()
    {
        base.Reset();
        _lowpass = 0;
        _highpass = 0;
        _bandpass = 0;
        _notch = 0;
    }
}
