using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    // Legacy aft-bay cutter; the current installation uses Fs20VentralHookBay.
    internal static class Fs20HookBayGeometry
    {
        internal static readonly Vector3 Mount = new Vector3(-0.95f, -0.57f, -3.97f);
        internal static readonly Vector2[] Outline =
        {
            new Vector2(-1.10f, -3.86f),
            new Vector2(-0.80f, -3.86f),
            new Vector2(-0.76f, -4.02f),
            new Vector2(-0.76f, -5.91f),
            new Vector2(-0.82f, -6.06f),
            new Vector2(-1.08f, -6.06f),
            new Vector2(-1.14f, -5.91f),
            new Vector2(-1.14f, -4.02f)
        };
        internal static Fs20HookBay Build(Transform root)
        {
            var tail = root.Find("engine/tail_L");
            var cavity = new Fs20HookInterior.Shell();
            var bay = tail.gameObject.AddComponent<Fs20HookBay>();
            bay.Cover = new GameObject("Conformal tail hook hatch hinge").transform;
            bay.Cover.SetParent(tail, false);
            bay.Cover.position = root.TransformPoint(new Vector3(-1.15f, 0.05f, -3.90f));
            bay.Cover.rotation = root.rotation * Quaternion.Euler(9.77f, 0, 0);
            bay.ClosedRotation = bay.Cover.localRotation;
            bay.ClosedPosition = bay.Cover.localPosition;
            bay.DropOffset = tail.InverseTransformVector(root.TransformVector(Vector3.down * 0.15f));
            foreach (string path in new[]
            {
                "engine/tail_L",
                "engine/fuselage_R"
            }

            )
            {
                var branch = root.Find(path);
                foreach (var filter in branch.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.transform != branch && !filter.name.StartsWith(branch.name + "_LOD"))
                        continue;
                    if (filter.sharedMesh == null)
                        continue;
                    var source = Fs20Geometry.ReadableCopy(filter.sharedMesh);
                    var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var outside = new Builder(source);
                    var inside = new Builder(source);
                    var rim = new Builder(source);
                    int removed = 0;
                    for (int sub = 0; sub < source.subMeshCount; sub++)
                    {
                        var triangles = source.GetTriangles(sub);
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            var polygon = new List<Vertex>
                            {
                                outside.Read(triangles[i]),
                                outside.Read(triangles[i + 1]),
                                outside.Read(triangles[i + 2])
                            };
                            // The lower bay volume opens; its outward skin becomes the hatch.
                            foreach (var plane in Planes())
                            {
                                List<Vertex> keep, cut;
                                Split(polygon, toRoot, plane, out keep, out cut);
                                outside.Add(cut, sub);
                                polygon = keep;
                                if (polygon.Count < 3)
                                    break;
                            }

                            if (polygon.Count >= 3)
                            {
                                if (sub == 0)
                                {
                                    if (filter.transform == branch)
                                        cavity.AddSkinPolygon(polygon.Select(v => toRoot.MultiplyPoint3x4(v.P)).ToArray());
                                    var remainder = polygon;
                                    foreach (var p in Planes())
                                    {
                                        var inset = p;
                                        inset.w -= 0.008f * new Vector3(p.x, p.y, p.z).magnitude;
                                        List<Vertex> keep, strip;
                                        Split(remainder, toRoot, inset, out keep, out strip);
                                        rim.Add(strip, sub);
                                        remainder = keep;
                                    }

                                    // A two-millimetre seam clears the fixed cut edge.
                                    foreach (var p in Planes())
                                    {
                                        var inset = p;
                                        inset.w -= 0.002f * new Vector3(p.x, p.y, p.z).magnitude;
                                        List<Vertex> keep, discard;
                                        Split(polygon, toRoot, inset, out keep, out discard);
                                        polygon = keep;
                                    }

                                    inside.Add(polygon, sub);
                                }

                                removed++;
                            }
                        }
                    }

                    if (removed == 0)
                    {
                        UnityEngine.Object.Destroy(source);
                        continue;
                    }

                    filter.sharedMesh = outside.Finish(source.name + "_hook_bay");
                    // LODs receive the same opening; high-detail skin supplies the hatch.
                    if (filter.transform == branch)
                    {
                        var door = new GameObject(branch.name + " conformal hatch skin");
                        door.transform.SetParent(bay.Cover, false);
                        var mesh = inside.Finish(source.name + "_hook_hatch");
                        TransformMesh(mesh, bay.Cover.worldToLocalMatrix * filter.transform.localToWorldMatrix);
                        door.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = door.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials;
                        var lip = new GameObject(branch.name + " internal hatch seal");
                        lip.transform.SetParent(filter.transform, false);
                        var lipMesh = rim.Finish(source.name + "_hook_seal");
                        var lp = lipMesh.vertices;
                        var ln = lipMesh.normals;
                        for (int j = 0; j < lp.Length; j++)
                            lp[j] -= ln[j].normalized * 0.004f;
                        lipMesh.vertices = lp;
                        lipMesh.RecalculateBounds();
                        lip.AddComponent<MeshFilter>().sharedMesh = lipMesh;
                        var lipRenderer = lip.AddComponent<MeshRenderer>();
                        lipRenderer.sharedMaterials = renderer.sharedMaterials;
                        // Native livery and damage updates also include the moving skin.
                        var part = branch.GetComponent<AeroPart>();
                        var damageField = Fs20Tuning.Field(typeof(UnitPart), "damageMaterial");
                        var damage = (DamageMaterial)damageField.GetValue(part);
                        if (damage.renderers == null)
                            damage.renderers = new List<Renderer>();
                        // UnitPart.Awake only adds its own renderer when the list is
                        // empty. Retain that default before registering the hatch.
                        if (damage.renderers.Count == 0 && branch.GetComponent<Renderer>() != null)
                            damage.renderers.Add(branch.GetComponent<Renderer>());
                        damage.renderers.Add(renderer);
                        damage.renderers.Add(lipRenderer);
                        var originalIndices = damage.indices ?? Array.Empty<byte>();
                        damage.indices = originalIndices.Concat(Enumerable.Repeat((byte)0, Math.Max(0, damage.renderers.Count - originalIndices.Length))).ToArray();
                        damageField.SetValue(part, damage);
                    }

                    UnityEngine.Object.Destroy(source);
                }
            }

            if (bay.Cover.childCount == 0)
                throw new InvalidOperationException("No tail hook hatch skin was extracted.");
            cavity.Build(root, tail);
            return bay;
        }

        private static IEnumerable<Vector4> Planes()
        {
            // Clockwise X/Z outline; positive signed distance is inside.
            for (int i = 0; i < Outline.Length; i++)
            {
                var a = Outline[i];
                var b = Outline[(i + 1) % Outline.Length];
                var edge = b - a;
                yield return new Vector4(edge.y, 0, -edge.x, -edge.y * a.x + edge.x * a.y);
            }

            yield return new Vector4(0, -1, 0, -0.10f); // y <= -0.10 m
        }

        internal static void Split(List<Vertex> input, Matrix4x4 map, Vector4 plane, out List<Vertex> positive, out List<Vertex> negative)
        {
            positive = new List<Vertex>();
            negative = new List<Vertex>();
            if (input.Count == 0)
                return;
            for (int i = 0; i < input.Count; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Count];
                var p = map.MultiplyPoint3x4(a.P);
                var q = map.MultiplyPoint3x4(b.P);
                float da = Vector4.Dot(plane, new Vector4(p.x, p.y, p.z, 1));
                float db = Vector4.Dot(plane, new Vector4(q.x, q.y, q.z, 1));
                if (da >= 0)
                    positive.Add(a);
                else
                    negative.Add(a);
                if ((da >= 0) != (db >= 0))
                {
                    var cross = Vertex.Lerp(a, b, da / (da - db));
                    positive.Add(cross);
                    negative.Add(cross);
                }
            }
        }

        internal static void TransformMesh(Mesh mesh, Matrix4x4 matrix)
        {
            mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var normalMatrix = matrix.inverse.transpose;
            mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            mesh.tangents = mesh.tangents.Select(t =>
            {
                var d = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                return new Vector4(d.x, d.y, d.z, t.w);
            }).ToArray();
            mesh.RecalculateBounds();
        }

        internal sealed class Vertex
        {
            internal Vector3 P, N;
            internal Vector4 T;
            internal Color C;
            internal Vector4[] U;
            internal static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            {
                P = Vector3.LerpUnclamped(a.P, b.P, t),
                N = Vector3.LerpUnclamped(a.N, b.N, t),
                T = Vector4.LerpUnclamped(a.T, b.T, t),
                C = Color.LerpUnclamped(a.C, b.C, t),
                U = a.U.Zip(b.U, (x, y) => Vector4.LerpUnclamped(x, y, t)).ToArray()
            };
        }

        internal sealed class Builder
        {
            private readonly Vector3[] p, n;
            private readonly Vector4[] t;
            private readonly Color[] c;
            private readonly List<Vector4>[] uv;
            private readonly List<Vertex> vertices = new List<Vertex>();
            private readonly List<int>[] indices;
            internal Builder(Mesh source)
            {
                p = source.vertices;
                n = source.normals;
                t = source.tangents;
                c = source.colors;
                uv = Enumerable.Range(0, 8).Select(i =>
                {
                    var list = new List<Vector4>();
                    source.GetUVs(i, list);
                    return list;
                }).ToArray();
                indices = Enumerable.Range(0, source.subMeshCount).Select(i => new List<int>()).ToArray();
            }

            internal Vertex Read(int i) => new Vertex
            {
                P = p[i],
                N = n.Length == p.Length ? n[i] : Vector3.up,
                T = t.Length == p.Length ? t[i] : new Vector4(1, 0, 0, 1),
                C = c.Length == p.Length ? c[i] : Color.white,
                U = uv.Select(u => u.Count == p.Length ? u[i] : Vector4.zero).ToArray()
            };
            internal void Add(List<Vertex> poly, int sub)
            {
                if (poly.Count < 3)
                    return;
                int start = vertices.Count;
                vertices.AddRange(poly);
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    indices[sub].Add(start);
                    indices[sub].Add(start + i);
                    indices[sub].Add(start + i + 1);
                }
            }

            internal Mesh Finish(string name)
            {
                var mesh = new Mesh
                {
                    name = name,
                    indexFormat = IndexFormat.UInt32
                };
                mesh.SetVertices(vertices.Select(v => v.P).ToList());
                mesh.SetNormals(vertices.Select(v => v.N).ToList());
                mesh.SetTangents(vertices.Select(v => v.T).ToList());
                if (c.Length == p.Length)
                    mesh.SetColors(vertices.Select(v => v.C).ToList());
                for (int channel = 0; channel < 8; channel++)
                    if (uv[channel].Count == p.Length)
                        mesh.SetUVs(channel, vertices.Select(v => v.U[channel]).ToList());
                mesh.subMeshCount = indices.Length;
                for (int sub = 0; sub < indices.Length; sub++)
                    mesh.SetTriangles(indices[sub], sub);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    public sealed class Fs20HookBay : MonoBehaviour
    {
        [SerializeField]
        internal Transform Cover;
        [SerializeField]
        internal Quaternion ClosedRotation;
        [SerializeField]
        internal Vector3 ClosedPosition;
        [SerializeField]
        internal Vector3 DropOffset;
        [SerializeField]
        internal float OpeningDegrees = 20f;
    }
}
