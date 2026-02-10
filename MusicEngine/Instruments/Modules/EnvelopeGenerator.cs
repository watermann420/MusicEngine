// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: ADSR envelope helper for synths.

using System;

namespace MusicEngine.Instruments.Modules;

/// <summary>
/// ADSR envelope helper for synths.
/// </summary>
public static class EnvelopeGenerator
{
    /// <summary>
    /// Advance an ADSR envelope one sample.
    /// </summary>
    /// <param name="stage">Current envelope stage (0=attack, 1=decay, 2=sustain, 3=release).</param>
    /// <param name="current">Current envelope value.</param>
    /// <param name="attack">Attack time in seconds.</param>
    /// <param name="decay">Decay time in seconds.</param>
    /// <param name="sustain">Sustain level in [0, 1].</param>
    /// <param name="release">Release time in seconds.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="stageRef">Stage reference to advance when thresholds are reached.</param>
    /// <returns>Updated envelope value.</returns>
    public static float Process(int stage, float current, float attack, float decay,
        float sustain, float release, int sampleRate, ref int stageRef)
    {
        const float minTime = 0.002f;

        switch (stage)
        {
            case 0:
                float attackTime = Math.Max(attack, minTime);
                float attackCoeff = 1f - (float)Math.Exp(-1.0 / (attackTime * sampleRate));
                current += attackCoeff * (1.02f - current);
                if (current >= 0.999f)
                {
                    current = 1f;
                    stageRef = 1;
                }
                break;
            case 1:
                float decayTime = Math.Max(decay, minTime);
                float decayCoeff = 1f - (float)Math.Exp(-1.0 / (decayTime * sampleRate));
                current += decayCoeff * (sustain - current);
                if (Math.Abs(current - sustain) < 0.001f)
                {
                    current = sustain;
                    stageRef = 2;
                }
                break;
            case 2:
                float sustainDiff = sustain - current;
                if (Math.Abs(sustainDiff) > 0.0005f)
                {
                    current += sustainDiff * 0.005f;
                }
                else
                {
                    current = sustain;
                }
                break;
            case 3:
                float releaseTime = Math.Max(release, minTime);
                float releaseCoeff = (float)Math.Exp(-1.0 / (releaseTime * sampleRate));
                current *= releaseCoeff;
                if (current < 0.0001f)
                {
                    current = 0f;
                }
                break;
        }

        return current;
    }
}
