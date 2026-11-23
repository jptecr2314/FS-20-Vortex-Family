using System.Reflection;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20TFlightController : MonoBehaviour
    {
        [SerializeField] private float basePitch, baseRoll, baseAlpha, baseG;
        [SerializeField] private float basePitchGain, basePitchDerivative, baseRollGain;
        [SerializeField] private float pitchScale, rollScale, hoverPitchScale, hoverRollScale;
        private Aircraft aircraft;
        private SwivelDuctSystem swivel;
        private readonly Fs20LandingEnvelope landing = new Fs20LandingEnvelope();
        private readonly Fs20TLoadEnvelope loads = new Fs20TLoadEnvelope();
        internal Vector3 RawInputs { get; private set; }
        internal Rigidbody ControlledBody { get; private set; }
        internal float AppliedGLimit { get; private set; } = 9f;
        internal bool LoadProtectionActive { get; private set; }
        internal float NozzleAngle => swivel == null ? 0f : swivel.GetNozzleAngle();
        private static readonly FieldInfo G = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "gLimitPositive");
        private static readonly FieldInfo PitchRate = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "maxPitchAngularVel");
        private static readonly FieldInfo RollRate = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "maxRollAngularVel");
        private static readonly FieldInfo Alpha = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "alphaLimiter");
        private static readonly FieldInfo PitchGain = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "pFactorFast");
        private static readonly FieldInfo PitchDerivative = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "dFactorFast");
        private static readonly FieldInfo RollGain = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "rollTightness");
        private static readonly FieldInfo PitchAdjuster = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "pitchAdjuster");
        private static readonly FieldInfo PitchIntegral = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "i");
        private static readonly FieldInfo CornerSpeed = Fs20Tuning.Field(typeof(ControlsFilter.FlyByWire), "cornerSpeed");
        private float appliedPitchGain = float.NaN, appliedDerivative = float.NaN, appliedRollGain = float.NaN;
        private float lastPitchScale = 1f, lastDerivativeScale = 1f, lastRollScale = 1f;
        private float nativeRollOutput;
        internal void ObserveNativeRoll(float value) => nativeRollOutput = value;

        internal void Configure(Transform root, float speedScale, Fs20TControlCalibration.Measurement baseline, AircraftParameters parameters)
        {
            var fbw = root.GetComponentInChildren<ControlsFilter>(true).GetFlyByWire();
            basePitch = (float)PitchRate.GetValue(fbw);
            baseRoll = (float)RollRate.GetValue(fbw);
            baseAlpha = Fs20FlightEnvelope.MaximumAlpha;
            baseG = Fs20TStructure.MaximumG;
            G.SetValue(fbw, baseG);
            Alpha.SetValue(fbw, baseAlpha);
            basePitchGain = (float)PitchGain.GetValue(fbw);
            basePitchDerivative = (float)PitchDerivative.GetValue(fbw);
            baseRollGain = (float)RollGain.GetValue(fbw);
            var current = Fs20TControlCalibration.Measure(root, parameters);
            float pitchInertia = current.PitchInertia / baseline.PitchInertia;
            float rollInertia = current.RollInertia / baseline.RollInertia;
            pitchScale = Fs20TFlightPolicy.GainScale(pitchInertia, current.PitchAuthority / baseline.PitchAuthority);
            rollScale = Fs20TFlightPolicy.GainScale(rollInertia, current.RollAuthority / baseline.RollAuthority);
            hoverPitchScale = Fs20TFlightPolicy.GainScale(pitchInertia, 1f);
            hoverRollScale = Fs20TFlightPolicy.GainScale(rollInertia, 1f);
            float takeoff = (float)Fs20Tuning.Field(fbw.GetType(), "takeoffSpeed").GetValue(fbw);
            Fs20Tuning.Set(fbw, "takeoffSpeed", takeoff * Mathf.Clamp(speedScale, .65f, 1f));
            Fs20Tuning.Set(fbw, "rollTrimLimit", .15f);
            Fs20Tuning.Set(fbw, "rollTrimRate", .025f);
            VortexConventionalPlugin.ReportAssetState($"FS-20T initial FBW calibration: pitch gain={pitchScale:F3}, roll gain={rollScale:F3}, " +
                $"pitch/roll inertia ratios={pitchInertia:F3}/{rollInertia:F3}. Derived from the dry mass and actuator moment model; flight validation pending.");
        }

        private void Awake()
        {
            aircraft = GetComponent<Aircraft>();
            swivel = GetComponent<SwivelDuctSystem>();
        }

        internal void PrepareControls(ControlsFilter filter, ControlInputs inputs, Vector3 raw, Rigidbody body,
            bool assisted, float minimumFbwSpeed, float minimumFbwAltitude)
        {
            if (aircraft == null || (swivel == null && GetComponent<Fs20NAMarker>() == null)) return;
            RawInputs = raw;
            ControlledBody = body;
            float angle = NozzleAngle;
            bool forward = assisted && aircraft.speed >= minimumFbwSpeed && aircraft.radarAlt >= minimumFbwAltitude
                && !aircraft.IsAutoHoverEnabled();
            bool airborne = (bool)Fs20Tuning.Airborne.GetValue(aircraft);
            float wing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 65f, angle));
            var fbw = filter.GetFlyByWire();
            Vector3 bodyUp = body.rotation*Vector3.up;
            float normalG = Vector3.Dot(aircraft.accel - Physics.gravity / 9.81f, bodyUp);
            Vector3 flow = Quaternion.Inverse(body.rotation)*(body.velocity - aircraft.GetWindVelocity());
            float alpha = -Mathf.Atan2(flow.y, Mathf.Max(1f, flow.z)) * Mathf.Rad2Deg;
            float ceiling = Mathf.Min(baseG, Fs20TStructure.MaximumG * Mathf.Min(1f,
                Fs20TStructure.DesignGrossMass / Mathf.Max(Fs20TStructure.DesignGrossMass, aircraft.GetMass())));
            G.SetValue(fbw, ceiling);
            float ratePerG = Fs20Tuning.NativeRatePerG(filter, aircraft, ceiling);
            bool protectedFlight = forward && airborne && flow.z > 30f;
            loads.Step(inputs.pitch, flow.magnitude, aircraft.airDensity, aircraft.GetMass(), normalG, alpha,
                protectedFlight, true, Time.fixedDeltaTime, ratePerG,
                -Vector3.Dot(body.angularVelocity,body.rotation*Vector3.right),
                Vector3.Dot(-Physics.gravity/9.81f,bodyUp),0f,
                Vector3.Dot(aircraft.accel * 9.81f, body.rotation * flow.normalized), baseG, baseAlpha);
            AppliedGLimit = loads.LimitG;
            LoadProtectionActive = protectedFlight && wing > .01f;
            G.SetValue(fbw, AppliedGLimit);
            if (protectedFlight) Fs20Tuning.TrackProtection(filter, aircraft, normalG, alpha, AppliedGLimit, baseAlpha, wing, loads.LoadRate);
            if (forward && airborne)
            {
                inputs.pitch = Mathf.Lerp(inputs.pitch, loads.Pitch, wing);
            }
            // The native roll loop requests half maxRollAngularVel in this flight envelope.
            float rollTarget = Mathf.Lerp(2f * 30f * Mathf.Deg2Rad, baseRoll, wing);
            RollRate.SetValue(fbw, rollTarget);
            Alpha.SetValue(fbw, baseAlpha);
            float pitchGain = Mathf.Lerp(hoverPitchScale, pitchScale, wing);
            // Delta step-response calibration: retain hover damping and give the
            // wingborne rate loop enough damping for the enlarged lifting surface.
            float derivativeGain = Mathf.Lerp(hoverPitchScale, 1.5f * Mathf.Sqrt(pitchScale), wing);
            if (GetComponent<Fs20NAMarker>() != null)
            {
                // A tailless wing loses symmetric stroke while the same elevons
                // supply differential roll. Retain damping as that pitch reserve
                // falls; this changes no target rate or pilot roll command.
                float roll = Mathf.Clamp01(Mathf.Abs(nativeRollOutput));
                float outerReserve = Mathf.Clamp01((40f - 30f * roll) / 26f);
                float innerReserve = Mathf.Clamp01((40f - 18f * roll) / 36f);
                derivativeGain *= 1f / Mathf.Sqrt(Mathf.Max(.25f, .5f * (outerReserve + innerReserve)));
            }
            float rollGain = Mathf.Lerp(hoverRollScale, rollScale, wing);
            // ApplyParameters can be inlined by Mono. Track actual external gain
            // changes instead of relying on patching that tiny native wrapper.
            float observedP = (float)PitchGain.GetValue(fbw), observedD = (float)PitchDerivative.GetValue(fbw), observedR = (float)RollGain.GetValue(fbw);
            if (!float.IsNaN(appliedPitchGain) && !Mathf.Approximately(observedP, appliedPitchGain)) basePitchGain = observedP / Mathf.Max(.01f, lastPitchScale);
            if (!float.IsNaN(appliedDerivative) && !Mathf.Approximately(observedD, appliedDerivative)) basePitchDerivative = observedD / Mathf.Max(.01f, lastDerivativeScale);
            if (!float.IsNaN(appliedRollGain) && !Mathf.Approximately(observedR, appliedRollGain)) baseRollGain = observedR / Mathf.Max(.01f, lastRollScale);
            PitchGain.SetValue(fbw, basePitchGain * pitchGain);
            PitchDerivative.SetValue(fbw, basePitchDerivative * derivativeGain);
            RollGain.SetValue(fbw, baseRollGain * rollGain);
            appliedPitchGain = basePitchGain * pitchGain; appliedDerivative = basePitchDerivative * derivativeGain; appliedRollGain = baseRollGain * rollGain;
            lastPitchScale = pitchGain; lastDerivativeScale = derivativeGain; lastRollScale = rollGain;
        }
    }

    public sealed class Fs20TElevon : MonoBehaviour
    {
        [SerializeField] internal float Side;
        [SerializeField] internal bool Outer;
    }
}
