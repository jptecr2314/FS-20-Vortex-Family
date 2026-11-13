using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed class Fs20TAsset
    {
        internal sealed class MeshData
        {
            internal Vector3[] Vertices, Normals;
            internal Vector2[] Uv;
            internal int[][] Indices;
            internal Mesh Create(Matrix4x4 map)
            {
                var mesh = new Mesh { name = "FS-20T wing", indexFormat = IndexFormat.UInt32 };
                var vertices = new Vector3[Vertices.Length];
                var normals = new Vector3[Vertices.Length];
                var normalMap = map.inverse.transpose;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = map.MultiplyPoint3x4(Vertices[i]);
                    if (Normals != null) normals[i] = normalMap.MultiplyVector(Normals[i]).normalized;
                }
                mesh.vertices = vertices;
                mesh.subMeshCount = Indices.Length;
                for (int i = 0; i < Indices.Length; i++) mesh.SetTriangles(Indices[i], i, false);
                if (Normals != null) mesh.normals = normals; else mesh.RecalculateNormals();
                if (Uv != null) { mesh.uv = Uv; mesh.RecalculateTangents(); }
                var colors = new Color32[vertices.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = colors;
                mesh.RecalculateBounds();
                return mesh;
            }
        }
        internal sealed class Piece
        {
            internal string Name, Path, Parent, Owner, Motion;
            internal bool Replace;
            internal string[] Materials;
            internal MeshData[] Lods, Hulls;
        }
        internal sealed class Area
        {
            internal string Path;
            internal float Before, After, HighLiftRatio;
            internal Vector3 BeforeCenter, AfterCenter;
        }
        internal sealed class Hinge
        {
            internal string Tag, Path;
            internal Vector3 Pivot, Axis, Stroke;
            internal Quaternion Rotation;
            internal float Angle;
        }
        internal sealed class DamageState
        {
            internal string Piece;
            internal float AreaFraction, DragScale;
        }
        internal sealed class DamageRegion
        {
            internal string Id, Attachment, Budget;
            internal float Area;
            internal Vector3 Center, Anchor;
            internal DamageState[] States;
        }
        internal Piece[] Pieces;
        internal Area[] Areas;
        internal Hinge[] Hinges;
        internal DamageRegion[] Regions;
        internal string SourceHash;
        private static Fs20TAsset cached;
        internal static Fs20TAsset Read()
        {
            if (cached != null) return cached;
            var result = new Fs20TAsset();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Fs20T.bin"))
            using (var r = new BinaryReader(stream ?? throw new InvalidOperationException("FS-20T geometry is missing.")))
            {
                if (r.ReadInt32() != 0x46535431) throw new InvalidDataException("FS-20T geometry magic mismatch.");
                int version = r.ReadInt32();
                if (version != 2 && version != 3) throw new InvalidDataException("FS-20T geometry version mismatch.");
                result.SourceHash = Text(r);
                result.Pieces = new Piece[Count(r, 200)];
                for (int i = 0; i < result.Pieces.Length; i++)
                {
                    var p = new Piece { Name = Text(r), Path = Text(r), Parent = Text(r), Owner = Text(r), Motion = Text(r), Replace = r.ReadInt32() == 1 };
                    p.Materials = new string[Count(r, 32)];
                    for (int j = 0; j < p.Materials.Length; j++) p.Materials[j] = Text(r);
                    p.Lods = new MeshData[3];
                    for (int j = 0; j < 3; j++) p.Lods[j] = ReadMesh(r, p.Materials.Length, true);
                    p.Hulls = new MeshData[Count(r, 200)];
                    for (int j = 0; j < p.Hulls.Length; j++) p.Hulls[j] = ReadMesh(r, 1, false);
                    result.Pieces[i] = p;
                }
                result.Areas = new Area[Count(r, 100)];
                for (int i = 0; i < result.Areas.Length; i++) result.Areas[i] = new Area { Path = Text(r), Before = Float(r), After = Float(r), BeforeCenter = Point(r), AfterCenter = Point(r), HighLiftRatio = Float(r) };
                result.Hinges = new Hinge[Count(r, 8)];
                for (int i = 0; i < result.Hinges.Length; i++) result.Hinges[i] = new Hinge { Tag = Text(r), Path = Text(r), Pivot = Point(r), Rotation = new Quaternion(Float(r), Float(r), Float(r), Float(r)), Axis = Point(r), Angle = Float(r), Stroke = Point(r) };
                result.Regions = new DamageRegion[version == 3 ? Count(r, 32) : 0];
                for (int i = 0; i < result.Regions.Length; i++)
                {
                    var region = new DamageRegion { Id = Text(r), Attachment = Text(r), Budget = Text(r), Area = Float(r), Center = Point(r), Anchor = Point(r), States = new DamageState[3] };
                    for (int state = 0; state < region.States.Length; state++) region.States[state] = new DamageState { Piece = Text(r), AreaFraction = Float(r), DragScale = Float(r) };
                    if (!(region.Area > 0) || region.States[0].AreaFraction < .999f || region.States[1].AreaFraction >= region.States[0].AreaFraction || region.States[2].AreaFraction >= region.States[1].AreaFraction || region.States[2].AreaFraction <= 0)
                        throw new InvalidDataException("Invalid FS-20T damage area progression: " + region.Id);
                    result.Regions[i] = region;
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing FS-20T geometry data.");
            }
            return cached = result;
        }
        private static MeshData ReadMesh(BinaryReader r, int materials, bool visual)
        {
            int vertices = Count(r, 200000), faces = Count(r, 200000);
            if (!visual && faces > 240) throw new InvalidDataException("FS-20T convex cell exceeds its triangle budget.");
            var data = new MeshData { Vertices = new Vector3[vertices], Normals = visual ? new Vector3[vertices] : null, Uv = visual ? new Vector2[vertices] : null, Indices = new int[materials][] };
            for (int i = 0; i < vertices; i++)
            {
                data.Vertices[i] = Point(r);
                if (visual) { data.Normals[i] = Point(r); data.Uv[i] = new Vector2(Float(r), Float(r)); }
            }
            var indices = new List<int>[materials];
            for (int i = 0; i < materials; i++) indices[i] = new List<int>();
            for (int i = 0; i < faces; i++)
            {
                int slot = visual ? r.ReadInt32() : 0;
                if (slot < 0 || slot >= materials) throw new InvalidDataException("Invalid FS-20T material.");
                for (int j = 0; j < 3; j++)
                {
                    int index = r.ReadInt32();
                    if (index < 0 || index >= vertices) throw new InvalidDataException("Invalid FS-20T vertex index.");
                    indices[slot].Add(index);
                }
            }
            for (int i = 0; i < materials; i++) data.Indices[i] = indices[i].ToArray();
            return data;
        }
        private static int Count(BinaryReader r, int limit) { int n = r.ReadInt32(); if (n < 0 || n > limit) throw new InvalidDataException("Invalid FS-20T data size."); return n; }
        private static string Text(BinaryReader r) { int n = Count(r, 1024); var bytes = r.ReadBytes(n); if (bytes.Length != n) throw new EndOfStreamException(); return Encoding.UTF8.GetString(bytes); }
        private static float Float(BinaryReader r) { float n = r.ReadSingle(); if (float.IsNaN(n) || float.IsInfinity(n)) throw new InvalidDataException("Non-finite FS-20T data."); return n; }
        private static Vector3 Point(BinaryReader r) => new Vector3(Float(r), Float(r), Float(r));
    }
}
