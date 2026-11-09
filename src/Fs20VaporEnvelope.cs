using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Artistic condensation response, not a humidity/thermodynamic simulation.
    internal sealed class Fs20VaporEnvelope
    {
        internal float Strength { get; private set; }
        internal float Left { get; private set; }
        internal float Right { get; private set; }

        internal static float Target(float speed, float density, float alpha, float normalG, float radarAlt, float detail)
        {
            float eas = speed * (float)Math.Sqrt(Math.Max(0, density) / 1.225f);
            float speedGate = Smooth((eas - 65f) / 55f);
            float airborne = Smooth((radarAlt - 3f) / 5f);
            float aoa = Smooth((alpha - 5f) / 14f);
            float load = Smooth((normalG - 1.5f) / 6.5f);
            float air = (float)Math.Pow(Math.Max(0, Math.Min(1, density / 1.225f)), .65);
            return speedGate * airborne * air * Smooth((detail - .4f) / .35f) * Math.Max(aoa, load);
        }

        internal void Step(float speed, float density, float alpha, float normalG, float radarAlt, float detail, float rollRate, float dt)
        {
            float target = Target(speed, density, alpha, normalG, radarAlt, detail);
            float step = Math.Max(0, Math.Min(.1f, dt)) * (target > Strength ? 4f : 5f);
            Strength += Math.Max(-step, Math.Min(step, target - Strength));
            float bank = Math.Max(-1, Math.Min(1, rollRate / 3f)) * .18f;
            Left = Math.Max(0, Math.Min(1, Strength * (1 + bank)));
            Right = Math.Max(0, Math.Min(1, Strength * (1 - bank)));
        }

        private static float Smooth(float x)
        {
            x = Math.Max(0, Math.Min(1, x));
            return x * x * (3 - 2 * x);
        }
    }
}
