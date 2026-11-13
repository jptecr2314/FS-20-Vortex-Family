using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TMass
    {
        internal struct Budget
        {
            internal float Before, After;
            internal Vector3 CenterShift;
        }

        internal static Budget Apply(Transform root)
        {
            var parts = root.GetComponentsInChildren<UnitPart>(true);
            var before = parts.Sum(p => p.GetMass());
            var centerBefore = Center(root, parts, before);
            var baseMass = Fs20Tuning.Field(typeof(UnitPart), "baseMass");
            foreach (var part in parts)
            {
                if (!(part is AeroPart) || part.mass <= 0f) continue;
                float ratio = RetainedFraction(part.name, part.transform == root, part.GetComponent<Fs20TDamageState>() != null);
                if (ratio == 1f) continue;
                part.mass *= ratio;
                baseMass.SetValue(part, part.mass);
            }
            var after = parts.Sum(p => p.GetMass());
            return new Budget { Before = before, After = after, CenterShift = Center(root, parts, after) - centerBefore };
        }

        private static Vector3 Center(Transform root, UnitPart[] parts, float mass)
        {
            Vector3 moment = Vector3.zero;
            foreach (var part in parts) moment += part.GetMass() * root.InverseTransformPoint(part.transform.position);
            return moment / mass;
        }

        internal static float RetainedFraction(string name, bool mainBody, bool deltaPanel)
        {
            // Game mass budget for composite skins and structure. Keep the native
            // propulsion, gear bays and equipment bays outside this adjustment.
            if (deltaPanel) return .46f;
            if (mainBody) return .62f;
            if (name.EndsWith("_L", System.StringComparison.Ordinal) || name.EndsWith("_R", System.StringComparison.Ordinal))
                name = name.Substring(0, name.Length - 2);
            switch (name)
            {
                case "cockpit": case "nose": case "intake": return .62f;
                case "fuselage": case "tail": return .53f;
                case "wingRoot": case "wing1a": case "wing1b": case "wing2b": case "wingtip": return .58f;
                case "wing2a": case "aileron": case "elevator": case "vstab": case "rudder": return .60f;
                default: return 1f;
            }
        }
    }
}
