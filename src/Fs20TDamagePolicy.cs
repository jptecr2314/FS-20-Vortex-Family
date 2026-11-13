using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TDamagePolicy
    {
        internal const int Lost = 3;
        internal static int State(float hitPoints, bool fragmented)
        {
            if (fragmented) return Lost;
            return hitPoints <= 33f ? 2 : hitPoints <= 68f ? 1 : 0;
        }

        internal static float Fraction(float regionArea, float totalArea)
        {
            if (!(regionArea > 0) || !(totalArea >= regionArea) || float.IsInfinity(totalArea)) throw new ArgumentOutOfRangeException();
            return regionArea / totalArea;
        }
    }
}
