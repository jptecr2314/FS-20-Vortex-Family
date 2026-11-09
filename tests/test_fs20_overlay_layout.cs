using System;
using System.Globalization;
using System.IO;
using Japensou.NuclearOption.VortexConventional;

internal static class Fs20OverlayLayoutChecks
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok)
            throw new Exception(message);
    }

    private static void Main(string[] args)
    {
        float[, ] displays =
        {
            {
                1024,
                576
            },
            {
                1280,
                720
            },
            {
                1920,
                1080
            },
            {
                2560,
                1080
            },
            {
                2560,
                1440
            },
            {
                3840,
                2160
            },
            {
                720,
                1280
            }
        };
        int states = 0;
        for (int display = 0; display < displays.GetLength(0); display++)
            foreach (float scale in new[]
            {
                .75f,
                1f,
                1.5f,
                2f
            }

            )
                foreach (float tileX in new[]
                {
                    -.95f,
                    0f,
                    .95f
                }

                )
                    foreach (float tileY in new[]
                    {
                        -.95f,
                        0f,
                        .95f
                    }

                    )
                    {
                        float w = displays[display, 0] / scale, h = displays[display, 1] / scale;
                        float x = tileX * w / 2, y = tileY * h / 2;
                        var p = Fs20VariantOverlayLayout.Fit(-w / 2, -h / 2, w / 2, h / 2, x - 30, x + 30, y + 30, 3);
                        Check(p.X >= -w / 2 + 15.99 && p.X + p.Width <= w / 2 - 15.99, "Panel fits canvas horizontally");
                        Check(p.Y <= h / 2 - 15.99 && p.Y - p.Height >= -h / 2 + 15.99, "Panel fits canvas vertically");
                        Check(p.Width >= 204, "Overlay is wider than the native 60-unit grid tile");
                        float tile = (p.Width - 24) / 3;
                        Check(tile >= 60, "Native labels have space in each tile");
                        for (int i = 0; i < 3; i++)
                        {
                            float start = 6 + i * (tile + 6);
                            Check(start + tile <= p.Width - 5.99, "Native tile fits panel");
                            Check(6 + 60 <= p.Height - 5.99, "Native tile fits vertically");
                        }

                        states++;
                    }

        var sample = Fs20VariantOverlayLayout.Fit(-640, -360, 640, 360, 400, 460, 300, 3);
        Check(sample.X < 400, "Right-edge tile opens its panel to the left");
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(Path.GetDirectoryName(args[0]));
        File.WriteAllText(args[0], "{\"scope\":\"Offline canvas geometry; not a Unity screenshot or interaction test\",\"states\":" + states + ",\"checks\":" + checks + ",\"sample\":{\"canvasWidth\":1280,\"canvasHeight\":720,\"x\":" + sample.X + ",\"y\":" + sample.Y + ",\"width\":" + sample.Width + ",\"height\":" + sample.Height + ",\"rowHeight\":" + sample.RowHeight + "}}");
        Console.WriteLine("FS20_OVERLAY_LAYOUT_PASS states=" + states + " checks=" + checks + "; offline geometry only; no game opened");
    }
}
