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

        private float pullDemand;
        private float previousG, loadRate;
        private bool sampled;
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

        internal void Step(float pitch, float roll, float speed, float density, float mass, float alpha, float normalG, bool gearDown, bool assisted, float dt)
        {
            if (!assisted)
            {
                LimitG = BaselineG;
                Pitch = pitch;
                Roll = roll;
                FlapScale = 1f;
                pullDemand = 0f;
                PullReserve=1f;loadRate=0f;sampled=false;previousG=normalG;
                return;
            }

            dt = Math.Max(0f, Math.Min(0.1f, dt));
            float target = TargetLimit(speed, density, mass, gearDown);
            // Ramp authority up; apply reductions and stick release immediately.
            LimitG = Math.Min(target, LimitG + 2f * dt);
            float requested = Math.Max(0f, Math.Min(1f, -pitch));
            float measuredRate=sampled && dt>0f ? Math.Max(-60f,Math.Min(60f,(normalG-previousG)/dt)) : 0f;
            loadRate+=(measuredRate-loadRate)*dt/(.1f+dt);previousG=normalG;sampled=true;
            float predicted=normalG+Math.Max(0f,loadRate)*.1f;
            float alphaReserve = 1f - Smooth((alpha - 22f) / (MaximumAlpha-22f));
            float overloadReserve = 1f - Smooth((predicted - (LimitG-.2f)) / .55f);
            PullReserve=Math.Min(alphaReserve,overloadReserve);
            pullDemand = Math.Min(requested * PullReserve, pullDemand + 5f * dt);
            Pitch = pitch >= 0f ? pitch * Math.Min(1f, BaselineG / LimitG) : -pullDemand;
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
}
