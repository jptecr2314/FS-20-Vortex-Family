using System;
using System.Collections.Generic;
using System.Linq;

namespace Japensou.NuclearOption.VortexConventional
{
    // Reference registered native definitions; never clone/register weapons or infer
    // their role from a mod's display name, folder or jsonKey prefix.
    internal static class Fs20WingtipOptions
    {
        internal const float MaximumCarriedMassKg = 240f;

        internal static double CarriedMassKg(WeaponMount mount)
        {
            if (mount == null || mount.info == null || !Finite(mount.emptyMass) || !Finite(mount.info.massPerRound) || mount.emptyMass < 0f || mount.info.massPerRound < 0f) return double.NaN;
            // Native SpawnMount adds emptyMass; a single MountedMissile's Attach
            // adds massPerRound. mount.mass can be a stale catalogue cache.
            return (double)mount.emptyMass + mount.info.massPerRound;
        }

        internal static bool Compatible(WeaponMount mount)
        {
            if (mount == null || string.IsNullOrWhiteSpace(mount.jsonKey) || mount.prefab == null || mount.info == null || mount.ammo != 1)
                return false;
            if (!(CarriedMassKg(mount) <= MaximumCarriedMassKg)) return false;
            if (mount.turret || mount.missileBay || mount.Cargo || mount.Troops || mount.tailHook || mount.slingloadHook || mount.countermeasure || mount.radar || mount.GunAmmo)
                return false;
            var info = mount.info;
            if (!info.missile || info.bomb || info.glideBomb || info.gun || info.cargo || info.troops || info.sling) return false;
            var role = info.effectiveness;
            if (!Finite(role.antiAir) || !Finite(role.antiSurface) || !Finite(role.antiRadar)) return false;
            // Target class is not launch-platform compatibility. Even a one-round
            // SAM/VLS definition passes role checks; require a single mounted round
            // as well as aircraft provenance in Synchronize below.
            if (!(role.antiAir > 0f && role.antiAir > role.antiSurface && role.antiAir > role.antiRadar)) return false;
            var weapons = mount.prefab.GetComponentsInChildren<Weapon>(true);
            return weapons.Length == 1 && weapons[0] is MountedMissile;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static void Synchronize(HardpointSet group, IEnumerable<WeaponMount> registry, IEnumerable<WeaponMount> donor, IEnumerable<WeaponMount> airborne)
        {
            if (group == null || group.name != "Wingtip rails") return;
            var air = new HashSet<string>(StringComparer.Ordinal);
            var preferred = new HashSet<string>(StringComparer.Ordinal);
            var aircraftMounts = airborne == null ? new List<WeaponMount>() : airborne.Where(m => m != null && !string.IsNullOrWhiteSpace(m.jsonKey)).ToList();
            foreach (var mount in aircraftMounts) air.Add(mount.jsonKey);
            var donorMounts = donor == null ? new List<WeaponMount>() : donor.Where(m => m != null && !string.IsNullOrWhiteSpace(m.jsonKey)).ToList();
            foreach (var mount in donorMounts) preferred.Add(mount.jsonKey);
            var definitions = new Dictionary<string, WeaponMount>(StringComparer.Ordinal);
            // The current registry wins over stale donor references with the same key,
            // including a replacement that is no longer wingtip-compatible.
            if (registry != null) foreach (var mount in registry)
                if (mount != null && !string.IsNullOrWhiteSpace(mount.jsonKey) && !definitions.ContainsKey(mount.jsonKey)) definitions.Add(mount.jsonKey, mount);
            foreach (var mount in donorMounts.Concat(aircraftMounts))
                if (mount != null && !string.IsNullOrWhiteSpace(mount.jsonKey) && !definitions.ContainsKey(mount.jsonKey)) definitions.Add(mount.jsonKey, mount);
            var wanted = new List<WeaponMount> { null };
            // Never use our generated tip list as evidence of aircraft compatibility.
            // Keep valid aliases here for native save keys, validation and availability;
            // deduplicate only the original checker's already-allowed output.
            wanted.AddRange(definitions.Values.Where(m => air.Contains(m.jsonKey) && Compatible(m))
                .OrderBy(m => preferred.Contains(m.jsonKey) ? 0 : 1).ThenBy(m => m.jsonKey, StringComparer.Ordinal));
            var current = group.weaponOptions;
            if (current != null && current.Count == wanted.Count)
            {
                bool same = true;
                for (int i = 0; i < current.Count; i++) if (!ReferenceEquals(current[i], wanted[i])) { same = false; break; }
                if (same) return;
            }
            // Keep references held by the native selectors. Allowed/event/faction/stock
            // restrictions remain in the native menu/save/spawn paths, not baked here.
            if (current == null) group.weaponOptions = wanted;
            else { current.Clear(); current.AddRange(wanted); }
        }

        internal static bool SameMissile(WeaponMount a, WeaponMount b)
        {
            if (a == null || b == null || a.info == null || b.info == null) return false;
            if (ReferenceEquals(a.info, b.info)) return true;
            return a.info.weaponPrefab != null && ReferenceEquals(a.info.weaponPrefab, b.info.weaponPrefab);
        }

        internal static void DeduplicateAvailable(List<WeaponMount> available, WeaponMount selected)
        {
            if (available == null) return;
            var unique = new List<WeaponMount>();
            bool empty = false;
            foreach (var mount in available)
            {
                if (mount == null) { if (!empty) { unique.Add(null); empty = true; } continue; }
                int index = unique.FindIndex(other => SameMissile(other, mount));
                if (index < 0) unique.Add(mount);
                else if (ReferenceEquals(mount, selected)) unique[index] = mount;
            }
            available.Clear(); available.AddRange(unique);
        }

        internal static WeaponMount ResolveVisible(WeaponMount selected, List<WeaponMount> available)
        {
            if (selected == null || available == null) return null;
            if (available.Contains(selected)) return selected;
            return available.FirstOrDefault(m => SameMissile(selected, m));
        }
    }
}
