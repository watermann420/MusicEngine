namespace MusicEngine.Core.Synthesizers.Modular;

/// <summary>
/// ADSR Envelope Generator module.
/// Generates attack-decay-sustain-release envelopes triggered by gate signals.
/// Supports retrigger input for legato behavior control.
/// </summary>
public class ADSRModule : ModuleBase
{
    private enum EnvelopeStage
    {
        Idle,
        Attack,
        Decay,
        Sustain,
        Release
    }

    private EnvelopeStage _stage = EnvelopeStage.Idle;
    private double _envelopeValue;
    private double _attackBase;
    private double _decayBase;
    private double _releaseBase;
    private bool _gateActive;
    private bool _lastRetrigger;

    // Inputs
    private readonly ModulePort _gateInput;
    private readonly ModulePort _retriggerInput;

    // Outputs
    private readonly ModulePort _envelopeOutput;
    private readonly ModulePort _endOfStageOutput;

    // Curve coefficients for exponential envelope
    private const double AttackCurve = 0.3;
    private const double DecayCurve = 0.0001;
    private const double ReleaseCurve = 0.0001;

    public ADSRModule(int sampleRate = 44100, int bufferSize = 1024)
        : base("ADSR", sampleRate, bufferSize)
    {
        // Inputs
        _gateInput = AddInput("Gate", PortType.Gate);
        _retriggerInput = AddInput("Retrigger", PortType.Trigger);

        // Outputs
        _envelopeOutput = AddOutput("Envelope Out", PortType.Control);
        _endOfStageOutput = AddOutput("EOS", PortType.Trigger);

        // Parameters (times in seconds)
        RegisterParameter("Attack", 0.01f, 0.001f, 10f);
        RegisterParameter("Decay", 0.1f, 0.001f, 10f);
        RegisterParameter("Sustain", 0.7f, 0f, 1f);
        RegisterParameter("Release", 0.3f, 0.001f, 10f);
        RegisterParameter("Curve", 0.5f, 0f, 1f);  // 0 = Linear, 1 = Exponential
    }

    public override void Process(int sampleCount)
    {
        float attack = GetParameter("Attack");
        float decay = GetParameter("Decay");
        float sustain = GetParameter("Sustain");
        float release = GetParameter("Release");
        float curve = GetParameter("Curve");

        // Calculate time constants
        double attackRate = CalculateCoefficient(attack, AttackCurve);
        double decayRate = CalculateCoefficient(decay, DecayCurve);
        double releaseRate = CalculateCoefficient(release, ReleaseCurve);

        // Calculate target levels for curved envelopes
        double attackTarget = 1.0 + AttackCurve;
        double decayTarget = sustain - DecayCurve;
        double releaseTarget = -ReleaseCurve;

        for (int i = 0; i < sampleCount; i++)
        {
            float gate = _gateInput.GetValue(i);
            float retrigger = _retriggerInput.GetValue(i);

            bool gateOn = gate > 0.5f;
            bool retriggerPulse = retrigger > 0.5f && !_lastRetrigger;
            _lastRetrigger = retrigger > 0.5f;

            float eosOutput = 0f;

            // Handle gate transitions
            if (gateOn && !_gateActive)
            {
                // Gate just went high - start attack
                _stage = EnvelopeStage.Attack;
                _gateActive = true;
            }
            else if (!gateOn && _gateActive)
            {
                // Gate just went low - start release
                _stage = EnvelopeStage.Release;
                _gateActive = false;
            }

            // Handle retrigger
            if (retriggerPulse && _gateActive)
            {
                _stage = EnvelopeStage.Attack;
            }

            // Process envelope
            switch (_stage)
            {
                case EnvelopeStage.Idle:
                    _envelopeValue = 0;
                    break;

                case EnvelopeStage.Attack:
                    if (curve > 0.5f)
                    {
                        // Exponential attack
                        _envelopeValue = attackTarget - (attackTarget - _envelopeValue) * (1.0 - attackRate);
                    }
                    else
                    {
                        // Linear attack
                        _envelopeValue += 1.0 / (attack * SampleRate);
                    }

                    if (_envelopeValue >= 1.0)
                    {
                        _envelopeValue = 1.0;
                        _stage = EnvelopeStage.Decay;
                        eosOutput = 1f;
                    }
                    break;

                case EnvelopeStage.Decay:
                    if (curve > 0.5f)
                    {
                        // Exponential decay
                        _envelopeValue = decayTarget + (_envelopeValue - decayTarget) * (1.0 - decayRate);
                    }
                    else
                    {
                        // Linear decay
                        _envelopeValue -= (1.0 - sustain) / (decay * SampleRate);
                    }

                    if (_envelopeValue <= sustain)
                    {
                        _envelopeValue = sustain;
                        _stage = EnvelopeStage.Sustain;
                        eosOutput = 1f;
                    }
                    break;

                case EnvelopeStage.Sustain:
                    _envelopeValue = sustain;
                    break;

                case EnvelopeStage.Release:
                    if (curve > 0.5f)
                    {
                        // Exponential release
                        _envelopeValue = releaseTarget + (_envelopeValue - releaseTarget) * (1.0 - releaseRate);
                    }
                    else
                    {
                        // Linear release
                        _envelopeValue -= _envelopeValue / (release * SampleRate);
                    }

                    if (_envelopeValue <= 0.0001)
                    {
                        _envelopeValue = 0;
                        _stage = EnvelopeStage.Idle;
                        eosOutput = 1f;
                    }
                    break;
            }

            // Clamp and output
            float output = (float)Math.Clamp(_envelopeValue, 0.0, 1.0);
            _envelopeOutput.SetValue(i, output);
            _endOfStageOutput.SetValue(i, eosOutput);
        }
    }

    private double CalculateCoefficient(float time, double targetRatio)
    {
        double samples = time * SampleRate;
        if (samples <= 0) return 1.0;
        return 1.0 - Math.Exp(-Math.Log((1.0 + targetRatio) / targetRatio) / samples);
    }

    /// <summary>
    /// Triggers the envelope (simulates gate on).
    /// </summary>
    public void Trigger()
    {
        _stage = EnvelopeStage.Attack;
        _gateActive = true;
    }

    /// <summary>
    /// Releases the envelope (simulates gate off).
    /// </summary>
    public void ReleaseEnvelope()
    {
        _stage = EnvelopeStage.Release;
        _gateActive = false;
    }

    /// <summary>
    /// Gets the current envelope stage.
    /// </summary>
    public string CurrentStage => _stage.ToString();

    /// <summary>
    /// Gets whether the envelope is currently active (not idle).
    /// </summary>
    public bool IsActive => _stage != EnvelopeStage.Idle;

    public override void Reset()
    {
        base.Reset();
        _stage = EnvelopeStage.Idle;
        _envelopeValue = 0;
        _gateActive = false;
        _lastRetrigger = false;
    }
}
