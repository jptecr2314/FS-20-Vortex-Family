using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Gear-down stick response and rate schedule.
    internal sealed class Fs20LandingEnvelope
    {
        internal float Blend { get; private set; }
        internal float Pitch { get; private set; }
        internal float Roll { get; private set; }

        internal void Step(float pitch, float roll, float speed, bool gear, bool airborne, bool assisted, float dt)
        {
            dt = Math.Max(0, Math.Min(.1f, dt));
            float target = assisted && gear && airborne ? 1f - Clamp((speed - 100f) / 50f) : 0f;
            if (!assisted)
                Blend = 0;
            else
                Blend += Math.Max(-dt * 2f, Math.Min(dt * 2f, target - Blend));
            // Soften centre-stick corrections while retaining full travel.
            Pitch = pitch * ((1 - Blend) + Blend * (.65f + .35f * Math.Abs(pitch)));
            Roll = roll * ((1 - Blend) + Blend * (.65f + .35f * Math.Abs(roll)));
        }

        internal static float Clamp(float x) => Math.Max(0, Math.Min(1, x));
    }

    internal sealed class Fs20HookDepartureLock
    {
        internal bool Locked { get; private set; } = true;
        internal static bool AllowAutomatic(bool locked, bool gear, bool overloaded, bool hooked) =>
            hooked || (!locked && gear && !overloaded);

        private float airborneTime, descentTime;
        internal void Step(bool airborne, bool gear, float altitude, float verticalSpeed, float throttle, bool hooked, float dt)
        {
            if (hooked)
                return; // Never release an arresting wire through this guard.
            dt = Math.Max(0, Math.Min(.1f, dt));
            if (!airborne)
            {
                airborneTime = descentTime = 0;
                if (throttle > .85f)
                    Locked = true;
                return;
            }

            airborneTime += dt;
            descentTime = verticalSpeed < -.5f && throttle < .85f ? descentTime + dt : 0;
            // Arm after established flight, including low circuits.
            if ((!gear && airborneTime >= 3f) || (altitude >= 150f && airborneTime >= 8f) || (airborneTime >= 15f && descentTime >= 3f))
                Locked = false;
            // Rearm only after another established flight segment.
            if (!Locked && gear && throttle > .9f && verticalSpeed > 1f)
            {
                Locked = true;
                airborneTime = descentTime = 0;
            }
        }
    }
}
