namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed class Fs20VtolSpec
    {
        internal readonly string Path, Material;
        internal readonly int RemovedTriangles;
        internal readonly Fs20IslandRule[] Rules;
        private Fs20VtolSpec(string path, string material, int triangles, params Fs20IslandRule[] rules)
        {
            Path = path;
            Material = material;
            RemovedTriangles = triangles;
            Rules = rules;
        }
        internal static Fs20VtolSpec[] All()
        {
            return new[] {
                new Fs20VtolSpec("liftFan", "jet_mechanical", 22,
                    new Fs20IslandRule(22, -.07137f, .07137f, -.31858f, -.17583f, 1.02657f, 2.29120f)),
                new Fs20VtolSpec("liftFan", "aircraft_structure", 460),
                new Fs20VtolSpec("", "jet_mechanical", 28,
                    new Fs20IslandRule(28, -.07137f, .07137f, -.31858f, -.17583f, -.57777f, 1.05735f)),
                new Fs20VtolSpec("engine", "aircraft_structure", 128,
                    new Fs20IslandRule(64, -1.05390f, -.49619f, -.37133f, -.09575f, -2.55346f, -2.03681f),
                    new Fs20IslandRule(64, .49619f, 1.05390f, -.37133f, -.09575f, -2.55346f, -2.03681f)),
                new Fs20VtolSpec("engine/wing1b_L", "aircraft_structure", 176,
                    new Fs20IslandRule(176, -2.42960f, -.66979f, -.22796f, .19339f, -2.59993f, -2.02902f)),
                new Fs20VtolSpec("engine/wing1b_R", "aircraft_structure", 176,
                    new Fs20IslandRule(176, .66979f, 2.42960f, -.22796f, .19339f, -2.59993f, -2.02902f)),
                new Fs20VtolSpec("wing1a_L/wing2a_L", "aircraft_structure", 96,
                    new Fs20IslandRule(96, -3.51078f, -2.38321f, -.32382f, -.07180f, -2.35911f, -2.02902f)),
                new Fs20VtolSpec("wing1a_R/wing2a_R", "aircraft_structure", 96,
                    new Fs20IslandRule(96, 2.38321f, 3.51078f, -.32382f, -.07180f, -2.35911f, -2.02902f))
            };
        }
    }
}
