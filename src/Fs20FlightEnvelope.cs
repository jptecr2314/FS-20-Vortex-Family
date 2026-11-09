using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Candidate control laws, deliberately independent of Unity for offline
    // envelope/transient checks. These are not structural strength predictions.
    internal sealed class Fs20FlightEnvelope
    {
        internal const float MaximumG = 11f;
        internal const float BaselineG = 9f;
        internal const float ReferenceMass = 12000f;
        internal float LimitG { get; private set; } = BaselineG;
        internal float Pitch { get; private set; }
        internal float Roll { get; private set; }
        internal float FlapScale { get; private set; } = 1f;
        private float pullDemand;

        internal static float TargetLimit(float speed, float density, float mass, bool gearDown)
        {
            // EAS schedules extra authority with dynamic pressure. The original
            // native low-speed lift/pitch-rate limiter still runs afterward.
            float eas = speed * (float)Math.Sqrt(Math.Max(0f, density) / 1.225f);
            float speedLimit = BaselineG + 2f * Smooth((eas - 100f) / 60f);
            float forceLimit = MaximumG * ReferenceMass / Math.Max(ReferenceMass, mass);
            return Math.Max(1f, Math.Min(gearDown ? BaselineG : MaximumG,
                Math.Min(speedLimit, forceLimit)));
        }

        internal void Step(float pitch, float roll, float speed, float density,
            float mass, float alpha, float normalG, bool gearDown, bool assisted, float dt)
        {
            if (!assisted)
            {
                LimitG=BaselineG; Pitch=pitch; Roll=roll; FlapScale=1f; pullDemand=0f;
                return;
            }
            dt=Math.Max(0f,Math.Min(0.1f,dt));
            float target=TargetLimit(speed,density,mass,gearDown);
            // Rising authority is gradual. Reductions immediately respect the
            // new mass/pressure ceiling; unloading the stick is never delayed.
            LimitG=Math.Min(target,LimitG+2f*dt);
            float requested=Math.Max(0f,Math.Min(1f,-pitch));
            float alphaReserve=1f-Smooth((alpha-20f)/6f);
            float overloadReserve=1f-Smooth((normalG-LimitG-0.15f)/0.85f);
            pullDemand=Math.Min(requested*alphaReserve*overloadReserve,pullDemand+3f*dt);
            Pitch=pitch>=0f ? pitch*Math.Min(1f,BaselineG/LimitG)
                : -pullDemand;
            Roll=roll*(0.6f+0.4f*alphaReserve);
            float load=Math.Max(requested,Smooth((normalG-7f)/4f));
            FlapScale=gearDown ? 1f : 1f-0.75f*Smooth(load);
        }

        private static float Smooth(float value)
        {
            float x=Math.Max(0f,Math.Min(1f,value));
            return x*x*(3f-2f*x);
        }
    }
}
