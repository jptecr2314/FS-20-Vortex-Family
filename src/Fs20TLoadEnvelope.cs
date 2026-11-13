using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Command shaping around the native FBW. Manual flight retains its native risks.
    internal sealed class Fs20TLoadEnvelope
    {
        internal float LimitG { get; private set; } = 9f;
        internal float Pitch { get; private set; }
        internal float PullReserve { get; private set; } = 1f;
        internal bool HardProtection { get; private set; }
        private float pull, previousG, previousAlpha, rate, alphaRate;
        private bool sampled;

        internal void Step(float pitch, float speed, float density, float mass, float normalG, float alpha,
            bool enabled, bool highAuthority, float dt, float nativeRatePerG,
            float pitchRateUp, float gravityNormalG, float approachBlend)
        {
            dt = Math.Max(0f, Math.Min(.1f, dt));
            if (!enabled || dt == 0f)
            {
                LimitG = 9f; Pitch = pitch; PullReserve = 1f;
                pull = rate = alphaRate = 0f; previousG = normalG; previousAlpha = alpha; sampled = false;
                HardProtection = false;
                return;
            }
            float eas = speed * (float)Math.Sqrt(Math.Max(0f, density) / 1.225f);
            float cornerScale = (float)Math.Sqrt(Math.Max(1f, mass / 10000f));
            float target = highAuthority ? 9f + 2f * Smooth((eas - 140f * cornerScale) / (60f * cornerScale)) : 9f;
            target *= Math.Min(1f, Fs20TStructure.DesignGrossMass / Math.Max(Fs20TStructure.DesignGrossMass, mass));
            // One second from 9 to 11 G; reductions and stick release are immediate.
            LimitG = Math.Min(target, LimitG + 2f * dt);
            float measuredRate = sampled ? Math.Max(-60f, Math.Min(60f, (normalG - previousG) / dt)) : 0f;
            float measuredAlphaRate = sampled ? Math.Max(-120f, Math.Min(120f, (alpha - previousAlpha) / dt)) : 0f;
            rate += (measuredRate - rate) * dt / (.12f + dt);
            alphaRate += (measuredAlphaRate - alphaRate) * dt / (.08f + dt);
            previousG = normalG; previousAlpha = alpha; sampled = true;
            float ceiling = LimitG + (Math.Min(LimitG,5.5f)-LimitG)*Math.Max(0f,Math.Min(1f,approachBlend));
            HardProtection = normalG >= ceiling + .5f || alpha >= 28f;
            float request = Math.Max(0f, Math.Min(1f, -pitch));
            float fullRate = Math.Max(.001f,nativeRatePerG*ceiling);
            // Native FBW tracks body pitch rate. Bound that reference smoothly
            // rather than repeatedly cutting both the stick and its integrator.
            float gRate = (ceiling-gravityNormalG-.5f*(normalG-(ceiling-.25f))-.20f*Math.Max(0f,rate))*9.81f/Math.Max(30f,speed);
            const float radians = (float)(Math.PI/180d);
            float trajectoryRate = pitchRateUp-alphaRate*radians;
            float alphaCeiling = trajectoryRate+(25f-alpha-.10f*Math.Max(0f,alphaRate))*2f*radians;
            float allowed = Math.Max(0f,Math.Min(request,Math.Min(gRate,alphaCeiling)/fullRate));
            if(HardProtection || request==0f)pull=0f;
            else pull += Math.Max(-6f*dt,Math.Min(3f*dt,allowed-pull));
            pull = Math.Min(request,Math.Max(0f,pull));
            PullReserve = request>.001f ? pull/request : 1f;
            // Preserve the previous push authority when the positive limit rises.
            Pitch = pitch >= 0f ? pitch * Math.Min(1f, 9f / LimitG) : -pull;
        }
        private static float Smooth(float value)
        {
            float x = Math.Max(0f, Math.Min(1f, value));
            return x * x * (3f - 2f * x);
        }
    }
}
