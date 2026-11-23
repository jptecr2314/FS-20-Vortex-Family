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
        private float configuredG = Fs20AEnergyEnvelope.MaximumG, configuredAlpha = Fs20FlightEnvelope.MaximumAlpha;
        internal void CaptureSettings(ControlsFilter filter)
        {
            configuredG = (float)G.GetValue(filter.GetFlyByWire());
            configuredAlpha = (float)Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "alphaLimiter").GetValue(filter.GetFlyByWire());
        }
        private void Awake()
        {
            aircraft = GetComponent<Aircraft>();
            CaptureSettings(GetComponentInChildren<ControlsFilter>(true));
        }

        internal void PrepareControls(ControlsFilter filter, ControlInputs inputs, Rigidbody body, bool assisted)
        {
            if (aircraft == null || body == null)
                return;
            var velocity = Quaternion.Inverse(body.rotation) * (body.velocity - (Vector3)Wind.GetValue(aircraft));
            float alpha = -Mathf.Atan2(velocity.y, Mathf.Max(1f, velocity.z)) * Mathf.Rad2Deg;
            var up = body.rotation * Vector3.up;
            float normalG = Vector3.Dot(aircraft.accel - Physics.gravity / 9.81f, up);
            float ceiling = Mathf.Min(configuredG, Fs20AEnergyEnvelope.MaximumG * Fs20AEnergyEnvelope.ReferenceMass / Mathf.Max(Fs20AEnergyEnvelope.ReferenceMass, aircraft.GetMass()));
            G.SetValue(filter.GetFlyByWire(), ceiling);
            envelope.Step(inputs.pitch, inputs.roll, aircraft.GetMass(), alpha, normalG, assisted, Time.fixedDeltaTime,
                velocity.magnitude, Fs20Tuning.NativeRatePerG(filter, aircraft, ceiling),
                -Vector3.Dot(body.angularVelocity, body.rotation * Vector3.right),
                Vector3.Dot(-Physics.gravity / 9.81f, up), 0f,
                Vector3.Dot(aircraft.accel * 9.81f, body.rotation * velocity.normalized), configuredG, configuredAlpha);
            if (assisted && (bool)Fs20Tuning.Airborne.GetValue(aircraft))
                Fs20Tuning.TrackProtection(filter, aircraft, normalG, alpha, envelope.LimitG, configuredAlpha, 1f, envelope.LoadRate);
            inputs.pitch = envelope.Pitch;
            inputs.roll = envelope.Roll;
        }

        internal static void Configure(Transform root)
        {
            var filter = root.GetComponentInChildren<ControlsFilter>(true);
            var fbw = filter.GetFlyByWire();
            Fs20Tuning.Set(fbw, "gLimitPositive", Fs20AEnergyEnvelope.MaximumG);
            Fs20Tuning.Set(fbw, "cornerSpeed", 155f);
            Fs20Tuning.Set(fbw, "takeoffSpeed", 70f);
            Fs20Tuning.Set(fbw, "maxPitchAngularVel", .82f);
            Fs20Tuning.Set(fbw, "maxRollAngularVel", 5f);
            Fs20Tuning.Set(fbw, "alphaLimiter", Fs20FlightEnvelope.MaximumAlpha);
            Fs20Tuning.Set(fbw, "alphaLimiterStrength", .08f);
            Fs20Tuning.Set(fbw, "dFactorFast", 1.6f);
            root.gameObject.AddComponent<Fs20AFlightController>();
        }
    }
}
