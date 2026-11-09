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
            var source = encyclopedia.weaponMounts.FirstOrDefault(m =>
                m != null && m.jsonKey == "TailHook_Multirole1");
            if (source == null || source.prefab == null ||
                source.prefab.GetComponent<TailHook>() == null)
                throw new InvalidOperationException("Native multirole tail hook is unavailable.");
            var manager = definition.unitPrefab.GetComponent<Aircraft>().weaponManager;
            if (manager.hardpointSets.Any(s => s.name == "Internal Tail Hook")) return;
            var parent = definition.unitPrefab.transform;
            if (parent == null) throw new InvalidOperationException("FS-20C tail attachment is missing.");
            var mount = UnityEngine.Object.Instantiate(source);
            GameObject anchor = null;
            try
            {
                mount.name = mount.jsonKey = Key;
                mount.mountName = "Retractable Tail Hook";
                mount.RCS = mount.emptyRCS = 0f;
                mount.drag = mount.emptyDrag = 0f;
                // Retain the prior 95 kg assembly allowance. No mass saving is
                // claimed for the shorter arm before structural/flight testing.
                mount.mass = mount.emptyMass = 95f;
                mount.prefab = UnityEngine.Object.Instantiate(source.prefab,
                    definition.unitPrefab.transform.parent, false);
                mount.prefab.name = Key;
                // The original tail-hook component retains its hinge, ground
                // probe, endpoint, deployment logic and arrestor interface.
                Fs20Tuning.Set(mount.prefab.GetComponent<TailHook>(), "mass", 95f);
                var nativeHook=mount.prefab.GetComponent<TailHook>();
                Fs20Tuning.Set(nativeHook,"stowedAngle",189.77f);
                Fs20Tuning.Set(nativeHook,"deployedAngle",110f);
                var arm=(Transform)Fs20Tuning.Field(typeof(TailHook),"hook").GetValue(nativeHook);
                arm.localScale=new Vector3(1,1,.5f);
                Fs20HookInterior.AddSupport(mount.prefab.transform,.19f,.04f,.12f);
                mount.prefab.AddComponent<Fs20HookFairing>();
                // Clone starts from the untouched native aircraft: omitting the
                // old cut restores the entire aft skin and its original UVs.
                Fs20VentralHookBay.Build(definition.unitPrefab.transform,Fs20HookInterior.NativeMetal(mount.prefab.transform));
                anchor = new GameObject("FS-20C internal hook mount");
                anchor.transform.SetParent(parent, false);
                anchor.transform.position = definition.unitPrefab.transform.TransformPoint(
                    Fs20VentralHookBay.Mount);
                anchor.transform.rotation = definition.unitPrefab.transform.rotation;
                var hardpoint = new Hardpoint
                {
                    transform = anchor.transform,
                    part = parent.GetComponent<AeroPart>(),
                    bayDoors = Array.Empty<BayDoor>(),
                    BuiltInWeapons = Array.Empty<Weapon>(),
                    BuiltInTurrets = Array.Empty<Turret>(),
                    HardpointIndex = manager.hardpointSets.SelectMany(s => s.hardpoints)
                        .Max(h => h.HardpointIndex) + 1
                };
                var set = new HardpointSet
                {
                    name = "Internal Tail Hook",
                    precludingHardpointSets = new List<byte>(),
                    weaponOptions = new List<WeaponMount> { null, mount },
                    hardpoints = new List<Hardpoint> { hardpoint }
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
                if (anchor != null) UnityEngine.Object.Destroy(anchor);
                if (mount.prefab != source.prefab) UnityEngine.Object.Destroy(mount.prefab);
                UnityEngine.Object.Destroy(mount);
                throw;
            }
        }
    }

    public sealed class Fs20HookFairing : MonoBehaviour
    {
        private static readonly System.Reflection.FieldInfo Amount =
            Fs20Tuning.Field(typeof(TailHook), "deployedAmount");
        private static readonly System.Reflection.FieldInfo Deployed =
            Fs20Tuning.Field(typeof(TailHook), "deployed");
        private static readonly System.Reflection.FieldInfo Hooked =
            Fs20Tuning.Field(typeof(TailHook), "hooked");
        private TailHook hook;
        private Fs20HookBay bay;
        private Unit unit;
        private float appliedRcs;
        private float coverOpen;
        private readonly Fs20HookDepartureLock departure=new Fs20HookDepartureLock();
        private float sampledAt=-1f;

        internal void EnforceDepartureLock()
        {
            ResolveBay();
            var owner=hook==null || hook.unitPart==null ? null : hook.unitPart.parentUnit as Aircraft;
            if(owner==null || owner.rb==null)return;
            bool hooked=(bool)Hooked.GetValue(hook);
            if(sampledAt!=Time.fixedTime)
            {
                sampledAt=Time.fixedTime;
                departure.Step((bool)Fs20Tuning.Airborne.GetValue(owner),owner.gearDeployed,
                    owner.radarAlt,owner.rb.velocity.y,owner.GetInputs().throttle,hooked,Time.fixedDeltaTime);
            }
            if((departure.Locked || !owner.gearDeployed) && !hooked)
            {
                Deployed.SetValue(hook,false);
                if((float)Amount.GetValue(hook)>.001f)hook.enabled=true;
            }
        }
        private void FixedUpdate()=>EnforceDepartureLock();

        internal bool CanAnimateHook
        {
            get
            {
                ResolveBay();
                return !(bool)Deployed.GetValue(hook) || (bool)Hooked.GetValue(hook) ||
                    (bay!=null && bay.Cover!=null && coverOpen>=0.98f);
            }
        }

        private void Awake()
        {
            hook = GetComponent<TailHook>();
            ResolveBay();
        }

        private void ResolveBay()
        {
            if(hook==null)hook=GetComponent<TailHook>();
            if(bay==null)bay=GetComponentInParent<Fs20HookBay>();
            if(bay==null && hook!=null && hook.unitPart!=null)
                bay=hook.unitPart.GetComponent<Fs20HookBay>();
        }

        private void LateUpdate() => Advance(Time.deltaTime);

        internal void Advance(float deltaTime)
        {
            ResolveBay();
            if (hook == null || hook.unitPart == null) return;
            unit = hook.unitPart.parentUnit;
            float amount = (float)Amount.GetValue(hook);
            bool open = (bool)Deployed.GetValue(hook) || (bool)Hooked.GetValue(hook) || amount > 0.001f;
            coverOpen = Mathf.MoveTowards(coverOpen, open ? 1f : 0f, 2f * deltaTime);
            if (bay != null && bay.Cover != null)
            {
                float drop=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(coverOpen/0.25f));
                float turn=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.25f,1f,coverOpen));
                bay.Cover.localPosition=bay.ClosedPosition+bay.DropOffset*drop;
                bay.Cover.localRotation=bay.ClosedRotation*Quaternion.AngleAxis(-bay.OpeningDegrees*turn,Vector3.forward);
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
            if (unit != null && appliedRcs != 0f) unit.ModifyRCS(-appliedRcs);
            if (bay != null && bay.Cover != null)
            {
                bay.Cover.localRotation=bay.ClosedRotation;
                bay.Cover.localPosition=bay.ClosedPosition;
            }
        }

        [HarmonyPatch(typeof(TailHook), "FixedUpdate")]
        internal static class BayInterlock
        {
            private static bool Prefix(TailHook __instance)
            {
                var bay = __instance.GetComponent<Fs20HookFairing>();
                if(bay!=null)bay.EnforceDepartureLock();
                return bay == null || bay.CanAnimateHook;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original=AccessTools.Method(typeof(Physics),nameof(Physics.Linecast),
                    new[] {typeof(Vector3),typeof(Vector3),typeof(RaycastHit).MakeByRefType()});
                var replacement=AccessTools.Method(typeof(BayInterlock),nameof(LinecastOutsideAirframe));
                foreach(var code in instructions)
                {
                    if(code.Calls(original))
                    {
                        // Preserve labels on the first instruction in the replacement.
                        var instance=new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
                        instance.labels.AddRange(code.labels);code.labels.Clear();
                        yield return instance;
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call,replacement);
                    }
                    else yield return code;
                }
            }

            private static bool LinecastOutsideAirframe(Vector3 start,Vector3 end,out RaycastHit hit,TailHook hook)
            {
                // Convex airframe collision envelopes cannot contain a hatch hole.
                // Only this hook's ground/wire probe ignores its own aircraft;
                // native airframe collision, damage and arrestor forces are retained.
                if(hook.GetComponent<Fs20HookFairing>()==null)
                    return Physics.Linecast(start,end,out hit);
                var fairing=hook.GetComponent<Fs20HookFairing>();
                if((fairing.departure.Locked || !(bool)Deployed.GetValue(hook)) && !(bool)Hooked.GetValue(hook))
                {hit=default(RaycastHit);return false;}
                var delta=end-start;
                var own=hook.unitPart == null ? null : hook.unitPart.parentUnit;
                foreach(var candidate in Physics.RaycastAll(start,delta.normalized,delta.magnitude)
                    .OrderBy(h=>h.distance))
                {
                    var part=candidate.collider.GetComponentInParent<UnitPart>();
                    if(own!=null && part!=null && part.parentUnit==own && !part.IsDetached())continue;
                    hit=candidate;return true;
                }
                hit=default(RaycastHit);return false;
            }
        }
        [HarmonyPatch(typeof(TailHook),"CheckDeployConditions")]
        internal static class DepartureInterlock
        {
            private static bool Prefix(TailHook __instance)
            {
                var fairing=__instance.GetComponent<Fs20HookFairing>();
                if(fairing==null)return true;
                fairing.EnforceDepartureLock();
                var owner=__instance.unitPart==null?null:__instance.unitPart.parentUnit as Aircraft;
                return (bool)Hooked.GetValue(__instance) ||
                    (!fairing.departure.Locked && owner!=null && owner.gearDeployed);
            }
        }
    }
}
