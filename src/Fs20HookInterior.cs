using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Legacy aft well and shared hook mesh builders.
    internal static class Fs20HookInterior
    {
        internal const float RoofY = -.075f;
        internal sealed class Shell
        {
            private sealed class Edge
            {
                internal Vector3 A, B;
                internal int Count;
            }

            private readonly Dictionary<string, Edge> edges = new Dictionary<string, Edge>();
            private readonly HashSet<string> polygons = new HashSet<string>();
            internal void AddSkinPolygon(Vector3[] p)
            {
                // Native skin carries coincident copies with different normals.
                // Count geometry once so those copies cannot erase the perimeter.
                if (!polygons.Add(string.Join("/", p.Select(Key).OrderBy(k => k, StringComparer.Ordinal))))
                    return;
                for (int i = 0; i < p.Length; i++)
                {
                    var a = p[i];
                    var b = p[(i + 1) % p.Length];
                    if ((a - b).sqrMagnitude < 1e-12f)
                        continue;
                    string ka = Key(a), kb = Key(b);
                    if (ka == kb)
                        continue;
                    string key = string.CompareOrdinal(ka, kb) < 0 ? ka + "/" + kb : kb + "/" + ka;
                    Edge edge;
                    if (edges.TryGetValue(key, out edge))
                        edge.Count++;
                    else
                        edges.Add(key, new Edge { A = a, B = b, Count = 1 });
                }
            }

            private static string Key(Vector3 p) => Mathf.RoundToInt(p.x * 100000) + "," + Mathf.RoundToInt(p.y * 100000) + "," + Mathf.RoundToInt(p.z * 100000);
            internal void Build(Transform root, Transform tail)
            {
                var mesh = new Parts();
                var outline = Fs20HookBayGeometry.Outline;
                Vector3 center = new Vector3(outline.Average(p => p.x), RoofY, outline.Average(p => p.y));
                for (int i = 0; i < outline.Length; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Length];
                    mesh.Triangle(center, new Vector3(a.x, RoofY, a.y), new Vector3(b.x, RoofY, b.y), new Vector2(center.x, center.z), a, b, true);
                }

                foreach (var edge in edges.Values.Where(e => e.Count == 1))
                {
                    var a = edge.A;
                    var b = edge.B;
                    var c = new Vector3(b.x, RoofY, b.z);
                    var d = new Vector3(a.x, RoofY, a.z);
                    float u = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                    mesh.Quad(a, b, c, d, new Vector2(0, a.y - RoofY), new Vector2(u, b.y - RoofY), new Vector2(u, 0), Vector2.zero, true);
                }

                var ob = mesh.Create("FS-20C sealed tail hook well", tail, NativeMetal(root));
                // Build coordinates are in aircraft space; bake into the tail part.
                var map = tail.worldToLocalMatrix * root.localToWorldMatrix;
                var result = ob.GetComponent<MeshFilter>().sharedMesh;
                result.vertices = result.vertices.Select(map.MultiplyPoint3x4).ToArray();
                result.RecalculateNormals();
                result.RecalculateTangents();
                result.RecalculateBounds();
                RegisterDamageRenderer(tail, ob.GetComponent<Renderer>());
            }
        }

        internal static void AddSupport(Transform hook, float rise = .495f, float radius = .075f, float bridgeDepth = .18f)
        {
            var mesh = new Parts();
            // Two cheeks leave a 0.185 m slot around the native 0.143 m head.
            var profile = new List<Vector2>
            {
                new Vector2(-radius, rise),
                new Vector2(radius, rise)
            };
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.PI * i / 12;
                profile.Add(new Vector2(radius * Mathf.Cos(a), -radius * Mathf.Sin(a)));
            }

            foreach (float x in new[]
            {
                -.105f,
                .105f
            }

            )
                mesh.PrismX(x - .0125f, x + .0125f, profile);
            mesh.Box(new Vector3(0, rise - .0175f, 0), new Vector3(.26f, .035f, bridgeDepth));
            // The pin intentionally passes through the native pivot boss.
            mesh.CylinderX(-.15f, .15f, .035f, 16);
            mesh.CylinderX(-.145f, -.13f, .041f, 16);
            mesh.CylinderX(.13f, .145f, .041f, 16);
            mesh.Create("FS-20C hook clevis and pivot pin", hook, NativeMetal(hook));
        }

        internal static Material NativeMetal(Transform aircraft)
        {
            var materials = aircraft.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials);
            return materials.FirstOrDefault(m => m != null && m.name.StartsWith("Gunmetal")) ?? materials.First(m => m != null && m.name.StartsWith("Matte_black"));
        }

        internal static void RegisterDamageRenderer(Transform part, Renderer renderer)
        {
            var field = Fs20Tuning.Field(typeof(UnitPart), "damageMaterial");
            var target = part.GetComponent<AeroPart>();
            var damage = (DamageMaterial)field.GetValue(target);
            if (damage.renderers == null)
                damage.renderers = new List<Renderer>();
            if (damage.renderers.Count == 0)
                damage.renderers.Add(part.GetComponent<Renderer>());
            damage.renderers.Add(renderer);
            var indices = damage.indices ?? Array.Empty<byte>();
            damage.indices = indices.Concat(Enumerable.Repeat((byte)0, Math.Max(0, damage.renderers.Count - indices.Length))).ToArray();
            field.SetValue(target, damage);
        }

        internal sealed class Parts
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, bool both = false)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-14f)
                    return;
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c });
                uv.AddRange(new[] { ua, ub, uc });
                triangles.AddRange(new[] { i, i + 1, i + 2 });
                if (both)
                    Triangle(c, b, a, uc, ub, ua);
            }

            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, bool both = false)
            {
                Triangle(a, b, c, ua, ub, uc, both);
                Triangle(a, c, d, ua, uc, ud, both);
            }

            internal void PrismX(float low, float high, List<Vector2> profile)
            {
                float area = 0;
                for (int i = 0; i < profile.Count; i++)
                {
                    var a = profile[i];
                    var b = profile[(i + 1) % profile.Count];
                    area += a.x * b.y - b.x * a.y;
                }

                if (area < 0)
                    profile = profile.AsEnumerable().Reverse().ToList();
                var center = profile.Aggregate(Vector2.zero, (a, b) => a + b) / profile.Count;
                for (int i = 0; i < profile.Count; i++)
                {
                    var a = profile[i];
                    var b = profile[(i + 1) % profile.Count];
                    Func<float, Vector2, Vector3> p = (x, v) => new Vector3(x, v.y, v.x);
                    Triangle(p(low, center), p(low, a), p(low, b), center, a, b);
                    Triangle(p(high, center), p(high, b), p(high, a), center, b, a);
                    Quad(p(low, a), p(high, a), p(high, b), p(low, b), Vector2.zero, new Vector2(high - low, 0), new Vector2(high - low, (b - a).magnitude), new Vector2(0, (b - a).magnitude));
                }
            }

            internal void Box(Vector3 center, Vector3 size)
            {
                var p = new List<Vector2>
                {
                    new Vector2(center.z - size.z / 2, center.y - size.y / 2),
                    new Vector2(center.z + size.z / 2, center.y - size.y / 2),
                    new Vector2(center.z + size.z / 2, center.y + size.y / 2),
                    new Vector2(center.z - size.z / 2, center.y + size.y / 2)
                };
                PrismX(center.x - size.x / 2, center.x + size.x / 2, p);
            }

            internal void CylinderX(float low, float high, float radius, int segments)
            {
                var profile = Enumerable.Range(0, segments).Select(i => new Vector2(radius * Mathf.Cos(2 * Mathf.PI * i / segments), radius * Mathf.Sin(2 * Mathf.PI * i / segments))).ToList();
                PrismX(low, high, profile);
            }

            internal GameObject Create(string name, Transform parent, Material material)
            {
                var packed = Fs20HookUv.Pack(uv.SelectMany(p => new[] { p.x, p.y }).ToArray());
                var mapped = Enumerable.Range(0, uv.Count).Select(i => new Vector2(packed[i * 2], packed[i * 2 + 1])).ToList();
                var mesh = new Mesh
                {
                    name = name + " UV mapped"
                };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, mapped);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                var ob = new GameObject(name);
                ob.transform.SetParent(parent, false);
                ob.AddComponent<MeshFilter>().sharedMesh = mesh;
                ob.AddComponent<MeshRenderer>().sharedMaterial = material;
                return ob;
            }
        }
    }
}
