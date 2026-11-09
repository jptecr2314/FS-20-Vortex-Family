using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed class Fs20VariantOverlayLayout
    {
        internal float X, Y, Width, Height, RowHeight;
        internal const float Margin = 16f, Header = 66f, Gap = 8f, Padding = 14f;
        internal static Fs20VariantOverlayLayout Fit(float left, float bottom, float right, float top, float tileLeft, float tileRight, float tileTop, int count, float tileWidth = 60, float tileHeight = 60)
        {
            if (count < 1 || right - left < 180 || top - bottom < 180)
                throw new ArgumentException("Variant overlay canvas is too small.");
            float width = Math.Min(count * tileWidth + 6 * (count - 1) + 12, right - left - 2 * Margin);
            float height = Math.Min(tileHeight + 12, top - bottom - 2 * Margin);
            float x = tileRight + 12;
            if (x + width > right - Margin)
                x = tileLeft - 12 - width;
            x = Math.Max(left + Margin, Math.Min(right - Margin - width, x));
            float y = Math.Max(bottom + Margin + height, Math.Min(top - Margin, tileTop));
            return new Fs20VariantOverlayLayout
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                RowHeight = (height - Header - Padding - Gap * (count - 1)) / count
            };
        }

        internal float RowTop(int index) => Header + index * (RowHeight + Gap);
    }
}
