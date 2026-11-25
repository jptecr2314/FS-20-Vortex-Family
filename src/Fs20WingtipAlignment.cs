using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Placement only, before the native MountedMissile caches mountedPosition.
    // Never edit shared weapon prefabs, scales, launcher axes or physical budgets.
    internal static class Fs20WingtipAlignment
    {
        private static bool ready;
        private static float referenceX, referenceZ;
        private static Quaternion referenceRotation;

        internal static void Configure(WeaponMount reference)
        {
            if (ready || reference == null || reference.prefab == null) return;
            var missile = reference.prefab.GetComponentInChildren<MountedMissile>(true);
            Bounds bounds;
            if (missile == null || !TryBounds(missile.transform, reference.prefab.transform, out bounds)) return;
            referenceX = bounds.center.x; referenceZ = bounds.center.z;
            referenceRotation = Quaternion.Inverse(reference.prefab.transform.rotation) * missile.transform.rotation;
            ready = true;
        }

        internal static bool Align(MountedMissile missile, Aircraft aircraft, Hardpoint point, WeaponMount mount)
        {
            if (!ready || missile == null || aircraft == null || aircraft.weaponManager == null || point == null || point.transform == null || mount == null) return false;
            var groups = aircraft.weaponManager.hardpointSets;
            var group = groups == null ? null : groups.FirstOrDefault(g => g != null && g.hardpoints != null && g.hardpoints.Contains(point));
            string key = aircraft.definition == null ? null : aircraft.definition.jsonKey;
            // The native call supplies the authoritative aircraft and point. Physics
            // can reparent the wing owner; do not infer ownership from its ancestors.
            if ((key != "JP_FS20_A" && key != "JP_FS20_CTOL") || aircraft.GetComponent<Fs20ExtraWingStations>() == null || group == null || group.name != "Wingtip rails" || !missile.transform.IsChildOf(point.transform)) return false;
            Bounds before;
            if (!TryBounds(missile.transform, point.transform, out before)) return false;
            var position = missile.transform.position; var rotation = missile.transform.rotation;
            // Keep the provider's radial standoff and carrier hardware. A large
            // missile needs more lateral room; a common XYZ offset would clip fins.
            var target = new Vector3(referenceX, before.center.y, referenceZ);
            missile.transform.rotation = point.transform.rotation * referenceRotation;
            Bounds rotated;
            if (!TryBounds(missile.transform, point.transform, out rotated))
            {
                missile.transform.SetPositionAndRotation(position, rotation); return false;
            }
            missile.transform.position += point.transform.TransformVector(target - rotated.center);
            return true;
        }

        internal static bool TryBounds(Transform missile, Transform frame, out Bounds bounds)
        {
            bounds = default(Bounds); bool any = false;
            foreach (var filter in missile.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled || !VisibleWithin(filter.transform, missile)) continue;
                if (!AddBounds(filter.sharedMesh.bounds, filter.transform, frame, ref bounds, ref any)) return false;
            }
            foreach (var renderer in missile.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null || !renderer.enabled || !VisibleWithin(renderer.transform, missile)) continue;
                if (!AddBounds(renderer.localBounds, renderer.transform, frame, ref bounds, ref any)) return false;
            }
            return any && Finite(bounds.center) && Finite(bounds.size) && bounds.size.z > .1f;
        }

        private static bool VisibleWithin(Transform node, Transform root)
        {
            for (var current = node; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
            }
            return false;
        }

        private static bool AddBounds(Bounds local, Transform source, Transform frame, ref Bounds bounds, ref bool any)
        {
            if (!Finite(local.center) || !Finite(local.extents)) return false;
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var value = frame.InverseTransformPoint(source.TransformPoint(corner));
                if (!Finite(value)) return false;
                if (!any) { bounds = new Bounds(value, Vector3.zero); any = true; }
                else bounds.Encapsulate(value);
            }
            return true;
        }

        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        [HarmonyPatch(typeof(MountedMissile), "AttachToHardpoint", new[] { typeof(Aircraft), typeof(Hardpoint), typeof(WeaponMount) })]
        internal static class Attach
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(MountedMissile __instance, Aircraft __0, Hardpoint __1, WeaponMount __2) => Align(__instance, __0, __1, __2);
        }
    }
}
