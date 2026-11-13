using System;
using System.Linq;
using Mirage;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TAirframe
    {
        internal const string Key = "JP_FS20_T";
        internal const int Hash = 0x6A207701;
        internal static AircraftDefinition Register(Encyclopedia encyclopedia, AircraftDefinition donor)
        {
            if (Encyclopedia.Lookup.TryGetValue(Key, out var existing)) return existing as AircraftDefinition ?? throw new InvalidOperationException("FS-20T key is occupied.");
            if (encyclopedia.IndexLookup.OfType<UnitDefinition>().Any(d => d != null && d.unitPrefab != null && d.unitPrefab.TryGetComponent<NetworkIdentity>(out var n) && n.PrefabHash == Hash)) throw new InvalidOperationException("FS-20T prefab hash is occupied.");
            var holder = new GameObject("FS-20T templates");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            AircraftDefinition definition = null;
            AircraftParameters parameters = null;
            try
            {
                var prefab = UnityEngine.Object.Instantiate(donor.unitPrefab, holder.transform, false);
                prefab.name = Key;
                var aircraft = prefab.GetComponent<Aircraft>();
                var network = prefab.GetComponent<NetworkIdentity>();
                var swivel = prefab.GetComponent<SwivelDuctSystem>();
                if (aircraft == null || network == null || swivel == null) throw new InvalidOperationException("Native FS-20T VTOL donor layout changed.");
                ValidateVtol(prefab.transform, swivel);
                var nativeResponse = Fs20TControlCalibration.Measure(donor.unitPrefab.transform, donor.aircraftParameters);
                var result = Fs20TGeometry.Apply(prefab.transform);
                ValidateVtol(prefab.transform, swivel);
                var mass = Fs20TMass.Apply(prefab.transform);
                Fs20TStructure.Apply(prefab.transform);
                float massRatio = mass.After / (mass.Before - result.AddedMass);
                float liftAreaRatio = result.TunedLiftArea / result.NativeLiftArea;
                float speedScale = Fs20TFlightPolicy.SpeedScale(massRatio, liftAreaRatio);
                parameters = UnityEngine.Object.Instantiate(donor.aircraftParameters);
                // The game assigns airfoil chart IDs at runtime; cloning the entries resets them.
                parameters.airfoils = donor.aircraftParameters.airfoils;
                parameters.aircraftName = "FS-20T Vortex";
                parameters.verticalLanding = true;
                // Wing-loading scaling is an initial AI planning baseline.
                parameters.takeoffSpeed *= speedScale;
                parameters.approachSpeed *= speedScale;
                parameters.landingSpeed *= speedScale;
                parameters.cornerSpeed *= speedScale;
                var controller = prefab.AddComponent<Fs20TFlightController>();
                controller.Configure(prefab.transform, speedScale, nativeResponse, parameters);
                prefab.AddComponent<Fs20TFlightDiagnostics>();
                definition = UnityEngine.Object.Instantiate(donor);
                definition.name = definition.jsonKey = Key;
                definition.unitName = "FS-20T Vortex";
                definition.code = "FS-20T";
                definition.width = result.Span;
                definition.aircraftParameters = parameters;
                definition.description = "Integrated-delta Vortex with a lightweight composite fuselage and wing, articulated slats and independently damageable wing sections. Load-aware FBW with an 11 G positive ceiling. Retains the FS-20B engine and VTOL system.";
                definition.aircraftInfo = new AircraftInfo { emptyWeight = mass.After, maxSpeed = donor.aircraftInfo.maxSpeed, stallSpeed = donor.aircraftInfo.stallSpeed * speedScale, maneuverability = donor.aircraftInfo.maneuverability, maxWeight = donor.aircraftInfo.maxWeight };
                definition.mass = mass.After;
                definition.unitPrefab = prefab;
                ((Unit)aircraft).definition = definition;
                network.PrefabHash = Hash;
                prefab.SetActive(true);
                encyclopedia.aircraft.Add(definition);
                Encyclopedia.Lookup.Add(Key, definition);
                ((INetworkDefinition)definition).LookupIndex = encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(definition);
                VortexConventionalPlugin.ReportAssetState($"FS-20T template: {result.ElevonHinges}/4 elevon hinges bound, {result.DamageRegions} independent damage sections, {result.IntactColliders} intact convex cells ({result.Colliders} stored across all states), {result.LodGroups} stored LOD groups, lift area {result.TunedLiftArea:F1} vs {result.NativeLiftArea:F1} m². Composite dry mass {mass.After:F0} kg vs {mass.Before:F0} kg, dry center shift {mass.CenterShift.ToString("F3")} m. Native VTOL retained.");
                return definition;
            }
            catch
            {
                if (definition != null)
                {
                    encyclopedia.aircraft.Remove(definition);
                    if (Encyclopedia.Lookup.TryGetValue(Key, out var registered) && registered == definition) Encyclopedia.Lookup.Remove(Key);
                    encyclopedia.IndexLookup.Remove(definition);
                    UnityEngine.Object.Destroy(definition);
                }
                if (parameters != null) UnityEngine.Object.Destroy(parameters);
                UnityEngine.Object.Destroy(holder);
                throw;
            }
        }
        private static void ValidateVtol(Transform root, SwivelDuctSystem swivel)
        {
            foreach (string path in new[] { "liftFan/liftFan_blades", "liftFan/liftFan_thrustTransform", "engine/nozzle", "wing1a_L/wing2a_L/rollPost_L", "wing1a_R/wing2a_R/rollPost_R" })
                if (root.Find(path) == null) throw new InvalidOperationException("FS-20T VTOL hardware missing: " + path);
            foreach (string name in new[] { "bearings", "liftFans", "thrusters", "thrustDoors" })
                if (!((Array)Fs20Tuning.Field(typeof(SwivelDuctSystem), name).GetValue(swivel)).Cast<object>().Any()) throw new InvalidOperationException("FS-20T VTOL binding missing: " + name);
        }
    }
}
