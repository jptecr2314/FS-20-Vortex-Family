using System;
using System.Linq;
using Mirage;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20AirframeA
    {
        internal const string Key="JP_FS20_A";
        internal const int Hash=0x6A20A701;
        internal const float RemovedMass=350f;

        internal static AircraftDefinition Register(Encyclopedia encyclopedia,AircraftDefinition source)
        {
            if(Encyclopedia.Lookup.TryGetValue(Key,out var existing))
                return existing as AircraftDefinition ?? throw new InvalidOperationException("FS-20A key is occupied.");
            if(encyclopedia.IndexLookup.OfType<UnitDefinition>().Any(d=>d!=null && d.unitPrefab!=null &&
                d.unitPrefab.TryGetComponent<NetworkIdentity>(out var n) && n.PrefabHash==Hash))
                throw new InvalidOperationException("FS-20A prefab hash is occupied.");
            var holder=new GameObject("FS-20A templates");holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            AircraftDefinition definition=null;AircraftParameters parameters=null;
            try
            {
                var prefab=UnityEngine.Object.Instantiate(source.unitPrefab,holder.transform,false);
                prefab.name=Key;
                var aircraft=prefab.GetComponent<Aircraft>();
                var network=prefab.GetComponent<NetworkIdentity>();
                var swivel=prefab.GetComponent<SwivelDuctSystem>();
                if(aircraft==null || network==null || swivel==null)
                    throw new InvalidOperationException("Native FS-20A donor layout changed.");
                // Original wing/fin/exterior geometry and engine thrust remain.
                VortexConventionalPlugin.RemoveVtolHardware(prefab.transform,swivel);
                foreach(string path in new[]{"engine","engine/nozzle"})
                {
                    var part=prefab.transform.Find(path).GetComponent<AeroPart>();
                    float credit=Fs20AUpgradeSpec.EngineInstallationSaving*.5f;
                    if(part.mass<=credit)throw new InvalidOperationException("Invalid fixed engine installation mass.");
                    part.mass-=credit;
                }
                Fs20AInternalTank.Install(prefab.transform);
                Fs20AFlightController.Configure(prefab.transform);
                var filter=prefab.GetComponentInChildren<ControlsFilter>(true);
                var hover=Fs20Tuning.Field(typeof(ControlsFilter),"autoHover").GetValue(filter);
                Fs20Tuning.Set(hover,"Enabled",false);
                prefab.AddComponent<Fs20ConventionalAirframe>();
                parameters=UnityEngine.Object.Instantiate(source.aircraftParameters);
                parameters.aircraftName="FS-20A Vortex";
                parameters.verticalLanding=false;
                // Initial AI runway-planning values, not measured takeoff/landing performance.
                parameters.takeoffSpeed=80f;
                parameters.takeoffDistance=800f;
                parameters.approachSpeed=90f;
                parameters.landingSpeed=80f;
                parameters.shortLandingSpeed=80f;
                parameters.cornerSpeed=155f;
                definition=UnityEngine.Object.Instantiate(source);
                definition.name=definition.jsonKey=Key;
                definition.unitName="FS-20A Vortex";
                definition.code="FS-20A";
                definition.description="Runway-operated Vortex with original wings, a lighter fixed engine installation, extra internal fuel and energy-managed 11 G flight assist.";
                definition.aircraftParameters=parameters;
                var info=source.aircraftInfo;
                definition.aircraftInfo=new AircraftInfo {
                    emptyWeight=info.emptyWeight-RemovedMass-Fs20AUpgradeSpec.EngineInstallationSaving+Fs20AUpgradeSpec.TankDryMass,maxSpeed=info.maxSpeed,
                    stallSpeed=info.stallSpeed,maneuverability=Fs20AEnergyEnvelope.MaximumG,maxWeight=info.maxWeight
                };
                definition.unitPrefab=prefab;
                ((Unit)aircraft).definition=definition;
                network.PrefabHash=Hash;
                prefab.SetActive(true);
                definition.mass=prefab.GetComponentsInChildren<UnitPart>(true).Sum(p=>p.GetMass());
                encyclopedia.aircraft.Add(definition);
                Encyclopedia.Lookup.Add(Key,definition);
                ((INetworkDefinition)definition).LookupIndex=encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(definition);
                return definition;
            }
            catch
            {
                if(definition!=null)
                {
                    encyclopedia.aircraft.Remove(definition);
                    if(Encyclopedia.Lookup.TryGetValue(Key,out var registered) && registered==definition)
                        Encyclopedia.Lookup.Remove(Key);
                    encyclopedia.IndexLookup.Remove(definition);
                    UnityEngine.Object.Destroy(definition);
                }
                if(parameters!=null)UnityEngine.Object.Destroy(parameters);
                UnityEngine.Object.Destroy(holder);
                throw;
            }
        }
    }

    public sealed class Fs20ConventionalAirframe : MonoBehaviour { }
}
