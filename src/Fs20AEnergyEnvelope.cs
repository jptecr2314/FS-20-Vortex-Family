using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Original-wing A command envelope.
    internal sealed class Fs20AEnergyEnvelope
    {
        internal const float MaximumG = 11f;
        internal const float BaselineG = 9f;
        internal const float ReferenceMass = 13500f;
        internal float LimitG { get; private set; } = BaselineG;
        internal float Pitch { get; private set; }
        internal float Roll { get; private set; }

        private readonly Fs20PitchGovernor governor = new Fs20PitchGovernor();
        internal bool HardProtection => governor.HardProtection;
        internal void Step(float pitch, float roll, float mass, float alpha, float normalG, bool assisted, float dt,
            float speed = 200f, float nativeRatePerG = 0f, float pitchRateUp = 0f, float gravityNormalG = 1f, float approachBlend = 0f)
        {
            if (!assisted)
            {
                LimitG = BaselineG;
                Pitch = pitch;
                Roll = roll;
                governor.Reset();
                return;
            }

            float step = Math.Max(0f, Math.Min(.1f, dt));
            float ceiling = MaximumG * ReferenceMass / Math.Max(ReferenceMass, mass);
            LimitG = Math.Min(ceiling, LimitG + 2f * step);
            float alphaReserve = 1f - Smooth((alpha - 18f) / 6f);
            float applied = Math.Min(LimitG, 11f - 7f * Math.Max(0f, Math.Min(1f, approachBlend)));
            float alphaLimit = 24f - 6f * Math.Max(0f, Math.Min(1f, approachBlend));
            Pitch = governor.Step(pitch, speed, normalG, alpha, applied, alphaLimit, step,
                nativeRatePerG > 0f ? nativeRatePerG : 9.81f / Math.Max(30f, speed), pitchRateUp, gravityNormalG);
            Roll = roll * (.75f + .25f * alphaReserve);
        }

        private static float Smooth(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }
    }
}
