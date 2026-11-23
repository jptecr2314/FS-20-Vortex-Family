using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Flight-assist limits; these do not predict structural strength.
    internal sealed class Fs20FlightEnvelope
    {
        internal const float MaximumG = 11f;
        internal const float BaselineG = 9f;
        internal const float ReferenceMass = 12000f;
        internal float LimitG { get; private set; } = MaximumG;
        internal float Pitch { get; private set; }
        internal float Roll { get; private set; }
        internal float FlapScale { get; private set; } = 1f;
        internal float PullReserve { get; private set; } = 1f;
        internal const float CombatPitchRate = 1f;
        // The native roll loop requests half this value in the normal envelope.
        internal const float CombatRollRate = 6.4f;
        // Native polar/authority sweep retains useful lift and pitch authority at
        // 40 degrees; peak wind-axis lift is near 44 in the current donor model.
        internal const float MaximumAlpha = 40f;

        private readonly Fs20PitchGovernor governor = new Fs20PitchGovernor();
        internal bool HardProtection => governor.HardProtection;
        internal float LoadRate => governor.LoadRate;
        internal static float EquivalentSpeed(float speed,float density) =>
            Math.Max(0f,speed)*(float)Math.Sqrt(Math.Max(0f,density)/1.225f);
        internal static float SurfaceGain(float equivalentSpeed) => 1f;
        internal static float ElevonAngle(float pitch,float roll,float pitchRange,float rollRange,float gain,float flap)
        {
            float differential = Math.Max(-40f, Math.Min(40f, roll * rollRange * gain));
            // Reserve differential travel; combined pitch gives up symmetric
            // travel instead of silently scaling the requested roll down.
            float symmetric = Math.Max(-40f + Math.Abs(differential),
                Math.Min(40f - Math.Abs(differential), pitch * pitchRange * gain));
            float command = symmetric + differential;
            float freeTravel=Math.Max(0f,40f-Math.Abs(command));
            return command-Math.Min(Math.Max(0f,flap),freeTravel);
        }
        internal static float TargetLimit(float speed, float density, float mass, bool gearDown)
        {
            // Keep the load budget. Available lift follows native aerodynamics;
            // gear and a second low-speed schedule do not reduce that budget.
            float forceLimit = MaximumG * ReferenceMass / Math.Max(ReferenceMass, mass);
            return Math.Max(1f, Math.Min(MaximumG, forceLimit));
        }

        internal void Step(float pitch, float roll, float speed, float density, float mass, float alpha, float normalG, bool gearDown, bool assisted, float dt,
            float nativeRatePerG = 0f, float pitchRateUp = 0f, float gravityNormalG = 1f, float approachBlend = 0f,
            float speedRate = 0f, float configuredLimit = MaximumG, float alphaLimit = MaximumAlpha)
        {
            LimitG = Math.Min(Math.Max(1f, configuredLimit), TargetLimit(speed, density, mass, gearDown));
            if (!assisted)
            {
                Pitch = pitch;
                Roll = roll;
                FlapScale = 1f;
                PullReserve=1f; governor.Reset();
                return;
            }

            dt = Math.Max(0f, Math.Min(0.1f, dt));
            float requested = Math.Max(0f, Math.Min(1f, -pitch));
            Pitch = governor.Step(pitch, speed, normalG, alpha, LimitG, alphaLimit, dt,
                nativeRatePerG > 0f ? nativeRatePerG : 9.81f / Math.Max(30f, speed), pitchRateUp, gravityNormalG, speedRate);
            PullReserve = governor.PullReserve;
            Roll = roll;
            float load = Math.Max(requested, Smooth((normalG - 7f) / 4f));
            FlapScale = gearDown ? 1f : 1f - 0.75f * Smooth(load);
        }

        private static float Smooth(float value)
        {
            float x = Math.Max(0f, Math.Min(1f, value));
            return x * x * (3f - 2f * x);
        }
    }

    // Shared load/AoA reference for A/C/T/NA. Safety changes the requested body
    // rate continuously, preserving the native actuator bias and PID memory.
    internal sealed class Fs20PitchGovernor
    {
        internal bool HardProtection { get; private set; }
        internal float PullReserve { get; private set; } = 1f;
        private float pull, previousG, previousAlpha, loadRate, alphaRate, recoveryTime;
        internal float LoadRate => loadRate;
        private bool sampled;
        internal void Reset()
        {
            pull = loadRate = alphaRate = recoveryTime = 0f; sampled = false;
            HardProtection = false; PullReserve = 1f;
        }
        internal float Step(float pitch, float speed, float normalG, float alpha, float ceiling,
            float maxAlpha, float dt, float nativeRatePerG, float pitchRateUp, float gravityNormalG, float speedRate = 0f, float loadLead = .18f)
        {
            dt = Math.Max(0f, Math.Min(.1f, dt));
            if (dt == 0f) return pitch;
            float dg = sampled ? Clamp((normalG - previousG) / dt, -60f, 60f) : 0f;
            float da = sampled ? Clamp((alpha - previousAlpha) / dt, -120f, 120f) : 0f;
            loadRate += (dg - loadRate) * (1f - (float)Math.Exp(-dt / .08f));
            alphaRate += (da - alphaRate) * (1f - (float)Math.Exp(-dt / .10f));
            bool first = !sampled;
            previousG = normalG; previousAlpha = alpha; sampled = true;
            if (normalG > ceiling + 1f || alpha > maxAlpha + 3f) { HardProtection = true; recoveryTime = 0f; }
            else if (normalG < ceiling - .25f && alpha < maxAlpha - 1f)
            {
                recoveryTime += dt;
                if (recoveryTime >= .15f) HardProtection = false;
            }
            else recoveryTime = 0f;
            float request = Clamp(-pitch, 0f, 1f);
            float fullRate = Math.Max(.001f, nativeRatePerG * ceiling);
            const float radians = (float)(Math.PI / 180d);
            float incidence = Clamp(alpha, -65f, 65f) * radians;
            float overshoot = Math.Max(0f, normalG - ceiling);
            float feedback = .55f + .65f * Smooth(overshoot / 2f);
            // Anticipate native actuator/rate-loop lag without destroying its trim.
            float targetLoad = ceiling + feedback * (ceiling - normalG) - loadLead * Math.Max(0f, loadRate);
            // Increment from the measured body rate. In the path-to-body identity,
            // alphaDot/longitudinal acceleration/gravity cancel between measured
            // and desired normal load. This avoids feeding a delayed alphaDot
            // estimate back into the fast load loop at high dynamic pressure.
            float gRate = pitchRateUp + (targetLoad - normalG) * 9.81f /
                (Math.Max(30f, speed) * Math.Max(.5f, (float)Math.Cos(incidence)));
            // The incremental request must not carry an existing overspeed into
            // the protected turn. Use the load/path-rate identity as an absolute
            // bound too; the proximity blend below leaves safe manoeuvres free.
            float steadyLoadRate = ((targetLoad - gravityNormalG) * 9.81f + speedRate * (float)Math.Sin(incidence)) /
                (Math.Max(30f, speed) * Math.Max(.5f, (float)Math.Cos(incidence)));
            gRate = Math.Min(gRate, steadyLoadRate);
            float trajectoryRate = pitchRateUp - alphaRate * radians;
            float predictedAlpha = alpha + .35f * Math.Max(0f, alphaRate);
            // Slow the closure near the polar boundary to fit native actuator lag;
            // a fast outer AoA loop otherwise over-corrects the retained trim.
            float alphaCeiling = trajectoryRate + (maxAlpha - predictedAlpha) * .9f * radians;
            float proximity = Smooth((predictedAlpha - (maxAlpha - 5f)) / 5f);
            // Only a real post-envelope state requests gentle recovery against a
            // held pull. The diagnostic hard flag never switches the command.
            float recovery = -.35f * Math.Max(Smooth((alpha - maxAlpha) / 4f), Smooth((normalG - ceiling - 2f) / 4f));
            float alphaAllowed = request + (Clamp(alphaCeiling / fullRate, recovery, request) - request) * proximity;
            // A negative/current body rate is not itself an overload. Only blend
            // in the G bound when the measured/predicted load approaches its
            // budget; otherwise it can suppress a perfectly safe recovery pull
            // during a roll reversal and amplify the resulting excursion.
            float predictedLoad = normalG + loadLead * Math.Max(0f, loadRate);
            float loadProximity = Smooth((predictedLoad - (ceiling - 3f)) / 3f);
            float loadAllowed = request + (Clamp(gRate / fullRate, recovery, request) - request) * loadProximity;
            float allowed = Math.Min(loadAllowed, alphaAllowed);
            if (first) pull = allowed;
            else pull += Clamp(allowed - pull, -4f * dt, 6f * dt);
            pull = Clamp(pull, recovery, request);
            PullReserve = request > .001f ? Clamp(pull / request, 0f, 1f) : 1f;
            // Release and deliberate recovery take effect immediately.
            return pitch >= 0f ? pitch : -pull;
        }
        private static float Smooth(float x) { x = Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }
        private static float Clamp(float x, float lo, float hi) => Math.Max(lo, Math.Min(hi, x));
    }
}
