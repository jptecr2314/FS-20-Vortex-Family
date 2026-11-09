using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20ManeuverVapor : MonoBehaviour
    {
        [SerializeField] internal ParticleSystem[] Clouds;
        [SerializeField] internal UnitPart[] Owners;
        [SerializeField] internal bool[] LeftSide;
        [SerializeField] internal bool[] InnerLayer;
        private Aircraft aircraft;
        private readonly Fs20VaporEnvelope envelope=new Fs20VaporEnvelope();
        private static readonly FieldInfo Wind=Fs20Tuning.Field(typeof(Aircraft),"windVelocity");

        internal static void Install(Transform root,bool extendedWing)
        {
            var native=root.GetComponent<VaporEffect>();
            if(native==null)throw new InvalidOperationException("Native FS-20 vapor effect unavailable.");
            var field=Fs20Tuning.Field(typeof(VaporEffect),"emitters");
            var emitters=(VaporEmitter[])field.GetValue(native);
            var sheets=emitters.Where(e=>e.particles!=null && e.particles.name.StartsWith("wingVapor_")).ToArray();
            if(sheets.Length!=2)throw new InvalidOperationException("Expected two native wing vapor sheets.");
            // Resolve surfaces/owners before changing any native particle system.
            var roots=new Transform[2];var anchors=new Vector3[2];var outerAnchors=new Vector3[2];
            for(int i=0;i<2;i++)
            {
                var ps=sheets[i].particles;bool left=ps.name.EndsWith("_L");
                // Templates live below an inactive holder during registration.
                var owner=ps.GetComponentInParent<UnitPart>(true);
                if(owner==null)throw new InvalidOperationException("Wing vapor has no structural owner.");
                roots[i]=root.Find("liftFan/wingRoot_"+(left?"L":"R"));
                outerAnchors[i]=root.InverseTransformPoint(ps.transform.position);
                // Native sheet origins sit just ahead of the leading edge;
                // preserve that placement rather than projecting onto the wing.
                outerAnchors[i].y+=.08f;
                anchors[i]=outerAnchors[i];anchors[i].x*=.70f;anchors[i].z+=.55f;
                anchors[i].y=SurfaceHeight(root,roots[i],anchors[i].x,anchors[i].z)+.08f;
            }
            var controller=root.gameObject.AddComponent<Fs20ManeuverVapor>();
            var clouds=new List<ParticleSystem>();var parts=new List<UnitPart>();
            var sides=new List<bool>();var layers=new List<bool>();
            for(int i=0;i<2;i++)
            {
                var sheet=sheets[i];
                var ps=sheet.particles;
                bool left=ps.name.EndsWith("_L");
                var part=ps.GetComponentInParent<UnitPart>(true);
                if(part==null)throw new InvalidOperationException("Wing vapor has no structural owner.");
                Configure(ps,extendedWing,false);
                ps.transform.position=root.TransformPoint(outerAnchors[i]);
                clouds.Add(ps);parts.Add(part);sides.Add(left);layers.Add(false);
                var branch=roots[i];
                var inner=UnityEngine.Object.Instantiate(ps,branch,false);
                inner.name="FS-20 root condensation "+(left?"L":"R");
                inner.transform.position=root.TransformPoint(anchors[i]);
                inner.transform.localScale=Vector3.one;
                Configure(inner,extendedWing,true);
                clouds.Add(inner);parts.Add(branch.GetComponent<AeroPart>());sides.Add(left);layers.Add(true);
            }
            controller.Clouds=clouds.ToArray();controller.Owners=parts.ToArray();
            controller.LeftSide=sides.ToArray();controller.InnerLayer=layers.ToArray();
            // Native tip vortices and transonic effects retain their own behavior.
            field.SetValue(native,emitters.Where(e=>!sheets.Contains(e)).ToArray());
        }

        private static void Configure(ParticleSystem ps,bool wide,bool inner)
        {
            var main=ps.main;
            main.stopAction=ParticleSystemStopAction.None;
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            main.duration=1f;main.startDelay=0f;main.simulationSpeed=1f;
            main.loop=true;main.prewarm=false;main.playOnAwake=false;
            main.startSize3D=false;main.gravityModifier=0f;
            main.stopAction=ParticleSystemStopAction.None;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Local;
            main.maxParticles=inner?48:64;
            main.startLifetime=new ParticleSystem.MinMaxCurve(inner?.10f:.14f,inner?.22f:.30f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(inner?12f:18f,inner?18f:26f);
            float scale=wide?1.18f:1f;
            main.startSize=new ParticleSystem.MinMaxCurve((inner?1.2f:2.2f)*scale,(inner?2.5f:4.2f)*scale);
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;
            shape.angle=inner?10f:8f;shape.radius=inner?.04f:.06f;
            var emission=ps.emission;emission.enabled=true;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            emission.rateOverTime=0f;emission.rateOverDistance=0f;
            var color=ps.colorOverLifetime;color.enabled=true;
            var fade=new Gradient();
            fade.SetKeys(new[]{new GradientColorKey(new Color(.96f,.985f,1f),0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.10f),new GradientAlphaKey(.65f,.5f),new GradientAlphaKey(0,1)});
            color.color=fade;
            var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=false;
            size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.3f,1f),new Keyframe(1,1.35f)));
        }

        private static float SurfaceHeight(Transform root,Transform branch,float x,float z)
        {
            if(branch==null)throw new InvalidOperationException("Wing-root vapor anchor missing.");
            var mesh=Fs20Geometry.ReadableCopy(branch.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                var map=root.worldToLocalMatrix*branch.localToWorldMatrix;
                var v=mesh.vertices.Select(map.MultiplyPoint3x4).ToArray();var t=mesh.triangles;
                float highest=float.NegativeInfinity;
                for(int i=0;i<t.Length;i+=3)
                {
                    var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];
                    float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                    if(Mathf.Abs(denominator)<.000001f)continue;
                    float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/denominator;
                    float w=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/denominator;
                    if(u>=-.00001f && w>=-.00001f && u+w<=1.00001f)
                        highest=Mathf.Max(highest,u*a.y+w*b.y+(1-u-w)*c.y);
                }
                if(float.IsNegativeInfinity(highest))throw new InvalidOperationException("Vapor anchor outside wing-root skin.");
                return highest;
            }
            finally{UnityEngine.Object.Destroy(mesh);}
        }

        private void Awake(){aircraft=GetComponent<Aircraft>();}
        private void LateUpdate()
        {
            if(Clouds==null)return;
            if(aircraft==null || aircraft.rb==null){StopClouds();return;}
            var airflow=aircraft.rb.velocity-(Vector3)Wind.GetValue(aircraft);
            var local=aircraft.transform.InverseTransformDirection(airflow);
            float alpha=-Mathf.Atan2(local.y,Mathf.Max(1,local.z))*Mathf.Rad2Deg;
            float normalG=Vector3.Dot(aircraft.accel,aircraft.transform.up);
            float roll=aircraft.transform.InverseTransformDirection(aircraft.rb.angularVelocity).z;
            envelope.Step(aircraft.speed,aircraft.airDensity,alpha,normalG,aircraft.radarAlt,aircraft.displayDetail,roll,Time.deltaTime);
            for(int i=0;i<Clouds.Length;i++)
            {
                var ps=Clouds[i];if(ps==null)continue;
                bool alive=Owners[i]!=null && !Owners[i].IsDetached();
                float strength=alive?(LeftSide[i]?envelope.Left:envelope.Right):0;
                var emission=ps.emission;emission.rateOverTime=strength*(InnerLayer[i]?80f:140f);
                var main=ps.main;main.startColor=new Color(1,1,1,strength*(InnerLayer[i]?.24f:.38f));
                if(strength>.015f)
                {
                    if(airflow.sqrMagnitude>1)ps.transform.rotation=Quaternion.LookRotation(-airflow,aircraft.transform.up);
                    if(!ps.isPlaying)ps.Play(false);
                }
                else if(ps.isPlaying)ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            }
        }
        private void OnDisable(){StopClouds();}
        private void StopClouds()
        {
            if(Clouds==null)return;
            foreach(var ps in Clouds)if(ps!=null)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
