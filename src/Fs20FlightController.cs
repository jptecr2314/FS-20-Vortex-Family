using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public enum Fs20FlapMode
    {
        Auto,
        Retracted,
        Takeoff,
        Landing
    }

    internal static class Fs20Tuning
    {
        internal static readonly FieldInfo Airborne = Field(typeof(Aircraft), "airborne");
        internal static void LandingSchedule(ControlsFilter filter, Fs20LandingEnvelope landing, float basePitch, float baseRoll, float baseAlpha)
        {
            var fbw = filter.GetFlyByWire();
            float t = landing.Blend;
            Set(fbw, "maxPitchAngularVel", Mathf.Lerp(basePitch, .45f, t));
            Set(fbw, "maxRollAngularVel", Mathf.Lerp(baseRoll, 1.4f, t));
            Set(fbw, "alphaLimiter", Mathf.Lerp(baseAlpha, 18f, t));
        }

        internal const float EmptyMass = 9500f;
        internal const float DryThrust = 110000f;
        // Native afterburner thrust is additive: 110 + 55 = 165 kN total.
        internal const float AfterburnerThrust = 55000f;
        internal const float ConsumptionScale = 0.85f;
        internal const float TakeoffFlaps = 15f;
        internal const float LandingFlaps = 30f;
        internal const float VectoringFullSpeed = 250f;
        internal const float VectoringMediumSpeed = 400f;
        internal const float VectoringEndSpeed = 550f;
        internal static float VectoringLimit(float speed)
        {
            float limit = Mathf.Lerp(20f, 10f, Mathf.InverseLerp(VectoringFullSpeed, VectoringMediumSpeed, speed));
            return limit * (1f - Mathf.InverseLerp(VectoringMediumSpeed, VectoringEndSpeed, speed));
        }

        internal static FieldInfo Field(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        }

        internal static void Set(object target, string name, object value)
        {
            Field(target.GetType(), name).SetValue(target, value);
        }

        internal static void Configure(Transform root)
        {
            var engine = root.GetComponentInChildren<Turbojet>(true);
            engine.maxThrust = DryThrust;
            foreach (string name in new[]
            {
                "fuelConsumptionMin",
                "fuelConsumptionMax"
            }

            )
            {
                var field = Field(typeof(Turbojet), name);
                field.SetValue(engine, (float)field.GetValue(engine) * ConsumptionScale);
            }

            var nozzle = root.GetComponentInChildren<JetNozzle>(true);
            var burners = (Array)Field(typeof(JetNozzle), "afterburners").GetValue(nozzle);
            if (burners.Length != 1)
                throw new InvalidOperationException("Unexpected FS-20 afterburner layout.");
            var burner = burners.GetValue(0);
            Set(burner, "thrust", AfterburnerThrust);
            var consumption = Field(burner.GetType(), "fuelConsumption");
            consumption.SetValue(burner, (float)consumption.GetValue(burner) * ConsumptionScale);
            burners.SetValue(burner, 0);
            Set(engine, "thrustVectoring", new Vector3(20f, 0f, 0f));
            Set(engine, "thrustVectoringMaxAirspeed", VectoringEndSpeed);
            var filter = root.GetComponentInChildren<ControlsFilter>(true);
            var flyByWire = Field(typeof(ControlsFilter), "flyByWire").GetValue(filter);
            Set(flyByWire, "gLimitPositive", Fs20FlightEnvelope.MaximumG);
            Set(flyByWire, "cornerSpeed", 140f);
            Set(flyByWire, "takeoffSpeed", 42f);
            Set(flyByWire, "maxPitchAngularVel", 0.85f);
            Set(flyByWire, "alphaLimiter", 26f);
            Set(flyByWire, "alphaLimiterStrength", 0.12f);
            Set(flyByWire, "dFactorFast", 1.2f);
            // Wider wings need controlled roll response as dynamic pressure rises.
            Set(flyByWire, "maxRollAngularVel", 4.5f);
            var hover = Field(typeof(ControlsFilter), "autoHover").GetValue(filter);
            Set(hover, "Enabled", false);
            foreach (var surface in root.GetComponentsInChildren<ControlSurface>(true))
            {
                if (!surface.name.StartsWith("elevator_", StringComparison.Ordinal) && !surface.name.StartsWith("aileron_", StringComparison.Ordinal))
                    continue;
                var marker = surface.gameObject.AddComponent<Fs20Elevon>();
                marker.PitchRange = (float)Field(typeof(ControlSurface), "pitchRange").GetValue(surface);
                marker.RollRange = (float)Field(typeof(ControlSurface), "rollRange").GetValue(surface);
                marker.FlapFraction = surface.name.StartsWith("elevator_", StringComparison.Ordinal) ? 1f : 0.4f;
                Set(surface, "servoSpeed", 90f);
            }

            foreach (var device in root.GetComponentsInChildren<HighLiftDevice>(true))
            {
                Set(device, "speedDeployed", 100f);
                Set(device, "speedRetracted", 160f);
                Set(device, "alphaMin", 8f);
                Set(device, "alphaFactor", 0.35f);
            }
        }
    }

    public sealed class Fs20Elevon : MonoBehaviour
    {
        [SerializeField]
        internal float PitchRange;
        [SerializeField]
        internal float RollRange;
        [SerializeField]
        internal float FlapFraction;
        internal Fs20FlightController Controller;
        private void Awake()
        {
            ResolveController();
        }

        internal void ResolveController()
        {
            if (Controller != null)
                return;
            var part = GetComponentInParent<UnitPart>();
            var owner = part == null ? null : part.parentUnit as Aircraft;
            Controller = owner == null ? GetComponentInParent<Fs20FlightController>() : owner.GetComponent<Fs20FlightController>();
        }
    }

    public sealed class Fs20FlightController : MonoBehaviour
    {
        private static readonly FieldInfo Vectoring = Fs20Tuning.Field(typeof(Turbojet), "thrustVectoring");
        private static readonly FieldInfo WindVelocity = Fs20Tuning.Field(typeof(Aircraft), "windVelocity");
        private Aircraft aircraft;
        private Turbojet engine;
        private readonly Fs20FlightEnvelope envelope = new Fs20FlightEnvelope();
        private readonly Fs20LandingEnvelope landing = new Fs20LandingEnvelope();
        private static readonly FieldInfo GLimit = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "gLimitPositive");
        internal float FlapDegrees { get; private set; }
        internal float Airspeed { get; private set; }
        internal float EffectiveGLimit => envelope.LimitG;

        private void Awake()
        {
            aircraft = GetComponent<Aircraft>();
            engine = GetComponentInChildren<Turbojet>(true);
        }

        internal void Sample()
        {
            if (aircraft == null || aircraft.rb == null)
                return;
            Airspeed = Mathf.Max(0f, aircraft.speed);
            Vector3 local = aircraft.transform.InverseTransformDirection(aircraft.rb.velocity - (Vector3)WindVelocity.GetValue(aircraft));
            float alpha = -Mathf.Atan2(local.y, Mathf.Max(1f, local.z)) * Mathf.Rad2Deg;
            float lowSpeed = 1f - Mathf.InverseLerp(100f, 190f, Airspeed);
            float requested;
            switch (VortexConventionalPlugin.FlapMode)
            {
                case Fs20FlapMode.Retracted:
                    requested = 0f;
                    break;
                case Fs20FlapMode.Takeoff:
                    requested = Fs20Tuning.TakeoffFlaps;
                    break;
                case Fs20FlapMode.Landing:
                    requested = Fs20Tuning.LandingFlaps;
                    break;
                default:
                    requested = aircraft.gearDeployed ? (aircraft.GetInputs().throttle < 0.65f && aircraft.radarAlt > 5f ? Fs20Tuning.LandingFlaps : Fs20Tuning.TakeoffFlaps) : 8f * Mathf.InverseLerp(5f, 20f, alpha) * envelope.FlapScale;
                    break;
            }

            FlapDegrees = Mathf.MoveTowards(FlapDegrees, requested * lowSpeed, 12f * Time.fixedDeltaTime);
            float vectorLimit = Fs20Tuning.VectoringLimit(Airspeed);
            if (engine == null && aircraft.partLookup != null)
                engine = aircraft.partLookup.Select(p => p.GetComponent<Turbojet>()).FirstOrDefault(e => e != null);
            if (engine != null)
                Vectoring.SetValue(engine, new Vector3(vectorLimit, 0f, 0f));
        }

        internal void PrepareControls(ControlsFilter filter, ControlInputs inputs, Rigidbody body, bool assisted)
        {
            if (aircraft == null || body == null)
                return;
            Vector3 airflow = aircraft.transform.InverseTransformDirection(body.velocity - (Vector3)WindVelocity.GetValue(aircraft));
            float alpha = -Mathf.Atan2(airflow.y, Mathf.Max(1f, airflow.z)) * Mathf.Rad2Deg;
            // Project native acceleration in G onto the aircraft normal.
            float normalG = Vector3.Dot(aircraft.accel, aircraft.transform.up);
            envelope.Step(inputs.pitch, inputs.roll, aircraft.speed, aircraft.airDensity, aircraft.GetMass(), alpha, normalG, aircraft.gearDeployed, assisted, Time.fixedDeltaTime);
            landing.Step(envelope.Pitch, envelope.Roll, aircraft.speed, aircraft.gearDeployed, (bool)Fs20Tuning.Airborne.GetValue(aircraft), assisted, Time.fixedDeltaTime);
            GLimit.SetValue(filter.GetFlyByWire(), Mathf.Min(envelope.LimitG, Mathf.Lerp(11f, 4f, landing.Blend)));
            Fs20Tuning.LandingSchedule(filter, landing, .85f, 4.5f, 26f);
            inputs.pitch = landing.Pitch;
            inputs.roll = landing.Roll;
        }

        [HarmonyPatch(typeof(ControlsFilter), "Filter")]
        internal static class PrepareEnvelope
        {
            private static void Prefix(ControlsFilter __instance, ControlInputs __0, Rigidbody __2, bool __4, Aircraft ___aircraft, float ___minSpeed, float ___minAlt)
            {
                if (___aircraft == null)
                    return;
                var controller = ___aircraft.GetComponent<Fs20FlightController>();
                var aController = controller == null ? ___aircraft.GetComponent<Fs20AFlightController>() : null;
                if (controller == null && aController == null)
                    return;
                bool active = __4 && __instance.GetFlyByWire().Enabled && ___aircraft.speed >= ___minSpeed;
                if (controller != null)
                    controller.PrepareControls(__instance, __0, __2, active);
                else
                    aController.PrepareControls(__instance, __0, __2, active);
            }
        }

        internal static void ScaleHighLiftArea(HighLiftDevice device, float ratio)
        {
            var field = Fs20Tuning.Field(typeof(HighLiftDevice), "deployedArea");
            field.SetValue(device, (float)field.GetValue(device) * ratio);
        }

        [HarmonyPatch(typeof(Aircraft), "LocalSimFixedUpdate")]
        internal static class FlightSample
        {
            private static void Prefix(Aircraft __instance)
            {
                __instance.GetComponent<Fs20FlightController>()?.Sample();
            }
        }

        [HarmonyPatch(typeof(ControlSurface), "UpdateJobFields")]
        internal static class ElevonJob
        {
            private static void Postfix(ControlSurface __instance, ref PtrAllocation<ControlSurfaceFields> ___JobFields)
            {
                var marker = __instance.GetComponent<Fs20Elevon>();
                if (marker == null)
                    return;
                marker.ResolveController();
                if (marker.Controller == null)
                    return;
                ref ControlSurfaceFields fields = ref ___JobFields.Ref();
                if (fields.IsDetached)
                    return;
                var controller = marker.Controller;
                float gain = Mathf.Lerp(1f, 0.65f, Mathf.InverseLerp(140f, 300f, controller.Airspeed));
                float command = marker.PitchRange * fields.controlInputs.pitch * gain + marker.RollRange * fields.controlInputs.roll * gain;
                // Reduce flap bias under stick demand to stay within surface travel.
                float reserve = 1f - 0.8f * Mathf.Clamp01(Mathf.Abs(command) / 40f);
                float angle = Mathf.Clamp(command - controller.FlapDegrees * marker.FlapFraction * reserve, -40f, 40f);
                fields.pitchRange = 40f;
                fields.rollRange = 0f;
                fields.yawRange = 0f;
                fields.flap = false;
                fields.currentRoll = 0f;
                fields.currentYaw = 0f;
                fields.controlInputs.pitch = angle / 40f;
            }
        }
    }
}
