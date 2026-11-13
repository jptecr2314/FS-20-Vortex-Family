using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // No extra UnitPart or network damage IDs: each cell forwards to its
    // existing native wing section, including armour and detachment.
    public sealed class Fs20TDamageProxy : MonoBehaviour, IDamageable
    {
        [SerializeField] internal UnitPart Owner;
        public void TakeDamage(float a, float b, float c, float d, float e, PersistentID source)
        {
            if (Owner != null && Fs20TDamageGate.Allow(Owner, false)) ((IDamageable)Owner).TakeDamage(a, b, c, d, e, source);
        }
        public void ApplyDamage(float a, float b, float c, float d) { if (Owner != null) ((IDamageable)Owner).ApplyDamage(a, b, c, d); }
        public void TakeShockwave(Vector3 origin, float a, float b)
        {
            if (Owner != null && Fs20TDamageGate.Allow(Owner, true)) ((IDamageable)Owner).TakeShockwave(origin, a, b);
        }
        public void Detach(Vector3 velocity, Vector3 angularVelocity) { if (Owner != null) ((IDamageable)Owner).Detach(velocity, angularVelocity); }
        public ArmorProperties GetArmorProperties() => Owner == null ? default(ArmorProperties) : ((IDamageable)Owner).GetArmorProperties();
        public Unit GetUnit() => Owner == null ? null : ((IDamageable)Owner).GetUnit();
        public float GetMass() => Owner == null ? 0 : ((IDamageable)Owner).GetMass();
        public Transform GetTransform() => Owner == null ? transform : ((IDamageable)Owner).GetTransform();

        [HarmonyPatch(typeof(DamageEffects), "BlastFrag")]
        internal static class FragmentScope
        {
            private static void Prefix() => Fs20TDamageGate.Begin(false);
            private static void Finalizer() => Fs20TDamageGate.End(false);
        }
        [HarmonyPatch(typeof(Explosion), "SimulateForce")]
        internal static class ShockScope
        {
            private static void Prefix() => Fs20TDamageGate.Begin(true);
            private static void Finalizer() => Fs20TDamageGate.End(true);
        }
    }
}
