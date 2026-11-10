using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TailHook
    {
        internal const string Key = "JP_FS20C_INTERNAL_HOOK";
        internal const float ExposedRcs = 0.002f;
        internal static void Add(Encyclopedia encyclopedia, AircraftDefinition definition)
        {
            var source = encyclopedia.weaponMounts.FirstOrDefault(m => m != null && m.jsonKey == "TailHook_Multirole1");
            if (source == null || source.prefab == null || source.prefab.GetComponent<TailHook>() == null)
                throw new InvalidOperationException("Native multirole tail hook is unavailable.");
            var manager = definition.unitPrefab.GetComponent<Aircraft>().weaponManager;
            if (manager.hardpointSets.Any(s => s.name == "Internal Tail Hook"))
                return;
            var parent = definition.unitPrefab.transform;
            if (parent == null)
                throw new InvalidOperationException("FS-20C tail attachment is missing.");
            var mount = UnityEngine.Object.Instantiate(source);
            GameObject anchor = null;
            try
            {
                mount.name = mount.jsonKey = Key;
                mount.mountName = "Retractable Tail Hook";
                mount.RCS = mount.emptyRCS = 0f;
                mount.drag = mount.emptyDrag = 0f;
                mount.mass = mount.emptyMass = Fs20BaySpec.HookMass;
                mount.prefab = UnityEngine.Object.Instantiate(source.prefab, definition.unitPrefab.transform.parent, false);
                mount.prefab.name = Key;
                // Use the native hook and arrestor interface.
                Fs20Tuning.Set(mount.prefab.GetComponent<TailHook>(), "mass", Fs20BaySpec.HookMass);
                var nativeHook = mount.prefab.GetComponent<TailHook>();
                Fs20Tuning.Set(nativeHook, "stowedAngle", Fs20BaySpec.HookStowedAngle);
                Fs20Tuning.Set(nativeHook, "deployedAngle", Fs20BaySpec.HookDeployedAngle);
                var arm = (Transform)Fs20Tuning.Field(typeof(TailHook), "hook").GetValue(nativeHook);
                arm.localScale = new Vector3(1, 1, Fs20BaySpec.HookScale);
                arm.localRotation = Quaternion.Euler(Fs20BaySpec.HookStowedAngle, 0, 0);
                Fs20HookMechanism.Install(mount.prefab.transform, arm, Fs20HookInterior.NativeMetal(mount.prefab.transform));
                mount.prefab.AddComponent<Fs20HookFairing>();
                // Leave the native aft skin and UVs intact.
                Fs20VentralHookBay.Build(definition.unitPrefab.transform, Fs20HookInterior.NativeMetal(mount.prefab.transform));
                anchor = new GameObject("FS-20C internal hook mount");
                anchor.transform.SetParent(parent, false);
                anchor.transform.position = definition.unitPrefab.transform.TransformPoint(Fs20VentralHookBay.Mount);
                anchor.transform.rotation = definition.unitPrefab.transform.rotation;
                var hardpoint = new Hardpoint
                {
                    transform = anchor.transform,
                    part = parent.GetComponent<AeroPart>(),
                    bayDoors = Array.Empty<BayDoor>(),
                    BuiltInWeapons = Array.Empty<Weapon>(),
                    BuiltInTurrets = Array.Empty<Turret>(),
                    HardpointIndex = manager.hardpointSets.SelectMany(s => s.hardpoints).Max(h => h.HardpointIndex) + 1
                };
                var set = new HardpointSet
                {
                    name = "Internal Tail Hook",
                    precludingHardpointSets = new List<byte>(),
                    weaponOptions = new List<WeaponMount>
                    {
                        null,
                        mount
                    },
                    hardpoints = new List<Hardpoint>
                    {
                        hardpoint
                    }
                };
                Encyclopedia.WeaponLookup.Add(Key, mount);
                encyclopedia.weaponMounts.Add(mount);
                ((INetworkDefinition)mount).LookupIndex = encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(mount);
                manager.hardpointSets = manager.hardpointSets.Concat(new[] { set }).ToArray();
            }
            catch
            {
                if (Encyclopedia.WeaponLookup.TryGetValue(Key, out var existing) && existing == mount)
                    Encyclopedia.WeaponLookup.Remove(Key);
                encyclopedia.weaponMounts.Remove(mount);
                encyclopedia.IndexLookup.Remove(mount);
                if (anchor != null)
                    UnityEngine.Object.Destroy(anchor);
                if (mount.prefab != source.prefab)
                    UnityEngine.Object.Destroy(mount.prefab);
                UnityEngine.Object.Destroy(mount);
                throw;
            }
        }
    }

    public sealed class Fs20HookFairing : MonoBehaviour
    {
        private static readonly System.Reflection.FieldInfo Amount = Fs20Tuning.Field(typeof(TailHook), "deployedAmount");
        private static readonly System.Reflection.FieldInfo Deployed = Fs20Tuning.Field(typeof(TailHook), "deployed");
        private static readonly System.Reflection.FieldInfo Hooked = Fs20Tuning.Field(typeof(TailHook), "hooked");
        private TailHook hook;
        private Fs20HookBay bay;
        private Unit unit;
        private float appliedRcs;
        private float coverOpen;
        private readonly Fs20HookDepartureLock departure = new Fs20HookDepartureLock();
        private float sampledAt = -1f;
        internal bool Overloaded;
        internal static Fs20HookFairing ForHook(TailHook hook)
        {
            var fairing = hook.GetComponent<Fs20HookFairing>();
            if (fairing != null) return fairing;
            var owner = hook.unitPart == null ? hook.GetComponentInParent<Aircraft>() : hook.unitPart.parentUnit as Aircraft;
            if (owner == null || owner.definition == null || owner.definition.jsonKey != "JP_FS20_CTOL") return null;
            return hook.gameObject.AddComponent<Fs20HookFairing>();
        }

        private bool AllowAutomatic()
        {
            EnforceDepartureLock();
            var owner = hook == null || hook.unitPart == null ? null : hook.unitPart.parentUnit as Aircraft;
            return hook != null && owner != null && Fs20HookDepartureLock.AllowAutomatic(
                departure.Locked, owner.gearDeployed, Overloaded, (bool)Hooked.GetValue(hook));
        }

        // Guard the scheduled delegate itself. Other hook mounts on a C must
        // obey the same departure lock as the optional internal hook.
        [HarmonyPatch(typeof(TailHook), "Start")]
        internal static class ScheduledInterlock
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var schedule = AccessTools.Method(typeof(SlowUpdateExtensions), "StartSlowUpdate", new[]
                    { typeof(MonoBehaviour), typeof(float), typeof(Action), typeof(System.Threading.CancellationToken) });
                var guarded = AccessTools.Method(typeof(ScheduledInterlock), nameof(Schedule));
                foreach (var code in instructions)
                {
                    if (code.Calls(schedule)) code.operand = guarded;
                    yield return code;
                }
            }

            private static void Schedule(MonoBehaviour behaviour, float interval, Action update, System.Threading.CancellationToken token)
            {
                var nativeHook = behaviour as TailHook;
                var fairing = nativeHook == null ? null : ForHook(nativeHook);
                if (fairing != null)
                {
                    Action nativeUpdate = update;
                    update = () => { if (fairing != null && fairing.AllowAutomatic()) nativeUpdate(); };
                    VortexConventionalPlugin.ReportAssetState("FS-20C scheduled tailhook guard bound; departure lock=" + fairing.departure.Locked + ".");
                }
                SlowUpdateExtensions.StartSlowUpdate(behaviour, interval, update, token);
            }
        }
        internal void EnforceDepartureLock()
        {
            ResolveBay();
            var owner = hook == null || hook.unitPart == null ? null : hook.unitPart.parentUnit as Aircraft;
            if (owner == null || owner.rb == null)
                return;
            bool hooked = (bool)Hooked.GetValue(hook);
            if (sampledAt != Time.fixedTime)
            {
                sampledAt = Time.fixedTime;
                departure.Step((bool)Fs20Tuning.Airborne.GetValue(owner), owner.gearDeployed, owner.radarAlt, owner.rb.velocity.y, owner.GetInputs().throttle, hooked, Time.fixedDeltaTime);
            }

            if ((Overloaded || departure.Locked || !owner.gearDeployed) && !hooked)
            {
                Deployed.SetValue(hook, false);
                if ((float)Amount.GetValue(hook) > .001f)
                    hook.enabled = true;
            }
        }

        private void FixedUpdate() => EnforceDepartureLock();
        internal bool CanAnimateHook
        {
            get
            {
                ResolveBay();
                return !(bool)Deployed.GetValue(hook) || (bool)Hooked.GetValue(hook) || (bay != null && bay.Cover != null && coverOpen >= 0.98f);
            }
        }

        private void Awake()
        {
            hook = GetComponent<TailHook>();
            ResolveBay();
        }

        private void ResolveBay()
        {
            if (hook == null)
                hook = GetComponent<TailHook>();
            if (bay == null)
                bay = GetComponentInParent<Fs20HookBay>();
            if (bay == null && hook != null && hook.unitPart != null)
                bay = hook.unitPart.GetComponent<Fs20HookBay>();
        }

        private void LateUpdate() => Advance(Time.deltaTime);
        internal void Advance(float deltaTime)
        {
            ResolveBay();
            if (hook == null || hook.unitPart == null)
                return;
            unit = hook.unitPart.parentUnit;
            float amount = (float)Amount.GetValue(hook);
            bool open = (bool)Deployed.GetValue(hook) || (bool)Hooked.GetValue(hook) || amount > 0.001f;
            coverOpen = Mathf.MoveTowards(coverOpen, open ? 1f : 0f, 2f * deltaTime);
            if (bay != null && bay.Cover != null)
            {
                float drop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(coverOpen / 0.25f));
                float turn = Mathf.SmoothStep(0f, 1f, bay.DropOffset.sqrMagnitude < 1e-10f ? coverOpen : Mathf.InverseLerp(0.25f, 1f, coverOpen));
                bay.Cover.localPosition = bay.ClosedPosition + bay.DropOffset * drop;
                bay.Cover.localRotation = bay.ClosedRotation * Quaternion.AngleAxis(-bay.OpeningDegrees * turn, Vector3.forward);
            }

            bool exposed = amount > 0.05f || coverOpen > 0.02f;
            float rcs = exposed ? Fs20TailHook.ExposedRcs : 0f;
            if (unit != null && rcs != appliedRcs)
            {
                unit.ModifyRCS(rcs - appliedRcs);
                appliedRcs = rcs;
            }
        }

        private void OnDestroy()
        {
            if (unit != null && appliedRcs != 0f)
                unit.ModifyRCS(-appliedRcs);
            if (bay != null && bay.Cover != null)
            {
                bay.Cover.localRotation = bay.ClosedRotation;
                bay.Cover.localPosition = bay.ClosedPosition;
            }
        }

        [HarmonyPatch(typeof(TailHook), "FixedUpdate")]
        internal static class BayInterlock
        {
            private static bool Prefix(TailHook __instance)
            {
                var bay = ForHook(__instance);
                if (bay != null)
                    bay.EnforceDepartureLock();
                return bay == null || bay.CanAnimateHook;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original = AccessTools.Method(typeof(Physics), nameof(Physics.Linecast), new[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType() });
                var replacement = AccessTools.Method(typeof(BayInterlock), nameof(LinecastOutsideAirframe));
                foreach (var code in instructions)
                {
                    if (code.Calls(original))
                    {
                        // Preserve labels on the first instruction in the replacement.
                        var instance = new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
                        instance.labels.AddRange(code.labels);
                        code.labels.Clear();
                        yield return instance;
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, replacement);
                    }
                    else
                        yield return code;
                }
            }

            private static bool LinecastOutsideAirframe(Vector3 start, Vector3 end, out RaycastHit hit, TailHook hook)
            {
                // Ignore own-aircraft hits only for this hook's wire probe.
                if (hook.GetComponent<Fs20HookFairing>() == null)
                    return Physics.Linecast(start, end, out hit);
                var fairing = hook.GetComponent<Fs20HookFairing>();
                if ((fairing.departure.Locked || !(bool)Deployed.GetValue(hook)) && !(bool)Hooked.GetValue(hook))
                {
                    hit = default(RaycastHit);
                    return false;
                }

                var delta = end - start;
                var own = hook.unitPart == null ? null : hook.unitPart.parentUnit;
                foreach (var candidate in Physics.RaycastAll(start, delta.normalized, delta.magnitude).OrderBy(h => h.distance))
                {
                    var part = candidate.collider.GetComponentInParent<UnitPart>();
                    if (own != null && part != null && part.parentUnit == own && !part.IsDetached())
                        continue;
                    hit = candidate;
                    return true;
                }

                hit = default(RaycastHit);
                return false;
            }
        }

        [HarmonyPatch(typeof(TailHook), "CheckDeployConditions")]
        internal static class DepartureInterlock
        {
            private static bool Prefix(TailHook __instance)
            {
                var fairing = ForHook(__instance);
                return fairing == null || fairing.AllowAutomatic();
            }
        }

        [HarmonyPatch(typeof(TailHook), "ApplyForce")]
        internal static class ArrestingOverload
        {
            private static bool Prefix(TailHook __instance, Vector3 __0)
            {
                var fairing = __instance.GetComponent<Fs20HookFairing>();
                if (fairing == null) return true;
                if (fairing.Overloaded) return false;
                if (__0.sqrMagnitude <= Fs20BaySpec.HookBreakForce * Fs20BaySpec.HookBreakForce) return true;
                fairing.Overloaded = true;
                AccessTools.Method(typeof(TailHook), "Unhook").Invoke(__instance, null);
                Deployed.SetValue(__instance, false);
                Debug.LogWarning("FS-20C tail hook overload: arrestor released; hook unavailable until respawn.");
                return false;
            }
        }
    }
}
