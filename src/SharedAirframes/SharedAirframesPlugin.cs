using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirage.Collections;
using NuclearOption.Networking;
using Japensou.NuclearOption.VortexConventional;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace Japensou.NuclearOption.SharedAirframes
{
    internal static class SharedAirframeStock
    {
        private const string PatchId = "br.japensou.nuclearoption.vortexctol.stock";
        private static Harmony harmony;
        private static ConfigEntry<bool> sharedStock;
        private static bool installed;
        internal static bool Enabled => installed && sharedStock.Value;

        internal static ConfigEntry<bool> Configure(ConfigFile config)
        {
            sharedStock = config.Bind("Inventory", "Shared airframe stock", true, "Share faction stock and owned airframes across FS-20A/B/C. Disable for separate native counters. " + "All three variants remain selectable. Existing stock is never duplicated or redistributed.");
            return sharedStock;
        }

        internal static void Enable(ManualLogSource logger)
        {
            harmony = new Harmony(PatchId);
            try
            {
                if (new[]
                {
                    FamilyButton,
                    FamilyPlayer,
                    OwnedLabel,
                    SupplyLabel
                }.Any(f => f == null))
                    throw new MissingFieldException("FS-20 family inventory badge API changed.");
                foreach (var patch in new[]
                {
                    typeof(SupplyReads),
                    typeof(SupplyDebit),
                    typeof(FamilyMatches),
                    typeof(OwnedDebit),
                    typeof(FamilyBadge),
                    typeof(UpdateBadge)
                }

                )
                    harmony.PatchAll(patch);
                installed = true;
                logger.LogInfo("Integrated FS-20 stock controls ready. Shared stock: " + Enabled);
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                installed = false;
                logger.LogError("Shared airframe API mismatch; stock patches removed: " + error);
            }
        }

        internal static void Disable()
        {
            installed = false;
            harmony?.UnpatchSelf();
            harmony = null;
        }

        internal static bool Family(UObject value) => value is AircraftDefinition d && AirframePool.IsFamily(d.jsonKey);
        internal static bool SameDefinition(UObject a, UObject b)
        {
            if (Enabled && Family(a) && Family(b))
                return true;
            return a == b;
        }

        internal static bool SharedTryGet(SyncIDictionary<AircraftDefinition, FactionHQ.RuntimeSupply> supply, AircraftDefinition definition, out FactionHQ.RuntimeSupply result)
        {
            if (!Enabled || !Family(definition))
                return supply.TryGetValue(definition, out result);
            bool found = false;
            var counts = new List<int>();
            foreach (var pair in supply)
                if (Family(pair.Key))
                {
                    found = true;
                    counts.Add(pair.Value.Count);
                }

            result = new FactionHQ.RuntimeSupply(AirframePool.Count(counts));
            return found;
        }

        internal static bool SharedContains(SyncIDictionary<AircraftDefinition, FactionHQ.RuntimeSupply> supply, AircraftDefinition definition)
        {
            FactionHQ.RuntimeSupply value;
            return SharedTryGet(supply, definition, out value);
        }

        internal static FactionHQ.RuntimeSupply SharedGet(SyncIDictionary<AircraftDefinition, FactionHQ.RuntimeSupply> supply, AircraftDefinition definition)
        {
            if (!Enabled || !Family(definition))
                return supply[definition];
            FactionHQ.RuntimeSupply value;
            SharedTryGet(supply, definition, out value);
            return value;
        }

        [HarmonyPatch]
        internal static class SupplyReads
        {
            internal static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (string method in new[]
                {
                    "GetUnitSupply",
                    "RequestReservedAirframe",
                    "DeployAIAircraft"
                }

                )
                    yield return AccessTools.Method(typeof(FactionHQ), method) ?? throw new MissingMethodException(method);
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int changed = 0;
                foreach (var code in instructions)
                {
                    var called = code.operand as MethodInfo;
                    var type = called?.DeclaringType;
                    if (type == typeof(SyncIDictionary<AircraftDefinition, FactionHQ.RuntimeSupply>))
                    {
                        string replacement = called.Name == "TryGetValue" ? nameof(SharedTryGet) : called.Name == "ContainsKey" ? nameof(SharedContains) : called.Name == "get_Item" ? nameof(SharedGet) : null;
                        if (replacement != null)
                        {
                            code.opcode = System.Reflection.Emit.OpCodes.Call;
                            code.operand = AccessTools.Method(typeof(SharedAirframeStock), replacement);
                            changed++;
                        }
                    }

                    yield return code;
                }

                if (changed == 0)
                    throw new InvalidOperationException("Native supply read contract changed.");
            }
        }

        [HarmonyPatch(typeof(FactionHQ), "ModifyUnitSupply")]
        internal static class SupplyDebit
        {
            private static bool Prefix(FactionHQ __instance, UnitDefinition __0, int __1)
            {
                if (!Enabled || __1 >= 0 || !Family(__0) || !__instance.IsServer)
                    return true;
                var cells = __instance.AircraftSupply.Where(p => Family(p.Key)).OrderBy(p => p.Key.jsonKey == "SmallFighter1" ? 0 : 1).ToArray();
                var debit = AirframePool.PlanDebit(cells.Select(p => p.Value.Count).ToArray(), -(long)__1);
                for (int i = 0; i < cells.Length; i++)
                    if (debit[i] != 0)
                        __instance.AircraftSupply[cells[i].Key] = new FactionHQ.RuntimeSupply(cells[i].Value.Count - debit[i]);
                return false;
            }
        }

        [HarmonyPatch]
        internal static class FamilyMatches
        {
            internal static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Player), "OwnsAirframe", new[] { typeof(AircraftDefinition), typeof(bool) });
                yield return AccessTools.Method(typeof(Player), "OwnedAirframeTypeCount");
                yield return AccessTools.Method(typeof(Player), "PossessesReservedAirframe", new[] { typeof(AircraftDefinition) });
                yield return AccessTools.Method(typeof(Player), "FlyOwnedAirframe");
                yield return AccessTools.Method(typeof(FactionHQ), "IsReservingAirframe", new[] { typeof(Player), typeof(AircraftDefinition) });
                yield return AccessTools.Method(typeof(FactionHQ), "RequestReservedAirframe");
                yield return AccessTools.Method(typeof(FactionHQ), "AddSupplyUnit");
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int changed = 0;
                var equality = AccessTools.Method(typeof(UObject), "op_Equality");
                var same = AccessTools.Method(typeof(SharedAirframeStock), nameof(SameDefinition));
                foreach (var code in instructions)
                {
                    if (code.Calls(equality))
                    {
                        code.operand = same;
                        changed++;
                    }

                    yield return code;
                }

                if (changed == 0)
                    throw new InvalidOperationException("Native airframe identity contract changed.");
            }
        }

        [HarmonyPatch(typeof(Player), "CreditAirframe")]
        internal static class OwnedDebit
        {
            private static bool Prefix(Player __instance, AircraftDefinition __0, int __1)
            {
                if (!Enabled || __1 >= 0 || !Family(__0) || !__instance.IsServer)
                    return true;
                var cells = __instance.OwnedAirframes.ToArray();
                var indices = AirframePool.SelectOwned(cells.Select(c => c.Definition == null ? null : c.Definition.jsonKey).ToArray(), cells.Select(c => c.Reserved).ToArray(), __0.jsonKey, -(long)__1);
                foreach (int i in indices)
                    __instance.OwnedAirframes.RemoveAt(i);
                return false;
            }
        }

        // Show the shared pool once on the family tile.
        [HarmonyPatch(typeof(Fs20FamilyMenu), "Refresh")]
        internal static class FamilyBadge
        {
            private static void Postfix(Fs20FamilyMenu __instance)
            {
                RefreshBadge(__instance);
            }
        }

        [HarmonyPatch(typeof(AircraftSelectionButton), "UpdateOwned")]
        internal static class UpdateBadge
        {
            private static void Postfix(AircraftSelectionButton __instance)
            {
                if (Family(__instance.definition))
                    RefreshBadge(__instance.GetComponentInParent<Fs20FamilyMenu>(true));
            }
        }

        private static readonly FieldInfo FamilyButton = AccessTools.Field(typeof(Fs20FamilyMenu), "family");
        private static readonly FieldInfo FamilyPlayer = AccessTools.Field(typeof(Fs20FamilyMenu), "player");
        private static readonly FieldInfo OwnedLabel = AccessTools.Field(typeof(AircraftSelectionButton), "ownedValue");
        private static readonly FieldInfo SupplyLabel = AccessTools.Field(typeof(AircraftSelectionButton), "supplyValue");
        private static void RefreshBadge(Fs20FamilyMenu menu)
        {
            if (!Enabled || menu == null)
                return;
            var tile = (AircraftSelectionButton)FamilyButton.GetValue(menu);
            var player = (Player)FamilyPlayer.GetValue(menu);
            if (tile == null || player == null || tile.definition == null)
                return;
            int owned = player.OwnedAirframeTypeCount(tile.definition, true);
            SetCount((Text)OwnedLabel.GetValue(tile), owned);
            var hq = player.HQ;
            if (hq != null)
                SetCount((Text)SupplyLabel.GetValue(tile), hq.GetUnitSupply(tile.definition));
        }

        private static void SetCount(Text label, int count)
        {
            if (label == null)
                return;
            label.text = count > 0 ? count.ToString() : "0";
            label.enabled = count > 0;
        }
    }
}
