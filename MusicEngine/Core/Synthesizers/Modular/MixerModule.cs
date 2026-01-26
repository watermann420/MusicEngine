namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// Mixer module with 4 inputs and master output.
/// Supports both audio and CV mixing with individual level controls.
/// </summary>
public class MixerModule : ModuleBase
{
    private const int ChannelCount = 4;

    // Inputs
    private readonly ModulePort[] _inputs;
    private readonly ModulePort[] _cvInputs;

    // Outputs
    private readonly ModulePort _mixOutput;
    private readonly ModulePort _invertedOutput;

    public MixerModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("Mixer", sampleRate, bufferSize)
    {
        _inputs = new ModulePort[ChannelCount];
        _cvInputs = new ModulePort[ChannelCount];

        // Add inputs
        for (int ch = 0; ch < ChannelCount; ch++)
        {
            _inputs[ch] = AddInput($"In{ch + 1}", PortType.Audio);
            _cvInputs[ch] = AddInput($"CV{ch + 1}", PortType.Control);
        }

        // Outputs
        _mixOutput = AddOutput("Mix Out", PortType.Audio);
        _invertedOutput = AddOutput("Inverted", PortType.Audio);

        // Parameters
        for (int ch = 0; ch < ChannelCount; ch++)
        {
            RegisterParameter($"Level{ch + 1}", 1f, 0f, 2f);
            RegisterParameter($"Pan{ch + 1}", 0.5f, 0f, 1f);  // 0 = Left, 0.5 = Center, 1 = Right
            RegisterParameter($"Mute{ch + 1}", 0f, 0f, 1f);
        }

        RegisterParameter("MasterLevel", 1f, 0f, 2f);
        RegisterParameter("DCOffset", 0f, -1f, 1f);
    }

    public override void Process(int sampleCount)
    {
        float masterLevel = GetParameter("MasterLevel");
        float dcOffset = GetParameter("DCOffset");

        for (int i = 0; i < sampleCount; i++)
        {
            float mix = 0f;

            for (int ch = 0; ch < ChannelCount; ch++)
            {
                float mute = GetParameter($"Mute{ch + 1}");
                if (mute > 0.5f) continue;

                float level = GetParameter($"Level{ch + 1}");
                float input = _inputs[ch].GetValue(i);
                float cv = _cvInputs[ch].GetValue(i);

                // Apply CV modulation to level (0 to 1 range)
                float modulatedLevel = level * Math.Max(0f, 1f + cv);

                mix += input * modulatedLevel;
            }

            // Apply master level and DC offset
            float output = mix * masterLevel + dcOffset;

            // Soft clip to prevent harsh clipping
            output = SoftClip(output);

            _mixOutput.SetValue(i, output);
            _invertedOutput.SetValue(i, -output);
        }
    }

    private static float SoftClip(float x)
    {
        if (x > 1.5f) return 1f;
        if (x < -1.5f) return -1f;
        if (x > 1f) return 1f - (x - 1f) * (x - 1f) / 2f;
        if (x < -1f) return -1f + (x + 1f) * (x + 1f) / 2f;
        return x;
    }

    /// <summary>
    /// Sets the level for a specific channel (1-4).
    /// </summary>
    public void SetChannelLevel(int channel, float level)
    {
        if (channel >= 1 && channel <= ChannelCount)
        {
            SetParameter($"Level{channel}", level);
        }
    }

    /// <summary>
    /// Gets the level for a specific channel (1-4).
    /// </summary>
    public float GetChannelLevel(int channel)
    {
        if (channel >= 1 && channel <= ChannelCount)
        {
            return GetParameter($"Level{channel}");
        }
        return 0f;
    }

    /// <summary>
    /// Mutes or unmutes a channel.
    /// </summary>
    public void SetChannelMute(int channel, bool muted)
    {
        if (channel >= 1 && channel <= ChannelCount)
        {
            SetParameter($"Mute{channel}", muted ? 1f : 0f);
        }
    }

    public override void Reset()
    {
        base.Reset();
    }
}
