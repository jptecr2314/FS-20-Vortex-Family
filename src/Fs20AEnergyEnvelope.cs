using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Original-wing A command envelope.
    internal sealed class Fs20AEnergyEnvelope
    {
        internal const float MaximumG = 11f;
        internal const float BaselineG = 9f;
        internal const float ReferenceMass = 13500f;
        internal float LimitG { get; private set; } = MaximumG;
        internal float Pitch { get; private set; }
        internal float Roll { get; private set; }

        private readonly Fs20PitchGovernor governor = new Fs20PitchGovernor();
        internal bool HardProtection => governor.HardProtection;
        internal float LoadRate => governor.LoadRate;
        internal void Step(float pitch, float roll, float mass, float alpha, float normalG, bool assisted, float dt,
            float speed = 200f, float nativeRatePerG = 0f, float pitchRateUp = 0f, float gravityNormalG = 1f, float approachBlend = 0f,
            float speedRate = 0f, float configuredLimit = MaximumG, float alphaLimit = Fs20FlightEnvelope.MaximumAlpha)
        {
            LimitG = Math.Min(Math.Max(1f, configuredLimit), MaximumG * ReferenceMass / Math.Max(ReferenceMass, mass));
            if (!assisted)
            {
                Pitch = pitch;
                Roll = roll;
                governor.Reset();
                return;
            }

            float step = Math.Max(0f, Math.Min(.1f, dt));
            Pitch = governor.Step(pitch, speed, normalG, alpha, LimitG, alphaLimit, step,
                nativeRatePerG > 0f ? nativeRatePerG : 9.81f / Math.Max(30f, speed), pitchRateUp, gravityNormalG, speedRate);
            Roll = roll;
        }

        private static float Smooth(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }
    }
}
