using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20TFlightDiagnostics : MonoBehaviour
    {
        private struct Sample
        {
            internal Vector3 Force, Torque, Center;
            internal Vector3 Inertia, Normal;
            internal Quaternion InertiaRotation, BodyRotation;
            internal float Incidence;
            internal float CollisionHalfY, WingEffectiveness;
            internal Vector3 SampledVelocity;
            internal int ForceState;
            internal float Area, Mass;
            internal int Airfoil, Body;
        }
        private readonly Dictionary<AeroPart, Sample> samples = new Dictionary<AeroPart, Sample>();
        private Aircraft aircraft;
        private Fs20TFlightController controller;
        private StreamWriter summary, detail;
        private float nextCapture, captureTick = -1f, writtenTick = -1f, started = -1f;
        private bool stopped;
        private Vector3 baselineRaw;
        private Rigidbody baselineBody;

        private bool Active()
        {
            if (stopped) return false;
            if (aircraft == null) { aircraft = GetComponent<Aircraft>(); controller = GetComponent<Fs20TFlightController>(); }
            bool enabled = controller != null ? VortexConventionalPlugin.RecordTDiagnostics :
                aircraft != null && aircraft.definition != null && aircraft.definition.jsonKey == "SmallFighter1" && VortexConventionalPlugin.RecordBDiagnostics;
            return enabled && aircraft != null && aircraft.LocalSim &&
                GameManager.GetLocalAircraft(out var player) && player == aircraft;
        }

        internal void Record(AeroPart part, AeroPartFields fields)
        {
            if (!Active() || Time.fixedTime < nextCapture || part.rb == null) return;
            if (started < 0f) started = Time.fixedTime;
            if (Time.fixedTime - started > 120f) { Stop(); return; }
            if (captureTick != Time.fixedTime) { samples.Clear(); captureTick = Time.fixedTime; }
            var lift = part.GetLiftTransform() ?? part.transform;
            Vector3 forward = transform.InverseTransformDirection(lift.forward);
            samples[part] = new Sample
            {
                Force = fields.hasForce == JobForceType.NoForce ? Vector3.zero : fields.force,
                Torque = fields.hasForce == JobForceType.ForceAndTorque ? fields.torque : Vector3.zero,
                Center = part.rb.worldCenterOfMass, Mass = part.mass, Area = fields.wingArea,
                Airfoil = fields.airfoilID, Body = part.rb.GetInstanceID(),
                Inertia = part.rb.inertiaTensor, InertiaRotation = part.rb.inertiaTensorRotation,
                BodyRotation = Quaternion.Inverse(transform.rotation) * part.rb.rotation,
                Normal = transform.InverseTransformDirection(lift.up),
                Incidence = Mathf.Atan2(-forward.y, forward.z) * Mathf.Rad2Deg,
                CollisionHalfY = fields.collisionSize.y, WingEffectiveness = fields.wingEffectiveness,
                SampledVelocity = transform.InverseTransformDirection(fields.velocity), ForceState = (int)fields.hasForce
            };
        }

        private void LateUpdate()
        {
            if (!Active() || samples.Count == 0 || captureTick == writtenTick) return;
            try
            {
                if (summary == null)
                {
                    string folder = Path.GetFullPath(Path.Combine(Paths.ConfigPath, "../FS20T-flight"));
                    Directory.CreateDirectory(folder);
                    string stamp = (controller == null ? "FS20B-" : "FS20T-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + GetInstanceID();
                    summary = new StreamWriter(Path.Combine(folder, stamp + "-flight.csv"));
                    detail = new StreamWriter(Path.Combine(folder, stamp + "-parts.csv"));
                    summary.WriteLine("fixedTime,speedMps,alphaDeg,verticalSpeedMps,massKg,nozzleDeg,gear,simplePhysics,rawPitch,rawYaw,rawRoll,filteredPitch,filteredYaw,filteredRoll,pitchRateDegS,yawRateDegS,rollRateDegS,rootRollRateDegS,specificNormalG,aeroFx,aeroFy,aeroFz,aeroMx,aeroMy,aeroMz,density,fuelKg,windX,windY,windZ,loadProtectionActive,appliedGLimit");
                    detail.WriteLine("fixedTime,part,bodyId,massKg,area,airfoilId,health,detached,comX,comY,comZ,forceX,forceY,forceZ,momentX,momentY,momentZ,inertiaX,inertiaY,inertiaZ,inertiaQx,inertiaQy,inertiaQz,inertiaQw,bodyQx,bodyQy,bodyQz,bodyQw,normalX,normalY,normalZ,liftIncidenceDeg,collisionHalfY,wingEffectiveness,sampledVx,sampledVy,sampledVz,forceState");
                    VortexConventionalPlugin.ReportAssetState("FS-20 flight diagnostics: " + folder + "/" + stamp + "-flight.csv (10 Hz, 120 s maximum).");
                }
                Vector3 center = Vector3.zero;
                float mass = 0f;
                foreach (var entry in samples) { center += entry.Value.Center * entry.Value.Mass; mass += entry.Value.Mass; }
                if (!(mass > 0f)) return;
                center /= mass;
                Vector3 force = Vector3.zero, moment = Vector3.zero;
                float fuel = 0f;
                foreach (var entry in samples)
                {
                    var part = entry.Key;
                    var sample = entry.Value;
                    var tank = part.GetComponent<FuelTank>();
                    if (tank != null) fuel += tank.fuelMass;
                    force += sample.Force;
                    moment += sample.Torque + Vector3.Cross(sample.Center - center, sample.Force);
                    Vector3 localCenter = transform.InverseTransformPoint(sample.Center);
                    Vector3 localForce = transform.InverseTransformDirection(sample.Force);
                    Vector3 localMoment = transform.InverseTransformDirection(sample.Torque);
                    detail.WriteLine(FormattableString.Invariant($"{captureTick:F3},\"{part.name.Replace("\"", "\"\"")}\",{sample.Body},{sample.Mass:F2},{sample.Area:F4},{sample.Airfoil},{part.hitPoints:F2},{part.IsDetached()},{localCenter.x:F4},{localCenter.y:F4},{localCenter.z:F4},{localForce.x:F2},{localForce.y:F2},{localForce.z:F2},{localMoment.x:F2},{localMoment.y:F2},{localMoment.z:F2},") +
                        Columns(sample.Inertia) + "," + Columns(sample.InertiaRotation) + "," + Columns(sample.BodyRotation) + "," +
                        Columns(sample.Normal) + FormattableString.Invariant($",{sample.Incidence:F3},{sample.CollisionHalfY:F6},{sample.WingEffectiveness:F4},") +
                        Columns(sample.SampledVelocity) + FormattableString.Invariant($",{sample.ForceState}"));
                }
                var body = controller != null ? controller.ControlledBody ?? aircraft.CockpitRB() : baselineBody ?? aircraft.CockpitRB();
                Vector3 wind = aircraft.GetWindVelocity();
                Vector3 velocity = body.velocity - wind;
                Vector3 local = transform.InverseTransformDirection(velocity);
                Vector3 rates = transform.InverseTransformDirection(body.angularVelocity) * Mathf.Rad2Deg;
                Vector3 rootRates = transform.InverseTransformDirection(aircraft.rb.angularVelocity) * Mathf.Rad2Deg;
                Vector3 raw = controller != null ? controller.RawInputs : baselineRaw;
                var input = aircraft.GetInputs();
                force = transform.InverseTransformDirection(force);
                moment = transform.InverseTransformDirection(moment);
                float alpha = -Mathf.Atan2(local.y, Mathf.Max(1f, local.z)) * Mathf.Rad2Deg;
                float normalG = Vector3.Dot(aircraft.accel + Vector3.up, transform.up);
                float nozzle = controller != null ? controller.NozzleAngle : aircraft.GetComponent<SwivelDuctSystem>().GetNozzleAngle();
                summary.WriteLine(FormattableString.Invariant($"{captureTick:F3},{velocity.magnitude:F3},{alpha:F3},{body.velocity.y:F3},{aircraft.GetMass():F2},{nozzle:F2},{aircraft.gearDeployed},{aircraft.simplePhysics},{raw.x:F4},{raw.y:F4},{raw.z:F4},{input.pitch:F4},{input.yaw:F4},{input.roll:F4},{rates.x:F3},{rates.y:F3},{-rates.z:F3},{-rootRates.z:F3},{normalG:F3},{force.x:F2},{force.y:F2},{force.z:F2},{moment.x:F2},{moment.y:F2},{moment.z:F2},{aircraft.airDensity:F5},{fuel:F2},") + Columns(wind) + FormattableString.Invariant($",{controller != null && controller.LoadProtectionActive},{(controller == null ? 0f : controller.AppliedGLimit):F3}"));
                summary.Flush();
                detail.Flush();
                writtenTick = captureTick;
                nextCapture = captureTick + .1f;
            }
            catch (Exception error)
            {
                Debug.LogWarning("FS-20T diagnostics stopped: " + error.Message);
                Stop();
            }
        }

        private void Stop()
        {
            stopped = true;
            var files = new[] { summary, detail };
            summary = detail = null;
            foreach (var file in files)
            {
                try { file?.Dispose(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        private void OnDestroy() => Stop();
        private static string Columns(Vector3 v) => FormattableString.Invariant($"{v.x:F4},{v.y:F4},{v.z:F4}");
        private static string Columns(Quaternion q) => FormattableString.Invariant($"{q.x:F5},{q.y:F5},{q.z:F5},{q.w:F5}");

        [HarmonyPatch(typeof(Aircraft), "FilterInputs")]
        internal static class BaselineInputs
        {
            private static void Prefix(Aircraft __instance)
            {
                if (!VortexConventionalPlugin.RecordBDiagnostics || __instance.definition == null ||
                    __instance.definition.jsonKey != "SmallFighter1" || !__instance.LocalSim ||
                    !GameManager.GetLocalAircraft(out var player) || player != __instance) return;
                var log = __instance.GetComponent<Fs20TFlightDiagnostics>() ?? __instance.gameObject.AddComponent<Fs20TFlightDiagnostics>();
                var input = __instance.GetInputs();
                log.baselineRaw = new Vector3(input.pitch, input.yaw, input.roll);
                log.baselineBody = __instance.CockpitRB();
            }
        }
    }
}
