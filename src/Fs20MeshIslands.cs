using System;
using System.Collections.Generic;

namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed class Fs20IslandRule
    {
        internal readonly int Triangles;
        internal readonly float[] Bounds;
        internal Fs20IslandRule(int triangles, params float[] bounds)
        {
            Triangles = triangles;
            Bounds = bounds;
        }
    }

    internal static class Fs20MeshIslands
    {
        private struct PointKey : IEquatable<PointKey>
        {
            private readonly int x, y, z;
            internal PointKey(float[] points, int index)
            {
                x = (int)Math.Round(points[index * 3] * 10000);
                y = (int)Math.Round(points[index * 3 + 1] * 10000);
                z = (int)Math.Round(points[index * 3 + 2] * 10000);
            }
            public bool Equals(PointKey other) => x == other.x && y == other.y && z == other.z;
            public override bool Equals(object obj) => obj is PointKey && Equals((PointKey)obj);
            public override int GetHashCode() => unchecked((x * 397 ^ y) * 397 ^ z);
        }

        private static int Find(int[] parents, int i)
        {
            while (parents[i] != i)
            {
                parents[i] = parents[parents[i]];
                i = parents[i];
            }
            return i;
        }

        internal static int[] Remove(float[] points, int[] indices, Fs20IslandRule[] rules)
        {
            int count = indices.Length / 3;
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            var shared = new Dictionary<PointKey, int>();
            for (int i = 0; i < count; i++)
                for (int k = 0; k < 3; k++)
                {
                    var key = new PointKey(points, indices[i * 3 + k]);
                    int first;
                    if (shared.TryGetValue(key, out first)) parent[Find(parent, i)] = Find(parent, first);
                    else shared.Add(key, i);
                }
            var islands = new Dictionary<int, List<int>>();
            for (int i = 0; i < count; i++)
            {
                int root = Find(parent, i);
                List<int> faces;
                if (!islands.TryGetValue(root, out faces)) islands.Add(root, faces = new List<int>());
                faces.Add(i);
            }
            var removed = new bool[count];
            foreach (var rule in rules)
            {
                List<int> match = null;
                foreach (var faces in islands.Values)
                {
                    if (faces.Count != rule.Triangles) continue;
                    var bounds = new[] { float.PositiveInfinity, float.NegativeInfinity,
                        float.PositiveInfinity, float.NegativeInfinity, float.PositiveInfinity, float.NegativeInfinity };
                    foreach (int face in faces)
                        for (int k = 0; k < 3; k++)
                            for (int axis = 0; axis < 3; axis++)
                            {
                                float v = points[indices[face * 3 + k] * 3 + axis];
                                bounds[axis * 2] = Math.Min(bounds[axis * 2], v);
                                bounds[axis * 2 + 1] = Math.Max(bounds[axis * 2 + 1], v);
                            }
                    bool same = true;
                    for (int i = 0; i < 6; i++) same &= Math.Abs(bounds[i] - rule.Bounds[i]) < .002f;
                    if (!same) continue;
                    if (match != null) throw new InvalidOperationException("Ambiguous FS-20 VTOL mesh island.");
                    match = faces;
                }
                if (match == null) throw new InvalidOperationException("FS-20 VTOL mesh layout changed.");
                foreach (int face in match) removed[face] = true;
            }
            var kept = new List<int>();
            for (int i = 0; i < count; i++)
                if (!removed[i])
                    for (int k = 0; k < 3; k++) kept.Add(indices[i * 3 + k]);
            return kept.ToArray();
        }
    }
}
