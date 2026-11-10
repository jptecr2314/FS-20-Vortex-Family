using System.Reflection;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20AFlightController : MonoBehaviour
    {
        private readonly Fs20AEnergyEnvelope envelope = new Fs20AEnergyEnvelope();
        private readonly Fs20LandingEnvelope landing = new Fs20LandingEnvelope();
        private static readonly FieldInfo Wind = Fs20Tuning.Field(typeof(Aircraft), "windVelocity");
        private static readonly FieldInfo G = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "gLimitPositive");
        private Aircraft aircraft;
        private void Awake()
        {
            aircraft = GetComponent<Aircraft>();
        }

        internal void PrepareControls(ControlsFilter filter, ControlInputs inputs, Rigidbody body, bool assisted)
        {
            if (aircraft == null || body == null)
                return;
            var velocity = aircraft.transform.InverseTransformDirection(body.velocity - (Vector3)Wind.GetValue(aircraft));
            float alpha = -Mathf.Atan2(velocity.y, Mathf.Max(1f, velocity.z)) * Mathf.Rad2Deg;
            float normalG = Vector3.Dot(aircraft.accel, aircraft.transform.up);
            envelope.Step(inputs.pitch, inputs.roll, aircraft.GetMass(), alpha, normalG, assisted, Time.fixedDeltaTime);
            landing.Step(envelope.Pitch, envelope.Roll, aircraft.speed, aircraft.gearDeployed, (bool)Fs20Tuning.Airborne.GetValue(aircraft), assisted, Time.fixedDeltaTime);
            G.SetValue(filter.GetFlyByWire(), Mathf.Min(envelope.LimitG, Mathf.Lerp(11f, 4f, landing.Blend)));
            Fs20Tuning.LandingSchedule(filter, landing, .82f, 4.8f, 24f);
            inputs.pitch = landing.Pitch;
            inputs.roll = landing.Roll;
        }

        internal static void Configure(Transform root)
        {
            var filter = root.GetComponentInChildren<ControlsFilter>(true);
            var fbw = filter.GetFlyByWire();
            Fs20Tuning.Set(fbw, "gLimitPositive", Fs20AEnergyEnvelope.MaximumG);
            Fs20Tuning.Set(fbw, "cornerSpeed", 155f);
            Fs20Tuning.Set(fbw, "takeoffSpeed", 70f);
            Fs20Tuning.Set(fbw, "maxPitchAngularVel", .82f);
            Fs20Tuning.Set(fbw, "maxRollAngularVel", 4.8f);
            Fs20Tuning.Set(fbw, "alphaLimiter", 24f);
            Fs20Tuning.Set(fbw, "alphaLimiterStrength", .12f);
            Fs20Tuning.Set(fbw, "dFactorFast", 1.15f);
            root.gameObject.AddComponent<Fs20AFlightController>();
        }
    }
}
