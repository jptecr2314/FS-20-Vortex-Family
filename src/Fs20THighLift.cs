using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20THighLift : MonoBehaviour
    {
        [SerializeField] private AeroPart part;
        [SerializeField] private float baseArea, nativeIncrement, extraIncrement;
        [SerializeField] private Fs20TDamageState[] sections;
        [SerializeField] private Fs20TSlat[] slats;
        private float deployment;
        internal float Deployment => deployment;

        internal void Configure(HighLiftDevice device, AeroPart owner, float ratio,
            Fs20TDamageState[] extensions, Fs20TSlat[] moving)
        {
            part = owner;
            baseArea = owner.GetWingArea();
            nativeIncrement = (float)Fs20Tuning.Field(typeof(HighLiftDevice), "deployedArea").GetValue(device);
            extraIncrement = nativeIncrement * Mathf.Max(0f, ratio - 1f);
            sections = extensions;
            slats = moving;
            // This component owns both lift and animation; do not run two schedules.
            device.enabled = false;
        }

        private void FixedUpdate()
        {
            if (part == null || part.IsDetached() || !(part.parentUnit is Aircraft aircraft) || aircraft.rb == null) return;
            var body = aircraft.CockpitRB();
            Vector3 velocity = body.velocity - aircraft.GetWindVelocity();
            Vector3 local = aircraft.transform.InverseTransformDirection(velocity);
            float alpha = -Mathf.Atan2(local.y, Mathf.Max(1f, local.z)) * Mathf.Rad2Deg;
            float target = Fs20TFlightPolicy.SlatFraction(velocity.magnitude, alpha, aircraft.airDensity);
            deployment = Mathf.MoveTowards(deployment, target, Time.fixedDeltaTime);
            part.SetWingArea(baseArea + nativeIncrement * deployment);
            float total = sections.Sum(section => section.IntactArea);
            if (total > 0f)
                foreach (var section in sections)
                    section.SetSlatArea(extraIncrement * deployment * section.IntactArea / total);
            foreach (var slat in slats) if (slat != null) slat.Deploy(deployment);
        }
    }
}
