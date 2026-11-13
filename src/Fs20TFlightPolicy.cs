using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TFlightPolicy
    {
        // Game balance: additional skin and framing, without duplicating
        // the native mechanisms and hardware in the larger section.
        internal const float AddedStructureFraction = .25f;
        internal const float AddedParasiteDragFraction = .35f;

        internal static float SpeedScale(float massRatio, float areaRatio)
        {
            if (!(massRatio > 0) || !(areaRatio > 0) || float.IsInfinity(massRatio) || float.IsInfinity(areaRatio)) throw new ArgumentOutOfRangeException();
            return (float)Math.Sqrt(massRatio / areaRatio);
        }
        internal const float ElevonTravel = 40f;

        internal static bool Approach(bool assisted, bool gear, float speed, float minimumFbwSpeed, float nozzleAngle) =>
            assisted && gear && speed >= minimumFbwSpeed && nozzleAngle < 25f;

        internal static float ForwardFlightWeight(bool assisted, bool airborne, bool gear, float nozzleAngle)
        {
            if (!assisted || !airborne || gear || float.IsNaN(nozzleAngle)) return 0f;
            return 1f - SmoothStep(9f, 25f, nozzleAngle);
        }

        internal static float GainScale(float inertiaRatio, float authorityRatio)
        {
            if (!(inertiaRatio > 0f) || !(authorityRatio > 0f) || float.IsInfinity(inertiaRatio) || float.IsInfinity(authorityRatio)) return 1f;
            return Clamp(inertiaRatio / authorityRatio, .25f, 1.5f);
        }

        internal static float SlatFraction(float speed, float alpha, float density)
        {
            if (!(speed >= 0f) || float.IsInfinity(speed) || float.IsNaN(alpha) || !(density > 0f) || float.IsInfinity(density)) return 0f;
            float equivalentSpeed = speed * (float)Math.Sqrt(density / 1.225f);
            float lowSpeed = 1f - SmoothStep(120f, 160f, equivalentSpeed);
            float maneuver = SmoothStep(8f, 18f, Math.Abs(alpha)) * (1f - SmoothStep(140f, 180f, equivalentSpeed));
            return Math.Max(lowSpeed, maneuver);
        }

        private static float Clamp(float x, float low, float high) => Math.Max(low, Math.Min(high, x));
        private static float SmoothStep(float from, float to, float value)
        {
            float t = Math.Max(0f, Math.Min(1f, (value - from) / (to - from)));
            return t * t * (3f - 2f * t);
        }

        internal static float ElevonAngle(float pitch, float roll, float side, bool outer)
        {
            float differential = Clamp(roll, -1f, 1f) * side * (outer ? 30f : 18f);
            float symmetric = -Clamp(pitch, -1f, 1f) * (outer ? 26f : 36f);
            float scale = Math.Min(1f, ElevonTravel / Math.Max(.001f, Math.Abs(symmetric) + Math.Abs(differential)));
            return (symmetric + differential) * scale;
        }
    }
}
