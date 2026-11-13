using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed partial class Fs20TVapor
    {
        private static void AddTrailingCoverage(Transform root,List<Layer> result,Material material,ParticleSystem donor)
        {
            foreach(string side in new[]{"L","R"})
                foreach(string path in new[]{"engine/wing1b_"+side,"engine/wing1b_"+side+"/wing2b_"+side}){
                    var frame=root.Find(path);
                    AddRearSkin(root,result,frame.GetComponent<AeroPart>(),frame,material,donor,"rear",false);
                }
            var visibleMesh=Fs20Tuning.Field(typeof(ControlSurface),"visibleMesh");
            foreach(var control in root.GetComponentsInChildren<ControlSurface>(true)){
                if(!control.name.StartsWith("elevator_",StringComparison.Ordinal) && !control.name.StartsWith("aileron_",StringComparison.Ordinal))continue;
                var visible=(GameObject)visibleMesh.GetValue(control);
                if(visible==null || !visible.transform.IsChildOf(control.transform))
                    throw new InvalidOperationException("FS20 moving trailing skin missing: "+control.name);
                var attached=(UnitPart)Fs20Tuning.Field(typeof(ControlSurface),"attachedSurface").GetValue(control);
                var owner=attached as AeroPart ?? control.GetComponentInParent<AeroPart>(true);
                AddRearSkin(root,result,owner,visible.transform,material,donor,"trailing",true);
            }
            if(result.Count(l=>l.Role=="trailing")!=4)throw new InvalidOperationException("FS20 expected four moving trailing surfaces.");
        }

        private static void AddRearSkin(Transform root,List<Layer> result,AeroPart owner,Transform frame,
            Material material,ParticleSystem donor,string role,bool moving)
        {
            var filter=frame.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null)filter=frame.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(f=>f.sharedMesh!=null && f.name.IndexOf("LOD",StringComparison.OrdinalIgnoreCase)<0 &&
                    f.GetComponent<MeshRenderer>()!=null && f.GetComponent<MeshRenderer>().enabled);
            if(owner==null || filter==null || filter.sharedMesh==null)throw new InvalidOperationException("FS20 aft wing skin missing: "+frame.name);
            var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);
            try{
                var layer=Create(root,owner,donor,material,role+"_"+owner.name,
                    new[]{Skin(mesh.vertices,mesh.GetTriangles(0),filter.transform.localToWorldMatrix,frame,root)},new[]{1f},0,0,0,frame);
                layer.Auxiliary=true;layer.Role=role;
                // Use the control's own native force; it is not added again to
                // tip circulation. Incoming condensate comes from the adjacent wing.
                BindIncoming(root,result,layer,moving);
                result.Add(layer);
            }finally{Release(mesh);}
        }

        private static void BindIncoming(Transform root,List<Layer> result,Layer layer,bool moving)
        {
            var points=layer.Surfaces[0].vertices.Select(p=>root.InverseTransformPoint(layer.Frame.TransformPoint(p))).ToArray();
            float front=points.Max(p=>p.z),band=Mathf.Max(.04f,(front-points.Min(p=>p.z))*.1f);
            float nearest=float.PositiveInfinity;Vector3 entry=default,exit=default;
            for(int index=0;index<result.Count;index++){
                var candidate=result[index];
                if(candidate.Left!=layer.Left || (moving ? candidate.Role!="rear" : candidate.Role!="wing"))continue;
                var shape=candidate.SheetShapes[0];
                foreach(var p in points.Where(p=>p.z>=front-band))for(int sample=0;sample<shape.Points.Length;sample++){
                    if(shape.Edge[sample]<=0f)continue;
                    var q=root.InverseTransformPoint(candidate.Frame.TransformPoint(shape.Points[sample]));
                    if(q.z<p.z-.2f)continue;
                    float distance=(p-q).sqrMagnitude;
                    if(distance>=nearest)continue;
                    nearest=distance;layer.UpstreamIndex=index;entry=p;exit=q;
                }
            }
            if(layer.UpstreamIndex<0)throw new InvalidOperationException("FS20 upstream wing missing: "+layer.Key);
            layer.FlowEntry=layer.Frame.InverseTransformPoint(root.TransformPoint(entry));
            layer.FlowExit=result[layer.UpstreamIndex].Frame.InverseTransformPoint(root.TransformPoint(exit));
        }

        private static void FitWingChords(Transform root,List<Layer> layers)
        {
            // A single planform coordinate across front/rear pieces prevents a
            // new leading-edge suction peak at every hinge or ownership boundary.
            var wing=layers.Where(l=>l.Role!="body" && l.Role!="slat").ToArray();
            var vertices=new List<Vector3>();var indices=new List<int>();
            foreach(var layer in wing){
                int offset=vertices.Count;
                foreach(var p in layer.Surfaces[0].vertices){
                    var point=root.InverseTransformPoint(layer.Frame.TransformPoint(p));
                    vertices.Add(new Vector3(point.z,point.y,point.x));
                }
                indices.AddRange(layer.Surfaces[0].triangles.Select(i=>i+offset));
            }
            var skin=vertices.ToArray();var triangles=indices.ToArray();
            foreach(var layer in wing){
                float length=0f;
                foreach(var shape in layer.SheetShapes){
                    var map=shape.VolumeSkin;var pixels=map.GetPixels();
                    for(int x=0;x<map.width;x++){
                        float px=Mathf.Lerp(shape.VolumeMin.x,shape.VolumeMax.x,(x+.5f)/map.width);
                        var spans=Fs20TVaporPath.Slice(skin,triangles,px);if(spans.Count==0)continue;
                        float front=spans.Max(s=>s.Max),rear=spans.Min(s=>s.Min);length=Mathf.Max(length,front-rear);
                        for(int z=0;z<map.height;z++){
                            float pz=Mathf.Lerp(shape.VolumeMin.z,shape.VolumeMax.z,(z+.5f)/map.height);
                            pixels[z*map.width+x].b=Mathf.InverseLerp(front,rear,pz);
                        }
                    }
                    map.SetPixels(pixels);map.Apply(false,false);
                }
                layer.FlowLength=Mathf.Max(.01f,length);
            }
        }

        private float CarryIncoming(Layer layer,bool fresh,int stage,Vector3 flow)
        {
            if(!fresh || stage!=0 || layer.UpstreamIndex<0 || layer.UpstreamIndex>=layers.Length ||
                Vector3.Dot(flow,transform.forward)<=1f)return 0f;
            var source=layers[layer.UpstreamIndex];
            if(source.LastState!=0 || source.LoadCount==0 || Time.fixedTime-source.LoadTime>.15f)return 0f;
            float gap=Vector3.Distance(layer.Frame.TransformPoint(layer.FlowEntry),source.Frame.TransformPoint(layer.FlowExit));
            var up=layer.Frame.TransformDirection(layer.SheetShapes[0].GridToFrame.MultiplyVector(Vector3.up)).normalized;
            var sourceUp=source.Frame.TransformDirection(source.SheetShapes[0].GridToFrame.MultiplyVector(Vector3.up)).normalized;
            float alignment=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(Vector3.Dot(up,sourceUp)));
            float retention=alignment*Mathf.Exp(-gap/(Mathf.Max(1f,flow.magnitude)*.06f));
            float peak=0f;
            for(int station=0;station<layer.Profile.Length;station++){
                float water=-Mathf.Log(Mathf.Max(.0001f,1f-source.Profile[station]))/(750f*.6f);
                float carried=1f-Mathf.Exp(-750f*.6f*water*retention);
                layer.Profile[station]=Mathf.Max(layer.Profile[station],carried);peak=Mathf.Max(peak,layer.Profile[station]);
            }
            return peak;
        }
    }
}
