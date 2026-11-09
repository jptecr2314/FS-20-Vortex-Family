using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20AInternalTank
    {
        internal static void Install(Transform root)
        {
            var bay=root.Find("liftFan");var part=bay.GetComponent<AeroPart>();
            if(bay.GetComponent<FuelTank>()!=null)throw new InvalidOperationException("Lift-fan bay already has a fuel tank.");
            var donor=root.GetComponent<FuelTank>();
            if(donor==null)throw new InvalidOperationException("Native fuel-system donor missing.");
            var tank=bay.gameObject.AddComponent<FuelTank>();
            // Retain native leaks, ignition, refuel/consumption and damage hooks.
            // Attaching to the existing part also preserves native fuel-status RPC lookup.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(donor),tank);
            Fs20Tuning.Set(tank,"fuelCapacity",Fs20AUpgradeSpec.ExtraFuel);
            Fs20Tuning.Set(tank,"part",part);
            Fs20Tuning.Set(tank,"connectedTanks",Array.Empty<FuelTank>());
            tank.fuelMass=0f;
            part.mass+=Fs20AUpgradeSpec.TankDryMass;
            var body=new GameObject("FS-20A internal fuel cell");
            body.transform.SetParent(bay,false);
            body.transform.position=root.TransformPoint(new Vector3(Fs20AUpgradeSpec.TankCenterX,
                Fs20AUpgradeSpec.TankCenterY,Fs20AUpgradeSpec.TankCenterZ));
            body.transform.rotation=root.rotation;
            body.AddComponent<MeshFilter>().sharedMesh=BuildMesh();
            var materials=bay.GetComponent<Renderer>().sharedMaterials;
            body.AddComponent<MeshRenderer>().sharedMaterial=materials.FirstOrDefault(m=>m!=null &&
                m.name.StartsWith("aircraft_structure")) ?? materials.Last();
        }

        private static Mesh BuildMesh()
        {
            const int count=48;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float outer=Fs20AUpgradeSpec.TankOuterRadius,inner=Fs20AUpgradeSpec.TankInnerRadius,
                half=Fs20AUpgradeSpec.TankHeight*.5f;
            Func<int,float> angle=i=>(Fs20AUpgradeSpec.TankStartDegrees+Fs20AUpgradeSpec.TankSectorDegrees*i/count)*Mathf.Deg2Rad;
            Func<float,float,float,Vector3> point=(r,y,a)=>new Vector3(r*Mathf.Cos(a),y,r*Mathf.Sin(a));
            for(int i=0;i<count;i++)
            {
                float a=angle(i),b=angle(i+1),u=(float)i/count,v=(float)(i+1)/count;
                var radial=point(1,0,(a+b)*.5f);
                Quad(vertices,uv,triangles,point(outer,-half,a),point(outer,-half,b),point(outer,half,b),point(outer,half,a),radial,
                    new Vector2(u,0),new Vector2(v,0),new Vector2(v,1),new Vector2(u,1));
                Quad(vertices,uv,triangles,point(inner,-half,a),point(inner,half,a),point(inner,half,b),point(inner,-half,b),-radial,
                    new Vector2(u,0),new Vector2(u,1),new Vector2(v,1),new Vector2(v,0));
                foreach(float y in new[]{-half,half})
                {
                    var p=new[]{point(outer,y,a),point(inner,y,a),point(inner,y,b),point(outer,y,b)};
                    Quad(vertices,uv,triangles,p[0],p[1],p[2],p[3],y<0?Vector3.down:Vector3.up,
                        CapUv(p[0],outer),CapUv(p[1],outer),CapUv(p[2],outer),CapUv(p[3],outer));
                }
            }
            foreach(int end in new[]{0,count})
            {
                float a=angle(end);var normal=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*(end==0?-1:1);
                Quad(vertices,uv,triangles,point(inner,-half,a),point(outer,-half,a),point(outer,half,a),point(inner,half,a),normal,
                    new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1));
            }
            var mesh=new Mesh{name="FS20A annular fuel cell with driveline clearance"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        private static Vector2 CapUv(Vector3 p,float radius)=>new Vector2(.5f+.48f*p.x/radius,.5f+.48f*p.z/radius);
        private static void Quad(List<Vector3> v,List<Vector2> uv,List<int> t,
            Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud)
        {
            int i=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{ua,ub,uc,ud});
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),n)>0)t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            else t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
        }
    }
}
