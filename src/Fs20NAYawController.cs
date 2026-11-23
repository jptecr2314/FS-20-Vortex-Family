using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // A finless airframe needs sideslip feedback, not just yaw-rate damping.
    // This allocates commands to the existing drag rudders and vectored nozzle;
    // it adds no forces, invisible fins or direct velocity corrections.
    public sealed class Fs20NAYawController : MonoBehaviour
    {
        [SerializeField] private Aircraft aircraft;
        [SerializeField] private ControlSurface[] splits;
        [SerializeField] private JetNozzle nozzle;
        [SerializeField] private Turbojet engine;
        [SerializeField] private Transform thrustPoint;
        [SerializeField] private UnitPart[] parts;
        [SerializeField] private AeroPart[] pitchParts;
        [SerializeField] private float[] pitchCoefficients;
        private readonly List<Rigidbody> bodies=new List<Rigidbody>(48);
        private bool reported;
        private float trim;
        private float previousRate,rateFeedforward;
        private bool rateInitialized;
        private float gyroscopicTorque;
        private float pitchGyroscopicTorque, pitchFeedforward;
        private float command;
        public float SideslipDegrees {get;private set;}
        public float Output=>command;
        public float PitchFeedforward=>pitchFeedforward;
        private static readonly AccessTools.FieldRef<ControlSurface,float> MaxSplit=AccessTools.FieldRefAccess<ControlSurface,float>("maxSplit");
        private static readonly AccessTools.FieldRef<ControlSurface,float> Side=AccessTools.FieldRefAccess<ControlSurface,float>("yawSplitFactor");
        private static readonly AccessTools.FieldRef<ControlSurface,float> Drag=AccessTools.FieldRefAccess<ControlSurface,float>("splitDrag");
        private static readonly AccessTools.FieldRef<Turbojet,Vector3> Vectoring=AccessTools.FieldRefAccess<Turbojet,Vector3>("thrustVectoring");
        private static readonly AccessTools.FieldRef<Turbojet,float> VectorLimit=AccessTools.FieldRefAccess<Turbojet,float>("thrustVectoringMaxAirspeed");
        private static readonly AccessTools.FieldRef<Aircraft,ControlsFilter> Controls=AccessTools.FieldRefAccess<Aircraft,ControlsFilter>("controlsFilter");
        internal void Configure(Transform root)
        {
            aircraft=root.GetComponent<Aircraft>();nozzle=root.GetComponentInChildren<JetNozzle>(true);engine=root.GetComponentInChildren<Turbojet>(true);
            thrustPoint=nozzle==null?null:(Transform)Fs20Tuning.Field(typeof(JetNozzle),"thrustTransform").GetValue(nozzle);
            splits=root.GetComponentsInChildren<ControlSurface>(true).Where(s=>MaxSplit(s)>0f).ToArray();
            parts=root.GetComponentsInChildren<UnitPart>(true);
            var parameters=((AircraftDefinition)aircraft.definition).aircraftParameters;
            pitchParts=root.GetComponentsInChildren<AeroPart>(true).Where(p=>p.GetComponent<ControlSurface>()!=null).ToArray();
            pitchCoefficients=new float[pitchParts.Length];
            var all=root.GetComponentsInChildren<AeroPart>(true);float mass=all.Sum(p=>p.mass);
            Vector3 center=Vector3.zero;foreach(var p in all)center+=p.mass*Fs20TControlCalibration.MassCenter(p);center/=Mathf.Max(1f,mass);
            for(int i=0;i<pitchParts.Length;i++){
                var p=pitchParts[i];var c=p.GetComponent<ControlSurface>();var visible=(GameObject)Fs20Tuning.Field(typeof(ControlSurface),"visibleMesh").GetValue(c);
                if(visible==null)continue;
                var marker=p.GetComponent<Fs20TPartPhysics>();var point=marker==null?Fs20TControlCalibration.MassCenter(p)+visible.transform.rotation*(Vector3)Fs20Tuning.Field(typeof(AeroPart),"centerOfLift").GetValue(p):marker.WorldForcePoint;
                pitchCoefficients[i]=Fs20TControlCalibration.Derivative(root,parameters,p,c,visible.transform.rotation,point-center,true);
            }
        }
        internal static float CoordinatedRate(Vector3 flow,Vector3 angular,Vector3 gravity)
        {
            // Exact x component of dv_body/dt = a_body - omega x v_body,
            // evaluated at beta=0. Roll at positive AoA requires anticipatory yaw.
            return (gravity.x+angular.z*flow.y)/Mathf.Max(35f,flow.z);
        }
        internal void Dynamics(Rigidbody reference,out Vector3 center,out float inertia)
        {
            bodies.Clear();
            if(parts!=null)foreach(var part in parts){if(Fs20NARecon.Lost(part))continue;var rb=part.rb;if(rb!=null&&!rb.isKinematic&&!bodies.Contains(rb))bodies.Add(rb);}
            if(bodies.Count==0)bodies.Add(reference);
            float mass=0f;center=Vector3.zero;
            foreach(var rb in bodies){mass+=rb.mass;center+=rb.worldCenterOfMass*rb.mass;}
            center/=Mathf.Max(1f,mass);inertia=0f;var up=reference.rotation*Vector3.up;var momentum=Vector3.zero;var omega=reference.angularVelocity;
            foreach(var rb in bodies){
                var rotation=rb.rotation*rb.inertiaTensorRotation;var inverse=Quaternion.Inverse(rotation);
                var axis=inverse*up;var tensor=rb.inertiaTensor;
                var arm=rb.worldCenterOfMass-center;
                inertia+=tensor.x*axis.x*axis.x+tensor.y*axis.y*axis.y+tensor.z*axis.z*axis.z+rb.mass*Vector3.Cross(arm,up).sqrMagnitude;
                momentum+=rotation*Vector3.Scale(tensor,inverse*omega)+rb.mass*Vector3.Cross(arm,Vector3.Cross(omega,arm));
            }
            gyroscopicTorque=Vector3.Dot(Vector3.Cross(omega,momentum),up);
            pitchGyroscopicTorque=Vector3.Dot(Vector3.Cross(omega,momentum),reference.rotation*Vector3.right);
        }
        private static float Acceleration(float beta,float rate,float coordinated,float pilot,float frequency)
        {
            float desiredRate=coordinated+Mathf.Clamp(pilot,-1f,1f)*.65f;
            return frequency*frequency*beta*(1f-.75f*Mathf.Abs(pilot))+2.2f*frequency*(desiredRate-rate);
        }
        internal static float Demand(float beta,float rate,float coordinated,float pilot,float inertia,float authority,float previous,float dt,float frequency)
        {
            if(!(authority>1f)||!(inertia>1f))return 0f;
            float accel=Acceleration(beta,rate,coordinated,pilot,frequency);
            float target=Mathf.Clamp(accel*inertia/authority,-1f,1f);
            return Mathf.MoveTowards(previous,target,Mathf.Max(0f,dt)*3f);
        }
        internal float Authority(Rigidbody body,Vector3 center,Vector3 relativeVelocity,float density,float sign)
        {
            float result=0f,speed=relativeVelocity.magnitude;var direction=relativeVelocity.normalized;
            foreach(var split in splits) {
                if(split==null||Fs20NARecon.Lost(split.GetComponent<UnitPart>())||Side(split)*sign<=0f)continue;
                var marker=split.GetComponent<Fs20TPartPhysics>();
                var point=marker==null?split.transform.position:marker.WorldForcePoint;
                var torque=Vector3.Cross(point-center,-direction*(MaxSplit(split)*Drag(split)*density*speed*speed));
                result+=Mathf.Max(0f,Vector3.Dot(torque,body.rotation*Vector3.up)*sign);
            }
            var enginePart=nozzle==null?null:nozzle.GetComponentInParent<UnitPart>(true);
            if(engine!=null&&nozzle!=null&&!Fs20NARecon.Lost(enginePart)&&speed<=VectorLimit(engine)) {
                float arm=Mathf.Max(0f,-Vector3.Dot((thrustPoint==null?nozzle.transform.position:thrustPoint.position)-center,body.rotation*Vector3.forward));
                result+=Mathf.Max(0f,nozzle.GetTotalThrust())*Mathf.Sin(Mathf.Abs(Vectoring(engine).y)*Mathf.Deg2Rad)*arm;
            }
            return result;
        }
        internal void Apply(ControlsFilter filter,ControlInputs inputs,Vector3 raw,Rigidbody body,bool assisted,float dt)
        {
            if(aircraft==null||body==null)return;
            var relative=body.velocity-aircraft.GetWindVelocity();var flow=Quaternion.Inverse(body.rotation)*relative;
            SideslipDegrees=Mathf.Atan2(flow.x,Mathf.Max(1f,flow.z))*Mathf.Rad2Deg;
            if(!assisted||!filter.GetFlyByWire().Enabled||!(bool)Fs20Tuning.Airborne.GetValue(aircraft)||flow.z<35f){command=inputs.yaw;trim=0f;rateInitialized=false;rateFeedforward=0f;return;}
            float rate=Vector3.Dot(body.angularVelocity,body.rotation*Vector3.up);
            var inverse=Quaternion.Inverse(body.rotation);
            var angular=inverse*body.angularVelocity;
            float coordinated=CoordinatedRate(flow,angular,inverse*Physics.gravity);
            Dynamics(body,out var center,out float inertia);
            // Cancel roll/yaw gyroscopic pitch coupling through the real elevons.
            // Authority is pressure-scaled and excludes lost control surfaces.
            float pitchAuthority=0f;
            if(pitchParts!=null)for(int i=0;i<pitchParts.Length;i++)if(!Fs20NARecon.Lost(pitchParts[i]))pitchAuthority+=pitchCoefficients[i];
            pitchAuthority=Mathf.Abs(pitchAuthority)*.5f*aircraft.airDensity*relative.sqrMagnitude;
            float desiredPitch=pitchAuthority>1f?Mathf.Clamp(pitchGyroscopicTorque/pitchAuthority,-.35f,.35f):0f;
            pitchFeedforward=Mathf.MoveTowards(pitchFeedforward,desiredPitch,Mathf.Max(0f,dt)*3f);
            inputs.pitch=Mathf.Clamp(inputs.pitch+pitchFeedforward,-1f,1f);
            float frequency=Mathf.Clamp(1.15f*relative.magnitude/80f*Mathf.Sqrt(Mathf.Max(.01f,aircraft.airDensity)/1.167f),.8f,9f);
            float beta=SideslipDegrees*Mathf.Deg2Rad;
            float desiredRate=coordinated+Mathf.Clamp(raw.y,-1f,1f)*.65f;
            if(!rateInitialized){previousRate=desiredRate;rateFeedforward=0f;rateInitialized=true;}
            float derivative=Mathf.Clamp((desiredRate-previousRate)/Mathf.Max(.001f,dt),-4f,4f);previousRate=desiredRate;
            rateFeedforward=Mathf.Lerp(rateFeedforward,derivative,1f-Mathf.Exp(-Mathf.Max(0f,dt)/.1f));
            if(Mathf.Abs(command)<.95f&&Mathf.Abs(raw.y)<.1f&&Mathf.Abs(angular.z)<.1f&&Mathf.Abs(beta)<5f*Mathf.Deg2Rad)trim=Mathf.Clamp(trim+beta*dt,-.15f,.15f);
            else trim*=Mathf.Exp(-Mathf.Max(0f,dt)/2f);
            float required=Acceleration(beta,rate,coordinated,raw.y,frequency)+rateFeedforward+.35f*frequency*frequency*trim;
            float torque=required*inertia+gyroscopicTorque;
            float authority=Authority(body,center,relative,aircraft.airDensity,torque>=0f?1f:-1f);
            float target=authority>1f?Mathf.Clamp(torque/authority,-1f,1f):0f;
            command=Mathf.MoveTowards(command,target,Mathf.Max(0f,dt)*3f);
            inputs.yaw=command;
            if(!reported){reported=true;VortexConventionalPlugin.ReportAssetState($"FS20NA FBW yaw bound: bodies={bodies.Count}, Iy={inertia:F0}, beta={SideslipDegrees:F2}, native split/nozzle allocation active.");}
        }
        [HarmonyPatch(typeof(ControlsFilter.FlyByWire),"Filter")]
        internal static class FilterYaw
        {
            private static void Prefix(ControlInputs __1,out Vector3 __state)=>__state=new Vector3(__1.pitch,__1.yaw,__1.roll);
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Aircraft __0,ControlInputs __1,bool __3,Vector3 __state)
            {
                if(__0==null)return;var controller=__0.GetComponent<Fs20NAYawController>();
                if(controller!=null&&Controls(__0)!=null)controller.Apply(Controls(__0),__1,__state,__0.CockpitRB(),__3,Time.fixedDeltaTime);
            }
        }
    }
}
