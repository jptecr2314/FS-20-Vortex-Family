using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20NATailClosure
    {
        internal static void Install(Transform root)
        {
            var surfaces=new List<(Vector3[] p,Vector2[] uv,int[] ids)>();Material skin=null;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                var renderer=filter.GetComponent<Renderer>();if(renderer==null || filter.sharedMesh==null || filter.name.Contains("LOD"))continue;
                var slots=renderer.sharedMaterials;
                if(!slots.Any(m=>m!=null&&m.name.Contains("skin")))continue;
                var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);var map=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int sub=0;sub<mesh.subMeshCount && sub<slots.Length;sub++)if(slots[sub]!=null&&slots[sub].name.Contains("skin")) {
                    skin=slots[sub];surfaces.Add((mesh.vertices.Select(map.MultiplyPoint3x4).ToArray(),mesh.uv,mesh.GetTriangles(sub)));
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            (Vector3 point,Vector2 uv) At(float x,float z) {
                float height=-99f;Vector2 uv=default;
                foreach(var s in surfaces)for(int k=0;k<s.ids.Length;k+=3) {
                    int ia=s.ids[k],ib=s.ids[k+1],ic=s.ids[k+2];var a=s.p[ia];var b=s.p[ib];var c=s.p[ic];
                    float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<1e-9f)continue;
                    float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d;
                    float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d;
                    if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                    float y=u*a.y+v*b.y+(1-u-v)*c.y;
                    if(y<=height)continue;height=y;
                    if(s.uv.Length==s.p.Length)uv=u*s.uv[ia]+v*s.uv[ib]+(1-u-v)*s.uv[ic];
                }
                if(height<-2f)throw new InvalidOperationException("NA fin-root closure has no supporting skin.");
                return (new Vector3(x,height+.002f,z),uv);
            }
            foreach(int side in new[]{-1,1}) {
                var owner=root.Find("engine/tail_"+(side<0?"L":"R"));
                var points=new List<Vector3>();var coords=new List<Vector2>();var triangles=new List<int>();
                foreach(float z in new[]{-3.48f,-3.8f,-4.15f,-4.5f,-4.85f,-5.2f,-5.55f,-5.92f}) {
                    float t=Mathf.InverseLerp(-3.48f,-5.92f,z),center=Mathf.Lerp(1.075f,.915f,t),width=Mathf.Lerp(.16f,.11f,t);
                    foreach(float x in new[]{center-width,center+width}){var sample=At(side*x,z);points.Add(owner.InverseTransformPoint(root.TransformPoint(sample.point)));coords.Add(sample.uv);}
                }
                for(int i=0;i<points.Count-2;i+=2)foreach(var tri in new[]{new[]{i,i+1,i+2},new[]{i+1,i+3,i+2}}) {
                    var normal=owner.TransformDirection(Vector3.Cross(points[tri[1]]-points[tri[0]],points[tri[2]]-points[tri[0]]));
                    if(Vector3.Dot(normal,root.up)<0){int swap=tri[1];tri[1]=tri[2];tri[2]=swap;}triangles.AddRange(tri);
                }
                var mesh=new Mesh{name="FS20NA sealed former fin root"};mesh.SetVertices(points);mesh.SetUVs(0,coords);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var node=new GameObject("FS20NA fin-root closure "+(side<0?"L":"R"));node.layer=owner.gameObject.layer;node.transform.SetParent(owner,false);
                node.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=skin;
                Fs20HookInterior.RegisterDamageRenderer(owner,renderer);
                var part=owner.GetComponent<AeroPart>();var field=Fs20Tuning.Field(typeof(UnitPart),"disintegrateObjects");
                field.SetValue(part,((GameObject[])field.GetValue(part)??Array.Empty<GameObject>()).Concat(new[]{node}).ToArray());
                var group=owner.GetComponent<LODGroup>();if(group!=null){var levels=group.GetLODs();for(int i=0;i<levels.Length;i++)levels[i].renderers=levels[i].renderers.Concat(new[]{renderer}).ToArray();group.SetLODs(levels);}
            }
        }
    }
}
