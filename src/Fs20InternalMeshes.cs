using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20InternalMeshes
    {
        internal sealed class Data
        {
            internal Vector3[] Vertices;
            internal int[] Triangles;
            internal Vector3 Pivot;
            internal float Angle;
            internal Vector2[] InteriorUv;
        }
        private static Dictionary<string, Data> cache;
        internal static Data Get(string name)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Data>();
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Fs20Internals.bin"))
                using (var reader = new BinaryReader(stream ?? throw new InvalidOperationException("FS-20 internal geometry missing.")))
                {
                    if (reader.ReadInt32() != 0x46533230 || reader.ReadInt32() != 1)
                        throw new InvalidOperationException("FS-20 internal geometry version mismatch.");
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadInt32()));
                        var data = new Data { Pivot = Point(reader), Angle = reader.ReadSingle() };
                        data.Vertices = new Vector3[reader.ReadInt32()];
                        data.Triangles = new int[reader.ReadInt32()];
                        for (int j = 0; j < data.Vertices.Length; j++) data.Vertices[j] = Point(reader);
                        for (int j = 0; j < data.Triangles.Length; j++) data.Triangles[j] = reader.ReadInt32();
                        cache.Add(key, data);
                    }
                }
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Fs20InteriorUv.bin"))
                using (var reader = new BinaryReader(stream ?? throw new InvalidOperationException("FS-20 interior UVs missing.")))
                {
                    if (reader.ReadInt32() != 0x46535556 || reader.ReadInt32() != 1)
                        throw new InvalidOperationException("FS-20 interior UV version mismatch.");
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadInt32()));
                        var data = cache[key];
                        data.InteriorUv = new Vector2[reader.ReadInt32()];
                        if (data.InteriorUv.Length != data.Triangles.Length)
                            throw new InvalidOperationException("FS-20 interior UV corner count mismatch: " + key);
                        for (int j = 0; j < data.InteriorUv.Length; j++)
                            data.InteriorUv[j] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    }
                }
            }
            return cache[name];
        }
        private static Vector3 Point(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        internal static GameObject Create(string key, Transform root, Transform parent, Material material, int side = 1, Vector3? offset = null)
        {
            var data = Get(key);
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            var metric = new List<float>();
            var surfaceUv = new List<Vector2>();
            var map = parent.worldToLocalMatrix * root.localToWorldMatrix;
            for (int i = 0; i < data.Triangles.Length; i += 3)
            {
                var p = new Vector3[3];
                for (int k = 0; k < 3; k++)
                {
                    p[k] = data.Vertices[data.Triangles[i + (side < 0 ? 2 - k : k)]];
                    p[k].x *= side;
                    p[k] += offset ?? Vector3.zero;
                }
                // Independent front/back vertices keep correct normals on the
                // open lining, without relying on a particular shader cull mode.
                for (int back = 0; back < 2; back++)
                {
                    var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                    int axis = Mathf.Abs(n.x) > Mathf.Abs(n.y) ? (Mathf.Abs(n.x) > Mathf.Abs(n.z) ? 0 : 2) : (Mathf.Abs(n.y) > Mathf.Abs(n.z) ? 1 : 2);
                    for (int j = 0; j < 3; j++)
                    {
                        int corner = back == 0 ? j : 2 - j;
                        var v = p[corner];
                        triangles.Add(positions.Count);positions.Add(map.MultiplyPoint3x4(v));
                        metric.Add(axis == 0 ? v.y : v.x);metric.Add(axis == 2 ? v.y : v.z);
                        if (data.InteriorUv != null)
                            surfaceUv.Add(data.InteriorUv[i + (side < 0 ? 2 - corner : corner)]);
                    }
                }
            }
            var mesh = new Mesh { name = "FS20 " + key, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);mesh.SetTriangles(triangles, 0);
            var packed = Fs20HookUv.Pack(metric.ToArray());var uv = new Vector2[packed.Length / 2];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(packed[i * 2], packed[i * 2 + 1]);
            mesh.uv = data.InteriorUv != null ? surfaceUv.ToArray() : uv;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var obj = new GameObject("FS20 " + key);obj.transform.SetParent(parent, false);obj.layer = parent.gameObject.layer;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }
    }
}
