using System;
using System.Linq;
using Mirage;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Independent factory: do not mutate the T/C templates or widen the delta twice.
    internal static class Fs20NAAirframe
    {
        internal const string Key = "JP_FS20_NA";
        internal const int Hash = 0x6A20DA01;
        internal const string HookKey = "JP_FS20NA_INTERNAL_HOOK";
        internal const float CleanRadarRatio = .0001f;

        private sealed class CannonDoorState
        {
            internal readonly Mesh Mesh;internal readonly Transform Parent;
            internal readonly Vector3 Position,Scale;internal readonly Quaternion Rotation;
            internal readonly bool Enabled;
            internal CannonDoorState(MeshFilter filter){Mesh=filter.sharedMesh;Parent=filter.transform.parent;Position=filter.transform.localPosition;Rotation=filter.transform.localRotation;Scale=filter.transform.localScale;Enabled=filter.GetComponent<Renderer>().enabled;}
        }

        internal static AircraftDefinition Register(Encyclopedia encyclopedia, AircraftDefinition donor)
        {
            if (Encyclopedia.Lookup.TryGetValue(Key, out var existing))
                return existing as AircraftDefinition ?? throw new InvalidOperationException("FS-20NA key is occupied.");
            if (encyclopedia.IndexLookup.OfType<UnitDefinition>().Any(d => d != null && d.unitPrefab != null &&
                d.unitPrefab.TryGetComponent<NetworkIdentity>(out var n) && n.PrefabHash == Hash))
                throw new InvalidOperationException("FS-20NA prefab hash is occupied.");
            var holder = new GameObject("FS-20NA templates"); holder.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(holder);
            AircraftDefinition definition = null; AircraftParameters parameters = null;
            try
            {
                var prefab = UnityEngine.Object.Instantiate(donor.unitPrefab, holder.transform, false);
                prefab.name = Key;
                var aircraft = prefab.GetComponent<Aircraft>();
                prefab.AddComponent<Fs20TakeoffPlanner>().Variant="NA";
                var baseline = Fs20TControlCalibration.Measure(donor.unitPrefab.transform, donor.aircraftParameters);
                var geometry = ConfigureGeometry(prefab.transform, encyclopedia);
                parameters = UnityEngine.Object.Instantiate(donor.aircraftParameters);
                parameters.airfoils = donor.aircraftParameters.airfoils;
                parameters.aircraftName = "FS-20NA Night Attack";
                Fs20NALiveries.Configure(parameters);
                parameters.verticalLanding = false;
                // Existing recovery/base-selection policy, not a takeoff test.
                parameters.takeoffDistance = 800f;
                float dry = prefab.GetComponentsInChildren<UnitPart>(true).Sum(p => p.GetMass());
                float nativeDry = donor.unitPrefab.GetComponentsInChildren<UnitPart>(true).Sum(p => p.GetMass());
                float speedScale = Fs20TFlightPolicy.SpeedScale(dry / nativeDry, geometry.AreaRatio);
                Fs20AiCalibration.Apply("NA", parameters);
                prefab.AddComponent<Fs20TFlightController>().Configure(prefab.transform, speedScale, baseline, parameters);
                definition = UnityEngine.Object.Instantiate(donor);
                definition.name = definition.jsonKey = Key;
                definition.unitName = "FS-20NA Night Attack"; definition.code = "FS-20NA";
                Fs20VariantPricing.Apply(definition);
                definition.unitPrefab = prefab; definition.aircraftParameters = parameters;
                definition.width = geometry.Span;
                definition.radarSize = Mathf.Max(1e-7f, donor.radarSize * CleanRadarRatio);
                definition.visibleRange = 1250f;
                definition.description = "Tailless conventional Night Attack Vortex. T delta wing, C internal weapon bays and retractable arresting hook. Passive optical apertures, differential split drag rudders and yaw thrust vectoring. Reduced clean radar signature; external stores and exposed equipment retain their radar contribution. Development flight model.";
                definition.aircraftInfo = new AircraftInfo { emptyWeight = dry, maxSpeed = donor.aircraftInfo.maxSpeed,
                    stallSpeed = donor.aircraftInfo.stallSpeed * speedScale, maneuverability = donor.aircraftInfo.maneuverability,
                    maxWeight = donor.aircraftInfo.maxWeight };
                ((Unit)aircraft).definition = definition;
                prefab.GetComponent<NetworkIdentity>().PrefabHash = Hash;
                Fs20TailHook.AddVariant(encyclopedia, definition, HookKey);
                Fs20ExpandedBays.AddCarrierWeapons(encyclopedia, prefab.transform);
                prefab.AddComponent<Fs20NARecon>().Configure(prefab.transform);
                prefab.AddComponent<Fs20NASignature>().Configure(prefab.transform);
                prefab.AddComponent<Fs20NAYawController>().Configure(prefab.transform);
                definition.mass = prefab.GetComponentsInChildren<UnitPart>(true).Sum(p => p.GetMass());
                definition.aircraftInfo.emptyWeight = definition.mass;
                Fs20PaintLods.Prepare(prefab);
                prefab.SetActive(true);
                encyclopedia.aircraft.Add(definition); Encyclopedia.Lookup.Add(Key, definition);
                ((INetworkDefinition)definition).LookupIndex = encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(definition);
                return definition;
            }
            catch
            {
                if (definition != null) {
                    encyclopedia.aircraft.Remove(definition); encyclopedia.IndexLookup.Remove(definition);
                    if (Encyclopedia.Lookup.TryGetValue(Key, out var registered) && registered == definition) Encyclopedia.Lookup.Remove(Key);
                    UnityEngine.Object.Destroy(definition);
                }
                if (parameters != null) UnityEngine.Object.Destroy(parameters);
                UnityEngine.Object.Destroy(holder); throw;
            }
        }

        internal static Fs20TGeometry.Result ConfigureGeometry(Transform root, Encyclopedia encyclopedia)
        {
            // Strip native VTOL islands before the delta replaces the wing skin.
            VortexConventionalPlugin.RemoveVtolHardware(root, root.GetComponent<SwivelDuctSystem>());
            var result = Fs20TGeometry.Apply(root);
            Fs20TMass.Apply(root); Fs20TStructure.Apply(root);
            Fs20NAGeometry.Install(root);
            // Expanded-bay surgery walks liftFan descendants. Cannon doors are
            // independent moving panels, not retired weapon-bay hatches.
            var cannonDoors=root.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter=>filter.name=="gunDoor1"||filter.name=="gunDoor2")
                .ToDictionary(filter=>filter,filter=>new CannonDoorState(filter));
            if(cannonDoors.Count!=2)throw new InvalidOperationException("FS-20NA native cannon door mesh references missing: "+string.Join(",",root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.IndexOf("gun",StringComparison.OrdinalIgnoreCase)>=0).Select(t=>t.name)));
            Fs20ExpandedBays.Install(encyclopedia, root, true);
            foreach(var door in cannonDoors){
                door.Key.sharedMesh=door.Value.Mesh;
                door.Key.transform.SetParent(door.Value.Parent,false);
                door.Key.transform.localPosition=door.Value.Position;door.Key.transform.localRotation=door.Value.Rotation;door.Key.transform.localScale=door.Value.Scale;
                door.Key.GetComponent<Renderer>().enabled=door.Value.Enabled;
            }
            root.gameObject.AddComponent<Fs20NANightPaint>().Refresh();
            // Bay and sensor installation changes dry mass markers after the T
            // stores its force offsets. Preserve the force point and refresh COM.
            foreach (var marker in root.GetComponentsInChildren<Fs20TPartPhysics>(true)) {
                var part = marker.Part;
                var center = (Transform)Fs20Tuning.Field(typeof(UnitPart),"centerOfMass").GetValue(part);
                Fs20TPartPhysics.Configure(part,center == null ? part.transform.position : center.position,marker.WorldForcePoint);
                Fs20Tuning.Set(part,"baseMass",part.mass);
            }
            root.gameObject.AddComponent<Fs20ConventionalAirframe>();
            root.gameObject.AddComponent<Fs20NAMarker>();
            var filter = root.GetComponentInChildren<ControlsFilter>(true);
            Fs20Tuning.Set(Fs20Tuning.Field(typeof(ControlsFilter), "autoHover").GetValue(filter), "Enabled", false);
            var engine = root.GetComponentInChildren<Turbojet>(true);
            Fs20Tuning.Set(engine, "maxThrust", 110000f);
            var nozzle = root.GetComponentInChildren<JetNozzle>(true);
            var burners = (Array)Fs20Tuning.Field(typeof(JetNozzle), "afterburners").GetValue(nozzle);
            for (int i = 0; i < burners.Length; i++) { var burner = burners.GetValue(i); Fs20Tuning.Set(burner, "thrust", 55000f); burners.SetValue(burner,i); }
            Fs20Tuning.Set(engine, "thrustVectoring", new Vector3(20f, 15f, 0f));
            Fs20Tuning.Set(engine, "thrustVectoringMaxAirspeed", 550f);
            return result;
        }
    }
    public sealed class Fs20NAMarker : MonoBehaviour { }
}

