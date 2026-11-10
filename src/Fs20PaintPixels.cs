using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20PaintPixels
    {
        // Top-left coordinates in the native 2048 atlas. All three rectangles
        // are inside the painted panel perimeter, clear of neighbouring labels.
        internal static readonly int[][] Regions =
        {
            new[] { 60, 268, 144, 334 },
            new[] { 1655, 254, 1741, 321 },
            new[] { 592, 1226, 643, 1278 }
        };

        internal static byte[] Clean(byte[] source, int width, int height)
        {
            if (source == null || width < 16 || height < 16 || source.Length != width * height * 4)
                throw new ArgumentException("RGBA aircraft atlas required.");
            var result = (byte[])source.Clone();
            foreach (var region in Regions)
            {
                int x0 = Math.Max(1, region[0] * width / 2048), x1 = Math.Min(width - 2, (region[2] * width + 2047) / 2048);
                int y0 = Math.Max(1, region[1] * height / 2048), y1 = Math.Min(height - 2, (region[3] * height + 2047) / 2048);
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float u = (x - x0 + 1f) / (x1 - x0 + 1f), v = (y - y0 + 1f) / (y1 - y0 + 1f);
                    for (int c = 0; c < 4; c++)
                    {
                        // Interpolate the existing paint from all four edges.
                        // Subtract the corner blend to keep boundary colours.
                        float vertical = At(source, width, height, x, y0 - 1, c) * (1 - v) + At(source, width, height, x, y1, c) * v;
                        float horizontal = At(source, width, height, x0 - 1, y, c) * (1 - u) + At(source, width, height, x1, y, c) * u;
                        float top = At(source, width, height, x0 - 1, y0 - 1, c) * (1 - u) + At(source, width, height, x1, y0 - 1, c) * u;
                        float bottom = At(source, width, height, x0 - 1, y1, c) * (1 - u) + At(source, width, height, x1, y1, c) * u;
                        int value = (int)Math.Round(vertical + horizontal - top * (1 - v) - bottom * v);
                        result[((height - 1 - y) * width + x) * 4 + c] = (byte)Math.Max(0, Math.Min(255, value));
                    }
                }
            }
            return result;
        }
        private static int At(byte[] pixels, int width, int height, int x, int y, int channel) =>
            pixels[((height - 1 - y) * width + x) * 4 + channel];
    }
}
