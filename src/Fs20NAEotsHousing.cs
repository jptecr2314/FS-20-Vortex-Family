using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // A visual housing for the existing ventral EO port, not a new laser/TGP.
    internal static class Fs20NAEotsHousing
    {
        internal static void Install(Transform root)
        {
            var node=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FS20NA EO Ventral");
            var owner=node.GetComponentInParent<AeroPart>(true);
            var surfaces=new List<(Vector3[] p,Vector2[] uv,int[] ids)>();Material skin=null;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                var renderer=filter.GetComponent<Renderer>();if(renderer==null||filter.sharedMesh==null||filter.name.Contains("LOD"))continue;
                var materials=renderer.sharedMaterials;if(!materials.Any(m=>m!=null&&m.name.Contains("skin")))continue;
                var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);var map=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int sub=0;sub<mesh.subMeshCount&&sub<materials.Length;sub++)if(materials[sub]!=null&&materials[sub].name.Contains("skin")) {
                    skin=materials[sub];surfaces.Add((mesh.vertices.Select(map.MultiplyPoint3x4).ToArray(),mesh.uv,mesh.GetTriangles(sub)));
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            (float y,Vector2 uv) SkinAt(float x,float z) {
                float best=float.PositiveInfinity,y=0;Vector2 uv=default;
                foreach(var s in surfaces)for(int k=0;k<s.ids.Length;k+=3) {
                    int ia=s.ids[k],ib=s.ids[k+1],ic=s.ids[k+2];var a=s.p[ia];var b=s.p[ib];var c=s.p[ic];
                    if(Vector3.Cross(b-a,c-a).y>=-1e-9f)continue;
                    float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<1e-9f)continue;
                    float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d;
                    float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d;if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                    float height=u*a.y+v*b.y+(1-u-v)*c.y,score=Mathf.Abs(height+1.33f);if(score>=best)continue;
                    best=score;y=height;if(s.uv.Length==s.p.Length)uv=u*s.uv[ia]+v*s.uv[ib]+(1-u-v)*s.uv[ic];
                }
                if(float.IsInfinity(best))throw new InvalidOperationException("EOTS has no belly skin support.");return (y,uv);
            }
            // Recess every component against the same local belly contour.
            Vector3 LowProfile(Vector3 p){float y=SkinAt(p.x,p.z).y;return new Vector3(p.x,y+(p.y-y)*.40f,p.z);}
            // Front ends before the nose-gear doors (native rear limit z=3.086).
            // Rear stays ahead of the C weapon-bay opening (front z=1.875).
            var footprint=new[]{new Vector2(-.18f,2.98f),new Vector2(.18f,2.98f),new Vector2(.26f,2.80f),new Vector2(.24f,2.25f),new Vector2(.12f,2.08f),new Vector2(-.12f,2.08f),new Vector2(-.24f,2.25f),new Vector2(-.26f,2.80f)};
            var top=new Vector3[8];var bottom=new Vector3[8];
            for(int i=0;i<8;i++) {
                var p=footprint[i];var at=SkinAt(p.x,p.y);top[i]=new Vector3(p.x,at.y+.003f,p.y);
                float bx=p.x*.75f,bz=Mathf.Lerp(2.53f,p.y,.88f);
                bottom[i]=new Vector3(bx,-1.575f+.20f*Mathf.Abs(bx)+.08f*(2.53f-bz),bz);
            }
            // The scanner origin follows the actual forward glazing, not the
            // old flat window buried underneath the housing.
            node.position=root.TransformPoint(LowProfile(new Vector3(0,(bottom[0].y+top[0].y)*.5f,2.975f)));
            node.rotation=root.rotation*Quaternion.LookRotation(new Vector3(0,-.14f,1).normalized);
            var body=new MeshBuilder(root,node,SkinAt,LowProfile);var glass=new MeshBuilder(root,node,SkinAt,LowProfile);var hull=new MeshBuilder(root,node,SkinAt,LowProfile);
            Vector3 inside=new Vector3(0,-1.40f,2.53f);
            void Face(Vector3[] points,bool window,int coating) {
                var center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;
                var normal=Vector3.Cross(points[1]-points[0],points[2]-points[0]);
                if(Vector3.Dot(normal,center-inside)<0)Array.Reverse(points);
                hull.Polygon(points,0);
                if(!window){body.Polygon(points,0);return;}
                var inset=points.Select(p=>Vector3.Lerp(center,p,.90f)).ToArray();
                for(int i=0;i<points.Length;i++)body.Polygon(new[]{points[i],points[(i+1)%points.Length],inset[(i+1)%points.Length],inset[i]},0);
                glass.Polygon(inset,coating);
            }
            Face(top,false,0);
            for(int i=0;i<8;i++)Face(new[]{top[i],top[(i+1)%8],bottom[(i+1)%8],bottom[i]},i<3||i==6||i==7,i%3);
            // Broad planar glazing, rather than a star of small triangular panes.
            Vector3 Ridge(float z)=>new Vector3(0,-1.575f+.08f*(2.53f-z),z);
            var ridgeFront=Ridge(bottom[0].z);var ridgeRear=Ridge(bottom[4].z);
            Face(new[]{bottom[1],bottom[2],bottom[3],bottom[4],ridgeRear,ridgeFront},true,0);
            Face(new[]{bottom[5],bottom[6],bottom[7],bottom[0],ridgeFront,ridgeRear},true,1);
            Face(new[]{bottom[0],bottom[1],ridgeFront},true,2);
            Face(new[]{bottom[4],bottom[5],ridgeRear},false,0);
            var casing=new Material(skin){name="SmallFighter1_skin FS20NA EOTS fairing"};
            node.GetComponent<MeshFilter>().sharedMesh=body.Mesh("FS20NA faceted EOTS frame");node.GetComponent<Renderer>().sharedMaterial=casing;
            var coatings=new Material[3];
            var colors=new[]{new Color(.035f,.10f,.105f,.63f),new Color(.085f,.04f,.115f,.60f),new Color(.09f,.09f,.055f,.60f)};
            for(int i=0;i<3;i++) {
                var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="FS20NA EOTS coated optical glass "+i,color=colors[i],renderQueue=3000};
                m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Smoothness",.70f);m.SetFloat("_Metallic",.15f);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");coatings[i]=m;
            }
            var glazing=new GameObject("FS20NA EOTS faceted optical glazing");glazing.layer=node.gameObject.layer;glazing.transform.SetParent(node,false);
            glazing.AddComponent<MeshFilter>().sharedMesh=glass.Mesh("FS20NA multi-facet optical window");var glazingRenderer=glazing.AddComponent<MeshRenderer>();glazingRenderer.sharedMaterials=coatings;
            Fs20HookInterior.RegisterDamageRenderer(owner.transform,glazingRenderer);
            var interior=new MeshBuilder(root,node,SkinAt,LowProfile);var centerLens=new Vector3(0,-1.445f,2.68f);
            var axis=new Vector3(0,-.25f,1).normalized;var side=Vector3.right;var up=Vector3.Cross(axis,side).normalized;
            var front=new Vector3[8];var back=new Vector3[8];
            for(int i=0;i<8;i++){var offset=(side*Mathf.Cos(i*Mathf.PI/4)+up*Mathf.Sin(i*Mathf.PI/4))*.068f;front[i]=centerLens+axis*.08f+offset;back[i]=centerLens-axis*.06f+offset;}
            for(int i=0;i<8;i++)interior.Polygon(new[]{front[i],back[i],back[(i+1)%8],front[(i+1)%8]},0);
            interior.Polygon(front,1);interior.Polygon(back.Reverse().ToArray(),0);
            var barrel=new GameObject("FS20NA EOTS internal optical barrel");barrel.layer=node.gameObject.layer;barrel.transform.SetParent(node,false);
            barrel.AddComponent<MeshFilter>().sharedMesh=interior.Mesh("FS20NA eight-sided internal optic");
            var barrelMaterial=new Material(coatings[0]){name="FS20NA EOTS optical barrel",color=new Color(.025f,.027f,.030f,1),renderQueue=2000};
            barrelMaterial.SetFloat("_Surface",0);barrelMaterial.SetFloat("_SrcBlend",1);barrelMaterial.SetFloat("_DstBlend",0);barrelMaterial.SetFloat("_ZWrite",1);barrelMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            var barrelRenderer=barrel.AddComponent<MeshRenderer>();barrelRenderer.sharedMaterials=new[]{barrelMaterial,coatings[0]};Fs20HookInterior.RegisterDamageRenderer(owner.transform,barrelRenderer);
            var lod=node.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[]{new LOD(.006f,new Renderer[]{node.GetComponent<Renderer>(),glazingRenderer,barrelRenderer}),new LOD(.001f,new Renderer[]{node.GetComponent<Renderer>(),glazingRenderer})});lod.RecalculateBounds();
            var collider=node.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=hull.Mesh("FS20NA EOTS convex contact hull");collider.convex=true;
            var disintegration=Fs20Tuning.Field(typeof(UnitPart),"disintegrateObjects");
            disintegration.SetValue(owner,((GameObject[])disintegration.GetValue(owner)??Array.Empty<GameObject>()).Concat(new[]{node.gameObject}).Distinct().ToArray());
            Fs20ExpandedBays.AddMassAt(root,owner,25f,LowProfile(new Vector3(0,-1.44f,2.53f)));
            var marker=owner.GetComponent<Fs20TPartPhysics>();
            if(marker!=null) {
                var bounds=(Bounds)Fs20Tuning.Field(typeof(UnitPart),"bounds").GetValue(owner);
                if(bounds.size.sqrMagnitude>0f){foreach(var p in collider.sharedMesh.vertices)bounds.Encapsulate(owner.transform.InverseTransformPoint(node.TransformPoint(p)));marker.StoreCollisionBounds(bounds);}
            }
        }
        private sealed class MeshBuilder
        {
            private readonly Transform root,node;private readonly Func<float,float,(float y,Vector2 uv)> sample;
            private readonly Func<Vector3,Vector3> profile;
            private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<List<int>> indices=new List<List<int>>();
            internal MeshBuilder(Transform r,Transform n,Func<float,float,(float y,Vector2 uv)> s,Func<Vector3,Vector3> p){root=r;node=n;sample=s;profile=p;}
            internal void Polygon(Vector3[] points,int material) {
                while(indices.Count<=material)indices.Add(new List<int>());int start=vertices.Count;
                foreach(var p in points){vertices.Add(node.InverseTransformPoint(root.TransformPoint(profile(p))));uv.Add(sample(p.x,p.z).uv);}
                for(int i=1;i<points.Length-1;i++)indices[material].AddRange(new[]{start,start+i,start+i+1});
            }
            internal Mesh Mesh(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=indices.Count;for(int i=0;i<indices.Count;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
