using System;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20InternalDucts
    {
        internal static void Apply(Transform root, Material metal)
        {
            var part = root.Find("liftFan");
            var filter = part.GetComponent<MeshFilter>();
            var renderer = part.GetComponent<Renderer>();
            var mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);
            int slot = Array.FindIndex(renderer.sharedMaterials, m => m != null && m.name.StartsWith("jet_mechanical", StringComparison.Ordinal));
            if (slot < 0 || mesh.GetTriangles(slot).Length != 1404 * 3)
                throw new InvalidOperationException("FS-20 intake geometry changed.");
            var map = root.worldToLocalMatrix * part.localToWorldMatrix;
            var inverse = map.inverse;var vertices = mesh.vertices;
            foreach (int i in mesh.GetTriangles(slot).Distinct())
            {
                var p = map.MultiplyPoint3x4(vertices[i]);
                p.y += .20f * Smooth((p.z + .93429f) / 1.65f) * Smooth((3f - p.z) / .70f);
                vertices[i] = inverse.MultiplyPoint3x4(p);
            }
            mesh.vertices = vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();filter.sharedMesh = mesh;
            Fs20DuctIslandSpec.RemoveCenter(root);
            var tube = Fs20InternalMeshes.Create("center_duct", root, part, metal);
            Fs20HookInterior.RegisterDamageRenderer(part, tube.GetComponent<Renderer>());
        }
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
