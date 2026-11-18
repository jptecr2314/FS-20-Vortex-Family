using System;
using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Own game abstraction, not a simulation of the F-35's image processor.
    public sealed class Fs20NADAS : TargetDetector
    {
        [SerializeField] private Transform[] apertures;
        [SerializeField] private AeroPart[] owners;
        [SerializeField] private Aircraft aircraft;
        [SerializeField] private Fs20NARecon recon;
        internal void Bind(Fs20NARecon value){recon=value;}
        private static readonly AccessTools.FieldRef<Unit,List<IRSource>> Sources=AccessTools.FieldRefAccess<Unit,List<IRSource>>("IRSources");
        private static readonly AccessTools.FieldRef<IRSource,float> Heat=AccessTools.FieldRefAccess<IRSource,float>("intensity");
        private static readonly AccessTools.FieldRef<TargetDetector,Transform> Scanner=AccessTools.FieldRefAccess<TargetDetector,Transform>("scanner");
        public int LastScanCandidates=>recon==null?0:recon.LastCandidates;
        public int ConfirmedContacts { get; private set; }
        public int OperationalSensorCount { get { int count=0;for(int i=0;apertures!=null&&i<apertures.Length;i++)if(Healthy(i))count++;return count; } }
        internal void Configure(Transform root,Transform[] ports,AeroPart[] parts)
        {
            apertures=ports;owners=parts;aircraft=root.GetComponent<Aircraft>();
            Fs20Tuning.Set(this,"attachedUnit",aircraft);Fs20Tuning.Set(this,"part",root.GetComponent<AeroPart>());
            Fs20Tuning.Set(this,"scanner",ports[0]);Fs20Tuning.Set(this,"visualRange",12000f);
            Fs20Tuning.Set(this,"magnification",1f);Fs20Tuning.Set(this,"maxSpeed",1600f);
            Fs20Tuning.Set(this,"checkInterval",.5f);Fs20Tuning.Set(this,"alertCheckInterval",.5f);Fs20Tuning.Set(this,"shared",false);
        }
        private bool Healthy(int i)=>apertures[i]!=null&&!Fs20NARecon.Lost(owners[i]);
        // Adapter entrypoint for an HMD provider, requiring no camera or shader hook.
        public bool TryGetSensorForDirection(Vector3 worldDirection,out Transform sensor)
        {
            sensor=null;if(worldDirection.sqrMagnitude<.0001f)return false;
            float best=-.18f;worldDirection.Normalize();
            for(int i=0;apertures!=null&&i<apertures.Length;i++)if(Healthy(i)) {
                float alignment=Vector3.Dot(apertures[i].forward,worldDirection);
                if(alignment>best){best=alignment;sensor=apertures[i];}
            }
            return sensor!=null;
        }
        internal static float ThermalRange(float intensity)=>float.IsNaN(intensity)?0f:Mathf.Min(12000f,4000f*Mathf.Sqrt(Mathf.Max(0f,intensity)));
        internal bool ThermalVisible(Unit target)
        {
            if(target==null||target==aircraft||target.disabled||!target.HasIRSignature())return false;
            if(!TryGetSensorForDirection(target.transform.position-aircraft.transform.position,out var sensor))return false;
            var sources=Sources(target);bool visible=false;
            // Deterministic, bounded evaluation. Native GetIRSource chooses a random source.
            for(int i=0;sources!=null&&i<Mathf.Min(8,sources.Count);i++) {
                var source=sources[i];if(source==null||source.transform==null||source.flare)continue;
                if((source.transform.position-target.transform.position).sqrMagnitude>10000f)continue;
                float range=ThermalRange(Heat(source));
                if(range>0f&&(source.transform.position-sensor.position).sqrMagnitude<=range*range){visible=true;break;}
            }
            if(!visible||!Fs20NARecon.ClearOwnBody(aircraft.transform,sensor,target.transform.position))return false;
            Scanner(this)=sensor;return true;
        }
        protected override void TargetSearch()
        {
            if(recon!=null)recon.Search();
        }
        [HarmonyPatch(typeof(TargetDetector),"InVisualRange")]
        internal static class ThermalGate
        {
            private static bool Prefix(TargetDetector __instance,Unit __0,ref bool __result)
            {
                if(!(__instance is Fs20NADAS das))return true;
                __result=das.ThermalVisible(__0);return false;
            }
        }
        [HarmonyPatch(typeof(TargetDetector),"DetectTarget")]
        internal static class Confirmed
        {
            private static void Postfix(TargetDetector __instance,Unit __0,bool __runOriginal)
            {
                if(!__runOriginal)return;
                if(!(__instance is Fs20NADAS das)||__0==null||das.aircraft==null||das.aircraft.disabled||das.OperationalSensorCount==0)return;
                var accepted=(List<Unit>)Fs20Tuning.Field(typeof(TargetDetector),"detectedTargets").GetValue(das);
                if(!accepted.Contains(__0))return;
                das.ConfirmedContacts++;
                // Preserve the native warning UI/voice and its range. Never turn
                // off the original MWS or invent a lock on a non-threatening missile.
                if(!(__0 is Missile missile)||missile.targetID!=das.aircraft.persistentID)return;
                var warning=das.aircraft.GetMissileWarningSystem();if(warning==null)return;
                var pending=(List<Missile>)Fs20Tuning.Field(typeof(MissileWarning),"unknownMissiles").GetValue(warning);
                if(!pending.Contains(missile)&&!warning.knownMissiles.Contains(missile))warning.LockedByMissile(das.aircraft,missile);
            }
        }
    }
}
