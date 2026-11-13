using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Authored visual streamlines sampled from the actual upper skin, in part space.
    // Nothing here applies force or estimates pressure on the aircraft.
    [Serializable]
    internal sealed class Fs20TVaporPath
    {
        [Serializable] internal sealed class Lane
        {
            public Vector3[] Points, Normals;
            public float[] Widths;
            public float Length;
        }
        public Lane[] Lanes;
        internal struct Span { internal float Min, Max; }

        internal static Fs20TVaporPath Build(Mesh skin, Transform owner, Transform root)
        {
            var vertices = skin.vertices.Select(p => root.InverseTransformPoint(owner.TransformPoint(p))).ToArray();
            var indices = skin.triangles;
            float front = vertices.Max(p => p.z), rear = vertices.Min(p => p.z);
            var lanes = new List<Lane>();
            for (int laneIndex = 0; laneIndex < 9; laneIndex++)
            {
                float fraction = Mathf.Lerp(.1f,.9f,laneIndex/8f);
                var points = new List<Vector3>(); var normals = new List<Vector3>(); var widths = new List<float>();
                var bestPoints = new List<Vector3>(); var bestNormals = new List<Vector3>(); var bestWidths = new List<float>();
                Action finish = () =>
                {
                    if (points.Count > bestPoints.Count)
                    { bestPoints = new List<Vector3>(points); bestNormals = new List<Vector3>(normals); bestWidths = new List<float>(widths); }
                    points.Clear(); normals.Clear(); widths.Clear();
                };
                for (int station = 0; station < 49; station++)
                {
                    float z = Mathf.Lerp(front, rear, Mathf.Lerp(.015f, .985f, station / 48f));
                    var spans = Slice(vertices, indices, z);
                    if (spans.Count == 0) { finish(); continue; }
                    float target = Mathf.Lerp(spans[0].Min, spans[spans.Count - 1].Max, fraction);
                    var span = spans.OrderBy(s => Mathf.Abs(Mathf.Clamp(target, s.Min, s.Max) - target)).First();
                    float x = Mathf.Clamp(target, Mathf.Lerp(span.Min, span.Max, .08f), Mathf.Lerp(span.Min, span.Max, .92f));
                    if (!Height(vertices, indices, x, z, out float y, out Vector3 normal)) { finish(); continue; }
                    var point = new Vector3(x, y, z);
                    if (points.Count > 0 && Vector3.Distance(point, points[points.Count - 1]) > Mathf.Max(.25f, (front - rear) / 12f)) finish();
                    float width = Mathf.Min(.6f, Mathf.Min(x - span.Min, span.Max - x) * 1.25f);
                    if (points.Count > 0 && !ClearSegment(vertices, indices, points[points.Count - 1], point)) finish();
                    points.Add(point); normals.Add(normal);
                    widths.Add(width);
                }
                finish();
                if (bestPoints.Count < 2) continue;
                float length = 0f;
                for (int i = 1; i < bestPoints.Count; i++) length += Vector3.Distance(bestPoints[i - 1], bestPoints[i]);
                lanes.Add(new Lane {
                    Points = bestPoints.Select(p => owner.InverseTransformPoint(root.TransformPoint(p))).ToArray(),
                    Normals = bestNormals.Select(n => owner.InverseTransformVector(root.TransformVector(n)).normalized).ToArray(),
                    Widths = bestWidths.ToArray(), Length = length
                });
            }
            if (lanes.Count == 0) throw new InvalidOperationException("No continuous vapor path on " + owner.name);
            return new Fs20TVaporPath { Lanes = lanes.ToArray() };
        }

        internal static bool ClearSegment(Vector3[] vertices, int[] indices, Vector3 a, Vector3 b,float surfaceTolerance=.022f)
        {
            // Clip the whole segment against the triangle union. Point sampling alone
            // misses the tiny notches in some of the severely damaged skins.
            var coverage = new List<Span>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var p = vertices[indices[i]]; var q = vertices[indices[i + 1]]; var r = vertices[indices[i + 2]];
                float det = (q.z - r.z) * (p.x - r.x) + (r.x - q.x) * (p.z - r.z);
                if (Mathf.Abs(det) < 1e-8f) continue;
                Func<Vector3, Vector3> barycentric = v => {
                    float u = ((q.z - r.z) * (v.x - r.x) + (r.x - q.x) * (v.z - r.z)) / det;
                    float w = ((r.z - p.z) * (v.x - r.x) + (p.x - r.x) * (v.z - r.z)) / det;
                    return new Vector3(u, w, 1f - u - w);
                };
                var start = barycentric(a); var end = barycentric(b); float min = 0f, max = 1f;
                for (int axis = 0; axis < 3; axis++)
                {
                    float slope = end[axis] - start[axis];
                    if (Mathf.Abs(slope) < 1e-8f) { if (start[axis] < -.00001f) max = -1f; }
                    else if (slope > 0f) min = Mathf.Max(min, (-.00001f - start[axis]) / slope);
                    else max = Mathf.Min(max, (-.00001f - start[axis]) / slope);
                }
                if (max >= min) coverage.Add(new Span { Min = min, Max = max });
            }
            coverage.Sort((x, y) => x.Min.CompareTo(y.Min));
            float covered = 0f;
            foreach (var span in coverage)
            {
                if (span.Min > covered + .00001f) return false;
                covered = Mathf.Max(covered, span.Max);
            }
            if (covered < .99999f) return false;
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f;
                var point = Vector3.Lerp(a, b, t);
                if (!Height(vertices, indices, point.x, point.z, out float y, out _) || Mathf.Abs(point.y - y) > surfaceTolerance) return false;
            }
            return true;
        }

        internal static List<Span> Slice(Vector3[] vertices, int[] indices, float z)
        {
            var spans = new List<Span>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                for (int edge = 0; edge < 3; edge++)
                {
                    var a = vertices[indices[i + edge]]; var b = vertices[indices[i + (edge + 1) % 3]];
                    if (Mathf.Abs(a.z - b.z) < .000001f || z < Mathf.Min(a.z, b.z) || z > Mathf.Max(a.z, b.z)) continue;
                    float x = Mathf.Lerp(a.x, b.x, (z - a.z) / (b.z - a.z));
                    min = Mathf.Min(min, x); max = Mathf.Max(max, x);
                }
                if (max > min + .000001f) spans.Add(new Span { Min = min, Max = max });
            }
            spans.Sort((a, b) => a.Min.CompareTo(b.Min));
            var merged = new List<Span>();
            foreach (var span in spans)
            {
                if (merged.Count == 0 || span.Min > merged[merged.Count - 1].Max + .0002f) merged.Add(span);
                else { var previous = merged[merged.Count - 1]; previous.Max = Mathf.Max(previous.Max, span.Max); merged[merged.Count - 1] = previous; }
            }
            return merged;
        }

        // Vertical barycentric projection, used only while building the prototype.
        internal static bool Height(Vector3[] vertices, int[] indices, float x, float z, out float y, out Vector3 normal)
        {
            y = float.NegativeInfinity; normal = Vector3.up;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]]; var b = vertices[indices[i + 1]]; var c = vertices[indices[i + 2]];
                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < .00000001f) continue;
                float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / det;
                float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / det;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                float height = u * a.y + v * b.y + (1f - u - v) * c.y;
                if (height > y) { y = height; normal = Vector3.Cross(b - a, c - a).normalized; }
            }
            return !float.IsNegativeInfinity(y);
        }

        internal void Sample(uint seed, float age, Vector3 airflow, float strength,
            out Vector3 position, out Quaternion rotation, out Vector3 size)
        {
            var lane = Lanes[(int)(seed % (uint)Lanes.Length)];
            // Stagger formation along the first part of the surface, so every
            // particle does not reach peak opacity at the same physical station.
            float entry = ((seed >> 16) & 65535u) / 65535f * .22f;
            float along = Mathf.Lerp(entry,1f,Mathf.Clamp01(age/.86f)) * (lane.Points.Length - 1);
            int index = Mathf.Min(lane.Points.Length - 2, (int)along); float t = along - index;
            var point = Vector3.Lerp(lane.Points[index], lane.Points[index + 1], t);
            var normal = Vector3.Slerp(lane.Normals[index], lane.Normals[index + 1], t).normalized;
            var tangent = Vector3.ProjectOnPlane(lane.Points[index + 1] - lane.Points[index], normal).normalized;
            var side = Vector3.Cross(tangent, normal).normalized;
            float width = Mathf.Lerp(lane.Widths[index], lane.Widths[index + 1], t);
            float sideslip = Mathf.Clamp(Vector3.Dot(airflow.normalized, side), -.5f, .5f);
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.86f, 1f, age));
            // Most of the lifetime hugs the skin. Only the fading tail leaves it.
            position = point + normal * (release * Mathf.Lerp(.025f, .06f, strength)) +
                tangent * release * Mathf.Min(.4f, lane.Length * .12f);
            var heading = Vector3.ProjectOnPlane(-airflow, normal).normalized;
            rotation = Quaternion.LookRotation(normal, Vector3.Slerp(tangent, heading, Mathf.Abs(sideslip) * .15f));
            float length = Mathf.Clamp(lane.Length * .32f, .25f, 1.15f);
            float variation = Mathf.Lerp(.7f,1.1f,((seed >> 7) & 511u)/511f);
            size = new Vector3(Mathf.Max(.012f, width * Mathf.Lerp(.8f, 1.1f, strength)*variation), length*.6f, .012f);
        }

        internal static Mesh Sheet()
        {
            var mesh = new Mesh { name = "FS20T vapor surface ribbon" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0f), new Vector3(.5f,-.5f,0f), new Vector3(.5f,.5f,0f), new Vector3(-.5f,.5f,0f) };
            mesh.uv = new[] { new Vector2(0f,0f), new Vector2(1f,0f), new Vector2(1f,1f), new Vector2(0f,1f) };
            mesh.triangles = new[] { 0,1,2, 0,2,3, 2,1,0, 3,2,0 };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.tangents = Enumerable.Repeat(new Vector4(1f, 0f, 0f, 1f), 4).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
