using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20AugerMount
    {
        internal const string Key = "JP_FS20_INTERNAL_AUGER";
        private static readonly Dictionary<int, WeaponMount> mounts = new Dictionary<int, WeaponMount>();
        private static readonly Dictionary<int, Vector3> rackCenters = new Dictionary<int, Vector3>();
        internal static bool IsAuger(string key) => key != null && (key == Key || key == Key + "_L" || key == Key + "_R" ||
            key == "bomb_penetrator1_internalx2" || key == "aryx_navex_bomb_penetrator1_internalx1");
        internal static WeaponMount ForSide(int side) => mounts[side < 0 ? -1 : 1];
        internal static WeaponMount Choice => mounts.TryGetValue(0, out var choice) ? choice : null;
        internal static Vector3 RackCenter(int side) =>
            new Vector3(side * Fs20BaySpec.BombX, Fs20BaySpec.BombY, Fs20BaySpec.BombZ) +
            Quaternion.AngleAxis(side * Fs20BaySpec.BombRoll, Vector3.forward) * rackCenters[side];
        private static WeaponMount Mount(Encyclopedia encyclopedia, int side)
        {
            WeaponMount existing;
            if (mounts.TryGetValue(side, out existing) && existing != null && existing.prefab != null) return existing;
            string key = "JP_FS20_INTERNAL_AUGER_" + (side < 0 ? "L" : "R");
            var donor = encyclopedia.weaponMounts.FirstOrDefault(w => w != null && w.jsonKey == "bomb_penetrator1_internalx2");
            if (donor == null || donor.prefab == null) throw new InvalidOperationException("Native internal Auger carrier unavailable.");
            var mount = UnityEngine.Object.Instantiate(donor);
            mount.name = mount.jsonKey = key;mount.mountName = "GPO-2P Auger";
            mount.dontAutomaticallyAddToEncyclopedia = true;
            mount.mass = 1000f;mount.emptyMass = 0f;mount.ammo = 1;mount.GearSafety = mount.GroundSafety = true;
            var holder = new GameObject(key + " template");holder.SetActive(false);UnityEngine.Object.DontDestroyOnLoad(holder);
            var wrapper = new GameObject(key);wrapper.transform.SetParent(holder.transform, false);
            var pair = UnityEngine.Object.Instantiate(donor.prefab, wrapper.transform, false);
            var weapons = pair.GetComponentsInChildren<MountedMissile>(true).OrderBy(m => m.transform.localPosition.x).ToArray();
            if (weapons.Length != 2) throw new InvalidOperationException("Native Auger carrier child layout changed.");
            var kept = weapons[side < 0 ? 0 : 1];var offset = kept.transform.localPosition;
            UnityEngine.Object.DestroyImmediate(weapons[side < 0 ? 1 : 0].gameObject);
            // Keep the native side-specific rack/bomb relation, not just the
            // bomb's position. The pair root carries the compact rack mesh.
            pair.transform.localPosition = -offset;
            var rack = pair.GetComponent<MeshFilter>();
            if (rack == null) throw new InvalidOperationException("Native Auger carrier mesh unavailable.");
            var readable = Fs20Geometry.ReadableCopy(rack.sharedMesh);
            var vertices = readable.vertices;var center = Vector3.zero;
            foreach (var vertex in vertices) center += vertex;
            rackCenters[side] = wrapper.transform.InverseTransformPoint(rack.transform.TransformPoint(center / vertices.Length));
            UnityEngine.Object.DestroyImmediate(readable);
            Fs20Tuning.Set(kept, "doorOpenDuration", Fs20BaySpec.DoorHold);
            Fs20Tuning.Set(kept, "railLength", 1.5f);Fs20Tuning.Set(kept, "railSpeed", 2f);Fs20Tuning.Set(kept, "railDelay", Fs20BaySpec.RailDelay);
            Fs20Tuning.Set(kept, "railDirection", Enum.ToObject(Fs20Tuning.Field(typeof(MountedMissile), "railDirection").FieldType, 1));
            var box = pair.GetComponent<BoxCollider>();if (box != null) UnityEngine.Object.DestroyImmediate(box);
            mount.prefab = wrapper;
            Encyclopedia.WeaponLookup.Add(key, mount);encyclopedia.weaponMounts.Add(mount);
            ((INetworkDefinition)mount).LookupIndex = encyclopedia.IndexLookup.Count;encyclopedia.IndexLookup.Add(mount);
            mounts[side] = mount;return mount;
        }
        internal static void Add(Encyclopedia encyclopedia, WeaponManager manager, Transform root, Dictionary<int, BayDoor[]> doors)
        {
            var inner = manager.hardpointSets[1];
            Mount(encyclopedia, -1);var right = Mount(encyclopedia, 1);
            WeaponMount choice;
            if (!mounts.TryGetValue(0, out choice) || choice == null)
            {
                choice = UnityEngine.Object.Instantiate(right);choice.name = choice.jsonKey = Key;
                choice.dontAutomaticallyAddToEncyclopedia = false;
                Encyclopedia.WeaponLookup.Add(Key, choice);encyclopedia.weaponMounts.Add(choice);
                ((INetworkDefinition)choice).LookupIndex = encyclopedia.IndexLookup.Count;encyclopedia.IndexLookup.Add(choice);
                mounts[0] = choice;
            }
            // Reuse the original paired inner-bay hardpoints and saved slot
            // indices. Side-specific carriers are resolved only when spawned.
            EnsureOptions(manager);
        }

        internal static void EnsureOptions(WeaponManager manager)
        {
            WeaponMount choice;
            if (manager == null || manager.hardpointSets.Length < 3 || !mounts.TryGetValue(0, out choice) || choice == null) return;
            for (int group = 1; group <= 2; group++)
            {
                var set = manager.hardpointSets[group];
                // Addons can offer another mount for the same native Auger.
                // Only the custom paired carrier fits these enlarged bays.
                var previous = set.weaponOptions;
                var keep = Fs20AugerOptions.Keep(previous.Select(w => w == null ? null : w.jsonKey).ToArray(),
                    previous.Select(w => w != null && w.info != null && w.info == choice.info).ToArray(), group == 1);
                set.weaponOptions = keep.Select(i => i < 0 ? choice : previous[i]).ToList();
            }
        }
    }
}
