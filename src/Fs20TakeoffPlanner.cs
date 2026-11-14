using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // On-demand preflight advisor. Never substitutes a recovery-runway field or
    // prevents a native takeoff based on an unvalidated longitudinal model.
    public sealed class Fs20TakeoffPlanner : MonoBehaviour
    {
        [SerializeField] public string Variant;
        public Fs20TakeoffPlan LastPlan { get; private set; }
        private string lastKey;
        private readonly List<Rigidbody> bodies=new List<Rigidbody>();
        private UnitPart[] watched;
        private bool damageSeen;
        private void Awake(){watched=GetComponentsInChildren<UnitPart>(true);foreach(var part in watched)part.onApplyDamage+=Damaged;}
        private void Damaged(UnitPart.OnApplyDamage _) {damageSeen=true;lastKey=null;}
        private void OnDestroy(){if(watched!=null)foreach(var part in watched)if(part!=null)part.onApplyDamage-=Damaged;}
        public Fs20TakeoffPlan Estimate(Vector3 runwayDirection,Vector3 deckVelocity,float remainingRun,bool afterburner)
        {
            var a=GetComponent<Aircraft>();
            Fs20TakeoffPlan Unknown(string reason)=>LastPlan=new Fs20TakeoffPlan{status="Not evaluated",reason=reason};
            if(a==null||!a.LocalSim||a.rb==null||a.partLookup==null)return Unknown("Local mass properties unavailable.");
            if(runwayDirection.sqrMagnitude<.5f||Mathf.Abs(runwayDirection.normalized.y)>.035f)return Unknown("Runway direction/slope outside the level longitudinal model.");
            var relativeWind=deckVelocity-a.GetWindVelocity();
            var side=Vector3.Cross(Vector3.up,runwayDirection.normalized).normalized;
            if(Mathf.Abs(Vector3.Dot(relativeWind,side))>2)return Unknown("Crosswind above 2 m/s requires lateral validation.");
            if(damageSeen)return Unknown("Damage observed; intact-airframe prediction requires a fresh airframe.");
            bodies.Clear();
            foreach(var part in a.partLookup) {
                if(part==null||Fs20NARecon.Lost(part))return Unknown("Damaged/detached geometry is outside the intact-airframe model.");
                if(part.rb!=null&&!part.rb.isKinematic&&!bodies.Contains(part.rb))bodies.Add(part.rb);
            }
            if(bodies.Count==0)return Unknown("Native rigidbody mass properties not initialized.");
            float mass=bodies.Sum(b=>b.mass);var worldCenter=bodies.Aggregate(Vector3.zero,(sum,b)=>sum+b.worldCenterOfMass*b.mass)/Mathf.Max(1,mass);
            var axis=transform.right;float inertia=0;
            foreach(var body in bodies) {
                var localAxis=Quaternion.Inverse(body.rotation*body.inertiaTensorRotation)*axis;
                var tensor=body.inertiaTensor;inertia+=tensor.x*localAxis.x*localAxis.x+tensor.y*localAxis.y*localAxis.y+tensor.z*localAxis.z*localAxis.z+
                    body.mass*Vector3.Cross(body.worldCenterOfMass-worldCenter,axis).sqrMagnitude;
            }
            float fuel=0,capacity=0;
            foreach(var part in a.partLookup){var tank=part.GetComponent<FuelTank>();if(tank==null)continue;
                fuel+=(float)Fs20Tuning.Field(typeof(FuelTank),"fuelMass").GetValue(tank);capacity+=tank.GetCapacity();}
            if(capacity<=0)return Unknown("Fuel capacity unavailable.");
            var drag=new List<Fs20TakeoffStore>();var seen=new HashSet<int>();
            if(a.weaponManager!=null)foreach(var set in a.weaponManager.hardpointSets)foreach(var hp in set.hardpoints) {
                if(hp==null||!seen.Add(hp.HardpointIndex))continue;
                if(hp.bayDoors!=null&&hp.bayDoors.Any(d=>d!=null&&(float)Fs20Tuning.Field(typeof(BayDoor),"openAmount").GetValue(d)>.001f))return Unknown("Open weapon bay is outside the closed-bay drag model.");
                var mount=(WeaponMount)Fs20Tuning.Field(typeof(Hardpoint),"mount").GetValue(hp);
                var spawned=(GameObject)Fs20Tuning.Field(typeof(Hardpoint),"spawnedPrefab").GetValue(hp);
                if(mount==null||spawned==null)continue;
                // Closed internal bays add mass through native bodies, not external drag.
                if(hp.bayDoors!=null&&hp.bayDoors.Length>0) {
                    continue;
                }
                var mounted=spawned.GetComponentsInChildren<Weapon>(true);int rounds=mounted.Sum(w=>w.GetAmmoTotal());
                float area=mount.emptyDrag;
                if(mount.ammo>0)area+=Mathf.Max(0,mount.drag-mount.emptyDrag)*Mathf.Clamp01((float)rounds/mount.ammo);
                drag.Add(new Fs20TakeoffStore{key=mount.jsonKey,mass=0,dragArea=Mathf.Max(0,area),position=transform.InverseTransformPoint(hp.transform.position)});
            }
            var request=new Fs20TakeoffRequest{fuelFraction=fuel/capacity,actualMassProperties=true,actualMass=mass,actualCenter=transform.InverseTransformPoint(worldCenter),
                actualPitchInertia=inertia,stores=drag.ToArray(),headwind=Vector3.Dot(relativeWind,runwayDirection.normalized),
                availableRun=remainingRun,density=a.airDensity,afterburner=afterburner};
            string key=JsonUtility.ToJson(request);
            if(key!=lastKey){LastPlan=Fs20DynamicTakeoff.Calculate(Variant,request);lastKey=key;}
            return LastPlan;
        }
        [HarmonyPatch(typeof(AIPilotTakeoffState),"EnterState")]
        internal static class NativePreflight
        {
            static void Postfix(AIPilotTakeoffState __instance,Pilot __0,Vector3 ___runwayVector,float ___runwayLength,Airbase ___airbase)
            {
                var a=__0==null?null:__0.aircraft;if(a==null||!a.LocalSim||__0.playerControlled)return;
                var planner=a.GetComponent<Fs20TakeoffPlanner>();if(planner==null)return;
                try {
                    var attached=___airbase==null?null:(Unit)Fs20Tuning.Field(typeof(Airbase),"attachedUnit").GetValue(___airbase);
                    var velocity=attached!=null&&attached.rb!=null?attached.rb.velocity:Vector3.zero;
                    float remaining=___runwayLength;
                    var usage=Fs20Tuning.Field(typeof(AIPilotTakeoffState),"runwayUsage").GetValue(__instance);
                    if(usage!=null) {
                        var runway=Fs20Tuning.Field(usage.GetType(),"Runway").GetValue(usage);
                        var start=(Transform)Fs20Tuning.Field(runway.GetType(),"Start").GetValue(runway);
                        var end=(Transform)Fs20Tuning.Field(runway.GetType(),"End").GetValue(runway);
                        var dir=___runwayVector.normalized;
                        var launchEnd=Vector3.Dot(end.position-start.position,dir)>=0?end.position:start.position;
                        remaining=Mathf.Min(remaining,Mathf.Max(0,Vector3.Dot(launchEnd-a.transform.position,dir)));
                    }
                    var plan=planner.Estimate(___runwayVector,velocity,remaining,true);
                    VortexConventionalPlugin.ReportAssetState("FS-20 takeoff advisory: "+planner.Variant+" "+plan.status+
                        "; mass="+plan.mass.ToString("F0")+" kg; required="+plan.requiredRun.ToString("F1")+" m; remaining="+remaining.ToString("F1")+
                        " m; ready-afterburner model; "+plan.reason);
                }catch(Exception e){VortexConventionalPlugin.ReportAssetState("FS-20 takeoff advisor unavailable: "+e.GetType().Name);}
            }
        }
    }
}

