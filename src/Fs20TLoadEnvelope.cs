using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Same continuous body-rate reference as A/C; delta structural load budget.
    internal sealed class Fs20TLoadEnvelope
    {
        internal float LimitG { get; private set; } = Fs20TStructure.MaximumG;
        internal float Pitch { get; private set; }
        internal float PullReserve => governor.PullReserve;
        internal bool HardProtection => governor.HardProtection;
        internal float LoadRate => governor.LoadRate;
        private readonly Fs20PitchGovernor governor = new Fs20PitchGovernor();
        internal void Step(float pitch, float speed, float density, float mass, float normalG, float alpha,
            bool enabled, bool highAuthority, float dt, float nativeRatePerG,
            float pitchRateUp, float gravityNormalG, float approachBlend,
            float speedRate = 0f, float configuredLimit = Fs20TStructure.MaximumG,
            float alphaLimit = Fs20FlightEnvelope.MaximumAlpha)
        {
            LimitG = Math.Min(Math.Max(1f, configuredLimit), Fs20TStructure.MaximumG *
                Math.Min(1f, Fs20TStructure.DesignGrossMass / Math.Max(Fs20TStructure.DesignGrossMass, mass)));
            if (!enabled)
            {
                Pitch = pitch;
                governor.Reset();
                return;
            }
            Pitch = governor.Step(pitch, speed, normalG, alpha, LimitG, alphaLimit, dt,
                nativeRatePerG, pitchRateUp, gravityNormalG, speedRate, .25f);
        }
    }
}
