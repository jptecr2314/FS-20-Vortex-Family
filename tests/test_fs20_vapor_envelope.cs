using System;
using Japensou.NuclearOption.VortexConventional;

internal static class Fs20VaporChecks
{
    private static int checks;
    private static void Check(bool ok, string label)
    {
        checks++;
        if (!ok)
            throw new Exception(label);
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
    private static void Tick(Fs20VaporEnvelope e, float alpha = 15, float g = 8, float speed = 180, float density = 1.225f, float altitude = 100, float detail = 1, float roll = 0, float dt = .02f) => e.Step(speed, density, alpha, g, altitude, detail, roll, dt);
    private static Fs20VaporEnvelope Settled()
    {
        var e = new Fs20VaporEnvelope();
        for (int i = 0; i < 20; i++)
            Tick(e);
        return e;
    }

    private static void Main()
    {
        Check(Near(Fs20VaporEnvelope.Target(200, 1.225f, 0, 0, 100, 1), 0), "straight unloaded cruise is clear");
        Check(Near(Fs20VaporEnvelope.Target(0, 1.225f, 20, 11, 100, 1), 0), "parked aircraft never emits");
        Check(Near(Fs20VaporEnvelope.Target(200, 1.225f, 20, 11, 0, 1), 0), "ground state suppressed");
        Check(Near(Fs20VaporEnvelope.Target(200, 1.225f, 20, 11, 100, 0), 0), "distant-detail emission suppressed");
        Check(Near(Fs20VaporEnvelope.Target(200, 0, 20, 11, 100, 1), 0), "no vapor without air density");
        Check(Fs20VaporEnvelope.Target(180, 1.225f, 2, 8, 100, 1) > .95f, "strong turn load forms vapor without extreme AoA");
        var e = new Fs20VaporEnvelope();
        Tick(e);
        Check(e.Strength > 0 && e.Strength <= .0801f, "bounded rapid formation");
        e = Settled();
        Tick(e, roll: 2);
        float l = e.Left, r = e.Right;
        Check(l > r, "roll produces asymmetric loading accent");
        Tick(e, roll: -2);
        Check(Near(e.Left, r) && Near(e.Right, l), "roll accent mirrors across wings");
        Tick(e, alpha: 0, g: 0);
        Check(e.Strength < 1 && e.Strength > 0, "unloading fades rather than pops");
        for (int i = 0; i < 20; i++)
            Tick(e, alpha: 0, g: 0);
        Check(Near(e.Strength, 0) && Near(e.Left, 0) && Near(e.Right, 0), "unloaded vapor fully disappears");
        foreach (float speed in new[]
        {
            0f,
            60f,
            90f,
            140f,
            250f,
            550f
        }

        )
            foreach (float density in new[]
            {
                0f,
                .1f,
                .3f,
                .7f,
                1.225f
            }

            )
                foreach (float alpha in new[]
                {
                    -20f,
                    0f,
                    6f,
                    12f,
                    24f,
                    50f
                }

                )
                    foreach (float g in new[]
                    {
                        -5f,
                        0f,
                        3f,
                        8f,
                        11f,
                        20f
                    }

                    )
                    {
                        float value = Fs20VaporEnvelope.Target(speed, density, alpha, g, 100, 1);
                        Check(!float.IsNaN(value) && value >= 0 && value <= 1, "finite bounded target");
                    }

        Console.WriteLine("FS20_VAPOR_POLICY_PASS assertions=" + checks + "; offline response only, not rendered game appearance");
    }
}
