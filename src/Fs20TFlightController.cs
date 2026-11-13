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

        internal void Configure(Transform root, float speedScale, Fs20TControlCalibration.Measurement baseline, AircraftParameters parameters)
        {
            var fbw = root.GetComponentInChildren<ControlsFilter>(true).GetFlyByWire();
            basePitch = (float)PitchRate.GetValue(fbw);
            baseRoll = Mathf.Min((float)RollRate.GetValue(fbw), 2f * 120f * Mathf.Deg2Rad);
            baseAlpha = (float)Alpha.GetValue(fbw);
            baseG = Fs20TStructure.MaximumG;
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
            if (aircraft == null || swivel == null) return;
            RawInputs = raw;
            ControlledBody = body;
            float angle = swivel.GetNozzleAngle();
            bool forward = assisted && aircraft.speed >= minimumFbwSpeed && aircraft.radarAlt >= minimumFbwAltitude
                && !aircraft.IsAutoHoverEnabled();
            bool airborne = (bool)Fs20Tuning.Airborne.GetValue(aircraft);
            bool approach = Fs20TFlightPolicy.Approach(forward, aircraft.gearDeployed, aircraft.speed, minimumFbwSpeed, angle);
            landing.Step(inputs.pitch, inputs.roll, aircraft.speed, approach, airborne, forward, Time.fixedDeltaTime);
            float blend = forward ? landing.Blend * (1f - Mathf.InverseLerp(9f, 25f, angle)) : 0f;
            float wing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 65f, angle));
            var fbw = filter.GetFlyByWire();
            Vector3 bodyUp = body.rotation*Vector3.up;
            float normalG = Vector3.Dot(aircraft.accel - Physics.gravity / 9.81f, bodyUp);
            Vector3 flow = Quaternion.Inverse(body.rotation)*(body.velocity - aircraft.GetWindVelocity());
            float alpha = -Mathf.Atan2(flow.y, Mathf.Max(1f, flow.z)) * Mathf.Rad2Deg;
            float corner = Mathf.Max(1f,(float)CornerSpeed.GetValue(fbw));
            float pressure = aircraft.speed*aircraft.speed*aircraft.airDensity/(corner*corner*1.225f);
            float ratePerG = 9.81f/Mathf.Max(aircraft.speed,corner*.75f)*(pressure<1f?Mathf.Clamp(pressure,.3f,1f):1f);
            bool protectedFlight = forward && airborne && wing>.99f;
            loads.Step(inputs.pitch, flow.magnitude, aircraft.airDensity, aircraft.GetMass(), normalG, alpha,
                protectedFlight, !aircraft.gearDeployed, Time.fixedDeltaTime, ratePerG,
                -Vector3.Dot(body.angularVelocity,body.rotation*Vector3.right),
                Vector3.Dot(-Physics.gravity/9.81f,bodyUp),blend);
            AppliedGLimit = Mathf.Lerp(loads.LimitG, Mathf.Min(baseG, 5.5f), blend);
            LoadProtectionActive = protectedFlight;
            G.SetValue(fbw, AppliedGLimit);
            if (forward && airborne)
            {
                inputs.pitch = loads.Pitch;
                // Only a hard exceedance clears accumulated nose-up authority.
                // Near the envelope, native rate feedback retains its steady trim.
                if (loads.HardProtection)
                {
                    float value = (float)PitchAdjuster.GetValue(fbw);
                    if (value < 0f) PitchAdjuster.SetValue(fbw, 0f);
                    float integral = (float)PitchIntegral.GetValue(fbw);
                    if (integral > 0f) PitchIntegral.SetValue(fbw, 0f);
                }
            }
            PitchRate.SetValue(fbw, Mathf.Lerp(basePitch, Mathf.Min(basePitch, .45f), blend));
            // The native roll loop requests half maxRollAngularVel in this flight envelope.
            float rollTarget = Mathf.Lerp(2f * 30f * Mathf.Deg2Rad, baseRoll, wing);
            RollRate.SetValue(fbw, rollTarget * Mathf.Lerp(1f, .65f, blend));
            Alpha.SetValue(fbw, Mathf.Lerp(baseAlpha, Mathf.Min(baseAlpha, 22f), blend));
            float pitchGain = Mathf.Lerp(hoverPitchScale, pitchScale, wing);
            PitchGain.SetValue(fbw, basePitchGain * pitchGain);
            PitchDerivative.SetValue(fbw, basePitchDerivative * pitchGain);
            RollGain.SetValue(fbw, baseRollGain * Mathf.Lerp(hoverRollScale, rollScale, wing));
            if (blend > 0f)
            {
                inputs.pitch *= 1f - blend + blend * (.65f + .35f * Mathf.Abs(inputs.pitch));
                inputs.roll = landing.Roll;
            }
        }
    }

    public sealed class Fs20TElevon : MonoBehaviour
    {
        [SerializeField] internal float Side;
        [SerializeField] internal bool Outer;
    }
}
