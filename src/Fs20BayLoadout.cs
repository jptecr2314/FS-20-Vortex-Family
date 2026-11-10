using System;
using System.Linq;
using HarmonyLib;
using NuclearOption.SavedMission;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20BayLoadout
    {
        internal static string[] NormalizeKeys(string[] keys, int count)
        {
            var result = keys.Take(count).ToArray();
            if (result.Length > 1 && Fs20AugerMount.IsAuger(result[1])) result[1] = Fs20AugerMount.Key;
            return result;
        }
        internal static bool LegacyPair(string[] keys, int count) => keys.Length >= count + 2 &&
            Fs20AugerMount.IsAuger(keys[count]) && Fs20AugerMount.IsAuger(keys[count + 1]);
        private static bool Ours(WeaponManager manager) => manager != null && manager.hardpointSets.Length > 2 &&
            manager.hardpointSets[1].weaponOptions.Any(w => w != null && w.jsonKey == Fs20AugerMount.Key);
        private static void Normalize(WeaponManager manager, Loadout loadout)
        {
            if (!Ours(manager) || loadout == null || loadout.weapons.Count <= 2) return;
            var keys = NormalizeKeys(loadout.weapons.Select(w => w == null ? null : w.jsonKey).ToArray(), manager.hardpointSets.Length);
            if (Fs20AugerMount.IsAuger(keys[1]))
                loadout.weapons[1] = manager.hardpointSets[1].weaponOptions.First(w => w != null && w.jsonKey == Fs20AugerMount.Key);
            if (keys[2] == null) loadout.weapons[2] = null;
            if (loadout.weapons.Count > keys.Length) loadout.weapons.RemoveRange(keys.Length, loadout.weapons.Count - keys.Length);
        }

        [HarmonyPatch(typeof(WeaponSelector), "PopulateOptions")]
        internal static class Options
        {
            private static void Prefix(HardpointSet __2)
            {
                Fs20AugerMount.EnsureOptions(ManagerFor(__2));
            }
        }

        private static WeaponManager ManagerFor(HardpointSet set)
        {
            var point = set?.hardpoints.FirstOrDefault()?.transform;
            var aircraft = point == null ? null : point.GetComponentInParent<Aircraft>(true);
            return aircraft != null && aircraft.definition != null &&
                (aircraft.definition.jsonKey == "JP_FS20_CTOL" || aircraft.definition.jsonKey == Fs20AirframeA.Key)
                ? aircraft.weaponManager : null;
        }

        [HarmonyPatch(typeof(WeaponSelector), nameof(WeaponSelector.SetValue))]
        internal static class Selection
        {
            private static void Prefix(HardpointSet ___hardpointSet, ref WeaponMount __0)
            {
                var manager = ManagerFor(___hardpointSet);var choice = Fs20AugerMount.Choice;
                if (manager == null || choice == null || __0 == null || !ReferenceEquals(manager.hardpointSets[1], ___hardpointSet)) return;
                if (Fs20AugerMount.IsAuger(__0.jsonKey) || (__0.info != null && __0.info == choice.info)) __0 = choice;
            }
        }

        [HarmonyPatch(typeof(LoadoutSelector), nameof(LoadoutSelector.GenerateLoadoutFromDropdowns))]
        internal static class Menu
        {
            private static void Postfix(LoadoutSelector __instance, Loadout __result)
            {
                var aircraft = (Aircraft)Fs20Tuning.Field(typeof(LoadoutSelector), "aircraft").GetValue(__instance);
                Normalize(aircraft == null ? null : aircraft.weaponManager, __result);
            }
        }
        [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.SpawnWeapons))]
        internal static class Spawn
        {
            private static void Prefix(WeaponManager __instance)
            {
                var aircraft = __instance.GetComponentInParent<Aircraft>();
                Normalize(__instance, aircraft == null ? null : aircraft.loadout);
            }
        }
        [HarmonyPatch(typeof(SavedLoadout), nameof(SavedLoadout.CreateLoadout), new[] { typeof(WeaponManager) })]
        internal static class Saved
        {
            private static void Postfix(SavedLoadout __instance, WeaponManager __0, Loadout __result)
            {
                if (!Ours(__0) || __result == null) return;
                // 0.5.0/1 appended two Auger-only slots after the native ones.
                // Migrate the paired selection without shifting the hook slot.
                int first = __0.hardpointSets.Length;
                if (LegacyPair(__instance.Selected.Select(s => s.Key).ToArray(), first))
                    __result.weapons[1] = __0.hardpointSets[1].weaponOptions.First(w => w != null && w.jsonKey == Fs20AugerMount.Key);
                else if (__instance.Selected.Count > 1 && Fs20AugerMount.IsAuger(__instance.Selected[1].Key))
                    __result.weapons[1] = __0.hardpointSets[1].weaponOptions.First(w => w != null && w.jsonKey == Fs20AugerMount.Key);
                Normalize(__0, __result);
            }
        }
    }
}
