using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private static readonly FieldInfo NativeCorner = Field(typeof(ControlsFilter.FlyByWire), "cornerSpeed");
        private static readonly FieldInfo NativePitchRate = Field(typeof(ControlsFilter.FlyByWire), "maxPitchAngularVel");
        private static readonly FieldInfo NativeLimitBlend = Field(typeof(ControlsFilter.FlyByWire), "limitFactorSmoothed");
        private static readonly FieldInfo PitchBias = Field(typeof(ControlsFilter.FlyByWire), "pitchAdjuster");
        private static readonly FieldInfo PitchIntegral = Field(typeof(ControlsFilter.FlyByWire), "i");
        internal static float NativeRatePerG(ControlsFilter filter, Aircraft aircraft, float ceiling)
        {
            var fbw = filter.GetFlyByWire();
            float corner = Mathf.Max(1f, (float)NativeCorner.GetValue(fbw));
            float pressure = aircraft.speed * aircraft.speed * aircraft.airDensity / (corner * corner * 1.225f);
            float physical = 9.81f / Mathf.Max(aircraft.speed, corner * .75f) *
                (pressure < 1f ? Mathf.Clamp(pressure, .3f, 1f) : 1f);
            // The native filter interpolates from direct rate to G mapping after toggling assist.
            float blend = Mathf.Lerp((float)NativeLimitBlend.GetValue(fbw), 1f, Time.fixedDeltaTime);
            return Mathf.Lerp((float)NativePitchRate.GetValue(fbw) / Mathf.Max(1f, ceiling), physical, blend);
        }
        internal static void TrackProtection(ControlsFilter filter, Aircraft aircraft, float normalG, float alpha, float limitG, float limitAlpha, float weight, float loadRate)
        {
            if (weight <= 0f || !float.IsFinite(normalG) || !float.IsFinite(alpha)) return;
            // A gust or coupled roll can create an overload with neutral stick.
            // Track the actual load through the native actuator state instead of
            // erasing that state. The correction is continuous and rate bounded;
            // it vanishes inside the envelope and never alters the raw roll axis.
            float predictedLoad = normalG + .20f * loadRate;
            float loadError = Mathf.Max(0f, predictedLoad - limitG - .25f) + Mathf.Min(0f, predictedLoad + limitG * .5f + .25f);
            float incidenceError = Mathf.Max(0f, alpha - limitAlpha - .5f);
            var fbw = filter.GetFlyByWire();
            float corner = Mathf.Max(1f, (float)NativeCorner.GetValue(fbw));
            float pressure = aircraft.speed * aircraft.speed * aircraft.airDensity / (corner * corner * 1.225f);
            float correction = Mathf.Clamp(.6f * loadError + .15f * incidenceError, -2f, 2f) / Mathf.Max(1f, pressure);
            if (Mathf.Abs(correction) < .00001f) return;
            float bias = (float)PitchBias.GetValue(fbw);
            PitchBias.SetValue(fbw, Mathf.Clamp(bias + correction * Mathf.Clamp01(weight) * Time.fixedDeltaTime, -1f, 1f));
        }
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
            Set(flyByWire, "maxPitchAngularVel", Fs20FlightEnvelope.CombatPitchRate);
            Set(flyByWire, "alphaLimiter", Fs20FlightEnvelope.MaximumAlpha);
            // Native multiplier is 1 - min(alpha excess, 10) * strength.
            // Values above .1 can reverse the requested rate beyond the alpha limit.
            Set(flyByWire, "alphaLimiterStrength", 0.08f);
            Set(flyByWire, "pFactorFast", 9.2f);
            Set(flyByWire, "dFactorFast", 1.6f);
            Set(flyByWire, "maxRollAngularVel", Fs20FlightEnvelope.CombatRollRate);
            Set(flyByWire, "rollTightness", .75f);
            Set(flyByWire, "rollTrimRate", .02f);
            Set(flyByWire, "rollTrimLimit", .12f);
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
        private float configuredG = Fs20FlightEnvelope.MaximumG, configuredAlpha = Fs20FlightEnvelope.MaximumAlpha;
        internal void CaptureSettings(ControlsFilter filter)
        {
            configuredG = (float)GLimit.GetValue(filter.GetFlyByWire());
            configuredAlpha = (float)Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "alphaLimiter").GetValue(filter.GetFlyByWire());
        }
        private static readonly FieldInfo GLimit = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "gLimitPositive");
        private static readonly FieldInfo Cockpit = Fs20Tuning.Field(typeof(Aircraft), "cockpit");
        internal float FlapDegrees { get; private set; }
        internal float Airspeed { get; private set; }
        internal float EquivalentAirspeed { get; private set; }
        internal float EffectiveGLimit => envelope.LimitG;

        private void Awake()
        {
            aircraft = GetComponent<Aircraft>();
            engine = GetComponentInChildren<Turbojet>(true);
            CaptureSettings(GetComponentInChildren<ControlsFilter>(true));
        }

        internal void Sample()
        {
            if(aircraft==null)return;
            var cockpit=(UnitPart)Cockpit.GetValue(aircraft);
            var body=cockpit!=null && cockpit.rb!=null ? cockpit.rb : aircraft.rb;
            if (body == null)
                return;
            Vector3 local = Quaternion.Inverse(body.rotation)*(body.velocity - (Vector3)WindVelocity.GetValue(aircraft));
            Airspeed = local.magnitude;
            EquivalentAirspeed=Fs20FlightEnvelope.EquivalentSpeed(Airspeed,aircraft.airDensity);
            float alpha = -Mathf.Atan2(local.y, Mathf.Max(1f, local.z)) * Mathf.Rad2Deg;
            float lowSpeed = 1f - Mathf.InverseLerp(100f, 190f, EquivalentAirspeed);
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
            Vector3 airflow = Quaternion.Inverse(body.rotation)*(body.velocity - (Vector3)WindVelocity.GetValue(aircraft));
            Airspeed=airflow.magnitude;
            EquivalentAirspeed=Fs20FlightEnvelope.EquivalentSpeed(Airspeed,aircraft.airDensity);
            float alpha = -Mathf.Atan2(airflow.y, Mathf.Max(1f, airflow.z)) * Mathf.Rad2Deg;
            float normalG = Vector3.Dot(aircraft.accel-Physics.gravity/9.81f, body.rotation*Vector3.up);
            float ceiling = Mathf.Min(configuredG, Fs20FlightEnvelope.TargetLimit(Airspeed, aircraft.airDensity, aircraft.GetMass(), aircraft.gearDeployed));
            GLimit.SetValue(filter.GetFlyByWire(), ceiling);
            envelope.Step(inputs.pitch, inputs.roll, Airspeed, aircraft.airDensity, aircraft.GetMass(), alpha, normalG, aircraft.gearDeployed, assisted, Time.fixedDeltaTime,
                Fs20Tuning.NativeRatePerG(filter, aircraft, ceiling),
                -Vector3.Dot(body.angularVelocity, body.rotation * Vector3.right),
                Vector3.Dot(-Physics.gravity / 9.81f, body.rotation * Vector3.up), 0f,
                Vector3.Dot(aircraft.accel * 9.81f, body.rotation * airflow.normalized), configuredG, configuredAlpha);
            if (assisted && (bool)Fs20Tuning.Airborne.GetValue(aircraft))
                Fs20Tuning.TrackProtection(filter, aircraft, normalG, alpha, envelope.LimitG, configuredAlpha, 1f, envelope.LoadRate);
            inputs.pitch = envelope.Pitch;
            inputs.roll = envelope.Roll;
        }

        [HarmonyPatch(typeof(ControlsFilter), "Filter")]
        internal static class PrepareEnvelope
        {
            private static void Prefix(ControlsFilter __instance, ControlInputs __0, Vector3 __1, Rigidbody __2, bool __4, Aircraft ___aircraft, float ___minSpeed, float ___minAlt)
            {
                if (___aircraft == null)
                    return;
                var tController = ___aircraft.GetComponent<Fs20TFlightController>();
                if (tController != null)
                {
                    tController.PrepareControls(__instance, __0, __1, __2, __4 && __instance.GetFlyByWire().Enabled, ___minSpeed, ___minAlt);
                    return;
                }
                var controller = ___aircraft.GetComponent<Fs20FlightController>();
                var aController = controller == null ? ___aircraft.GetComponent<Fs20AFlightController>() : null;
                if (controller == null && aController == null)
                    return;
                bool active = __4 && __instance.GetFlyByWire().Enabled && ___aircraft.speed >= ___minSpeed;
                if (controller != null)
                    controller.PrepareControls(__instance, __0, __2, active && ___aircraft.radarAlt>=___minAlt);
                else
                    aController.PrepareControls(__instance, __0, __2, active && ___aircraft.radarAlt>=___minAlt);
            }
        }

        [HarmonyPatch(typeof(ControlsFilter.FlyByWire), "Filter")]
        internal static class ContinuousPitchDerivative
        {
            private sealed class History { public History() {} internal bool Initialized; internal float Rate, Time; }
            private static readonly ConditionalWeakTable<ControlsFilter.FlyByWire, History> Histories =
                new ConditionalWeakTable<ControlsFilter.FlyByWire, History>();

            private static float PreviousError(float nativePrevious, ControlsFilter.FlyByWire filter, Aircraft aircraft)
            {
                if (aircraft == null || (aircraft.GetComponent<Fs20AFlightController>() == null &&
                    aircraft.GetComponent<Fs20FlightController>() == null && aircraft.GetComponent<Fs20TFlightController>() == null))
                    return nativePrevious;
                var history = Histories.GetOrCreateValue(filter);
                float current = filter.GetPitchAngVel();
                float previous = history.Initialized && Time.fixedTime - history.Time <= 2f * Time.fixedDeltaTime ? history.Rate : current;
                history.Rate = current; history.Time = Time.fixedTime; history.Initialized = true;
                // Differentiate the measured rate, not a pilot/setpoint step.
                // Keep the native P/I error bounds, but do not let those bounds
                // erase damping once a rate excursion exceeds .25 rad/s. The
                // native (currentError - previousError) then equals delta(rate).
                float error = Mathf.Clamp(current - filter.GetTargetPitchAngVel(), -.25f, .25f);
                return error - (current - previous);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var field = AccessTools.Field(typeof(ControlsFilter.FlyByWire), "pPrev");
                var correction = AccessTools.Method(typeof(ContinuousPitchDerivative), nameof(PreviousError));
                int replacements = 0;
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                    if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, field))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, correction);
                        replacements++;
                    }
                }
                if (replacements != 1) throw new InvalidOperationException("Unexpected native pitch derivative layout; FS20 FCS patch was not applied.");
            }

            private static void Postfix(Aircraft __0, ControlInputs __1)
            {
                if (__0 != null) __0.GetComponent<Fs20TFlightController>()?.ObserveNativeRoll(__1.roll);
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
                var deltaElevon = __instance.GetComponent<Fs20TElevon>();
                if (deltaElevon != null)
                {
                    ref ControlSurfaceFields delta = ref ___JobFields.Ref();
                    if (!delta.IsDetached)
                    {
                        float deflection = Fs20TFlightPolicy.ElevonAngle(delta.controlInputs.pitch, delta.controlInputs.roll, deltaElevon.Side, deltaElevon.Outer);
                        delta.pitchRange = Fs20TFlightPolicy.ElevonTravel;
                        delta.rollRange = 0f;
                        delta.yawRange = 0f;
                        delta.currentRoll = 0f;
                        delta.currentYaw = 0f;
                        delta.controlInputs.pitch = deflection / Fs20TFlightPolicy.ElevonTravel;
                    }
                    return;
                }
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
                float gain=Fs20FlightEnvelope.SurfaceGain(controller.EquivalentAirspeed);
                float angle=Fs20FlightEnvelope.ElevonAngle(fields.controlInputs.pitch,fields.controlInputs.roll,
                    marker.PitchRange,marker.RollRange,gain,controller.FlapDegrees*marker.FlapFraction);
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
