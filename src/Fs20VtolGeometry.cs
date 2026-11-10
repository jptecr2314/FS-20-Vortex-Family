using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20VtolGeometry
    {
        internal static void Remove(Transform root)
        {
            // These islands are internal VTOL hardware. Skin, bay walls, gear,
            // main engine and thrust-vectoring nozzle are separate and retained.
            foreach (var spec in Fs20VtolSpec.All())
                Filter(root, spec.Path, spec.Material, spec.RemovedTriangles, spec.Rules);
            CleanFan(root);
            root.gameObject.AddComponent<Fs20VtolCleanup>();
        }

        internal static void CleanFan(Transform root)
        {
            var rotors = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("liftFan_blades", StringComparison.Ordinal)).ToArray();
            var removed = new HashSet<Renderer>(rotors.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)));
            foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = group.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                    levels[i].renderers = levels[i].renderers.Where(r => r != null && !removed.Contains(r)).ToArray();
                group.SetLODs(levels);
            }
            foreach (var part in root.GetComponentsInChildren<AeroPart>(true))
            {
                var field = Fs20Tuning.Field(typeof(UnitPart), "damageMaterial");
                var damage = (DamageMaterial)field.GetValue(part);
                if (damage == null || damage.renderers == null) continue;
                var indices = damage.indices ?? Array.Empty<byte>();
                var keep = Enumerable.Range(0, damage.renderers.Count)
                    .Where(i => damage.renderers[i] != null && !removed.Contains(damage.renderers[i])).ToArray();
                damage.indices = keep.Select(i => i < indices.Length ? indices[i] : (byte)0).ToArray();
                damage.renderers = keep.Select(i => damage.renderers[i]).ToList();field.SetValue(part, damage);
            }
            foreach (var rotor in rotors) UnityEngine.Object.DestroyImmediate(rotor.gameObject);
            int stripped = 0;
            foreach (string path in new[] {
                "liftFan/liftFan_door_upper_hinge/liftFan_door_upper",
                "liftFan/fanDoor_LL_mount/fanDoor_LL_hinge/fanDoor_LL",
                "liftFan/fanDoor_LR_mount/fanDoor_LR_hinge/fanDoor_LR" })
            {
                var cover = root.Find(path);if (cover == null) continue;
                var filter = cover.GetComponent<MeshFilter>();var renderer = cover.GetComponent<Renderer>();
                if (filter == null || renderer == null || filter.sharedMesh == null || filter.sharedMesh.name.EndsWith("_CTOLFanCover", StringComparison.Ordinal)) continue;
                var mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);bool changed = false;
                for (int slot = 0; slot < mesh.subMeshCount; slot++)
                {
                    var material = slot < renderer.sharedMaterials.Length ? renderer.sharedMaterials[slot] : null;
                    if (material != null && material.name.Contains("SmallFighter1_skin")) continue;
                    stripped += mesh.GetTriangles(slot).Length / 3;mesh.SetTriangles(Array.Empty<int>(), slot, false);changed = true;
                }
                if (changed) { mesh.name += "_CTOLFanCover";filter.sharedMesh = mesh; }
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            if (rotors.Length > 0 || stripped > 0)
                VortexConventionalPlugin.ReportAssetState("FS20 former VTOL fan cleanup: rotor objects=" + rotors.Length + ", cover internal triangles=" + stripped + "; skin and main intake retained.");
        }

        private static void Filter(Transform root, string path, string material, int expected, Fs20IslandRule[] rules)
        {
            Transform part = path.Length == 0 ? root : root.Find(path);
            var filter = part == null ? null : part.GetComponent<MeshFilter>();
            var renderer = part == null ? null : part.GetComponent<Renderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null)
                throw new InvalidOperationException("FS-20 VTOL mesh missing: " + path);
            int slot = Array.FindIndex(renderer.sharedMaterials, m => m != null && m.name.StartsWith(material, StringComparison.Ordinal));
            if (slot < 0) throw new InvalidOperationException("FS-20 VTOL material missing: " + path);
            Mesh mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);
            try
            {
                int[] before = mesh.GetTriangles(slot);
                int[] after;
                if (rules.Length == 0) after = new int[0];
                else
                {
                    Vector3[] vertices = mesh.vertices;
                    var points = new float[vertices.Length * 3];
                    Matrix4x4 matrix = root.worldToLocalMatrix * part.localToWorldMatrix;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 p = matrix.MultiplyPoint3x4(vertices[i]);
                        points[i * 3] = p.x;
                        points[i * 3 + 1] = p.y;
                        points[i * 3 + 2] = p.z;
                    }
                    after = Fs20MeshIslands.Remove(points, before, rules);
                }
                if (before.Length - after.Length != expected * 3)
                    throw new InvalidOperationException("FS-20 VTOL triangle count changed: " + path);
                mesh.SetTriangles(after, slot, false);
                mesh.name += "_CTOL_NoVtol";
                filter.sharedMesh = mesh;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }
    }

    // Finish after spawn/loadout initialization as well as on the prefab, so
    // replacement visuals from another initialization step cannot restore it.
    public sealed class Fs20VtolCleanup : MonoBehaviour
    {
        private int frames = 2;
        private void LateUpdate()
        {
            if (frames-- > 0) Fs20VtolGeometry.CleanFan(transform);
            else
            {
                VortexConventionalPlugin.ReportAssetState("FS20 spawned CTOL fan audit: rotor transforms=" +
                    GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("liftFan_blades", StringComparison.Ordinal)) + ".");
                enabled = false;
            }
        }
    }
}
