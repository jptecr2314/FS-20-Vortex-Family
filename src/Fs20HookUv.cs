using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20HookUv
    {
        // Pack metric islands into 0..1 with a shared scale and padding.
        internal static float[] Pack(float[] metric)
        {
            if (metric.Length % 6 != 0)
                throw new ArgumentException("Triangle UV pairs required.");
            int count = metric.Length / 6, grid = (int)Math.Ceiling(Math.Sqrt(count));
            var packed = new float[metric.Length];
            if (count == 0)
                return packed;
            float span = .000001f;
            for (int i = 0; i < count; i++)
                for (int axis = 0; axis < 2; axis++)
                {
                    float a = metric[i * 6 + axis], b = metric[i * 6 + 2 + axis], c = metric[i * 6 + 4 + axis];
                    span = Math.Max(span, Math.Max(a, Math.Max(b, c)) - Math.Min(a, Math.Min(b, c)));
                }

            float cell = 1f / grid, scale = cell * .9f / span;
            for (int i = 0; i < count; i++)
                for (int axis = 0; axis < 2; axis++)
                {
                    float min = Math.Min(metric[i * 6 + axis], Math.Min(metric[i * 6 + 2 + axis], metric[i * 6 + 4 + axis]));
                    float offset = ((axis == 0 ? i % grid : i / grid) + .05f) * cell;
                    for (int v = 0; v < 3; v++)
                        packed[i * 6 + 2 * v + axis] = offset + (metric[i * 6 + 2 * v + axis] - min) * scale;
                }

            return packed;
        }
    }
}
