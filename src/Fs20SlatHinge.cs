using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20SlatHinge : MonoBehaviour
    {
        [SerializeField] internal Vector3 Axis;
        [SerializeField] internal Quaternion ClosedRotation;

        internal static Vector3 MappedAxis(Vector3 pivot, Vector3 axis, float start, float end)
        {
            Vector3 a = Fs20Geometry.Widen(pivot + axis * start);
            Vector3 b = Fs20Geometry.Widen(pivot + axis * end);
            if ((b - a).sqrMagnitude < 0.000001f)
                throw new InvalidOperationException("FS-20C slat hinge has no span.");
            return (b - a).normalized;
        }

        [HarmonyPatch]
        internal static class AnimatePatch
        {
            private static MethodBase TargetMethod()
            {
                var part = typeof(HighLiftDevice).GetNestedType("MovingPart", BindingFlags.Public | BindingFlags.NonPublic);
                return AccessTools.Method(part, "Animate", new[] { typeof(float) })
                    ?? throw new MissingMethodException("HighLiftDevice.MovingPart.Animate");
            }

            private static void Postfix(float __0, Transform ___transform,
                Vector3 ___anglesRetracted, Vector3 ___anglesDeployed)
            {
                var tSlat = ___transform == null ? null : ___transform.GetComponent<Fs20TSlat>();
                if (tSlat != null) { tSlat.Deploy(__0); return; }
                var hinge = ___transform == null ? null : ___transform.GetComponent<Fs20SlatHinge>();
                if (hinge == null)
                    return;
                float angle = Mathf.Lerp(___anglesRetracted.z, ___anglesDeployed.z, __0) - ___anglesRetracted.z;
                // Keep the native deployment schedule and area change. Only its
                // rigid rotation axis follows the enlarged wing's hinge line.
                ___transform.localRotation = Quaternion.AngleAxis(angle, hinge.Axis) * hinge.ClosedRotation;
            }
        }
    }
}
