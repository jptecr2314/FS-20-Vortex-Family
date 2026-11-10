using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using V = Japensou.NuclearOption.VortexConventional.Fs20HookBayGeometry.Vertex;
using Builder = Japensou.NuclearOption.VortexConventional.Fs20HookBayGeometry.Builder;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20SkinSurgery
    {
        internal sealed class Door
        {
            internal string Name;
            internal Vector4[] Planes;
            internal Transform Hinge, Owner;
            internal BayDoor Native;
            internal float BackThickness;
            internal Material BackMaterial;
            internal float NativeSkinFront = float.PositiveInfinity;
        }
        internal sealed class Surface
        {
            private readonly List<Vector3[]> triangles = new List<Vector3[]>();
            internal readonly Fs20PanelUv Panels;
            internal Material SkinMaterial;
            internal Surface(Transform root)
            {
                var paint = new List<float[]>();
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || filter.sharedMesh == null || filter.name.Contains("LOD")) continue;
                    var mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);
                    var points = mesh.vertices;
                    var uv = mesh.uv;var colors = mesh.colors;
                    var map = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var materials = renderer.sharedMaterials;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        if (sub >= materials.Length || materials[sub] == null || !materials[sub].name.Contains("skin")) continue;
                        var indices = mesh.GetTriangles(sub);
                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            var p = new[] { map.MultiplyPoint3x4(points[indices[i]]), map.MultiplyPoint3x4(points[indices[i + 1]]), map.MultiplyPoint3x4(points[indices[i + 2]]) };
                            if (p.Min(v => v.z) > 2f || p.Max(v => v.z) < -3.7f || p.Min(v => v.x) > 1.2f || p.Max(v => v.x) < -1.2f) continue;
                            triangles.Add(p);
                            if (filter.name.StartsWith("weaponDoor_", StringComparison.Ordinal) && uv.Length == points.Length && Vector3.Cross(p[1] - p[0], p[2] - p[0]).y < -1e-8f)
                            {
                                var ids = new[] { indices[i], indices[i + 1], indices[i + 2] };
                                if (colors.Length == points.Length && ids.Any(id => colors[id].r < .98f)) continue;
                                var sample = new float[15];
                                for (int j = 0; j < 3; j++) { sample[j * 5] = p[j].x;sample[j * 5 + 1] = p[j].y;sample[j * 5 + 2] = p[j].z;sample[j * 5 + 3] = uv[ids[j]].x;sample[j * 5 + 4] = uv[ids[j]].y; }
                                paint.Add(sample);
                                if(SkinMaterial==null)SkinMaterial=materials[sub];
                            }
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                Panels = new Fs20PanelUv(paint.ToArray());
            }
            internal float Floor(float x, float z)
            {
                float result = float.PositiveInfinity;
                foreach (var p in triangles)
                {
                    var a = p[0]; var b = p[1]; var c = p[2];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-9f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    if (u >= -.00001f && v >= -.00001f && u + v <= 1.00001f)
                        result = Mathf.Min(result, u * a.y + v * b.y + (1 - u - v) * c.y);
                }
                return result;
            }
        }
        private static readonly Dictionary<int, Surface> surfaces = new Dictionary<int, Surface>();
        internal static Surface Reference(Transform root)
        {
            Surface value;
            if (!surfaces.TryGetValue(root.GetInstanceID(), out value)) surfaces.Add(root.GetInstanceID(), value = new Surface(root));
            return value;
        }
        internal static Transform Attach(Transform root, Transform parent, string name, Vector3 point)
        {
            var obj = new GameObject(name);obj.layer = parent.gameObject.layer;
            var t = obj.transform;t.SetParent(parent, false);
            var m = parent.worldToLocalMatrix * root.localToWorldMatrix;
            var forward = m.MultiplyVector(Vector3.forward);var up = m.MultiplyVector(Vector3.up);
            t.localPosition = m.MultiplyPoint3x4(point);t.localRotation = Quaternion.LookRotation(forward, up);
            var right = t.localRotation * Vector3.right;
            t.localScale = new Vector3(Vector3.Dot(m.MultiplyVector(Vector3.right), right), up.magnitude, forward.magnitude);
            return t;
        }
        internal static Vector4[] Box(float x0, float x1, float z0, float z1, float roof) => new[]
        {
            new Vector4(1,0,0,-x0),new Vector4(-1,0,0,x1),
            new Vector4(0,0,1,-z0),new Vector4(0,0,-1,z1),new Vector4(0,-1,0,roof)
        };
        internal static List<V> Clip(List<V> poly, Vector4[] planes, Builder outside = null, int sub = 0)
        {
            foreach (var plane in planes)
            {
                List<V> inside, discard;
                Fs20HookBayGeometry.Split(poly, Matrix4x4.identity, plane, out inside, out discard);
                outside?.Add(discard, sub);poly = inside;
                if (poly.Count < 3) break;
            }
            return poly;
        }
        internal sealed class PartitionResult
        {
            internal List<List<V>> Outside;
            internal List<List<V>>[] Inside;
        }
        internal static PartitionResult PartitionSkin(List<V> polygon, Vector4[][] volumes)
        {
            var result = new PartitionResult { Outside = new List<List<V>> { polygon }, Inside = volumes.Select(v => new List<List<V>>()).ToArray() };
            for (int d = 0; d < volumes.Length; d++)
            {
                var next = new List<List<V>>();
                foreach (var part in result.Outside)
                {
                    var inside = part;
                    foreach (var plane in volumes[d])
                    {
                        List<V> keep, cut;Fs20HookBayGeometry.Split(inside, Matrix4x4.identity, plane, out keep, out cut);
                        if (cut.Count >= 3) next.Add(cut);
                        inside = keep;if (inside.Count < 3) break;
                    }
                    if (inside.Count >= 3) result.Inside[d].Add(inside);
                }
                result.Outside = next;
            }
            return result;
        }
        internal static PartitionResult PartitionGeometry(List<V> poly, float depth, bool skin, bool frame, bool lowDetail,
            bool retiredDoor, Vector4[][] doorVolumes, Vector4[][] frameVolumes)
        {
            var center = (poly[0].P + poly[1].P + poly[2].P) / 3f;
            var normal = Vector3.Cross(poly[1].P - poly[0].P, poly[2].P - poly[0].P);
            bool exterior = skin && center.y < -.545f && depth < .0002f && normal.y < -1e-8f;
            var empty = new PartitionResult { Outside = new List<List<V>>(), Inside = doorVolumes.Select(v => new List<List<V>>()).ToArray() };
            if (retiredDoor && !exterior) return empty;
            if (skin && center.y < -.545f && (depth < .020f || lowDetail))
            {
                var result = PartitionSkin(poly, doorVolumes);
                if (!exterior) result.Inside = empty.Inside;
                return result;
            }
            // Internal skin plates may obstruct the enlarged chamber, but
            // deleting them with the wider frame boxes also cuts exterior
            // strips beside the hatches. Restrict skin cuts to the portals.
            empty.Outside = frame ? PartitionSkin(poly, skin ? doorVolumes : frameVolumes).Outside : new List<List<V>> { poly };
            return empty;
        }
        internal static void Cut(Transform root, IEnumerable<Transform> branches, IList<Door> doors, bool removeBayFrames, Vector4[][] frameVolumes = null)
        {
            var floor = Reference(root);
            var portals = doors.Select(d => d.Planes).ToArray();
            var frames = new List<Vector4[]>();
            if (removeBayFrames)
                foreach (int side in new[] { -1, 1 })
                    frames.Add(Box(side < 0 ? -1f : .10f, side < 0 ? -.10f : 1f, Fs20BaySpec.Rear, Fs20BaySpec.Front, -.490f));
            if (frameVolumes != null) frames.AddRange(frameVolumes);
            var frameCuts = frames.ToArray();
            var seen = new HashSet<MeshFilter>();
            foreach (var branch in branches)
            foreach (var filter in branch.GetComponentsInChildren<MeshFilter>(true))
            {
                if (branch == root && filter.transform != root && !(filter.transform.parent == root && filter.name.Contains("LOD"))) continue;
                if (filter.name.StartsWith("FS20", StringComparison.Ordinal) || filter.name.StartsWith("Native skin ", StringComparison.Ordinal) || filter.name == "FS-20A internal fuel cell") continue;
                if (!seen.Add(filter) || filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null) continue;
                var retiredDoor = filter.GetComponentInParent<BayDoor>(true);
                var fixedOwner = retiredDoor == null ? null : retiredDoor.GetComponentInParent<AeroPart>(true);
                var source = Fs20Geometry.ReadableCopy(filter.sharedMesh);
                Fs20HookBayGeometry.TransformMesh(source, root.worldToLocalMatrix * filter.transform.localToWorldMatrix);
                var outside = new Builder(source);
                var patches = doors.Select(d => new Builder(source)).ToArray();
                var materials = renderer.sharedMaterials;
                bool emit = !filter.name.Contains("LOD");
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    bool lowDetail = filter.name.Contains("LOD");
                    bool skin = sub < materials.Length && materials[sub] != null && (materials[sub].name.Contains("skin") || lowDetail);
                    var owner = filter.GetComponentInParent<AeroPart>(true);
                    bool bayMechanics = owner != null && (owner.name.StartsWith("weaponBay_", StringComparison.Ordinal) || owner.name.StartsWith("wingRoot_", StringComparison.Ordinal)) && sub < materials.Length && materials[sub] != null && materials[sub].name.StartsWith("jet_mechanical", StringComparison.Ordinal);
                    bool frame = sub >= materials.Length || materials[sub] == null || skin || bayMechanics || materials[sub].name.StartsWith("aircraft_structure", StringComparison.Ordinal);
                    var indices = source.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var poly = new List<V> { outside.Read(indices[i]), outside.Read(indices[i + 1]), outside.Read(indices[i + 2]) };
                        var center = (poly[0].P + poly[1].P + poly[2].P) / 3f;
                        float depth = Mathf.Abs(center.y - floor.Floor(center.x, center.z));
                        var partition = PartitionGeometry(poly, depth, skin, frame, lowDetail, retiredDoor != null, portals, frameCuts);
                        for (int d = 0; d < doors.Count; d++)
                        {
                            if (!emit) continue;
                            foreach (var inDoor in partition.Inside[d])
                            {
                                var inset = doors[d].Planes.Select(p => new Vector4(p.x, p.y, p.z, p.w - .002f * new Vector3(p.x, p.y, p.z).magnitude)).ToArray();
                                var panel = Clip(inDoor, inset);
                                if (!float.IsPositiveInfinity(doors[d].NativeSkinFront))
                                    panel = Clip(panel, new[] { new Vector4(0, 0, -1, doors[d].NativeSkinFront) });
                                patches[d].Add(panel, sub);
                            }
                        }
                        foreach (var part in partition.Outside) outside.Add(part, sub);
                    }
                }
                var result = outside.Finish(source.name + "_expanded_internal");
                Fs20HookBayGeometry.TransformMesh(result, filter.transform.worldToLocalMatrix * root.localToWorldMatrix);
                filter.sharedMesh = result;
                // Keep the separately authored native collision hull. A
                // pierced render surface is not a replacement convex hull.
                if (fixedOwner != null) filter.transform.SetParent(fixedOwner.transform, true);
                for (int d = 0; d < doors.Count; d++)
                {
                    var patch = patches[d].Finish("FS20 native UV " + doors[d].Name);
                    if (patch.vertexCount == 0) { UnityEngine.Object.DestroyImmediate(patch); continue; }
                    if (doors[d].Name.EndsWith("/4", StringComparison.Ordinal) || doors[d].Name.EndsWith("/5", StringComparison.Ordinal)) patch.RecalculateTangents();
                    if (doors[d].BackThickness > 0)
                    {
                        var backing = Backing(patch, doors[d].BackThickness);
                        Fs20HookBayGeometry.TransformMesh(backing, doors[d].Hinge.worldToLocalMatrix * root.localToWorldMatrix);
                        var inner = new GameObject("FS20 hatch inner panel " + filter.name);inner.layer = doors[d].Owner.gameObject.layer;
                        inner.transform.SetParent(doors[d].Hinge, false);inner.AddComponent<MeshFilter>().sharedMesh = backing;
                        inner.AddComponent<MeshRenderer>().sharedMaterial = doors[d].BackMaterial;
                        Fs20HookInterior.RegisterDamageRenderer(doors[d].Owner, inner.GetComponent<Renderer>());
                    }
                    Fs20HookBayGeometry.TransformMesh(patch, doors[d].Hinge.worldToLocalMatrix * root.localToWorldMatrix);
                    var obj = new GameObject("Native skin " + doors[d].Name + " " + filter.name);obj.layer = doors[d].Owner.gameObject.layer;
                    obj.transform.SetParent(doors[d].Hinge, false);obj.AddComponent<MeshFilter>().sharedMesh = patch;
                    obj.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    Fs20HookInterior.RegisterDamageRenderer(doors[d].Owner, obj.GetComponent<Renderer>());
                }
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
        private static Mesh Backing(Mesh source, float thickness)
        {
            var vertices = source.vertices;int count = vertices.Length;
            var positions = vertices.Concat(vertices.Select(p => p + Vector3.up * thickness)).ToArray();
            var triangles = new List<int>();var edges = new Dictionary<string, int[]>();var occurrences = new Dictionary<string, int>();
            Func<Vector3, string> key = p => Mathf.RoundToInt(p.x * 100000) + "," + Mathf.RoundToInt(p.y * 100000) + "," + Mathf.RoundToInt(p.z * 100000);
            foreach (int sub in Enumerable.Range(0, source.subMeshCount))
            {
                var indices = source.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    triangles.AddRange(new[] { indices[i + 2] + count, indices[i + 1] + count, indices[i] + count });
                    for (int j = 0; j < 3; j++)
                    {
                        int a = indices[i + j], b = indices[i + (j + 1) % 3];string x = key(vertices[a]), y = key(vertices[b]);
                        string e = string.CompareOrdinal(x, y) < 0 ? x + "/" + y : y + "/" + x;
                        occurrences[e] = occurrences.ContainsKey(e) ? occurrences[e] + 1 : 1;edges[e] = new[] { a, b };
                    }
                }
            }
            foreach (var e in edges)
                if (occurrences[e.Key] == 1)
                { int a = e.Value[0], b = e.Value[1];triangles.AddRange(new[] { a, b, b + count, a, b + count, a + count }); }
            var mesh = new Mesh { name = "FS20 hatch backing", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = positions;mesh.triangles = triangles.ToArray();
            mesh.uv = positions.Select(p => Fs20InteriorFinish.Coordinates(p, Vector3.up)).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }
}
