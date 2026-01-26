namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Quantizer module.
/// Quantizes continuous CV to discrete musical scale notes.
/// Supports multiple scale types and custom scales.
/// </summary>
public class QuantizerModule : ModuleBase
{
    // Predefined scales (semitone offsets from root)
    private static readonly int[][] Scales = new[]
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },  // Chromatic
        new[] { 0, 2, 4, 5, 7, 9, 11 },                   // Major
        new[] { 0, 2, 3, 5, 7, 8, 10 },                   // Natural Minor
        new[] { 0, 2, 3, 5, 7, 8, 11 },                   // Harmonic Minor
        new[] { 0, 2, 3, 5, 7, 9, 11 },                   // Melodic Minor
        new[] { 0, 3, 5, 6, 7, 10 },                      // Blues
        new[] { 0, 2, 4, 7, 9 },                          // Pentatonic Major
        new[] { 0, 3, 5, 7, 10 },                         // Pentatonic Minor
        new[] { 0, 2, 3, 5, 6, 8, 9, 11 },                // Diminished
        new[] { 0, 2, 4, 6, 8, 10 },                      // Whole Tone
        new[] { 0, 1, 4, 5, 7, 8, 11 },                   // Hungarian Minor
        new[] { 0, 2, 4, 5, 7, 9, 10 },                   // Mixolydian
        new[] { 0, 2, 3, 5, 7, 9, 10 },                   // Dorian
        new[] { 0, 1, 3, 5, 7, 8, 10 },                   // Phrygian
        new[] { 0, 2, 4, 6, 7, 9, 11 },                   // Lydian
        new[] { 0, 1, 3, 5, 6, 8, 10 },                   // Locrian
    };

    public static readonly string[] ScaleNames = new[]
    {
        "Chromatic",
        "Major",
        "Natural Minor",
        "Harmonic Minor",
        "Melodic Minor",
        "Blues",
        "Pentatonic Major",
        "Pentatonic Minor",
        "Diminished",
        "Whole Tone",
        "Hungarian Minor",
        "Mixolydian",
        "Dorian",
        "Phrygian",
        "Lydian",
        "Locrian"
    };

    private float _lastQuantized;
    private float _currentOutput;

    // Inputs
    private readonly ModulePort _cvInput;
    private readonly ModulePort _triggerInput;  // Sample on trigger

    // Outputs
    private readonly ModulePort _cvOutput;
    private readonly ModulePort _triggerOutput;  // Fires when note changes
    private readonly ModulePort _gateOutput;

    public QuantizerModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Quantizer", sampleRate, bufferSize)
    {
        // Inputs
        _cvInput = AddInput("CV In", PortType.Control);
        _triggerInput = AddInput("Trigger", PortType.Trigger);

        // Outputs
        _cvOutput = AddOutput("CV Out", PortType.Control);
        _triggerOutput = AddOutput("Trigger", PortType.Trigger);
        _gateOutput = AddOutput("Gate", PortType.Gate);

        // Parameters
        RegisterParameter("Scale", 0f, 0f, 15f);  // Scale index
        RegisterParameter("Root", 0f, 0f, 11f);   // Root note (0=C, 1=C#, etc.)
        RegisterParameter("Range", 5f, 1f, 10f);  // Octave range
        RegisterParameter("Mode", 0f, 0f, 1f);    // 0=Continuous, 1=Sample & Hold
        RegisterParameter("Glide", 0f, 0f, 1f);   // Portamento amount
    }

    public override void Process(int sampleCount)
    {
        int scaleIndex = (int)Math.Clamp(GetParameter("Scale"), 0, Scales.Length - 1);
        int root = (int)GetParameter("Root");
        float range = GetParameter("Range");
        float mode = GetParameter("Mode");
        float glide = GetParameter("Glide");

        int[] scale = Scales[scaleIndex];
        bool lastTrigger = false;

        for (int i = 0; i < sampleCount; i++)
        {
            float cvIn = _cvInput.GetValue(i);
            float trigger = _triggerInput.GetValue(i);

            bool triggerRising = trigger > 0.5f && !lastTrigger;
            lastTrigger = trigger > 0.5f;

            float triggerOut = 0f;

            // In S&H mode, only sample on trigger
            bool shouldQuantize = mode < 0.5f || triggerRising;

            if (shouldQuantize)
            {
                // Convert CV to semitones (assuming 1V/octave, 0V = C4)
                float semitones = cvIn * 12f * range;

                // Quantize to scale
                float quantized = QuantizeToScale(semitones, scale, root);

                // Check if note changed
                if (Math.Abs(quantized - _lastQuantized) > 0.001f)
                {
                    triggerOut = 1f;
                    _lastQuantized = quantized;
                }

                // Apply glide
                if (glide > 0)
                {
                    float glideSpeed = 1f - glide * 0.999f;
                    _currentOutput += (quantized - _currentOutput) * glideSpeed;
                }
                else
                {
                    _currentOutput = quantized;
                }
            }

            // Convert back to CV (1V/octave)
            float cvOut = _currentOutput / 12f / range;

            _cvOutput.SetValue(i, cvOut);
            _triggerOutput.SetValue(i, triggerOut);
            _gateOutput.SetValue(i, mode < 0.5f ? 1f : (trigger > 0.5f ? 1f : 0f));
        }
    }

    private static float QuantizeToScale(float semitones, int[] scale, int root)
    {
        // Find octave and position within octave
        int octave = (int)Math.Floor(semitones / 12f);
        float position = semitones - octave * 12f;

        // Adjust for root note
        position = (position - root + 12) % 12;

        // Find closest scale degree
        float closest = float.MaxValue;
        float closestDist = float.MaxValue;

        foreach (int degree in scale)
        {
            float dist = Math.Abs(position - degree);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = degree;
            }

            // Check wrapping
            float wrapDist = Math.Abs(position - (degree - 12));
            if (wrapDist < closestDist)
            {
                closestDist = wrapDist;
                closest = degree - 12;
            }
        }

        // Convert back to semitones
        return octave * 12 + closest + root;
    }

    public override void Reset()
    {
        base.Reset();
        _lastQuantized = 0;
        _currentOutput = 0;
    }
}
