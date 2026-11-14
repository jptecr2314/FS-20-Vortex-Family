using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Flight-assist limits; these do not predict structural strength.
    internal sealed class Fs20FlightEnvelope
    {
        internal const float MaximumG = 11f;
        internal const float BaselineG = 9f;
        internal const float ReferenceMass = 12000f;
        internal float LimitG { get; private set; } = BaselineG;
        internal float Pitch { get; private set; }
        internal float Roll { get; private set; }
        internal float FlapScale { get; private set; } = 1f;
        internal float PullReserve { get; private set; } = 1f;
        internal const float CombatPitchRate = 1f;
        // The native roll loop requests half this value in the normal envelope.
        internal const float CombatRollRate = 6.4f;
        internal const float MaximumAlpha = 28f;

        private readonly Fs20PitchGovernor governor = new Fs20PitchGovernor();
        internal bool HardProtection => governor.HardProtection;
        internal static float EquivalentSpeed(float speed,float density) =>
            Math.Max(0f,speed)*(float)Math.Sqrt(Math.Max(0f,density)/1.225f);
        internal static float SurfaceGain(float equivalentSpeed) =>
            1f-.15f*Smooth((equivalentSpeed-160f)/160f)-.1f*Smooth((equivalentSpeed-320f)/230f);
        internal static float ElevonAngle(float pitch,float roll,float pitchRange,float rollRange,float gain,float flap)
        {
            float symmetric=pitch*pitchRange*gain, differential=roll*rollRange*gain;
            float allocation=Math.Min(1f,40f/Math.Max(.001f,Math.Abs(symmetric)+Math.Abs(differential)));
            float command=(symmetric+differential)*allocation;
            float freeTravel=Math.Max(0f,40f-Math.Abs(command));
            return command-Math.Min(Math.Max(0f,flap),freeTravel);
        }
        internal static float TargetLimit(float speed, float density, float mass, bool gearDown)
        {
            // EAS schedules authority before the native low-speed limiter.
            float eas = EquivalentSpeed(speed,density);
            float speedLimit = BaselineG + 2f * Smooth((eas - 100f) / 60f);
            float forceLimit = MaximumG * ReferenceMass / Math.Max(ReferenceMass, mass);
            return Math.Max(1f, Math.Min(gearDown ? BaselineG : MaximumG, Math.Min(speedLimit, forceLimit)));
        }

        internal void Step(float pitch, float roll, float speed, float density, float mass, float alpha, float normalG, bool gearDown, bool assisted, float dt,
            float nativeRatePerG = 0f, float pitchRateUp = 0f, float gravityNormalG = 1f, float approachBlend = 0f)
        {
            if (!assisted)
            {
                LimitG = BaselineG;
                Pitch = pitch;
                Roll = roll;
                FlapScale = 1f;
                PullReserve=1f; governor.Reset();
                return;
            }

            dt = Math.Max(0f, Math.Min(0.1f, dt));
            float target = TargetLimit(speed, density, mass, gearDown);
            // Ramp authority up; apply reductions and stick release immediately.
            LimitG = Math.Min(target, LimitG + 2f * dt);
            float requested = Math.Max(0f, Math.Min(1f, -pitch));
            float alphaReserve = 1f - Smooth((alpha - 22f) / (MaximumAlpha-22f));
            float ceiling = Math.Min(LimitG, 11f - 7f * Math.Max(0f, Math.Min(1f, approachBlend)));
            float alphaLimit = MaximumAlpha + (18f - MaximumAlpha) * Math.Max(0f, Math.Min(1f, approachBlend));
            Pitch = governor.Step(pitch, speed, normalG, alpha, ceiling, alphaLimit, dt,
                nativeRatePerG > 0f ? nativeRatePerG : 9.81f / Math.Max(30f, speed), pitchRateUp, gravityNormalG);
            PullReserve = governor.PullReserve;
            Roll = roll * (.8f + .2f * alphaReserve);
            float load = Math.Max(requested, Smooth((normalG - 7f) / 4f));
            FlapScale = gearDown ? 1f : 1f - 0.75f * Smooth(load);
        }

        private static float Smooth(float value)
        {
            float x = Math.Max(0f, Math.Min(1f, value));
            return x * x * (3f - 2f * x);
        }
    }

    // Shared A/C rate-reference governor. A soft limit retains the native trim needed
    // to sustain the turn; it must not repeatedly command zero rate or erase trim.
    internal sealed class Fs20PitchGovernor
    {
        internal bool HardProtection { get; private set; }
        internal float PullReserve { get; private set; } = 1f;
        private float pull, previousG, previousAlpha, loadRate, alphaRate;
        private bool sampled;
        internal void Reset()
        {
            pull = loadRate = alphaRate = 0f; sampled = false;
            HardProtection = false; PullReserve = 1f;
        }
        internal float Step(float pitch, float speed, float normalG, float alpha, float ceiling,
            float maxAlpha, float dt, float nativeRatePerG, float pitchRateUp, float gravityNormalG)
        {
            dt = Math.Max(0f, Math.Min(.1f, dt));
            if (dt == 0f) return pitch;
            float dg = sampled ? Clamp((normalG - previousG) / dt, -60f, 60f) : 0f;
            float da = sampled ? Clamp((alpha - previousAlpha) / dt, -120f, 120f) : 0f;
            loadRate += (dg - loadRate) * dt / (.12f + dt);
            alphaRate += (da - alphaRate) * dt / (.08f + dt);
            previousG = normalG; previousAlpha = alpha; sampled = true;
            HardProtection = normalG >= ceiling + .5f || alpha >= maxAlpha + .5f;
            float request = Clamp(-pitch, 0f, 1f);
            float fullRate = Math.Max(.001f, nativeRatePerG * ceiling);
            float gRate = (ceiling - gravityNormalG - .5f * (normalG - ceiling) -
                .2f * Math.Max(0f, loadRate)) * 9.81f / Math.Max(30f, speed);
            const float radians = (float)(Math.PI / 180d);
            float trajectoryRate = pitchRateUp - alphaRate * radians;
            float alphaCeiling = trajectoryRate + (maxAlpha - alpha - .1f * Math.Max(0f, alphaRate)) * 2f * radians;
            float proximity = Clamp((alpha + .1f * Math.Max(0f, alphaRate) - (maxAlpha - 8f)) / 4f, 0f, 1f);
            proximity = proximity * proximity * (3f - 2f * proximity);
            float alphaAllowed = request + (Clamp(alphaCeiling / fullRate, 0f, request) - request) * proximity;
            float allowed = Math.Min(Clamp(gRate / fullRate, 0f, request), alphaAllowed);
            if (HardProtection || request == 0f) pull = 0f;
            else pull += Clamp(allowed - pull, -6f * dt, 3f * dt);
            pull = Clamp(pull, 0f, request);
            PullReserve = request > .001f ? pull / request : 1f;
            return pitch >= 0f ? pitch * Math.Min(1f, 9f / ceiling) : -pull;
        }
        private static float Clamp(float x, float lo, float hi) => Math.Max(lo, Math.Min(hi, x));
    }
}
