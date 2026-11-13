using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20VaporDriver
    {
        internal const string CKey="JP_FS20_CTOL";
        private static readonly FieldInfo Emitters=Fs20Tuning.Field(typeof(VaporEffect),"emitters");
        private static readonly Dictionary<int,List<Fs20TVapor.Layer>> Samples=new Dictionary<int,List<Fs20TVapor.Layer>>();
        internal static void Register(Fs20TVapor.Layer layer){
            layer.OwnerId=layer.SourceOwner.GetInstanceID();
            if(!Samples.TryGetValue(layer.OwnerId,out var list)){list=new List<Fs20TVapor.Layer>();Samples.Add(layer.OwnerId,list);}
            if(!list.Contains(layer))list.Add(layer);
        }
        internal static void Unregister(Fs20TVapor.Layer layer)
        {
            if(Samples.TryGetValue(layer.OwnerId,out var list)){list.Remove(layer);if(list.Count==0)Samples.Remove(layer.OwnerId);}
        }
        [HarmonyPatch(typeof(AeroPart),"ApplyJobFields")]
        internal static class ReadLoads
        {
            // The native application point runs after the aero job completes.
            // Copy scalars only; never read unsynchronised jobs or modify forces.
            private static void Postfix(AeroPart __instance,ref PtrAllocation<AeroPartFields> ___JobFields)
            {
                if(!Samples.ContainsKey(__instance.GetInstanceID()) || !___JobFields.IsCreated)return;
                ref AeroPartFields fields=ref ___JobFields.Ref();
                Capture(__instance,fields);
            }
            internal static void Capture(AeroPart part,AeroPartFields fields)
            {
                if(!Samples.TryGetValue(part.GetInstanceID(),out var list))return;
                var lift=part.GetLiftTransform();
                Vector3 normal=lift==null ? part.transform.up : lift.up;
                var perpendicular=Vector3.ProjectOnPlane(normal,fields.velocity).normalized;
                float liftForce=fields.hasForce==JobForceType.NoForce || fields.submergedAmount>0f ? 0f : Vector3.Dot(fields.force,perpendicular);
                foreach(var layer in list){
                    if(!layer.CollectLoads)continue;
                    layer.Lift=liftForce;layer.Area=fields.wingArea;layer.LoadTime=Time.fixedTime;layer.LoadCount++;
                }
            }
        }
        internal static string Family(Aircraft aircraft)
        {
            if(aircraft==null)return null;
            string key=aircraft.definition==null?null:aircraft.definition.jsonKey;
            if(key==CKey || key==Fs20TAirframe.Key)return key;
            string name=aircraft.name.Replace("(Clone)","");
            return name==CKey || name==Fs20TAirframe.Key ? name : null;
        }
        private static Fs20TVapor Resolve(VaporEffect effect,Aircraft aircraft)
        {
            if(Family(aircraft)==null)return null;
            var controller=effect.GetComponent<Fs20TVapor>();
            return controller!=null?controller:effect.gameObject.AddComponent<Fs20TVapor>();
        }
        [HarmonyPatch(typeof(VaporEffect),"OnEnable")]
        internal static class Activate
        {
            private static void Prefix(VaporEffect __instance,ref Aircraft ___aircraft)
            {
                if(___aircraft==null){var candidate=__instance.GetComponent<Aircraft>();if(Family(candidate)!=null)___aircraft=candidate;}
            }
            private static void Postfix(VaporEffect __instance,Aircraft ___aircraft)
            {
                if(Family(___aircraft)==null)return;
                foreach(var emitter in (VaporEmitter[])Emitters.GetValue(__instance))emitter.Initialize();
                var controller=Resolve(__instance,___aircraft);
                controller.BindBeforePhysics();
                controller.GuardTips();
                VortexConventionalPlugin.ReportAssetState(___aircraft.name+" vapor native lifecycle bound.");
            }
        }
        [HarmonyPatch(typeof(Aircraft),"LocalSimFixedUpdate")]
        internal static class Update
        {
            private static void Postfix(Aircraft __instance)
            {
                if(Family(__instance)==null)return;
                var effect=__instance.GetComponent<VaporEffect>();
                if(effect==null)return;
                // The live flight never entered the patched VaporEffect message.
                // Use the managed aircraft simulation entry observed running.
                // Disable automatic emission so native tips cannot be emitted twice.
                effect.enabled=false;
                Resolve(effect,__instance).NativeTick(Time.fixedDeltaTime);
            }
        }
        internal static void FitTips(Transform root,VaporEffect effect)
        {
            foreach(var emitter in (VaporEmitter[])Emitters.GetValue(effect))
            {
                if(emitter.particles==null || emitter.particles.name.IndexOf("wingtip",StringComparison.OrdinalIgnoreCase)<0)continue;
                var main=emitter.particles.main;main.startSizeMultiplier*=.7f;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                main.maxParticles=128;
                var emission=emitter.particles.emission;emission.enabled=false;
                for(int i=0;i<emitter.emitTransforms.Length;i++)
                {
                    var origin=emitter.emitTransforms[i];var owner=origin.GetComponentInParent<UnitPart>(true);
                    if(owner==null)throw new InvalidOperationException("FS-20 tip trail has no structural owner.");
                    var target=new GameObject("FS20 vapor tip origin").transform;target.SetParent(owner.transform,false);target.position=origin.position;
                    var filter=owner.GetComponent<MeshFilter>();
                    if(filter!=null && filter.sharedMesh!=null)
                    {
                        var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);
                        try{
                            var points=mesh.vertices.Select(p=>root.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();
                            float side=Mathf.Sign(root.InverseTransformPoint(origin.position).x);float edge=points.Max(p=>p.x*side);
                            var point=points.Where(p=>p.x*side>edge-.025f).OrderBy(p=>p.z).ThenByDescending(p=>p.y).First();
                            target.position=root.TransformPoint(point+Vector3.up*.035f);
                        }finally{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
                    }
                    emitter.emitTransforms[i]=target;
                }
            }
        }
    }
}
