using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20Geometry
    {
        private const float RootStation = 2.12f;
        private const float NativeHalfSpan = 4.6735f;
        private const float TipExtension = (NativeHalfSpan - 0.60f) * 0.25f;
        internal const float Span = 2f * (NativeHalfSpan + TipExtension);
        internal static readonly string[] WingPaths =
        {
            "liftFan/wingRoot_L",
            "liftFan/wingRoot_R",
            "wing1a_L",
            "wing1a_R",
            "engine/wing1b_L",
            "engine/wing1b_R"
        };
        internal static Vector3 Widen(Vector3 point)
        {
            float t = Mathf.InverseLerp(RootStation, NativeHalfSpan, Mathf.Abs(point.x));
            point.x += Mathf.Sign(point.x) * TipExtension * t * t * (3f - 2f * t);
            return point;
        }

        internal static Vector3 ExtendFin(Vector3 point)
        {
            float height = Mathf.Clamp01(point.y / 1.75f);
            float leading = -3.687f - 1.38f * height;
            float hinge = -5.03f - 0.60f * height;
            float chord = Mathf.Clamp01((point.z - hinge) / (leading - hinge));
            point.z += 1.62f * (1f - height) * chord * chord;
            return point;
        }

        internal static void Apply(Transform root)
        {
            foreach (string path in WingPaths)
            {
                var branch = root.Find(path);
                if (branch == null || branch.GetComponent<AeroPart>() == null)
                    throw new InvalidOperationException("FS-20C wing layout changed: " + path);
                Deform(root, branch, Widen, true);
            }

            foreach (string side in new[]
            {
                "L",
                "R"
            }

            )
            {
                var fin = root.Find("engine/tail_" + side + "/vstab_" + side);
                if (fin == null)
                    throw new InvalidOperationException("FS-20C fin missing: " + side);
                // The rudder and its hinges keep the original aft edge.
                Deform(root, fin, ExtendFin, false);
            }
        }

        private sealed class MeshState
        {
            internal Transform Transform;
            internal MeshFilter Filter;
            internal MeshCollider Collider;
            internal Mesh Source;
            internal Matrix4x4 LocalToAircraft;
            internal Func<Vector3, Vector3> Map;
        }

        internal static Mesh ReadableCopy(Mesh source)
        {
            if (source.isReadable)
                return UnityEngine.Object.Instantiate(source);
            var copy = new Mesh
            {
                name = source.name + "_readable"
            };
            copy.SetVertexBufferParams(source.vertexCount, source.GetVertexAttributes());
            for (int stream = 0; stream < source.vertexBufferCount; stream++)
            {
                using (var buffer = source.GetVertexBuffer(stream))
                {
                    var data = new byte[buffer.count * buffer.stride];
                    buffer.GetData(data);
                    copy.SetVertexBufferData(data, 0, 0, data.Length, stream);
                }
            }

            using (var buffer = source.GetIndexBuffer())
            {
                copy.SetIndexBufferParams(buffer.count, source.indexFormat);
                if (source.indexFormat == IndexFormat.UInt16)
                {
                    var indices = new ushort[buffer.count];
                    buffer.GetData(indices);
                    copy.SetIndexBufferData(indices, 0, 0, indices.Length);
                }
                else
                {
                    var indices = new uint[buffer.count];
                    buffer.GetData(indices);
                    copy.SetIndexBufferData(indices, 0, 0, indices.Length);
                }
            }

            copy.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
                copy.SetSubMesh(i, source.GetSubMesh(i));
            copy.bounds = source.bounds;
            return copy;
        }

        private static void Deform(Transform root, Transform branch, Func<Vector3, Vector3> map, bool wing)
        {
            var transforms = branch.GetComponentsInChildren<Transform>(true).Where(t => wing || !HasAncestorPrefix(t, branch, "rudder_")).ToArray();
            var originals = transforms.ToDictionary(t => t, t => root.worldToLocalMatrix * t.localToWorldMatrix);
            var states = new List<MeshState>();
            var areaRatios = new Dictionary<AeroPart, float>();
            foreach (Transform transform in transforms)
            {
                Matrix4x4 matrix = originals[transform];
                // Pylons remain rigid, and attached weapons inherit a unit scale.
                Transform pylon = FindAncestorPrefix(transform, branch, "pylon_");
                Func<Vector3, Vector3> localMap = map;
                if (wing && pylon != null)
                {
                    Vector3 native = root.InverseTransformPoint(pylon.position);
                    Vector3 displacement = map(native) - native;
                    localMap = p => p + displacement;
                }

                var filter = transform.GetComponent<MeshFilter>();
                var collider = transform.GetComponent<MeshCollider>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Mesh readable = ReadableCopy(filter.sharedMesh);
                    states.Add(new MeshState { Transform = transform, Filter = filter, Source = readable, LocalToAircraft = matrix, Map = localMap });
                    var part = transform.GetComponent<AeroPart>();
                    if (part != null)
                        areaRatios[part] = ProjectedAreaRatio(readable, matrix, localMap);
                }

                if (collider != null && collider.sharedMesh != null)
                    states.Add(new MeshState { Transform = transform, Collider = collider, Source = ReadableCopy(collider.sharedMesh), LocalToAircraft = matrix, Map = localMap });
            }

            if (wing)
            {
                // Parent-first positions preserve the native hierarchy, rest
                // rotations, control pivots, and damage connections.
                foreach (Transform transform in transforms)
                {
                    Vector3 native = originals[transform].MultiplyPoint3x4(Vector3.zero);
                    Transform pylon = FindAncestorPrefix(transform, branch, "pylon_");
                    Vector3 position = map(native);
                    if (pylon != null)
                    {
                        Vector3 pylonNative = originals[pylon].MultiplyPoint3x4(Vector3.zero);
                        position = native + map(pylonNative) - pylonNative;
                    }

                    transform.position = root.TransformPoint(position);
                }

                foreach (Transform hinge in transforms.Where(t => t.name.StartsWith("slatHinge_", StringComparison.Ordinal)))
                {
                    Matrix4x4 native = originals[hinge];
                    Vector3 pivot = native.MultiplyPoint3x4(Vector3.zero);
                    Vector3 axis = native.MultiplyVector(Vector3.forward).normalized;
                    float start = float.PositiveInfinity;
                    float end = float.NegativeInfinity;
                    foreach (MeshState state in states.Where(s => s.Filter != null && s.Transform.IsChildOf(hinge)))
                    {
                        foreach (Vector3 vertex in state.Source.vertices)
                        {
                            Vector3 point = state.LocalToAircraft.MultiplyPoint3x4(vertex);
                            float station = Vector3.Dot(point - pivot, axis);
                            start = Mathf.Min(start, station);
                            end = Mathf.Max(end, station);
                        }
                    }

                    if (float.IsInfinity(start) || float.IsInfinity(end))
                        throw new InvalidOperationException("FS-20C slat mesh missing: " + hinge.name);
                    Vector3 mappedAxis = Fs20SlatHinge.MappedAxis(pivot, axis, start, end);
                    var marker = hinge.gameObject.AddComponent<Fs20SlatHinge>();
                    marker.Axis = hinge.parent.InverseTransformDirection(root.TransformDirection(mappedAxis)).normalized;
                    marker.ClosedRotation = hinge.localRotation;
                }
            }

            foreach (MeshState state in states)
            {
                Mesh mesh = state.Source;
                mesh.name = state.Source.name + "_FS20C";
                Vector3[] vertices = mesh.vertices;
                Matrix4x4 aircraftToLocal = state.Transform.worldToLocalMatrix * root.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = aircraftToLocal.MultiplyPoint3x4(state.Map(state.LocalToAircraft.MultiplyPoint3x4(vertices[i])));
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                if (mesh.uv.Length == vertices.Length)
                    mesh.RecalculateTangents();
                if (state.Filter != null)
                    state.Filter.sharedMesh = mesh;
                else
                    state.Collider.sharedMesh = mesh;
            }

            // Area changes follow the new planform. Native flap/slat physics
            // then operates on this baseline instead of a blanket multiplier.
            foreach (var pair in areaRatios)
            {
                pair.Key.SetWingArea(pair.Key.GetWingArea() * pair.Value);
                pair.Key.dragArea *= pair.Value;
                var device = pair.Key.GetComponent<HighLiftDevice>();
                if (device != null)
                    Fs20FlightController.ScaleHighLiftArea(device, pair.Value);
            }
        }

        private static bool HasAncestorPrefix(Transform node, Transform root, string prefix)
        {
            return FindAncestorPrefix(node, root, prefix) != null;
        }

        private static Transform FindAncestorPrefix(Transform node, Transform root, string prefix)
        {
            for (var current = node; current != null; current = current == root ? null : current.parent)
                if (current.name.StartsWith(prefix, StringComparison.Ordinal))
                    return current;
            return null;
        }

        private static float ProjectedAreaRatio(Mesh mesh, Matrix4x4 matrix, Func<Vector3, Vector3> map)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            float before = 0f;
            float after = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                before += Mathf.Abs(Vector3.Cross(b - a, c - a).y);
                after += Mathf.Abs(Vector3.Cross(map(b) - map(a), map(c) - map(a)).y);
            }

            return before > 0.001f ? Mathf.Clamp(after / before, 0.5f, 3f) : 1f;
        }
    }
}
