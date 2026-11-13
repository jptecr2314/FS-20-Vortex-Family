using System;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TStructure
    {
        internal const float MaximumG = 11f;
        internal const float DesignGrossMass = 16000f;
        internal const float Reserve = 1.35f;
        internal const float NativeJointScale = 10f;

        internal static void Apply(Transform root)
        {
            var parts = root.GetComponentsInChildren<AeroPart>(true).Where(IsWing).ToArray();
            float area = parts.Sum(p => p.GetWingArea());
            if (!(area > 1f)) throw new InvalidOperationException("FS-20T wing load area is invalid.");
            foreach (var owner in parts)
            {
                bool panel = owner.GetComponent<Fs20TDamageState>() != null;
                // A native support may carry several descendants. Assess the full branch at
                // each attachment rather than treating a small component as the whole wing.
                var branch = panel ? new[] { owner } : owner.GetComponentsInChildren<AeroPart>(true).Where(IsWing).ToArray();
                foreach (var joint in owner.Joints)
                {
                    Vector3 anchor = joint.anchor != null ? joint.anchor.position : owner.transform.position;
                    float force = 0f, moment = 0f;
                    foreach (var part in branch)
                    {
                        var marker = part.GetComponent<Fs20TPartPhysics>();
                        if (marker == null) throw new InvalidOperationException("FS-20T structural force point missing: " + part.name);
                        var tank = part.GetComponent<FuelTank>();
                        float fuel = tank == null ? 0f : Mathf.Max(0f,
                            (float)Fs20Tuning.Field(typeof(FuelTank), "fuelCapacity").GetValue(tank) -
                            (float)Fs20Tuning.Field(typeof(FuelTank), "fuelMass").GetValue(tank));
                        float lift = DesignGrossMass * MaximumG * 9.81f * part.GetWingArea() / area;
                        float inertia = (part.mass + fuel) * MaximumG * 9.81f;
                        // Sum magnitudes: conservative positive-G attachment budget, including
                        // a central payload up to the native 16 t gross-mass reference.
                        force += lift + inertia;
                        moment += lift * Vector3.Distance(marker.WorldForcePoint, anchor) +
                            inertia * Vector3.Distance(marker.WorldMassCenter, anchor);
                    }
                    float requiredForce = Mathf.Max(250f, force * Reserve) / NativeJointScale;
                    float requiredTorque = Mathf.Max(moment * Reserve, force * Reserve * .15f) / NativeJointScale;
                    // The old delta joints inherited a fraction of an entire wing's strength.
                    // Give those eight panels a finite load-based budget; retain the native
                    // support structure's existing strength wherever it is already greater.
                    joint.breakForce = panel ? requiredForce : Mathf.Max(joint.breakForce, requiredForce);
                    joint.breakTorque = panel ? requiredTorque : Mathf.Max(joint.breakTorque, requiredTorque);
                    joint.solverIterations = Mathf.Max(joint.solverIterations, 12);
                }
            }
            VortexConventionalPlugin.ReportAssetState("FS-20T 11 G structure configured: 16 t reference, finite attachment force/torque budgets and native damage weakening retained.");
        }

        private static bool IsWing(AeroPart p) => p.name.StartsWith("wing", StringComparison.Ordinal) ||
            p.name.StartsWith("elevator", StringComparison.Ordinal) || p.name.StartsWith("aileron", StringComparison.Ordinal) ||
            p.GetComponent<Fs20TDamageState>() != null;
    }
}
