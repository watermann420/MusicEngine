namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Noise Generator module.
/// Generates white, pink, and brown noise for audio and modulation purposes.
/// </summary>
public class NoiseModule : ModuleBase
{
    private readonly Random _random;

    // Pink noise state (Voss-McCartney algorithm)
    private readonly float[] _pinkRows;
    private int _pinkIndex;
    private float _pinkRunningSum;
    private const int PinkNumRows = 16;

    // Brown noise state
    private float _brownValue;

    // Outputs
    private readonly ModulePort _whiteOutput;
    private readonly ModulePort _pinkOutput;
    private readonly ModulePort _brownOutput;
    private readonly ModulePort _digitalOutput;  // Sample and hold noise

    private int _digitalCounter;
    private float _digitalValue;

    public NoiseModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Noise", sampleRate, bufferSize)
    {
        _random = new Random();
        _pinkRows = new float[PinkNumRows];

        // Initialize pink noise
        for (int i = 0; i < PinkNumRows; i++)
        {
            _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            _pinkRunningSum += _pinkRows[i];
        }

        // Outputs
        _whiteOutput = AddOutput("White", PortType.Audio);
        _pinkOutput = AddOutput("Pink", PortType.Audio);
        _brownOutput = AddOutput("Brown", PortType.Audio);
        _digitalOutput = AddOutput("Digital", PortType.Audio);

        // Parameters
        RegisterParameter("Level", 1f, 0f, 1f);
        RegisterParameter("DigitalRate", 10000f, 100f, 44100f);  // S&H rate for digital noise
    }

    public override void Process(int sampleCount)
    {
        float level = GetParameter("Level");
        float digitalRate = GetParameter("DigitalRate");
        int digitalPeriod = Math.Max(1, (int)(SampleRate / digitalRate));

        for (int i = 0; i < sampleCount; i++)
        {
            // White noise
            float white = (float)(_random.NextDouble() * 2.0 - 1.0);
            _whiteOutput.SetValue(i, white * level);

            // Pink noise (Voss-McCartney algorithm)
            float pink = GeneratePinkNoise();
            _pinkOutput.SetValue(i, pink * level * 0.5f);  // Scale down for similar perceived loudness

            // Brown noise (integrated white noise)
            float brown = GenerateBrownNoise(white);
            _brownOutput.SetValue(i, brown * level);

            // Digital noise (sample and hold)
            _digitalCounter++;
            if (_digitalCounter >= digitalPeriod)
            {
                _digitalCounter = 0;
                _digitalValue = (float)(_random.NextDouble() * 2.0 - 1.0);
            }
            _digitalOutput.SetValue(i, _digitalValue * level);
        }
    }

    private float GeneratePinkNoise()
    {
        // Voss-McCartney algorithm for pink noise
        int lastIndex = _pinkIndex;
        _pinkIndex++;

        if (_pinkIndex >= (1 << PinkNumRows))
        {
            _pinkIndex = 0;
        }

        // Find which rows to update
        int diff = lastIndex ^ _pinkIndex;

        for (int row = 0; row < PinkNumRows; row++)
        {
            if ((diff & (1 << row)) != 0)
            {
                _pinkRunningSum -= _pinkRows[row];
                _pinkRows[row] = (float)(_random.NextDouble() * 2.0 - 1.0);
                _pinkRunningSum += _pinkRows[row];
            }
        }

        // Add white noise and normalize
        float white = (float)(_random.NextDouble() * 2.0 - 1.0);
        return (_pinkRunningSum + white) / (PinkNumRows + 1);
    }

    private float GenerateBrownNoise(float white)
    {
        // Brown noise: integrate white noise with leaky integrator
        _brownValue += white * 0.02f;
        _brownValue *= 0.99f;  // Leak to prevent DC drift
        return Math.Clamp(_brownValue, -1f, 1f);
    }

    public override void Reset()
    {
        base.Reset();
        _brownValue = 0;
        _digitalCounter = 0;
        _digitalValue = 0;
        _pinkIndex = 0;
        _pinkRunningSum = 0;
        for (int i = 0; i < PinkNumRows; i++)
        {
            _pinkRows[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            _pinkRunningSum += _pinkRows[i];
        }
    }
}
