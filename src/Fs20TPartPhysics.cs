using HarmonyLib;
using System;
using System.Reflection;
using NuclearOption.Jobs;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20TPartPhysics : MonoBehaviour
    {
        [SerializeField] private AeroPart part;
        [SerializeField] private Transform lift, massCenter;
        [SerializeField] private Vector3 forcePointInLift;
        [SerializeField] private Bounds storedCollisionBounds;
        [SerializeField] private bool hasStoredCollisionBounds;
        private static readonly FieldInfo CollisionSize = Fs20Tuning.Field(typeof(UnitPart), "collisionSize");
        private static readonly FieldInfo CollisionBounds = Fs20Tuning.Field(typeof(UnitPart), "bounds");
        private static readonly FieldInfo Fans = Fs20Tuning.Field(typeof(SwivelDuctSystem), "liftFans");
        private static readonly Type FanType = typeof(SwivelDuctSystem).GetNestedType("LiftFan", BindingFlags.NonPublic);
        private static readonly FieldInfo FanCenterTime = Fs20Tuning.Field(FanType, "lastCoMCheck");
        private static readonly FieldInfo FanOffsetTime = Fs20Tuning.Field(FanType, "lastOffsetCalc");
        internal AeroPart Part => part;
        internal Vector3 WorldMassCenter => massCenter.position;
        internal Vector3 WorldForcePoint => lift.TransformPoint(forcePointInLift);
        private static bool IsT(UnitPart owner) => owner.parentUnit is Aircraft aircraft &&
            aircraft.definition != null && aircraft.definition.jsonKey == Fs20TAirframe.Key;

        internal void StoreCollisionBounds(Bounds bounds)
        {
            var size = bounds.extents;
            if (!(size.x > 0f && size.y > 0f && size.z > 0f) ||
                float.IsInfinity(size.x) || float.IsInfinity(size.y) || float.IsInfinity(size.z))
                throw new InvalidOperationException("FS-20T collision bounds are degenerate: " + part.name);
            storedCollisionBounds = bounds;
            hasStoredCollisionBounds = true;
            RestoreCollisionBounds();
        }

        private void Awake() => RestoreCollisionBounds();
        private void RestoreCollisionBounds()
        {
            if (!hasStoredCollisionBounds || part == null) return;
            CollisionSize.SetValue(part, storedCollisionBounds.extents);
            CollisionBounds.SetValue(part, storedCollisionBounds);
        }

        internal static Fs20TPartPhysics Configure(AeroPart owner, Vector3 center, Vector3 forcePoint)
        {
            if (!owner.TryGetComponent<Fs20TPartPhysics>(out var marker)) marker = owner.gameObject.AddComponent<Fs20TPartPhysics>();
            marker.part = owner;
            marker.lift = owner.GetLiftTransform() ?? owner.transform;
            marker.massCenter = (Transform)Fs20Tuning.Field(typeof(UnitPart), "centerOfMass").GetValue(owner);
            if (marker.massCenter == null || marker.massCenter.parent != owner.transform)
            {
                marker.massCenter = new GameObject("FS20T mass center").transform;
                marker.massCenter.SetParent(owner.transform, false);
            }
            marker.massCenter.position = center;
            Fs20Tuning.Set(owner, "centerOfMass", marker.massCenter);
            marker.forcePointInLift = marker.lift.InverseTransformPoint(forcePoint);
            Fs20Tuning.Set(owner, "centerOfLift", marker.lift.InverseTransformDirection(forcePoint - center));
            return marker;
        }

        internal static void ApplySimpleMass(Aircraft aircraft)
        {
            if (aircraft == null || !aircraft.simplePhysics || aircraft.rb == null || aircraft.partLookup == null) return;
            float mass = 0f;
            Vector3 moment = Vector3.zero;
            foreach (var item in aircraft.partLookup)
            {
                if (item == null || item.IsDetached()) continue;
                var marker = item.GetComponent<Fs20TPartPhysics>();
                if (marker == null) continue;
                mass += item.mass;
                moment += item.mass * marker.WorldMassCenter;
            }
            if (!(mass > 0f)) return;
            aircraft.rb.mass = mass;
            aircraft.rb.centerOfMass = aircraft.rb.transform.InverseTransformPoint(moment / mass);
            aircraft.rb.ResetInertiaTensor();
            RefreshVtolBalance(aircraft);
        }

        private static void RefreshVtolBalance(Aircraft aircraft)
        {
            var swivel = aircraft.GetComponent<SwivelDuctSystem>();
            if (swivel == null) return;
            foreach (var fan in (Array)Fans.GetValue(swivel))
            {
                // The native fan normally caches COM for ten seconds.
                FanCenterTime.SetValue(fan, float.NegativeInfinity);
                FanOffsetTime.SetValue(fan, float.NegativeInfinity);
            }
        }

        [HarmonyPatch(typeof(AeroPart), "CreateRB")]
        internal static class CreateBody
        {
            private static void Postfix(AeroPart __instance)
            {
                var marker = __instance.GetComponent<Fs20TPartPhysics>();
                if (marker == null || __instance.rb == null || __instance.rb.transform != __instance.transform) return;
                __instance.rb.centerOfMass = __instance.rb.transform.InverseTransformPoint(marker.WorldMassCenter);
                __instance.rb.ResetInertiaTensor();
            }
        }

        [HarmonyPatch(typeof(AeroPart), "CreateJoints")]
        internal static class CreateJoints
        {
            private static void Postfix(AeroPart __instance)
            {
                if (__instance.GetComponent<Fs20TPartPhysics>() == null || __instance.rb == null) return;
                foreach (var entry in __instance.Joints)
                {
                    if (entry.joint == null || entry.anchor == null || entry.joint.connectedBody == null) continue;
                    Vector3 world = entry.anchor.position;
                    entry.joint.autoConfigureConnectedAnchor = false;
                    entry.joint.anchor = __instance.rb.transform.InverseTransformPoint(world);
                    entry.joint.connectedAnchor = entry.joint.connectedBody.transform.InverseTransformPoint(world);
                }
            }
        }

        [HarmonyPatch(typeof(Aircraft), "SetSimplePhysics")]
        internal static class SimpleBody
        {
            private static void Postfix(Aircraft __instance)
            {
                if (__instance.GetComponent<Fs20TFlightController>() != null) ApplySimpleMass(__instance);
            }
        }

        [HarmonyPatch(typeof(Aircraft), "SetComplexPhysics")]
        internal static class ComplexBody
        {
            private static void Postfix(Aircraft __instance)
            {
                if (__instance.GetComponent<Fs20TFlightController>() == null || __instance.partLookup == null) return;
                // Native SetComplexPhysics resets the root COM after CreateRB.
                foreach (var item in __instance.partLookup)
                {
                    var marker = item.GetComponent<Fs20TPartPhysics>();
                    if (marker == null || item.rb == null || item.rb.transform != item.transform) continue;
                    item.rb.centerOfMass = item.rb.transform.InverseTransformPoint(marker.WorldMassCenter);
                    item.rb.ResetInertiaTensor();
                }
                RefreshVtolBalance(__instance);
            }
        }

        [HarmonyPatch(typeof(UnitPart), "ModifyMass")]
        internal static class PayloadMass
        {
            private static void Postfix(UnitPart __instance)
            {
                if (!IsT(__instance)) return;
                if (__instance.GetComponent<Fs20TPartPhysics>() != null && __instance.parentUnit is Aircraft aircraft)
                {
                    ApplySimpleMass(aircraft);
                    if (!aircraft.simplePhysics) RefreshVtolBalance(aircraft);
                }
            }
        }

        [HarmonyPatch(typeof(AeroPart), "UpdateJobFields")]
        internal static class PointVelocity
        {
            private static void Postfix(AeroPart __instance, ref PtrAllocation<AeroPartFields> ___JobFields)
            {
                if (!IsT(__instance)) return;
                var marker = __instance.GetComponent<Fs20TPartPhysics>();
                if (marker == null || __instance.rb == null || !___JobFields.IsCreated) return;
                // Include angular velocity at the section, not just COM translation.
                ref AeroPartFields fields = ref ___JobFields.Ref();
                fields.velocity = __instance.rb.GetPointVelocity(marker.WorldForcePoint);
                // UnitPart's protected bounds are not serialized into spawned clones.
                // Child convex cells cannot populate native GetComponent<Collider>().
                if (marker.hasStoredCollisionBounds) fields.collisionSize = marker.storedCollisionBounds.extents;
            }
        }

        [HarmonyPatch(typeof(AeroPart), "ApplyJobFields")]
        internal static class ApplyMoment
        {
            private static void Prefix(AeroPart __instance, ref PtrAllocation<AeroPartFields> ___JobFields)
            {
                if (!IsT(__instance) && !VortexConventionalPlugin.RecordBDiagnostics) return;
                var marker = __instance.GetComponent<Fs20TPartPhysics>();
                if (__instance.rb == null || !___JobFields.IsCreated) return;
                ref AeroPartFields fields = ref ___JobFields.Ref();
                if (marker == null)
                {
                    if (VortexConventionalPlugin.RecordBDiagnostics)
                        (__instance.parentUnit as Aircraft)?.GetComponent<Fs20TFlightDiagnostics>()?.Record(__instance, fields);
                    return;
                }
                if (fields.hasForce != JobForceType.NoForce)
                {
                    fields.torque = Vector3.Cross(marker.WorldForcePoint - __instance.rb.worldCenterOfMass, fields.force);
                    fields.hasForce = JobForceType.ForceAndTorque;
                }
                (__instance.parentUnit as Aircraft)?.GetComponent<Fs20TFlightDiagnostics>()?.Record(__instance, fields);
            }
        }
    }
}
