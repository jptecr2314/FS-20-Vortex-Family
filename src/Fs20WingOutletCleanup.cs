using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Visual-only removal of the donor's obsolete roll-post fairings, A/C only.
    public sealed class Fs20WingOutletCleanup : MonoBehaviour
    {
        [Serializable] private sealed class Layout { public Patch[] patches; }
        [Serializable] private sealed class Patch
        {
            public string path;
            public int vertexCount;
            public int[] slotCounts, removeSkinFaces, fillSkinTriangles, removeMechanicalFaces;
            public Vertex[] addedVertices;
            public UvOverride[] uvOverrides;
        }
        [Serializable] private sealed class UvOverride { public int index; public Vector2 uv; }
        [Serializable] private sealed class Vertex
        {
            public Vector3 position;
            public Vector2 uv;
            public int[] refs;
            public float[] weights;
            public bool back, wall;
        }
        private static Layout layout;
        internal static void Apply(Transform root, bool expandedWing)
        {
            if (root.GetComponent<Fs20WingOutletCleanup>()) return;
            if (layout == null)
                using (var stream = typeof(Fs20WingOutletCleanup).Assembly.GetManifestResourceStream("Fs20WingOutletCleanup.json"))
                using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing conventional wing cleanup layout")))
                    layout = Newtonsoft.Json.JsonConvert.DeserializeObject<Layout>(reader.ReadToEnd());
            foreach (var patch in layout.patches) Replace(root, expandedWing, patch);
            root.gameObject.AddComponent<Fs20WingOutletCleanup>();
        }

        private static void Replace(Transform root, bool expandedWing, Patch patch)
        {
            var filter = root.Find(patch.path)?.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) throw new InvalidOperationException("Conventional wing skin missing: " + patch.path);
            var mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);
            try
            {
                if (mesh.vertexCount != patch.vertexCount || mesh.subMeshCount != patch.slotCounts.Length)
                    throw new InvalidOperationException("Wing skin topology changed: " + patch.path);
                var slots = Enumerable.Range(0, mesh.subMeshCount).Select(mesh.GetTriangles).ToArray();
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].Length != patch.slotCounts[i]) throw new InvalidOperationException("Wing skin material topology changed");
                slots[0] = Without(slots[0], patch.removeSkinFaces).Concat(patch.fillSkinTriangles).ToArray();
                slots[1] = Without(slots[1], patch.removeMechanicalFaces);
                var vertices = mesh.vertices.ToList();var normals = mesh.normals.ToList();var tangents = mesh.tangents.ToList();var colors = mesh.colors.ToList();
                var channels = new List<Vector4>[8];
                for (int c = 0; c < 8; c++) { channels[c] = new List<Vector4>();mesh.GetUVs(c, channels[c]); }
                foreach (var change in patch.uvOverrides)
                {
                    var uv = channels[0][change.index];uv.x = change.uv.x;uv.y = change.uv.y;channels[0][change.index] = uv;
                }
                foreach (var spec in patch.addedVertices)
                {
                    var position = expandedWing ? Fs20Geometry.Widen(spec.position) : spec.position;
                    vertices.Add(filter.transform.InverseTransformPoint(root.TransformPoint(position)));
                    Vector3 normal = Vector3.zero;Vector4 tangent = Vector4.zero;Color color = Color.clear;
                    for (int i = 0; i < spec.refs.Length; i++)
                    {
                        int index = spec.refs[i];float weight = spec.weights[i];normal += normals[index] * weight;
                        if (tangents.Count != 0) tangent += tangents[index] * weight;
                        if (colors.Count != 0) color += colors[index] * weight;
                    }
                    normals.Add((spec.back ? -normal : normal).normalized);
                    if (tangents.Count != 0) { if (spec.back) tangent.w = -tangent.w;tangents.Add(tangent); }
                    if (colors.Count != 0) colors.Add(color);
                    for (int c = 0; c < 8; c++)
                    {
                        if (channels[c].Count == 0) continue;
                        Vector4 uv = Vector4.zero;
                        for (int i = 0; i < spec.refs.Length; i++) uv += channels[c][spec.refs[i]] * spec.weights[i];
                        if (c == 0) { uv.x = spec.uv.x;uv.y = spec.uv.y; }
                        channels[c].Add(uv);
                    }
                }
                // JSON faces are oriented in aircraft coordinates; mirror-aware conversion.
                bool mirror = (root.worldToLocalMatrix * filter.transform.localToWorldMatrix).determinant < 0f;
                if (mirror)
                    for (int i = slots[0].Length - patch.fillSkinTriangles.Length; i < slots[0].Length; i += 3)
                    { int t = slots[0][i + 1];slots[0][i + 1] = slots[0][i + 2];slots[0][i + 2] = t; }
                var wallNormals = new Dictionary<int, Vector3>();
                for (int i = slots[0].Length - patch.fillSkinTriangles.Length; i < slots[0].Length; i += 3)
                {
                    int a = slots[0][i], b = slots[0][i + 1], c = slots[0][i + 2];
                    var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    foreach (int index in new[] { a, b, c })
                        if (index >= patch.vertexCount && patch.addedVertices[index - patch.vertexCount].wall)
                            wallNormals[index] = (wallNormals.TryGetValue(index, out var old) ? old : Vector3.zero) + normal;
                }
                foreach (var pair in wallNormals) normals[pair.Key] = pair.Value.normalized;
                // Drop the removed fairing's unused render vertices, preserving all retained attributes.
                var used = slots.SelectMany(x => x).Distinct().OrderBy(i => i).ToArray();
                var remap = used.Select((old, index) => new { old, index }).ToDictionary(x => x.old, x => x.index);
                mesh.Clear();mesh.name = filter.sharedMesh.name + "_CTOLWingSkin";
                mesh.SetVertices(used.Select(i => vertices[i]).ToList());mesh.SetNormals(used.Select(i => normals[i]).ToList());
                if (tangents.Count != 0) mesh.SetTangents(used.Select(i => tangents[i]).ToList());
                if (colors.Count != 0) mesh.SetColors(used.Select(i => colors[i]).ToList());
                for (int c = 0; c < 8; c++) if (channels[c].Count != 0) mesh.SetUVs(c, used.Select(i => channels[c][i]).ToList());
                mesh.subMeshCount = slots.Length;
                for (int i = 0; i < slots.Length; i++) mesh.SetTriangles(slots[i].Select(j => remap[j]).ToArray(), i, false);
                mesh.RecalculateTangents();mesh.RecalculateBounds();filter.sharedMesh = mesh;
            }
            catch { UnityEngine.Object.DestroyImmediate(mesh);throw; }
        }
        private static int[] Without(int[] triangles, int[] faces)
        {
            var removed = new HashSet<int>(faces);
            return triangles.Where((value, index) => !removed.Contains(index / 3)).ToArray();
        }
    }
}
